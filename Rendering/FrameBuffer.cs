using ILGPU;
using ILGPU.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley3D.StardewInterfaces;

namespace StardewValley3D.Rendering;

class Framebuffer
{
    public int Width { get; }
    public int Height { get; }
    public Color[] ColorBuffer { get; set; }
    public Texture2D Texture { get; }

    public Framebuffer(GraphicsDevice graphicsDevice, int width, int height)
    {
        Width = width;
        Height = height;
        ColorBuffer = new Color[width * height];
        Texture = new Texture2D(graphicsDevice, width, height);
    }

    public void Clear(Color color)
    {
        for (int i = 0; i < ColorBuffer.Length; i++)
        {
            ColorBuffer[i] = color;
        }
        Texture.SetData(ColorBuffer);
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;

        int index = y * Width + x;
        ColorBuffer[index] = color;
    }

    public void UpdateTexture()
    {
        Texture.SetData(ColorBuffer);
    }
}