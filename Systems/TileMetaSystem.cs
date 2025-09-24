using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley3D.Structures;

namespace StardewValley3D.Systems
{
    [Serializable]
    public class BoxComposition
    {
        [JsonPropertyName("boxes")]
        public List<CompositionBox> Boxes { get; set; } = new();
        
        [JsonPropertyName("sourceSprite")]
        public string SourceSprite { get; set; }
        
        [JsonPropertyName("useOriginalColors")]
        public bool UseOriginalColors { get; set; } = true;
    }

    [Serializable]
    public class CompositionBox
    {
        [JsonPropertyName("position")]
        public Vector3 Position { get; set; }
        
        [JsonPropertyName("size")]
        public Vector3 Size { get; set; }
        
        [JsonPropertyName("faceUVs")]
        public Dictionary<BoxFace, UVMapping> FaceUVs { get; set; } = new();
        
        [JsonPropertyName("tint")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Color? Tint { get; set; }
        
        [JsonPropertyName("useSprite")]
        public bool UseSprite { get; set; } = true;
    }

    [Serializable]
    public class UVMapping
    {
        [JsonPropertyName("sourceRect")]
        public Rectangle SourceRect { get; set; }
        
        [JsonPropertyName("flipHorizontal")]
        public bool FlipHorizontal { get; set; }
        
        [JsonPropertyName("flipVertical")]
        public bool FlipVertical { get; set; }
        
        [JsonPropertyName("stretch")]
        public bool Stretch { get; set; } = true;
    }

    public enum BoxFace
    {
        Front, Back, Left, Right, Top, Bottom
    }
    
    public enum TileMeshType
    {
        Box,
        Plane,
        Billboard,
        SplitBox,
        CrossedBillboard,
        Custom
    }
    
    public enum TileStackingMode
    {
        None,
        Vertical,
        Tree,
        Building
    }
    
    [Serializable]
    public class TileMetadata
    {
        public string TileId { get; set; }
        public string TileSheet { get; set; }
        public int TileIndex { get; set; }
        public string Layer { get; set; }
        
        // Texture information for proper loading
        public string TexturePath { get; set; }  // Full path to texture
        public Rectangle? SourceRectangle { get; set; }  // Actual source rect from sprite sheet
        public string TextureType { get; set; }  // "tile", "object", "furniture", "character", etc.
        
        // Rendering properties
        public TileMeshType MeshType { get; set; } = TileMeshType.Box;
        public float HeightOffset { get; set; } = 0f;
        public float Scale { get; set; } = 1f;
        public bool IsTransparent { get; set; } = false;
        public bool CastsShadows { get; set; } = true;
        public bool ReceivesShadows { get; set; } = true;
        public float SplitRatio { get; set; } = 0.3f; // For SplitBox mesh type
        
        // Stacking properties
        public TileStackingMode StackingMode { get; set; } = TileStackingMode.None;
        public List<int> StackedTileIndices { get; set; } = new();
        public float StackHeight { get; set; } = 1f;
        public int MaxStackLevels { get; set; } = 1;
        
        // Material properties
        public string MaterialType { get; set; } = "default";
        public float Roughness { get; set; } = 0.5f;
        public float Metallic { get; set; } = 0f;
        public Color? TintColor { get; set; }
        
        // Behavior flags
        public bool IsWall { get; set; } = false;
        public bool IsFloor { get; set; } = false;
        public bool IsDecoration { get; set; } = false;
        public bool AlwaysFront { get; set; } = false;
        public bool Passable { get; set; } = true;
        
        // Custom properties
        public Dictionary<string, object> CustomProperties { get; set; } = new();
        
        // Editor metadata
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string Notes { get; set; } = "";
        
        public string GetUniqueId() => $"{TileSheet}_{TileIndex}_{Layer}";
        
        [JsonPropertyName("boxComposition")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public BoxComposition BoxComposition { get; set; } = new BoxComposition();
        
        /// <summary>
        /// Set texture info based on object type
        /// </summary>
        public void SetTextureInfo(string texturePath, Rectangle sourceRect, string textureType)
        {
            TexturePath = texturePath;
            SourceRectangle = sourceRect;
            TextureType = textureType;
        }
    }
    
