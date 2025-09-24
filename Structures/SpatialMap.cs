using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley3D.Rendering;
using StardewValley3D.Structures;

namespace StardewValley3D.Systems
{
    /// <summary>
    /// Simple spatial map for fast lookups and updates
    /// Replaces the BVH system with something more suitable for tile-based worlds
    /// </summary>
    public class SpatialMap
    {
        private Dictionary<Point3D, List<MeshObject>> _spatialGrid = new();
        private Dictionary<string, MeshObject> _objectLookup = new();
        private int _cellSize;
        private MonitorHelper _helper;
        
        public struct Point3D : IEquatable<Point3D>
        {
            public int X, Y, Z;
            
            public Point3D(int x, int y, int z)
            {
                X = x; Y = y; Z = z;
            }
            
            public bool Equals(Point3D other) => X == other.X && Y == other.Y && Z == other.Z;
            public override int GetHashCode() => HashCode.Combine(X, Y, Z);
            public override string ToString() => $"({X}, {Y}, {Z})";
        }
        
        public SpatialMap(MonitorHelper helper, int cellSize = 64)
        {
            _helper = helper;
            _cellSize = cellSize;
        }
        
        /// <summary>
        /// Add or update an object in the spatial map
        /// </summary>
        public void AddObject(MeshObject obj)
        {
            // Remove old position if updating
            if (_objectLookup.ContainsKey(obj.Id))
            {
                RemoveObject(obj.Id);
            }
    
            // Calculate grid cells this object occupies
            var cells = GetOccupiedCells(obj);
    
            // Track if this is the first cell for this object
            bool isFirstCell = true;
    
            foreach (var cell in cells)
            {
                if (!_spatialGrid.ContainsKey(cell))
                    _spatialGrid[cell] = new List<MeshObject>();
        
                // Check if object is already in this cell (shouldn't happen after RemoveObject above)
                if (!_spatialGrid[cell].Contains(obj))
                {
                    _spatialGrid[cell].Add(obj);
                }
                else
                {
                    _helper.Monitor.Log($"Warning: Object {obj.Id} already in cell {cell}");
                }
            }
    
            _objectLookup[obj.Id] = obj;
        }
        
        /// <summary>
        /// Remove an object from the spatial map
        /// </summary>
        public void RemoveObject(string objectId)
        {
            if (!_objectLookup.TryGetValue(objectId, out var obj))
                return;
            
            var cells = GetOccupiedCells(obj);
            
            foreach (var cell in cells)
            {
                if (_spatialGrid.TryGetValue(cell, out var list))
                {
                    list.Remove(obj);
                    if (list.Count == 0)
                        _spatialGrid.Remove(cell);
                }
            }
            
            _objectLookup.Remove(objectId);
        }
        
        /// <summary>
        /// Update an object's position
        /// </summary>
        public void UpdateObject(string objectId, Vector3 newPosition)
        {
            if (_objectLookup.TryGetValue(objectId, out var obj))
            {
                var worldObject = obj.WorldObject;
                worldObject.Position = newPosition;
                obj.WorldObject = worldObject;
                obj.UpdateBounds();
                
                // Re-add to update spatial position
                AddObject(obj);
            }
        }
        
        /// <summary>
        /// Get all objects in a region
        /// </summary>
        public List<MeshObject> GetObjectsInRegion(BoundingBox region)
        {
            var objects = new HashSet<MeshObject>(); // Use HashSet to prevent duplicates
            var cells = GetCellsInRegion(region);
    
            foreach (var cell in cells)
            {
                if (_spatialGrid.TryGetValue(cell, out var list))
                {
                    foreach (var obj in list)
                    {
                        if (obj.Bounds.Intersects(region))
                            objects.Add(obj); // HashSet prevents duplicates
                    }
                }
            }
    
            return objects.ToList();
        }
        
