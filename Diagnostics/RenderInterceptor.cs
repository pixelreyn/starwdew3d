using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using xTile.Display;

namespace StardewValley3D.Diagnostics
{
    public class RenderInterceptor
    {
        private static IModHelper _helper;
        private static List<RenderCall> _renderCalls = new();
        private static bool _isRecording = false;
        private static int _frameCount = 0;
        
        public struct RenderCall
        {
            public int Frame;
            public string Method;
            public Vector2 Position;
            public float LayerDepth;
            public string ObjectType;
            public string Details;
            public Rectangle? SourceRect;
            public Texture2D Texture;
        }
        
        public static void Initialize(IModHelper helper, Harmony harmony)
        {
            _helper = helper;
            
            // Patch the main drawing methods
            harmony.Patch(
                AccessTools.Method(typeof(GameLocation), "draw", new[] { typeof(SpriteBatch) }),
                prefix: new HarmonyMethod(typeof(RenderInterceptor), nameof(BeforeLocationDraw)),
                postfix: new HarmonyMethod(typeof(RenderInterceptor), nameof(AfterLocationDraw))
            );
            
            // Patch SpriteBatch.Draw to capture all draw calls
            harmony.Patch(
                AccessTools.Method(typeof(SpriteBatch), "Draw", new[] { 
                    typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), 
                    typeof(Color), typeof(float), typeof(Vector2), 
                    typeof(float), typeof(SpriteEffects), typeof(float) 
                }),
                prefix: new HarmonyMethod(typeof(RenderInterceptor), nameof(InterceptDraw))
            );
        }
        
        public static void StartRecording()
        {
            _isRecording = true;
            _frameCount = 0;
            _renderCalls.Clear();
            Game1.addHUDMessage(new HUDMessage("Started recording render calls", 2));
        }
        
        public static void StopRecording()
        {
            _isRecording = false;
            SaveRecording();
            Game1.addHUDMessage(new HUDMessage($"Saved {_renderCalls.Count} render calls", 2));
        }
        
        private static bool BeforeLocationDraw(GameLocation __instance)
        {
            if (_isRecording)
            {
                _frameCount++;
                _renderCalls.Add(new RenderCall
                {
                    Frame = _frameCount,
                    Method = "GameLocation.draw",
                    ObjectType = "LocationStart",
                    Details = __instance.Name
                });
            }
            return true; // Continue to original method
        }
        
        private static void AfterLocationDraw(GameLocation __instance)
        {
            if (_isRecording)
            {
                _renderCalls.Add(new RenderCall
                {
                    Frame = _frameCount,
                    Method = "GameLocation.draw",
                    ObjectType = "LocationEnd",
                    Details = __instance.Name
                });
            }
        }
        
        private static bool InterceptDraw(
            Texture2D texture,
            Vector2 position,
            Rectangle? sourceRectangle,
            Color color,
            float rotation,
            Vector2 origin,
            float scale,
            SpriteEffects effects,
            float layerDepth)
        {
            if (_isRecording && _renderCalls.Count < 10000) // Limit to prevent memory issues
            {
                // Try to identify what's being drawn
                string objectType = "Unknown";
                string details = "";
                
                if (texture != null)
                {
                    if (texture == Game1.mouseCursors)
                        objectType = "UI";
                    else if (texture == Game1.objectSpriteSheet)
                        objectType = "Object";
                    else if (texture.Name?.Contains("Characters") == true)
                        objectType = "Character";
                    else if (texture.Name?.Contains("TileSheet") == true)
                        objectType = "Tile";
                    else if (texture.Name?.Contains("Buildings") == true)
                        objectType = "Building";
                    
                    details = texture.Name ?? "unnamed";
                }
                
                _renderCalls.Add(new RenderCall
                {
                    Frame = _frameCount,
                    Method = "SpriteBatch.Draw",
                    Position = position,
                    LayerDepth = layerDepth,
                    ObjectType = objectType,
                    Details = details,
                    SourceRect = sourceRectangle,
                    Texture = texture
                });
            }
            
            return true; // Continue to original method
        }
        
        private static void SaveRecording()
        {
            var sb = new System.Text.StringBuilder();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filename = $"render_recording_{timestamp}.csv";
            
            // CSV header
            sb.AppendLine("Frame,Method,PosX,PosY,LayerDepth,ObjectType,Details,SourceRect");
            
            foreach (var call in _renderCalls)
            {
                string sourceRect = call.SourceRect.HasValue 
                    ? $"{call.SourceRect.Value.X};{call.SourceRect.Value.Y};{call.SourceRect.Value.Width};{call.SourceRect.Value.Height}"
                    : "null";
                    
                sb.AppendLine($"{call.Frame},{call.Method},{call.Position.X:F2},{call.Position.Y:F2}," +
                             $"{call.LayerDepth:F6},{call.ObjectType},{call.Details},{sourceRect}");
            }
            
            var outputPath = Path.Combine(_helper.DirectoryPath, "analysis", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, sb.ToString());
            
            // Also save a summary
            SaveRenderingSummary(timestamp);
        }
        
        private static void SaveRenderingSummary(string timestamp)
        {
            var sb = new System.Text.StringBuilder();
            var filename = $"render_summary_{timestamp}.txt";
            
            sb.AppendLine("=== RENDERING SUMMARY ===");
            sb.AppendLine($"Total draw calls: {_renderCalls.Count}");
            sb.AppendLine($"Frames recorded: {_frameCount}");
            sb.AppendLine();
            
            // Group by layer depth
            sb.AppendLine("=== LAYER DEPTH DISTRIBUTION ===");
            var depthGroups = _renderCalls
                .Where(c => c.Method == "SpriteBatch.Draw")
                .GroupBy(c => Math.Round(c.LayerDepth, 3))
                .OrderBy(g => g.Key);
            
            foreach (var group in depthGroups.Take(50))
            {
                var sample = group.First();
                sb.AppendLine($"Depth {group.Key:F3}: {group.Count()} calls - {sample.ObjectType} ({sample.Details})");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== OBJECT TYPE DISTRIBUTION ===");
            var typeGroups = _renderCalls
                .GroupBy(c => c.ObjectType)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in typeGroups)
            {
                sb.AppendLine($"{group.Key}: {group.Count()} calls");
            }
            
            sb.AppendLine();
            sb.AppendLine("=== Y-SORTING ANALYSIS ===");
            var ySorted = _renderCalls
                .Where(c => c.Method == "SpriteBatch.Draw" && c.LayerDepth > 0.001f && c.LayerDepth < 0.999f)
                .OrderBy(c => c.LayerDepth)
                .Take(100);
            
            foreach (var call in ySorted)
            {
                float inferredY = call.LayerDepth * 10000;
                sb.AppendLine($"Depth {call.LayerDepth:F6} -> Y≈{inferredY:F0}: {call.ObjectType} at ({call.Position.X:F0},{call.Position.Y:F0})");
            }
            
            var outputPath = Path.Combine(_helper.DirectoryPath, "analysis", filename);
            File.WriteAllText(outputPath, sb.ToString());
        }
    }
}