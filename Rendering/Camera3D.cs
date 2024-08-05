using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewValley;

namespace StardewValley3D.Rendering;

public class Camera3D
{
    private Vector3 _forward;
    private Vector3 _up;
    private float _yaw; // Y-axis rotation
    private float _pitch; // X-axis rotation

    private Matrix _viewMatrix;
    private Matrix _projectionMatrix;

    public Matrix ViewMatrix => _viewMatrix;
    public Matrix ProjectionMatrix => _projectionMatrix;
    public float Yaw => _yaw;
    public float Pitch => _pitch;

    public Vector3 Position;

    public Camera3D(Vector3 startPosition)
    {
        Position = startPosition;
        _forward = Vector3.Forward;
        _up = Vector3.Up;
        _yaw = 0;
        _pitch = 0;
        UpdateProjectionMatrix();
        UpdateViewMatrix();
    }

    public void UpdateCamera(GameTime gameTime)
    {
        HandleMouseInput();
        UpdateViewMatrix();
    }

    private void HandleMouseInput()
    {
        if (!Game1.game1.IsActive || Game1.activeClickableMenu != null)
            return;

        MouseState mouseState = Mouse.GetState();
        
        //Set mouse to corner so that it's far enough from player to force toolbox forward
        int centerX = 32;
        int centerY = 32;

        // Determine mouse movement delta
        int mouseX = mouseState.X - centerX;
        int mouseY = mouseState.Y - centerY;

        // Define sensitivity for rotation
        float sensitivity = 0.002f; // Adjust as needed

        // Calculate yaw and pitch from mouse movement
        _yaw += mouseX * sensitivity;
        _pitch -= mouseY * sensitivity;

        // Clamp yaw to wrap between -180 and 180 degrees
        _yaw = ClampAngle(_yaw);

        // Constrain pitch to avoid flipping
        _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2 + 0.1f, MathHelper.PiOver2 - 0.1f);

        // Update the mouse position to be the center of the screen
        Mouse.SetPosition(centerX, centerY);
    }
    
    private float ClampAngle(float angle)
    {
        while (angle > MathHelper.Pi) angle -= MathHelper.TwoPi;
        while (angle < -MathHelper.Pi) angle += MathHelper.TwoPi;
        return angle;
    }
    
    private void UpdateViewMatrix()
    {
        // Calculate forward vector based on yaw and pitch
        _forward = new Vector3(
            (float)(Math.Cos(_yaw) * Math.Cos(_pitch)),
            (float)Math.Sin(_pitch),
            (float)(Math.Sin(_yaw) * Math.Cos(_pitch))
        );

        // Normalize forward vector
        _forward = Vector3.Normalize(_forward);

        // Update the view matrix
        _viewMatrix = Matrix.CreateLookAt(Position, Position + _forward, _up);
    }

    // Method to get the forward vector from the camera
    public Vector3 GetForward()
    {
        return _forward;
    }

    private void UpdateProjectionMatrix()
    {
        // Update the projection matrix
        float fieldOfView = 70; // 45 degrees field of view
        float aspectRatio = Game1.game1.GraphicsDevice.Viewport.Width / (float)Game1.game1.GraphicsDevice.Viewport.Height;
        _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(fieldOfView * ((3.14159265358f) / 180f), aspectRatio, .05f, 1000f);
    }

    public Vector3 GetRight()
    {
        // Right vector is the cross product of forward and up vectors
        return Vector3.Normalize(Vector3.Cross(_forward, _up));
    }
}
