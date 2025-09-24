using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley3D.Rendering;
using StardewValley3D.StardewInterfaces;
using StardewValley3D.Structures;

namespace StardewValley3D.Systems
{
        /// <summary>
    /// Updated rendering pipeline with incremental instance updates
    /// </summary>
    public class RenderingPipeline : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly MonitorHelper _helper;
        private TextureManager _textureManager;
        
        // Renderers
        private readonly MeshRenderer _meshRenderer;
        private readonly InstancedMeshRenderer _instancedRenderer;
        
        // Render targets
        private RenderTarget2D _mainTarget;
        private RenderTarget2D _depthTarget;
        private SpriteBatch _spriteBatch;
        
        // Effects
        private BasicEffect _basicEffect;
        private Effect _shadowEffect;
        
        // Lighting
        private DirectionalLight _sunLight;
        private List<PointLight> _pointLights = new();
        
        // Stats
        public int ObjectsRendered { get; private set; }
        public int DrawCalls { get; private set; }
        public int BatchCount { get; private set; }
        public int CulledObjects { get; private set; }
        
        // Settings
        private bool _enableShadows = false;
        private bool _enableInstancing = true;
        private float _renderDistance = 1000f;
        
        // Track if instances need full rebuild
        private bool _instancesNeedFullRebuild = true;
        private HashSet<string> _dirtyObjectIds = new();
        
        public RenderingPipeline(GraphicsDevice device, MonitorHelper helper, TextureManager textureManager)
        {
            _device = device;
            _helper = helper;
            _textureManager = textureManager;
            
            // Initialize renderers
            _meshRenderer = new MeshRenderer(device, helper);
            _instancedRenderer = new InstancedMeshRenderer(device, helper, textureManager);
            
            // Setup render targets
            var vp = device.Viewport;
            _mainTarget = new RenderTarget2D(device, vp.Width, vp.Height, 
                false, SurfaceFormat.Color, DepthFormat.Depth24);
            
            if (_enableShadows)
            {
                _depthTarget = new RenderTarget2D(device, 2048, 2048,
                    false, SurfaceFormat.Single, DepthFormat.Depth24);
            }
            
            _spriteBatch = new SpriteBatch(device);
            
            // Setup effects
            _basicEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                LightingEnabled = true,
                PreferPerPixelLighting = true,
                FogEnabled = true,
                FogStart = 200f,
                FogEnd = 800f,
                FogColor = GetFogColor()
            };
            
