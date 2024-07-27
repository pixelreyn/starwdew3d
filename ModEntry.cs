using System;
using Microsoft.Xna.Framework;
using Raylib_cs;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using Color = Microsoft.Xna.Framework.Color;

namespace StardewValley3D
{
    /// <summary>The mod entry point.</summary>
    internal sealed class ModEntry : Mod
    {
        
        private IModHelper? m_Helper = null;

        private StardewInterfaces.Character? m_Character = null;
        private StardewInterfaces.Map? m_Map = null;
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
            m_Helper = helper;
        }

        private void GameLoopOnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;
            
            m_Character ??= new StardewInterfaces.Character();
            m_Map ??= new StardewInterfaces.Map();
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

                Raylib.SetTargetFPS(60);
                while (!Raylib.WindowShouldClose())
                {
                    if (m_Character != null && m_Map != null)
                    {
                        m_Character.UpdateCamera();
                        Raylib.UpdateCamera(ref m_Character.Camera, cameraMode);
                        Raylib.BeginDrawing();
                        Raylib.ClearBackground(Raylib_cs.Color.White);
                        Raylib.BeginMode3D(m_Character.Camera);
                        m_Map.DrawMap();

                        Raylib.EndMode3D();
                        Raylib.EndDrawing();
                    }
                }

                Raylib.CloseWindow();
            });
        }

        private void DisplayOnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            
        }


        /*********
         ** Private methods
         *********/
        /// <summary>Raised after the player presses a button on the keyboard, controller, or mouse.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event data.</param>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // ignore if player hasn't loaded a save yet
            if (!Context.IsWorldReady)
                return;
            
        }
    }
}