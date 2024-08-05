using System.Collections.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;
using StardewValley3D.Rendering;
using xTile.Layers;

namespace StardewValley3D.StardewInterfaces;

public struct WorldTile
{
    public Color Color;
    public Vector3 Position;
    public Vector3 Size;
    
    public BoundingBox GetBoundingBox()
    {
        Vector3 min = Position - Size / 2.0f;
        Vector3 max = Position + Size / 2.0f;
        return new BoundingBox(min, max);
    }
    
    public Vector3 GetNormal(Vector3 hitPoint)
    {
        // Calculate which face of the bounding box was hit based on the hit point
        BoundingBox box = GetBoundingBox();
        Vector3 normal = Vector3.Zero;

        float bias = 0.0001f; // Small bias to prevent numerical errors
        Vector3 boxMin = box.Min;
        Vector3 boxMax = box.Max;

        if (Math.Abs(hitPoint.X - boxMin.X) < bias) normal = Vector3.Left;
        else if (Math.Abs(hitPoint.X - boxMax.X) < bias) normal = Vector3.Right;
        else if (Math.Abs(hitPoint.Y - boxMin.Y) < bias) normal = Vector3.Down;
        else if (Math.Abs(hitPoint.Y - boxMax.Y) < bias) normal = Vector3.Up;
        else if (Math.Abs(hitPoint.Z - boxMin.Z) < bias) normal = Vector3.Backward;
        else if (Math.Abs(hitPoint.Z - boxMax.Z) < bias) normal = Vector3.Forward;

        return normal;
    }
}

public struct BvhNode
{
    public BoundingBox BoundingBox;
    public int LeftChild; // Index of the left child node (-1 if none)
    public int RightChild; // Index of the right child node (-1 if none)
    public int Start; // Start index for tiles (used in leaf nodes)
    public int Count; // Number of tiles (used in leaf nodes)

    public bool IsLeaf => LeftChild == -1 && RightChild == -1;
}

public class Map
{
    public ConcurrentDictionary<(int x, int y, int z), Object3D> _3dObjects = new ConcurrentDictionary<(int x, int y, int z), Object3D>();
    private List<BvhNode> _flatBvhNodes = new List<BvhNode>();
    private List<Object3D> _flatTiles = new List<Object3D>();
    private List<Sprite> _dynamicSprites = new List<Sprite>();
    private Dictionary<string, Texture2D> _loadedTextures = new Dictionary<string, Texture2D>();
    public IModHelper Helper;