            // Setup default lighting
            _sunLight = new DirectionalLight
            {
                Direction = Vector3.Normalize(new Vector3(0.5f, -1.0f, 0.5f)),
                Color = GetTimeOfDayLightColor(),
                Intensity = GetTimeOfDayIntensity()
            };
        }
        
        /// <summary>
        /// Prepare world data for rendering with incremental update support
        /// </summary>
        public void PrepareWorldData(World world, bool forceFullRebuild = false)
        {
            if (world == null) return;
    
            var startTime = DateTime.Now;
    
            // Get all mesh objects
            var allObjects = world.SpatialMap.GetObjectsInRegion(
                new BoundingBox(Vector3.One * -10000, Vector3.One * 10000));
    
            // Separate by rendering strategy
            var instanceable = new List<MeshObject>();
            var unique = new List<MeshObject>();
    
            foreach (var obj in allObjects)
            {
                if (_enableInstancing && IsInstanceable(obj))
                    instanceable.Add(obj);
                else
                    unique.Add(obj);
            }
    
            // Handle instanced rendering
            if (_enableInstancing && instanceable.Count > 0)
            {
                // Handle full rebuild vs incremental
                if (forceFullRebuild || _instancesNeedFullRebuild)
                {
                    _instancedRenderer.MarkForFullRebuild();
                    _instancesNeedFullRebuild = false;
                }
                else if (_dirtyObjectIds.Count > 0)
                {
                    // Mark dirty objects in the instancer
                    foreach (var objectId in _dirtyObjectIds)
                    {
                        _instancedRenderer.MarkObjectDirty(objectId);
                
                        // Also mark the object itself as dirty for mesh updates
                        var obj = instanceable.FirstOrDefault(o => o.Id == objectId);
                        if (obj != null)
                        {
                            obj.IsDirty = true;
                        }
                    }
                }
        
                _instancedRenderer.PrepareInstances(instanceable, forceFullRebuild || _instancesNeedFullRebuild);
        
                // Clear dirty tracking
                _dirtyObjectIds.Clear();
        
                // Clear dirty flags on objects
                foreach (var obj in instanceable)
                {
                    obj.IsDirty = false;
                }
            }
    
            var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
            _helper.Monitor.Log($"Prepared {allObjects.Count} objects in {elapsed:F0}ms", LogLevel.Debug);
        }
        
        /// <summary>
        /// Mark a specific object as needing update
        /// </summary>
        public void MarkObjectDirty(string objectId)
        {
            _dirtyObjectIds.Add(objectId);
    
            if (_enableInstancing)
            {
                _instancedRenderer?.MarkObjectDirty(objectId);
            }
    
            _helper.Monitor.Log($"Marked object {objectId} as dirty", LogLevel.Debug);
        }
        
        public void RemoveObject(string objectId)
        {
            _dirtyObjectIds.Remove(objectId);
    
            if (_enableInstancing)
            {
                _instancedRenderer?.RemoveObject(objectId);
            }
    
            _helper.Monitor.Log($"Removed object {objectId} from rendering", LogLevel.Debug);
        }
        
        /// <summary>
        /// Mark instances for full rebuild
        /// </summary>
        public void InvalidateInstances()
        {
            _instancesNeedFullRebuild = true;
            _dirtyObjectIds.Clear();
    
            if (_enableInstancing)
            {
                _instancedRenderer?.MarkForFullRebuild();
            }
    
            _helper.Monitor.Log("Marked instances for full rebuild", LogLevel.Debug);
        }
        
        /// <summary>
        /// Main render method
        /// </summary>
        public void Render(World world, Camera3D camera)
        {
            if (world == null || camera == null) return;
            
            // If we have dirty objects, update instances
            if (_dirtyObjectIds.Count > 0 && _enableInstancing)
            {
                PrepareWorldData(world, false);
            }
            
            // Reset stats
            ObjectsRendered = 0;
            DrawCalls = 0;
            CulledObjects = 0;
            BatchCount = 0;
            
            // Update lighting based on time of day
            UpdateLighting();
            
            // Get visible objects from spatial map
            var frustum = new BoundingFrustum(camera.ViewMatrix * camera.ProjectionMatrix);
            var visibleObjects = world.SpatialMap.GetVisibleObjects(frustum);
            
            // Additional distance culling
            visibleObjects = CullByDistance(visibleObjects, camera.Position);
            
            // Sort for rendering
            var sortedObjects = SortForRendering(visibleObjects, camera.Position);
            
            // Shadow pass (if enabled)
            if (_enableShadows)
            {
                RenderShadowMap(sortedObjects.opaque, camera);
            }
            
            // Main render pass
            RenderMainPass(sortedObjects, camera);
            
            // Present to screen
            PresentToScreen();
            
            // Update stats
            ObjectsRendered = sortedObjects.opaque.Count + sortedObjects.transparent.Count;
            CulledObjects = world.TotalObjects - ObjectsRendered;
        }
        
        private List<MeshObject> CullByDistance(List<MeshObject> objects, Vector3 cameraPos)
        {
            return objects.Where(obj => 
            {
                var distance = Vector3.Distance(obj.WorldObject.Position, cameraPos);
                
                // Apply LOD-based culling distances
                float maxDistance = _renderDistance;
                if (obj.Metadata != null)
                {
                    // Smaller objects culled earlier
                    if (obj.WorldObject.Size.Length() < 64)
                        maxDistance *= 0.5f;
                    // Important objects rendered further
                    if (obj.WorldObject.ObjectType == ObjectType.Building)
                        maxDistance *= 1.5f;
                }
                
                return distance <= maxDistance;
            }).ToList();
        }
        
        private (List<MeshObject> opaque, List<MeshObject> transparent) SortForRendering(
            List<MeshObject> objects, Vector3 cameraPos)
        {
            var opaque = new List<MeshObject>();
            var transparent = new List<MeshObject>();
            
            // Use HashSet to prevent duplicates
            var processedIds = new HashSet<string>();
            
            foreach (var obj in objects)
            {
                // Skip if already processed (prevents duplicates)
                if (!processedIds.Add(obj.Id))
                {
                    continue;
                }
                
                if (obj.WorldObject.Material.IsTransparent || 
                    (obj.Metadata?.IsTransparent ?? false))
                {
                    transparent.Add(obj);
                }
                else
                {
                    opaque.Add(obj);
                }
            }
            
            // Sort opaque front-to-back for early Z rejection
            opaque.Sort((a, b) => 
            {
                var distA = Vector3.DistanceSquared(a.WorldObject.Position, cameraPos);
                var distB = Vector3.DistanceSquared(b.WorldObject.Position, cameraPos);
                return distA.CompareTo(distB);
            });
            
            // Sort transparent back-to-front for proper blending
            transparent.Sort((a, b) => 
            {
                var distA = Vector3.DistanceSquared(a.WorldObject.Position, cameraPos);
                var distB = Vector3.DistanceSquared(b.WorldObject.Position, cameraPos);
                return distB.CompareTo(distA);
            });
            
            return (opaque, transparent);
        }
        
        private void RenderMainPass((List<MeshObject> opaque, List<MeshObject> transparent) sortedObjects, 
            Camera3D camera)
        {
            // Set render target
            _device.SetRenderTarget(_mainTarget);
            
            // Clear with sky color
            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
                GetSkyColor(), 1.0f, 0);
            
            // Setup render states
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
            _device.SamplerStates[0] = SamplerState.LinearWrap;
            
            // Setup effect matrices
            _basicEffect.View = camera.ViewMatrix;
            _basicEffect.Projection = camera.ProjectionMatrix;
            
            // Render opaque objects
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
            if (sortedObjects.opaque.Count > 0)
            {
                RenderMeshObjects(sortedObjects.opaque, camera, false);
            }
            
            // Render instanced objects
            if (_enableInstancing && _instancedRenderer != null)
            {
                _instancedRenderer.Render(camera, _sunLight);
                DrawCalls += _instancedRenderer.DrawCallCount;
                BatchCount += _instancedRenderer.BatchCount;
            }
            
            // Render transparent objects
            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            if (sortedObjects.transparent.Count > 0)
            {
                RenderMeshObjects(sortedObjects.transparent, camera, true);
            }
            
            // Reset states
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
        }
        
        private void RenderMeshObjects(List<MeshObject> objects, Camera3D camera, bool transparent)
        {
            // Get excluded objects from instancer
            var excludedFromInstancing = _instancedRenderer?.GetExcludedObjects() ?? new HashSet<string>();
    
            // Group by texture and material for batching
            var batches = objects.GroupBy(obj => GetBatchKey(obj));
    
            foreach (var batch in batches)
            {
                // Filter to only non-instanced objects
                var nonInstancedObjects = batch.Where(obj => 
                {
                    // Don't render if it's being instanced
                    if (_enableInstancing && IsInstanceable(obj))
                    {
                        // Check if it's actually being instanced or was excluded
                        return excludedFromInstancing.Contains(obj.Id) || 
                               !_instancedRenderer.IsActuallyInstanced(obj.Id);
                    }
                    return true; // Not instanceable, so render normally
                }).ToList();
        
                if (nonInstancedObjects.Count == 0)
                    continue;
        
                // Setup material
                var firstObj = nonInstancedObjects.First();
                SetupMaterial(firstObj);
        
                // Render each object in batch
                foreach (var meshObj in nonInstancedObjects)
                {
                    if (meshObj.Mesh == null) continue;
                    if (IsInstanceable(meshObj) && (excludedFromInstancing.Contains(meshObj.Id) || 
                        !_instancedRenderer.IsActuallyInstanced(meshObj.Id))) 
                        _basicEffect.World = Matrix.CreateTranslation(meshObj.WorldObject.Position);
                    else 
                        _basicEffect.World = Matrix.Identity;
                    _basicEffect.Alpha = transparent ? 0.8f : 1.0f;
            
                    RenderMesh(meshObj.Mesh);
                    DrawCalls++;
                }
        
                BatchCount++;
            }
        }
        
        private void RenderMesh(Mesh mesh)
        {
            _device.SetVertexBuffer(mesh.VertexBuffer);
            _device.Indices = mesh.IndexBuffer;
            
            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, mesh.PrimitiveCount);
            }
        }
        
        private void SetupMaterial(MeshObject meshObj)
        {
            var material = meshObj.WorldObject.Material;
            var metadata = meshObj.Metadata;
            
            // Configure effect based on material
            _basicEffect.DiffuseColor = new Vector3(
                meshObj.WorldObject.Tint.R / 255f,
                meshObj.WorldObject.Tint.G / 255f,
                meshObj.WorldObject.Tint.B / 255f
            );
            
            _basicEffect.SpecularColor = new Vector3(1 - material.Roughness);
            _basicEffect.SpecularPower = Math.Max(1, (int)(128 * (1 - material.Roughness)));
            
            // Apply emission
            if (material.EmissionStrength > 0)
            {
                _basicEffect.EmissiveColor = new Vector3(
                    material.EmissionColor.R / 255f * material.EmissionStrength,
                    material.EmissionColor.G / 255f * material.EmissionStrength,
                    material.EmissionColor.B / 255f * material.EmissionStrength
                );
            }
            else
            {
                _basicEffect.EmissiveColor = Vector3.Zero;
            }
            
            // Set texture if available
            _basicEffect.Texture = GetTextureForObject(meshObj);
        }
        
        private Texture2D GetTextureForObject(MeshObject meshObj)
        {
            // Get the actual texture from the TextureManager
            var texture = _textureManager.GetTextureById(meshObj.WorldObject.TextureId);
            
            if (texture == null)
            {
                // Only fall back if texture truly doesn't exist
                texture = new Texture2D(_device, 1, 1);
                texture.SetData(new[] { Color.White });
            }
            
            return texture;
        }
        
        private void RenderShadowMap(List<MeshObject> objects, Camera3D camera)
        {
            if (!_enableShadows || _depthTarget == null) return;
            
            _device.SetRenderTarget(_depthTarget);
            _device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.White, 1.0f, 0);
            
            // Setup shadow projection from light's perspective
            var lightView = Matrix.CreateLookAt(
                -_sunLight.Direction * 500 + camera.Position,
                camera.Position,
                Vector3.Up
            );
            var lightProjection = Matrix.CreateOrthographic(1000, 1000, 1, 2000);
            
            // Render depth only
            // Would use shadow shader here
        }
        
        private void PresentToScreen()
        {
            _device.SetRenderTarget(null);
            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            _spriteBatch.Draw(_mainTarget, _device.Viewport.Bounds, Color.White);
            _spriteBatch.End();
        }
        
        private void UpdateLighting()
        {
            // Update based on game time
            var timeOfDay = Game1.timeOfDay;
            
            _sunLight.Color = GetTimeOfDayLightColor();
            _sunLight.Intensity = GetTimeOfDayIntensity();
            
            // Update fog
            _basicEffect.FogColor = GetFogColor();
            
            // Configure effect lighting
            _basicEffect.AmbientLightColor = new Vector3(0.3f) * _sunLight.Intensity;
            _basicEffect.DirectionalLight0.Enabled = true;
            _basicEffect.DirectionalLight0.Direction = _sunLight.Direction;
            _basicEffect.DirectionalLight0.DiffuseColor = new Vector3(
                _sunLight.Color.R / 255f * _sunLight.Intensity,
                _sunLight.Color.G / 255f * _sunLight.Intensity,
                _sunLight.Color.B / 255f * _sunLight.Intensity
            );
            _basicEffect.DirectionalLight0.SpecularColor = _basicEffect.DirectionalLight0.DiffuseColor * 0.3f;
        }
        
        private Color GetTimeOfDayLightColor()
        {
            var time = Game1.timeOfDay;
            
            if (time < 600 || time > 2000)
                return new Color(100, 100, 150); // Night
            else if (time < 800)
                return new Color(255, 200, 150); // Dawn
            else if (time < 1700)
                return new Color(255, 250, 230); // Day
            else
                return new Color(255, 180, 100); // Dusk
        }
        
        private float GetTimeOfDayIntensity()
        {
            var time = Game1.timeOfDay;
            
            if (time < 600 || time > 2000)
                return 0.3f; // Night
            else if (time < 800 || time > 1700)
                return 0.7f; // Dawn/Dusk
            else
                return 1.0f; // Day
        }
        
        private Color GetSkyColor()
        {
            var time = Game1.timeOfDay;
            
            if (time < 600 || time > 2000)
                return new Color(20, 30, 60); // Night sky
            else if (time < 800)
                return new Color(150, 120, 180); // Dawn sky
            else if (time < 1700)
                return new Color(135, 206, 250); // Day sky
            else
                return new Color(255, 150, 100); // Sunset sky
        }
        
        private Vector3 GetFogColor()
        {
            var skyColor = GetSkyColor();
            return new Vector3(skyColor.R / 255f, skyColor.G / 255f, skyColor.B / 255f) * 0.8f;
        }

        public static bool IsInstanceable(MeshObject obj)
        {
            if (obj.Metadata == null || obj.Mesh == null) return false;

            // More inclusive logic - instance any object that appears multiple times
            // The batching system will group them appropriately
            return obj.Metadata.MeshType == TileMeshType.Plane ||
                   obj.Metadata.MeshType == TileMeshType.Box ||
                   obj.Metadata.MeshType == TileMeshType.Billboard ||
                   obj.Metadata.MeshType == TileMeshType.CrossedBillboard ||
                   obj.Metadata.IsFloor ||
                   obj.WorldObject.ObjectType == ObjectType.Tile ||
                   obj.WorldObject.ObjectType == ObjectType.TerrainFeature;
        }


        public static bool IsInstanceableExt(MeshObject obj)
        {
            if (obj.Metadata == null) return false;

            // More inclusive logic - instance any object that appears multiple times
            // The batching system will group them appropriately
            return obj.Metadata.MeshType == TileMeshType.Plane ||
                   obj.Metadata.MeshType == TileMeshType.Box ||
                   obj.Metadata.MeshType == TileMeshType.Billboard ||
                   obj.Metadata.MeshType == TileMeshType.CrossedBillboard ||
                   obj.Metadata.IsFloor ||
                   obj.WorldObject.ObjectType == ObjectType.Tile ||
                   obj.WorldObject.ObjectType == ObjectType.TerrainFeature;
        }

        private string GetBatchKey(MeshObject obj)
        {
            var material = obj.WorldObject.Material;
            var textureId = obj.WorldObject.TextureId;
            return $"{textureId}_{material.GetHashCode()}";
        }

        public void Dispose()
        {
            _mainTarget?.Dispose();
            _depthTarget?.Dispose();
            _spriteBatch?.Dispose();
            _basicEffect?.Dispose();
            _shadowEffect?.Dispose();
            _meshRenderer?.Dispose();
            _instancedRenderer?.Dispose();
        }
    }

    /// <summary>
    /// Simple mesh renderer for non-instanced objects
    /// </summary>
    public class MeshRenderer : IDisposable
    {
        private GraphicsDevice _device;
        private MonitorHelper _helper;

        public MeshRenderer(GraphicsDevice device, MonitorHelper helper)
        {
            _device = device;
            _helper = helper;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Instanced renderer for repeated objects
    /// </summary>
    public class InstancedMeshRenderer : IDisposable
    {
        private GraphicsDevice _device;
        private MonitorHelper _helper;
        private TextureManager _textureManager;
        private Effect _instancedEffect;
        private bool _shaderLoaded = false;

        // Vertex declaration for instance data
        private VertexDeclaration _instanceVertexDeclaration;

        // Batches organized by texture and material
        private Dictionary<string, HardwareInstanceBatch> _batches = new();

        public int DrawCallCount { get; private set; }
        public int TotalInstances { get; private set; }
        public int BatchCount => _batches.Count;

        private Dictionary<string, HashSet<string>> _batchObjectIds = new(); // Track which objects are in each batch
        private bool _needsFullRebuild = true;

        private HashSet<string> _excludedFromInstancing = new();
        private bool _pendingUpdate = false;

        // Instance vertex structure
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
        public struct InstanceData : IVertexType
        {
            public Matrix World;
            public Color Color;

            public static readonly VertexDeclaration VertexDeclaration = new VertexDeclaration(
                new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
                new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
                new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
                new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
                new VertexElement(64, VertexElementFormat.Color, VertexElementUsage.Color, 1)
            );

            VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
        }

        private class HardwareInstanceBatch
        {
            public Mesh SharedMesh;
            public Material Material;
            public Texture2D Texture;
            public List<InstanceData> Instances = new();
            public DynamicVertexBuffer InstanceBuffer;
            public bool NeedsUpdate = true;
        }

        public InstancedMeshRenderer(GraphicsDevice device, MonitorHelper helper, TextureManager textureManager)
        {
            _device = device;
            _helper = helper;
            _textureManager = textureManager;
            _instanceVertexDeclaration = InstanceData.VertexDeclaration;

            LoadShader();
        }

        private void LoadShader()
        {
            try
            {
                // Try to load the custom shader
                _instancedEffect = _helper.ModContent.Load<Effect>("Content/Shaders/InstancedShader.xnb");
                _shaderLoaded = true;
                _helper.Monitor.Log("Hardware instancing shader loaded successfully", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _helper.Monitor.Log($"Failed to load instancing shader: {ex.Message}", LogLevel.Warn);
                _helper.Monitor.Log("Falling back to standard rendering", LogLevel.Warn);
                _shaderLoaded = false;

                // Create a basic effect as fallback
                _instancedEffect = new BasicEffect(_device)
                {
                    TextureEnabled = true,
                    LightingEnabled = true,
                    VertexColorEnabled = true
                };
            }
        }

        public void PrepareInstances(List<MeshObject> objects, bool fullRebuild = false)
        {
            if (fullRebuild || _needsFullRebuild)
            {
                FullRebuildInstances(objects);
                _needsFullRebuild = false;
                _pendingUpdate = false;
            }
            else if (_pendingUpdate)
            {
                // Incremental update - only process changed objects
                UpdateChangedInstances(objects);
                _pendingUpdate = false;
            }
        }
        
        private void FullRebuildInstances(List<MeshObject> objects)
    {
        // Clear previous batches
        foreach (var batch in _batches.Values)
        {
            batch.InstanceBuffer?.Dispose();
        }
        _batches.Clear();
        _batchObjectIds.Clear();
        _excludedFromInstancing.Clear();
        TotalInstances = 0;
        
        // Group objects by batch key
        var groups = objects.Where(obj => obj.Mesh != null)
                           .GroupBy(obj => GetBatchKey(obj));
        
        int skippedSingletons = 0;
        int createdBatches = 0;
        
        foreach (var group in groups)
        {
            var groupList = group.ToList();
            
            // Track objects that don't have enough instances
            if (groupList.Count < 2)
            {
                foreach (var obj in groupList)
                {
                    _excludedFromInstancing.Add(obj.Id);
                }
                skippedSingletons++;
                continue;
            }
            
            var firstObj = groupList.First();
            var key = GetBatchKey(firstObj);
            
            var batch = CreateBatch(groupList);
            if (batch != null)
            {
                _batches[key] = batch;
                _batchObjectIds[key] = new HashSet<string>(groupList.Select(o => o.Id));
                TotalInstances += batch.Instances.Count;
                createdBatches++;
            }
        }
        
        _helper.Monitor.Log($"Full rebuild: {createdBatches} batches, {TotalInstances} instances, {skippedSingletons} excluded groups", LogLevel.Debug);
    }
    
    private void UpdateChangedInstances(List<MeshObject> objects)
    {
        var batchesToUpdate = new HashSet<string>();
        var objectsById = objects.ToDictionary(o => o.Id);
        
        // Check all objects for changes
        foreach (var obj in objects)
        {
            if (!obj.IsDirty) continue;
            
            var newKey = GetBatchKey(obj);
            
            // Find which batch this object was in
            string oldBatch = null;
            foreach (var kvp in _batchObjectIds)
            {
                if (kvp.Value.Contains(obj.Id))
                {
                    oldBatch = kvp.Key;
                    break;
                }
            }
            
            // Handle batch changes
            if (oldBatch != newKey)
            {
                if (oldBatch != null)
                {
                    _batchObjectIds[oldBatch].Remove(obj.Id);
                    batchesToUpdate.Add(oldBatch);
                }
                
                if (!_batchObjectIds.ContainsKey(newKey))
                    _batchObjectIds[newKey] = new HashSet<string>();
                
                _batchObjectIds[newKey].Add(obj.Id);
                batchesToUpdate.Add(newKey);
            }
            else if (oldBatch != null)
            {
                batchesToUpdate.Add(oldBatch);
            }
        }
        
        // Update affected batches and check for singletons
        foreach (var batchKey in batchesToUpdate)
        {
            UpdateBatch(batchKey, objects);
        }
        
        _helper.Monitor.Log($"Incremental update: {batchesToUpdate.Count} batches updated", LogLevel.Debug);
    }
    
    private void UpdateBatch(string batchKey, List<MeshObject> allObjects)
    {
        if (!_batchObjectIds.TryGetValue(batchKey, out var objectIds) || objectIds.Count == 0)
        {
            // Remove empty batch
            if (_batches.TryGetValue(batchKey, out var emptyBatch))
            {
                emptyBatch.InstanceBuffer?.Dispose();
                _batches.Remove(batchKey);
            }
            _batchObjectIds.Remove(batchKey);
            return;
        }
        
        // Get objects for this batch
        var batchObjects = allObjects.Where(o => objectIds.Contains(o.Id)).ToList();
        
        if (batchObjects.Count < 2)
        {
            // Not worth instancing - mark objects as excluded
            foreach (var obj in batchObjects)
            {
                _excludedFromInstancing.Add(obj.Id);
            }
            
            // Remove the batch
            if (_batches.TryGetValue(batchKey, out var oldBatch))
            {
                oldBatch.InstanceBuffer?.Dispose();
                _batches.Remove(batchKey);
            }
            _batchObjectIds.Remove(batchKey);
            
            _helper.Monitor.Log($"Batch {batchKey} now has only {batchObjects.Count} objects, removing from instancing", LogLevel.Debug);
            return;
        }
        
        // Remove from excluded list if they're now instanceable
        foreach (var obj in batchObjects)
        {
            _excludedFromInstancing.Remove(obj.Id);
        }
        
        // Create or update batch
        var batch = CreateBatch(batchObjects);
        if (batch != null)
        {
            // Dispose old buffer if exists
            if (_batches.TryGetValue(batchKey, out var oldBatch))
            {
                oldBatch.InstanceBuffer?.Dispose();
            }
            
            _batches[batchKey] = batch;
        }
    }

        private HardwareInstanceBatch CreateBatch(List<MeshObject> objects)
        {
            if (objects.Count == 0) return null;

            var firstObj = objects.First();
            var batch = new HardwareInstanceBatch
            {
                SharedMesh = firstObj.Mesh,
                Material = firstObj.WorldObject.Material
            };

            // Get texture
            if (firstObj.WorldObject.TextureId >= 0)
            {
                batch.Texture = _textureManager.GetTextureById(firstObj.WorldObject.TextureId);
            }

            if (batch.Texture == null)
            {
                batch.Texture = CreateFallbackTexture();
            }

            // Create instance data
            foreach (var obj in objects)
            {
                var instanceData = new InstanceData
                {
                    World = Matrix.CreateTranslation(obj.WorldObject.Position),
                    Color = obj.WorldObject.Tint
                };
                batch.Instances.Add(instanceData);
            }

            // Create instance buffer
            if (_shaderLoaded && batch.Instances.Count > 0)
            {
                batch.InstanceBuffer = new DynamicVertexBuffer(
                    _device,
                    _instanceVertexDeclaration,
                    batch.Instances.Count,
                    BufferUsage.WriteOnly
                );
                batch.InstanceBuffer.SetData(batch.Instances.ToArray());
            }

            return batch;
        }

        // Fix the batch key to handle furniture properly
        private string GetBatchKey(MeshObject obj)
        {
            var textureId = obj.WorldObject.TextureId;
            var meshType = obj.Metadata?.MeshType ?? TileMeshType.Box;
            var isTransparent = obj.Metadata?.IsTransparent ?? false;
            var tileIndex = obj.WorldObject.TileIndex;
            var layer = obj.WorldObject.Layer;
            var rect = $"{obj.WorldObject.SourceRectangle.Width}_{obj.WorldObject.SourceRectangle.Height}_{obj.WorldObject.SourceRectangle.X}_{obj.WorldObject.SourceRectangle.Y}";

            // Include layer to prevent furniture from batching with other object types
            return $"tex{textureId}_tile{tileIndex}_mesh{meshType}_trans{isTransparent}_layer{layer}_rect{rect}";
        }
        
        public void UpdateSingleObject(string objectId, MeshObject updatedObject)
        {
            // Find which batch contains this object
            string targetBatch = null;
            foreach (var kvp in _batchObjectIds)
            {
                if (kvp.Value.Contains(objectId))
                {
                    targetBatch = kvp.Key;
                    break;
                }
            }
    
            if (targetBatch == null)
            {
                // Object not in any batch, check if it should be added
                var newKey = GetBatchKey(updatedObject);
                if (!_batchObjectIds.ContainsKey(newKey))
                {
                    _batchObjectIds[newKey] = new HashSet<string>();
                }
                _batchObjectIds[newKey].Add(objectId);
                targetBatch = newKey;
            }
            else
            {
                // Check if batch key changed
                var newKey = GetBatchKey(updatedObject);
                if (newKey != targetBatch)
                {
                    // Move to different batch
                    _batchObjectIds[targetBatch].Remove(objectId);
                    if (!_batchObjectIds.ContainsKey(newKey))
                    {
                        _batchObjectIds[newKey] = new HashSet<string>();
                    }
                    _batchObjectIds[newKey].Add(objectId);
                }
            }
        }

        public bool IsActuallyInstanced(string objectId)
        {
            return !_excludedFromInstancing.Contains(objectId) && 
                   _batchObjectIds.Any(kvp => kvp.Value.Contains(objectId));
        }
    
        public HashSet<string> GetExcludedObjects()
        {
            return new HashSet<string>(_excludedFromInstancing);
        }
    
        public void MarkObjectDirty(string objectId)
        {
            _pendingUpdate = true;
            _helper.Monitor.Log($"Marked object {objectId} dirty, pending instance update", LogLevel.Debug);
        }
    
        public void MarkForFullRebuild()
        {
            _needsFullRebuild = true;
            _pendingUpdate = false;
            _excludedFromInstancing.Clear();
        }

        public void Render(Camera3D camera, DirectionalLight light)
        {
            DrawCallCount = 0;

            if (_batches.Count == 0) return;

            if (_shaderLoaded && _instancedEffect != null)
            {
                RenderWithHardwareInstancing(camera, light);
            }
            else
            {
                RenderWithFallback(camera, light);
            }
        }

        private void RenderWithHardwareInstancing(Camera3D camera, DirectionalLight light)
        {
            // Setup common shader parameters
            _instancedEffect.Parameters["View"]?.SetValue(camera.ViewMatrix);
            _instancedEffect.Parameters["Projection"]?.SetValue(camera.ProjectionMatrix);
            _instancedEffect.Parameters["LightDirection"]?.SetValue(light.Direction);
            _instancedEffect.Parameters["LightColor"]?.SetValue(light.Color.ToVector3() * light.Intensity);
            _instancedEffect.Parameters["AmbientColor"]?.SetValue(new Vector3(0.3f) * light.Intensity);

            // Setup fog
            _instancedEffect.Parameters["FogStart"]?.SetValue(600f);
            _instancedEffect.Parameters["FogEnd"]?.SetValue(1200f);
            _instancedEffect.Parameters["FogColor"]?.SetValue(GetFogColor());

            // Render each batch
            foreach (var batch in _batches.Values)
            {
                if (batch.SharedMesh == null || batch.Instances.Count == 0) continue;

                // Skip if no hardware instancing available
                if (batch.InstanceBuffer == null) continue;

                // Set texture
                _instancedEffect.Parameters["Texture"]?.SetValue(batch.Texture);

                // Setup vertex buffers
                _device.SetVertexBuffers(
                    new VertexBufferBinding(batch.SharedMesh.VertexBuffer, 0, 0),
                    new VertexBufferBinding(batch.InstanceBuffer, 0, 1)
                );
                _device.Indices = batch.SharedMesh.IndexBuffer;

                // Apply material settings based on blend state needs
                if (batch.Material.IsTransparent)
                {
                    _device.BlendState = BlendState.AlphaBlend;
                    _device.DepthStencilState = DepthStencilState.DepthRead;
                }
                else
                {
                    _device.BlendState = BlendState.AlphaBlend;
                    _device.DepthStencilState = DepthStencilState.Default;
                }

                // Draw all instances in a single call
                foreach (var pass in _instancedEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawInstancedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        batch.SharedMesh.PrimitiveCount,
                        batch.Instances.Count
                    );
                }

                DrawCallCount++;
            }

            // Reset states
            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;
        }

        private void RenderWithFallback(Camera3D camera, DirectionalLight light)
        {
            // Fallback to non-instanced rendering if shader not available
            var basicEffect = _instancedEffect as BasicEffect;
            if (basicEffect == null) return;

            basicEffect.View = camera.ViewMatrix;
            basicEffect.Projection = camera.ProjectionMatrix;
            basicEffect.DirectionalLight0.Direction = light.Direction;
            basicEffect.DirectionalLight0.DiffuseColor = light.Color.ToVector3() * light.Intensity;
            basicEffect.AmbientLightColor = new Vector3(0.3f) * light.Intensity;
            basicEffect.FogEnabled = true;
            basicEffect.FogStart = 200f;
            basicEffect.FogEnd = 800f;
            basicEffect.FogColor = GetFogColor();

            foreach (var batch in _batches.Values)
            {
                if (batch.SharedMesh == null || batch.Instances.Count == 0) continue;

                basicEffect.Texture = batch.Texture;

                _device.SetVertexBuffer(batch.SharedMesh.VertexBuffer);
                _device.Indices = batch.SharedMesh.IndexBuffer;

                // Render each instance separately (slower but works without custom shader)
                foreach (var instance in batch.Instances)
                {
                    basicEffect.World = instance.World;
                    basicEffect.DiffuseColor = instance.Color.ToVector3();
                    basicEffect.Alpha = batch.Material.IsTransparent ? 0.8f : 1.0f;

                    foreach (var pass in basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        _device.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0, 0,
                            batch.SharedMesh.PrimitiveCount
                        );
                    }

                    DrawCallCount++;
                }
            }
        }

        public void UpdateInstances(string batchKey, List<InstanceData> newInstances)
        {
            if (_batches.TryGetValue(batchKey, out var batch))
            {
                batch.Instances = newInstances;
                batch.NeedsUpdate = true;

                // Update the instance buffer
                if (_shaderLoaded && batch.InstanceBuffer != null && newInstances.Count > 0)
                {
                    if (batch.InstanceBuffer.VertexCount != newInstances.Count)
                    {
                        batch.InstanceBuffer.Dispose();
                        batch.InstanceBuffer = new DynamicVertexBuffer(
                            _device,
                            _instanceVertexDeclaration,
                            newInstances.Count,
                            BufferUsage.WriteOnly
                        );
                    }

                    batch.InstanceBuffer.SetData(newInstances.ToArray());
                }
            }
        }

        private Texture2D CreateFallbackTexture()
        {
            var texture = new Texture2D(_device, 1, 1);
            texture.SetData(new[] { Color.White });
            return texture;
        }

        private Vector3 GetFogColor()
        {
            var time = Game1.timeOfDay;
            Color skyColor;

            if (time < 600 || time > 2000)
                skyColor = new Color(20, 30, 60);
            else if (time < 800)
                skyColor = new Color(150, 120, 180);
            else if (time < 1700)
                skyColor = new Color(135, 206, 250);
            else
                skyColor = new Color(255, 150, 100);

            return skyColor.ToVector3() * 0.8f;
        }

        public void Dispose()
        {
            foreach (var batch in _batches.Values)
            {
                batch.InstanceBuffer?.Dispose();
            }

            _instancedEffect?.Dispose();
        }

        public void RemoveObject(string objectId)
        {
            // Find which batch contains this object
            string targetBatch = null;
            foreach (var kvp in _batchObjectIds)
            {
                if (kvp.Value.Contains(objectId))
                {
                    targetBatch = kvp.Key;
                    kvp.Value.Remove(objectId);
                    break;
                }
            }
    
            // Remove from excluded list if present
            _excludedFromInstancing.Remove(objectId);
    
            if (targetBatch != null)
            {
                // Check if batch is now too small
                if (_batchObjectIds[targetBatch].Count < 2)
                {
                    // Mark remaining objects as excluded
                    foreach (var remainingId in _batchObjectIds[targetBatch])
                    {
                        _excludedFromInstancing.Add(remainingId);
                    }
            
                    // Remove the batch
                    if (_batches.TryGetValue(targetBatch, out var batch))
                    {
                        batch.InstanceBuffer?.Dispose();
                        _batches.Remove(targetBatch);
                    }
                    _batchObjectIds.Remove(targetBatch);
                }
                else
                {
                    // Mark batch for update
                    _pendingUpdate = true;
                }
            }
    
            _helper.Monitor.Log($"Removed object {objectId} from instancing", LogLevel.Debug);
        }
    }

    /// <summary>
    /// Directional light for sun/moon
    /// </summary>
    public struct DirectionalLight
    {
        public Vector3 Direction;
        public Color Color;
        public float Intensity;
    }

    /// <summary>
    /// Point light for torches, lamps, etc
    /// </summary>
    public struct PointLight
    {
        public Vector3 Position;
        public Color Color;
        public float Intensity;
        public float Range;
    }
}