        /// <summary>
        /// Get objects visible from camera (frustum culling)
        /// </summary>
        public List<MeshObject> GetVisibleObjects(BoundingFrustum frustum)
        {
            var meshes = new List<MeshObject>();
            foreach (var kvp in _spatialGrid)
            {
                meshes.AddRange(kvp.Value);
            }
                
            //return meshes;
            var visible = new List<MeshObject>();
            var checkedCells = new HashSet<Point3D>();
            
            // Get frustum corners to determine region
            var corners = frustum.GetCorners();
            var minPoint = corners[0];
            var maxPoint = corners[0];
            
            foreach (var corner in corners)
            {
                minPoint = Vector3.Min(minPoint, corner);
                maxPoint = Vector3.Max(maxPoint, corner);
            }
            
            var region = new BoundingBox(minPoint, maxPoint);
            var cells = GetCellsInRegion(region);
            
            foreach (var cell in cells)
            {
                if (!_spatialGrid.TryGetValue(cell, out var list))
                    continue;
                
                foreach (var obj in list)
                {
                    // Quick frustum check
                    var result = frustum.Contains(obj.Bounds);
                    if (result != ContainmentType.Disjoint)
                    {
                        visible.Add(obj);
                    }
                }
            }
            
            return visible;
        }
        
        /// <summary>
        /// Get object at specific tile position
        /// </summary>
        public MeshObject GetObjectAt(int tileX, int tileY, int layer = 0)
        {
            var worldPos = new Vector3(tileX * 64, layer * 64, tileY * 64);
            var cell = WorldToCell(worldPos);
            
            if (_spatialGrid.TryGetValue(cell, out var list))
            {
                return list.FirstOrDefault(obj => 
                    Math.Abs(obj.WorldObject.Position.X - worldPos.X) < 32 &&
                    Math.Abs(obj.WorldObject.Position.Z - worldPos.Z) < 32);
            }
            
            return null;
        }
        
        /// <summary>
        /// Clear all objects
        /// </summary>
        public void Clear()
        {
            _spatialGrid.Clear();
            _objectLookup.Clear();
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public (int objectCount, int cellCount, float avgObjectsPerCell) GetStats()
        {
            int objectCount = _objectLookup.Count;
            int cellCount = _spatialGrid.Count;
            float avgObjectsPerCell = cellCount > 0 ? 
                _spatialGrid.Values.Sum(l => l.Count) / (float)cellCount : 0;
            
            return (objectCount, cellCount, avgObjectsPerCell);
        }
        
        private Point3D WorldToCell(Vector3 position)
        {
            return new Point3D(
                (int)Math.Floor(position.X / _cellSize),
                (int)Math.Floor(position.Y / _cellSize),
                (int)Math.Floor(position.Z / _cellSize)
            );
        }
        
        private List<Point3D> GetOccupiedCells(MeshObject obj)
        {
            var cells = new List<Point3D>();
            
            var minCell = WorldToCell(obj.Bounds.Min);
            var maxCell = WorldToCell(obj.Bounds.Max);
            
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        cells.Add(new Point3D(x, y, z));
                    }
                }
            }
            
            return cells;
        }
        
        private List<Point3D> GetCellsInRegion(BoundingBox region)
        {
            var cells = new List<Point3D>();
            
            var minCell = WorldToCell(region.Min);
            var maxCell = WorldToCell(region.Max);
            
            // Expand by one cell to catch edge cases
            minCell = new Point3D(minCell.X - 1, minCell.Y - 1, minCell.Z - 1);
            maxCell = new Point3D(maxCell.X + 1, maxCell.Y + 1, maxCell.Z + 1);
            
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        cells.Add(new Point3D(x, y, z));
                    }
                }
            }
            
            return cells;
        }
    }
    
    /// <summary>
    /// Mesh object that references its mesh and can be updated
    /// </summary>
    public class MeshObject
    {
        public string Id;
        public WorldObject WorldObject;
        public Mesh? Mesh;
        public BoundingBox Bounds { get; private set; }
        public bool IsDirty = true;
        public TileMetadata Metadata { get; set; }
        
        public MeshObject(string id, WorldObject worldObject, Mesh? mesh = null)
        {
            Id = id;
            WorldObject = worldObject;
            Mesh = mesh;
            UpdateBounds();
        }
        
        public void UpdateBounds()
        {
            var halfSize = WorldObject.Size * 0.5f;
            Bounds = new BoundingBox(
                WorldObject.Position - halfSize,
                WorldObject.Position + halfSize
            );
        }
        
        public void UpdateMesh(Mesh newMesh)
        {
            Mesh?.Dispose();
            Mesh = newMesh;
            IsDirty = false;
        }
    }
}