using Microsoft.Xna.Framework;
using Vector3 = Microsoft.Xna.Framework.Vector3;

namespace StardewValley3D.Rendering;

public class Camera
{
    public Vector3 Position { get; set; }
    public Matrix ViewMatrix { get; set; }
    public Matrix ProjectionMatrix { get; set; }

    public Camera(Vector3 position, Matrix viewMatrix, Matrix projectionMatrix)
    {
        Position = position;
        ViewMatrix = viewMatrix;
        ProjectionMatrix = projectionMatrix;
    }

    public static Matrix CreatePerspectiveFieldOfView(float fov, float aspect, float near, float far)
    {
        return Matrix.CreatePerspectiveFieldOfView(fov, aspect, near, far);
    }

    public static Matrix CreateLookAt(Vector3 cameraPosition, Vector3 cameraTarget, Vector3 cameraUpVector)
    {
        
        return Matrix.CreateLookAt(cameraPosition, cameraTarget, cameraUpVector);
    }
}