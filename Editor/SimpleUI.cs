
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace StardewValley3D.Editor.UI
{
    public abstract class SimpleUIElement
    {
        public Rectangle Bounds { get; set; }
        public string Label { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        
        public abstract void HandleClick(int x, int y);
        public abstract void HandleDrag(int x, int y);
        public abstract void Draw(SpriteBatch batch, float alpha = 1f);
    }
    
    public class SimpleClickableText : SimpleUIElement
    {
        private Action _onClick;
        private Color _textColor;
        private Color _hoverColor;
        private bool _isHovered;
        private SpriteFont _font;

        public void UpdateTextColor(Color color)
        {
            _textColor = color;
        }
        
        public SimpleClickableText(string text, Color textColor, Action onClick, SpriteFont font = null)
        {
            Label = text;
            _textColor = textColor;
            _hoverColor = Color.Yellow; // Default hover color
            _onClick = onClick;
            _font = font ?? Game1.smallFont; // Use smallFont as default
        }
        
        public SimpleClickableText(string text, Color textColor, Color hoverColor, Action onClick, SpriteFont font = null)
        {
            Label = text;
            _textColor = textColor;
            _hoverColor = hoverColor;
            _onClick = onClick;
            _font = font ?? Game1.smallFont;
        }
        
        public override void HandleClick(int x, int y)
        {
            if (Bounds.Contains(x, y) && IsEnabled)
            {
                _onClick?.Invoke();
            }
        }
        
        public override void HandleDrag(int x, int y) { }
        
        public void UpdateHover(int mouseX, int mouseY)
        {
            _isHovered = Bounds.Contains(mouseX, mouseY) && IsEnabled;
        }
        
        public override void Draw(SpriteBatch batch, float alpha = 1f)
        {
            if (!IsVisible) return;
            
            Color drawColor = IsEnabled ? 
                (_isHovered ? _hoverColor : _textColor) : 
                Color.Gray;
            
            drawColor *= alpha;
            
            batch.DrawString(_font, Label, new Vector2(Bounds.X, Bounds.Y), drawColor);
        }
        
        // Helper method to auto-size bounds based on text
        public void AutoSizeBounds(int x, int y)
        {
            var textSize = _font.MeasureString(Label);
            Bounds = new Rectangle(x, y, (int)textSize.X, (int)textSize.Y);
        }
    }
    
    public class SimpleSlider : SimpleUIElement
    {
        private float _value;
        private float _min;
        private float _max;
        private bool _isDragging;
        private Action<float> _onChange;
        
        public float Value => _value;

        public void SetValue(float value)
        {
            _value = value;
        }
        
        public SimpleSlider(string label, float min, float max, float value, Action<float> onChange)
        {
            Label = label;
            _min = min;
            _max = max;
            _value = value;
            _onChange = onChange;
        }
        
        public override void HandleClick(int x, int y)
        {
            if (Bounds.Contains(x, y))
            {
                _isDragging = true;
                UpdateValue(x);
            }
        }
        
        public override void HandleDrag(int x, int y)
        {
            if (_isDragging)
            {
                UpdateValue(x);
            }
        }
        
        public void Release()
        {
            _isDragging = false;
        }
        
        private void UpdateValue(int x)
        {
            float percent = Math.Max(0, Math.Min(1, (x - Bounds.X) / (float)Bounds.Width));
            float newValue = _min + percent * (_max - _min);
            
            if (Math.Abs(newValue - _value) > 0.001f)
            {
                _value = newValue;
                _onChange?.Invoke(_value);
            }
        }
        
        public override void Draw(SpriteBatch batch, float alpha = 1f)
        {
            var color = IsEnabled ? Color.White * alpha : Color.Gray * alpha;
            
            // Label
            batch.DrawString(Game1.smallFont, Label, 
                new Vector2(Bounds.X, Bounds.Y - 25), Color.Black * alpha);
            
            // Value
            string valueText = _value.ToString("F2");
            var valueSize = Game1.smallFont.MeasureString(valueText);
            batch.DrawString(Game1.smallFont, valueText,
                new Vector2(Bounds.Right - valueSize.X, Bounds.Y - 25), Color.Black * alpha);
            
            // Track
            var trackRect = new Rectangle(Bounds.X, Bounds.Y + 8, Bounds.Width, 4);
            batch.Draw(Game1.staminaRect, trackRect, Color.DarkGray * alpha);
            
            // Knob
            float percent = (_value - _min) / (_max - _min);
            int knobX = Bounds.X + (int)(percent * Bounds.Width) - 6;
            var knobRect = new Rectangle(knobX, Bounds.Y, 12, 20);
            batch.Draw(Game1.staminaRect, knobRect, color);
        }
    }
    
    public class SimpleDropdown : SimpleUIElement
    {
        private List<string> _options;
        private int _selectedIndex;
        private bool _isOpen;
        private Action<string> _onChange;
        
        public string SelectedValue => _selectedIndex >= 0 ? _options[_selectedIndex] : null;
        
        public SimpleDropdown(string label, List<string> options, string current, Action<string> onChange)
        {
            Label = label;
            _options = options;
            _selectedIndex = Math.Max(0, options.IndexOf(current));
            _onChange = onChange;
        }
        
        public override void HandleClick(int x, int y)
        {
            if (!IsEnabled) return;
            
            // Check main button
            if (Bounds.Contains(x, y))
            {
                _isOpen = !_isOpen;
                return;
            }
            
            // Check dropdown options if open
            if (_isOpen)
            {
                for (int i = 0; i < _options.Count; i++)
                {
                    var optionRect = new Rectangle(Bounds.X, Bounds.Bottom + i * 30, Bounds.Width, 30);
                    if (optionRect.Contains(x, y))
                    {
                        _selectedIndex = i;
                        _onChange?.Invoke(_options[i]);
                        _isOpen = false;
                        break;
                    }
                }
            }
        }
        
        public override void HandleDrag(int x, int y) { }
        
        public override void Draw(SpriteBatch batch, float alpha = 1f)
        {
            var color = IsEnabled ? Color.White * alpha : Color.Gray * alpha;
            
            // Label
            batch.DrawString(Game1.smallFont, Label,
                new Vector2(Bounds.X, Bounds.Y - 25), Color.Black * alpha);
            
            // Main button
            batch.Draw(Game1.staminaRect, Bounds, color);
            
            // Selected text
            string text = SelectedValue ?? "Select...";
            batch.DrawString(Game1.smallFont, text,
                new Vector2(Bounds.X + 5, Bounds.Y + 5), Color.Black * alpha);
            
            // Arrow
            batch.DrawString(Game1.smallFont, _isOpen ? "▲" : "▼",
                new Vector2(Bounds.Right - 20, Bounds.Y + 5), Color.Black * alpha);
        }
        
        public void DrawOverlay(SpriteBatch batch, float alpha = 1f)
        {
            if (!_isOpen || !IsEnabled) return;
            
            // Draw dropdown options on top of everything else
            for (int i = 0; i < _options.Count; i++)
            {
                var rect = new Rectangle(Bounds.X, Bounds.Bottom + i * 30, Bounds.Width, 30);
                var bgColor = i == _selectedIndex ? Color.LightBlue : Color.LightGray;
                
                batch.Draw(Game1.staminaRect, rect, bgColor * alpha);
                batch.DrawString(Game1.smallFont, _options[i],
                    new Vector2(rect.X + 5, rect.Y + 5), Color.Black * alpha);
            }
        }
    }
    
    public class SimpleCheckbox : SimpleUIElement
    {
        private bool _isChecked;
        private Action<bool> _onChange;
        
        public bool IsChecked => _isChecked;

        public void SetChecked(bool isChecked)
        {
            _isChecked = isChecked;
        }
        
        public SimpleCheckbox(string label, bool isChecked, Action<bool> onChange)
        {
            Label = label;
            _isChecked = isChecked;
            _onChange = onChange;
        }
        
        public override void HandleClick(int x, int y)
        {
            if (Bounds.Contains(x, y) && IsEnabled)
            {
                _isChecked = !_isChecked;
                _onChange?.Invoke(_isChecked);
            }
        }
        
        public override void HandleDrag(int x, int y) { }
        
        public override void Draw(SpriteBatch batch, float alpha = 1f)
        {
            var color = IsEnabled ? Color.White * alpha : Color.Gray * alpha;
            
            // Checkbox
            var checkRect = new Rectangle(Bounds.X, Bounds.Y, 24, 24);
            batch.Draw(Game1.staminaRect, checkRect, color);
            
            if (_isChecked)
            {
                batch.DrawString(Game1.smallFont, "✓",
                    new Vector2(checkRect.X + 4, checkRect.Y), Color.Green * alpha);
            }
            
            // Label
            batch.DrawString(Game1.smallFont, Label,
                new Vector2(checkRect.Right + 10, Bounds.Y + 2), Color.Black * alpha);
        }
    }
    
    public class SimpleButton : SimpleUIElement
    {
        private Action<SimpleButton> _onClick;
        private Color _color;
        
        public SimpleButton(string label, Color color, Action<SimpleButton> onClick)
        {
            Label = label;
            _color = color;
            _onClick = onClick;
        }
        
        public override void HandleClick(int x, int y)
        {
            if (Bounds.Contains(x, y) && IsEnabled)
            {
                _onClick?.Invoke(this);
            }
        }
        
        public override void HandleDrag(int x, int y) { }
        
        public override void Draw(SpriteBatch batch, float alpha = 1f)
        {
            var color = IsEnabled ? _color * alpha : Color.Gray * alpha;
            
            batch.Draw(Game1.staminaRect, Bounds, color);
            
            var textSize = Game1.smallFont.MeasureString(Label);
            var textPos = new Vector2(
                Bounds.X + (Bounds.Width - textSize.X) / 2,
                Bounds.Y + (Bounds.Height - textSize.Y) / 2
            );
            
            batch.DrawString(Game1.smallFont, Label, textPos, Color.White * alpha);
        }
    }
    
    // Main UI container
    public class SimpleUIPanel
    {
        private List<SimpleUIElement> _elements = new();
        private List<SimpleDropdown> _dropdowns = new(); // Track separately for overlay rendering
        private List<SimpleClickableText> _clickableTexts = new(); // Track for hover updates
        private Rectangle _bounds;
        private string _title;
        
        public SimpleUIPanel(string title, Rectangle bounds)
        {
            _title = title;
            _bounds = bounds;
        }
        
        public void AddElement(SimpleUIElement element)
        {
            _elements.Add(element);
            
            if (element is SimpleDropdown dropdown)
                _dropdowns.Add(dropdown);
            else if (element is SimpleClickableText clickableText)
                _clickableTexts.Add(clickableText);
        }
        
        public void UpdateHover(int mouseX, int mouseY)
        {
            for (int i = 0; i < _clickableTexts.Count; i++)
            {
                _clickableTexts[i].UpdateHover(mouseX, mouseY);
            }
        }
        
        public void HandleClick(int x, int y)
        {
            // Handle dropdowns first (in reverse order for proper overlay behavior)
            for (int i = _dropdowns.Count - 1; i >= 0; i--)
            {
                _dropdowns[i].HandleClick(x, y);
            }
            
            // Then other elements
            
            for (int i = 0; i < _elements.Count; i++)
            {
                if (!(_elements[i] is SimpleDropdown))
                    _elements[i].HandleClick(x, y);
            }
        }
        
        public void HandleDrag(int x, int y)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                _elements[i].HandleDrag(x, y);
            }
        }
        
        public void HandleRelease()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i] is SimpleSlider slider)
                    slider.Release();
            }
        }
        
        public void Draw(SpriteBatch batch)
        {
            // Draw panel background
            Game1.drawDialogueBox(_bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, false, true);
            
            // Draw title
            var titleSize = Game1.dialogueFont.MeasureString(_title);
            batch.DrawString(Game1.dialogueFont, _title,
                new Vector2(_bounds.X + (_bounds.Width - titleSize.X) / 2, _bounds.Y + 20),
                Color.Black);
            
            // Draw all elements
            foreach (var element in _elements)
            {
                element.Draw(batch);
            }
            
            // Draw dropdown overlays on top
            foreach (var dropdown in _dropdowns)
            {
                dropdown.DrawOverlay(batch);
            }
        }
    }
}