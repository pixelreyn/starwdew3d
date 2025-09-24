using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley3D.Editor;
using StardewValley3D.Rendering;
using StardewValley3D.StardewInterfaces;
using StardewValley3D.Systems;
using HarmonyLib;
using StardewValley3D.Diagnostics;
using StardewValley3D.Structures;

namespace StardewValley3D
{
    internal sealed class ModEntry : Mod
    {
        private StardewInterfaces.Character? _character;
        private World? _world;
        private MapEditor? _mapEditor;
        private RenderingPipeline? _renderPipeline;
        private LocationAnalyzer? _locationAnalyzer;
        private MonitorHelper _helper;
        
        private bool _3dEnabled = true;
        private bool _editorMode = false;
        private bool _needsRegeneration = false;
        private bool _showDebugInfo = false;
        
        // Performance stats
        private int _frameCounter = 0;
        private double _frameTime = 0;
        private double _averageFps = 60;
        
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Player.Warped += OnWarped;
            helper.Events.GameLoop.UpdateTicking += OnUpdateTicking;
            helper.Events.Display.RenderedWorld += OnRenderedWorld;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Display.Rendered += OnRendered;
            
            var harmony = new Harmony(this.ModManifest.UniqueID);
            
            // Initialize diagnostic tools
            _locationAnalyzer = new LocationAnalyzer(helper);
            
            // Dynamic world change events
            helper.Events.World.ObjectListChanged += OnObjectListChanged;
            helper.Events.World.TerrainFeatureListChanged += OnTerrainFeatureListChanged;
            helper.Events.World.DebrisListChanged += OnDebrisListChanged;
            helper.Events.World.LargeTerrainFeatureListChanged += OnLargeTerrainFeatureListChanged;
            helper.Events.World.FurnitureListChanged += OnFurnitureListChanged;
            helper.Events.World.BuildingListChanged += OnBuildingListChanged;
            
            _helper = new MonitorHelper(Helper, Monitor);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            Monitor.Log("Initializing 3D Rendering System with Metadata Support", LogLevel.Info);
    
            var device = Game1.graphics.GraphicsDevice;
    
            // Initialize core systems
            _character = new StardewInterfaces.Character();
            _world = new World(_helper, device);
            _renderPipeline = new RenderingPipeline(device, _helper, _world.TextureManager);
            _mapEditor = new MapEditor(_world, _character.Camera, _helper);
    
            // Connect world updates to rendering pipeline
            _world.OnObjectUpdated += (s, objectId) => 
            {
                _renderPipeline.MarkObjectDirty(objectId);
            };
    
            _world.OnObjectAdded += (s, objectId) => 
            {
                // For new objects, we need to rebuild instances
                // as they might create new batches
                _renderPipeline.InvalidateInstances();
            };
    
            _world.OnObjectRemoved += (s, objectId) => 
            {
                _renderPipeline.RemoveObject(objectId);
            };
    
            _world.OnFullRebuildNeeded += (s, args) => 
            {
                _renderPipeline.InvalidateInstances();
            };
    
            // Subscribe to metadata changes
            _mapEditor.OnWorldChanged += (s, args) => 
            {
                // Map editor changes are handled per-object
            };
    
            // Generate initial world
            RegenerateWorld();
    
            Monitor.Log("3D System initialized with targeted change tracking", LogLevel.Info);
        }

        private void OnUpdateTicking(object? sender, UpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Handle 3D character and world updates only when 3D is enabled
            if (_3dEnabled && _character != null)
            {
                _character.DoInput();
                _character.UpdateCamera();
                _character.DoMovement();

                // Update dynamic objects
                _world?.UpdateDynamicObjects();
            }

            // Handle editor mode regardless of 3D mode
            if (_editorMode && _mapEditor != null)
            {
                _mapEditor.Update();
            }

            // Update dirty meshes (this will trigger individual object updates)
            if (_3dEnabled)
            {
                _world?.UpdateDirtyMeshes(5);

                // Full regeneration only when needed
                if (_needsRegeneration)
                {
                    RegenerateWorld();
                    _needsRegeneration = false;
                }
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!_3dEnabled || _world == null || _character == null || _renderPipeline == null)
                return;
            
            var startTime = Game1.currentGameTime.TotalGameTime.TotalMilliseconds;
            
            // Render the 3D world
            _renderPipeline.Render(_world, _character.Camera);
            
            // Update performance stats
            var renderTime = Game1.currentGameTime.TotalGameTime.TotalMilliseconds - startTime;
            UpdatePerformanceStats(renderTime);
        }
        
