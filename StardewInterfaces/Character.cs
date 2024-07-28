using Microsoft.Xna.Framework;
using Raylib_cs;
using StardewValley;

namespace StardewValley3D.StardewInterfaces;

public class Character
{
    public Vector2 WorldPosition => Game1.player.Position;
    public int WorldRotation => Game1.player.FacingDirection;
    public Camera3D Camera;

    public Character()
    {
        Camera = new();
        Camera.Position = new System.Numerics.Vector3(WorldPosition.X, 2.0f, WorldPosition.Y);
        Camera.Target = Raymath.Vector3Add(Camera.Position, new System.Numerics.Vector3(0.0f, 0.0f, 10f));
        Camera.Up = new System.Numerics.Vector3(0.0f, 1.0f, 0.0f);
        Camera.FovY = 60.0f;
        Camera.Projection = CameraProjection.Perspective;
    }

    public void UpdateCamera()
    {
        Camera.Position = new System.Numerics.Vector3(WorldPosition.X, 62.0f, WorldPosition.Y);
        if(WorldRotation == 0)
            Camera.Target = Raymath.Vector3Add(Camera.Position, new System.Numerics.Vector3(0.0f, 0.0f, -10f));
        else if (WorldRotation == 1)
            Camera.Target = Raymath.Vector3Add(Camera.Position, new System.Numerics.Vector3(10f, 0.0f, 0.0f));
        else if(WorldRotation == 2)
            Camera.Target = Raymath.Vector3Add(Camera.Position, new System.Numerics.Vector3(0.0f, 0.0f, 10f));
        else if (WorldRotation == 3)
            Camera.Target = Raymath.Vector3Add(Camera.Position, new System.Numerics.Vector3(-10f, 0.0f, 0f));
    }

    public void DoInput()
    {
        Vector2 mInput = new(Raylib.IsKeyDown(KeyboardKey.D) ? 1 : Raylib.IsKeyDown(KeyboardKey.A) ? -1 : 0.0f,
            Raylib.IsKeyDown(KeyboardKey.W) ? 1 : Raylib.IsKeyDown(KeyboardKey.S) ? -1 : 0.0f);
        
        Game1.player.movementDirections.Clear();
        if (Game1.player.canMove)
        {
            if (Raylib.IsKeyDown(KeyboardKey.D))
                Game1.player.movementDirections.Add(1);
            if (Raylib.IsKeyDown(KeyboardKey.A))
                Game1.player.movementDirections.Add(3);
            if (Raylib.IsKeyDown(KeyboardKey.W))
                Game1.player.movementDirections.Add(0);
            if (Raylib.IsKeyDown(KeyboardKey.S))
                Game1.player.movementDirections.Add(2);

            Game1.player.xVelocity = mInput.X;
            Game1.player.yVelocity = mInput.Y;
        }
        else
        {
            Game1.eventUp = false;
        }
        //Game1.player.FacingDirection = Camera.Target.z;
    }
}