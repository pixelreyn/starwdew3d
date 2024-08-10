using ILGPU;
using ILGPU.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley3D.StardewInterfaces;
using StardewValley3D.Structures;

namespace StardewValley3D.Rendering;

class Renderer
{
    private Framebuffer _framebuffer;
    private Camera3D _camera;
    private Accelerator _accelerator;

    private Action<Index2D, ArrayView<FlattenedBVHNode>, ArrayView<Color>,
        ArrayView<Light>, ArrayView<Color>, Matrix, Matrix, Vector2, Vector3> _loadedKernel;

    private MemoryBuffer1D<Color, Stride1D.Dense> _deviceOutput;

    private MemoryBuffer1D<FlattenedBVHNode, Stride1D.Dense>? _bvhBuffer;
    private MemoryBuffer1D<Light, Stride1D.Dense>? _lightsBuffer;
    private MemoryBuffer1D<Color, Stride1D.Dense>? _colorBuffer;
    private List<Color> _concatenatedTextures = new List<Color>();
    private Dictionary<string, int> _textureStartIndices = new Dictionary<string, int>();
    private bool _buffersNeedReallocation = true;

    public Renderer(Framebuffer framebuffer, Camera3D camera)
    {
        _framebuffer = framebuffer;
        _camera = camera;
        Context context = Context.Create(builder => builder.Default().EnableAlgorithms());
        _accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);
        _loadedKernel =
            _accelerator
                .LoadAutoGroupedStreamKernel<Index2D, ArrayView<FlattenedBVHNode>, ArrayView<Color>,
                    ArrayView<Light>, ArrayView<Color>, Matrix, Matrix,
                    Vector2, Vector3>(RaytraceKernel);

