// BoxCompositionEditor.cs - Enhanced with UV editing
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
using StardewValley3D.Structures;
using StardewValley3D.Systems;

namespace StardewValley3D.Editor
{
    public class BoxCompositionEditor : IClickableMenu
    {
        private TileMetadata _metadata;
        private Texture2D _spriteTexture;
        private Rectangle _spriteSourceRect;
        private MonitorHelper _helper;
        private Action<TileMetadata> _onSave;

        // UI Panels
        private SimpleUIPanel _mainPanel;
        private SimpleUIPanel _boxListPanel;
        private SimpleUIPanel _propertiesPanel;
        private SimpleUIPanel _uvEditorPanel;

        // Controls
        private SimpleButton _addBoxButton;
        private SimpleButton _deleteBoxButton;
        private SimpleButton _duplicateButton;
        private SimpleButton _saveButton;
        private SimpleButton _cancelButton;
        private SimpleButton _copyUVButton;
        private SimpleButton _pasteUVButton;
        private SimpleButton _resetUVButton;
        private SimpleButton _autoUVButton;

        private List<SimpleClickableText> _boxListItems = new();
        private List<SimpleSlider> _positionSliders = new();
        private List<SimpleSlider> _sizeSliders = new();
        private List<SimpleButton> _faceButtons = new();
        private SimpleDropdown _faceDropdown;
        private SimpleCheckbox _useSpriteCheckbox;
        private SimpleCheckbox _stretchCheckbox;
        private SimpleCheckbox _flipHCheckbox;
        private SimpleCheckbox _flipVCheckbox;

        // UV Editor specific controls
        private Rectangle _uvSourceRect;
        private bool _isDraggingUV = false;
        private Point _uvDragStart;
        private Rectangle _uvDragStartRect;
        private float _uvZoom = 1.0f;
        private Vector2 _uvPanOffset = Vector2.Zero;
        private bool _isPanningUV = false;
        private Point _panStartMouse;
        private Vector2 _panStartOffset;
        private const float MIN_ZOOM = 0.5f;
        private const float MAX_ZOOM = 10.0f;

        // State
        private CompositionBox _selectedBox;
        private int _selectedBoxIndex = -1;
        private BoxFace _selectedFace = BoxFace.Front;
        private UVMapping _copiedUV = null;
        private int _previousScrollValue = 0;

        // Preview
        private float _previewRotationY = 0;
        private float _previewRotationX = 0;
        private BasicEffect _previewEffect;
        private GraphicsDevice _device;
        private MeshBuilder _meshBuilder;
        private Mesh _previewMesh;
        private bool _previewNeedsRebuild = true;

        // Layout
        private Rectangle _spriteViewport;
        private Rectangle _3dViewport;
        private Rectangle _uvEditViewport;


        private RenderTarget2D _previewRenderTarget;
        private bool _renderTargetNeedsResize = true;

        // Preview modes
        private enum PreviewMode
        {
            Wireframe,
            Textured,
            TexturedWireframe
        }

        private PreviewMode _previewMode = PreviewMode.Textured;

        public BoxCompositionEditor(TileMetadata metadata, Texture2D sprite, Rectangle sourceRect,
            MonitorHelper helper, Action<TileMetadata> onSave)
            : base(Game1.uiViewport.Width / 2 - 700, Game1.uiViewport.Height / 2 - 400, 1400, 800, true)
        {
            _metadata = metadata;
            _spriteTexture = sprite;
            _spriteSourceRect = sourceRect;
            _helper = helper;
            _onSave = onSave;
            _device = Game1.graphics.GraphicsDevice;
            _meshBuilder = new MeshBuilder();

            // Initialize box composition if needed
            if (_metadata.BoxComposition == null)
            {
                _metadata.BoxComposition = new BoxComposition
                {
                    SourceSprite = sprite.Name,
                    UseOriginalColors = true
                };
                AddDefaultBox();
            }

            // Setup effect for preview
            _previewEffect = new BasicEffect(_device)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                PreferPerPixelLighting = true,
                VertexColorEnabled = false
            };

            // Setup default lighting
            _previewEffect.AmbientLightColor = new Vector3(0.3f);
            _previewEffect.DirectionalLight0.Enabled = true;
            _previewEffect.DirectionalLight0.Direction = Vector3.Normalize(new Vector3(0.5f, -1.0f, 0.5f));
            _previewEffect.DirectionalLight0.DiffuseColor = new Vector3(1.0f);
            _previewEffect.DirectionalLight0.SpecularColor = new Vector3(0.2f);

            // Build UI FIRST - this defines _3dViewport!
            BuildUI();

            // NOW create the render target after _3dViewport is defined
            if (_3dViewport.Width > 0 && _3dViewport.Height > 0)
            {
                _previewRenderTarget = new RenderTarget2D(
                    _device,
                    _3dViewport.Width,
                    _3dViewport.Height,
                    false,
                    SurfaceFormat.Color,
                    DepthFormat.Depth24
                );
            }

            RefreshBoxList();

            if (_metadata.BoxComposition.Boxes.Count > 0)
            {
                SelectBox(0);
            }
        }

