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
}