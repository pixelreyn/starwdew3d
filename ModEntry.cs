using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Raylib_cs;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley3D.Rendering;
using Color = Microsoft.Xna.Framework.Color;
using Texture2D = Microsoft.Xna.Framework.Graphics.Texture2D;

namespace StardewValley3D
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {

        private StardewInterfaces.Character? _mCharacter;
        private StardewInterfaces.Map? _mMap;
        private Framebuffer? _framebuffer;
        private Renderer? _renderer;

        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += GameLoopOnSaveLoaded;
            helper.Events.Player.Warped += PlayerOnWarped;
            helper.Events.Display.Rendering += DisplayOnRendering;
            helper.Events.GameLoop.UpdateTicking += GameLoopOnUpdateTicking;
            helper.Events.World.DebrisListChanged += WorldOnDebrisListChanged;
            helper.Events.World.ObjectListChanged += WorldOnObjectListChanged;
            helper.Events.World.TerrainFeatureListChanged += WorldOnTerrainFeatureListChanged;
            helper.Events.Display.RenderedWorld += DisplayOnRenderedWorld;
        }

        private void GameLoopOnUpdateTicking(object? sender, UpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            
            if(_mCharacter != null)
                _mCharacter.DoInput();
        }

        private void DisplayOnRendering(object? sender, RenderingEventArgs e)
        {
            
        }


        private void DisplayOnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (_mMap == null || _mCharacter == null || _framebuffer == null || _renderer == null)
                return;
            _mCharacter.UpdateCamera();
            _renderer.RenderA(_mMap.Tiles.ToArray());
            Game1.spriteBatch.Draw(_framebuffer.Texture, Game1.game1.screen.Bounds, Color.White);
        }

        private void WorldOnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
        {
            if(_mMap != null)
                _mMap.GenerateMap();
        }

        private void WorldOnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            if(_mMap != null)
                _mMap.GenerateMap();
        }

        private void WorldOnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
        {
            if(_mMap != null)
                _mMap.GenerateMap();
        }

        private void PlayerOnWarped(object? sender, WarpedEventArgs e)
        {
            //Update the base map to render here
            if(_mMap != null)
                _mMap.GenerateMap();
        }
        
        private void GameLoopOnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Monitor.Log($"Game Launched", LogLevel.Debug);
            
            _mCharacter ??= new StardewInterfaces.Character();
            _mMap ??= new StardewInterfaces.Map();
            _mMap.GenerateMap();
            _framebuffer = new Framebuffer(Game1.graphics.GraphicsDevice, Game1.game1.screen.Width, Game1.game1.screen.Height);
            _framebuffer.Clear(Color.Black);
            _renderer = new Renderer(_framebuffer, _mCharacter.Camera);
        }

    }
}

