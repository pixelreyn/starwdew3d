using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley3D.Rendering;
using StardewValley3D.StardewInterfaces;
using StardewValley3D.Structures;
using StardewValley3D.Systems;

namespace StardewValley3D.Editor
{
    /// <summary>
    /// Updated MapEditor that works with the metadata system
    /// </summary>
    public class MapEditor
    {
        private World _world;
        private Camera3D _camera;
        private MonitorHelper _helper;
        private QuickTileConfig _tileConfig;
        
        // Editor state
        private (int x, int y)? _selectedTile;
        private MeshObject _hoveredObject;
        private bool _showGrid = true;
        private bool _showMetadata = false;
        private bool _is2DMode = false;
        private KeyboardState _previousKeyState;
        private MouseState _previousMouseState;
        
        // Editor modes
        private enum EditorMode
        {
            Select,
            Configure,
            Paint,
            Height,
            Stack
        }
        private EditorMode _currentMode = EditorMode.Select;
        
        // Paint mode settings
        private TileMetadata _paintBrush;
        
        public event EventHandler OnWorldChanged;
        
        public MapEditor(World world, Camera3D camera, MonitorHelper helper)
        {
            _world = world;
            _camera = camera;
            _helper = helper;
            _tileConfig = new QuickTileConfig(world.MetadataManager, _world.TextureManager, helper);
            
            // Subscribe to metadata changes
            world.MetadataManager.MetadataChanged += OnMetadataChanged;
        }
        
        public void Update()
        {
            
            if (Game1.activeClickableMenu != null)
                return;
            
            var keyState = Keyboard.GetState();
            var mouseState = Mouse.GetState();
            
            // Mode switching
            HandleModeSwitch(keyState);
            
            // Get tile under cursor
            UpdateSelection(mouseState);
            
            // Handle mode-specific input
            switch (_currentMode)
            {
                case EditorMode.Select:
                    HandleSelectMode(keyState, mouseState);
                    break;
                case EditorMode.Configure:
                    HandleConfigureMode(keyState, mouseState);
                    break;
                case EditorMode.Paint:
                    HandlePaintMode(keyState, mouseState);
                    break;
                case EditorMode.Height:
                    HandleHeightMode(keyState, mouseState);
                    break;
                case EditorMode.Stack:
                    HandleStackMode(keyState, mouseState);
                    break;
            }
            
            // Global hotkeys
            HandleGlobalHotkeys(keyState);
            
            // Handle tile configuration hotkeys
            _tileConfig.HandleHotkeys();
            
            _previousKeyState = keyState;
            _previousMouseState = mouseState;
        }
        
        private void HandleModeSwitch(KeyboardState keyState)
        {
            if (!keyState.IsKeyDown(Keys.LeftControl))
                return;
            
            if (WasKeyPressed(Keys.D1, keyState))
            {
                _currentMode = EditorMode.Select;
                Game1.addHUDMessage(new HUDMessage("Mode: Select", 1));
            }
            else if (WasKeyPressed(Keys.D2, keyState))
            {
                _currentMode = EditorMode.Configure;
                Game1.addHUDMessage(new HUDMessage("Mode: Configure", 1));
            }
            else if (WasKeyPressed(Keys.D3, keyState))
            {
                _currentMode = EditorMode.Paint;
                Game1.addHUDMessage(new HUDMessage("Mode: Paint", 1));
            }
            else if (WasKeyPressed(Keys.D4, keyState))
            {
                _currentMode = EditorMode.Height;
                Game1.addHUDMessage(new HUDMessage("Mode: Height", 1));
            }
            else if (WasKeyPressed(Keys.D5, keyState))
            {
                _currentMode = EditorMode.Stack;
                Game1.addHUDMessage(new HUDMessage("Mode: Stack", 1));
            }
        }
        
        private void HandleSelectMode(KeyboardState keyState, MouseState mouseState)
        {
            if (_hoveredObject == null)
                return;
            
            // Left click to select
            if (WasLeftClicked(mouseState))
            {
                _helper.Monitor.Log($"Selected: {_hoveredObject.Id}", LogLevel.Info);
                
                // Show quick info
                if (_hoveredObject.Metadata != null)
                {
                    var meta = _hoveredObject.Metadata;
                    Game1.addHUDMessage(new HUDMessage(
                        $"Mesh: {meta.MeshType}, Material: {meta.MaterialType}", 2));
                }
            }
            
            // Right click for context menu
            if (WasRightClicked(mouseState))
            {
                OpenContextMenu(_hoveredObject);
            }
        }
        
