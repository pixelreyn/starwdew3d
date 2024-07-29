using System.Numerics;
using Raylib_cs;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TerrainFeatures;
using xTile.Layers;

namespace StardewValley3D.StardewInterfaces;

public class WorldTile
{
    public Color Color;
    public Vector3 Position;
    public Vector3 Size;
}

public class Map
{
    private List<WorldTile> Tiles = new List<WorldTile>();
    private bool Generating = false;
    public void GenerateMap()
    {
        Generating = true;
        Tiles.Clear();
        foreach (var layer in Game1.game1.instanceGameLocation.backgroundLayers)
        {
            for (var x = 0; x < layer.Key.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Key.Tiles.Array.GetLength(1); y++)
                {

                    Raylib_cs.Color color = new Raylib_cs.Color(36, 36, 36, 255);
                    var tile = layer.Key.Tiles[x, y];
                    if (tile != null && tile.TileIndex != 0)
                    {
                        if (tile.TileIndex == 470 || tile.TileIndex == 271 || tile.TileIndex == 64 ||
                            tile.Properties.Keys.Count(x => x.Contains("Wall")) > 0)
                        {
                            color = new Raylib_cs.Color(36, 0, 0, 255);
                            Tiles.Add(new WorldTile()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f)
                            });
                        }
                        else if (tile.Properties.Count == 0 || tile.Properties.Keys.Count(x => x.Contains("Floor")) > 0)
                            Tiles.Add(new WorldTile()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 64.0f, 64.0f)
                            });

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

                    Raylib_cs.Color color = new Raylib_cs.Color(36, 36, 36, 255);
                    var tile = layer.Key.Tiles[x, y];
                    if (tile != null && tile.TileIndex != 0)
                    {
                        if (tile.TileIndex == 165 || tile.TileIndex == 163 || tile.TileIndex == 162 ||
                            tile.TileIndex == 160 || tile.TileIndex == 167 ||
                            tile.Properties.Keys.Count(a => a.Contains("Wall")) > 0)
                        {
                            color = new Raylib_cs.Color(36, 0, 0, 255);
                            Tiles.Add(new WorldTile()
                            {
                                Color = color,
                                Position = new Vector3(x * 64, 0 * 64, y * 64),
                                Size = new Vector3(64.0f, 128.0f, 64.0f)
                            });
                        }
                    }
                }
            }
        }

        if (Game1.game1.instanceGameLocation.waterTiles != null)
        {
            Raylib_cs.Color waterColor = new Color((int)Game1.game1.instanceGameLocation.waterColor.R,
                Game1.game1.instanceGameLocation.waterColor.G, Game1.game1.instanceGameLocation.waterColor.B, 255);
            for (var x = 0; x < Game1.game1.instanceGameLocation.waterTiles.waterTiles.GetLength(0); x++)
            {
                for (var y = 0; y < Game1.game1.instanceGameLocation.waterTiles.waterTiles.GetLength(1); y++)
                {
                    if (Game1.game1.instanceGameLocation.waterTiles.waterTiles[x, y].isWater)
                    {
                        Tiles.Add(new WorldTile()
                        {
                            Color = waterColor,
                            Position = new Vector3(x * 64, 0 * 64, y * 64),
                            Size = new Vector3(64.0f, 64.0f,64.0f)
                        });
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
                Tiles.Add(new WorldTile()
                {
                    Color = new Color(145, 113, 81, 255),
                    Position = new Vector3((building.tileX.Value + tX) * 64, 1 * 32, (building.tileY.Value + tY) * 64),
                    Size = new Vector3(64.0f, 128.0f, 64.0f)
                });
        }

        foreach (var furniture in Game1.game1.instanceGameLocation.furniture)
        {
            if (furniture == null)
                continue;
            for (int tX = 0; tX < (furniture.boundingBox.Width / 64); tX++)
            for (int tY = 0; tY < (furniture.boundingBox.Height / 64); tY++)
                Tiles.Add(new WorldTile()
                {
                    Color = new Color(92, 46, 0, 255),
                    Position = new Vector3((furniture.TileLocation.X + tX) * 64, 1 * 32, (furniture.TileLocation.Y + tY) * 64),
                    Size = new Vector3(64.0f, 32.0f, 64.0f)
                });
        }

        foreach (var wObject in Game1.game1.instanceGameLocation.objects.Values)
        {
            if (wObject == null)
                continue;
            for (int tX = 0; tX < (wObject.boundingBox.Width / 64); tX++)
            for (int tY = 0; tY < (wObject.boundingBox.Height / 64); tY++)
                Tiles.Add(new WorldTile()
                {
                    Color = new Color(0, 0, 255, 255),
                    Position = new Vector3((wObject.TileLocation.X + tX) * 64, 1 * 32, (wObject.TileLocation.Y + tY) * 64),
                    Size = new Vector3(32.0f, 16.0f, 32.0f)
                });
        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                if (tf is Tree)
                    Tiles.Add(new WorldTile()
                    {
                        Color = new Color(41, 25, 5, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64),
                        Size = new Vector3(64.0f, 128.0f, 64.0f)
                    });
                else if (tf is HoeDirt { crop: null })
                    Tiles.Add(new WorldTile()
                    {
                        Color = new Color(41, 25, 5, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 0, (tf.Tile.Y) * 64),
                        Size = new Vector3(64.0f, 64.0f, 64.0f)
                    });
                else if (tf is HoeDirt { crop: not null })
                    Tiles.Add(new WorldTile()
                    {
                        Color = new Color(0, 255, 0, 255),
                        Position = new Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64),
                        Size = new Vector3(32.0f, 32.0f, 32.0f)
                    });
            }
        }

        Generating = false;
    }

    public void DrawMap()
    {
        if (Tiles == null || Tiles.Count == 0 || Generating)
            return;
        
        foreach (var tile in Tiles)
        {
            Raylib.DrawCube(tile.Position, tile.Size.X, tile.Size.Y, tile.Size.Z, tile.Color);
            
            if (Generating)
                return;
        }
        foreach(var npc in Game1.game1.instanceGameLocation.characters) {
            if (npc == null)
                continue;
            
            Raylib.DrawCube(new System.Numerics.Vector3((npc.Tile.X) * 64, 1 * 32, (npc.Tile.Y) * 64), 16.0f, 92.0f,16.0f, new Raylib_cs.Color(255, 0, 0, 255));
        }
    }
}