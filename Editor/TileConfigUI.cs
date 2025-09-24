using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley3D.Editor.UI;
using StardewValley3D.Rendering;
using StardewValley3D.StardewInterfaces;
using StardewValley3D.Structures;
using StardewValley3D.Systems;

namespace StardewValley3D.Editor
{
    /// <summary>
    /// In-game UI for configuring tile metadata
    /// </summary>
    public class TileConfigUI : IClickableMenu
    {
        private MonitorHelper _helper;
        private TileMetadata _metadata;
        private TileMetadataManager _metadataManager;
        private TextureManager _textureManager;
        private MeshObject _meshObject; // Store the mesh object for texture info
        private Action<TileMetadata> _onSave;
        
        // UI elements
        private SimpleUIPanel _panel;
        private SimpleButton _saveButton;
        private SimpleButton _cancelButton;
        private SimpleSlider _splitRatioSlider;
        private SimpleSlider _stackHeightSlider;
        private SimpleDropdown _meshTypeDropdown;
        private SimpleDropdown _stackingModeDropdown;
        private bool _needsRebuild = false;
        
        // Texture info for editor
        private Texture2D _objectTexture;
        private Rectangle _objectSourceRect;
        
        public TileConfigUI(TileMetadata metadata, TileMetadataManager manager, 
            TextureManager textureManager, MonitorHelper helper, 
            MeshObject meshObject, Action<TileMetadata> onSave)
            : base(Game1.uiViewport.Width / 2 - 400, Game1.uiViewport.Height / 2 - 350, 800, 700, true)
        {
            _helper = helper;
            _metadata = metadata;
            _metadataManager = manager;
            _textureManager = textureManager;
            _meshObject = meshObject;
            _onSave = onSave;
            
            // Get texture info from mesh object
            ExtractTextureInfo();
            
            _panel = new SimpleUIPanel("Tile Configuration", 
                new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height));
            BuildUI();
        }
        
        /// <summary>
        /// Extract texture information from the mesh object
        /// </summary>
        private void ExtractTextureInfo()
        {
            if (_meshObject == null) return;
    
            // Simply use the texture info that's already in the WorldObject
            _objectTexture = _textureManager.GetTextureById(_meshObject.WorldObject.TextureId);
            _objectSourceRect = _meshObject.WorldObject.SourceRectangle;
    
            // That's it - the World.Process*** functions already did all the heavy lifting
        }
        