        private void OnRendered(object? sender, RenderedEventArgs e)
        {
            if (!_3dEnabled) return;
            
            // Draw editor overlay
            if (_editorMode && _mapEditor != null)
            {
                _mapEditor.RenderOverlay(e.SpriteBatch);
            }
            
            // Draw debug info
            if (_showDebugInfo)
            {
                DrawDebugInfo(e.SpriteBatch);
            }
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            Monitor.Log($"Warped to {e.NewLocation.Name}, regenerating world", LogLevel.Info);
            _needsRegeneration = true;
        }
        
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            switch (e.Button)
            {
                case SButton.F5:
                    // Force regenerate world
                    Game1.addHUDMessage(new HUDMessage("Regenerating 3D world...", 2));
                    RegenerateWorld();
                    break;
                    
                case SButton.F6:
                    // Toggle debug info
                    _showDebugInfo = !_showDebugInfo;
                    break;
                    
                case SButton.F7:
                    // Toggle 3D mode
                    _3dEnabled = !_3dEnabled;
                    Game1.addHUDMessage(new HUDMessage($"3D Mode: {(_3dEnabled ? "ON" : "OFF")}", 2));
                    break;
                    
                case SButton.F8:
                    // Toggle editor mode
                    _editorMode = !_editorMode;
                    Game1.addHUDMessage(new HUDMessage($"3D Editor Mode: {(_editorMode ? "ON" : "OFF")}", 2));
                    if (_editorMode)
                    {
                        Game1.addHUDMessage(new HUDMessage("Use Ctrl+1-5 to change modes", 1));
                    }
                    break;
                    
                case SButton.F9:
                    // Save metadata
                    _world?.MetadataManager.SaveAllMetadata();
                    Game1.addHUDMessage(new HUDMessage("Metadata saved!", 3));
                    break;
                    
                case SButton.F10:
                    // Export current location metadata
                    if (_world != null && Game1.currentLocation != null)
                    {
                        _world.MetadataManager.ExportLocationMetadata(Game1.currentLocation.Name);
                        Game1.addHUDMessage(new HUDMessage($"Exported {Game1.currentLocation.Name} metadata", 3));
                    }
                    break;
                    
                case SButton.F11:
                    // Reload metadata
                    if (_world != null)
                    {
                        _world = new World(_helper, Game1.graphics.GraphicsDevice);
                        _renderPipeline = new RenderingPipeline(Game1.graphics.GraphicsDevice, _helper, _world.TextureManager);
                        RegenerateWorld();
                        Game1.addHUDMessage(new HUDMessage("Metadata reloaded", 2));
                    }
                    break;
                case SButton.F12:
                    // Force render a test cube
                    var effect = new BasicEffect(Game1.graphics.GraphicsDevice)
                    {
                        VertexColorEnabled = true,
                        View = _character.Camera.ViewMatrix,
                        Projection = _character.Camera.ProjectionMatrix,
                        World = Matrix.Identity
                    };
    
                    // This should render a cube at origin
                    // If this doesn't appear, it's a rendering pipeline issue
                    // If it does appear, it's a positioning issue
                    break;
            }
        }
        
        private void RegenerateWorld()
        {
            if (_world == null || !_3dEnabled) return;
    
            var startTime = System.DateTime.Now;
    
            Monitor.Log("Regenerating world with metadata system", LogLevel.Debug);
    
            // Generate the world (this will trigger OnFullRebuildNeeded)
            _world.GenerateMap();
    
            // Prepare rendering data with full rebuild
            _renderPipeline?.PrepareWorldData(_world, true);
    
            var elapsed = (System.DateTime.Now - startTime).TotalMilliseconds;
            Monitor.Log($"World regenerated in {elapsed:F0}ms", LogLevel.Info);
    
            // Show stats
            if (_world.SpatialMap != null)
            {
                var stats = _world.SpatialMap.GetStats();
                Game1.addHUDMessage(new HUDMessage(
                    $"Generated {stats.objectCount} objects in {stats.cellCount} cells", 2));
            }
        }
        
        private void MarkDirty()
        {
            // Debounce world regeneration
            if (!_needsRegeneration && _3dEnabled)
            {
                _needsRegeneration = true;
                Monitor.Log("World marked for regeneration", LogLevel.Debug);
            }
        }
        
        private void OnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            if (!_3dEnabled || _world == null) return;
    
            Monitor.Log($"Objects changed: {e.Added.Count()} added, {e.Removed.Count()} removed", LogLevel.Debug);
            _world.HandleObjectListChanged(e);
        }

        private void OnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
        {
            if (!_3dEnabled || _world == null) return;
    
            Monitor.Log($"Terrain features changed: {e.Added.Count()} added, {e.Removed.Count()} removed", LogLevel.Debug);
            _world.HandleTerrainFeatureListChanged(e);
        }
        
        private void OnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
        {
            if (!_3dEnabled || _world == null) return;
    
            Monitor.Log($"Debris changed: {e.Added.Count()} added, {e.Removed.Count()} removed", LogLevel.Debug);
            _world.HandleDebrisListChanged(e);
        }

        private void OnLargeTerrainFeatureListChanged(object? sender, LargeTerrainFeatureListChangedEventArgs e)
        {
            if (!_3dEnabled || _world == null) return;
    
            Monitor.Log($"Large terrain features changed: {e.Added.Count()} added, {e.Removed.Count()} removed", LogLevel.Debug);
            _world.HandleLargeTerrainFeatureListChanged(e);
        }
        private void OnFurnitureListChanged(object? sender, FurnitureListChangedEventArgs e)
        {
            if (!_3dEnabled || _world == null) return;
    
            Monitor.Log($"Objects changed: {e.Added.Count()} added, {e.Removed.Count()} removed", LogLevel.Debug);
            _world.HandleFurnitureListChanged(e);
        }

        private void OnBuildingListChanged(object? sender, BuildingListChangedEventArgs e)
        {
            if (!_3dEnabled || _world == null) return;
    
            Monitor.Log($"Terrain features changed: {e.Added.Count()} added, {e.Removed.Count()} removed", LogLevel.Debug);
            _world.HandleBuildingListChanged(e);
        }
        
        private void UpdatePerformanceStats(double renderTime)
        {
            _frameCounter++;
            _frameTime += renderTime;
            
            if (_frameCounter >= 60)
            {
                _averageFps = 1000.0 / (_frameTime / _frameCounter);
                _frameCounter = 0;
                _frameTime = 0;
            }
        }
        
        private void DrawDebugInfo(SpriteBatch spriteBatch)
        {
            if (_world == null || _renderPipeline == null) return;
            
            var font = Game1.smallFont;
            var position = new Vector2(10, 100);
            var lineHeight = 20;
            
            // Performance stats
            spriteBatch.DrawString(font, $"FPS: {_averageFps:F0}", position, Color.Yellow);
            position.Y += lineHeight;
            
            // World stats
            var spatialStats = _world.SpatialMap.GetStats();
            spriteBatch.DrawString(font, $"Objects: {spatialStats.objectCount}", position, Color.White);
            position.Y += lineHeight;
            
            spriteBatch.DrawString(font, $"Cells: {spatialStats.cellCount}", position, Color.White);
            position.Y += lineHeight;
            
            spriteBatch.DrawString(font, $"Avg/Cell: {spatialStats.avgObjectsPerCell:F1}", position, Color.White);
            position.Y += lineHeight;
            
            // Rendering stats
            spriteBatch.DrawString(font, $"Visible: {_renderPipeline.ObjectsRendered}", position, Color.White);
            position.Y += lineHeight;
            
            spriteBatch.DrawString(font, $"Draw Calls: {_renderPipeline.DrawCalls}", position, Color.White);
            position.Y += lineHeight;
            
            spriteBatch.DrawString(font, $"Batches: {_renderPipeline.BatchCount}", position, Color.White);
            position.Y += lineHeight;
            
            // Metadata stats
            var metadataCount = _world.MetadataManager.GetAllMetadata().Count;
            spriteBatch.DrawString(font, $"Metadata: {metadataCount}", position, Color.White);
            position.Y += lineHeight;
            
            // Dirty meshes
            spriteBatch.DrawString(font, $"Dirty Meshes: {_world.DirtyMeshCount}", position, Color.White);
            position.Y += lineHeight;
            
            // Camera position
            var camPos = _character?.Camera.Position ?? Vector3.Zero;
            spriteBatch.DrawString(font, $"Camera: ({camPos.X:F0}, {camPos.Y:F0}, {camPos.Z:F0})", 
                position, Color.Gray);
            position.Y += lineHeight;
            
            // Current location
            spriteBatch.DrawString(font, $"Location: {Game1.currentLocation?.Name ?? "Unknown"}", 
                position, Color.Gray);
            
            // Controls help
            position = new Vector2(10, Game1.viewport.Height - 100);
            spriteBatch.DrawString(font, "Controls:", position, Color.Yellow);
            position.Y += lineHeight;
            spriteBatch.DrawString(font, "[F5] Regenerate | [F6] Debug | [F7] 3D Toggle", position, Color.Gray);
            position.Y += lineHeight;
            spriteBatch.DrawString(font, "[F8] Editor | [F9] Save | [F10] Export | [F11] Reload", position, Color.Gray);
        }
    }
}