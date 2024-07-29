using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Raylib_cs;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewValley3D
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {

        private StardewInterfaces.Character? _mCharacter;
        private StardewInterfaces.Map? _mMap;

        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.Events.GameLoop.SaveLoaded += GameLoopOnSaveLoaded;
            helper.Events.Player.Warped += PlayerOnWarped;
            helper.Events.GameLoop.UpdateTicked += GameLoopOnUpdateTicked;
            helper.Events.World.DebrisListChanged += WorldOnDebrisListChanged;
            helper.Events.World.ObjectListChanged += WorldOnObjectListChanged;
            helper.Events.World.TerrainFeatureListChanged += WorldOnTerrainFeatureListChanged;
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

        private void GameLoopOnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

           
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
            Task.Run(() =>
            {
                Raylib.InitWindow(640, 360, "StardewValley3D");
                Game1.options.pauseWhenOutOfFocus = false;
                
                Raylib.SetTargetFPS(60);
                while (!Raylib.WindowShouldClose())
                {
                    if (_mCharacter != null && _mMap != null)
                    {
                        _mCharacter.DoInput();
                        _mCharacter.UpdateCamera();
                        
                        Raylib.BeginDrawing();
                        Raylib.ClearBackground(Raylib_cs.Color.Black);
        
                        _mCharacter.Camera.BeginMode3D();
                        _mMap.DrawMap();
                        _mCharacter.Camera.EndMode3D();
                        Raylib.EndDrawing();
                    }
                }

                Raylib.CloseWindow();
            });
        }

    }
}

