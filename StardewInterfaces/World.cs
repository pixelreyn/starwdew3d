using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;
using StardewValley3D.Rendering;
using StardewValley3D.Structures;

namespace StardewValley3D.StardewInterfaces;

public class World
{
    private List<WorldObject> _worldObjects = new List<WorldObject>();
    private IModHelper _helper;
    
    public World(IModHelper helper)
    {
        _helper = helper;
    }

    public void GenerateMap()
    {
        _worldObjects.Clear();
        foreach (var building in Game1.game1.instanceGameLocation.buildings)
        {
            if (building == null)
                continue;

            // Create a new Object3D for each building
            var buildingPosition = new Vector3(
                (building.tileX.Value - 1) * 64 + (float)building.tilesWide.Value * 64 / 2,
                (float)building.tilesHigh.Value * 64 / 2,
                (building.tileY.Value - 1) * 64 + (float)building.tilesHigh.Value * 64 / 2);
            //new Vector3((building.tileX.Value - building.tilesWide.Value * 64 / 2) * 64, 1 * 32, (building.tileY.Value - building.tilesHigh.Value * 64 / 2) * 64),
            var buildingObject = new WorldObject(buildingPosition,
                new Vector3(building.tilesWide.Value * 64,
                    (building.tilesHigh.Value + (building.buildingType.Contains("house") ? 1 : 0)) * 64,
                    (building.tilesHigh.Value - (building.buildingType.Contains("house") ? 2 : 0)) * 64),
                building.texture.Value, building.getSourceRect(), Color.SandyBrown, ObjectType.Building,
                building.buildingType.Value, false);

            _worldObjects.Add(buildingObject);
        }

        foreach (var activeObjects in Game1.game1.instanceGameLocation._activeTerrainFeatures)
        {
            if (activeObjects == null || activeObjects is not Flooring)
                continue;
            var tilePos = new Vector3((activeObjects.Tile.X) * 64, 0, (activeObjects.Tile.Y) * 64);
            var wtile = new WorldObject(tilePos,
                new Vector3(64.0f * ((float)activeObjects.getBoundingBox().Width / 64), 64.0f,
                    64.0f * ((float)activeObjects.getBoundingBox().Height / 64)),
                (activeObjects as Flooring).GetTexture(),
                new Rectangle((activeObjects as Flooring).GetTextureCorner(), new Point(16, 16)),
                new Color(92, 46, 0, 255), ObjectType.Tile, (activeObjects as Flooring).whichFloor.Value, true);
            _worldObjects.Add(wtile);
        }

        foreach (var furniture in Game1.game1.instanceGameLocation.furniture)
        {
            if (furniture == null)
                continue;

            var type = furniture.furniture_type.Value != 12 ? ObjectType.Object : ObjectType.Tile;
            
            ParsedItemData dataOrErrorItem1 = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
            var fPos = new Vector3((furniture.TileLocation.X) * 64, type == ObjectType.Object ? 64 : 2, (furniture.TileLocation.Y) * 64);
            var wtile = new WorldObject(fPos,
                new Vector3(64.0f * ((float)furniture.boundingBox.Width / 64), furniture.ItemId.Contains("bed") ? 32.0f : 64.0f,
                    64.0f * ((float)furniture.boundingBox.Height / 64)),
                dataOrErrorItem1.GetTexture(), furniture.sourceRect.Value, new Color(92, 46, 0, 255),
                type, furniture.furniture_type.Value.ToString() + furniture.Type, true);

            _worldObjects.Add(wtile);
        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                var fPos = new Vector3((tf.Tile.X) * 64, 0, (tf.Tile.Y) * 64);
                WorldObject wtile = new WorldObject(fPos, new Vector3(64.0f, 64, 64.0f),
                    null, new Rectangle(0, 0, 64, 64), new Color(41, 25, 5, 255), ObjectType.Tile, tf is Tree ? "TreeDirt" : "HoeDirt", true);

                _worldObjects.Add(wtile);
            }
        }

