using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley3D.Rendering;

namespace StardewValley3D.StardewInterfaces;

public class Character
{
    public Vector2 WorldPosition => Game1.player.Position;
    public int WorldRotation => Game1.player.FacingDirection;
    private int CurrentRotation = 0;
    public Camera Camera;

    public Character()
    {
        Camera = new(new Vector3(WorldPosition.X, 62.0f, WorldPosition.Y), 
            Camera.CreateLookAt(new Vector3(WorldPosition.X, 62.0f, WorldPosition.Y), new Vector3(WorldPosition.X, 62.0f, WorldPosition.Y + 10.0f), Vector3.UnitY),
            Camera.CreatePerspectiveFieldOfView((float)Math.PI / 4, 800f / 600f, 0.1f, 100f));

    }

    public void UpdateCamera()
    {
        Camera.Position = new Vector3(WorldPosition.X, 62.0f, WorldPosition.Y);
        Vector3 forward = Vector3.Zero;
    
        if (CurrentRotation == 0)
            forward = new Vector3(0, 0, -1); // Looking towards the negative Z direction
        else if (CurrentRotation == 1)
            forward = new Vector3(1, 0, 0);  // Looking towards the positive X direction
        else if (CurrentRotation == 2)
            forward = new Vector3(0, 0, 1);  // Looking towards the positive Z direction
        else if (CurrentRotation == 3)
            forward = new Vector3(-1, 0, 0); // Looking towards the negative X direction

        Vector3 target = Camera.Position + forward;
        Camera.ViewMatrix = Camera.CreateLookAt(Camera.Position, target, Vector3.Up);
    }

    private bool JustEd;
    private bool JustQd;

    public void DoInput()
    {
        Vector2 mInput = new(Game1.input.GetKeyboardState().IsKeyDown(Keys.D) ? 1 : Game1.input.GetKeyboardState().IsKeyDown(Keys.A) ? -1 : 0.0f,
            Game1.input.GetKeyboardState().IsKeyDown(Keys.W) ? 1 : Game1.input.GetKeyboardState().IsKeyDown(Keys.S) ? -1 : 0.0f);
        if (mInput.X != 0 || mInput.Y != 0)
            Game1.freezeControls = true;
        else
            Game1.freezeControls = false;
        
        Game1.player.movementDirections.Clear();
        Game1.player.xVelocity = 0;
        Game1.player.yVelocity = 0;
        if (Game1.player.canMove)
        {

            if (Game1.input.GetKeyboardState().IsKeyDown(Keys.E) && !JustEd)
            {
                CurrentRotation = CurrentRotation + 1 > 3 ? 0 : CurrentRotation + 1;
                JustEd = true;
            }
            else if(JustEd && Game1.input.GetKeyboardState().IsKeyUp(Keys.E))
            {
                JustEd = false;
            }

            if (Game1.input.GetKeyboardState().IsKeyDown(Keys.Q) && !JustQd)
            {
                CurrentRotation = CurrentRotation - 1 < 0 ? 3 : CurrentRotation - 1;
                JustQd = true;
            }
            else if(JustQd && Game1.input.GetKeyboardState().IsKeyUp(Keys.Q))
            {
                JustQd = false;
            }

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