    public void GenerateMap()
    {
        _3dObjects.Clear();
        _loadedTextures.Clear();
        
        foreach (var layer in Game1.game1.instanceGameLocation.frontLayers)
        {
            for (var x = 0; x < layer.Key.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Key.Tiles.Array.GetLength(1); y++)
                {

                    Color color = new Color(36, 36, 36, 255);
                    var tile = layer.Key.Tiles[x, y];
                    if (tile != null && tile.TileIndex != 0)
                    {
                        if (tile.TileIndex == 165 || tile.TileIndex == 163 || tile.TileIndex == 162 ||
                            tile.TileIndex == 160 || tile.TileIndex == 167 ||
                            tile.Properties.Keys.Count(a => a.Contains("Wall")) > 0)
                        {
                            color = new Color(36, 0, 0, 255);
                            if (!_loadedTextures.TryGetValue(tile.TileSheet.ImageSource, out var texture))
                            {
                                texture = Helper.GameContent.Load<Texture2D>(tile.TileSheet.ImageSource);
                                _loadedTextures.Add(tile.TileSheet.ImageSource, texture);
                            }
                            
                            var tileRect = tile.TileSheet.GetTileImageBounds(tile.TileIndex);
                            var wtile = new Object3D()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f),
                                Texture = texture,
                                ObjectType = ObjectType.Object,
                                SourceRectangle = new Rectangle(new Point(tileRect.X, tileRect.Y), new Point(tileRect.Width, tileRect.Height)),
                                TileIndex = tile.TileIndex.ToString()
                            };

                            _3dObjects.TryAdd((x,1,y), wtile);
                        }
                    }
                }
            }
        }
         
        foreach (var layer in Game1.game1.instanceGameLocation.map.Layers)
         {
             if (!layer.Id.Contains("Path"))
                 continue;
             
            for (var x = 0; x < layer.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Tiles.Array.GetLength(1); y++)
                {

                    Color color = new Color(36, 36, 36, 255);
                    var tile = layer.Tiles[x, y];
                    if (tile != null && tile.TileIndex != 0)
                    {
                        if (tile.TileIndex == 165 || tile.TileIndex == 163 || tile.TileIndex == 162 ||
                            tile.TileIndex == 160 || tile.TileIndex == 167 ||
                            tile.Properties.Keys.Count(a => a.Contains("Wall")) > 0)
                        {
                            color = new Color(36, 0, 0, 255);
                            if (!_loadedTextures.TryGetValue(tile.TileSheet.ImageSource, out var texture))
                            {
                                texture = Helper.GameContent.Load<Texture2D>(tile.TileSheet.ImageSource);
                                _loadedTextures.Add(tile.TileSheet.ImageSource, texture);
                            }
                            
                            var tileRect = tile.TileSheet.GetTileImageBounds(tile.TileIndex);
                            var wtile = new Object3D()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f),
                                Texture = texture,
                                ObjectType = ObjectType.Tile,
                                SourceRectangle = new Rectangle(new Point(tileRect.X, tileRect.Y), new Point(tileRect.Width, tileRect.Height)),
                                TileIndex = tile.TileIndex.ToString()
                            };

                            _3dObjects.TryAdd((x,0,y), wtile);
                        }
                    }
                }
            }
        }
        if (Game1.game1.instanceGameLocation.waterTiles != null)
        {
            Color waterColor = new Color((int)Game1.game1.instanceGameLocation.waterColor.R,
                Game1.game1.instanceGameLocation.waterColor.G, Game1.game1.instanceGameLocation.waterColor.B, 255);
            for (var x = 0; x < Game1.game1.instanceGameLocation.waterTiles.waterTiles.GetLength(0); x++)
            {
                for (var y = 0; y < Game1.game1.instanceGameLocation.waterTiles.waterTiles.GetLength(1); y++)
                {
                    if (Game1.game1.instanceGameLocation.waterTiles.waterTiles[x, y].isWater)
                    {
                        var wtile = new Object3D()
                        {
                            Color = waterColor,
                            Position = new Vector3(x * 64, 0 * 64, y * 64),
                            Size = new Vector3(64.0f, 64.0f,64.0f),
                            ObjectType = ObjectType.Tile,
                            SourceRectangle = new Rectangle(0, 0, 64, 64),
                            Texture = null
                        };

                        _3dObjects.TryAdd((x,0, y), wtile);
                    }
                }
            }
        }
        
        //if(Game1.game1.instanceGameLocation.map.)

        foreach (var building in Game1.game1.instanceGameLocation.buildings)
        {
            if (building == null)
                continue;

            // Create a new Object3D for each building
            var buildingPosition = new Vector3((building.tileX.Value - 1) * 64 + (float)building.tilesWide.Value * 64 / 2,
                (float)building.tilesHigh.Value * 64 / 2,
                (building.tileY.Value - 1) * 64 + (float)building.tilesHigh.Value * 64 / 2);
            //new Vector3((building.tileX.Value - building.tilesWide.Value * 64 / 2) * 64, 1 * 32, (building.tileY.Value - building.tilesHigh.Value * 64 / 2) * 64),
            var buildingObject = new Object3D()
            {
                Position = buildingPosition,
                Size = new Vector3(building.tilesWide.Value * 64,
                    (building.tilesHigh.Value + (building.buildingType.Contains("house") ? 1 : 0)) * 64,
                    (building.tilesHigh.Value - (building.buildingType.Contains("house") ? 2 : 0)) * 64),
                Texture = building.texture.Value, // Use the building's texture
                SourceRectangle = building.getSourceRect(), // Use the building's source rectangle
                Color = Color.SandyBrown, // Default color
                ObjectType = ObjectType.Building,
                TileIndex = building.buildingType.Value
            };

            _3dObjects.TryAdd((building.tileX.Value,1, building.tileY.Value), buildingObject); // Add to the Object3D list
        }
        
        foreach (var buildingLayer in Game1.game1.instanceGameLocation.map.Layers)
        {
            if (!buildingLayer.Id.Contains("Building"))
                continue;
/*
            // Create a new Object3D for each building
            var buildingPosition = new Vector3((building.tileX.Value - 1) * 64 + (float)building.tilesWide.Value * 64 / 2,
                (float)building.tilesHigh.Value * 64 / 2,
                (building.tileY.Value - 1) * 64 + (float)building.tilesHigh.Value * 64 / 2);
            //new Vector3((building.tileX.Value - building.tilesWide.Value * 64 / 2) * 64, 1 * 32, (building.tileY.Value - building.tilesHigh.Value * 64 / 2) * 64),
            var buildingObject = new Object3D()
            {
                Position = buildingPosition,
                Size = new Vector3(building.tilesWide.Value * 64,
                    (building.tilesHigh.Value + (building.buildingType.Contains("house") ? 1 : 0)) * 64,
                    (building.tilesHigh.Value - (building.buildingType.Contains("house") ? 2 : 0)) * 64),
                Texture = building.texture.Value, // Use the building's texture
                SourceRectangle = building.getSourceRect(), // Use the building's source rectangle
                Color = Color.SandyBrown // Default color
            };

            _3dObjects.TryAdd((building.tileX.Value, building.tileY.Value), buildingObject); // Add to the Object3D list*/
        }

        foreach (var furniture in Game1.game1.instanceGameLocation.furniture)
        {
            if (furniture == null || furniture.furniture_type.Value != 12)
                continue;

            ParsedItemData dataOrErrorItem1 = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
            var wtile = new Object3D()
            {
                Color = new Color(92, 46, 0, 255),
                Position = new Vector3((furniture.TileLocation.X) * 64, 0,
                    (furniture.TileLocation.Y) * 64),
                Size = new Vector3(64.0f * ((float)furniture.boundingBox.Width / 64),
                    64.0f,
                    64.0f * ((float)furniture.boundingBox.Height / 64)),
                ObjectType = ObjectType.Tile,
                SourceRectangle = furniture.sourceRect.Value,
                Texture = dataOrErrorItem1.GetTexture(),
                TileIndex = furniture.furniture_type.Value.ToString()
            };

            _3dObjects.TryAdd(((int)furniture.TileLocation.X, 1, (int)furniture.TileLocation.Y), wtile);

        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                Object3D wtile = new Object3D();
                if (tf is HoeDirt)
                    wtile = new Object3D()
                    {
                        Color = new Color(41, 25, 5, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 0, (tf.Tile.Y) * 64),
                        Size = new Vector3(64.0f, 64.0f, 64.0f),
                        ObjectType = ObjectType.Tile,
                        SourceRectangle = new Rectangle(0, 0, 64, 64),
                        Texture = null,
                        TileIndex = "HoeDirt"
                    };
                else if (tf is Tree)
                    wtile = new Object3D()
                    {
                        Color = new Color(41, 25, 5, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 0, (tf.Tile.Y) * 64),
                        Size = new Vector3(64.0f, 64.0f, 64.0f),
                        ObjectType = ObjectType.Tile,
                        SourceRectangle = new Rectangle(0, 0, 64, 64),
                        Texture = null,
                        TileIndex = "TreeDirt"
                    };
                _3dObjects.TryAdd(((int)tf.Tile.X, 0, (int)tf.Tile.Y), wtile);
            }
        }
        
        foreach (var layer in Game1.game1.instanceGameLocation.backgroundLayers)
        {
            for (var x = 0; x < layer.Key.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Key.Tiles.Array.GetLength(1); y++)
                {

                    Color color = new Color(36, 36, 36, 255);
                    var tile = layer.Key.Tiles[x, y];
                    color = new Color(36, 0, 0, 255);
                    if (tile == null)
                        continue;
                    
                    if (!_loadedTextures.TryGetValue(tile.TileSheet.ImageSource, out var texture))
                    {
                        texture =  Helper.GameContent.Load<Texture2D>(tile.TileSheet.ImageSource);
                        _loadedTextures.Add(tile.TileSheet.ImageSource, texture);
                    }
                            
                    var tileRect = tile.TileSheet.GetTileImageBounds(tile.TileIndex);
                    if (tile != null && tile.TileIndex != 0)
                    {
                        if (tile.TileIndex == 470 || tile.TileIndex == 271 || tile.TileIndex == 64 ||
                            tile.Properties.Keys.Count(x => x.Contains("Wall")) > 0)
                        {
                            color = new Color(36, 0, 0, 255);
                            var wtile = new Object3D()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f),
                                ObjectType = ObjectType.Object,
                                Texture = texture,
                                SourceRectangle = new Rectangle(new Point(tileRect.X, tileRect.Y), new Point(tileRect.Width, tileRect.Height)),
                                TileIndex = tile.TileIndex.ToString()
                                
                            };
                            _3dObjects.TryAdd((x,0,y), wtile);
                        }
                        else if (tile.Properties.Count == 0 || tile.Properties.Keys.Count(x => x.Contains("Floor")) > 0)
                        {
                            var wtile = new Object3D()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 64.0f, 64.0f),
                                Texture = texture,
                                ObjectType = ObjectType.Tile,
                                SourceRectangle = new Rectangle(new Point(tileRect.X, tileRect.Y), new Point(tileRect.Width, tileRect.Height)),
                                TileIndex = tile.TileIndex.ToString()
                            };

                            _3dObjects.TryAdd((x,0,y), wtile);
                        }
                    }
                }
            }
        }
        GenerateStaticSprites();
        
        _flatTiles = new List<Object3D>(_3dObjects.Values);
        _flatBvhNodes = new List<BvhNode>();
        ConstructBvh(0, _flatTiles.Count);
    }

    public void GenerateStaticSprites()
    {
        foreach (var furniture in Game1.game1.instanceGameLocation.furniture)
        {
            if (furniture == null)
                continue;

            if (furniture.furniture_type.Value != 12)
            {
                ParsedItemData dataOrErrorItem1 = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                var texture = dataOrErrorItem1.GetTexture();
                var sourceRect = dataOrErrorItem1.GetSourceRect();
                var wObject = new Object3D()
                {
                    Color = Color.Red,
                    ObjectType = ObjectType.Object,
                    Position = new Vector3((furniture.TileLocation.X) * 64, 64, (furniture.TileLocation.Y) * 64),
                    Size = new Vector3(furniture.boundingBox.Width, furniture.boundingBox.Height, furniture.boundingBox.Height),
                    SourceRectangle = sourceRect,
                    Texture = texture,
                    TileIndex = furniture.furniture_type.Value.ToString() + furniture.Name
                };
                _3dObjects.TryAdd(((int)furniture.TileLocation.X, 1, (int)furniture.TileLocation.Y), wObject);
            }
        }

        foreach (var wObject in Game1.game1.instanceGameLocation.objects.Values)
        {
            if (wObject == null)
                continue;
            
            ParsedItemData dataOrErrorItem1 = ItemRegistry.GetDataOrErrorItem(wObject.QualifiedItemId);
            var texture = dataOrErrorItem1.GetTexture();
            var sourceRect = dataOrErrorItem1.GetSourceRect();
            var wObject3d = new Object3D()
            {
                Color = Color.Red,
                ObjectType = ObjectType.Object,
                Position = new Vector3((wObject.TileLocation.X) * 64, 48, (wObject.TileLocation.Y) * 64),
                Size = new Vector3(wObject.boundingBox.Width / 3, wObject.boundingBox.Height / 3, wObject.boundingBox.Width / 3),
                SourceRectangle = sourceRect,
                Texture = texture,
                TileIndex = wObject.ItemId
            };
            _3dObjects.TryAdd(((int)wObject.TileLocation.X, 1, (int)wObject.TileLocation.Y), wObject3d);

        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                var sprite = new Object3D();
                if (tf is Tree)
                {
                    var tree = tf as Tree;
                    Rectangle sourceRect;
                    var TreeSize = new Vector3(tf.getBoundingBox().Width, tf.getBoundingBox().Height * 2, tf.getBoundingBox().Width);
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

                        sourceRect = rectangle;
                    }
                    else
                    {
                        sourceRect = !tree.stump.Value ? Tree.treeTopSourceRect : Tree.stumpSourceRect;
                    }

                    TreeSize.Y =  Math.Clamp(TreeSize.Y * tree.growthStage.Value / 6, 16, 128);
                    var yPos = 48 + (92 - 48) * Math.Clamp(tree.growthStage.Value, 0, 6) / 6;
                        
                    sprite = new Object3D()
                    {
                        Position = new Vector3((tf.Tile.X) * 64, yPos, (tf.Tile.Y) * 64),
                        Color = Color.Green,
                        ObjectType = ObjectType.Object,
                        Size = TreeSize,
                        Texture = (tf as Tree).texture.Value,
                        SourceRectangle = sourceRect,
                        TileIndex = (tf as Tree).treeType.Value + tree.growthStage.Value + tree.stump.Value
                    };
                }
                else if (tf is HoeDirt { crop: not null })
                {
                    var crop = (tf as HoeDirt).crop;
                    
                    crop.updateDrawMath(tf.Tile);
                    
                    sprite = new Object3D()
                    {
                        Position = new Vector3((tf.Tile.X) * 64, 56, (tf.Tile.Y) * 64),
                        Color = Color.Green,
                        ObjectType = ObjectType.Object,
                        Size = new Vector3(tf.getBoundingBox().Width, tf.getBoundingBox().Height, tf.getBoundingBox().Width),
                        Texture = crop.DrawnCropTexture,
                        SourceRectangle = crop.sourceRect,
                        TileIndex = crop.indexOfHarvest.Value.ToString() + crop.currentPhase.Value.ToString()
                    };
                }

                _3dObjects.TryAdd(((int)tf.Tile.X, 1, (int)tf.Tile.Y), sprite);
            }
        }
        
    }

    public void GenerateDynamicSprites()
    {
        _dynamicSprites.Clear();
        foreach (var npc in Game1.game1.instanceGameLocation.characters)
        {
            if (npc == null)
                continue;

            var sprite = new Sprite(
                new Vector3((npc.Tile.X) * 64, 64, (npc.Tile.Y) * 64),
                new Vector2(0, npc.GetBoundingBox().Height),
                new Vector2(npc.GetBoundingBox().Width, npc.GetBoundingBox().Height), npc);
            _dynamicSprites.Add(sprite);
        }
        foreach(var chara in Game1.game1.instanceGameLocation.farmers)
        {
            if (chara == null)
                continue;
            
            var sprite = new Sprite(
                new Vector3((chara.Tile.X) * 64, 64, (chara.Tile.Y) * 64),
                new Vector2(0, chara.GetBoundingBox().Height),
                new Vector2(chara.GetBoundingBox().Width, chara.GetBoundingBox().Height), chara);
            _dynamicSprites.Add(sprite);
        }
    }
    private int ConstructBvh(int start, int end)
    {
        BvhNode node = new BvhNode();
        node.Start = start;
        node.Count = end - start;

        // Compute the bounding box for the current set of tiles
        node.BoundingBox = ComputeBoundingBox(_flatTiles, start, end);

        int nodeIndex = _flatBvhNodes.Count;
        _flatBvhNodes.Add(node);

        // If this is a leaf node, return
        if (node.Count <= 2)
        {
            node.LeftChild = -1;
            node.RightChild = -1;
            _flatBvhNodes[nodeIndex] = node; // Update node with children
            return nodeIndex;
        }

        // Determine axis to split on (using longest axis for example)
        Vector3 size = node.BoundingBox.Max - node.BoundingBox.Min;
        int axis = size.X > size.Y && size.X > size.Z ? 0 : (size.Y > size.Z ? 1 : 2);

        // Sort tiles based on the chosen axis
        _flatTiles.Sort(start, end - start, Comparer<Object3D>.Create((a, b) =>
        {
            return axis switch
            {
                0 => a.Position.X.CompareTo(b.Position.X),
                1 => a.Position.Y.CompareTo(b.Position.Y),
                _ => a.Position.Z.CompareTo(b.Position.Z),
            };
        }));

        // Split the tiles into two groups and recurse
        int mid = start + node.Count / 2;
        node.LeftChild = ConstructBvh(start, mid);
        node.RightChild = ConstructBvh(mid, end);

        // Update the current node with child information
        _flatBvhNodes[nodeIndex] = node;

        return nodeIndex;
    }

    private BoundingBox ComputeBoundingBox(List<Object3D> tiles, int start, int end)
    {
        Vector3 min = new Vector3(float.MaxValue);
        Vector3 max = new Vector3(float.MinValue);

        for (int i = start; i < end; i++)
        {
            Object3D tile = tiles[i];
            BoundingBox tileBox = tile.GetBoundingBox();

            min = Vector3.Min(min, tileBox.Min);
            max = Vector3.Max(max, tileBox.Max);
        }

        return new BoundingBox(min, max);
    }

    public List<BvhNode> GetBvhNodes()
    {
        return _flatBvhNodes;
    }

    public List<Object3D> GetFlatTiles()
    {
        return _flatTiles;
    }
    

    public List<Sprite> GetDynamicSprites()
    {
        return _dynamicSprites;
    }
    
    public Dictionary<string, Texture2D> GetLoadedTextures()
    {
        return _loadedTextures;
    }
}