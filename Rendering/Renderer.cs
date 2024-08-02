using ILGPU;
using ILGPU.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley3D.StardewInterfaces;

namespace StardewValley3D.Rendering;

class Renderer
{
    private Framebuffer framebuffer;
    private Camera camera;
    private Context _context;
    private Accelerator _accelerator;
    private Action<Index2D, ArrayView<WorldTile>, ArrayView<Color>, Matrix, Matrix, Vector2, Vector3> _loadedKernel;
    private MemoryBuffer1D<Color, Stride1D.Dense> _deviceOutput;

    public Renderer(Framebuffer framebuffer, Camera camera)
    {
        this.framebuffer = framebuffer;
        this.camera = camera;
        _context = Context.CreateDefault();
        _accelerator = _context.GetPreferredDevice(preferCPU: false).CreateAccelerator(_context);
        _loadedKernel =
            _accelerator
                .LoadAutoGroupedStreamKernel<Index2D, ArrayView<WorldTile>, ArrayView<Color>, Matrix, Matrix, Vector2,
                    Vector3>(Kernel);
        _deviceOutput = _accelerator.Allocate1D<Color>(framebuffer.Texture.Width * framebuffer.Texture.Height);

    }

    public void RenderA(WorldTile[] tiles)
    {
        framebuffer.Clear(Color.Black);
        MemoryBuffer1D<WorldTile, Stride1D.Dense> gpuTiles = _accelerator.Allocate1D(tiles);
        _loadedKernel(new Index2D(Game1.game1.screen.Width, Game1.game1.screen.Height), gpuTiles.View,
            _deviceOutput.View, camera.ViewMatrix, camera.ProjectionMatrix,
            new Vector2(Game1.game1.screen.Width, Game1.game1.screen.Height), camera.Position);
        _accelerator.Synchronize();
        framebuffer.ColorBuffer = _deviceOutput.GetAsArray1D();
        framebuffer.UpdateTexture();
        gpuTiles.Dispose();
    }


    static void Kernel(Index2D index, ArrayView<WorldTile> tiles, ArrayView<Color> output, Matrix viewMatrix,
        Matrix projectionMatrix, Vector2 screenSize, Vector3 cameraPosition)
    {
        int screenWidth = (int)screenSize.X;
        int screenHeight = (int)screenSize.Y;

        // Calculate normalized device coordinates (NDC)
        Vector2 pixelCoord = new Vector2(index.X, index.Y);
        Vector2 ndc = new Vector2(
            (2.0f * pixelCoord.X) / screenWidth - 1.0f,
            1.0f - (2.0f * pixelCoord.Y) / screenHeight
        );

        // Create clip space coordinates
        Vector4 clipCoords = new Vector4(ndc, -1.0f, 1.0f);

        // Transform from clip space to view space
        Vector4 viewCoords = Vector4.Transform(clipCoords, Matrix.Invert(projectionMatrix));
        viewCoords.Z = -1.0f; // Ray points forward in view space
        viewCoords.W = 0.0f; // Direction vector, not a position

        // Transform from view space to world space
        Vector4 worldCoords = Vector4.Transform(viewCoords, Matrix.Invert(viewMatrix));
        Vector3 rayDirection = Vector3.Normalize(new Vector3(worldCoords.X, worldCoords.Y, worldCoords.Z));

        // Initialize ray intersection parameters
        bool hit = false;
        float minDistance = float.MaxValue;
        Color pixelColor= new Color(0, 0, 0, 0);

        // Check for intersections with all tiles
        for (int i = 0; i < tiles.Length; i++)
        {
            WorldTile tile = tiles[i];

            // AABB intersection test
            Vector3 boxMin = tile.Position - tile.Size / 2.0f;
            Vector3 boxMax = tile.Position + tile.Size / 2.0f;

            if (RayIntersectsAABB(cameraPosition, rayDirection, boxMin, boxMax, out float distance))
            {
                if (distance < minDistance)
                {
                    hit = true;
                    minDistance = distance;
                    pixelColor = tile.Color;
                }
            }
        }

        // Set the output color if hit
        if (hit)
        {
            int outputIndex = index.Y * screenWidth + index.X;
            output[outputIndex] = pixelColor;
        }
        else
        {
            int outputIndex = index.Y * screenWidth + index.X;
            output[outputIndex] = new Color(0, 0, 0, 0);
        }
    }

    static bool RayIntersectsAABB(Vector3 rayOrigin, Vector3 rayDirection, Vector3 boxMin, Vector3 boxMax,
        out float distance)
    {
        float tmin = (boxMin.X - rayOrigin.X) / rayDirection.X;
        float tmax = (boxMax.X - rayOrigin.X) / rayDirection.X;

        if (tmin > tmax) (tmin, tmax) = (tmax, tmin);

        float tymin = (boxMin.Y - rayOrigin.Y) / rayDirection.Y;
        float tymax = (boxMax.Y - rayOrigin.Y) / rayDirection.Y;

        if (tymin > tymax) (tymin, tymax) = (tymax, tymin);

        if ((tmin > tymax) || (tymin > tmax))
        {
            distance = 0;
            return false;
        }

        if (tymin > tmin)
            tmin = tymin;

        if (tymax < tmax)
            tmax = tymax;

        float tzmin = (boxMin.Z - rayOrigin.Z) / rayDirection.Z;
        float tzmax = (boxMax.Z - rayOrigin.Z) / rayDirection.Z;

        if (tzmin > tzmax) (tzmin, tzmax) = (tzmax, tzmin);

        if ((tmin > tzmax) || (tzmin > tmax))
        {
            distance = 0;
            return false;
        }

        if (tzmin > tmin)
            tmin = tzmin;

        if (tzmax < tmax)
            tmax = tzmax;

        distance = tmin;

        return (tmin < tmax) && (tmax > 0);
    }
}