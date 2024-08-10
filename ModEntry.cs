using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley3D.Rendering;
using StardewValley3D.StardewInterfaces;
using Color = Microsoft.Xna.Framework.Color;

namespace StardewValley3D
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {

        private StardewInterfaces.Character? _mCharacter;
        private World? _world;
        private Framebuffer? _framebuffer;
        private Renderer? _renderer;
        private Light _worldLight;

        private IModHelper _helper;
        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += GameLoopOnSaveLoaded;
            helper.Events.Player.Warped += PlayerOnWarped;
            helper.Events.GameLoop.UpdateTicking += GameLoopOnUpdateTicking;
            helper.Events.World.DebrisListChanged += WorldOnDebrisListChanged;
            helper.Events.World.ObjectListChanged += WorldOnObjectListChanged;
            helper.Events.World.TerrainFeatureListChanged += WorldOnTerrainFeatureListChanged;
            helper.Events.Display.RenderedWorld += DisplayOnRenderedWorld;
            _helper = helper;
        }

        private void GameLoopOnUpdateTicking(object? sender, UpdateTickingEventArgs e)
        {
            if (!Context.IsWorldReady || _mCharacter == null)
                return;
            
            _mCharacter.DoInput();
            _mCharacter.UpdateCamera();
            _mCharacter.DoMovement();
        }

        private void DisplayOnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (_world == null || _framebuffer == null || _renderer == null)
                return;
            
            _world.UpdateCharacters();
            _renderer.Render(_world.GetFlattenedNodes(), new List<Light>(new [] {_worldLight}));
        }

        private void WorldOnTerrainFeatureListChanged(object? sender, TerrainFeatureListChangedEventArgs e)
        {
            RenderingData.ResetData();
            if(_renderer != null)
                _renderer.ClearTextures();
            if(_world != null)
                _world.GenerateMap();
        }

        private void WorldOnObjectListChanged(object? sender, ObjectListChangedEventArgs e)
        {
            RenderingData.ResetData();
            if(_renderer != null)
                _renderer.ClearTextures();
            if(_world != null)
                _world.GenerateMap();
        }

        private void WorldOnDebrisListChanged(object? sender, DebrisListChangedEventArgs e)
        {
            RenderingData.ResetData();
            if(_renderer != null)
                _renderer.ClearTextures();
            if(_world != null)
                _world.GenerateMap();
        }

        private void PlayerOnWarped(object? sender, WarpedEventArgs e)
        {
            //Update the base map to render here
            RenderingData.ResetData();
            if(_renderer != null)
                _renderer.ClearTextures();
            if(_world != null)
                _world.GenerateMap();
        }
        
        private void GameLoopOnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Monitor.Log($"Game Launched", LogLevel.Debug);
            
            RenderingData.ResetData();
            
            _mCharacter ??= new StardewInterfaces.Character();
            
            _framebuffer = new Framebuffer(Game1.graphics.GraphicsDevice, Game1.game1.screen.Width, Game1.game1.screen.Height);
            _framebuffer.Clear(Color.Black);
            
            _renderer = new Renderer(_framebuffer, _mCharacter.Camera);
            _renderer.ClearTextures();
            
            _worldLight = new() { Color = Color.LightYellow, Intensity = 2.0f, Position = new Vector3(128, 1024, 128), Direction = new Vector3(0.5f, -1.0f, 0.5f), IsDirectional = 1};
            
            
            _world ??= new World(_helper);
            _world.GenerateMap();
        }

    }
}

