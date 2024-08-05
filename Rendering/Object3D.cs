using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using xTile.Tiles;

namespace StardewValley3D.Rendering;

public enum ObjectType
{
    Building,
    Object,
    Tile
}

public struct Object3D
{
    public Vector3 Position;
    public Texture2D? Texture;
    public string TileIndex;
    public Rectangle SourceRectangle;
    public ObjectType ObjectType;
    public Color Color;
    public Vector3 Size;
    private static Dictionary<String, int> stringIntDict = new Dictionary<string, int>();
    private static int nextId = 0;
    public (Color[], Object3DDataOnly) GetObject3DDataOnly(bool GetColor)
    {
        if (TileIndex == null)
            return (new Color[] { Color }, new Object3DDataOnly(Position, Color, Size, 0, 0, ObjectType, 0));
        
        if (!stringIntDict.TryGetValue(TileIndex, out var id))
        {
            id = nextId++;
            stringIntDict.Add(TileIndex, id);
        }
        
        if (Texture != null && GetColor)
        {
            
            var texColor = new Color[SourceRectangle.Width * SourceRectangle.Height];
            Texture.GetData(0, SourceRectangle, texColor, 0, SourceRectangle.Width * SourceRectangle.Height);

            return (texColor,
                new Object3DDataOnly(Position, Color, Size, SourceRectangle.Width, SourceRectangle.Height, ObjectType, id));
        }
        else
        {
            return (new Color[] { Color },
                new Object3DDataOnly(Position, Color, Size, SourceRectangle.Width, SourceRectangle.Height, ObjectType, id));
        }
    } 
    
    public BoundingBox GetBoundingBox()
    {
        Vector3 min = Position - Size / 2.0f;
        Vector3 max = Position + Size / 2.0f;
        return new BoundingBox(min, max);
    }
    public static void ResetIndex()
    {
        nextId = 0;
        stringIntDict.Clear();
    }
}

public struct Object3DDataOnly
{
    public Vector3 Position;
    public Color Color;
    public Vector3 Size;
    public int TextureWidth;
    public int TextureHeight;
    public int TileIndex;
    public ObjectType ObjectType;
    public BoundingBox BoundingBox;
    public int TextureStartIndex; // Add this field to store the start index

    public Object3DDataOnly(Vector3 position,  Color color, Vector3 size, int textureWidth, int textureHeight, ObjectType objectType, int tileIndex)
    {
        Position = position;
        Color = color;
        Size = size;
        TextureWidth = textureWidth;
        TextureHeight = textureHeight;
        BoundingBox = new BoundingBox();
        TextureStartIndex = 0;
        ObjectType = objectType;
        TileIndex = tileIndex;
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);
        BoundingBox = GetBoundingBox();

        min = Vector3.Min(min, BoundingBox.Min);
        max = Vector3.Max(max, BoundingBox.Max);
        BoundingBox = new BoundingBox(min, max);
    }
    
    public BoundingBox GetBoundingBox()
    {
        Vector3 min = Position - Size / 2.0f;
        Vector3 max = Position + Size / 2.0f;
        return new BoundingBox(min, max);
    }

    public Vector3 GetNormal(Vector3 hitPoint)
    {
        // Calculate which face of the bounding box was hit based on the hit point
        Vector3 normal = Vector3.Zero;

        float bias = 0.0001f; // Small bias to prevent numerical errors
        Vector3 boxMin = BoundingBox.Min;
        Vector3 boxMax = BoundingBox.Max;

        if (Math.Abs(hitPoint.X - boxMin.X) < bias) normal = Vector3.Left;
        else if (Math.Abs(hitPoint.X - boxMax.X) < bias) normal = Vector3.Right;
        else if (Math.Abs(hitPoint.Y - boxMin.Y) < bias) normal = Vector3.Down;
        else if (Math.Abs(hitPoint.Y - boxMax.Y) < bias) normal = Vector3.Up;
        else if (Math.Abs(hitPoint.Z - boxMin.Z) < bias) normal = Vector3.Backward;
        else if (Math.Abs(hitPoint.Z - boxMax.Z) < bias) normal = Vector3.Forward;

        return normal;
    }

    public bool IsCorrectDirection(Vector3 normal)
    {
        switch (ObjectType)
        {
            case ObjectType.Building:
                return normal.Z < -0.999f;
            case ObjectType.Object:
                return normal.Z > 0.0f;
            case ObjectType.Tile:
                return normal.Y > 0.0f;
            default:
                return false;
        }
    }
    
}