    public class TileMetadataManager
    {
        private readonly MonitorHelper _helper;
        private readonly string _metadataDirectory;
        private Dictionary<string, TileMetadata> _metadata = new();
        private Dictionary<string, List<TileMetadata>> _metadataByLocation = new();
        private bool _isDirty = false;
        private JsonSerializerOptions _jsonOptions;
        
        public event EventHandler<TileMetadata> MetadataChanged;
        
        public TileMetadataManager(MonitorHelper helper)
        {
            _helper = helper;
            _metadataDirectory = Path.Combine(helper.DirectoryPath, "TileMetadata");
            Directory.CreateDirectory(_metadataDirectory);
            
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { 
                    new JsonStringEnumConverter(),
                    new Vector3Converter(),
                    new RectangleConverter(),
                    new ColorConverter(),
                    new NullableColorConverter(),
                    new BoxFaceUVMappingDictionaryConverter()
                },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Don't write null values
            };
            
            LoadAllMetadata();
        }
        
        /// <summary>
        /// Get or create metadata for a tile
        /// </summary>
        public TileMetadata GetOrCreateMetadata(string tileId, string tileSheet, int tileIndex, string layer)
        {
            var key = $"{tileSheet}_{tileIndex}_{layer}";
            
            if (_metadata.TryGetValue(key, out var existing))
                return existing;
            
            // Create new metadata with smart defaults
            var metadata = new TileMetadata
            {
                TileId = tileId,
                TileSheet = tileSheet,
                TileIndex = tileIndex,
                Layer = layer
            };
            
            // Apply smart defaults based on tile properties
            ApplySmartDefaults(metadata);
            
            _metadata[key] = metadata;
            _isDirty = true;
            
            return metadata;
        }
        
        /// <summary>
        /// Update metadata for a tile
        /// </summary>
        public void UpdateMetadata(TileMetadata metadata)
        {
            var key = metadata.GetUniqueId();
            _metadata[key] = metadata;
            metadata.LastModified = DateTime.Now;
            _isDirty = true;
            
            MetadataChanged?.Invoke(this, metadata);
            
            // Auto-save after changes
            SaveIfDirty();
        }
        
        /// <summary>
        /// Apply smart defaults based on tile properties
        /// </summary>
        private void ApplySmartDefaults(TileMetadata metadata)
        {
            // Layer-based defaults
            if (metadata.Layer.Contains("Building") || metadata.Layer.Contains("Front"))
            {
                metadata.MeshType = TileMeshType.SplitBox;
                metadata.IsWall = true;
                metadata.Passable = false;
                metadata.HeightOffset = 1f;
            }
            else if (metadata.Layer.Contains("AlwaysFront"))
            {
                metadata.AlwaysFront = true;
                metadata.HeightOffset = 2f;
            }
            else if (metadata.Layer.Contains("Back"))
            {
                metadata.IsFloor = true;
                metadata.MeshType = TileMeshType.Plane;
            }
            
            // Tile ID based defaults
            var tileLower = metadata.TileId.ToLower();
            
            if (tileLower.Contains("tree"))
            {
                metadata.MeshType = TileMeshType.Billboard;
                metadata.StackingMode = TileStackingMode.Tree;
                metadata.MaterialType = "foliage";
                metadata.CastsShadows = true;
            }
            else if (tileLower.Contains("grass"))
            {
                metadata.MeshType = TileMeshType.CrossedBillboard;
                metadata.MaterialType = "foliage";
                metadata.Passable = true;
            }
            else if (tileLower.Contains("fence"))
            {
                metadata.MeshType = TileMeshType.Box;
                metadata.MaterialType = "wood";
                metadata.Passable = false;
            }
            else if (tileLower.Contains("water"))
            {
                metadata.MaterialType = "water";
                metadata.IsTransparent = true;
                metadata.CastsShadows = false;
                metadata.HeightOffset = -0.1f;
            }
            else if (tileLower.Contains("floor") || tileLower.Contains("path"))
            {
                metadata.MeshType = TileMeshType.Plane;
                metadata.IsFloor = true;
                metadata.HeightOffset = 0.01f;
            }
            else if (tileLower.Contains("wall"))
            {
                metadata.IsWall = true;
                metadata.Passable = false;
                metadata.HeightOffset = 1f;
            }
            
            // Special tile indices (common patterns)
            if (metadata.TileIndex >= 160 && metadata.TileIndex <= 170)
            {
                // Common wall tiles
                metadata.IsWall = true;
            }
        }
        
