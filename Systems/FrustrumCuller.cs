using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley3D.Rendering;
using StardewValley3D.Structures;

namespace StardewValley3D.Systems
{
    public class FrustumCuller
    {
        private BoundingFrustum _frustum;
        private Camera3D _camera;
        private float _nearPlane = 0.1f;
        private float _farPlane = 1000f;
        
        // Performance metrics
        public int TotalObjects { get; private set; }
        public int VisibleObjects { get; private set; }
        public int CulledObjects { get; private set; }
        public float CullRatio => TotalObjects > 0 ? CulledObjects / (float)TotalObjects : 0;
        
        // Spatial grid for faster culling
        private Dictionary<Point, List<WorldObject>> _spatialGrid = new();
        private int _gridCellSize = 256; // 4x4 tiles per cell
        
        public FrustumCuller()
        {
        }
        
        public void UpdateFrustum(Camera3D camera)
        {
            _camera = camera;
            _frustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);
        }
        
        public void BuildSpatialGrid(Dictionary<(int x, int y, int z), WorldObject> worldObjects)
        {
            _spatialGrid.Clear();
            
            foreach (var kvp in worldObjects)
            {
                var obj = kvp.Value;
                var gridPos = new Point(
                    (int)(obj.Position.X / _gridCellSize),
                    (int)(obj.Position.Z / _gridCellSize)
                );
                
                if (!_spatialGrid.ContainsKey(gridPos))
                    _spatialGrid[gridPos] = new List<WorldObject>();
                    
                _spatialGrid[gridPos].Add(obj);
            }
        }
        
        public List<WorldObject> GetVisibleObjects(Dictionary<(int x, int y, int z), WorldObject> worldObjects)
        {
            var visible = new List<WorldObject>();
            TotalObjects = worldObjects.Count;
            
            // First pass: cull entire grid cells
            var visibleCells = GetVisibleGridCells();
            
            // Second pass: check individual objects in visible cells
            foreach (var cell in visibleCells)
            {
                if (_spatialGrid.TryGetValue(cell, out var objects))
                {
                    foreach (var obj in objects)
                    {
                        if (IsObjectVisible(obj))
                        {
                            visible.Add(obj);
                        }
                    }
                }
            }
            
            // Add any objects not in grid (dynamic objects)
            foreach (var kvp in worldObjects)
            {
                var obj = kvp.Value;
                if (obj.ObjectType == ObjectType.Sprite && IsObjectVisible(obj))
                {
                    visible.Add(obj);
                }
            }
            
            VisibleObjects = visible.Count;
            CulledObjects = TotalObjects - VisibleObjects;
            
            return visible;
        }
        
        private List<Point> GetVisibleGridCells()
        {
            var visibleCells = new List<Point>();
            
            // Get frustum corners in world space
            var corners = _frustum.GetCorners();
            
            // Find min/max grid coordinates
            int minX = int.MaxValue, maxX = int.MinValue;
            int minZ = int.MaxValue, maxZ = int.MinValue;
            
            foreach (var corner in corners)
            {
                int gridX = (int)(corner.X / _gridCellSize);
                int gridZ = (int)(corner.Z / _gridCellSize);
                
                minX = Math.Min(minX, gridX);
                maxX = Math.Max(maxX, gridX);
                minZ = Math.Min(minZ, gridZ);
                maxZ = Math.Max(maxZ, gridZ);
            }
            
            // Check each cell in range
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                for (int z = minZ - 1; z <= maxZ + 1; z++)
                {
                    var cellBounds = new BoundingBox(
                        new Vector3(x * _gridCellSize, -100, z * _gridCellSize),
                        new Vector3((x + 1) * _gridCellSize, 500, (z + 1) * _gridCellSize)
                    );
                    
                    if (_frustum.Intersects(cellBounds))
                    {
                        visibleCells.Add(new Point(x, z));
                    }
                }
            }
            
            return visibleCells;
        }
        
        private bool IsObjectVisible(WorldObject obj)
        {
            // Quick distance cull
            float distanceSquared = Vector3.DistanceSquared(obj.Position, _camera.Position);
            if (distanceSquared > _farPlane * _farPlane)
                return false;
                
            // Expand bounds slightly to prevent edge popping
            var expandedBounds = obj.Bounds;
            float expansion = obj.ObjectType == ObjectType.Building ? 64 : 32;
            expandedBounds.Min -= Vector3.One * expansion;
            expandedBounds.Max += Vector3.One * expansion;
            
            // Frustum test
            var result = _frustum.Contains(expandedBounds);
            return result != ContainmentType.Disjoint;
        }
        
        public CullResult CullWithLOD(Dictionary<(int x, int y, int z), WorldObject> worldObjects)
        {
            var result = new CullResult();
            TotalObjects = worldObjects.Count;
            
            foreach (var kvp in worldObjects)
            {
                var obj = kvp.Value;
                float distance = Vector3.Distance(obj.Position, _camera.Position);
                
                // Distance culling
                if (distance > _farPlane)
                    continue;
                
                // Frustum culling
                if (!IsObjectVisible(obj))
                    continue;
                
                // Determine LOD
                LODLevel lod = LODLevel.High;
                if (distance > 600)
                    lod = LODLevel.Billboard;
                else if (distance > 400)
                    lod = LODLevel.Low;
                else if (distance > 200)
                    lod = LODLevel.Medium;
                
                result.AddObject(obj, lod, distance);
            }
            
            VisibleObjects = result.GetTotalVisible();
            CulledObjects = TotalObjects - VisibleObjects;
            
            return result;
        }
        
        public class CullResult
        {
            public List<(WorldObject obj, LODLevel lod, float distance)> HighLOD = new();
            public List<(WorldObject obj, LODLevel lod, float distance)> MediumLOD = new();
            public List<(WorldObject obj, LODLevel lod, float distance)> LowLOD = new();
            public List<(WorldObject obj, LODLevel lod, float distance)> BillboardLOD = new();
            
            public void AddObject(WorldObject obj, LODLevel lod, float distance)
            {
                var entry = (obj, lod, distance);
                
                switch (lod)
                {
                    case LODLevel.High:
                        HighLOD.Add(entry);
                        break;
                    case LODLevel.Medium:
                        MediumLOD.Add(entry);
                        break;
                    case LODLevel.Low:
                        LowLOD.Add(entry);
                        break;
                    case LODLevel.Billboard:
                        BillboardLOD.Add(entry);
                        break;
                }
            }
            
            public int GetTotalVisible() => 
                HighLOD.Count + MediumLOD.Count + LowLOD.Count + BillboardLOD.Count;
            
            public void SortByDistance()
            {
                // Sort each LOD group by distance for optimal rendering order
                HighLOD = HighLOD.OrderBy(x => x.distance).ToList();
                MediumLOD = MediumLOD.OrderBy(x => x.distance).ToList();
                LowLOD = LowLOD.OrderBy(x => x.distance).ToList();
                BillboardLOD = BillboardLOD.OrderByDescending(x => x.distance).ToList(); // Back to front for transparency
            }
        }
        
        public enum LODLevel
        {
            High = 0,
            Medium = 1,
            Low = 2,
            Billboard = 3
        }
    }
}