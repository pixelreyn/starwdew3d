using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace StardewValley3D.Systems
{
    public struct Material
    {
        public float Roughness;
        public float Metallic;
        public bool IsTransparent;
        public bool ReceivesShadows;
        public bool CastsShadows;
        public float EmissionStrength;
        public Color EmissionColor;

        public static Material Default => new Material
        {
            Roughness = 0.5f,
            Metallic = 0.0f,
            IsTransparent = false,
            ReceivesShadows = true,
            CastsShadows = true,
            EmissionStrength = 0,
            EmissionColor = Color.Black
        };

        public static Material Water => new Material
        {
            Roughness = 0.1f,
            Metallic = 0.0f,
            IsTransparent = true,
            ReceivesShadows = false,
            CastsShadows = false,
            EmissionStrength = 0,
            EmissionColor = Color.Black
        };

        public static Material Metal => new Material
        {
            Roughness = 0.2f,
            Metallic = 0.9f,
            IsTransparent = false,
            ReceivesShadows = true,
            CastsShadows = true,
            EmissionStrength = 0,
            EmissionColor = Color.Black
        };

        public static Material Foliage => new Material
        {
            Roughness = 0.8f,
            Metallic = 0.0f,
            IsTransparent = false,
            ReceivesShadows = true,
            CastsShadows = true,
            EmissionStrength = 0,
            EmissionColor = Color.Black
        };

        public static Material Emissive => new Material
        {
            Roughness = 0.5f,
            Metallic = 0.0f,
            IsTransparent = false,
            ReceivesShadows = false,
            CastsShadows = false,
            EmissionStrength = 1.0f,
            EmissionColor = Color.LightYellow
        };
    }

    public class MaterialSystem
    {
        private Dictionary<string, Material> _materials = new();

        public MaterialSystem()
        {
            InitializeDefaultMaterials();
        }

        private void InitializeDefaultMaterials()
        {
            _materials["default"] = Material.Default;
            _materials["water"] = Material.Water;
            _materials["metal"] = Material.Metal;
            _materials["wood"] = new Material { Roughness = 0.7f, Metallic = 0.0f };
            _materials["stone"] = new Material { Roughness = 0.9f, Metallic = 0.0f };
            _materials["glass"] = new Material { Roughness = 0.0f, Metallic = 0.0f, IsTransparent = true };
            _materials["foliage"] = Material.Foliage;
            _materials["torch"] = Material.Emissive;
        }

        public Material GetMaterial(string objectName)
        {
            // Match object names to materials
            string nameLower = objectName.ToLower();

            if (nameLower.Contains("water"))
                return _materials["water"];
            if (nameLower.Contains("metal") || nameLower.Contains("iron") || nameLower.Contains("copper"))
                return _materials["metal"];
            if (nameLower.Contains("wood") || nameLower.Contains("fence") || nameLower.Contains("table"))
                return _materials["wood"];
            if (nameLower.Contains("stone") || nameLower.Contains("rock"))
                return _materials["stone"];
            if (nameLower.Contains("glass") || nameLower.Contains("window"))
                return _materials["glass"];
            if (nameLower.Contains("tree") || nameLower.Contains("grass") || nameLower.Contains("crop"))
                return _materials["foliage"];
            if (nameLower.Contains("torch") || nameLower.Contains("lamp") || nameLower.Contains("fire"))
                return _materials["torch"];

            return _materials["default"];
        }
    }
}
