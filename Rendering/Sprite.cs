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
    public Sprite(Vector3 position, Vector2 size, Vector2 offset, Object wobject)
    {
        Position = position;
        Size = size;
        Offset = offset;
        ParsedItemData dataOrErrorItem1 = ItemRegistry.GetDataOrErrorItem(wobject.QualifiedItemId);
        Texture = dataOrErrorItem1.GetTexture();
        SourceRect = dataOrErrorItem1.GetSourceRect();
    }
    
    public Sprite(Vector3 position, Vector2 size, Vector2 offset,  Character wobject)
    {
        Position = position;
        Size = size;
        Offset = offset;
        Texture = wobject.Sprite.Texture;
        SourceRect = wobject.Sprite.sourceRect;
    }
    
    public Sprite(Vector3 position, Vector2 size, Vector2 offset,  TerrainFeature wobject)
    {
        Position = position;
        Size = size;
        Offset = offset;
        Texture = null;
        SourceRect = null;
        if (wobject is Tree)
        {
            var tree = (wobject as Tree);
            if (tree is null)
                return;
            Texture = tree.texture.Value;
            if (tree.growthStage.Value < 5)
            {
                Rectangle rectangle;
                switch (tree.growthStage.Value)
                {
                    case 0:
                        rectangle = new Rectangle(32, 128, 16, 16);
                        break;
                    case 1:
                        rectangle = new Rectangle(0, 128, 16, 16);
                        break;
                    case 2:
                        rectangle = new Rectangle(16, 128, 16, 16);
                        break;
                    default:
                        rectangle = new Rectangle(0, 96, 16, 32);
                        break;
                }

                SourceRect = rectangle;
            }
            else
            {
                SourceRect = !tree.stump.Value ? Tree.treeTopSourceRect : Tree.stumpSourceRect;
            }
        }

        if (wobject is HoeDirt { crop: not null } && wobject as HoeDirt != null)
        {
            var crop = (wobject as HoeDirt).crop;
            crop.updateDrawMath(wobject.Tile);
            Texture = crop.DrawnCropTexture;
            SourceRect = crop.sourceRect;
        }

    }
    // Method to get the bounding box of the sprite for intersection tests
    public BoundingBox GetBoundingBox()
    {
        Vector3 min = Position - new Vector3(Size.X / 2.0f, 0, Size.Y / 2.0f);
        Vector3 max = Position + new Vector3(Size.X / 2.0f, Size.Y, Size.Y / 2.0f);
        return new BoundingBox(min, max);
    }
}