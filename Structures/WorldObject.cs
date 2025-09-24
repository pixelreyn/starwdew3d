using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley3D.Systems;
using System;

namespace StardewValley3D.Rendering
{
    public enum ObjectType
    {
        Object,      // 3D objects like buildings, walls
        Sprite,      // Billboarded sprites like NPCs
        Tile,        // Floor tiles
        Building,    // Large structures
        TerrainFeature, // Trees, grass, crops
        Water,       // Water tiles
        Debris       // Small items
    }
    
    /// <summary>
    /// Updated WorldObject that references metadata instead of storing all properties
    /// </summary>
    public struct WorldObject
    {
        // Core identification
        public string Id;
        public string TileSheet;
        public int TileIndex;
        public string Layer;
        
        // Transform
        public Vector3 Position;
        public Vector3 Size;
        public Quaternion Rotation;
        
        // Rendering data
        public Rectangle SourceRectangle;
        public Color Tint;
        public ObjectType ObjectType;
        
        // Material (cached from metadata)
        public Material Material;
        
        // Texture reference (not owned)
        public int TextureId;
        
        // Bounds (calculated)
        private BoundingBox _bounds;
        public BoundingBox Bounds => _bounds;
        
        // Metadata reference key
        public string MetadataKey => $"{TileSheet}_{TileIndex}_{Layer}";
        
        /// <summary>
        /// Create a world object with metadata reference
        /// </summary>
        public WorldObject(string id, Vector3 position, Vector3 size, string tileSheet, 
            int tileIndex, string layer, Rectangle sourceRect, Color tint, ObjectType type)
        {
            Id = id;
            Position = position;
            Size = size;
            Rotation = Quaternion.Identity;
            TileSheet = tileSheet;
            TileIndex = tileIndex;
            Layer = layer;
            SourceRectangle = sourceRect;
            Tint = tint;
            ObjectType = type;
            Material = Material.Default;
            TextureId = -1;
            
            // Calculate bounds
            var halfSize = size * 0.5f;
            _bounds = new BoundingBox(position - halfSize, position + halfSize);
        }
        
        /// <summary>
        /// Update bounds after position/size change
        /// </summary>
        public void UpdateBounds()
        {
            var halfSize = Size * 0.5f;
            _bounds = new BoundingBox(Position - halfSize, Position + halfSize);
        }
        
        /// <summary>
        /// Apply metadata to this object
        /// </summary>
        public void ApplyMetadata(TileMetadata metadata, MaterialSystem materialSystem)
        {
            // Update position with height offset
            var basePos = Position;
            Position = new Vector3(basePos.X, basePos.Y + metadata.HeightOffset * 64, basePos.Z);
            
            // Update size with scale
            Size = Size * metadata.Scale;
            
            // Apply tint if specified
            if (metadata.TintColor.HasValue)
                Tint = metadata.TintColor.Value;
            
            // Get material from system
            var mat = materialSystem?.GetMaterial(metadata.MaterialType) ?? Material.Default;
            
            // Apply transparency to material
            mat.IsTransparent = metadata.IsTransparent;
            mat.CastsShadows = metadata.CastsShadows;
            mat.ReceivesShadows = metadata.ReceivesShadows;
            mat.Roughness = metadata.Roughness;
            mat.Metallic = metadata.Metallic;
            
            Material = mat;
            // Update object type based on metadata
            if (metadata.IsWall)
                ObjectType = ObjectType.Object;
            else if (metadata.IsFloor)
                ObjectType = ObjectType.Tile;
            else if (metadata.MeshType == TileMeshType.Billboard || 
                     metadata.MeshType == TileMeshType.CrossedBillboard)
                ObjectType = ObjectType.Sprite;
            
            // Recalculate bounds
            UpdateBounds();
        }
        
        /// <summary>
        /// Get a hash for batching similar objects
        /// </summary>
        public int GetBatchingHash()
        {
            return HashCode.Combine(TileSheet, Material.GetHashCode(), ObjectType);
        }
        
        /// <summary>
        /// Check if this object can be instanced with another
        /// </summary>
        public bool CanInstanceWith(WorldObject other)
        {
            return TileSheet == other.TileSheet &&
                   Material.Equals(other.Material) &&
                   ObjectType == other.ObjectType &&
                   Math.Abs(Size.Length() - other.Size.Length()) < 0.01f;
        }
        
        /// <summary>
        /// Get normal at hit point for collision
        /// </summary>
        public Vector3 GetNormal(Vector3 hitPoint)
        {
            Vector3 normal = Vector3.Zero;
            float bias = 0.0001f;
            
            if (Math.Abs(hitPoint.X - _bounds.Min.X) < bias) normal = Vector3.Left;
            else if (Math.Abs(hitPoint.X - _bounds.Max.X) < bias) normal = Vector3.Right;
            else if (Math.Abs(hitPoint.Y - _bounds.Min.Y) < bias) normal = Vector3.Down;
            else if (Math.Abs(hitPoint.Y - _bounds.Max.Y) < bias) normal = Vector3.Up;
            else if (Math.Abs(hitPoint.Z - _bounds.Min.Z) < bias) normal = Vector3.Backward;
            else if (Math.Abs(hitPoint.Z - _bounds.Max.Z) < bias) normal = Vector3.Forward;
            
            return normal;
        }
    }
}