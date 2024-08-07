using ILGPU;
using ILGPU.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley3D.StardewInterfaces;

namespace StardewValley3D.Rendering;

class Renderer
{
    private Framebuffer _framebuffer;
    private Camera3D _camera;
    private Accelerator _accelerator;

    private Action<Index2D, ArrayView<BvhNode>, ArrayView<Object3DDataOnly>, ArrayView<Color>,
        ArrayView<Light>, ArrayView<Color>, ArrayView<float>, Matrix, Matrix, Vector2, Vector3> _loadedKernel;


    private MemoryBuffer1D<Color, Stride1D.Dense> _deviceInput;
    private MemoryBuffer1D<Color, Stride1D.Dense> _deviceOutput;

    private MemoryBuffer1D<BvhNode, Stride1D.Dense>? _bvhBuffer;
    private MemoryBuffer1D<Object3DDataOnly, Stride1D.Dense>? _tilesBuffer;
    private MemoryBuffer1D<Light, Stride1D.Dense>? _lightsBuffer;
    private MemoryBuffer1D<Color, Stride1D.Dense>? _colorBuffer;
    private MemoryBuffer1D<float, Stride1D.Dense>? _depthBuffer;
    private List<Color> concatenatedTextures = new List<Color>();
    private Dictionary<string, int> textureStartIndices = new Dictionary<string, int>();
    private float[] _cachedDepthBuffer;
    private bool GenDataOnce = false;
    private bool _buffersNeedReallocation = true;

    public Renderer(Framebuffer framebuffer, Camera3D camera)
    {
        _framebuffer = framebuffer;
        _camera = camera;
        Context context = Context.Create(builder => builder.Default().EnableAlgorithms());
        _accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);
        _loadedKernel =
            _accelerator
                .LoadAutoGroupedStreamKernel<Index2D, ArrayView<BvhNode>, ArrayView<Object3DDataOnly>, ArrayView<Color>,
                    ArrayView<Light>,
                    ArrayView<Color>, ArrayView<float>, Matrix, Matrix, Vector2, Vector3>(Kernel);

