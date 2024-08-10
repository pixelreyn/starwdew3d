using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace StardewValley3D.Rendering;

public class RenderingData
{
    public static Dictionary<string, Color[]> TextureCache = new();
    public static Dictionary<string, int> TextureIdString = new();
    public static Dictionary<int, string> TextureStringId = new();
    public static Dictionary<string, Texture2D> LoadedTextures = new();

    public static int NextTexId;

    public static int AddTextureToCache(string tileId, Texture2D texture, Rectangle sourceRectangle)
    {
        if (tileId.Contains("Sprite"))
        {
            if (TextureIdString.TryGetValue(tileId, out var idx))
            {
                TextureIdString.Remove(tileId);
                TextureStringId.Remove(idx);
                TextureCache.Remove(tileId);
            }

        }
        if (!TextureIdString.TryGetValue(tileId, out var id))
        {
            var texColor = new Color[sourceRectangle.Width * sourceRectangle.Height];
            texture.GetData(0, sourceRectangle, texColor, 0, texColor.Length);
            TextureCache.Add(tileId, texColor);
            id = NextTexId++;
            TextureIdString.Add(tileId, id);
            TextureStringId.Add(id, tileId);
        }

        return id;

    }

    public static void ResetData()
    {
        LoadedTextures.Clear();
        TextureIdString.Clear();
        TextureStringId.Clear();
        TextureCache.Clear();
        NextTexId = 0;
    }
}