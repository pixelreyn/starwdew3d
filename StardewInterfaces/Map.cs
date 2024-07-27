using Raylib_cs;
using StardewValley;
using StardewValley.TerrainFeatures;
using xTile.Layers;

namespace StardewValley3D.StardewInterfaces;

public class Map
{
    public void DrawMap()
    {
        var layerLevel = 0;
        Random rand = new Random();
        foreach (var layer in Game1.game1.instanceGameLocation.Map.Layers)
        {
            Raylib_cs.Color color;
            switch (layer.Id)
            {
                case "Back":
                    layerLevel = 0;
                    color = new Raylib_cs.Color(36, 36, 36, 255);
                    break;
                case "Paths":
                    layerLevel = 0;
                    color = new Raylib_cs.Color(156, 152, 152, 255);
                    break;
                case "Front":
                    layerLevel = 2;
                    color = new Raylib_cs.Color(108, 120, 91, 255);
                    break;
                case "Buildings":
                case "Buildings2":
                    layerLevel = 1;
                    color = new Raylib_cs.Color(66, 66, 66, 255);
                    break;
                case "AlwaysFront":
                    layerLevel = 2;
                    color = new Raylib_cs.Color(0, 66, 0, 255);
                    break;
                default:
                    layerLevel = 2;
                    color = new Raylib_cs.Color(66, 66, 66, 255);
                    break;
            }
            //Parallel.For((int)0, (int)layer.TileWidth, (x) =>
            for (var x = 0; x < layer.Tiles.Array.GetLength(0); x++)
            {
                for (var y = 0; y < layer.Tiles.Array.GetLength(1); y++)
                {
                    var tile = layer.Tiles[x, y];
                    if(tile != null && tile.TileIndex != 0)
                        Raylib.DrawCube(new System.Numerics.Vector3(x * 64, layerLevel * 64, y * 64), 64.0f, 64.0f,64.0f, color);
                }
            }
        }
        
        foreach (var building in Game1.game1.instanceGameLocation.buildings)
        {
            if (building == null)
                continue;
            for(int tX = 0; tX < building.tilesWide.Value; tX++)
                for(int tY = 0; tY < building.tilesHigh.Value; tY++)
                    Raylib.DrawCube(new System.Numerics.Vector3((building.tileX.Value + tX) * 64, 1 * 32, (building.tileY.Value + tY) * 64), 64.0f, 128.0f,64.0f, new Raylib_cs.Color(145, 113, 81, 255));
        }
        
        foreach(var furniture in Game1.game1.instanceGameLocation.furniture)
        {
            if (furniture == null)
                continue;
            for(int tX = 0; tX < (furniture.boundingBox.Width / 64); tX++)
            for(int tY = 0; tY < (furniture.boundingBox.Height / 64); tY++)
                Raylib.DrawCube(new System.Numerics.Vector3((furniture.TileLocation.X + tX) * 64, 1 * 32, (furniture.TileLocation.Y + tY) * 64), 64.0f, 32.0f,64.0f, new Raylib_cs.Color(92, 46, 0, 255));
        }
        
        foreach(var wObject in Game1.game1.instanceGameLocation.objects.Values) {
            if (wObject == null)
                continue;
            for(int tX = 0; tX < (wObject.boundingBox.Width / 64); tX++)
            for(int tY = 0; tY < (wObject.boundingBox.Height / 64); tY++)
                Raylib.DrawCube(new System.Numerics.Vector3((wObject.TileLocation.X + tX) * 64, 1 * 32, (wObject.TileLocation.Y + tY) * 64), 32.0f, 16.0f,32.0f, new Raylib_cs.Color(0,0,255, 255));
        }

        foreach (var tf in Game1.game1.instanceGameLocation.terrainFeatures.Values)
        {
            if (tf != null)
            {
                if (tf is Tree)
                    Raylib.DrawCube(new System.Numerics.Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64), 64.0f,
                        128.0f, 64.0f, new Raylib_cs.Color(41, 25, 5, 255));
                else if (tf is HoeDirt && (tf as HoeDirt).crop == null)
                    Raylib.DrawCube(new System.Numerics.Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64), 64.0f,
                        32.0f, 64.0f, new Raylib_cs.Color(41, 25, 5, 255));
                else if (tf is HoeDirt && (tf as HoeDirt).crop != null)
                    Raylib.DrawCube(new System.Numerics.Vector3((tf.Tile.X) * 64, 1 * 32, (tf.Tile.Y) * 64), 32.0f,
                        32.0f, 32.0f, new Raylib_cs.Color(0, 255, 0, 255));
            }
        }

        foreach(var npc in Game1.game1.instanceGameLocation.characters) {
            if (npc == null)
                continue;
            Raylib.DrawCube(new System.Numerics.Vector3((npc.Tile.X) * 64, 1 * 32, (npc.Tile.Y) * 64), 64.0f, 92.0f,64.0f, new Raylib_cs.Color(255, 0, 0, 255));
        }
    }
}