        /// <summary>
        /// Load all metadata from disk
        /// </summary>
        private void LoadAllMetadata()
        {
            _metadata.Clear();
            _metadataByLocation.Clear();
            
            // Load global metadata
            var globalFile = Path.Combine(_metadataDirectory, "global.json");
            if (File.Exists(globalFile))
            {
                LoadMetadataFile(globalFile);
            }
            
            // Load per-location metadata
            foreach (var file in Directory.GetFiles(_metadataDirectory, "*.json"))
            {
                if (Path.GetFileName(file) != "global.json")
                {
                    LoadMetadataFile(file);
                }
            }
            
            _helper.Monitor.Log($"Loaded {_metadata.Count} tile metadata entries", LogLevel.Debug);
        }
        
        private void LoadMetadataFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var metadataList = JsonSerializer.Deserialize<List<TileMetadata>>(json, _jsonOptions);
                
                if (metadataList != null)
                {
                    var locationName = Path.GetFileNameWithoutExtension(filePath);
                    
                    foreach (var metadata in metadataList)
                    {
                        var key = metadata.GetUniqueId();
                        _metadata[key] = metadata;
                        
                        if (locationName != "global")
                        {
                            if (!_metadataByLocation.ContainsKey(locationName))
                                _metadataByLocation[locationName] = new List<TileMetadata>();
                            _metadataByLocation[locationName].Add(metadata);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _helper.Monitor.Log($"Error loading metadata from {filePath}: {e.Message}", LogLevel.Error);
            }
        }
        
        /// <summary>
        /// Save metadata if dirty
        /// </summary>
        public void SaveIfDirty()
        {
            if (!_isDirty) return;
            
            SaveAllMetadata();
            _isDirty = false;
        }
        
        /// <summary>
        /// Save all metadata to disk
        /// </summary>
        public void SaveAllMetadata()
        {
            // Group metadata by location
            var globalMetadata = new List<TileMetadata>();
            var locationMetadata = new Dictionary<string, List<TileMetadata>>();
            
            foreach (var kvp in _metadata)
            {
                var metadata = kvp.Value;
                
                // For now, save everything to global
                // Later we can separate by location if needed
                globalMetadata.Add(metadata);
            }
            
            // Save global metadata
            var globalFile = Path.Combine(_metadataDirectory, "global.json");
            SaveMetadataFile(globalFile, globalMetadata);
            
            // Save location-specific metadata
            foreach (var kvp in locationMetadata)
            {
                var locationFile = Path.Combine(_metadataDirectory, $"{kvp.Key}.json");
                SaveMetadataFile(locationFile, kvp.Value);
            }
            
            _helper.Monitor.Log($"Saved {_metadata.Count} tile metadata entries", LogLevel.Debug);
        }
        
        private void SaveMetadataFile(string filePath, List<TileMetadata> metadataList)
        {
            try
            {
                var json = JsonSerializer.Serialize(metadataList, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch (Exception e)
            {
                _helper.Monitor.Log($"Error saving metadata to {filePath}: {e.Message}", LogLevel.Error);
            }
        }
        
        /// <summary>
        /// Get metadata for rendering
        /// </summary>
        public Dictionary<string, TileMetadata> GetAllMetadata() => new(_metadata);
        
        /// <summary>
        /// Export metadata for a specific location
        /// </summary>
        public void ExportLocationMetadata(string locationName)
        {
            var locationFile = Path.Combine(_metadataDirectory, $"{locationName}.json");
            
            var locationMeta = _metadata.Values
                .Where(m => m.TileId.Contains(locationName))
                .ToList();
            
            if (locationMeta.Any())
            {
                SaveMetadataFile(locationFile, locationMeta);
                _helper.Monitor.Log($"Exported {locationMeta.Count} metadata entries for {locationName}", LogLevel.Info);
            }
        }
        
        /// <summary>
        /// Import metadata from a file
        /// </summary>
        public void ImportMetadata(string filePath)
        {
            if (File.Exists(filePath))
            {
                LoadMetadataFile(filePath);
                _isDirty = true;
                SaveIfDirty();
            }
        }
        
        /// <summary>
        /// Clear all metadata (use with caution)
        /// </summary>
        public void ClearMetadata()
        {
            _metadata.Clear();
            _metadataByLocation.Clear();
            _isDirty = true;
        }
    }
    
        public class Vector3Converter : JsonConverter<Vector3>
    {
        public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
            
            float x = 0, y = 0, z = 0;
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Vector3(x, y, z);
                
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();
                    
                    switch (propertyName?.ToLower())
                    {
                        case "x":
                            x = reader.GetSingle();
                            break;
                        case "y":
                            y = reader.GetSingle();
                            break;
                        case "z":
                            z = reader.GetSingle();
                            break;
                    }
                }
            }
            
            throw new JsonException();
        }
        