        private void HandleConfigureMode(KeyboardState keyState, MouseState mouseState)
        {
            if (_hoveredObject == null)
                return;
    
            // Left click to open configuration UI
            if (WasLeftClicked(mouseState))
            {
                OpenTileConfiguration(_hoveredObject);
            }
    
            // Quick property toggles with keyboard
            if (keyState.IsKeyDown(Keys.LeftShift))
            {
                var metadata = _hoveredObject.Metadata;
                if (metadata == null) return;
        
                bool changed = false;
        
                // Quick adjustments
                if (WasKeyPressed(Keys.Q, keyState))
                {
                    metadata.HeightOffset -= 0.1f;
                    changed = true;
                }
                else if (WasKeyPressed(Keys.E, keyState))
                {
                    metadata.HeightOffset += 0.1f;
                    changed = true;
                }
                else if (WasKeyPressed(Keys.R, keyState))
                {
                    metadata.Scale = Math.Max(0.1f, metadata.Scale - 0.1f);
                    changed = true;
                }
                else if (WasKeyPressed(Keys.T, keyState))
                {
                    metadata.Scale = Math.Min(3f, metadata.Scale + 0.1f);
                    changed = true;
                }
        
                if (changed)
                {
                    UpdateTileMetadata(metadata);
                    Game1.addHUDMessage(new HUDMessage($"Updated: H={metadata.HeightOffset:F1} S={metadata.Scale:F1}", 1));
                }
            }
        }
        
        private void HandlePaintMode(KeyboardState keyState, MouseState mouseState)
        {
            // Pick brush with right click
            if (WasRightClicked(mouseState) && _hoveredObject?.Metadata != null)
            {
                _paintBrush = _hoveredObject.Metadata;
                Game1.addHUDMessage(new HUDMessage($"Brush: {_paintBrush.MeshType}", 1));
            }
            
            // Paint with left click/drag
            if (mouseState.LeftButton == ButtonState.Pressed && _paintBrush != null && _hoveredObject != null)
            {
                // Apply brush metadata to hovered object
                var targetMeta = _hoveredObject.Metadata;
                if (targetMeta != null)
                {
                    // Copy paint properties
                    targetMeta.MeshType = _paintBrush.MeshType;
                    targetMeta.MaterialType = _paintBrush.MaterialType;
                    targetMeta.HeightOffset = _paintBrush.HeightOffset;
                    targetMeta.Scale = _paintBrush.Scale;
                    targetMeta.IsTransparent = _paintBrush.IsTransparent;
                    targetMeta.SplitRatio = _paintBrush.SplitRatio;
                    
                    UpdateTileMetadata(targetMeta);
                }
            }
        }
        
        private void HandleHeightMode(KeyboardState keyState, MouseState mouseState)
        {
            if (_hoveredObject == null)
                return;
            
            var metadata = _hoveredObject.Metadata;
            if (metadata == null) return;
            
            // Adjust height with scroll wheel
            int scrollDelta = mouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
            if (scrollDelta != 0)
            {
                float adjustment = scrollDelta > 0 ? 0.1f : -0.1f;
                metadata.HeightOffset += adjustment;
                UpdateTileMetadata(metadata);
                Game1.addHUDMessage(new HUDMessage($"Height: {metadata.HeightOffset:F1}", 1));
            }
            
            // Set specific heights with number keys
            for (int i = 0; i <= 9; i++)
            {
                if (WasKeyPressed(Keys.D0 + i, keyState))
                {
                    metadata.HeightOffset = i * 0.5f;
                    UpdateTileMetadata(metadata);
                }
            }
        }
        
        private void HandleStackMode(KeyboardState keyState, MouseState mouseState)
        {
            if (_hoveredObject == null)
                return;
            
            var metadata = _hoveredObject.Metadata;
            if (metadata == null) return;
            
            // Toggle stacking mode
            if (WasKeyPressed(Keys.S, keyState))
            {
                var modes = Enum.GetValues<TileStackingMode>();
                var currentIndex = Array.IndexOf(modes, metadata.StackingMode);
                metadata.StackingMode = modes[(currentIndex + 1) % modes.Length];
                UpdateTileMetadata(metadata);
                Game1.addHUDMessage(new HUDMessage($"Stacking: {metadata.StackingMode}", 1));
            }
            
            // Add/remove stacked tiles
            if (metadata.StackingMode != TileStackingMode.None)
            {
                if (WasKeyPressed(Keys.Add, keyState))
                {
                    // Add a stacked tile
                    if (metadata.StackedTileIndices == null)
                        metadata.StackedTileIndices = new List<int>();
                    
                    metadata.StackedTileIndices.Add(metadata.TileIndex);
                    UpdateTileMetadata(metadata);
                }
                else if (WasKeyPressed(Keys.Subtract, keyState) && metadata.StackedTileIndices?.Count > 0)
                {
                    // Remove last stacked tile
                    metadata.StackedTileIndices.RemoveAt(metadata.StackedTileIndices.Count - 1);
                    UpdateTileMetadata(metadata);
                }
            }
        }
        
