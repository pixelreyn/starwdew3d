using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley3D.Structures;

namespace StardewValley3D.Rendering;

public enum ObjectType
{
    Object,
    Sprite,
    Tile,
    Building
}
public struct WorldObject
{
    public Vector3 Position;
    public Color Color;
    public Vector3 Size;
    public int TextureWidth;
    public int TextureHeight;
    public Rectangle SourceRectangle;
    public int TextureStartIndex;
    public int TextureSet;
    public ObjectType ObjectType;
    public int TileIndex;
    public int RenderTop;
    public BoundingBox Bounds;

    public WorldObject(Vector3 position,  Vector3 size, Texture2D? texture, Rectangle sourceRectangle,
        Color color, ObjectType objectType, string tileId, bool renderTop)
    {
        Position = position;
        Color = color;
        Size = size;
        Bounds = new BoundingBox(position - Size / 2, position + Size / 2);
        
        ObjectType = objectType;
        TileIndex = -1;
        RenderTop = renderTop ? 1 : 0;
        SourceRectangle = sourceRectangle;
        TextureWidth = SourceRectangle.Width;
        TextureHeight = SourceRectangle.Height;
        TextureStartIndex = -1;
        TextureSet = 0;

        if (texture != null)
        {
            TileIndex = RenderingData.AddTextureToCache(tileId, texture, SourceRectangle);
            TextureSet = 1;
        }

    }
    public bool ShouldRenderTop() => RenderTop == 1;
    
    public Vector3 GetNormal(Vector3 hitPoint)
    {
        // Calculate which face of the bounding box was hit based on the hit point
        Vector3 normal = Vector3.Zero;

        float bias = 0.0001f; // Small bias to prevent numerical errors
        Vector3 boxMin = Bounds.Min;
        Vector3 boxMax = Bounds.Max;

        if (Math.Abs(hitPoint.X - boxMin.X) < bias) normal = Vector3.Left;
        else if (Math.Abs(hitPoint.X - boxMax.X) < bias) normal = Vector3.Right;
        else if (Math.Abs(hitPoint.Y - boxMin.Y) < bias) normal = Vector3.Down;
        else if (Math.Abs(hitPoint.Y - boxMax.Y) < bias) normal = Vector3.Up;
        else if (Math.Abs(hitPoint.Z - boxMin.Z) < bias) normal = Vector3.Backward;
        else if (Math.Abs(hitPoint.Z - boxMax.Z) < bias) normal = Vector3.Forward;

        return normal;
    }
}