        private void BuildUI()
        {
            int y = 100;
            int leftX = 50;
            int rightX = 420;
            int elementWidth = 300;
            int spacing = 70;
            
            // Title info text
            var infoText = $"Tile: {_metadata.TileSheet} #{_metadata.TileIndex} ({_metadata.Layer})";
            if (!string.IsNullOrEmpty(_metadata.TexturePath))
            {
                infoText += $"\nTexture: {System.IO.Path.GetFileName(_metadata.TexturePath)}";
            }
            
            var infoLabel = new SimpleClickableText(infoText, Color.Gray, () => {})
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + 70, 600, 20),
                IsEnabled = false
            };
            _panel.AddElement(infoLabel);
            
            // Left column
            // Mesh Type dropdown
            _meshTypeDropdown = new SimpleDropdown(
                "Mesh Type",
                Enum.GetNames(typeof(TileMeshType)).ToList(),
                _metadata.MeshType.ToString(),
                value => {
                    _metadata.MeshType = Enum.Parse<TileMeshType>(value);
                    _needsRebuild = true; // Flag for conditional elements
                })
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 30)
            };
            _panel.AddElement(_meshTypeDropdown);
            y += spacing;
            
            // Height Offset slider
            var heightSlider = new SimpleSlider(
                "Height Offset", -2f, 5f, _metadata.HeightOffset,
                value => _metadata.HeightOffset = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 20)
            };
            _panel.AddElement(heightSlider);
            y += spacing;
            
            // Scale slider
            var scaleSlider = new SimpleSlider(
                "Scale", 0.1f, 3f, _metadata.Scale,
                value => _metadata.Scale = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 20)
            };
            _panel.AddElement(scaleSlider);
            y += spacing;
            
            // Split Ratio slider (conditional)
            _splitRatioSlider = new SimpleSlider(
                "Split Ratio", 0.1f, 0.9f, _metadata.SplitRatio,
                value => _metadata.SplitRatio = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 20),
                IsVisible = _metadata.MeshType == TileMeshType.SplitBox
            };
            _panel.AddElement(_splitRatioSlider);
            y += spacing;
            
            // Material Type dropdown
            var materials = new List<string> { "default", "water", "metal", "wood", "stone", "glass", "foliage" };
            var materialDropdown = new SimpleDropdown(
                "Material", materials, _metadata.MaterialType,
                value => _metadata.MaterialType = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 30)
            };
            _panel.AddElement(materialDropdown);
            y += spacing;
            
            // Stacking Mode dropdown
            _stackingModeDropdown = new SimpleDropdown(
                "Stacking Mode",
                Enum.GetNames(typeof(TileStackingMode)).ToList(),
                _metadata.StackingMode.ToString(),
                value => {
                    _metadata.StackingMode = Enum.Parse<TileStackingMode>(value);
                    _needsRebuild = true;
                })
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 30)
            };
            _panel.AddElement(_stackingModeDropdown);
            y += spacing;
            
            // Stack Height slider (conditional)
            _stackHeightSlider = new SimpleSlider(
                "Stack Height", 0.5f, 3f, _metadata.StackHeight,
                value => _metadata.StackHeight = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + leftX, yPositionOnScreen + y, elementWidth, 20),
                IsVisible = _metadata.StackingMode != TileStackingMode.None
            };
            _panel.AddElement(_stackHeightSlider);
            
            // Right column - Checkboxes
            y = 100;
            var checkboxSpacing = 50;
            
            var transparentCheckbox = new SimpleCheckbox(
                "Is Transparent", _metadata.IsTransparent,
                value => _metadata.IsTransparent = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + rightX, yPositionOnScreen + y, 200, 24)
            };
            _panel.AddElement(transparentCheckbox);
            y += checkboxSpacing;
            
            var shadowsCheckbox = new SimpleCheckbox(
                "Casts Shadows", _metadata.CastsShadows,
                value => _metadata.CastsShadows = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + rightX, yPositionOnScreen + y, 200, 24)
            };
            _panel.AddElement(shadowsCheckbox);
            y += checkboxSpacing;
            
            var wallCheckbox = new SimpleCheckbox(
                "Is Wall", _metadata.IsWall,
                value => _metadata.IsWall = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + rightX, yPositionOnScreen + y, 200, 24)
            };
            _panel.AddElement(wallCheckbox);
            y += checkboxSpacing;
            
            var floorCheckbox = new SimpleCheckbox(
                "Is Floor", _metadata.IsFloor,
                value => _metadata.IsFloor = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + rightX, yPositionOnScreen + y, 200, 24)
            };
            _panel.AddElement(floorCheckbox);
            y += checkboxSpacing;
            
            var passableCheckbox = new SimpleCheckbox(
                "Passable", _metadata.Passable,
                value => _metadata.Passable = value)
            {
                Bounds = new Rectangle(xPositionOnScreen + rightX, yPositionOnScreen + y, 200, 24)
            };
            _panel.AddElement(passableCheckbox);
            
            // Notes section (simple text display for now)
            var notesLabel = new SimpleClickableText(
                $"Notes: {_metadata.Notes ?? "None"}", 
                Color.Black, 
                () => {
                    // TODO: Implement text input dialog
                    Game1.addHUDMessage(new HUDMessage("Text editing not implemented yet", 2));
                })
            {
                Bounds = new Rectangle(xPositionOnScreen + rightX, yPositionOnScreen + y + 60, 300, 20)
            };
            _panel.AddElement(notesLabel);
            
            // Save and Cancel buttons
            _saveButton = new SimpleButton("Save", Color.Green, (_) => {
                Save();
                exitThisMenu();
            })
            {
                Bounds = new Rectangle(xPositionOnScreen + width - 120, yPositionOnScreen + height - 60, 100, 40)
            };
            _panel.AddElement(_saveButton);
            
            _cancelButton = new SimpleButton("Cancel", Color.Red, (_) => {
                exitThisMenu();
            })
            {
                Bounds = new Rectangle(xPositionOnScreen + 20, yPositionOnScreen + height - 60, 100, 40)
            };
            _panel.AddElement(_cancelButton);
            
            if (_metadata.MeshType == TileMeshType.Box || _metadata.MeshType == TileMeshType.SplitBox)
            {
                var boxCompositionButton = new SimpleButton("Edit 3D Shape", Color.Blue, (_) => {
                    OpenBoxCompositionEditor();
                })
                {
                    Bounds = new Rectangle(xPositionOnScreen + width/2 - 100, yPositionOnScreen + height - 120, 200, 40)
                };
                _panel.AddElement(boxCompositionButton);
            }
        }
        
        private void OpenBoxCompositionEditor()
        {
            if (_objectTexture == null)
            {
                Game1.addHUDMessage(new HUDMessage("No texture available for editing", 2));
                return;
            }
            
            // Make sure source rectangle is valid
            if (_objectSourceRect.Width == 0 || _objectSourceRect.Height == 0)
            {
                // Set default source rectangle based on texture
                if (_objectTexture != null)
                {
                    _objectSourceRect = new Rectangle(0, 0, 
                        Math.Min(64, _objectTexture.Width), 
                        Math.Min(64, _objectTexture.Height));
                }
            }
            
            var editor = new BoxCompositionEditor(_metadata, _objectTexture, _objectSourceRect, 
                _helper, 
                (updatedMetadata) => {
                    _metadata = updatedMetadata;
                    _onSave?.Invoke(_metadata);
                });
            
            Game1.activeClickableMenu = editor;
        }
        
        private void UpdateConditionalElements()
        {
            if (!_needsRebuild) return;
            
            // Update Split Ratio visibility
            _splitRatioSlider.IsVisible = _metadata.MeshType == TileMeshType.SplitBox;
            
            // Update Stack Height visibility
            _stackHeightSlider.IsVisible = _metadata.StackingMode != TileStackingMode.None;
            
            _needsRebuild = false;
        }
        
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);
            _panel.HandleClick(x, y);
        }
        
        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);
            _panel.HandleDrag(x, y);
        }
        
        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            _panel.HandleRelease();
        }
        
        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            _panel.UpdateHover(x, y);
        }
        
        public override void update(GameTime time)
        {
            base.update(time);
            UpdateConditionalElements();
        }
        
        public override void draw(SpriteBatch b)
        {
            // The panel handles all drawing including background
            _panel.Draw(b);
            
            // Draw cursor
            drawMouse(b);
        }
        
        private void Save()
        {
            _metadataManager.UpdateMetadata(_metadata);
            _onSave?.Invoke(_metadata);
            Game1.addHUDMessage(new HUDMessage("Tile configuration saved!", 3));
        }
    }
    
    /// <summary>
    /// Quick access tile configurator for the map editor
    /// </summary>
    public class QuickTileConfig
    {
        private TileMetadataManager _metadataManager;
        private TextureManager _textureManager;
        private MonitorHelper _helper;
        private bool _isConfiguring = false;
        private TileMetadata _currentMetadata;
    
        public QuickTileConfig(TileMetadataManager metadataManager, TextureManager textureManager, MonitorHelper helper)
        {
            _metadataManager = metadataManager;
            _textureManager = textureManager;
            _helper = helper;
        }
    
        public void StartConfiguration(int tileX, int tileY, string layer, string tileSheet, int tileIndex, MeshObject meshObj = null)
        {
            var tileId = $"{Game1.currentLocation.Name}_{tileX}_{tileY}_{layer}";
            _currentMetadata = _metadataManager.GetOrCreateMetadata(tileId, tileSheet, tileIndex, layer);
        
            // Open configuration UI with proper texture info
            var ui = new TileConfigUI(_currentMetadata, _metadataManager, _textureManager, _helper, meshObj, OnConfigSaved);
            Game1.activeClickableMenu = ui;
            _isConfiguring = true;
        }
        
        private void OnConfigSaved(TileMetadata metadata)
        {
            _isConfiguring = false;
            _helper.Monitor.Log($"Saved configuration for tile {metadata.TileId}", LogLevel.Info);
        }
        
        public void HandleHotkeys()
        {
            var input = _helper.Input;
            
            // Quick property toggles
            if (input.IsDown(SButton.LeftControl))
            {
                if (input.GetState(SButton.T) == SButtonState.Pressed && _currentMetadata != null)
                {
                    _currentMetadata.IsTransparent = !_currentMetadata.IsTransparent;
                    _metadataManager.UpdateMetadata(_currentMetadata);
                    Game1.addHUDMessage(new HUDMessage($"Transparency: {_currentMetadata.IsTransparent}", 1));
                }
                
                if (input.GetState(SButton.W) == SButtonState.Pressed && _currentMetadata != null)
                {
                    _currentMetadata.IsWall = !_currentMetadata.IsWall;
                    _metadataManager.UpdateMetadata(_currentMetadata);
                    Game1.addHUDMessage(new HUDMessage($"Is Wall: {_currentMetadata.IsWall}", 1));
                }
                
                if (input.GetState(SButton.M) == SButtonState.Pressed && _currentMetadata != null)
                {
                    // Cycle through mesh types
                    var meshTypes = Enum.GetValues<TileMeshType>();
                    var currentIndex = Array.IndexOf(meshTypes, _currentMetadata.MeshType);
                    var nextIndex = (currentIndex + 1) % meshTypes.Length;
                    _currentMetadata.MeshType = meshTypes[nextIndex];
                    _metadataManager.UpdateMetadata(_currentMetadata);
                    Game1.addHUDMessage(new HUDMessage($"Mesh Type: {_currentMetadata.MeshType}", 1));
                }
            }
        }
    }
}