using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;
using Object = StardewValley.Object;

namespace StardewValley3D.Rendering;

public struct Sprite
{
    public Vector3 Position;
    public Vector2 Size; // Size in the world space
    public Vector2 Offset; // Size in the world space
    public Texture2D? Texture;
    public Rectangle? SourceRect;
    
    public Sprite(Vector3 position, Vector2 size, Vector2 offset,  Character wobject)
    {
        Position = position;
        Size = size;
        Offset = offset;
        Texture = wobject.Sprite.Texture;
        SourceRect = wobject.Sprite.sourceRect;
    }
    
    // Method to get the bounding box of the sprite for intersection tests
    public BoundingBox GetBoundingBox()
    {
        Vector3 min = Position - new Vector3(Size.X / 2.0f, 0, Size.Y / 2.0f);
        Vector3 max = Position + new Vector3(Size.X / 2.0f, Size.Y, Size.Y / 2.0f);
        return new BoundingBox(min, max);
    }
}