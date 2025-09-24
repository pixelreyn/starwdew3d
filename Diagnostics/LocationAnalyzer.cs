using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.TerrainFeatures;
using xTile.Layers;
using xTile.Tiles;

namespace StardewValley3D.Diagnostics
{
    public class LocationAnalyzer
    {
        private IModHelper _helper;
        private List<DrawCall> _drawCalls = new();
        private bool _isCapturing = false;
        private GameLocation _currentLocation;
        
        public struct DrawCall
        {
            public string Type;
            public Vector2 Position;
            public float Depth;
            public string LayerName;
            public int TileIndex;
            public string TextureSource;
            public Rectangle SourceRect;
            public string Properties;
        }
        
        public LocationAnalyzer(IModHelper helper)
        {
            _helper = helper;
        }
        
        public void CaptureLocationData(GameLocation location)
        {
            _currentLocation = location;
            var sb = new StringBuilder();
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var filename = $"location_analysis_{location.Name}_{timestamp}.txt";
            
            sb.AppendLine($"=== Location Analysis: {location.Name} ===");
            sb.AppendLine($"Map Size: {location.map.Layers[0].LayerWidth}x{location.map.Layers[0].LayerHeight}");
            sb.AppendLine($"Is Outdoor: {location.IsOutdoors}");
            sb.AppendLine($"Is Farm: {location.IsFarm}");
            sb.AppendLine();
            
            // Analyze map layers
            sb.AppendLine("=== MAP LAYERS ===");
            foreach (Layer layer in location.map.Layers)
            {
                sb.AppendLine($"\nLayer: {layer.Id}");
                sb.AppendLine($"  Visible: {layer.Visible}");
                sb.AppendLine($"  Properties: {string.Join(", ", layer.Properties.Select(p => $"{p.Key}={p.Value}"))}");
                
                // Sample tiles from this layer
                var tileSamples = new Dictionary<int, List<(int x, int y)>>();
                for (int x = 0; x < Math.Min(layer.LayerWidth, 50); x++)
                {
                    for (int y = 0; y < Math.Min(layer.LayerHeight, 50); y++)
                    {
                        var tile = layer.Tiles[x, y];
                        if (tile != null && tile.TileIndex > 0)
                        {
                            if (!tileSamples.ContainsKey(tile.TileIndex))
                                tileSamples[tile.TileIndex] = new List<(int, int)>();
                            tileSamples[tile.TileIndex].Add((x, y));
                        }
                    }
                }
                
                sb.AppendLine($"  Unique tiles: {tileSamples.Count}");
                foreach (var kvp in tileSamples.Take(10))
                {
                    sb.AppendLine($"    TileIndex {kvp.Key}: {kvp.Value.Count} instances (first at {kvp.Value[0].x},{kvp.Value[0].y})");
                }
            }
            
            // Analyze rendering order by intercepting draw calls
            sb.AppendLine("\n=== RENDERING ORDER (Y-sorted) ===");
            var renderOrder = AnalyzeRenderOrder(location);
            foreach (var item in renderOrder.Take(100))
            {
                sb.AppendLine($"  Y={item.Position.Y:F0}, Depth={item.Depth:F4}: {item.Type} at ({item.Position.X:F0},{item.Position.Y:F0})");
            }
            
            // Analyze height indicators
            sb.AppendLine("\n=== HEIGHT INDICATORS ===");
            var heightIndicators = FindHeightIndicators(location);
            foreach (var indicator in heightIndicators)
            {
                sb.AppendLine($"  {indicator}");
            }
            
            // Analyze terrain features
            sb.AppendLine("\n=== TERRAIN FEATURES ===");
            var terrainGroups = location.terrainFeatures.Pairs
                .GroupBy(p => p.Value.GetType().Name)
                .Select(g => new { Type = g.Key, Count = g.Count() });
            foreach (var group in terrainGroups)
            {
                sb.AppendLine($"  {group.Type}: {group.Count}");
            }
            
            // Analyze buildings
            if (location.buildings?.Count > 0)
            {
                sb.AppendLine("\n=== BUILDINGS ===");
                foreach (var building in location.buildings)
                {
                    sb.AppendLine($"  {building.buildingType.Value} at ({building.tileX.Value},{building.tileY.Value})");
                    sb.AppendLine($"    Size: {building.tilesWide.Value}x{building.tilesHigh.Value}");
                    sb.AppendLine($"    Human door: ({building.humanDoor.Value.X},{building.humanDoor.Value.Y})");
                }
            }
            
            // Special location features
            sb.AppendLine("\n=== SPECIAL FEATURES ===");
            if (location.waterTiles != null)
            {
                int waterCount = 0;
                for (int x = 0; x < location.map.Layers[0].LayerWidth; x++)
                    for (int y = 0; y < location.map.Layers[0].LayerHeight; y++)
                        if (location.isWaterTile(x, y)) waterCount++;
                sb.AppendLine($"  Water tiles: {waterCount}");
            }
            
            // Analyze tile properties that might indicate height
            sb.AppendLine("\n=== TILE PROPERTIES (height-related) ===");
            var heightProperties = AnalyzeTileProperties(location);
            foreach (var prop in heightProperties)
            {
                sb.AppendLine($"  {prop}");
            }
            
            // Write to file
            var outputPath = Path.Combine(_helper.DirectoryPath, "analysis", filename);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, sb.ToString());
            
