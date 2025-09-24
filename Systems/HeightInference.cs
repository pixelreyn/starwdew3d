using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley3D.Structures;

namespace StardewValley3D.Systems
{
    /// <summary>
    /// Simplified height system that works with metadata
    /// </summary>
    public class HeightSystem
    {
        private MonitorHelper _helper;
        private TileMetadataManager _metadataManager;
        private Dictionary<string, float> _locationBaseHeights;
        
        public HeightSystem(MonitorHelper helper, TileMetadataManager metadataManager)
        {
            _helper = helper;
            _metadataManager = metadataManager;
            
            // Base heights for different location types
            _locationBaseHeights = new Dictionary<string, float>
            {
                { "Farm", 0f },
                { "Town", 0f },
                { "Mountain", 0.5f },
                { "Beach", -0.2f },
                { "Mine", 0f },
                { "Cave", 0f },
                { "Desert", 0.1f },
                { "Forest", 0f }
            };
        }
        
        /// <summary>
        /// Get height map for a location - prioritizes metadata
        /// </summary>
        public Dictionary<(int, int), float> InferHeights(GameLocation location)
        {
            var heightMap = new Dictionary<(int, int), float>();
            
            // Get base height for this location type
            float baseHeight = GetLocationBaseHeight(location.Name);
            
            // Simple approach: everything starts at base height
            // Metadata will override this during world generation
            for (int x = 0; x < location.Map.DisplayWidth / 64; x++)
            {
                for (int y = 0; y < location.Map.DisplayHeight / 64; y++)
                {
                    heightMap[(x, y)] = baseHeight;
                }
            }
            
            // Apply any saved height overrides from editor
            ApplyHeightOverrides(location, heightMap);
            
            return heightMap;
        }
        
        /// <summary>
        /// Get height for a specific tile - checks metadata first
        /// </summary>
        public float GetHeightForTile(GameLocation location, int x, int y, string layer = "Back")
        {
            // Check if there's metadata for this tile
            var tileId = $"{location.Name}_{x}_{y}_{layer}";
            var metadata = _metadataManager.GetAllMetadata();
            
            if (metadata.TryGetValue($"global_{x}_{y}_{layer}", out var tileMeta))
            {
                return tileMeta.HeightOffset;
            }
            
            // Return base height for location
            return GetLocationBaseHeight(location.Name);
        }
        
        private float GetLocationBaseHeight(string locationName)
        {
            // Check exact match first
            if (_locationBaseHeights.TryGetValue(locationName, out float height))
                return height;
            
            // Check partial matches
            foreach (var kvp in _locationBaseHeights)
            {
                if (locationName.Contains(kvp.Key))
                    return kvp.Value;
            }
            
            // Default
            return 0f;
        }
        
        private void ApplyHeightOverrides(GameLocation location, Dictionary<(int, int), float> heightMap)
        {
            // Load any saved height overrides from the editor
            var overridesFile = System.IO.Path.Combine(
                _helper.DirectoryPath, 
                "HeightMaps", 
                $"{location.Name}.json"
            );
            
            if (System.IO.File.Exists(overridesFile))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(overridesFile);
                    var overrides = System.Text.Json.JsonSerializer.Deserialize<HeightOverrides>(json);
                    
                    if (overrides?.Heights != null)
                    {
                        foreach (var entry in overrides.Heights)
                        {
                            heightMap[(entry.X, entry.Y)] = entry.Height;
                        }
                    }
                }
                catch
                {
                    // Ignore load errors
                }
            }
        }
        
        /// <summary>
        /// Save height overrides from editor
        /// </summary>
        public void SaveHeightOverrides(GameLocation location, Dictionary<(int, int), float> overrides)
        {
            var data = new HeightOverrides
            {
                LocationName = location.Name,
                Heights = new List<HeightEntry>()
            };
            
            foreach (var kvp in overrides)
            {
                data.Heights.Add(new HeightEntry
                {
                    X = kvp.Key.Item1,
                    Y = kvp.Key.Item2,
                    Height = kvp.Value
                });
            }
            
            var directory = System.IO.Path.Combine(_helper.DirectoryPath, "HeightMaps");
            System.IO.Directory.CreateDirectory(directory);
            
            var path = System.IO.Path.Combine(directory, $"{location.Name}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(data, 
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(path, json);
        }
        
        private class HeightOverrides
        {
            public string LocationName { get; set; }
            public List<HeightEntry> Heights { get; set; }
        }
        
        private class HeightEntry
        {
            public int X { get; set; }
            public int Y { get; set; }
            public float Height { get; set; }
        }
    }
}