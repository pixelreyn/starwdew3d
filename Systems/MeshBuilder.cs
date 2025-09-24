using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace StardewValley3D.Systems
{
    public class MeshBuilder
    {
        private List<VertexPositionNormalTexture> _vertices = new();
        private List<int> _indices = new();
        private int _currentVertex = 0;

        public void Clear()
        {
            _vertices.Clear();
            _indices.Clear();
            _currentVertex = 0;
        }

        public void AddBox(Vector3 position, Vector3 size, Rectangle textureRect, Texture2D texture)
        {
            Vector3 halfSize = size * 0.5f;
            Vector3 min = position - halfSize;
            Vector3 max = position + halfSize;

            // Calculate UV coordinates based on texture rect
            float texLeft = textureRect.X / (float)texture.Width;
            float texTop = textureRect.Y / (float)texture.Height;
            float texRight = (textureRect.X + textureRect.Width) / (float)texture.Width;
            float texBottom = (textureRect.Y + textureRect.Height) / (float)texture.Height;

            // Front face
            AddQuad(
                new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
                Vector3.Forward, texLeft, texTop, texRight, texBottom
            );

            // Back face
            AddQuad(
                new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z),
                Vector3.Backward, texLeft, texTop, texRight, texBottom
            );

            // Top face
            AddQuad(
                new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
                Vector3.Up, texLeft, texTop, texRight, texBottom
            );

            // Bottom face
            AddQuad(
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z),
                Vector3.Down, texLeft, texTop, texRight, texBottom
            );

            // Right face
            AddQuad(
                new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z),
                Vector3.Right, texLeft, texTop, texRight, texBottom
            );

            // Left face
            AddQuad(
                new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z),
                Vector3.Left, texLeft, texTop, texRight, texBottom
            );
        }
        
        public void AddBoxFace(Vector3 position, Vector3 size, BoxFace face, Rectangle textureRect, Texture2D texture)
        {
            Vector3 halfSize = size * 0.5f;
            Vector3 min = position - halfSize;
            Vector3 max = position + halfSize;

            // Calculate UV coordinates based on texture rect
            float texLeft = textureRect.X / (float)texture.Width;
            float texTop = textureRect.Y / (float)texture.Height;
            float texRight = (textureRect.X + textureRect.Width) / (float)texture.Width;
            float texBottom = (textureRect.Y + textureRect.Height) / (float)texture.Height;

            // Front face
            switch (face)
            {
                case BoxFace.Front:
                    AddQuad(
                        new Vector3(min.X, min.Y, max.Z), new Vector3(max.X, min.Y, max.Z),
                        new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
                        Vector3.Forward, texLeft, texTop, texRight, texBottom
                    );
                    break;
                case BoxFace.Back:
                    AddQuad(
                        new Vector3(max.X, min.Y, min.Z), new Vector3(min.X, min.Y, min.Z),
                        new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z),
                        Vector3.Backward, texLeft, texTop, texRight, texBottom
                    );
                    break;
                case BoxFace.Top:
                    AddQuad(
                        new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z),
                        new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
                        Vector3.Up, texLeft, texTop, texRight, texBottom
                    );
                    break;
                case BoxFace.Bottom:
                    AddQuad(
                        new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                        new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z),
                        Vector3.Down, texLeft, texTop, texRight, texBottom
                    );
                    break;
                case BoxFace.Right:
                    AddQuad(
                        new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z),
                        new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, max.Y, max.Z),
                        Vector3.Right, texLeft, texTop, texRight, texBottom
                    );
                    break;
                case BoxFace.Left:
                    AddQuad(
                        new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z),
                        new Vector3(min.X, max.Y, max.Z), new Vector3(min.X, max.Y, min.Z),
                        Vector3.Left, texLeft, texTop, texRight, texBottom
                    );
                    break;
            }
        }
        
        public void AddSplitBox(Vector3 position, Vector3 size, Rectangle textureRect, 
    Texture2D texture, float splitRatio, bool isBillboard = false)
{
    Vector3 halfSize = size * 0.5f;
    Vector3 min = position - halfSize;
    Vector3 max = position + halfSize;
    
    // Calculate split position - for sprites, this represents where the "top view" ends
    float splitY = max.Y - (size.Y * splitRatio);
    
    // Full texture UV coordinates
    float texLeft = textureRect.X / (float)texture.Width;
    float texTop = textureRect.Y / (float)texture.Height;
    float texRight = (textureRect.X + textureRect.Width) / (float)texture.Width;
    float texBottom = (textureRect.Y + textureRect.Height) / (float)texture.Height;
    
    // Calculate texture split point based on ratio
    float texSplitV = texTop + (texBottom - texTop) * splitRatio;
    
    if (isBillboard)
    {
        // For billboard sprites (trees, characters), only render front face
        AddQuad(
            new Vector3(min.X, min.Y, position.Z), 
            new Vector3(max.X, min.Y, position.Z),
            new Vector3(max.X, max.Y, position.Z), 
            new Vector3(min.X, max.Y, position.Z),
            Vector3.Forward, texLeft, texTop, texRight, texBottom
        );
    }
    else
    {
        // Top face - uses top portion of texture stretched horizontally
        AddQuad(
            new Vector3(min.X, splitY, min.Z), 
            new Vector3(max.X, splitY, min.Z),
            new Vector3(max.X, splitY, max.Z), 
            new Vector3(min.X, splitY, max.Z),
            Vector3.Up, texLeft, texTop, texRight, texSplitV, true
        );
        
        // Front face - uses full texture
        AddQuad(
            new Vector3(min.X, min.Y, max.Z), 
            new Vector3(max.X, min.Y, max.Z),
            new Vector3(max.X, splitY, max.Z), 
            new Vector3(min.X, splitY, max.Z),
            Vector3.Forward, texLeft, texTop, texRight, texBottom
        );
        
        // Back face - mirrored texture
        AddQuad(
            new Vector3(max.X, min.Y, min.Z), 
            new Vector3(min.X, min.Y, min.Z),
            new Vector3(min.X, splitY, min.Z), 
            new Vector3(max.X, splitY, min.Z),
            Vector3.Backward, texRight, texTop, texLeft, texBottom
        );
        
        // Right face - side view, use edge pixels stretched
        float edgeU = texRight - 0.01f; // Use right edge of texture
        AddQuad(
            new Vector3(max.X, min.Y, max.Z), 
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, splitY, min.Z), 
            new Vector3(max.X, splitY, max.Z),
            Vector3.Right, edgeU, texTop, texRight, texBottom
        );
        
        // Left face - side view, use edge pixels stretched
        float leftEdgeU = texLeft + 0.01f; // Use left edge of texture
        AddQuad(
            new Vector3(min.X, min.Y, min.Z), 
            new Vector3(min.X, min.Y, max.Z),
            new Vector3(min.X, splitY, max.Z), 
            new Vector3(min.X, splitY, min.Z),
            Vector3.Left, texLeft, texTop, leftEdgeU, texBottom
        );
        
        // Bottom face (optional, for completeness)
        AddQuad(
            new Vector3(min.X, min.Y, min.Z), 
            new Vector3(max.X, min.Y, min.Z),
            new Vector3(max.X, min.Y, max.Z), 
            new Vector3(min.X, min.Y, max.Z),
            Vector3.Down, texLeft, texBottom - 0.01f, texRight, texBottom, true
        );
    }
}

        public void AddBillboard(Vector3 position, Vector2 size, Rectangle textureRect, Texture2D texture)
        {
            float halfWidth = size.X * 0.5f;
            float halfHeight = size.Y * 0.5f;

            float texLeft = textureRect.X / (float)texture.Width;
            float texTop = textureRect.Y / (float)texture.Height;
            float texRight = (textureRect.X + textureRect.Width) / (float)texture.Width;
            float texBottom = (textureRect.Y + textureRect.Height) / (float)texture.Height;

            // Create billboard that will face camera (handled in shader)
            AddQuad(
                new Vector3(-halfWidth, -halfHeight, 0) + position,
                new Vector3(halfWidth, -halfHeight, 0) + position,
                new Vector3(halfWidth, halfHeight, 0) + position,
                new Vector3(-halfWidth, halfHeight, 0) + position,
                Vector3.Forward, texLeft, texTop, texRight, texBottom
            );
        }
        public void AddPlane(Vector3 position, Vector3 size, 
            Rectangle sourceRect, Texture2D texture)
        {
            float halfWidth = size.X * 0.5f;
            float halfDepth = size.Z * 0.5f;

            // Use position directly - it's already Vector3.Zero for instanced objects
            // and the actual world position for non-instanced objects
            float texLeft = sourceRect.X / (float)texture.Width;
            float texTop = sourceRect.Y / (float)texture.Height;
            float texRight = (sourceRect.X + sourceRect.Width) / (float)texture.Width;
            float texBottom = (sourceRect.Y + sourceRect.Height) / (float)texture.Height;

            // Create plane centered at the given position (0,0,0 for instanced, world pos for non-instanced)
            AddQuad(
                new Vector3(position.X - halfWidth, position.Y, position.Z - halfDepth),
                new Vector3(position.X + halfWidth, position.Y, position.Z - halfDepth),
                new Vector3(position.X + halfWidth, position.Y, position.Z + halfDepth),
                new Vector3(position.X - halfWidth, position.Y, position.Z + halfDepth),
                Vector3.Up, texLeft, texTop, texRight, texBottom, true
            );
        }
        
        public void AddCrossedBillboards(Vector3 position, Vector3 size,
            Rectangle sourceRect, Texture2D texture)
        {
            // First billboard (X-aligned) - already handles local space correctly
            AddBillboard(position, new Vector2(size.X, size.Y), sourceRect, texture);
    
            // Second billboard (Z-aligned) - update to handle local space
            float halfSize = size.X * 0.5f;
            float halfHeight = size.Y * 0.5f;
    
            float texLeft = sourceRect.X / (float)texture.Width;
            float texTop = sourceRect.Y / (float)texture.Height;
            float texRight = (sourceRect.X + sourceRect.Width) / (float)texture.Width;
            float texBottom = (sourceRect.Y + sourceRect.Height) / (float)texture.Height;
    
            // Use position directly (which will be Vector3.Zero for instanced objects)
            AddQuad(
                new Vector3(position.X, position.Y - halfHeight, position.Z - halfSize),
                new Vector3(position.X, position.Y - halfHeight, position.Z + halfSize),
                new Vector3(position.X, position.Y + halfHeight, position.Z + halfSize),
                new Vector3(position.X, position.Y + halfHeight, position.Z - halfSize),
                Vector3.Right, texLeft, texTop, texRight, texBottom
            );
        }
        
        public Mesh BuildFromComposition(BoxComposition composition, Texture2D texture, GraphicsDevice _device)
        {
            Clear();
    
            foreach (var box in composition.Boxes)
            {
                foreach (BoxFace face in Enum.GetValues(typeof(BoxFace)))
                {
                    var uvMap = box.FaceUVs.GetValueOrDefault(face);
                    if (uvMap == null && !box.UseSprite) continue;
            
                    var sourceRect = uvMap?.SourceRect ?? new Rectangle(0, 0, texture.Width, texture.Height);
                    AddBoxFace(box.Position, box.Size, face, sourceRect, texture);
                }
            }
    
            return BuildMesh(_device);
        }

        public void AddQuad(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, Vector3 normal,
            float u1, float v1t, float u2, float v2t, bool invert = false)
        {
            int startIndex = _currentVertex;

            _vertices.Add(new VertexPositionNormalTexture(v1, normal, new Vector2(u1, v2t)));
            _vertices.Add(new VertexPositionNormalTexture(v2, normal, new Vector2(u2, v2t)));
            _vertices.Add(new VertexPositionNormalTexture(v3, normal, new Vector2(u2, v1t)));
            _vertices.Add(new VertexPositionNormalTexture(v4, normal, new Vector2(u1, v1t)));

            // Two triangles
            if (invert)
            {
                _indices.Add(startIndex + 0);
                _indices.Add(startIndex + 1);
                _indices.Add(startIndex + 2);

                _indices.Add(startIndex + 0);
                _indices.Add(startIndex + 2);
                _indices.Add(startIndex + 3);
            }
            else
            {
                _indices.Add(startIndex + 0);
                _indices.Add(startIndex + 2);
                _indices.Add(startIndex + 1);

                _indices.Add(startIndex + 0);
                _indices.Add(startIndex + 3);
                _indices.Add(startIndex + 2);
            }

            _currentVertex += 4;
        }

        public Mesh BuildMesh(GraphicsDevice device)
        {
            if (_vertices.Count == 0)
                return null;

            return new Mesh(device, _vertices.ToArray(), _indices.ToArray());
        }
    }

    public class Mesh
    {
        public VertexBuffer VertexBuffer { get; }
        public IndexBuffer IndexBuffer { get; }
        private readonly List<VertexPositionNormalTexture> _vertices = new();
        public int PrimitiveCount { get; }

        public Mesh(GraphicsDevice device, VertexPositionNormalTexture[] vertices, int[] indices)
        {
            VertexBuffer = new VertexBuffer(device, typeof(VertexPositionNormalTexture), vertices.Length, BufferUsage.WriteOnly);
            _vertices = new List<VertexPositionNormalTexture>(vertices);
            VertexBuffer.SetData(vertices);

            IndexBuffer = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, indices.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indices);

            PrimitiveCount = indices.Length / 3;
        }

        public string getDebugVertexData()
        {
            string result = "";
            foreach (var vertex in _vertices)
            {
                result += vertex.Position.ToString() + ", " + vertex.Normal.ToString() + ", " + vertex.TextureCoordinate.ToString() + ";";
            }
            return result;
        }

        public void Dispose()
        {
            VertexBuffer?.Dispose();
            IndexBuffer?.Dispose();
        }
    }
}