        private void HandleGlobalHotkeys(KeyboardState keyState)
        {
            // Toggle 2D/3D view
            if (WasKeyPressed(Keys.Tab, keyState))
            {
                _is2DMode = !_is2DMode;
                _camera.EditorMode = _is2DMode; // Enable free mouse in 2D mode
            
                if (_is2DMode)
                {
                    // Set camera to top-down view for easier editing
                    // You might want to store the previous camera position/rotation
                    _camera.Position = new Vector3(_camera.Position.X, 500, _camera.Position.Z);
                    // Set pitch to look straight down
                    // Would need to expose these or add a method to Camera3D
                }
            
                Game1.addHUDMessage(new HUDMessage(_is2DMode ? "2D Mode" : "3D Mode", 1));
            }
            
            // Toggle grid
            if (WasKeyPressed(Keys.G, keyState))
            {
                _showGrid = !_showGrid;
            }
            
            // Toggle metadata overlay
            if (WasKeyPressed(Keys.M, keyState))
            {
                _showMetadata = !_showMetadata;
            }
            
            // Save all metadata
            if (keyState.IsKeyDown(Keys.LeftControl) && WasKeyPressed(Keys.S, keyState))
            {
                _world.MetadataManager.SaveAllMetadata();
                Game1.addHUDMessage(new HUDMessage("Metadata saved!", 3));
            }
            
            // Export current location metadata
            if (keyState.IsKeyDown(Keys.LeftControl) && WasKeyPressed(Keys.E, keyState))
            {
                _world.MetadataManager.ExportLocationMetadata(Game1.currentLocation.Name);
                Game1.addHUDMessage(new HUDMessage($"Exported {Game1.currentLocation.Name} metadata", 3));
            }
            
            // Refresh world
            if (WasKeyPressed(Keys.F5, keyState))
            {
                _world.GenerateMap();
                Game1.addHUDMessage(new HUDMessage("World refreshed!", 2));
            }
        }
        
        private void UpdateSelection(MouseState mouseState)
        {
            if (!_is2DMode)
                return; // Only allow selection in 2D mode for now
    
            var ray = GetMouseRay(mouseState);
            _hoveredObject = null;
    
            // Get visible objects and check ray intersection
            var visibleObjects = _world.GetVisibleObjects(_camera);
            float closestDistance = float.MaxValue;
    
            foreach (var obj in visibleObjects)
            {
                float? distance = ray.Intersects(obj.Bounds);
                if (distance.HasValue && distance.Value < closestDistance)
                {
                    closestDistance = distance.Value;
                    _hoveredObject = obj;
                }
            }
    
            // Update selected tile from hovered object
            if (_hoveredObject != null)
            {
                int tileX = (int)(_hoveredObject.WorldObject.Position.X / 64);
                int tileY = (int)(_hoveredObject.WorldObject.Position.Z / 64);
                _selectedTile = (tileX, tileY);
            }
        }
        
        private Ray GetMouseRay(MouseState mouseState)
        {
            var viewport = Game1.graphics.GraphicsDevice.Viewport;
            Vector3 nearPoint = viewport.Unproject(
                new Vector3(mouseState.X, mouseState.Y, 0),
                _camera.ProjectionMatrix,
                _camera.ViewMatrix,
                Matrix.Identity
            );
            Vector3 farPoint = viewport.Unproject(
                new Vector3(mouseState.X, mouseState.Y, 1),
                _camera.ProjectionMatrix,
                _camera.ViewMatrix,
                Matrix.Identity
            );
            
            Vector3 direction = Vector3.Normalize(farPoint - nearPoint);
            return new Ray(nearPoint, direction);
        }
        
        private void OpenTileConfiguration(MeshObject meshObj)
        {
            if (meshObj.Metadata == null)
                return;
    
            var ui = new TileConfigUI(
                meshObj.Metadata, 
                _world.MetadataManager,
                _world.TextureManager,  // Pass the texture manager
                _helper,
                meshObj,  // Pass the actual mesh object
                (metadata) => {
                    // Mark the specific object as dirty and update it
                    meshObj.IsDirty = true;
                    _world.UpdateSingleObject(metadata.TileId);
                });
    
            Game1.activeClickableMenu = ui;
        }
        
