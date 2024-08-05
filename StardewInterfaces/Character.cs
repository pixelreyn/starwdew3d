using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley3D.Rendering;

namespace StardewValley3D.StardewInterfaces;

public class Character
{
    private Vector2 WorldPosition => Game1.player.Position;
    public int WorldRotation => Game1.player.FacingDirection;
    public readonly Camera3D Camera;
    private Vector2 _mInput;
    
    public Character()
    {
        Camera = new Camera3D(new Vector3(WorldPosition.X, 92.0f, WorldPosition.Y));
    }

    public void UpdateCamera()
    {
        Camera.Position = new Vector3(WorldPosition.X, 92.0f, WorldPosition.Y);
        Camera.UpdateCamera(Game1.currentGameTime);
    }

    public void DoInput()
    {       
        Game1.input.IgnoreKeys(new []{Keys.A, Keys.D, Keys.W, Keys.S});
        var kbState = Keyboard.GetState();
        _mInput = new(kbState.IsKeyDown(Keys.D) ? 1 :kbState.IsKeyDown(Keys.A) ? -1 : 0.0f,
            kbState.IsKeyDown(Keys.W) ? 1 : kbState.IsKeyDown(Keys.S) ? -1 : 0.0f);
    }

    public void DoMovement()
    {        
        if (Game1.CurrentEvent != null && !Game1.CurrentEvent.playerControlSequence)
            return;
        if (Game1.player.UsingTool)
            return;
        
        Game1.player.movementDirections.Clear();
        Game1.player.xVelocity = 0;
        Game1.player.yVelocity = 0;
        
        if (!Game1.player.isSitting.Value && Game1.player.canMove && _mInput.X != 0 || _mInput.Y != 0)
        {
            Vector3 inputDirection = new Vector3(_mInput.Y, 0, -_mInput.X);

            // Create rotation matrix based on yaw and pitch
            Matrix rotationMatrix = Matrix.CreateFromYawPitchRoll(Camera.Yaw, Camera.Pitch, 0);

            // Transform input direction to world space using the rotation matrix
            Vector3 transformedDirection = Vector3.Transform(inputDirection, rotationMatrix);
            
            // Normalize the transformed direction to maintain consistent speed
            if (transformedDirection.Length() > 0)
                transformedDirection.Normalize();
            
            // Set the player's velocity based on the transformed direction
            var speed = Game1.player.Speed / 1.5f;
            Game1.player.xVelocity = transformedDirection.X * speed;
            Game1.player.yVelocity = transformedDirection.Z * speed;

            // Update player facing direction based on the transformed direction
            GameChecks();
        }
        
        Game1.player.faceDirection(GetFacingDirectionFromCamera());
        if(Game1.player.Sprite.currentAnimation.Count <= 0)
            Game1.player.animateInFacingDirection(Game1.currentGameTime);
    }
    private void GameChecks()
    {
        Warp w = Game1.currentLocation.isCollidingWithWarp(Game1.player.nextPosition(Game1.player.FacingDirection), Game1.player);
        if (w != null)
        {
            if (Game1.eventUp && Game1.CurrentEvent != null)
            {
                bool? isFestival = Game1.CurrentEvent?.isFestival;
                if (isFestival.HasValue && isFestival.GetValueOrDefault())
                {
                    Game1.CurrentEvent.TryStartEndFestivalDialogue(Game1.player);
                    goto label_5;
                }
            }
            Game1.player.warpFarmer(w, Game1.player.FacingDirection);
            label_5:
            return;
        }
    }
    
    private int GetFacingDirection(Vector3 direction)
    {
        // Use the camera's forward and right vectors
        Vector3 forward = Camera.GetForward();
        Vector3 right = Camera.GetRight();

        // Calculate the dot products between the input direction and the camera's axes
        float forwardDot = Vector3.Dot(direction, forward);
        float rightDot = Vector3.Dot(direction, right);

        // Determine the facing direction based on the greatest dot product
        if (MathF.Abs(forwardDot) > MathF.Abs(rightDot))
        {
            // The direction is more forward/backward
            return forwardDot > 0 ? 0 : 2; // 0 for Forward, 2 for Backward
        }
        else
        {
            // The direction is more left/right
            return rightDot > 0 ? 1 : 3; // 1 for Right, 3 for Left
        }
    }
    
    private int GetFacingDirectionFromCamera()
    {
        // Get the camera's yaw angle in radians
        float yaw = Camera.Yaw;

        // Normalize the yaw to the range [0, 2 * PI]
        yaw = (yaw + MathHelper.TwoPi) % MathHelper.TwoPi;

        // Determine the facing direction based on the yaw angle
        // Dividing the circle into four quadrants
        if (yaw >= 7 * MathHelper.PiOver4 || yaw < MathHelper.PiOver4)
            return 1; // Right (East)
        else if (yaw >= MathHelper.PiOver4 && yaw < 3 * MathHelper.PiOver4)
            return 2; // Forward (North)
        else if (yaw >= 3 * MathHelper.PiOver4 && yaw < 5 * MathHelper.PiOver4)
            return 3; // Left (West)
        else
            return 0; // Backward (South)
    }
    
}