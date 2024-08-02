using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
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
}

public class Map
{
    public List<WorldTile> Tiles = new List<WorldTile>();
    private bool Generating = false;
    
    public Dictionary<Point, List<WorldTile>> spatialGrid;
    private const int GridCellSize = 64; // Example cell size, adjust as needed
    private Point gridSize;
    
    public Map()
    {
        spatialGrid = new Dictionary<Point, List<WorldTile>>();
    }
    
    public void GenerateMap()
    {
        Generating = true;
        Tiles.Clear();
        spatialGrid.Clear();
        foreach (var layer in Game1.game1.instanceGameLocation.backgroundLayers)
        {
            for (var x = 0; x < layer.Key.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Key.Tiles.Array.GetLength(1); y++)
                {

                    Color color = new Color(36, 36, 36, 255);
                    var tile = layer.Key.Tiles[x, y];
                    if (tile != null && tile.TileIndex != 0)
                    {
                        if (tile.TileIndex == 470 || tile.TileIndex == 271 || tile.TileIndex == 64 ||
                            tile.Properties.Keys.Count(x => x.Contains("Wall")) > 0)
                        {
                            color = new Color(36, 0, 0, 255);
                            var wtile = new WorldTile()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f)
                            };
                        }
                        else if (tile.Properties.Count == 0 || tile.Properties.Keys.Count(x => x.Contains("Floor")) > 0)
                        {
                            var wtile = new WorldTile()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 64.0f, 64.0f)
                            };

                            Tiles.Add(wtile);
                            AddTileToGrid(wtile);
                        }
                    }
                }
            }
        }

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
                            var wtile = new WorldTile()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f)
                            };

                            Tiles.Add(wtile);
                            AddTileToGrid(wtile);
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
                        var wtile = new WorldTile()
                        {
                            Color = waterColor,
                            Position = new Vector3(x * 64, 0 * 64, y * 64),
                            Size = new Vector3(64.0f, 64.0f,64.0f)
                        };

                        Tiles.Add(wtile);
                        AddTileToGrid(wtile);
                    }
                }
            }
        }

        foreach (var building in Game1.game1.instanceGameLocation.buildings)
        {
            if (building == null)
                continue;
            for (int tX = 0; tX < building.tilesWide.Value; tX++)
            for (int tY = 0; tY < building.tilesHigh.Value; tY++)
            {
                var wtile = new WorldTile()
                {
                    Color = new Color(145, 113, 81, 255),
                    Position = new Vector3((building.tileX.Value + tX) * 64, 1 * 32, (building.tileY.Value + tY) * 64),
                    Size = new Vector3(64.0f, 128.0f, 64.0f)
                };

                Tiles.Add(wtile);
                AddTileToGrid(wtile);
            }
        }

        foreach (var furniture in Game1.game1.instanceGameLocation.furniture)
        {
            if (furniture == null)
                continue;
            for (int tX = 0; tX < (furniture.boundingBox.Width / 64); tX++)
            for (int tY = 0; tY < (furniture.boundingBox.Height / 64); tY++)
            {
                var wtile =new WorldTile()
                {
                    Color = new Color(92, 46, 0, 255),
                    Position = new Vector3((furniture.TileLocation.X + tX) * 64, 1 * 32,
                        (furniture.TileLocation.Y + tY) * 64),
                    Size = new Vector3(64.0f, 32.0f, 64.0f)
                };

                Tiles.Add(wtile);
                AddTileToGrid(wtile);
            }
        }

        foreach (var wObject in Game1.game1.instanceGameLocation.objects.Values)
        {
            if (wObject == null)
                continue;
            for (int tX = 0; tX < (wObject.boundingBox.Width / 64); tX++)
            for (int tY = 0; tY < (wObject.boundingBox.Height / 64); tY++)
            {
                var wtile =new WorldTile()
                {
                    Color = new Color(0, 0, 255, 255),
                    Position = new Vector3((wObject.TileLocation.X + tX) * 64, 1 * 32,
                        (wObject.TileLocation.Y + tY) * 64),
                    Size = new Vector3(32.0f, 16.0f, 32.0f)
                };

                Tiles.Add(wtile);
                AddTileToGrid(wtile);
            }
        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                WorldTile wtile = new WorldTile();
                if (tf is Tree)
                    wtile = new WorldTile()
                    {
                        Color = new Color(41, 25, 5, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64),
                        Size = new Vector3(64.0f, 128.0f, 64.0f)
                    };
                else if (tf is HoeDirt { crop: null })
                    wtile = new WorldTile()
                    {
                        Color = new Color(41, 25, 5, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 0, (tf.Tile.Y) * 64),
                        Size = new Vector3(64.0f, 64.0f, 64.0f)
                    };
                else if (tf is HoeDirt { crop: not null })
                    wtile = new WorldTile()
                    {
                        Color = new Color(0, 255, 0, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64),
                        Size = new Vector3(32.0f, 32.0f, 32.0f)
                    };

                Tiles.Add(wtile);
                AddTileToGrid(wtile);
            }
        }
        
        // Define the grid size based on the world dimensions
        gridSize = new Point(
            (int)Math.Ceiling(Game1.game1.instanceGameLocation.map.DisplayWidth / (float)GridCellSize),
            (int)Math.Ceiling(Game1.game1.instanceGameLocation.map.DisplayHeight / (float)GridCellSize)
        );
        
        Generating = false;
    }
    private void AddTileToGrid(WorldTile tile)
    {
        BoundingBox box = tile.GetBoundingBox();
        Point minCell = GetCellCoordinate(box.Min);
        Point maxCell = GetCellCoordinate(box.Max);

        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                Point cell = new Point(x, y);
                if (!spatialGrid.ContainsKey(cell))
                {
                    spatialGrid[cell] = new List<WorldTile>();
                }
                spatialGrid[cell].Add(tile);
            }
        }
    }

    private Point GetCellCoordinate(Vector3 position)
    {
        int cellX = (int)(position.X / GridCellSize);
        int cellY = (int)(position.Z / GridCellSize);
        return new Point(cellX, cellY);
    }

    public Dictionary<Point, List<WorldTile>> GetSpatialGrid()
    {
        return spatialGrid;
    }

    public Point GetGridSize()
    {
        return gridSize;
    }
}