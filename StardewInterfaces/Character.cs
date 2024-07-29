using Microsoft.Xna.Framework;
using Raylib_cs;
using raylibExtras;
using StardewValley;

namespace StardewValley3D.StardewInterfaces;

public class Character
{
    public Vector2 WorldPosition => Game1.player.Position;
    public int WorldRotation => Game1.player.FacingDirection;
    private int CurrentRotation = 0;
    public rlFPCamera Camera;

    public Character()
    {
        Camera = new();
        Camera.Setup(60, new System.Numerics.Vector3(WorldPosition.X, 2.0f, WorldPosition.Y));
        Camera.MoveSpeed.Z = 0;
        Camera.MoveSpeed.X = 0;
        Camera.FarPlane = 5000;

    }

    public void UpdateCamera()
    {
        Camera.ViewCamera.Position = new System.Numerics.Vector3(WorldPosition.X, 62.0f, WorldPosition.Y);
        if(WorldRotation == 0)
            Camera.ViewCamera.Target = Raymath.Vector3Add(Camera.ViewCamera.Position, new System.Numerics.Vector3(0.0f, 0.0f, -10f));
        else if (WorldRotation == 1)
            Camera.ViewCamera.Target = Raymath.Vector3Add(Camera.ViewCamera.Position, new System.Numerics.Vector3(10f, 0.0f, 0.0f));
        else if(WorldRotation == 2)
            Camera.ViewCamera.Target = Raymath.Vector3Add(Camera.ViewCamera.Position, new System.Numerics.Vector3(0.0f, 0.0f, 10f));
        else if (WorldRotation == 3)
            Camera.ViewCamera.Target = Raymath.Vector3Add(Camera.ViewCamera.Position, new System.Numerics.Vector3(-10f, 0.0f, 0f));
    }

    public void DoInput()
    {
        Vector2 mInput = new(Raylib.IsKeyDown(KeyboardKey.D) ? 1 : Raylib.IsKeyDown(KeyboardKey.A) ? -1 : 0.0f,
            Raylib.IsKeyDown(KeyboardKey.W) ? 1 : Raylib.IsKeyDown(KeyboardKey.S) ? -1 : 0.0f);
        
        Game1.player.movementDirections.Clear();
        if (Game1.player.canMove)
        {
            /*if (Raylib.IsKeyDown(KeyboardKey.D))
                Game1.player.movementDirections.Add(1);
            if (Raylib.IsKeyDown(KeyboardKey.A))
                Game1.player.movementDirections.Add(3);
            if (Raylib.IsKeyDown(KeyboardKey.W))
                Game1.player.movementDirections.Add(0);
            if (Raylib.IsKeyDown(KeyboardKey.S))
                Game1.player.movementDirections.Add(2);
                */

            if (Raylib.IsKeyPressed(KeyboardKey.E))
                CurrentRotation = CurrentRotation + 1 > 3 ? 0 : CurrentRotation + 1;
            if (Raylib.IsKeyPressed(KeyboardKey.Q))
                CurrentRotation = CurrentRotation - 1 < 0 ? 3 : CurrentRotation - 1;

            Game1.player.FacingDirection = CurrentRotation;

            switch (CurrentRotation)
            {
                case 0:
                    Game1.player.xVelocity = mInput.X;
                    Game1.player.yVelocity = mInput.Y;
                    break;
                case 2:
                    Game1.player.xVelocity = -mInput.X;
                    Game1.player.yVelocity = -mInput.Y;
                    break;
                case 1:
                    Game1.player.xVelocity = mInput.Y;
                    Game1.player.yVelocity = -mInput.X;
                    break;
                case 3:
                    Game1.player.xVelocity = -mInput.Y;
                    Game1.player.yVelocity = mInput.X;
                    break;
            }

            if (Game1.player.xVelocity < 0)
                Game1.player.movementDirections.Add(3);
            if (Game1.player.xVelocity > 0)
                Game1.player.movementDirections.Add(1);
            if (Game1.player.yVelocity < 0)
                Game1.player.movementDirections.Add(2);
            if (Game1.player.yVelocity > 0)
                Game1.player.movementDirections.Add(0);

            Game1.player.FacingDirection = CurrentRotation;
        }
        else
        {
            Game1.eventUp = false;
        }
        //Game1.player.FacingDirection = Camera.Target.z;
    }
}