        public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("z", value.Z);
            writer.WriteEndObject();
        }
    }
    
    public class RectangleConverter : JsonConverter<Rectangle>
    {
        public override Rectangle Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
            
            int x = 0, y = 0, width = 0, height = 0;
            
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return new Rectangle(x, y, width, height);
                
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string propertyName = reader.GetString();
                    reader.Read();
                    
                    switch (propertyName?.ToLower())
                    {
                        case "x":
                            x = reader.GetInt32();
                            break;
                        case "y":
                            y = reader.GetInt32();
                            break;
                        case "width":
                            width = reader.GetInt32();
                            break;
                        case "height":
                            height = reader.GetInt32();
                            break;
                    }
                }
            }
            
            throw new JsonException();
        }
        
        public override void Write(Utf8JsonWriter writer, Rectangle value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", value.X);
            writer.WriteNumber("y", value.Y);
            writer.WriteNumber("width", value.Width);
            writer.WriteNumber("height", value.Height);
            writer.WriteEndObject();
        }
    }
    
    public class ColorConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                // Support hex color strings
                string hex = reader.GetString();
                if (hex.StartsWith("#"))
                    hex = hex.Substring(1);
                
                if (hex.Length == 6)
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    return new Color(r, g, b);
                }
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                int r = 255, g = 255, b = 255, a = 255;
                
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        return new Color(r, g, b, a);
                    
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string propertyName = reader.GetString();
                        reader.Read();
                        
                        switch (propertyName?.ToLower())
                        {
                            case "r":
                                r = reader.GetInt32();
                                break;
                            case "g":
                                g = reader.GetInt32();
                                break;
                            case "b":
                                b = reader.GetInt32();
                                break;
                            case "a":
                                a = reader.GetInt32();
                                break;
                        }
                    }
                }
            }
            
            throw new JsonException();
        }
        
        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("r", value.R);
            writer.WriteNumber("g", value.G);
            writer.WriteNumber("b", value.B);
            writer.WriteNumber("a", value.A);
            writer.WriteEndObject();
        }
    }
    
    // Nullable versions
    public class NullableColorConverter : JsonConverter<Color?>
    {
        public override Color? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            
            var converter = new ColorConverter();
            return converter.Read(ref reader, typeof(Color), options);
        }
        
        public override void Write(Utf8JsonWriter writer, Color? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
            }
            else
            {
                var converter = new ColorConverter();
                converter.Write(writer, value.Value, options);
            }
        }
    }
    
    public class BoxFaceUVMappingDictionaryConverter : JsonConverter<Dictionary<BoxFace, UVMapping>>
    {
        public override Dictionary<BoxFace, UVMapping> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
        
            var dictionary = new Dictionary<BoxFace, UVMapping>();
        
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    return dictionary;
            
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string enumName = reader.GetString();
                    if (!Enum.TryParse<BoxFace>(enumName, out BoxFace face))
                    {
                        reader.Skip();
                        continue;
                    }
                
                    reader.Read();
                
                    // Read the UVMapping object
                    var uvMapping = JsonSerializer.Deserialize<UVMapping>(ref reader, options);
                    if (uvMapping != null)
                    {
                        dictionary[face] = uvMapping;
                    }
                }
            }
        
            throw new JsonException();
        }
    
        public override void Write(Utf8JsonWriter writer, Dictionary<BoxFace, UVMapping> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
        
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key.ToString());
                JsonSerializer.Serialize(writer, kvp.Value, options);
            }
        
            writer.WriteEndObject();
        }
    }
}