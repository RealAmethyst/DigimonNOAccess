using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.AI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Manages Steam Audio environmental audio: scene geometry, simulator, and per-source simulation.
    /// Provides occlusion, distance attenuation, and air absorption via DirectEffect.
    /// </summary>
    public static class SteamAudioEnvironment
    {
        // Steam Audio handles
        private static IntPtr _scene = IntPtr.Zero;
        private static IntPtr _simulator = IntPtr.Zero;
        private static IntPtr _staticMesh = IntPtr.Zero;

        // State
        private static bool _initialized;
        private static bool _geometryLoaded;
        private static int _lastMapNo = -1;
        private static int _lastAreaNo = -1;
        private static float _geometryRebuildTime;
        private static bool _geometryRebuildPending;

        // Source management
        private static readonly Dictionary<int, SourceData> _sources = new Dictionary<int, SourceData>();
        private static int _nextSourceId;
        private static readonly object _sourceLock = new object();

        // Listener state
        private static IPLCoordinateSpace3 _listenerCoords;
        private static readonly object _listenerLock = new object();

        // Geometry budget
        private const int MaxTriangles = 100000;

        public static bool IsInitialized => _initialized;
        public static bool IsGeometryLoaded => _geometryLoaded;

        private class SourceData
        {
            public IntPtr Handle;
            public float PositionX, PositionY, PositionZ;
            public IPLDirectEffectParams CachedDirectParams;
            public bool HasOutput;
        }

        // ====================================================================
        // Initialization / Shutdown
        // ====================================================================

        public static void Initialize()
        {
            if (_initialized) return;
            if (!SteamAudioManager.IsAvailable)
            {
                DebugLogger.Log("[SteamAudioEnv] Steam Audio not available, environment disabled");
                return;
            }

            try
            {
                var context = SteamAudioManager.Context;

                // Create scene
                var sceneSettings = new IPLSceneSettings
                {
                    type = IPLSceneType.Default,
                    closestHitCallback = IntPtr.Zero,
                    anyHitCallback = IntPtr.Zero,
                    batchedClosestHitCallback = IntPtr.Zero,
                    batchedAnyHitCallback = IntPtr.Zero,
                    userData = IntPtr.Zero,
                    embreeDevice = IntPtr.Zero,
                    radeonRaysDevice = IntPtr.Zero
                };

                var err = SteamAudioNative.iplSceneCreate(context, ref sceneSettings, out _scene);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[SteamAudioEnv] Scene create failed: {err}");
                    return;
                }

                // Create simulator (direct simulation only: occlusion, attenuation, air absorption)
                var simSettings = new IPLSimulationSettings
                {
                    flags = IPLSimulationFlags.Direct,
                    sceneType = IPLSceneType.Default,
                    reflectionType = IPLReflectionEffectType.Parametric,
                    maxNumOcclusionSamples = 16,
                    maxNumRays = 0,
                    numDiffuseSamples = 0,
                    maxDuration = 0f,
                    maxOrder = 0,
                    maxNumSources = 32,
                    numThreads = 1,
                    rayBatchSize = 0,
                    numVisSamples = 0,
                    samplingRate = SteamAudioManager.SampleRate,
                    frameSize = SteamAudioManager.FrameSize,
                    openCLDevice = IntPtr.Zero,
                    radeonRaysDevice = IntPtr.Zero,
                    tanDevice = IntPtr.Zero
                };

                err = SteamAudioNative.iplSimulatorCreate(context, ref simSettings, out _simulator);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[SteamAudioEnv] Simulator create failed: {err}");
                    SteamAudioNative.iplSceneRelease(ref _scene);
                    return;
                }

                // Attach scene to simulator
                SteamAudioNative.iplSimulatorSetScene(_simulator, _scene);
                SteamAudioNative.iplSimulatorCommit(_simulator);

                _initialized = true;
                DebugLogger.Log("[SteamAudioEnv] Initialized - scene + simulator ready");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudioEnv] Init error: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            if (!_initialized) return;

            lock (_sourceLock)
            {
                foreach (var kvp in _sources)
                {
                    var src = kvp.Value.Handle;
                    if (src != IntPtr.Zero)
                    {
                        SteamAudioNative.iplSourceRemove(src, _simulator);
                        SteamAudioNative.iplSourceRelease(ref src);
                    }
                }
                _sources.Clear();
            }

            ReleaseGeometry();

            if (_simulator != IntPtr.Zero)
                SteamAudioNative.iplSimulatorRelease(ref _simulator);
            if (_scene != IntPtr.Zero)
                SteamAudioNative.iplSceneRelease(ref _scene);

            _initialized = false;
            _geometryLoaded = false;

            DebugLogger.Log("[SteamAudioEnv] Shut down");
        }

        // ====================================================================
        // Source Lifecycle
        // ====================================================================

        /// <summary>
        /// Register a new simulation source. Returns source ID, or -1 on failure.
        /// </summary>
        public static int RegisterSource()
        {
            if (!_initialized || _simulator == IntPtr.Zero) return -1;

            try
            {
                var settings = new IPLSourceSettings
                {
                    flags = IPLSimulationFlags.Direct
                };

                var err = SteamAudioNative.iplSourceCreate(_simulator, ref settings, out IntPtr source);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[SteamAudioEnv] Source create failed: {err}");
                    return -1;
                }

                SteamAudioNative.iplSourceAdd(source, _simulator);
                SteamAudioNative.iplSimulatorCommit(_simulator);

                int id;
                lock (_sourceLock)
                {
                    id = _nextSourceId++;
                    _sources[id] = new SourceData
                    {
                        Handle = source,
                        CachedDirectParams = CreateDefaultDirectParams(),
                        HasOutput = false
                    };
                }

                return id;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudioEnv] RegisterSource error: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Unregister and release a simulation source.
        /// </summary>
        public static void UnregisterSource(int sourceId)
        {
            if (!_initialized || _simulator == IntPtr.Zero) return;

            try
            {
                lock (_sourceLock)
                {
                    if (!_sources.TryGetValue(sourceId, out var data)) return;
                    _sources.Remove(sourceId);

                    if (data.Handle != IntPtr.Zero)
                    {
                        SteamAudioNative.iplSourceRemove(data.Handle, _simulator);
                        SteamAudioNative.iplSourceRelease(ref data.Handle);
                        SteamAudioNative.iplSimulatorCommit(_simulator);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudioEnv] UnregisterSource error: {ex.Message}");
            }
        }

        // ====================================================================
        // Per-Frame Updates (called from game thread)
        // ====================================================================

        /// <summary>
        /// Update listener position and orientation. Called from game thread.
        /// </summary>
        public static void UpdateListener(UnityEngine.Vector3 position, UnityEngine.Vector3 forward, UnityEngine.Vector3 up)
        {
            // Convert Unity left-handed to Steam Audio right-handed
            var right = UnityEngine.Vector3.Cross(up, forward).normalized;

            var coords = new IPLCoordinateSpace3
            {
                right = new IPLVector3 { x = right.x, y = right.y, z = -right.z },
                up = new IPLVector3 { x = up.x, y = up.y, z = -up.z },
                ahead = new IPLVector3 { x = forward.x, y = forward.y, z = -forward.z },
                origin = new IPLVector3 { x = position.x, y = position.y, z = -position.z }
            };

            lock (_listenerLock)
            {
                _listenerCoords = coords;
            }
        }

        /// <summary>
        /// Update a source's world position. Called from game/position thread.
        /// </summary>
        public static void SetSourcePosition(int sourceId, float x, float y, float z)
        {
            lock (_sourceLock)
            {
                if (_sources.TryGetValue(sourceId, out var data))
                {
                    data.PositionX = x;
                    data.PositionY = y;
                    data.PositionZ = z;
                }
            }
        }

        /// <summary>
        /// Run direct simulation for all registered sources. Called from game thread per frame.
        /// </summary>
        public static void RunDirectSimulation()
        {
            if (!_initialized || _simulator == IntPtr.Zero) return;
            if (!_geometryLoaded) return; // No geometry = no meaningful simulation

            try
            {
                // Set shared listener inputs
                IPLCoordinateSpace3 listener;
                lock (_listenerLock)
                {
                    listener = _listenerCoords;
                }

                var sharedInputs = new IPLSimulationSharedInputs
                {
                    listener = listener,
                    numRays = 4096,
                    numBounces = 4,
                    duration = 2.0f,
                    order = 1,
                    irradianceMinDistance = 1.0f,
                    pathingVisCallback = IntPtr.Zero,
                    pathingUserData = IntPtr.Zero
                };
                SteamAudioNative.iplSimulatorSetSharedInputs(
                    _simulator, IPLSimulationFlags.Direct, ref sharedInputs);

                // Set per-source inputs
                lock (_sourceLock)
                {
                    foreach (var kvp in _sources)
                    {
                        var data = kvp.Value;
                        if (data.Handle == IntPtr.Zero) continue;

                        SetSourceInputs(data);
                    }
                }

                // Run direct simulation (fast: raycasts only)
                SteamAudioNative.iplSimulatorRunDirect(_simulator);

                // Read outputs
                lock (_sourceLock)
                {
                    foreach (var kvp in _sources)
                    {
                        var data = kvp.Value;
                        if (data.Handle == IntPtr.Zero) continue;

                        var outputs = new IPLSimulationOutputs();
                        SteamAudioNative.iplSourceGetOutputs(
                            data.Handle, IPLSimulationFlags.Direct, ref outputs);

                        data.CachedDirectParams = outputs.direct;
                        data.HasOutput = true;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudioEnv] RunDirectSimulation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cached direct effect parameters for a source. Thread-safe.
        /// </summary>
        public static bool GetDirectParams(int sourceId, out IPLDirectEffectParams directParams)
        {
            lock (_sourceLock)
            {
                if (_sources.TryGetValue(sourceId, out var data) && data.HasOutput)
                {
                    directParams = data.CachedDirectParams;
                    return true;
                }
            }

            directParams = CreateDefaultDirectParams();
            return false;
        }

        // ====================================================================
        // Area Transitions & Geometry
        // ====================================================================

        /// <summary>
        /// Check for area change and schedule geometry rebuild. Called from game thread.
        /// </summary>
        public static void CheckAreaChange(int mapNo, int areaNo)
        {
            if (!_initialized) return;

            if (mapNo != _lastMapNo || areaNo != _lastAreaNo)
            {
                _lastMapNo = mapNo;
                _lastAreaNo = areaNo;

                // Release old geometry
                ReleaseGeometry();

                // Schedule rebuild after objects finish loading (2 second delay)
                _geometryRebuildTime = Time.time + 2.0f;
                _geometryRebuildPending = true;

                DebugLogger.Log($"[SteamAudioEnv] Area change to {mapNo}/{areaNo}, geometry rebuild in 2s");
            }

            // Check if rebuild is due
            if (_geometryRebuildPending && Time.time >= _geometryRebuildTime)
            {
                _geometryRebuildPending = false;
                RebuildGeometry();
            }
        }

        private static void RebuildGeometry()
        {
            if (!_initialized || _scene == IntPtr.Zero) return;

            try
            {
                var startTime = DateTime.Now;

                // Collect geometry from two sources:
                // 1. NavMesh triangulation (ground/floor surfaces)
                // 2. Renderer bounding boxes (walls/buildings/obstacles)
                var vertList = new List<IPLVector3>();
                var triList = new List<IPLTriangle>();
                var matIdxList = new List<int>();

                int navMeshTris = 0;
                int boxTris = 0;

                // --- Source 1: Ground plane from NavMesh sampling ---
                // CalculateTriangulation is stripped from Il2Cpp, so we sample
                // the NavMesh on a grid and build triangles from valid points.
                try
                {
                    // Sample the NavMesh on a grid around the origin of the current area
                    // Use a broad grid to capture the walkable ground surface
                    const float gridExtent = 200f;
                    const float gridStep = 10f;
                    int gridSize = (int)(gridExtent * 2 / gridStep) + 1;
                    var gridPoints = new float[gridSize, gridSize, 3]; // x,y,z
                    var gridValid = new bool[gridSize, gridSize];

                    // Find a reference Y from player position
                    float refY = 0f;
                    var playerCtrl = UnityEngine.Object.FindObjectOfType<Il2Cpp.PlayerCtrl>();
                    if (playerCtrl != null)
                        refY = playerCtrl.transform.position.y;

                    int validCount = 0;
                    for (int gx = 0; gx < gridSize; gx++)
                    {
                        for (int gz = 0; gz < gridSize; gz++)
                        {
                            float wx = -gridExtent + gx * gridStep;
                            float wz = -gridExtent + gz * gridStep;
                            var samplePos = new Vector3(wx, refY + 50f, wz);

                            NavMeshHit hit;
                            if (NavMesh.SamplePosition(samplePos, out hit, 60f, NavMesh.AllAreas))
                            {
                                gridPoints[gx, gz, 0] = hit.position.x;
                                gridPoints[gx, gz, 1] = hit.position.y;
                                gridPoints[gx, gz, 2] = hit.position.z;
                                gridValid[gx, gz] = true;
                                validCount++;
                            }
                        }
                    }

                    // Build triangles from adjacent valid grid cells
                    if (validCount >= 3)
                    {
                        // Add vertices
                        var gridVertIndex = new int[gridSize, gridSize];
                        for (int gx = 0; gx < gridSize; gx++)
                        {
                            for (int gz = 0; gz < gridSize; gz++)
                            {
                                if (gridValid[gx, gz])
                                {
                                    gridVertIndex[gx, gz] = vertList.Count;
                                    vertList.Add(new IPLVector3
                                    {
                                        x = gridPoints[gx, gz, 0],
                                        y = gridPoints[gx, gz, 1],
                                        z = -gridPoints[gx, gz, 2] // Unity→Steam Audio Z flip
                                    });
                                }
                            }
                        }

                        // Create triangles between adjacent valid points
                        for (int gx = 0; gx < gridSize - 1; gx++)
                        {
                            for (int gz = 0; gz < gridSize - 1; gz++)
                            {
                                if (triList.Count + 2 > MaxTriangles) break;

                                bool tl = gridValid[gx, gz];
                                bool tr = gridValid[gx + 1, gz];
                                bool bl = gridValid[gx, gz + 1];
                                bool br = gridValid[gx + 1, gz + 1];

                                // Two triangles per quad (if all 4 corners valid)
                                if (tl && tr && bl)
                                {
                                    triList.Add(new IPLTriangle
                                    {
                                        index0 = gridVertIndex[gx, gz],
                                        index1 = gridVertIndex[gx + 1, gz],
                                        index2 = gridVertIndex[gx, gz + 1]
                                    });
                                    matIdxList.Add(0);
                                }
                                if (tr && br && bl)
                                {
                                    triList.Add(new IPLTriangle
                                    {
                                        index0 = gridVertIndex[gx + 1, gz],
                                        index1 = gridVertIndex[gx + 1, gz + 1],
                                        index2 = gridVertIndex[gx, gz + 1]
                                    });
                                    matIdxList.Add(0);
                                }
                            }
                        }

                        navMeshTris = triList.Count;
                        DebugLogger.Log($"[SteamAudioEnv] NavMesh grid: {validCount} sample points, {navMeshTris} ground tris");
                    }
                    else
                    {
                        DebugLogger.Warning("[SteamAudioEnv] NavMesh sampling found too few valid points");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[SteamAudioEnv] NavMesh sampling failed: {ex.GetType().Name}: {ex.Message}");
                }

                // --- Source 2: Renderer bounding boxes ---
                try
                {
                    var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>();
                    int countFiltered = 0, countTiny = 0, countAdded = 0;

                    if (renderers != null)
                    {
                        foreach (var r in renderers)
                        {
                            try
                            {
                                if (r == null || !r.enabled || r.gameObject == null) continue;

                                // Skip UI, effects, particles, shadows
                                string name = r.gameObject.name;
                                if (name.Contains("UI") || name.Contains("Canvas") ||
                                    name.Contains("Effect") || name.Contains("Particle") ||
                                    name.Contains("Shadow") || name.Contains("particle") ||
                                    name.Contains("effect")) { countFiltered++; continue; }

                                var bounds = r.bounds;
                                var size = bounds.size;

                                // Skip tiny objects (< 0.5m in all dimensions)
                                if (size.x < 0.5f && size.y < 0.5f && size.z < 0.5f) { countTiny++; continue; }

                                // Budget check
                                if (triList.Count + 12 > MaxTriangles) break;

                                AddBoundingBox(vertList, triList, matIdxList, bounds.center, bounds.extents, 1);
                                countAdded++;
                                boxTris += 12;
                            }
                            catch { }
                        }
                    }

                    DebugLogger.Log($"[SteamAudioEnv] Bounding boxes: {countAdded} objects ({countFiltered} filtered, {countTiny} tiny)");
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[SteamAudioEnv] Renderer scan failed: {ex.GetType().Name}: {ex.Message}");
                }

                int totalVerts = vertList.Count;
                int totalTris = triList.Count;

                if (totalVerts == 0 || totalTris == 0)
                {
                    DebugLogger.Warning("[SteamAudioEnv] No valid geometry extracted");
                    return;
                }

                // Materials: 0 = ground (NavMesh), 1 = walls/buildings (bounding boxes)
                var materials = new IPLMaterial[]
                {
                    new IPLMaterial // Ground: moderate absorption, high scattering
                    {
                        absorptionLow = 0.10f, absorptionMid = 0.20f, absorptionHigh = 0.30f,
                        scattering = 0.50f,
                        transmissionLow = 0.05f, transmissionMid = 0.04f, transmissionHigh = 0.03f
                    },
                    new IPLMaterial // Walls/buildings: lower absorption, blocks more sound
                    {
                        absorptionLow = 0.05f, absorptionMid = 0.06f, absorptionHigh = 0.08f,
                        scattering = 0.70f,
                        transmissionLow = 0.02f, transmissionMid = 0.01f, transmissionHigh = 0.005f
                    }
                };

                // Convert to arrays and pin for native interop
                var vertices = vertList.ToArray();
                var triangles = triList.ToArray();
                var materialIndices = matIdxList.ToArray();

                var vertHandle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
                var triHandle = GCHandle.Alloc(triangles, GCHandleType.Pinned);
                var matIdxHandle = GCHandle.Alloc(materialIndices, GCHandleType.Pinned);
                var matHandle = GCHandle.Alloc(materials, GCHandleType.Pinned);

                try
                {
                    var meshSettings = new IPLStaticMeshSettings
                    {
                        numVertices = totalVerts,
                        numTriangles = totalTris,
                        numMaterials = 2,
                        vertices = vertHandle.AddrOfPinnedObject(),
                        triangles = triHandle.AddrOfPinnedObject(),
                        materialIndices = matIdxHandle.AddrOfPinnedObject(),
                        materials = matHandle.AddrOfPinnedObject()
                    };

                    var err = SteamAudioNative.iplStaticMeshCreate(_scene, ref meshSettings, out _staticMesh);
                    if (err != IPLerror.Success)
                    {
                        DebugLogger.Error($"[SteamAudioEnv] StaticMesh create failed: {err}");
                        return;
                    }

                    SteamAudioNative.iplStaticMeshAdd(_staticMesh, _scene);
                    SteamAudioNative.iplSceneCommit(_scene);

                    // Re-attach scene to simulator
                    SteamAudioNative.iplSimulatorSetScene(_simulator, _scene);
                    SteamAudioNative.iplSimulatorCommit(_simulator);

                    _geometryLoaded = true;

                    var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                    DebugLogger.Log($"[SteamAudioEnv] Geometry loaded: {totalVerts} verts, {totalTris} tris " +
                        $"(NavMesh: {navMeshTris}, boxes: {boxTris}) in {elapsed:F0}ms");
                }
                finally
                {
                    vertHandle.Free();
                    triHandle.Free();
                    matIdxHandle.Free();
                    matHandle.Free();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudioEnv] RebuildGeometry error: {ex.Message}");
            }
        }

        /// <summary>
        /// Add an axis-aligned bounding box as 12 triangles (6 faces, 2 tris each).
        /// </summary>
        private static void AddBoundingBox(List<IPLVector3> verts, List<IPLTriangle> tris,
            List<int> matIndices, Vector3 center, Vector3 extents, int materialIndex)
        {
            int baseIdx = verts.Count;

            // 8 corner vertices (convert to Steam Audio right-handed: negate Z)
            float cx = center.x, cy = center.y, cz = -center.z;
            float ex = extents.x, ey = extents.y, ez = extents.z;

            verts.Add(new IPLVector3 { x = cx - ex, y = cy - ey, z = cz - ez }); // 0: left-bottom-back
            verts.Add(new IPLVector3 { x = cx + ex, y = cy - ey, z = cz - ez }); // 1: right-bottom-back
            verts.Add(new IPLVector3 { x = cx + ex, y = cy + ey, z = cz - ez }); // 2: right-top-back
            verts.Add(new IPLVector3 { x = cx - ex, y = cy + ey, z = cz - ez }); // 3: left-top-back
            verts.Add(new IPLVector3 { x = cx - ex, y = cy - ey, z = cz + ez }); // 4: left-bottom-front
            verts.Add(new IPLVector3 { x = cx + ex, y = cy - ey, z = cz + ez }); // 5: right-bottom-front
            verts.Add(new IPLVector3 { x = cx + ex, y = cy + ey, z = cz + ez }); // 6: right-top-front
            verts.Add(new IPLVector3 { x = cx - ex, y = cy + ey, z = cz + ez }); // 7: left-top-front

            // 6 faces × 2 triangles = 12 triangles
            int[] boxIndices = new int[]
            {
                // Back face
                0, 2, 1,  0, 3, 2,
                // Front face
                4, 5, 6,  4, 6, 7,
                // Left face
                0, 4, 7,  0, 7, 3,
                // Right face
                1, 2, 6,  1, 6, 5,
                // Bottom face
                0, 1, 5,  0, 5, 4,
                // Top face
                3, 7, 6,  3, 6, 2
            };

            for (int i = 0; i < boxIndices.Length; i += 3)
            {
                tris.Add(new IPLTriangle
                {
                    index0 = baseIdx + boxIndices[i],
                    index1 = baseIdx + boxIndices[i + 1],
                    index2 = baseIdx + boxIndices[i + 2]
                });
                matIndices.Add(materialIndex);
            }
        }

        private static void ReleaseGeometry()
        {
            _geometryLoaded = false;

            if (_staticMesh != IntPtr.Zero && _scene != IntPtr.Zero)
            {
                try
                {
                    SteamAudioNative.iplStaticMeshRemove(_staticMesh, _scene);
                    SteamAudioNative.iplStaticMeshRelease(ref _staticMesh);
                    SteamAudioNative.iplSceneCommit(_scene);
                    SteamAudioNative.iplSimulatorCommit(_simulator);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"[SteamAudioEnv] ReleaseGeometry error: {ex.Message}");
                    _staticMesh = IntPtr.Zero;
                }
            }

        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void SetSourceInputs(SourceData data)
        {
            // Convert source position to Steam Audio coordinates
            var sourceCoords = new IPLCoordinateSpace3
            {
                right = new IPLVector3 { x = 1, y = 0, z = 0 },
                up = new IPLVector3 { x = 0, y = 1, z = 0 },
                ahead = new IPLVector3 { x = 0, y = 0, z = -1 },
                origin = new IPLVector3
                {
                    x = data.PositionX,
                    y = data.PositionY,
                    z = -data.PositionZ // Unity → Steam Audio Z flip
                }
            };

            var inputs = new IPLSimulationInputs
            {
                flags = IPLSimulationFlags.Direct,
                directFlags = IPLDirectSimulationFlags.DistanceAttenuation
                            | IPLDirectSimulationFlags.AirAbsorption
                            | IPLDirectSimulationFlags.Occlusion,
                source = sourceCoords,
                distanceAttenuationModel = new IPLDistanceAttenuationModel
                {
                    type = IPLDistanceAttenuationModelType.Default
                },
                airAbsorptionModel = new IPLAirAbsorptionModel
                {
                    type = IPLAirAbsorptionModelType.Default
                },
                directivity = new IPLDirectivity
                {
                    dipoleWeight = 0f,
                    dipolePower = 0f
                },
                occlusionType = IPLOcclusionType.Raycast,
                occlusionRadius = 1.0f,
                numOcclusionSamples = 16,
                reverbScale0 = 1.0f,
                reverbScale1 = 1.0f,
                reverbScale2 = 1.0f
            };

            SteamAudioNative.iplSourceSetInputs(data.Handle, IPLSimulationFlags.Direct, ref inputs);
        }

        private static IPLDirectEffectParams CreateDefaultDirectParams()
        {
            return new IPLDirectEffectParams
            {
                flags = IPLDirectEffectFlags.ApplyDistanceAttenuation
                      | IPLDirectEffectFlags.ApplyAirAbsorption
                      | IPLDirectEffectFlags.ApplyOcclusion,
                transmissionType = IPLTransmissionType.FrequencyIndependent,
                distanceAttenuation = 1.0f,
                airAbsorptionLow = 1.0f,
                airAbsorptionMid = 1.0f,
                airAbsorptionHigh = 1.0f,
                directivity = 1.0f,
                occlusion = 1.0f,
                transmissionLow = 1.0f,
                transmissionMid = 1.0f,
                transmissionHigh = 1.0f
            };
        }
    }
}