        private void BuildUI()
        {
            // Define viewports
            _spriteViewport = new Rectangle(xPositionOnScreen + 20, yPositionOnScreen + 80, 200, 200);
            _uvEditViewport = new Rectangle(xPositionOnScreen + 240, yPositionOnScreen + 80, 256, 256);
            _3dViewport = new Rectangle(xPositionOnScreen + 520, yPositionOnScreen + 80, 400, 400);

            // Main panel
            _mainPanel = new SimpleUIPanel("Box Composition Editor",
                new Rectangle(xPositionOnScreen, yPositionOnScreen, width, height));

            // Info text
            var infoText = new SimpleClickableText(
                $"Editing: {_metadata.TileId}",
                Color.Gray,
                () => { })
            {
                Bounds = new Rectangle(xPositionOnScreen + 20, yPositionOnScreen + 50, 400, 20),
                IsEnabled = false
            };
            _mainPanel.AddElement(infoText);

            // Box list panel (right side)
            int boxListX = xPositionOnScreen + 940;
            _boxListPanel = new SimpleUIPanel("Boxes",
                new Rectangle(boxListX, yPositionOnScreen + 80, 250, 200));

            // Add/Delete/Duplicate buttons
            _addBoxButton = new SimpleButton("Add", Color.Green, (_) => AddNewBox())
            {
                Bounds = new Rectangle(boxListX + 10, yPositionOnScreen + 90, 50, 25)
            };
            _boxListPanel.AddElement(_addBoxButton);

            _duplicateButton = new SimpleButton("Copy", Color.Blue, (_) => DuplicateSelectedBox())
            {
                Bounds = new Rectangle(boxListX + 70, yPositionOnScreen + 90, 50, 25)
            };
            _boxListPanel.AddElement(_duplicateButton);

            _deleteBoxButton = new SimpleButton("Delete", Color.Red, (_) => DeleteSelectedBox())
            {
                Bounds = new Rectangle(boxListX + 130, yPositionOnScreen + 90, 50, 25)
            };
            _boxListPanel.AddElement(_deleteBoxButton);

            _autoUVButton = new SimpleButton("Auto", Color.Purple, (_) => AutoMapUVs())
            {
                Bounds = new Rectangle(boxListX + 190, yPositionOnScreen + 90, 50, 25)
            };
            _boxListPanel.AddElement(_autoUVButton);

            // Properties panel (right bottom)
            _propertiesPanel = new SimpleUIPanel("Box Properties",
                new Rectangle(boxListX, yPositionOnScreen + 290, 250, 250));

            // UV Editor panel (bottom)
            _uvEditorPanel = new SimpleUIPanel("UV Mapping",
                new Rectangle(xPositionOnScreen + 20, yPositionOnScreen + 350, 500, 190));

            BuildPropertyControls();
            BuildUVControls();

            // Preview mode selector
            var previewModeButton = new SimpleButton($"View: {_previewMode}", Color.Gray, (button) =>
            {
                _previewMode = (PreviewMode)(((int)_previewMode + 1) % 3);
                button.Label = $"View: {_previewMode}";
                _previewNeedsRebuild = true;
            })
            {
                Bounds = new Rectangle(xPositionOnScreen + 520, yPositionOnScreen + 490, 150, 30)
            };
            _mainPanel.AddElement(previewModeButton);

            // Main action buttons
            _saveButton = new SimpleButton("Save", Color.Green, (_) => SaveAndClose())
            {
                Bounds = new Rectangle(xPositionOnScreen + width - 220, yPositionOnScreen + height - 60, 100, 40)
            };
            _mainPanel.AddElement(_saveButton);

            _cancelButton = new SimpleButton("Cancel", Color.Red, (_) => exitThisMenu())
            {
                Bounds = new Rectangle(xPositionOnScreen + width - 110, yPositionOnScreen + height - 60, 100, 40)
            };
            _mainPanel.AddElement(_cancelButton);

            // Preview controls info
            var previewInfo = new SimpleClickableText(
                "3D: [Q/E] Rotate Y | [R/F] Rotate X | Box: [W/A/S/D] Move | [Shift+W/A/S/D] Resize",
                Color.Black,
                () => { })
            {
                Bounds = new Rectangle(xPositionOnScreen + 520, yPositionOnScreen + 525, 400, 20),
                IsEnabled = false
            };
            _mainPanel.AddElement(previewInfo);
        }

        private void BuildPropertyControls()
        {
            int baseY = yPositionOnScreen + 320;
            int baseX = xPositionOnScreen + 950;

            // Position sliders
            var posXSlider = new SimpleSlider("X", -128, 128, 0, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.Position = new Vector3(value, _selectedBox.Position.Y, _selectedBox.Position.Z);
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 230, 20)
            };
            _propertiesPanel.AddElement(posXSlider);
            _positionSliders.Add(posXSlider);

