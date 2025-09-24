using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;
using StardewValley.Objects;
using StardewValley3D.Rendering;
using StardewValley3D.Structures;
using StardewValley3D.Systems;
using xTile.Layers;
using Object = StardewValley.Object;

namespace StardewValley3D.StardewInterfaces
{
    /// <summary>
    /// Complete UpdatedWorld implementation with all processing methods
    /// </summary>
    public class World
    {
        private MonitorHelper _helper;
        private GraphicsDevice _device;
        private SpatialMap _spatialMap;
        private TileMetadataManager _metadataManager;
        private MeshBuilder _meshBuilder;
        
        // Systems
        private HeightSystem _heightInference;
        private MaterialSystem _materialSystem;
        private TextureManager _textureManager;
        
        // Object management
        private Dictionary<string, MeshObject> _meshObjects = new();
        private Queue<string> _dirtyMeshes = new();
        private bool _batchUpdateMode = false;
        
        // Statistics
        public int TotalObjects => _meshObjects.Count;
        public int DirtyMeshCount => _dirtyMeshes.Count;
        
        public SpatialMap SpatialMap => _spatialMap;
        public TileMetadataManager MetadataManager => _metadataManager;
        
        public TextureManager TextureManager => _textureManager;
        
        public event EventHandler OnFullRebuildNeeded;
        public event EventHandler<string> OnObjectAdded;
        public event EventHandler<string> OnObjectUpdated;
        public event EventHandler<string> OnObjectRemoved;
        
        public World(MonitorHelper helper, GraphicsDevice device)
        {
            _helper = helper;
            _device = device;
            
            _spatialMap = new SpatialMap(_helper, 128);
            _metadataManager = new TileMetadataManager(helper);
            _meshBuilder = new MeshBuilder();
            _materialSystem = new MaterialSystem();
            _textureManager = new TextureManager(helper, device);
            _heightInference = new HeightSystem(helper, _metadataManager);
            
            _metadataManager.MetadataChanged += OnMetadataChanged;
        }
        
        /// <summary>
        /// Main entry point for world generation
        /// </summary>
        public void GenerateMap()
        {
            var location = Game1.currentLocation;
            if (location == null) return;
            
            _helper.Monitor.Log($"Generating map for {location.Name}", LogLevel.Info);
            var startTime = DateTime.Now;
            
            BeginBatchUpdate();
            
            // Clear existing data
            ClearWorld();
            
            // Get height map for this location
            var heightMap = _heightInference.InferHeights(location);
            
            // Process all world elements
            ProcessMapLayers(location, heightMap);
            ProcessWaterTiles(location, heightMap);
            ProcessTerrainFeatures(location, heightMap);
            ProcessLargeTerrainFeatures(location, heightMap);
            ProcessResourceClumps(location, heightMap);
            ProcessObjects(location, heightMap);
            ProcessFurniture(location, heightMap);
            ProcessBuildings(location, heightMap);
            ProcessDebris(location, heightMap);
            ProcessCharacters(location, heightMap);
            
            EndBatchUpdate();
            
            // Save metadata for new tiles
            _metadataManager.SaveIfDirty();
            
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            var stats = _spatialMap.GetStats();
            _helper.Monitor.Log($"Map generated in {elapsed:F0}ms: {stats.objectCount} objects in {stats.cellCount} cells", LogLevel.Info);
            OnFullRebuildNeeded?.Invoke(this, EventArgs.Empty);
        }
        
        private void ProcessMapLayers(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.map == null) return;
            
            foreach (var layer in location.map.Layers)
            {
                if (layer == null) continue;
                
                for (int x = 0; x < layer.LayerWidth; x++)
                {
                    for (int y = 0; y < layer.LayerHeight; y++)
                    {
                        var tile = layer.Tiles[x, y];
                        if (tile == null || tile.TileIndex <= 0) continue;
                        
                        ProcessMapTile(tile, x, y, layer.Id, heightMap);
                    }
                }
            }
        }
        
        private void ProcessMapTile(xTile.Tiles.Tile tile, int x, int y, string layerId, 
            Dictionary<(int, int), float> heightMap)
        {
            var tileId = $"{Game1.currentLocation.Name}_{x}_{y}_{layerId}";
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, 
                tile.TileSheet.Id, 
                tile.TileIndex, 
                layerId
            );
            
            // Get texture for this tile
            var texture = _textureManager.GetTexture(tile.TileSheet.ImageSource);
            if (texture == null) return;
            
            // Calculate position
            var baseHeight = GetHeightForTile(heightMap, x, y);
            var position = new Vector3(
                x * 64 + 32,
                baseHeight * 64,
                y * 64 + 32
            );
            