        foreach (var wObject in Game1.game1.instanceGameLocation.objects.Values)
        {
            if (wObject == null)
                continue;
            
            ParsedItemData dataOrErrorItem1 = ItemRegistry.GetDataOrErrorItem(wObject.QualifiedItemId);
            var texture = dataOrErrorItem1.GetTexture();
            var sourceRect = dataOrErrorItem1.GetSourceRect();
            var objPos = new Vector3((wObject.TileLocation.X) * 64, 52, (wObject.TileLocation.Y) * 64);
            var obj = new WorldObject(objPos, new Vector3(wObject.boundingBox.Width, wObject.boundingBox.Height, wObject.boundingBox.Width),
                texture, sourceRect, Color.Red, ObjectType.Sprite, wObject.ItemId, false);
            
            _worldObjects.Add(obj);
        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                var tfObject = new WorldObject();
                var tfPos = new Vector3();
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
                    var yPos = 56 + (80 - 56) * (Math.Clamp(tree.growthStage.Value, 0, 6) / 6);
                    tfPos = new Vector3((tf.Tile.X) * 64, yPos, (tf.Tile.Y) * 64);
                    tfObject = new WorldObject(tfPos, TreeSize,
                        (tf as Tree).texture.Value, sourceRect, Color.Green, ObjectType.Sprite, (tf as Tree).treeType.Value + tree.growthStage.Value + tree.stump.Value, false);
                }
                else if (tf is HoeDirt { crop: not null })
                {
                    var crop = (tf as HoeDirt).crop;
                    
                    crop.updateDrawMath(tf.Tile);
                    tfPos = new Vector3((tf.Tile.X) * 64, 58, (tf.Tile.Y) * 64);
                    tfObject = new WorldObject(tfPos, new Vector3(tf.getBoundingBox().Width, tf.getBoundingBox().Height, tf.getBoundingBox().Width),
                        crop.DrawnCropTexture, crop.sourceRect, Color.Green, ObjectType.Sprite, crop.indexOfHarvest.Value + crop.currentPhase.Value, false);
                }
                
                _worldObjects.Add(tfObject);
            }
        }
        
        foreach (var layer in Game1.game1.instanceGameLocation.map.Layers)
        {

            for (var x = 0; x < layer.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Tiles.Array.GetLength(1); y++)
                {

                    Color color = new Color(36, 36, 36, 255);
                    var tile = layer.Tiles[x, y];
                    if (tile != null && tile.TileIndex != 0)
                    {
                        bool isWall = (tile.TileIndex == 165 || tile.TileIndex == 163 || tile.TileIndex == 162 ||
                                       tile.TileIndex == 160 || tile.TileIndex == 167 ||
                                       tile.Properties.Keys.Count(a => a.Contains("Wall")) > 0);

                        var objectType = ObjectType.Tile;
                        if (layer.Description.Contains("Front") || layer.Description.Contains("Building") || isWall)
                            objectType = ObjectType.Object;

                        if (!RenderingData.LoadedTextures.TryGetValue(tile.TileSheet.ImageSource, out var texture))
                        {
                            texture = _helper.GameContent.Load<Texture2D>(tile.TileSheet.ImageSource);
                            RenderingData.LoadedTextures.Add(tile.TileSheet.ImageSource, texture);
                        }

                        var tileRect = tile.TileSheet.GetTileImageBounds(tile.TileIndex);
                        var tilePos = new Vector3(x * 64, 0, y * 64);
                        var wtile = new WorldObject(tilePos,
                            new Vector3(64.0f, objectType == ObjectType.Object ? 128.0f : 64.0f, 64.0f), texture,
                            new Rectangle(new Point(tileRect.X, tileRect.Y),
                                new Point(tileRect.Width, tileRect.Height)),
                            color, objectType, tile.TileSheet.Id + tile.TileIndex.ToString(), true);

                        _worldObjects.Add(wtile);

                    }
                }
            }
        }

        
    }

    private FlattenedBVHNode[] _linearNodes;


    public void UpdateCharacters()
    {
        System.Diagnostics.Stopwatch sw = new();
        sw.Start();
        int maxNodes = _worldObjects.Count * 4; // Estimate the number of nodes needed.
        _linearNodes = new FlattenedBVHNode[maxNodes];
        FlattenedBVHNode.ResetCount();
        
        var npcs = new List<WorldObject>();
        foreach (var npc in Game1.game1.instanceGameLocation.characters)
        {
            if (npc == null)
                continue;

            var sprite = new WorldObject(
                new Vector3((npc.Position.X), 68, (npc.Position.Y)),
                new Vector3(64, 92, 64),
                npc.Sprite.spriteTexture,
                npc.Sprite.sourceRect,
                Color.Red,
                ObjectType.Sprite,
                "Sprite"+npc.Name,
                false);
            npcs.Add(sprite);
        }
        
        foreach(var chara in Game1.game1.instanceGameLocation.farmers)
        {
            if (chara == null || chara.IsLocalPlayer)
                continue;
            var sprite = new WorldObject(
                new Vector3((chara.Position.X), 68, (chara.Position.Y)),
                new Vector3(64, 92, 64),
                chara.Sprite.spriteTexture,
                chara.Sprite.sourceRect,
                Color.Red,
                ObjectType.Sprite,
                "Sprite"+chara.Name,
                false);
            npcs.Add(sprite);
        }
        var combined = _worldObjects.Concat(npcs).ToList();
        FlattenedBVHNode.ConstructBvh(combined, _linearNodes, 0, combined.Count);
        sw.Stop();
       // Console.WriteLine($"BVH construction took {sw.ElapsedMilliseconds}");
    }


    public FlattenedBVHNode[] GetFlattenedNodes() => _linearNodes;
}