            Game1.addHUDMessage(new HUDMessage($"Location analysis saved to: {filename}", 2));
        }
        
        private List<DrawCall> AnalyzeRenderOrder(GameLocation location)
        {
            var calls = new List<DrawCall>();
            
            // Simulate the drawing order used by Stardew Valley
            // Back layer tiles
            AddLayerTiles(calls, location, "Back", 0f);
            
            // Buildings layer
            AddLayerTiles(calls, location, "Buildings", 0.1f);
            
            // Objects and characters (Y-sorted)
            foreach (var kvp in location.Objects.Pairs)
            {
                calls.Add(new DrawCall
                {
                    Type = "Object",
                    Position = kvp.Key * 64,
                    Depth = (kvp.Key.Y * 64 + 32) / 10000f,
                    Properties = kvp.Value.DisplayName
                });
            }
            
            foreach (var character in location.characters)
            {
                calls.Add(new DrawCall
                {
                    Type = "NPC",
                    Position = character.Position,
                    Depth = character.Position.Y / 10000f,
                    Properties = character.Name
                });
            }
            
            // Front layer
            AddLayerTiles(calls, location, "Front", 0.4f);
            
            // AlwaysFront layer
            AddLayerTiles(calls, location, "AlwaysFront", 0.9f);
            
            return calls.OrderBy(c => c.Depth).ToList();
        }
        
        private void AddLayerTiles(List<DrawCall> calls, GameLocation location, string layerName, float baseDepth)
        {
            var layer = location.map.GetLayer(layerName);
            if (layer == null) return;
            
            for (int y = 0; y < Math.Min(layer.LayerHeight, 20); y++)
            {
                for (int x = 0; x < Math.Min(layer.LayerWidth, 20); x++)
                {
                    var tile = layer.Tiles[x, y];
                    if (tile != null && tile.TileIndex > 0)
                    {
                        float depth = baseDepth;
                        if (layerName == "Front" || layerName == "Buildings")
                        {
                            // Y-sorted layers
                            depth = (y * 64) / 10000f + baseDepth;
                        }
                        
                        calls.Add(new DrawCall
                        {
                            Type = $"Tile_{layerName}",
                            Position = new Vector2(x * 64, y * 64),
                            Depth = depth,
                            LayerName = layerName,
                            TileIndex = tile.TileIndex,
                            Properties = string.Join(";", tile.Properties.Select(p => $"{p.Key}={p.Value}"))
                        });
                    }
                }
            }
        }
        
        private List<string> FindHeightIndicators(GameLocation location)
        {
            var indicators = new List<string>();
            
            // Look for cliff tiles
            var cliffIndices = new[] { 165, 166, 167, 160, 161, 162, 163 };
            foreach (var layer in location.map.Layers)
            {
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    for (int x = 0; x < layer.LayerWidth; x++)
                    {
                        var tile = layer.Tiles[x, y];
                        if (tile != null && cliffIndices.Contains(tile.TileIndex))
                        {
                            indicators.Add($"Cliff tile {tile.TileIndex} at ({x},{y}) on layer {layer.Id}");
                        }
                    }
                }
            }
            
            // Look for bridges/stairs
            var bridgeIndices = new[] { 32, 33, 34, 35, 36 }; // Common bridge/stair tiles
            foreach (var layer in location.map.Layers)
            {
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    for (int x = 0; x < layer.LayerWidth; x++)
                    {
                        var tile = layer.Tiles[x, y];
                        if (tile != null && bridgeIndices.Contains(tile.TileIndex))
                        {
                            indicators.Add($"Bridge/Stair tile {tile.TileIndex} at ({x},{y}) on layer {layer.Id}");
                        }
                    }
                }
            }
            
            return indicators;
        }
        
        private List<string> AnalyzeTileProperties(GameLocation location)
        {
            var properties = new List<string>();
            var propertyCount = new Dictionary<string, int>();
            
            foreach (var layer in location.map.Layers)
            {
                for (int y = 0; y < Math.Min(layer.LayerHeight, 30); y++)
                {
                    for (int x = 0; x < Math.Min(layer.LayerWidth, 30); x++)
                    {
                        var tile = layer.Tiles[x, y];
                        if (tile?.Properties != null)
                        {
                            foreach (var prop in tile.Properties)
                            {
                                string key = prop.Key.ToString();
                                if (!propertyCount.ContainsKey(key))
                                    propertyCount[key] = 0;
                                propertyCount[key]++;
                                
                                // Log interesting properties
                                if (key.Contains("Height") || key.Contains("Level") || 
                                    key.Contains("Passable") || key.Contains("Shadow") ||
                                    key.Contains("Draw") || key.Contains("Layer"))
                                {
                                    properties.Add($"Tile at ({x},{y}) layer {layer.Id}: {key}={prop.Value}");
                                }
                            }
                        }
                    }
                }
            }
            
            properties.Insert(0, "Property counts: " + string.Join(", ", 
                propertyCount.OrderByDescending(p => p.Value).Take(10)
                .Select(p => $"{p.Key}({p.Value})")));
            
            return properties;
        }
        
        public void DumpRenderingFrame(SpriteBatch spriteBatch)
        {
            // This could be called from a patched Draw method to capture actual draw calls
            // Would require Harmony patching of the game's draw methods
            if (!_isCapturing) return;
            
            // Log draw call information
            // This would need to be hooked into the actual rendering
        }
    }
}