            // Create world object
            var tileRect = tile.TileSheet.GetTileImageBounds(tile.TileIndex);
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(64, 64, 64),
                tile.TileSheet.Id,
                tile.TileIndex,
                layerId,
                new Rectangle(tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height),
                Color.White,
                ObjectType.Tile
            );
            
            // Apply metadata
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            // Store texture reference
            worldObject.TextureId = _textureManager.RegisterTexture(texture, tile.TileSheet.ImageSource);
            
            //_helper.Monitor.Log($"Processing tile {tileId}, Position: {position}, Texture: {texture.Name}, TileRect: {tileRect}, BaseHeight: {baseHeight}, type: {metadata.MeshType}");
            // Create mesh object
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
            
            // Handle stacking
            if (metadata.StackingMode != TileStackingMode.None)
            {
                ProcessStackedTiles(metadata, worldObject, texture, x, y);
            }
        }
        
        private void ProcessWaterTiles(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.waterTiles == null) return;
            
            var waterColor = location.waterColor.Value * 0.8f; // Slightly transparent
            
            for (int x = 0; x < location.map.Layers[0].LayerWidth; x++)
            {
                for (int y = 0; y < location.map.Layers[0].LayerHeight; y++)
                {
                    if (!location.isWaterTile(x, y)) continue;
                    
                    var tileId = $"{location.Name}_water_{x}_{y}";
                    var metadata = _metadataManager.GetOrCreateMetadata(
                        tileId, "water", 0, "Water"
                    );
                    
                    // Water-specific metadata
                    metadata.MaterialType = "water";
                    metadata.IsTransparent = true;
                    metadata.HeightOffset = -0.1f;
                    metadata.MeshType = TileMeshType.Plane;
                    metadata.CastsShadows = false;
                    
                    var height = GetHeightForTile(heightMap, x, y);
                    var position = new Vector3(x * 64 + 32, height * 64, y * 64 + 32);
                    
                    var worldObject = new WorldObject(
                        tileId,
                        position,
                        new Vector3(64, 4, 64),
                        "water",
                        0,
                        "Water",
                        new Rectangle(0, 0, 16, 16),
                        waterColor,
                        ObjectType.Water
                    );
                    
                    worldObject.ApplyMetadata(metadata, _materialSystem);
                    
                    var meshObj = new MeshObject(tileId, worldObject)
                    {
                        Metadata = metadata
                    };
                    
                    if (!_batchUpdateMode)
                    {
                        GenerateMeshForObject(meshObj);
                    }
                    
                    _spatialMap.AddObject(meshObj);
                    _meshObjects[tileId] = meshObj;
                }
            }
        }
        
        #region Terrain Features
        private void ProcessTerrainFeatures(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.terrainFeatures == null) return;
            
            foreach (var kvp in location.terrainFeatures.Pairs)
            {
                var pos = kvp.Key;
                var feature = kvp.Value;
                if (feature == null) continue;
                ProcessTerrainFeature(location, feature, pos, heightMap);
            }
        }

        private void ProcessTerrainFeature(GameLocation location, TerrainFeature feature, Vector2 pos, Dictionary<(int, int), float> heightMap)
        {
            var height = GetHeightForTile(heightMap, (int)pos.X, (int)pos.Y);
            if (feature is HoeDirt dirt)
            {
                ProcessHoeDirt(dirt, pos, height);
            }
            else if (feature is Tree tree)
            {
                ProcessTree(tree, pos, height);
            }
            else if (feature is Grass grass)
            {
                ProcessGrass(grass, pos, height);
            }
            else if (feature is Bush bush)
            {
                ProcessBush(bush, pos, height);
            }
            else if (feature is Flooring flooring)
            {
                ProcessFlooring(flooring, pos, height);
            }
        }
        
        private void ProcessHoeDirt(HoeDirt dirt, Vector2 pos, float height)
        {
            var tileId = $"{Game1.currentLocation.Name}_hoedirt_{pos.X}_{pos.Y}";
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "hoedirt", 0, "TerrainFeatures"
            );
            
            metadata.MeshType = TileMeshType.Plane;
            metadata.MaterialType = "dirt";
            metadata.HeightOffset = 0.02f;
            
            var position = new Vector3(pos.X * 64 + 32, height * 64, pos.Y * 64 + 32);
            var dirtColor = new Color(101, 58, 23);
            
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(64, 2, 64),
                "hoedirt",
                0,
                "TerrainFeatures",
                new Rectangle(0, 0, 16, 16),
                dirtColor,
                ObjectType.Tile
            );
            
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
            
            // Process crop if present
            if (dirt.crop != null && !dirt.crop.dead.Value)
            {
                ProcessCrop(dirt.crop, pos, height + 0.1f);
            }
        }
        
        private void ProcessCrop(Crop crop, Vector2 pos, float height)
        {
            var tileId = $"{Game1.currentLocation.Name}_crop_{crop.indexOfHarvest.Value}_{pos.X}_{pos.Y}";
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "crops", crop.rowInSpriteSheet.Value, "Crops"
            );
            
            metadata.MeshType = TileMeshType.CrossedBillboard;
            metadata.MaterialType = "foliage";
            metadata.Passable = false;
            
            crop.updateDrawMath(pos);
            var cropHeight = 16 + (crop.currentPhase.Value * 8);
            var position = new Vector3(pos.X * 64 + 32, height * 64 + cropHeight/2, pos.Y * 64 + 32);
            
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(64, cropHeight, 64),
                "crops",
                crop.rowInSpriteSheet.Value,
                "Crops",
                crop.sourceRect,
                crop.tintColor.Value,
                ObjectType.TerrainFeature
            );
            
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            // Get crop texture
            var texture = _textureManager.GetTexture("TileSheets\\crops");
            if (texture != null)
            {
                worldObject.TextureId = _textureManager.RegisterTexture(texture, "crops");
            }
            
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessTree(Tree tree, Vector2 pos, float height)
        {
            var tileId = $"{Game1.currentLocation.Name}_tree_{tree.treeType.Value}_{pos.X}_{pos.Y}";
            int treeType = 0;
            int.TryParse(tree.treeType.Value, out treeType);
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "tree", treeType, "TerrainFeatures"
            );
            
            metadata.MeshType = TileMeshType.Billboard;
            metadata.MaterialType = "foliage";
            metadata.CastsShadows = true;
            metadata.Passable = false;
            
            Rectangle sourceRect;
            float treeHeight;
            
            if (tree.growthStage.Value < 5)
            {
                // Young tree stages
                switch (tree.growthStage.Value)
                {
                    case 0:
                        sourceRect = new Rectangle(32, 128, 16, 16);
                        treeHeight = 16;
                        break;
                    case 1:
                        sourceRect = new Rectangle(0, 128, 16, 16);
                        treeHeight = 24;
                        break;
                    case 2:
                        sourceRect = new Rectangle(16, 128, 16, 16);
                        treeHeight = 32;
                        break;
                    default:
                        sourceRect = new Rectangle(0, 96, 16, 32);
                        treeHeight = 64;
                        break;
                }
            }
            else
            {
                // Mature tree or stump
                sourceRect = tree.stump.Value ? Tree.stumpSourceRect : Tree.treeTopSourceRect;
                treeHeight = tree.stump.Value ? 32 : 192;
            }
            
            var position = new Vector3(pos.X * 64 + 32, height * 64 + treeHeight/2, pos.Y * 64 + 32);
            
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(128, treeHeight, 128),
                "tree",
                treeType,
                "TerrainFeatures",
                sourceRect,
                Color.White,
                ObjectType.TerrainFeature
            );
            
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            // Get tree texture
            var texture = tree.texture.Value;
            if (texture != null)
            {
                worldObject.TextureId = _textureManager.RegisterTexture(texture, $"tree_{tree.treeType.Value}");
            }
            
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessGrass(Grass grass, Vector2 pos, float height)
        {
            var tileId = $"{Game1.currentLocation.Name}_grass_{pos.X}_{pos.Y}";
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "grass", grass.grassType.Value, "TerrainFeatures"
            );
            
            metadata.MeshType = TileMeshType.CrossedBillboard;
            metadata.MaterialType = "foliage";
            metadata.Passable = true;
            
            var sourceRect = new Rectangle(
                grass.grassType.Value * 15, 
                grass.grassSourceOffset.Value, 
                15, 20
            );
            
            var position = new Vector3(pos.X * 64 + 32, height * 64 + 16, pos.Y * 64 + 32);
            
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(64, 32, 64),
                "grass",
                grass.grassType.Value,
                "TerrainFeatures",
                sourceRect,
                Color.White,
                ObjectType.TerrainFeature
            );
            
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            // Get grass texture
            var texture = grass.texture.Value;
            if (texture != null)
            {
                worldObject.TextureId = _textureManager.RegisterTexture(texture, "grass");
            }
            
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessBush(Bush bush, Vector2 pos, float height)
        {
            var tileId = $"{Game1.currentLocation.Name}_bush_{pos.X}_{pos.Y}";
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "bush", bush.size.Value, "TerrainFeatures"
            );
            
            metadata.MeshType = TileMeshType.Billboard;
            metadata.MaterialType = "foliage";
            
            var position = new Vector3(pos.X * 64 + 32, height * 64 + 32, pos.Y * 64 + 32);
            
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(64, 64, 64),
                "bush",
                bush.size.Value,
                "TerrainFeatures",
                bush.sourceRect.Value,
                Color.White,
                ObjectType.TerrainFeature
            );
            
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            // Get bush texture
            var texture = Bush.texture.Value;
            if (texture != null)
            {
                worldObject.TextureId = _textureManager.RegisterTexture(texture, "bush");
            }
            
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
        }
        #endregion
        
        private void ProcessFlooring(Flooring flooring, Vector2 pos, float height)
        {
            var tileId = $"{Game1.currentLocation.Name}_flooring_{pos.X}_{pos.Y}";
            int flooringType = 0;
            int.TryParse(flooring.whichFloor.Value, out flooringType);
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "flooring", flooringType, "TerrainFeatures"
            );
            
            metadata.MeshType = TileMeshType.Plane;
            metadata.MaterialType = "stone";
            metadata.HeightOffset = 0.01f;
            metadata.IsFloor = true;
            
            var position = new Vector3(pos.X * 64 + 32, height * 64, pos.Y * 64 + 32);
            
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(64, 4, 64),
                "flooring",
                flooringType,
                "TerrainFeatures",
                new Rectangle(flooring.GetTextureCorner(), new Point(16, 16)),
                Color.White,
                ObjectType.Tile
            );
            
            worldObject.ApplyMetadata(metadata, _materialSystem);
            
            // Get flooring texture
            var texture = flooring.GetTexture();
            if (texture != null)
            {
                worldObject.TextureId = _textureManager.RegisterTexture(texture, "flooring");
                _helper.Monitor.Log($"Found texture for: {flooringType}, whichFloor: {flooring.whichFloor.Value}, texture: {texture}");
            }
            else
            {
                _helper.Monitor.Log($"Failed to get texture for: {flooringType}, whichFloor: {flooring.whichFloor.Value}");
            }
            
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
            
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
            
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessLargeTerrainFeatures(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.largeTerrainFeatures == null) return;
            
            foreach (var feature in location.largeTerrainFeatures)
            {
                if (feature == null) continue;
                ProcessLargeTerrainFeature(location, feature, heightMap);
            }
        }

        private void ProcessLargeTerrainFeature(GameLocation location, LargeTerrainFeature feature, Dictionary<(int, int), float> heightMap)
        {
            var height = GetHeightForTile(heightMap, (int)feature.Tile.X, (int)feature.Tile.Y);
            var tileId = $"{location.Name}_largeterrain_{feature.Tile.X}_{feature.Tile.Y}";
            var metadata = _metadataManager.GetOrCreateMetadata(
                tileId, "largeterrain", 0, "LargeTerrainFeatures"
            );
                
            metadata.MeshType = TileMeshType.Billboard;
            metadata.MaterialType = "foliage";
            metadata.CastsShadows = true;
                
            var bounds = feature.getBoundingBox();
                
            var position = new Vector3(
                feature.Tile.X * 64 + bounds.Width / 2,
                height * 64 + bounds.Height / 2,
                feature.Tile.Y * 64 + bounds.Height / 2
            );
                
            var worldObject = new WorldObject(
                tileId,
                position,
                new Vector3(bounds.Width, bounds.Height, bounds.Width),
                "largeterrain",
                0,
                "LargeTerrainFeatures",
                new Rectangle(0, 0, 64, 64),
                Color.White,
                ObjectType.TerrainFeature
            );
                
            worldObject.ApplyMetadata(metadata, _materialSystem);
                
            var meshObj = new MeshObject(tileId, worldObject)
            {
                Metadata = metadata
            };
                
            if (!_batchUpdateMode)
            {
                GenerateMeshForObject(meshObj);
            }
                
            _spatialMap.AddObject(meshObj);
            _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessResourceClumps(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.resourceClumps == null) return;
            
            foreach (var clump in location.resourceClumps)
            {
                if (clump == null) continue;
                
                var tileId = $"{location.Name}_resourceclump_{clump.parentSheetIndex.Value}_{clump.Tile.X}_{clump.Tile.Y}";
                var metadata = _metadataManager.GetOrCreateMetadata(
                    tileId, "resourceclump", clump.parentSheetIndex.Value, "ResourceClumps"
                );
                
                metadata.MeshType = TileMeshType.Box;
                metadata.MaterialType = clump.parentSheetIndex.Value == 752 ? "stone" : "wood";
                metadata.Passable = false;
                
                var height = GetHeightForTile(heightMap, (int)clump.Tile.X, (int)clump.Tile.Y);
                var position = new Vector3(
                    clump.Tile.X * 64 + clump.width.Value * 32,
                    height * 64 + 64,
                    clump.Tile.Y * 64 + clump.height.Value * 32
                );
                
                var worldObject = new WorldObject(
                    tileId,
                    position,
                    new Vector3(clump.width.Value * 64, 128, clump.height.Value * 64),
                    "resourceclump",
                    clump.parentSheetIndex.Value,
                    "ResourceClumps",
                    new Rectangle(
                        clump.parentSheetIndex.Value % 8 * 16,
                        clump.parentSheetIndex.Value / 8 * 16,
                        clump.width.Value * 16,
                        clump.height.Value * 16
                    ),
                    Color.White,
                    ObjectType.Object
                );
                
                worldObject.ApplyMetadata(metadata, _materialSystem);
                
                // Get object sprite sheet
                var texture = Game1.objectSpriteSheet;
                if (texture != null)
                {
                    worldObject.TextureId = _textureManager.RegisterTexture(texture, "objects");
                }
                
                var meshObj = new MeshObject(tileId, worldObject)
                {
                    Metadata = metadata
                };
                
                if (!_batchUpdateMode)
                {
                    GenerateMeshForObject(meshObj);
                }
                
                _spatialMap.AddObject(meshObj);
                _meshObjects[tileId] = meshObj;
            }
        }
        
        private void ProcessObjects(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.objects == null) return;
            
            foreach (var kvp in location.Objects.Pairs)
            {
                var pos = kvp.Key;
                var obj = kvp.Value;
                if (obj == null) continue;
                ProcessObject(location, obj, pos, heightMap);
            }
        }

        private void ProcessObject(GameLocation location, Object obj, Vector2 pos, Dictionary<(int, int), float> heightMap)
        {
                var tileId = $"{location.Name}_object_{obj.ItemId}_{pos.X}_{pos.Y}";
                var metadata = _metadataManager.GetOrCreateMetadata(
                    tileId, "objects", obj.ParentSheetIndex, "Objects"
                );
                
                // Determine object properties
                bool isFence = obj.DisplayName.Contains("Fence") || obj.DisplayName.Contains("Gate");
                bool isWall = obj.DisplayName.Contains("Wall");
                bool isChest = obj is Chest;
                
                metadata.MeshType = isFence ? TileMeshType.Box : 
                                  isWall ? TileMeshType.SplitBox : 
                                  TileMeshType.Billboard;
                metadata.MaterialType = isFence ? "wood" : 
                                       isWall ? "stone" : 
                                       "default";
                metadata.Passable = !isFence && !isWall && !isChest;
                
                var height = GetHeightForTile(heightMap, (int)pos.X, (int)pos.Y);
                var objHeight = isFence ? 48 : isWall ? 96 : 32;
                
                ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(obj.QualifiedItemId);
                var texture = itemData.GetTexture();
                var sourceRect = itemData.GetSourceRect(0, obj.ParentSheetIndex);
                
                var position = new Vector3(pos.X * 64 + 32, height * 64 + objHeight/2, pos.Y * 64 + 32);
                
                var worldObject = new WorldObject(
                    tileId,
                    position,
                    new Vector3(obj.boundingBox.Value.Width, objHeight, obj.boundingBox.Value.Width),
                    "objects",
                    obj.ParentSheetIndex,
                    "Objects",
                    sourceRect,
                    Color.White,
                    isFence || isWall ? ObjectType.Object : ObjectType.Sprite
                );
                
                worldObject.ApplyMetadata(metadata, _materialSystem);
                
                if (texture != null)
                {
                    worldObject.TextureId = _textureManager.RegisterTexture(texture, "objects");
                }
                
                var meshObj = new MeshObject(tileId, worldObject)
                {
                    Metadata = metadata
                };
                
                if (!_batchUpdateMode)
                {
                    GenerateMeshForObject(meshObj);
                }
                
                _spatialMap.AddObject(meshObj);
                _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessFurniture(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.furniture == null) return;
            
            foreach (var furniture in location.furniture)
            {
                if (furniture == null) continue;
                ProcessSingleFurniture(location, furniture, furniture.TileLocation, heightMap);;
            }
        }

        private void ProcessSingleFurniture(GameLocation location, Furniture furniture, Vector2 pos,
            Dictionary<(int, int), float> heightMap)
        {
                var tileId = $"{location.Name}_furniture_{furniture.itemId.Value}_{furniture.TileLocation.X}_{furniture.TileLocation.Y}";
                var metadata = _metadataManager.GetOrCreateMetadata(
                    tileId, "furniture", furniture.ParentSheetIndex, "Furniture"
                );
                
                // Determine furniture type
                bool isTable = furniture.furniture_type.Value == 11;
                bool isRug = furniture.furniture_type.Value == 12;
                bool isBed = furniture.DisplayName.Contains("Bed");
                
                metadata.MeshType = isRug ? TileMeshType.Plane : TileMeshType.Box;
                metadata.MaterialType = "wood";
                metadata.IsFloor = isRug;
                metadata.Passable = isRug;
                
                var height = GetHeightForTile(heightMap, (int)furniture.TileLocation.X, (int)furniture.TileLocation.Y);
                var furnitureHeight = isRug ? 4 : isTable ? 48 : isBed ? 32 : 64;
                
                ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(furniture.QualifiedItemId);
                var texture = itemData.GetTexture();
                
                var position = new Vector3(
                    furniture.TileLocation.X * 64 + furniture.boundingBox.Width / 2,
                    height * 64 + furnitureHeight / 2,
                    furniture.TileLocation.Y * 64 + furniture.boundingBox.Height / 2
                );
                
                var worldObject = new WorldObject(
                    tileId,
                    position,
                    new Vector3(furniture.boundingBox.Width, furnitureHeight, furniture.boundingBox.Height),
                    "furniture",
                    furniture.ParentSheetIndex,
                    "Furniture",
                    furniture.sourceRect.Value,
                    Color.White,
                    isRug ? ObjectType.Tile : ObjectType.Object
                );
                
                worldObject.ApplyMetadata(metadata, _materialSystem);
                
                if (texture != null)
                {
                    worldObject.TextureId = _textureManager.RegisterTexture(texture, "furniture");
                }
                
                var meshObj = new MeshObject(tileId, worldObject)
                {
                    Metadata = metadata
                };
                
                if (!_batchUpdateMode)
                {
                    GenerateMeshForObject(meshObj);
                }
                
                _spatialMap.AddObject(meshObj);
                _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessBuildings(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.buildings == null) return;
            
            foreach (var building in location.buildings)
            {
                if (building == null) continue;
                ProcessBuilding(location,building, heightMap);
            }
        }

        private void ProcessBuilding(GameLocation location, Building building, Dictionary<(int, int), float> heightMap)
        {
            
                var tileId = $"{location.Name}_building_{building.buildingType.Value}_{building.tileX.Value}_{building.tileY.Value}";
                var metadata = _metadataManager.GetOrCreateMetadata(
                    tileId, "buildings", 0, "Buildings"
                );
                
                metadata.MeshType = TileMeshType.SplitBox;
                metadata.MaterialType = "wood";
                metadata.SplitRatio = 0.3f;
                metadata.Passable = false;
                metadata.CastsShadows = true;
                
                var height = GetHeightForTile(heightMap, (int)building.tileX.Value, (int)building.tileY.Value);
                
                var buildingSize = new Vector3(
                    building.tilesWide.Value * 64,
                    building.tilesHigh.Value * 64,
                    building.tilesHigh.Value * 64
                );
                
                // Adjust for specific building types
                if (building.buildingType.Value?.Contains("Coop") == true || 
                    building.buildingType.Value?.Contains("Barn") == true)
                {
                    buildingSize.Y *= 1.5f;
                }
                
                var position = new Vector3(
                    building.tileX.Value * 64 + buildingSize.X / 2,
                    height * 64 + buildingSize.Y / 2,
                    building.tileY.Value * 64 + buildingSize.Z / 2
                );
                
                var worldObject = new WorldObject(
                    tileId,
                    position,
                    buildingSize,
                    "buildings",
                    0,
                    "Buildings",
                    building.getSourceRect(),
                    Color.White,
                    ObjectType.Building
                );
                
                worldObject.ApplyMetadata(metadata, _materialSystem);
                
                // Get building texture
                var texture = building.texture.Value;
                if (texture != null)
                {
                    worldObject.TextureId = _textureManager.RegisterTexture(texture, $"building_{building.buildingType.Value}");
                }
                
                var meshObj = new MeshObject(tileId, worldObject)
                {
                    Metadata = metadata
                };
                
                if (!_batchUpdateMode)
                {
                    GenerateMeshForObject(meshObj);
                }
                
                _spatialMap.AddObject(meshObj);
                _meshObjects[tileId] = meshObj;
        }
        
        private void ProcessDebris(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            if (location.debris == null) return;
            
            foreach (var debris in location.debris)
            {
                if (debris == null || debris.Chunks.Count == 0) continue;
                ProcessSingleDebris(location, debris, heightMap);
            }
        }

        private void ProcessSingleDebris(GameLocation location, Debris debris, Dictionary<(int, int), float> heightMap)
        {
            foreach (var chunk in debris.Chunks)
                {
                    var tilePos = new Vector2(chunk.position.Value.X / 64, chunk.position.Value.Y / 64);
                    var tileId = $"{location.Name}_debris_{chunk.position.Value.X}_{chunk.position.Value.Y}";
                    
                    var metadata = _metadataManager.GetOrCreateMetadata(
                        tileId, "debris", chunk.netDebrisType.Value, "Debris"
                    );
                    
                    metadata.MeshType = TileMeshType.Billboard;
                    metadata.Passable = true;
                    
                    var height = GetHeightForTile(heightMap, (int)tilePos.X, (int)tilePos.Y);
                    var position = new Vector3(chunk.position.Value.X, height * 64 + 8, chunk.position.Value.Y);
                    
                    var worldObject = new WorldObject(
                        tileId,
                        position,
                        new Vector3(16, 16, 16),
                        "debris",
                        chunk.netDebrisType.Value,
                        "Debris",
                        new Rectangle(chunk.netDebrisType.Value * 16, 0, 16, 16),
                        Color.White,
                        ObjectType.Debris
                    );
                    
                    worldObject.ApplyMetadata(metadata, _materialSystem);
                    
                    var texture = Game1.objectSpriteSheet;
                    if (texture != null)
                    {
                        worldObject.TextureId = _textureManager.RegisterTexture(texture, "objects");
                    }
                    
                    var meshObj = new MeshObject(tileId, worldObject)
                    {
                        Metadata = metadata
                    };
                    
                    if (!_batchUpdateMode)
                    {
                        GenerateMeshForObject(meshObj);
                    }
                    
                    _spatialMap.AddObject(meshObj);
                    _meshObjects[tileId] = meshObj;
                }
        }
        
        private void ProcessCharacters(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            // Process NPCs (dynamic, don't create meshes yet)
            foreach (var npc in location.characters)
            {
                if (npc == null) continue;
                
                var tileId = $"npc_{npc.Name}";
                var metadata = _metadataManager.GetOrCreateMetadata(
                    tileId, "characters", 0, "NPCs"
                );
                
                metadata.MeshType = TileMeshType.Billboard;
                metadata.MaterialType = "character";
                metadata.CastsShadows = true;
                
                var position = new Vector3(npc.Position.X, 68, npc.Position.Y);
                
                var worldObject = new WorldObject(
                    tileId,
                    position,
                    new Vector3(64, 92, 64),
                    "characters",
                    0,
                    "NPCs",
                    npc.Sprite.sourceRect,
                    Color.White,
                    ObjectType.Sprite
                );
                
                worldObject.ApplyMetadata(metadata, _materialSystem);
                
                // Dynamic objects don't get meshes in batch mode
                var meshObj = new MeshObject(tileId, worldObject)
                {
                    Metadata = metadata
                };
                
                _spatialMap.AddObject(meshObj);
                _meshObjects[tileId] = meshObj;
            }
        }
        
        private void ProcessStackedTiles(TileMetadata baseTile, WorldObject baseObject, 
            Texture2D texture, int x, int y)
        {
            if (baseTile.StackedTileIndices == null || baseTile.StackedTileIndices.Count == 0)
                return;
            
            float currentHeight = baseObject.Position.Y / 64 + baseTile.StackHeight;
            int stackIndex = 0;
            
            foreach (var tileIndex in baseTile.StackedTileIndices)
            {
                var stackId = $"{baseTile.TileId}_stack_{stackIndex}";
                var stackPosition = new Vector3(baseObject.Position.X, currentHeight * 64, baseObject.Position.Z);
                
                var stackedObject = new WorldObject(
                    stackId,
                    stackPosition,
                    baseObject.Size,
                    baseObject.TileSheet,
                    tileIndex,
                    baseObject.Layer + "_stack",
                    GetTileSourceRect(baseTile.TileSheet, tileIndex),
                    baseObject.Tint,
                    ObjectType.Object
                );
                
                stackedObject.ApplyMetadata(baseTile, _materialSystem);
                stackedObject.TextureId = baseObject.TextureId;
                
                var stackMeshObj = new MeshObject(stackId, stackedObject)
                {
                    Metadata = baseTile
                };
                
                if (!_batchUpdateMode)
                {
                    GenerateMeshForObject(stackMeshObj);
                }
                
                _spatialMap.AddObject(stackMeshObj);
                _meshObjects[stackId] = stackMeshObj;
                
                currentHeight += baseTile.StackHeight;
                stackIndex++;
            }
        }
        
        // Add methods to handle specific world changes
        public void HandleObjectListChanged(ObjectListChangedEventArgs e)
        {
            var location = e.Location;
            
            // Handle removed objects
            foreach (var kvp in e.Removed)
            {
                var pos = kvp.Key;
                var obj = kvp.Value;
                var objectId = $"{location.Name}_object_{obj.ItemId}_{pos.X}_{pos.Y}";
                RemoveObject(objectId);
            }
            
            // Handle added objects
            var heightMap = _heightInference.InferHeights(location);
            foreach (var kvp in e.Added)
            {
                var pos = kvp.Key;
                var obj = kvp.Value;
                ProcessObject(location, obj, pos, heightMap);
            }
        }

        public void HandleTerrainFeatureListChanged(TerrainFeatureListChangedEventArgs e)
        {
            var location = e.Location;
            
            // Handle removed terrain features
            foreach (var kvp in e.Removed)
            {
                var pos = kvp.Key;
                var feature = kvp.Value;
                var featureId = GetTerrainFeatureId(location, feature, pos);
                RemoveObject(featureId);
            }
            
            // Handle added terrain features
            var heightMap = _heightInference.InferHeights(location);
            foreach (var kvp in e.Added)
            {
                var pos = kvp.Key;
                var feature = kvp.Value;
                ProcessTerrainFeature(location, feature, pos, heightMap);
            }
        }



        public void HandleBuildingListChanged(BuildingListChangedEventArgs e)
        {
            var location = e.Location;
            var heightMap = _heightInference.InferHeights(location);
            
            // Handle removed buildings
            foreach (var building in e.Removed)
            {
                var buildingId = $"{location.Name}_building_{building.buildingType.Value}_{building.tileX.Value}_{building.tileY.Value}";
                RemoveObject(buildingId);
            }
            
            // Handle added buildings
            foreach (var building in e.Added)
            {
                ProcessBuilding(location, building, heightMap);
            }
        }

        public void HandleFurnitureListChanged(FurnitureListChangedEventArgs e)
        {
            var location = e.Location;
            var heightMap = _heightInference.InferHeights(location);
            
            // Handle removed furniture
            foreach (var furniture in e.Removed)
            {
                var furnitureId = $"{location.Name}_furniture_{furniture.ItemId}_{furniture.TileLocation.X}_{furniture.TileLocation.Y}";
                RemoveObject(furnitureId);
            }
            
            // Handle added furniture
            foreach (var furniture in e.Added)
            {
                ProcessSingleFurniture(location, furniture, furniture.TileLocation, heightMap);
            }
        }

        public void HandleLargeTerrainFeatureListChanged(LargeTerrainFeatureListChangedEventArgs e)
        {
            var location = e.Location;
            var heightMap = _heightInference.InferHeights(location);
            
            // Handle removed large terrain features
            foreach (var feature in e.Removed)
            {
                var featureId = $"{location.Name}_largeterrain_{feature.Tile.X}_{feature.Tile.Y}";
                RemoveObject(featureId);
            }
            
            // Handle added large terrain features
            foreach (var feature in e.Added)
            {
                ProcessLargeTerrainFeature(location, feature, heightMap);
            }
        }

        public void HandleDebrisListChanged(DebrisListChangedEventArgs e)
        {
            var location = e.Location;
            var heightMap = _heightInference.InferHeights(location);
            
            // Handle removed debris
            foreach (var debris in e.Removed)
            {
                var debrisId = $"{location.Name}_debris_{debris.item?.ItemId ?? "unknown"}_{debris.Chunks[0].position.X}_{debris.Chunks[0].position.Y}";
                RemoveObject(debrisId);
            }
            
            // Handle added debris
            foreach (var debris in e.Added)
            {
                ProcessSingleDebris(location, debris, heightMap);
            }
        }
        
        public void UpdateSingleObject(string objectId)
        {
            if (!_meshObjects.TryGetValue(objectId, out var meshObj))
                return;
    
            // Only regenerate if actually dirty
            if (meshObj.IsDirty)
            {
                GenerateMeshForObject(meshObj);
                meshObj.IsDirty = false;
        
                // Notify the rendering pipeline
                OnObjectUpdated?.Invoke(this, objectId);
        
                _helper.Monitor.Log($"Updated single object: {objectId}", LogLevel.Debug);
            }
        }
        
        
        // Helper to generate consistent IDs
        private string GetTerrainFeatureId(GameLocation location, TerrainFeature feature, Vector2 pos)
        {
            if (feature is Tree tree)
                return $"{location.Name}_tree_{tree.treeType.Value}_{pos.X}_{pos.Y}";
            else if (feature is Grass)
                return $"{location.Name}_grass_{pos.X}_{pos.Y}";
            else if (feature is HoeDirt)
                return $"{location.Name}_hoedirt_{pos.X}_{pos.Y}";
            else if (feature is Bush bush)
                return $"{location.Name}_bush_{pos.X}_{pos.Y}";
            else if (feature is Flooring flooring)
                return $"{location.Name}_flooring_{pos.X}_{pos.Y}";
            else
                return $"{location.Name}_terrain_{feature.GetType().Name}_{pos.X}_{pos.Y}";
        }

        // Add method to get all objects for instance updates
        public List<MeshObject> GetAllMeshObjects()
        {
            return _meshObjects.Values.ToList();
        }
        
        private void GenerateMeshForObject(MeshObject meshObj)
        {
            _meshBuilder.Clear();
            var metadata = meshObj.Metadata;
            var obj = meshObj.WorldObject;
            
            var texture = _textureManager.GetTextureById(obj.TextureId);
            if (texture == null)
            {
                // Create fallback texture
                texture = new Texture2D(_device, 1, 1);
                texture.SetData(new[] { obj.Tint });
            }
            if (metadata.BoxComposition != null && metadata.BoxComposition.Boxes.Count > 0)
            {
                var mesh = _meshBuilder.BuildFromComposition(metadata.BoxComposition, texture, _device);
                meshObj.UpdateMesh(mesh);
            }
            else
            {

                var instanceable = RenderingPipeline.IsInstanceableExt(meshObj);
                obj.Position -= (Vector3.Right) * 32;
                Vector3 origin = Vector3.Left * 32;
                switch (metadata.MeshType)
                {
                    case TileMeshType.Box:
                        _meshBuilder.AddBox(instanceable ? origin : obj.Position, obj.Size, obj.SourceRectangle,
                            texture);
                        break;

                    case TileMeshType.Plane:
                        _meshBuilder.AddPlane(instanceable ? origin : obj.Position, obj.Size, obj.SourceRectangle,
                            texture);
                        break;

                    case TileMeshType.Billboard:
                        _meshBuilder.AddBillboard(instanceable ? origin : obj.Position,
                            new Vector2(obj.Size.X, obj.Size.Y),
                            obj.SourceRectangle, texture);
                        break;

                    case TileMeshType.SplitBox:
                        _meshBuilder.AddSplitBox(instanceable ? origin : obj.Position, obj.Size,
                            obj.SourceRectangle, texture,
                            metadata.SplitRatio, false);
                        break;

                    case TileMeshType.CrossedBillboard:
                        _meshBuilder.AddCrossedBillboards(instanceable ? origin : obj.Position, obj.Size,
                            obj.SourceRectangle, texture);
                        break;
                }

                var mesh = _meshBuilder.BuildMesh(_device);
                meshObj.UpdateMesh(mesh);
            }
        }
        
        // Update methods
        public void UpdateDynamicObjects()
        {
            // Update NPCs
            foreach (var npc in Game1.currentLocation.characters)
            {
                if (npc == null) continue;
                
                var tileId = $"npc_{npc.Name}";
                if (_meshObjects.TryGetValue(tileId, out var meshObj))
                {
                    var newPosition = new Vector3(npc.Position.X, 68, npc.Position.Y);
                    meshObj.WorldObject.Position = newPosition;
                    meshObj.WorldObject.SourceRectangle = npc.Sprite.sourceRect;
                    meshObj.UpdateBounds();
                    
                    _spatialMap.UpdateObject(tileId, newPosition);
                }
            }
        }
        
        public void UpdateDirtyMeshes(int maxUpdatesPerFrame = 5)
        {
            int updated = 0;
    
            while (_dirtyMeshes.Count > 0 && updated < maxUpdatesPerFrame)
            {
                var tileId = _dirtyMeshes.Dequeue();
        
                if (_meshObjects.TryGetValue(tileId, out var meshObj) && meshObj.IsDirty)
                {
                    GenerateMeshForObject(meshObj);
                    meshObj.IsDirty = false;
            
                    // Notify about the update
                    OnObjectUpdated?.Invoke(this, tileId);
                    updated++;
                }
            }
        }
        
        public void RemoveObject(string objectId)
        {
            if (_meshObjects.TryGetValue(objectId, out var meshObj))
            {
                // Dispose the mesh
                meshObj.Mesh?.Dispose();
        
                // Remove from spatial map
                _spatialMap.RemoveObject(objectId);
        
                // Remove from our tracking
                _meshObjects.Remove(objectId);
        
                // Notify renderer to remove from instances
                OnObjectRemoved?.Invoke(this, objectId);
        
                _helper.Monitor.Log($"Removed object: {objectId}", LogLevel.Debug);
            }
        }

        public void AddOrUpdateObject(string objectId, MeshObject meshObj)
        {
            bool isNew = !_meshObjects.ContainsKey(objectId);
    
            // Add to tracking
            _meshObjects[objectId] = meshObj;
    
            // Generate mesh if needed
            if (meshObj.Mesh == null)
            {
                GenerateMeshForObject(meshObj);
            }
    
            // Add to spatial map
            _spatialMap.AddObject(meshObj);
    
            // Notify renderer
            if (isNew)
            {
                OnObjectAdded?.Invoke(this, objectId);
            }
            else
            {
                OnObjectUpdated?.Invoke(this, objectId);
            }
    
            _helper.Monitor.Log($"{(isNew ? "Added" : "Updated")} object: {objectId}", LogLevel.Debug);
        }

        
        public List<MeshObject> GetVisibleObjects(Camera3D camera)
        {
            var frustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);
            return _spatialMap.GetVisibleObjects(frustum);
        }
        
        // Helper methods
        private void ClearWorld()
        {
            foreach (var meshObj in _meshObjects.Values)
            {
                meshObj.Mesh?.Dispose();
            }
            
            _spatialMap.Clear();
            _meshObjects.Clear();
            _dirtyMeshes.Clear();
        }
        
        private void BeginBatchUpdate()
        {
            _batchUpdateMode = true;
        }
        
        private void EndBatchUpdate()
        {
            _batchUpdateMode = false;
    
            // Generate all meshes without triggering individual events
            var updatedObjects = new List<string>();
            foreach (var meshObj in _meshObjects.Values)
            {
                if (meshObj.Mesh == null)
                {
                    GenerateMeshForObject(meshObj);
                    updatedObjects.Add(meshObj.Id);
                }
            }
    
            // After batch is complete, signal full rebuild is needed
            if (updatedObjects.Count > 0)
            {
                OnFullRebuildNeeded?.Invoke(this, EventArgs.Empty);
            }
        }
        

        
        private void OnMetadataChanged(object sender, TileMetadata metadata)
        {
            if (_meshObjects.TryGetValue(metadata.TileId, out var meshObj))
            {
                meshObj.IsDirty = true;
        
                if (!_batchUpdateMode)
                {
                    UpdateSingleObject(metadata.TileId);
                }
                else
                {
                    _dirtyMeshes.Enqueue(metadata.TileId);
                }
            }
        }
        
        private float GetHeightForTile(Dictionary<(int, int), float> heightMap, int x, int y)
        {
            return heightMap.TryGetValue((x, y), out var h) ? h : 0f;
        }
        
        private Rectangle GetTileSourceRect(string tileSheet, int tileIndex)
        {
            int tilesPerRow = 16;
            int tileSize = 16;
            
            int row = tileIndex / tilesPerRow;
            int col = tileIndex % tilesPerRow;
            
            return new Rectangle(col * tileSize, row * tileSize, tileSize, tileSize);
        }
    }
    /// <summary>
    /// Simple texture manager for the world
    /// </summary>
    public class TextureManager
    {
        private MonitorHelper _helper;
        private GraphicsDevice _device;
        private Dictionary<string, Texture2D> _textures = new();
        private Dictionary<int, Texture2D> _textureById = new();
        private Dictionary<string, int> _textureKeyToId = new(); // NEW: Track which keys map to which IDs
        private int _nextId = 1;
        
        public TextureManager(MonitorHelper helper, GraphicsDevice device)
        {
            _helper = helper;
            _device = device;
        }
        
        public Texture2D GetTexture(string path)
        {
            if (_textures.TryGetValue(path, out var cached))
                return cached;
            
            try
            {
                var texture = _helper.GameContent.Load<Texture2D>(path);
                _textures[path] = texture;
                return texture;
            }
            catch
            {
                _helper.Monitor.Log($"Failed to load texture: {path}", LogLevel.Warn);
                return null;
            }
        }
        
        public int RegisterTexture(Texture2D texture, string key)
        {
            // Check if we already have an ID for this key
            if (_textureKeyToId.TryGetValue(key, out var existingId))
            {
                return existingId; // Return existing ID instead of creating a new one
            }
            
            // Store the texture by key if not already stored
            if (!_textures.ContainsKey(key))
            {
                _textures[key] = texture;
            }
            
            // Create a new ID only for new textures
            var id = _nextId++;
            _textureById[id] = texture;
            _textureKeyToId[key] = id; // Map the key to this ID
            
            _helper.Monitor.Log($"Registered texture '{key}' with ID {id}", LogLevel.Debug);
            
            return id;
        }
        
        public Texture2D GetTextureById(int id)
        {
            return _textureById.TryGetValue(id, out var texture) ? texture : null;
        }
        
        // Helper method to get texture ID without creating a new one
        public int? GetTextureId(string key)
        {
            return _textureKeyToId.TryGetValue(key, out var id) ? id : null;
        }
        
        // Debug method to log texture statistics
        public void LogStats()
        {
            _helper.Monitor.Log($"TextureManager: {_textures.Count} unique textures, {_textureKeyToId.Count} keys, {_textureById.Count} IDs", LogLevel.Debug);
        }
    }
}