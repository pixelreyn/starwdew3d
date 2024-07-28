using Microsoft.Xna.Framework;
using Raylib_cs;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using Color = Raylib_cs.Color;

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
            helper.Events.Display.RenderedWorld += DisplayOnRenderedWorld;
            helper.Events.Player.Warped += PlayerOnWarped;
            helper.Events.GameLoop.UpdateTicked += GameLoopOnUpdateTicked;
        }

        private void GameLoopOnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

           
        }

        private void PlayerOnWarped(object? sender, WarpedEventArgs e)
        {
            //Update the base map to render here
        }
        
        private void GameLoopOnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.Monitor.Log($"Game Launched", LogLevel.Debug);
            Task.Run(() =>
            {
                Raylib.InitWindow(640, 360, "StardewValley3D");
                CameraMode cameraMode = CameraMode.ThirdPerson;
                
                _mCharacter ??= new StardewInterfaces.Character();
                _mMap ??= new StardewInterfaces.Map();
                Game1.options.pauseWhenOutOfFocus = false;
                
                Raylib.SetTargetFPS(60);
                while (!Raylib.WindowShouldClose())
                {
                    if (_mCharacter != null && _mMap != null)
                    {
                        _mCharacter.DoInput();
                        _mCharacter.UpdateCamera();
                        Raylib.UpdateCamera(ref _mCharacter.Camera, cameraMode);
                        Raylib.BeginDrawing();
                        Raylib.ClearBackground(Color.White);
                        Raylib.BeginMode3D(_mCharacter.Camera);
                        _mMap.DrawMap();
                        Raylib.EndMode3D();
                        Raylib.EndDrawing();
                        //Game1.game1.Instance_Update(Game1.currentGameTime);
                    }
                }

                Raylib.CloseWindow();
            });
        }

        private void DisplayOnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {

        }
    }
}