            baseY += 35;
            var posYSlider = new SimpleSlider("Y", -128, 128, 0, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.Position = new Vector3(_selectedBox.Position.X, value, _selectedBox.Position.Z);
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 230, 20)
            };
            _propertiesPanel.AddElement(posYSlider);
            _positionSliders.Add(posYSlider);

            baseY += 35;
            var posZSlider = new SimpleSlider("Z", -128, 128, 0, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.Position = new Vector3(_selectedBox.Position.X, _selectedBox.Position.Y, value);
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 230, 20)
            };
            _propertiesPanel.AddElement(posZSlider);
            _positionSliders.Add(posZSlider);

            // Size sliders
            baseY += 40;
            var sizeXSlider = new SimpleSlider("Width", 8, 128, 64, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.Size = new Vector3(value, _selectedBox.Size.Y, _selectedBox.Size.Z);
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 230, 20)
            };
            _propertiesPanel.AddElement(sizeXSlider);
            _sizeSliders.Add(sizeXSlider);

            baseY += 35;
            var sizeYSlider = new SimpleSlider("Height", 8, 128, 64, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.Size = new Vector3(_selectedBox.Size.X, value, _selectedBox.Size.Z);
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 230, 20)
            };
            _propertiesPanel.AddElement(sizeYSlider);
            _sizeSliders.Add(sizeYSlider);

            baseY += 35;
            var sizeZSlider = new SimpleSlider("Depth", 8, 128, 64, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.Size = new Vector3(_selectedBox.Size.X, _selectedBox.Size.Y, value);
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 230, 20)
            };
            _propertiesPanel.AddElement(sizeZSlider);
            _sizeSliders.Add(sizeZSlider);
        }

        private void BuildUVControls()
        {
            int baseY = yPositionOnScreen + 380;
            int baseX = xPositionOnScreen + 30;

            // Face selector buttons (visual)
            string[] faceNames = { "Front", "Back", "Top", "Bottom", "Left", "Right" };
            for (int i = 0; i < 6; i++)
            {
                int faceIndex = i;
                var faceButton = new SimpleButton(faceNames[i],
                    _selectedFace == (BoxFace)i ? Color.Yellow : Color.LightGray,
                    (_) =>
                    {
                        _selectedFace = (BoxFace)faceIndex;
                        UpdateFaceSelection();
                    })
                {
                    Bounds = new Rectangle(baseX + i * 80, baseY, 75, 25)
                };
                _faceButtons.Add(faceButton);
                _uvEditorPanel.AddElement(faceButton);
            }

            baseY += 40;

            // UV Controls
            _useSpriteCheckbox = new SimpleCheckbox("Use Texture", true, value =>
            {
                if (_selectedBox != null)
                {
                    _selectedBox.UseSprite = value;
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX, baseY, 120, 24)
            };
            _uvEditorPanel.AddElement(_useSpriteCheckbox);

            _stretchCheckbox = new SimpleCheckbox("Stretch", true, value =>
            {
                if (_selectedBox != null && _selectedBox.FaceUVs.TryGetValue(_selectedFace, out var uv))
                {
                    uv.Stretch = value;
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX + 130, baseY, 100, 24)
            };
            _uvEditorPanel.AddElement(_stretchCheckbox);

            _flipHCheckbox = new SimpleCheckbox("Flip H", false, value =>
            {
                if (_selectedBox != null && _selectedBox.FaceUVs.TryGetValue(_selectedFace, out var uv))
                {
                    uv.FlipHorizontal = value;
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX + 240, baseY, 80, 24)
            };
            _uvEditorPanel.AddElement(_flipHCheckbox);

            _flipVCheckbox = new SimpleCheckbox("Flip V", false, value =>
            {
                if (_selectedBox != null && _selectedBox.FaceUVs.TryGetValue(_selectedFace, out var uv))
                {
                    uv.FlipVertical = value;
                    _previewNeedsRebuild = true;
                }
            })
            {
                Bounds = new Rectangle(baseX + 330, baseY, 80, 24)
            };
            _uvEditorPanel.AddElement(_flipVCheckbox);

            baseY += 35;

            // UV action buttons
            _copyUVButton = new SimpleButton("Copy UV", Color.Blue, (_) => CopyUV())
            {
                Bounds = new Rectangle(baseX, baseY, 80, 25)
            };
            _uvEditorPanel.AddElement(_copyUVButton);

            _pasteUVButton = new SimpleButton("Paste UV", Color.Blue, (_) => PasteUV())
            {
                Bounds = new Rectangle(baseX + 90, baseY, 80, 25)
            };
            _uvEditorPanel.AddElement(_pasteUVButton);

            _resetUVButton = new SimpleButton("Reset UV", Color.Orange, (_) => ResetUV())
            {
                Bounds = new Rectangle(baseX + 180, baseY, 80, 25)
            };
            _uvEditorPanel.AddElement(_resetUVButton);

            var applyAllButton = new SimpleButton("Apply to All", Color.Purple, (_) => ApplyUVToAllFaces())
            {
                Bounds = new Rectangle(baseX + 270, baseY, 100, 25)
            };
            _uvEditorPanel.AddElement(applyAllButton);
            
            var resetZoomButton = new SimpleButton("Reset View", Color.Gray, (_) => {
                _uvZoom = 1.0f;
                _uvPanOffset = Vector2.Zero;
            })
            {
                Bounds = new Rectangle(baseX + 380, baseY, 90, 25)
            };
            _uvEditorPanel.AddElement(resetZoomButton);

            baseY += 35;

            // UV info text
            var uvInfo = new SimpleClickableText(
                "Click and drag on UV editor to select texture region",
                Color.DarkGray,
                () => { })
            {
                Bounds = new Rectangle(baseX, baseY, 400, 20),
                IsEnabled = false
            };
            _uvEditorPanel.AddElement(uvInfo);
        }

        private void UpdateFaceSelection()
        {
            // Update face button colors
            for (int i = 0; i < _faceButtons.Count; i++)
            {
                _faceButtons[i].Label = _faceButtons[i].Label; // Keep label
                // Can't directly change color in SimpleButton, would need to extend it
            }

            // Update UV controls for selected face
            if (_selectedBox != null)
            {
                if (_selectedBox.FaceUVs.TryGetValue(_selectedFace, out var uv))
                {
                    _stretchCheckbox.SetChecked(uv.Stretch);
                    _flipHCheckbox.SetChecked(uv.FlipHorizontal);
                    _flipVCheckbox.SetChecked(uv.FlipVertical);
                    _uvSourceRect = uv.SourceRect;
                }
                else
                {
                    // No UV mapping for this face yet
                    _uvSourceRect = _spriteSourceRect;
                    _stretchCheckbox.SetChecked(true);
                    _flipHCheckbox.SetChecked(false);
                    _flipVCheckbox.SetChecked(false);
                }
            }
        }

        private void CopyUV()
        {
            if (_selectedBox != null && _selectedBox.FaceUVs.TryGetValue(_selectedFace, out var uv))
            {
                _copiedUV = new UVMapping
                {
                    SourceRect = uv.SourceRect,
                    Stretch = uv.Stretch,
                    FlipHorizontal = uv.FlipHorizontal,
                    FlipVertical = uv.FlipVertical
                };
                Game1.addHUDMessage(new HUDMessage("UV copied", 1));
            }
        }

        private void PasteUV()
        {
            if (_selectedBox != null && _copiedUV != null)
            {
                _selectedBox.FaceUVs[_selectedFace] = new UVMapping
                {
                    SourceRect = _copiedUV.SourceRect,
                    Stretch = _copiedUV.Stretch,
                    FlipHorizontal = _copiedUV.FlipHorizontal,
                    FlipVertical = _copiedUV.FlipVertical
                };
                UpdateFaceSelection();
                _previewNeedsRebuild = true;
                Game1.addHUDMessage(new HUDMessage("UV pasted", 1));
            }
        }

        private void ResetUV()
        {
            if (_selectedBox != null)
            {
                _selectedBox.FaceUVs[_selectedFace] = new UVMapping
                {
                    SourceRect = _spriteSourceRect,
                    Stretch = true,
                    FlipHorizontal = false,
                    FlipVertical = false
                };
                UpdateFaceSelection();
                _previewNeedsRebuild = true;
            }
        }

        private void ApplyUVToAllFaces()
        {
            if (_selectedBox != null && _selectedBox.FaceUVs.TryGetValue(_selectedFace, out var sourceUV))
            {
                foreach (BoxFace face in Enum.GetValues(typeof(BoxFace)))
                {
                    _selectedBox.FaceUVs[face] = new UVMapping
                    {
                        SourceRect = sourceUV.SourceRect,
                        Stretch = sourceUV.Stretch,
                        FlipHorizontal = sourceUV.FlipHorizontal,
                        FlipVertical = sourceUV.FlipVertical
                    };
                }

                _previewNeedsRebuild = true;
                Game1.addHUDMessage(new HUDMessage("UV applied to all faces", 1));
            }
        }

        private void AutoMapUVs()
        {
            if (_selectedBox == null) return;

            // Auto-map based on common patterns
            int w = _spriteSourceRect.Width;
            int h = _spriteSourceRect.Height;

            // If sprite is tall (like a tree or character), map front to full sprite
            if (h > w * 1.5f)
            {
                // Front gets full sprite
                _selectedBox.FaceUVs[BoxFace.Front] = new UVMapping
                {
                    SourceRect = _spriteSourceRect,
                    Stretch = true
                };

                // Back gets mirrored
                _selectedBox.FaceUVs[BoxFace.Back] = new UVMapping
                {
                    SourceRect = _spriteSourceRect,
                    Stretch = true,
                    FlipHorizontal = true
                };

                // Top gets top portion
                _selectedBox.FaceUVs[BoxFace.Top] = new UVMapping
                {
                    SourceRect = new Rectangle(_spriteSourceRect.X, _spriteSourceRect.Y, w, h / 4),
                    Stretch = true
                };

                // Sides get edge strips
                _selectedBox.FaceUVs[BoxFace.Left] = new UVMapping
                {
                    SourceRect = new Rectangle(_spriteSourceRect.X, _spriteSourceRect.Y, w / 8, h),
                    Stretch = true
                };

                _selectedBox.FaceUVs[BoxFace.Right] = new UVMapping
                {
                    SourceRect = new Rectangle(_spriteSourceRect.Right - w / 8, _spriteSourceRect.Y, w / 8, h),
                    Stretch = true
                };
            }
            else
            {
                // For square-ish sprites, divide into regions
                _selectedBox.FaceUVs[BoxFace.Front] = new UVMapping
                {
                    SourceRect = _spriteSourceRect,
                    Stretch = true
                };

                foreach (BoxFace face in Enum.GetValues(typeof(BoxFace)))
                {
                    if (face != BoxFace.Front)
                    {
                        _selectedBox.FaceUVs[face] = new UVMapping
                        {
                            SourceRect = _spriteSourceRect,
                            Stretch = true
                        };
                    }
                }
            }

            _previewNeedsRebuild = true;
            Game1.addHUDMessage(new HUDMessage("Auto-mapped UVs", 1));
        }

        private void RefreshBoxList()
        {
            // Clear old list items
            _boxListItems.Clear();

            // Create new list items
            int y = yPositionOnScreen + 120;
            int x = xPositionOnScreen + 950;

            for (int i = 0; i < _metadata.BoxComposition.Boxes.Count; i++)
            {
                int index = i; // Capture for closure
                var box = _metadata.BoxComposition.Boxes[i];

                var item = new SimpleClickableText(
                    $"Box {i + 1}: {box.Size.X:F0}x{box.Size.Y:F0}x{box.Size.Z:F0}",
                    _selectedBoxIndex == i ? Color.Yellow : Color.Black,
                    Color.Yellow,
                    () => SelectBox(index))
                {
                    Bounds = new Rectangle(x, y, 230, 20)
                };

                _boxListPanel.AddElement(item);
                _boxListItems.Add(item);
                y += 22;
            }
        }

        private void SelectBox(int index)
        {
            if (index < 0 || index >= _metadata.BoxComposition.Boxes.Count)
                return;

            _selectedBoxIndex = index;
            _selectedBox = _metadata.BoxComposition.Boxes[index];

            // Update sliders with current values
            _positionSliders[0].SetValue(_selectedBox.Position.X);
            _positionSliders[1].SetValue(_selectedBox.Position.Y);
            _positionSliders[2].SetValue(_selectedBox.Position.Z);

            _sizeSliders[0].SetValue(_selectedBox.Size.X);
            _sizeSliders[1].SetValue(_selectedBox.Size.Y);
            _sizeSliders[2].SetValue(_selectedBox.Size.Z);

            _useSpriteCheckbox.SetChecked(_selectedBox.UseSprite);

            UpdateFaceSelection();
            UpdateBoxListColors();
            _previewNeedsRebuild = true;
        }

        private void UpdateBoxListColors()
        {
            for (int i = 0; i < _boxListItems.Count; i++)
            {
                // Update text color based on selection
                var item = _boxListItems[i];
                item.UpdateTextColor(_selectedBoxIndex == i ? Color.Yellow : Color.White);
            }
        }

        private void AddDefaultBox()
        {
            float aspectRatio = (float)_spriteSourceRect.Width / _spriteSourceRect.Height;

            var box = new CompositionBox
            {
                Position = Vector3.Zero,
                Size = new Vector3(64 * Math.Min(aspectRatio, 2f), 64, 32),
                UseSprite = true
            };

            // Default UV mapping - full sprite on front
            box.FaceUVs[BoxFace.Front] = new UVMapping
            {
                SourceRect = _spriteSourceRect,
                Stretch = true
            };

            _metadata.BoxComposition.Boxes.Add(box);
        }

        private void AddNewBox()
        {
            var box = new CompositionBox
            {
                Position = new Vector3(0, _metadata.BoxComposition.Boxes.Count * 20, 0),
                Size = new Vector3(64, 64, 32),
                UseSprite = true
            };

            box.FaceUVs[BoxFace.Front] = new UVMapping
            {
                SourceRect = _spriteSourceRect,
                Stretch = true
            };

            _metadata.BoxComposition.Boxes.Add(box);
            RefreshBoxList();
            SelectBox(_metadata.BoxComposition.Boxes.Count - 1);
        }

        private void DuplicateSelectedBox()
        {
            if (_selectedBox == null) return;

            var newBox = new CompositionBox
            {
                Position = _selectedBox.Position + new Vector3(10, 10, 10),
                Size = _selectedBox.Size,
                UseSprite = _selectedBox.UseSprite,
                Tint = _selectedBox.Tint,
                FaceUVs = new Dictionary<BoxFace, UVMapping>(_selectedBox.FaceUVs)
            };

            _metadata.BoxComposition.Boxes.Add(newBox);
            RefreshBoxList();
            SelectBox(_metadata.BoxComposition.Boxes.Count - 1);
        }

        private void DeleteSelectedBox()
        {
            if (_selectedBox == null || _metadata.BoxComposition.Boxes.Count <= 1) return;

            _metadata.BoxComposition.Boxes.RemoveAt(_selectedBoxIndex);
            RefreshBoxList();
            SelectBox(Math.Min(_selectedBoxIndex, _metadata.BoxComposition.Boxes.Count - 1));
        }

        public override void receiveKeyPress(Keys key)
        {
            base.receiveKeyPress(key);

            // Preview rotation
            if (key == Keys.Q) _previewRotationY -= 0.1f;
            if (key == Keys.E) _previewRotationY += 0.1f;
            if (key == Keys.R) _previewRotationX -= 0.1f;
            if (key == Keys.F) _previewRotationX += 0.1f;

            // Box manipulation
            if (_selectedBox != null)
            {
                float moveAmount = 4f;
                bool shift = Game1.oldKBState.IsKeyDown(Keys.LeftShift);

                if (shift)
                {
                    // Resize
                    if (key == Keys.W)
                        _selectedBox.Size = new Vector3(_selectedBox.Size.X, _selectedBox.Size.Y + moveAmount,
                            _selectedBox.Size.Z);
                    if (key == Keys.S)
                        _selectedBox.Size = new Vector3(_selectedBox.Size.X,
                            Math.Max(8, _selectedBox.Size.Y - moveAmount), _selectedBox.Size.Z);
                    if (key == Keys.A)
                        _selectedBox.Size = new Vector3(Math.Max(8, _selectedBox.Size.X - moveAmount),
                            _selectedBox.Size.Y, _selectedBox.Size.Z);
                    if (key == Keys.D)
                        _selectedBox.Size = new Vector3(_selectedBox.Size.X + moveAmount, _selectedBox.Size.Y,
                            _selectedBox.Size.Z);
                }
                else
                {
                    // Move
                    if (key == Keys.W)
                        _selectedBox.Position = new Vector3(_selectedBox.Position.X,
                            _selectedBox.Position.Y + moveAmount, _selectedBox.Position.Z);
                    if (key == Keys.S)
                        _selectedBox.Position = new Vector3(_selectedBox.Position.X,
                            _selectedBox.Position.Y - moveAmount, _selectedBox.Position.Z);
                    if (key == Keys.A)
                        _selectedBox.Position = new Vector3(_selectedBox.Position.X - moveAmount,
                            _selectedBox.Position.Y, _selectedBox.Position.Z);
                    if (key == Keys.D)
                        _selectedBox.Position = new Vector3(_selectedBox.Position.X + moveAmount,
                            _selectedBox.Position.Y, _selectedBox.Position.Z);
                }

                // Update sliders to reflect changes
                SelectBox(_selectedBoxIndex);
                _previewNeedsRebuild = true;
            }
        }
        
        public override void receiveScrollWheelAction(int direction)
        {
            // Check if mouse is over UV editor viewport
            var mouseState = Mouse.GetState();
            if (_uvEditViewport.Contains(mouseState.X, mouseState.Y))
            {
                // Zoom in/out
                float zoomDelta = direction > 0 ? 1.25f : 0.8f;
                float oldZoom = _uvZoom;
                _uvZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, _uvZoom * zoomDelta));
            
                // Adjust pan to zoom towards mouse position
                if (_uvZoom != oldZoom)
                {
                    // Get mouse position relative to viewport
                    float relX = (mouseState.X - _uvEditViewport.X) / (float)_uvEditViewport.Width;
                    float relY = (mouseState.Y - _uvEditViewport.Y) / (float)_uvEditViewport.Height;
                
                    // Adjust pan offset to keep the point under the mouse cursor stationary
                    _uvPanOffset.X += (relX * _spriteTexture.Width) * (1 - zoomDelta);
                    _uvPanOffset.Y += (relY * _spriteTexture.Height) * (1 - zoomDelta);
                
                    ClampPanOffset();
                }
                return;
            }
        
            base.receiveScrollWheelAction(direction);
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            // Check UV editor viewport for drag start
            if (_uvEditViewport.Contains(x, y))
            {
                // Check if shift is held for panning
                if (Game1.oldKBState.IsKeyDown(Keys.LeftShift))
                {
                    _isPanningUV = true;
                    _panStartMouse = new Point(x, y);
                    _panStartOffset = _uvPanOffset;
                }
                else
                {
                    // Normal UV selection
                    _isDraggingUV = true;
                
                    // Convert viewport coords to texture coords accounting for zoom/pan
                    var uvCoords = ViewportToTextureCoords(x - _uvEditViewport.X, y - _uvEditViewport.Y);
                    _uvDragStart = new Point((int)uvCoords.X, (int)uvCoords.Y);
                    _uvDragStartRect = new Rectangle((int)uvCoords.X, (int)uvCoords.Y, 0, 0);
                }
                return;
            }

            _mainPanel.HandleClick(x, y);
            _boxListPanel.HandleClick(x, y);
            _propertiesPanel.HandleClick(x, y);
            _uvEditorPanel.HandleClick(x, y);
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            if (_isPanningUV)
            {
                // Update pan offset
                _uvPanOffset = _panStartOffset + new Vector2(
                    (_panStartMouse.X - x) / _uvZoom,
                    (_panStartMouse.Y - y) / _uvZoom
                );
                ClampPanOffset();
            }
            else if (_isDraggingUV && _uvEditViewport.Contains(x, y))
            {
                // Convert to texture coordinates with zoom/pan
                var uvCoords = ViewportToTextureCoords(x - _uvEditViewport.X, y - _uvEditViewport.Y);
            
                // Create rectangle from drag
                int minX = Math.Min(_uvDragStartRect.X, (int)uvCoords.X);
                int minY = Math.Min(_uvDragStartRect.Y, (int)uvCoords.Y);
                int maxX = Math.Max(_uvDragStartRect.X, (int)uvCoords.X);
                int maxY = Math.Max(_uvDragStartRect.Y, (int)uvCoords.Y);
            
                _uvSourceRect = new Rectangle(minX, minY, maxX - minX, maxY - minY);
            
                // Update selected face UV
                if (_selectedBox != null && _uvSourceRect.Width > 0 && _uvSourceRect.Height > 0)
                {
                    _selectedBox.FaceUVs[_selectedFace] = new UVMapping
                    {
                        SourceRect = _uvSourceRect,
                        Stretch = _stretchCheckbox.IsChecked,
                        FlipHorizontal = _flipHCheckbox.IsChecked,
                        FlipVertical = _flipVCheckbox.IsChecked
                    };
                    _previewNeedsRebuild = true;
                }
            }

            _mainPanel.HandleDrag(x, y);
            _boxListPanel.HandleDrag(x, y);
            _propertiesPanel.HandleDrag(x, y);
            _uvEditorPanel.HandleDrag(x, y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);

            _isDraggingUV = false;
            _isPanningUV = false;

            _mainPanel.HandleRelease();
            _boxListPanel.HandleRelease();
            _propertiesPanel.HandleRelease();
            _uvEditorPanel.HandleRelease();
        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);

            _mainPanel.UpdateHover(x, y);
            _boxListPanel.UpdateHover(x, y);
            _propertiesPanel.UpdateHover(x, y);
            _uvEditorPanel.UpdateHover(x, y);
        }

        public override void draw(SpriteBatch b)
        {
            // Draw panels
            _mainPanel.Draw(b);
            _boxListPanel.Draw(b);
            _propertiesPanel.Draw(b);
            _uvEditorPanel.Draw(b);

            // Draw custom viewports
            DrawSpritePreview(b);
            DrawUVEditor(b);
            Draw3DPreview(b);

            drawMouse(b);
        }

        private void DrawSpritePreview(SpriteBatch b)
        {
            // Draw border
            b.Draw(Game1.staminaRect, new Rectangle(_spriteViewport.X - 2, _spriteViewport.Y - 2,
                _spriteViewport.Width + 4, _spriteViewport.Height + 4), Color.Black);

            // Draw background
            b.Draw(Game1.staminaRect, _spriteViewport, Color.DarkGray);

            // Draw sprite scaled to fit
            float scale = Math.Min(_spriteViewport.Width / (float)_spriteSourceRect.Width,
                _spriteViewport.Height / (float)_spriteSourceRect.Height);

            var destRect = new Rectangle(
                _spriteViewport.X + (_spriteViewport.Width - (int)(_spriteSourceRect.Width * scale)) / 2,
                _spriteViewport.Y + (_spriteViewport.Height - (int)(_spriteSourceRect.Height * scale)) / 2,
                (int)(_spriteSourceRect.Width * scale),
                (int)(_spriteSourceRect.Height * scale)
            );

            b.Draw(_spriteTexture, destRect, _spriteSourceRect, Color.White);

            // Label
            b.DrawString(Game1.smallFont, "Original Sprite",
                new Vector2(_spriteViewport.X, _spriteViewport.Y - 25), Color.Black);
        }

        private void DrawUVEditor(SpriteBatch b)
    {
        // Draw border
        b.Draw(Game1.staminaRect, new Rectangle(_uvEditViewport.X - 2, _uvEditViewport.Y - 2, 
            _uvEditViewport.Width + 4, _uvEditViewport.Height + 4), Color.Black);
        
        // Draw background
        b.Draw(Game1.staminaRect, _uvEditViewport, Color.Black);
        
        // Calculate visible portion of texture
        float visibleWidth = _uvEditViewport.Width / _uvZoom;
        float visibleHeight = _uvEditViewport.Height / _uvZoom;
        
        // Source rectangle from the texture
        var sourceRect = new Rectangle(
            (int)_uvPanOffset.X,
            (int)_uvPanOffset.Y,
            (int)Math.Min(visibleWidth, _spriteTexture.Width - _uvPanOffset.X),
            (int)Math.Min(visibleHeight, _spriteTexture.Height - _uvPanOffset.Y)
        );
        
        // Destination rectangle in viewport
        var destRect = new Rectangle(
            _uvEditViewport.X,
            _uvEditViewport.Y,
            (int)(sourceRect.Width * _uvZoom),
            (int)(sourceRect.Height * _uvZoom)
        );
        
        // Draw the zoomed texture
        b.Draw(_spriteTexture, destRect, sourceRect, Color.White * 0.7f);
        
        // Draw UV selection rectangle
        if (_selectedBox != null && _selectedBox.FaceUVs.TryGetValue(_selectedFace, out var uv))
        {
            var selectionRect = TextureToViewportRect(uv.SourceRect);
            
            // Only draw if visible
            if (selectionRect.Intersects(_uvEditViewport))
            {
                // Clip to viewport
                var clipped = Rectangle.Intersect(selectionRect, _uvEditViewport);
                
                // Draw selection with animated border
                float pulse = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalSeconds * 3) * 0.25f + 0.75f;
                DrawBorder(b, clipped, 2, Color.Yellow * pulse);
                
                // Draw semi-transparent overlay
                b.Draw(Game1.staminaRect, clipped, Color.Yellow * 0.2f);
            }
        }
        
        // Draw current drag selection
        if (_isDraggingUV && _uvSourceRect.Width > 0 && _uvSourceRect.Height > 0)
        {
            var dragRect = TextureToViewportRect(_uvSourceRect);
            
            if (dragRect.Intersects(_uvEditViewport))
            {
                var clipped = Rectangle.Intersect(dragRect, _uvEditViewport);
                DrawBorder(b, clipped, 1, Color.Cyan);
                b.Draw(Game1.staminaRect, clipped, Color.Cyan * 0.1f);
            }
        }
        
        // Draw zoom level and controls info
        string zoomText = $"Zoom: {_uvZoom:F1}x";
        b.DrawString(Game1.smallFont, zoomText, 
            new Vector2(_uvEditViewport.Right - 80, _uvEditViewport.Y - 25), Color.Black);
        
        // Label
        b.DrawString(Game1.smallFont, $"UV Editor - {_selectedFace}", 
            new Vector2(_uvEditViewport.X, _uvEditViewport.Y - 25), Color.Black);
        
        // Controls hint
        b.DrawString(Game1.tinyFont, "[Scroll: Zoom] [Shift+Drag: Pan] [Drag: Select]", 
            new Vector2(_uvEditViewport.X, _uvEditViewport.Bottom + 5), Color.DarkGray);
        
        // Draw texture dimensions if zoomed in
        if (_uvZoom > 1.5f)
        {
            string dimText = $"{_spriteTexture.Width}x{_spriteTexture.Height}";
            b.DrawString(Game1.tinyFont, dimText,
                new Vector2(_uvEditViewport.Right - 60, _uvEditViewport.Bottom + 5), Color.DarkGray);
        }
    }

        private void BuildPreviewMesh()
        {
            _meshBuilder.Clear();

            foreach (var box in _metadata.BoxComposition.Boxes)
            {
                if (!box.UseSprite && _previewMode == PreviewMode.Textured)
                    continue;

                // Build each face separately with proper UV mapping
                foreach (BoxFace face in Enum.GetValues(typeof(BoxFace)))
                {
                    if (box.FaceUVs.TryGetValue(face, out var uv))
                    {
                        _meshBuilder.AddBoxFace(box.Position, box.Size, face, uv.SourceRect, _spriteTexture);
                    }
                    else if (box.UseSprite)
                    {
                        // Use default UV if not specified
                        _meshBuilder.AddBoxFace(box.Position, box.Size, face, _spriteSourceRect, _spriteTexture);
                    }
                }
            }

            _previewMesh?.Dispose();
            _previewMesh = _meshBuilder.BuildMesh(_device);
        }

        public override void update(GameTime time)
        {
            base.update(time);

            // Render 3D preview to render target during update, not draw
            if (_previewNeedsRebuild)
            {
                BuildPreviewMesh();
                _previewNeedsRebuild = false;
            }

            // Render to the render target here
            if (_previewRenderTarget != null && _previewMesh != null)
            {
                RenderPreviewToTarget();
            }
        }

        private void RenderPreviewToTarget()
        {
            // Save current render targets
            var oldTargets = _device.GetRenderTargets();

            // Set our render target
            _device.SetRenderTarget(_previewRenderTarget);

            // Clear with background color
            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
                Color.CornflowerBlue, 1.0f, 0);

            // Setup matrices
            Matrix world = Matrix.CreateRotationX(_previewRotationX) *
                           Matrix.CreateRotationY(_previewRotationY);
            Matrix view = Matrix.CreateLookAt(
                new Vector3(0, 100, 200),
                Vector3.Zero,
                Vector3.Up
            );
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver4,
                (float)_3dViewport.Width / _3dViewport.Height,
                1f,
                1000f
            );

            _previewEffect.World = world;
            _previewEffect.View = view;
            _previewEffect.Projection = projection;
            _previewEffect.Texture = _spriteTexture;

            // Setup render states
            _device.DepthStencilState = DepthStencilState.Default;
            _device.BlendState = BlendState.AlphaBlend;

            // Choose rasterizer state based on preview mode
            if (_previewMode == PreviewMode.Wireframe)
            {
                _device.RasterizerState = new RasterizerState
                    { FillMode = FillMode.WireFrame, CullMode = CullMode.None };
                _previewEffect.TextureEnabled = false;
            }
            else
            {
                _device.RasterizerState = RasterizerState.CullCounterClockwise;
                _previewEffect.TextureEnabled = true;
            }

            // Render mesh
            _device.SetVertexBuffer(_previewMesh.VertexBuffer);
            _device.Indices = _previewMesh.IndexBuffer;

            foreach (var pass in _previewEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _previewMesh.PrimitiveCount);
            }

            // Restore original render targets
            _device.SetRenderTargets(oldTargets);
        }

        private void Draw3DPreview(SpriteBatch b)
        {
            // Draw border
            b.Draw(Game1.staminaRect, new Rectangle(_3dViewport.X - 2, _3dViewport.Y - 2,
                _3dViewport.Width + 4, _3dViewport.Height + 4), Color.Black);

            // Draw the render target as a texture
            if (_previewRenderTarget != null)
            {
                b.Draw(_previewRenderTarget, _3dViewport, Color.White);
            }
            else
            {
                // Fallback background
                b.Draw(Game1.staminaRect, _3dViewport, Color.CornflowerBlue);
            }

            // Label
            b.DrawString(Game1.smallFont, "3D Preview",
                new Vector2(_3dViewport.X, _3dViewport.Y - 25), Color.Black);
        }

        private void DrawBorder(SpriteBatch b, Rectangle rect, int thickness, Color color)
        {
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            b.Draw(Game1.staminaRect, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            b.Draw(Game1.staminaRect, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
        
        private Vector2 ViewportToTextureCoords(int viewX, int viewY)
        {
            float texX = (viewX / _uvZoom) + _uvPanOffset.X;
            float texY = (viewY / _uvZoom) + _uvPanOffset.Y;
        
            // Clamp to texture bounds
            texX = Math.Max(0, Math.Min(_spriteTexture.Width - 1, texX));
            texY = Math.Max(0, Math.Min(_spriteTexture.Height - 1, texY));
        
            return new Vector2(texX, texY);
        }
    
        private Rectangle TextureToViewportRect(Rectangle texRect)
        {
            return new Rectangle(
                _uvEditViewport.X + (int)((texRect.X - _uvPanOffset.X) * _uvZoom),
                _uvEditViewport.Y + (int)((texRect.Y - _uvPanOffset.Y) * _uvZoom),
                (int)(texRect.Width * _uvZoom),
                (int)(texRect.Height * _uvZoom)
            );
        }
    
        private void ClampPanOffset()
        {
            // Ensure we can't pan beyond texture bounds
            float maxPanX = Math.Max(0, _spriteTexture.Width - (_uvEditViewport.Width / _uvZoom));
            float maxPanY = Math.Max(0, _spriteTexture.Height - (_uvEditViewport.Height / _uvZoom));
        
            _uvPanOffset.X = Math.Max(0, Math.Min(maxPanX, _uvPanOffset.X));
            _uvPanOffset.Y = Math.Max(0, Math.Min(maxPanY, _uvPanOffset.Y));
        }

        private void SaveAndClose()
        {
            _onSave?.Invoke(_metadata);
            Game1.addHUDMessage(new HUDMessage("Box composition saved!", 3));
            exitThisMenu();
        }
    }
}