        _deviceOutput = _accelerator.Allocate1D<Color>(framebuffer.Texture.Width * framebuffer.Texture.Height);
        _depthBuffer = _accelerator.Allocate1D<float>(framebuffer.Texture.Width * framebuffer.Texture.Height);
        _cachedDepthBuffer = new float[framebuffer.Texture.Width * framebuffer.Texture.Height];
        for (int i = 0; i < _cachedDepthBuffer.Length; i++)
            _cachedDepthBuffer[i] = float.MaxValue;
    }

    public void ClearTextures()
    {
        concatenatedTextures.Clear();
        textureStartIndices.Clear();
        _colorBuffer?.Dispose();
        GenDataOnce = false;
    }

    public void RenderA(List<BvhNode> bvhNodes, List<Object3D> tiles, Dictionary<string, Texture2D> Textures,
        List<Light> lights, List<Sprite> dynamicSprites)
    {
        if (Game1.CurrentEvent != null && !Game1.CurrentEvent.playerControlSequence)
            return;
        
        List<Object3DDataOnly> objectDataList = new List<Object3DDataOnly>();

        foreach (var obj in tiles)
        {
            int textureStartIndex = -1;

            if (obj.Texture != null &&
                !textureStartIndices.TryGetValue(obj.Texture.Name + obj.TileIndex, out textureStartIndex))
            {
                // Texture not added yet, add it
                textureStartIndex = concatenatedTextures.Count;
                var (texColor, objectData) = obj.GetObject3DDataOnly(true);
                concatenatedTextures.AddRange(texColor);
                textureStartIndices[obj.Texture.Name + obj.TileIndex] = textureStartIndex;
            }

            var objectDataOnly = obj.GetObject3DDataOnly(false).Item2;
            objectDataOnly.TextureStartIndex = textureStartIndex;
            if (obj.ObjectType == ObjectType.Sprite)
                objectDataOnly.Size = new Vector3(obj.Size.X, obj.Size.Y, 0);
            
            objectDataList.Add(objectDataOnly);
        }

        if (concatenatedTextures.Count == 0)
        {
            concatenatedTextures.Add(new Color(0, 0, 0, 0));
            objectDataList.Add(new Object3DDataOnly(Vector3.Zero, new Color(0, 0, 0, 0), Vector3.Zero, 0, 0,
                ObjectType.Object, 0, new Rectangle()));
        }
        
        if (!GenDataOnce)
        {
            _colorBuffer = _accelerator.Allocate1D(concatenatedTextures.ToArray());
            _buffersNeedReallocation = true;
            GenDataOnce = true;
        }
        
        // Allocate memory for flattened data
        if (_buffersNeedReallocation || _bvhBuffer == null || _tilesBuffer == null || _lightsBuffer == null)
        {
            _bvhBuffer?.Dispose();
            _tilesBuffer?.Dispose();
            _lightsBuffer?.Dispose();

            _bvhBuffer = _accelerator.Allocate1D(bvhNodes.ToArray());
            _tilesBuffer = _accelerator.Allocate1D(objectDataList.ToArray());
            _lightsBuffer = _accelerator.Allocate1D(lights.ToArray());

            _buffersNeedReallocation = false;
        }

        _depthBuffer = _accelerator.Allocate1D(_cachedDepthBuffer);
        _framebuffer.Clear(Color.Black);
        _loadedKernel(
            new Index2D(_framebuffer.Texture.Width, _framebuffer.Texture.Height),
            _bvhBuffer.View,
            _tilesBuffer.View,
            _colorBuffer.View,
            _lightsBuffer.View,
            _deviceOutput.View,
            _depthBuffer.View,
            _camera.ViewMatrix,
            _camera.ProjectionMatrix,
            new Vector2(_framebuffer.Texture.Width, _framebuffer.Texture.Height),
            _camera.Position
        );
        _accelerator.Synchronize();
        _framebuffer.ColorBuffer = _deviceOutput.GetAsArray1D();
        _depthBuffer.Dispose();
        _framebuffer.UpdateTexture();


        Game1.spriteBatch.Draw(_framebuffer.Texture, Game1.game1.screen.Bounds, Color.White);
        RenderSpritesOnCPU(dynamicSprites);
    }

    private void RenderSpritesOnCPU(List<Sprite> sprites)
    {
        // List to hold tuples of sprites and their distances
        List<(Sprite sprite, float distance)> spriteDistances = new List<(Sprite, float)>();

        foreach (var sprite in sprites)
        {
            // Calculate ray direction from the camera to the sprite position
            Vector3 toSprite = sprite.Position - _camera.Position;
            Vector3 rayDirection = Vector3.Normalize(toSprite);

            // Check if the ray from the camera intersects the sprite using the forward vector
            if (RayIntersectsSprite(_camera.Position, rayDirection, sprite, _camera.GetForward(), out float distance))
            {
                // Add sprite and its distance to the list
                spriteDistances.Add((sprite, distance));
            }
        }

        // Sort sprites by distance in descending order (furthest first)
        spriteDistances.Sort((a, b) => b.distance.CompareTo(a.distance));

        // Render sprites in sorted order
        foreach (var (sprite, distance) in spriteDistances)
        {
            var spritePos = sprite.Position;
            //if (character.WorldRotation == 2)
            //    spritePos -= new Vector3(0, 0, sprite.Offset.Y);

            // Compute the screen position of the sprite for rendering
            Vector3 screenPos = Game1.game1.GraphicsDevice.Viewport.Project(
                spritePos,
                _camera.ProjectionMatrix,
                _camera.ViewMatrix,
                Matrix.Identity
            );

            // Check if the sprite is in front of the camera
            if (screenPos.Z < 0 || screenPos.Z > 100)
                continue; // Skip rendering if the sprite is behind the camera

            // Compute the depth and scale of the object.
            float scale = 512 / distance; // Adjust scale based on distance

            // Draw the sprite
            if (sprite.Texture == null || sprite.SourceRect == null)
                continue;
            Game1.spriteBatch.Draw(sprite.Texture, new Vector2(screenPos.X, screenPos.Y), sprite.SourceRect,
                new Color(255, 255, 255, 255), 0,
                Vector2.Zero, new Vector2(scale * 8, scale * 8), SpriteEffects.None, distance);
        }
    }

    private bool RayIntersectsSprite(Vector3 rayOrigin, Vector3 rayDirection, Sprite sprite, Vector3 cameraForward,
        out float distance)
    {
        BoundingBox box = sprite.GetBoundingBox();

        // Perform ray-box intersection test
        if (!RayIntersectsAabb(rayOrigin, rayDirection, box.Min, box.Max, out distance))
        {
            return false;
        }

        // Ensure the sprite is in front of the camera using the forward vector
        Vector3 toSprite = sprite.Position - rayOrigin;
        float dotProduct = Vector3.Dot(cameraForward, toSprite);

        return dotProduct > 0;
    }
    
    static void Kernel(Index2D index, ArrayView<BvhNode> bvhNodes, ArrayView<Object3DDataOnly> tiles,
        ArrayView<Color> concatenatedTextures, ArrayView<Light> lights, ArrayView<Color> output,
        ArrayView<float> depthBuffer,
        Matrix viewMatrix, Matrix projectionMatrix, Vector2 screenSize, Vector3 cameraPosition)
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
        Color finalColor = new Color(0, 0, 0, 0);

        // Traverse BVH for Object3D instances
        int[] stack = new int[64]; // Fixed size stack for BVH traversal
        int stackSize = 0;
        stack[stackSize++] = 0; // Start with the root node

        while (stackSize > 0)
        {
            int nodeIndex = stack[--stackSize];
            BvhNode node = bvhNodes[nodeIndex];

            // Check intersection with the node's bounding box
            if (!RayIntersectsAabb(cameraPosition, rayDirection, node.BoundingBox.Min, node.BoundingBox.Max,
                    out float nodeDistance))
                continue;

            if (node.IsLeaf)
            {
                // Test intersection with each object in the leaf node
                for (int i = node.Start; i < node.Start + node.Count; i++)
                {
                    Object3DDataOnly obj = tiles[i];
                    Vector3 boxMin = obj.Position - obj.Size / 2.0f;
                    Vector3 boxMax = obj.Position + obj.Size / 2.0f;
                    if (obj.ObjectType == ObjectType.Sprite)
                    {
                        // Modify ray intersection logic for billboards
                        if (RayIntersectsSprite(cameraPosition, rayDirection, obj, out float distance))
                        {
                            Vector3 hitPoint = cameraPosition + rayDirection * distance;
                            // Calculate texture coordinates for sprites
                            Vector2 uv = CalculateBillboardTextureCoordinates(hitPoint, obj,  cameraPosition);
                            uv = Vector2.Clamp(uv, Vector2.Zero, Vector2.One);

                            // Existing texture sampling and lighting code...
                            
                            Color textureColor = obj.TextureStartIndex == -1
                                ? obj.Color
                                : SampleTexture(concatenatedTextures, uv, obj.TextureWidth, obj.TextureHeight,
                                    obj.TextureStartIndex);
                            if (textureColor.A > 0 && distance < minDistance)
                            {
                                Vector3 viewDirection = Vector3.Normalize(cameraPosition - hitPoint);
                                Vector3 normal = new Vector3(0, 0, 1); // Billboard faces the camera
                                finalColor = CalculateLighting(textureColor, hitPoint, normal, viewDirection, lights);
                                minDistance = distance;
                                hit = true;
                            }
                        }
                    }
                    else if (RayIntersectsAabb(cameraPosition, rayDirection, boxMin, boxMax, out float distance))
                    {
                        // Determine the hit point and normal
                        Vector3 hitPoint = cameraPosition + rayDirection * distance;
                        Vector3 normal = Vector3.Normalize(obj.GetNormal(hitPoint));
                        
                        // Calculate texture coordinates for the hit point
                        Vector2 uv = CalculateTextureCoordinates(hitPoint, boxMin, boxMax, normal, obj, cameraPosition);
                        
                        // Clamp UV coordinates to avoid out-of-bounds access
                        uv = Vector2.Clamp(uv, Vector2.Zero, Vector2.One);

                        // Sample the texture
                        Color textureColor = obj.TextureStartIndex == -1
                            ? obj.Color
                            : SampleTexture(concatenatedTextures, uv, obj.TextureWidth, obj.TextureHeight,
                                obj.TextureStartIndex);

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
            }
            else
            {
                // Add child nodes to the stack for further traversal
                if (node.RightChild != -1)
                    stack[stackSize++] = node.RightChild;
                if (node.LeftChild != -1)
                    stack[stackSize++] = node.LeftChild;
            }
        }

        // Write the final color to the output buffer if a hit was detected and depth test passes
        int outputIndex = index.Y * screenWidth + index.X;
        if (hit && minDistance < depthBuffer[outputIndex])
        {
            output[outputIndex] = finalColor;
            depthBuffer[outputIndex] = minDistance;
        }
        else if (!hit)
        {
            output[outputIndex] = new Color(0, 0, 0, 0);
        }
    }
    
    static bool RayIntersectsSprite(Vector3 rayOrigin, Vector3 rayDirection, Object3DDataOnly sprite, out float distance)
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

    static Vector2 CalculateBillboardTextureCoordinates(Vector3 hitPoint, Object3DDataOnly sprite, Vector3 cameraPosition)
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
        Object3DDataOnly obj, Vector3 cameraPosition)
    {
        float u = 0.0f;
        float v = 0.0f;

        // Determine which face was hit based on the normal vector
        if (obj.ObjectType == ObjectType.Tile && normal == Vector3.Up || normal == Vector3.Down)
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
        if (x < 0 || y < 0 || x >= textureWidth || y >= textureHeight)
            return new Color(0, 0, 0, 0); // Default color if out of bounds

        int index = y * textureWidth + x;
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

    static bool RayIntersectsAabb(Vector3 rayOrigin, Vector3 rayDirection, Vector3 boxMin, Vector3 boxMax,
        out float distance)
    {
        const float epsilon = 1e-6f; // Small epsilon to handle precision issues

        float tmin = (boxMin.X - rayOrigin.X) / (Math.Abs(rayDirection.X) > epsilon ? rayDirection.X : epsilon);
        float tmax = (boxMax.X - rayOrigin.X) / (Math.Abs(rayDirection.X) > epsilon ? rayDirection.X : epsilon);

        if (tmin > tmax) (tmin, tmax) = (tmax, tmin);

        float tymin = (boxMin.Y - rayOrigin.Y) / (Math.Abs(rayDirection.Y) > epsilon ? rayDirection.Y : epsilon);
        float tymax = (boxMax.Y - rayOrigin.Y) / (Math.Abs(rayDirection.Y) > epsilon ? rayDirection.Y : epsilon);

        if (tymin > tymax) (tymin, tymax) = (tymax, tymin);

        if ((tmin > tymax) || (tymin > tmax))
        {
            distance = 0;
            return false;
        }

        if (tymin > tmin) tmin = tymin;
        if (tymax < tmax) tmax = tymax;

        float tzmin = (boxMin.Z - rayOrigin.Z) / (Math.Abs(rayDirection.Z) > epsilon ? rayDirection.Z : epsilon);
        float tzmax = (boxMax.Z - rayOrigin.Z) / (Math.Abs(rayDirection.Z) > epsilon ? rayDirection.Z : epsilon);

        if (tzmin > tzmax) (tzmin, tzmax) = (tzmax, tzmin);

        if ((tmin > tzmax) || (tzmin > tmax))
        {
            distance = 0;
            return false;
        }

        if (tzmin > tmin) tmin = tzmin;
        if (tzmax < tmax) tmax = tzmax;

        distance = tmin;

        return tmax > epsilon; // Ensure intersection is in front of the ray origin
    }
}