        _deviceOutput = _accelerator.Allocate1D<Color>(framebuffer.Texture.Width * framebuffer.Texture.Height);
    }

    public void ClearTextures()
    {
        _concatenatedTextures.Clear();
        _textureStartIndices.Clear();
        _colorBuffer?.Dispose();
        _buffersNeedReallocation = true;
    }

    public void Render(FlattenedBVHNode[] nodes, List<Light> lights)
    {
        if (Game1.CurrentEvent != null && !Game1.CurrentEvent.playerControlSequence)
            return;

        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        var localNodeList = nodes;
        
        for (int i = 0; i < localNodeList.Length; i++)
        {
            if (localNodeList[i].IsLeaf == 0)
                continue;
            
            int textureStartIndex = -1;
                
            if (localNodeList[i].Object.TextureSet == 1 && 
                RenderingData.TextureStringId.TryGetValue(localNodeList[i].Object.TileIndex, out var tileIndexString) &&
                !_textureStartIndices.TryGetValue(tileIndexString, out textureStartIndex))
            {
                textureStartIndex = _concatenatedTextures.Count;
                _concatenatedTextures.AddRange(RenderingData.TextureCache[tileIndexString]);
                if(!tileIndexString.Contains("Sprite"))
                    _textureStartIndices[tileIndexString] = textureStartIndex;
            }

            localNodeList[i].Object.TextureStartIndex = textureStartIndex;
        }
        _colorBuffer?.Dispose();
        _colorBuffer = _accelerator.Allocate1D(_concatenatedTextures.ToArray());
        _buffersNeedReallocation = false;
        
        _bvhBuffer = _accelerator.Allocate1D(localNodeList.ToArray());
        _lightsBuffer = _accelerator.Allocate1D(lights.ToArray());

        _framebuffer.Clear(Color.Black);
        _loadedKernel(
            new Index2D(_framebuffer.Texture.Width, _framebuffer.Texture.Height),
            _bvhBuffer.View,
            _colorBuffer.View,
            _lightsBuffer.View,
            _deviceOutput.View,
            _camera.ViewMatrix,
            _camera.ProjectionMatrix,
            new Vector2(_framebuffer.Texture.Width, _framebuffer.Texture.Height),
            _camera.Position
        );
        _accelerator.Synchronize();
        _framebuffer.ColorBuffer = _deviceOutput.GetAsArray1D();
        _framebuffer.UpdateTexture();
        
        _bvhBuffer?.Dispose();
        _lightsBuffer?.Dispose();

        Game1.spriteBatch.Draw(_framebuffer.Texture, Game1.game1.screen.Bounds, Color.White);
        
        sw.Stop();
        //Console.WriteLine($"Render Step took {sw.ElapsedMilliseconds}");
        //RenderSpritesOnCPU(dynamicSprites);
    }

    static void RaytraceKernel(Index2D index, ArrayView<FlattenedBVHNode> bvhNodes,
        ArrayView<Color> concatenatedTextures, ArrayView<Light> lights, ArrayView<Color> output,
        Matrix viewMatrix, Matrix projectionMatrix, Vector2 screenSize, Vector3 cameraPosition)
    {
        int outputIndex = index.Y * (int)screenSize.X + index.X;

        // Calculate normalized device coordinates (NDC)
        Vector2 ndc = new Vector2(
            (2.0f * index.X) / screenSize.X - 1.0f,
            1.0f - (2.0f * index.Y) / screenSize.Y
        );

        // Transform from clip space to world space
        Vector4 clipCoords = new Vector4(ndc, -1.0f, 1.0f);
        Vector4 viewCoords = Vector4.Transform(clipCoords, Matrix.Invert(projectionMatrix));
        viewCoords.Z = -1.0f;
        viewCoords.W = 0.0f;

        Vector4 worldCoords = Vector4.Transform(viewCoords, Matrix.Invert(viewMatrix));
        Vector3 rayDirection = Vector3.Normalize(new Vector3(worldCoords.X, worldCoords.Y, worldCoords.Z));

        float minDistance = float.MaxValue;
        Color finalColor = new Color(0, 0, 0, 0);
        bool hit = false;
        
        // Traverse BVH for object instances
        int[] stack = new int[64]; // Fixed size stack for BVH traversal
        int stackSize = 0;
        stack[stackSize++] = 0; // Start with the root node

        while (stackSize > 0)
        {
            int currentNodeIndex = stack[--stackSize];
            FlattenedBVHNode cnode = bvhNodes[currentNodeIndex];
            if (!RayBoxIntersectGpt(cameraPosition, rayDirection, cnode.Bounds.Min, cnode.Bounds.Max,
                    out var boxHitDistance) || boxHitDistance > minDistance)
                continue;

            if (cnode.IsLeaf == 1)
            {
                var node = cnode.Object;
                // Test intersection with each object in the leaf node
                Vector3 boxMin = node.Position - node.Size / 2.0f;
                Vector3 boxMax = node.Position + node.Size / 2.0f;
                if (node.ObjectType == ObjectType.Sprite)
                {
                    // Modify ray intersection logic for billboards
                    if (RayIntersectsSprite(cameraPosition, rayDirection, node, out float distance))
                    {
                        Vector3 hitPoint = cameraPosition + rayDirection * distance;
                        // Calculate texture coordinates for sprites
                        Vector2 uv = CalculateBillboardTextureCoordinates(hitPoint, node, cameraPosition);
                        uv = Vector2.Clamp(uv, Vector2.Zero, Vector2.One);

                        // Existing texture sampling and lighting code...

                        Color textureColor = node.TextureStartIndex == -1
                            ? node.Color
                            : SampleTexture(concatenatedTextures, uv, node.TextureWidth,
                                node.TextureHeight,
                                node.TextureStartIndex);
                        if (textureColor.A > 0 && distance < minDistance)
                        {
                            finalColor = textureColor;
                            minDistance = distance;
                            hit = true;
                        }
                    }
                }
                else if (RayBoxIntersectGpt(cameraPosition, rayDirection, boxMin, boxMax, out float distance))
                {
                    // Determine the hit point and normal
                    Vector3 hitPoint = cameraPosition + rayDirection * distance;
                    Vector3 normal = Vector3.Normalize(node.GetNormal(hitPoint));

                    // Calculate texture coordinates for the hit point
                    Vector2 uv = CalculateTextureCoordinates(hitPoint, boxMin, boxMax, normal, node);

                    // Clamp UV coordinates to avoid out-of-bounds access
                    uv = Vector2.Clamp(uv, Vector2.Zero, Vector2.One);

                    // Sample the texture
                    Color textureColor = node.TextureStartIndex == -1
                        ? node.Color
                        : SampleTexture(concatenatedTextures, uv, node.TextureWidth,
                            node.TextureHeight,
                            node.TextureStartIndex);

                    if (textureColor.A > 0 && distance < minDistance)
                    {
                        // If the texture is not transparent and the object is closer, update the hit parameters
                        Vector3 viewDirection = Vector3.Normalize(cameraPosition - hitPoint);
                        finalColor = CalculateLighting(textureColor, hitPoint, normal, viewDirection, lights);
                        minDistance = distance;
                        hit = true;
                    }
                }

            }
            else
            {
                // Add child nodes to the stack for further traversal
                if (cnode.RightChildIndex != -1)
                    stack[stackSize++] = cnode.RightChildIndex;
                if (cnode.LeftChildIndex != -1)
                    stack[stackSize++] = cnode.LeftChildIndex;
            }
        }

        // Write the final color to the output buffer if a hit was detected and depth test passes
        if (hit)
        {
            output[outputIndex] = finalColor;
        }
        else if (!hit)
        {
            output[outputIndex] = new Color(0, 0, 0, 0);
        }
    }
    


    static bool RayIntersectsSprite(Vector3 rayOrigin, Vector3 rayDirection, WorldObject sprite, out float distance)
    {
        // Calculate the intersection with a plane facing the camera
        Vector3 planeNormal = Vector3.Normalize(rayOrigin - sprite.Position);
        float denom = Vector3.Dot(planeNormal, rayDirection);
        distance = 0;

        if (Math.Abs(denom) > 1e-6)
        {
            Vector3 pointOnPlane = sprite.Position;
            distance = Vector3.Dot(pointOnPlane - rayOrigin, planeNormal) / denom;

            // Ensure intersection is within sprite's bounds
            if (distance >= 0)
            {
                Vector3 intersectionPoint = rayOrigin + distance * rayDirection;
                Vector3 localIntersection = intersectionPoint - sprite.Position;

                // Check if the intersection point is within the sprite's boundaries
                float halfWidth = sprite.Size.X / 2.0f;
                float halfHeight = sprite.Size.Y / 2.0f;

                if (Math.Abs(localIntersection.X) <= halfWidth && Math.Abs(localIntersection.Y) <= halfHeight)
                {
                    return true;
                }
            }
        }

        return false;
    }

    static Vector2 CalculateBillboardTextureCoordinates(Vector3 hitPoint, WorldObject sprite, Vector3 cameraPosition)
    {
        // Compute the direction from the camera to the sprite
        Vector3 cameraToSprite = sprite.Position - cameraPosition;
        cameraToSprite.Y = 0; // Ignore vertical offset for simplicity

        // Calculate right and up vectors based on camera orientation
        Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, cameraToSprite));
        Vector3 up = Vector3.Up; // Always keep up vector as Y-axis

        // Calculate local hit point relative to the sprite's position
        Vector3 localHitPoint = hitPoint - sprite.Position;

        float halfWidth = sprite.Size.X / 2.0f;
        float halfHeight = sprite.Size.Y / 2.0f;

        // Clamp localHitPoint to ensure it's within the sprite's bounding box
        localHitPoint.X = MathHelper.Clamp(localHitPoint.X, -halfWidth, halfWidth);
        localHitPoint.Y = MathHelper.Clamp(localHitPoint.Y, -halfHeight, halfHeight);

        // Map local hit point to UV coordinates in the sprite's texture space
        float u = (Vector3.Dot(localHitPoint, right) / sprite.Size.X) + 0.49f;
        float v = (Vector3.Dot(localHitPoint, up) / sprite.Size.Y) + 0.5f;

        // Adjust UVs to ensure they map correctly onto the sprite's texture
        return new Vector2(u, 1 - v);
    }

    static Vector2 CalculateTextureCoordinates(Vector3 hitPoint, Vector3 boxMin, Vector3 boxMax, Vector3 normal,
        WorldObject obj)
    {
        float u = 0.0f;
        float v = 0.0f;

        // Determine which face was hit based on the normal vector
        if ((obj.ObjectType == ObjectType.Tile || obj.ShouldRenderTop()) && normal == Vector3.Up ||
            normal == Vector3.Down)
        {
            // Top or Bottom face
            u = (hitPoint.X - boxMin.X) / (boxMax.X - boxMin.X);
            v = (hitPoint.Z - boxMin.Z) / (boxMax.Z - boxMin.Z);
        }
        else if (normal == Vector3.Left || normal == Vector3.Right)
        {
            // Left or Right face
            u = (hitPoint.Z - boxMin.Z) / (boxMax.Z - boxMin.Z);
            v = (hitPoint.Y - boxMin.Y) / (boxMax.Y - boxMin.Y);
            v = 1 - v;
        }
        else if (normal == Vector3.Forward || normal == Vector3.Backward)
        {
            // Front or Back face
            u = (hitPoint.X - boxMin.X) / (boxMax.X - boxMin.X);
            v = (hitPoint.Y - boxMin.Y) / (boxMax.Y - boxMin.Y);
            v = 1 - v;
        }

        return new Vector2(u, v);
    }

    static Color SampleTexture(ArrayView<Color> texture, Vector2 uv, int textureWidth, int textureHeight,
        int textureIndex)
    {
        // Calculate the pixel position in the texture
        int x = (int)(uv.X * textureWidth) % textureWidth;
        int y = (int)(uv.Y * textureHeight) % textureHeight;

        // Retrieve the color from the texture at the calculated position
        int index = y * textureWidth + x;

        if (index < 0 || index >= texture.Length || x < 0 || y < 0 || x >= textureWidth || y >= textureHeight)
            return new Color(0, 0, 0, 0); // Default color if out of bounds

        return texture[textureIndex + index];
    }

    static Color CalculateLighting(Color textureColor, Vector3 hitPoint, Vector3 normal, Vector3 viewDirection,
        ArrayView<Light> lights)
    {
        // Constants for the lighting model
        float ambientStrength = 0.2f;
        float diffuseStrength = 0.6f;
        float specularStrength = 0.4f;
        int shininess = 32;

        // Initialize color components
        Vector3 ambientColor = new Vector3(textureColor.R, textureColor.G, textureColor.B) * ambientStrength;
        Vector3 diffuseColor = Vector3.Zero;
        Vector3 specularColor = Vector3.Zero;

        // Calculate lighting for each light source
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            Vector3 lightDir = light.IsDirectional == 1
                ? Vector3.Normalize(-light.Direction)
                : Vector3.Normalize(light.Position - hitPoint);

            // No attenuation for directional lights
            float attenuation = light.IsDirectional == 1
                ? 1.0f
                : 1.0f / (1.0f + 0.1f * Vector3.Distance(light.Position, hitPoint));

            // Diffuse lighting
            float diff = Math.Max(Vector3.Dot(normal, lightDir), 0.0f);
            diffuseColor += new Vector3(textureColor.R, textureColor.G, textureColor.B) * diff * diffuseStrength *
                            light.Intensity * attenuation;

            // Specular lighting
            Vector3 reflectDir = Vector3.Reflect(-lightDir, normal);
            float spec = (float)Math.Pow(Math.Max(Vector3.Dot(viewDirection, reflectDir), 0.0f), shininess);
            specularColor += new Vector3(light.Color.R, light.Color.G, light.Color.B) * spec * specularStrength *
                             light.Intensity * attenuation;
        }

        // Combine all lighting components
        Vector3 finalColor = ambientColor + diffuseColor + specularColor;
        finalColor = Vector3.Clamp(finalColor / 255.0f, Vector3.Zero, Vector3.One);

        return new Color(finalColor.X, finalColor.Y, finalColor.Z, textureColor.A);
    }

    static bool RayBoxIntersectGpt(Vector3 rayOrigin, Vector3 rayDir, Vector3 boxMin, Vector3 boxMax,
        out float tMin)
    {
        Vector3 invDir = new Vector3(1 / rayDir.X, 1 / rayDir.Y, 1 / rayDir.Z);
        Vector3 t0 = (boxMin - rayOrigin) * invDir;
        Vector3 t1 = (boxMax - rayOrigin) * invDir;

        Vector3 tmin = Vector3.Min(t0, t1);
        Vector3 tmax = Vector3.Max(t0, t1);

        float tentry = MathF.Max(tmin.X, MathF.Max(tmin.Y, tmin.Z));
        float texit = MathF.Min(tmax.X, MathF.Min(tmax.Y, tmax.Z));

        // Check if the ray originates inside the box
        if (tentry < 0.0 && texit > 0.0)
        {
            tMin = 0.0f; // The ray originates inside the box
            return true;
        }

        // Check for intersection
        if (tentry > texit || texit < 0.0)
        {
            tMin = 0;
            return false; // No intersection
        }

        tMin = tentry; // Distance to the nearest intersection
        return true;
    }
}