        private void OpenContextMenu(MeshObject meshObj)
        {
            // Could implement a context menu here
            // For now, just open configuration
            OpenTileConfiguration(meshObj);
        }
        
        private void UpdateTileMetadata(TileMetadata metadata)
        {
            _world.MetadataManager.UpdateMetadata(metadata);
            _world.UpdateSingleObject(metadata.TileId);
        }
        
        private void OnMetadataChanged(object sender, TileMetadata metadata)
        {
            // Metadata was changed, world will handle mesh updates
            _helper.Monitor.Log($"Metadata changed for {metadata.TileId}", LogLevel.Debug);
        }
        
        public void RenderOverlay(SpriteBatch spriteBatch)
        {
            if (!_showGrid && !_showMetadata)
                return;
            
            // Draw mode indicator
            string modeText = $"Mode: {_currentMode}";
            spriteBatch.DrawString(Game1.smallFont, modeText, new Vector2(10, 10), Color.Yellow);
            
            // Draw selected tile info
            if (_selectedTile.HasValue && _hoveredObject != null)
            {
                var (x, y) = _selectedTile.Value;
                string info = $"Tile: ({x}, {y})";
                
                if (_hoveredObject.Metadata != null)
                {
                    var meta = _hoveredObject.Metadata;
                    info += $"\nType: {meta.MeshType}";
                    info += $"\nHeight: {meta.HeightOffset:F1}";
                    info += $"\nMaterial: {meta.MaterialType}";
                }
                
                spriteBatch.DrawString(Game1.smallFont, info, new Vector2(10, 40), Color.White);
            }
            
            // Draw controls
            string controls = "[Ctrl+1-5]: Change Mode | [Tab]: 2D/3D | [G]: Grid | [M]: Metadata";
            spriteBatch.DrawString(Game1.smallFont, controls, 
                new Vector2(10, Game1.viewport.Height - 30), Color.Gray);
            
            // Draw metadata overlay if enabled
            if (_showMetadata && _hoveredObject?.Metadata != null)
            {
                DrawMetadataOverlay(spriteBatch, _hoveredObject.Metadata);
            }
        }
        
        private void DrawMetadataOverlay(SpriteBatch spriteBatch, TileMetadata metadata)
        {
            var mouseState = Mouse.GetState();
            var position = new Vector2(mouseState.X + 20, mouseState.Y);
            
            var lines = new List<string>
            {
                $"ID: {metadata.TileId}",
                $"Sheet: {metadata.TileSheet} #{metadata.TileIndex}",
                $"Mesh: {metadata.MeshType}",
                $"Material: {metadata.MaterialType}",
                $"Height: {metadata.HeightOffset:F2}",
                $"Scale: {metadata.Scale:F2}",
                $"Transparent: {metadata.IsTransparent}",
                $"Shadows: {metadata.CastsShadows}",
                $"Passable: {metadata.Passable}"
            };
            
            // Draw background
            var maxWidth = 0f;
            foreach (var line in lines)
            {
                var size = Game1.smallFont.MeasureString(line);
                maxWidth = Math.Max(maxWidth, size.X);
            }
            
            var bgRect = new Rectangle((int)position.X - 5, (int)position.Y - 5,
                (int)maxWidth + 10, lines.Count * 20 + 10);
            
            // Draw semi-transparent background
            spriteBatch.Draw(Game1.staminaRect, bgRect, Color.Black * 0.8f);
            
            // Draw text
            float yOffset = 0;
            foreach (var line in lines)
            {
                spriteBatch.DrawString(Game1.smallFont, line,
                    position + new Vector2(0, yOffset), Color.White);
                yOffset += 20;
            }
        }
        
        // Helper methods
        private bool WasKeyPressed(Keys key, KeyboardState current)
        {
            return current.IsKeyDown(key) && !_previousKeyState.IsKeyDown(key);
        }
        
        private bool WasLeftClicked(MouseState current)
        {
            return current.LeftButton == ButtonState.Pressed && 
                   _previousMouseState.LeftButton == ButtonState.Released;
        }
        
        private bool WasRightClicked(MouseState current)
        {
            return current.RightButton == ButtonState.Pressed && 
                   _previousMouseState.RightButton == ButtonState.Released;
        }
    }
}