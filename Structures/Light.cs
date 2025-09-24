using Microsoft.Xna.Framework;

namespace StardewValley3D.Rendering
{
    public struct Light
    {
        public Vector3 Position;
        public Color Color;
        public float Intensity;
        public int IsDirectional; // New field to distinguish between point and directional lights
        public Vector3 Direction; // Used if the light is directional

        public Light(Vector3 position, Color color, float intensity, int isDirectional = 0, Vector3 direction = default)
        {
            Position = position;
            Color = color;
            Intensity = intensity;
            IsDirectional = isDirectional;
            Direction = Vector3.Normalize(direction); // Ensure the direction is normalized
        }
    }
}