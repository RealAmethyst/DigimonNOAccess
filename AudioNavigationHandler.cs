using Il2Cpp;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides always-on audio navigation for blind players.
    /// Plays simultaneous positional audio for each object type in range
    /// (items, NPCs, enemies, transitions). No toggle keys, no speech - just audio cues.
    /// </summary>
    public class AudioNavigationHandler : IAccessibilityHandler
    {
        public int Priority => 999;

        /// <summary>
        /// When true, all positional audio and wall detection is suspended.
        /// Used by PathfindingBeacon to silence other navigation sounds during pathfinding.
        /// </summary>
        public static bool Suspended { get; set; }

        /// <summary>
        /// Background handler - never owns the status announce.
        /// </summary>
        public bool IsOpen() => false;

        /// <summary>
        /// Background handler - never announces status.
        /// </summary>
        public void AnnounceStatus() { }

        // Detection ranges (in Unity units - roughly 1 unit = 1 step)
        private const float ItemRange = 80f;
        private const float NpcRange = 80f;
        private const float EnemyRange = 100f;
        private const float TransitionRange = 60f;
        private const float FacilityRange = 80f;

        // Max simultaneous sounds per type (nearest N only, rest are silent)
        private const int MaxItemSounds = 3;
        private const int MaxNpcSounds = 3;
        private const int MaxEnemySounds = 3;
        private const int MaxTransitionSounds = 2;
        private const int MaxFacilitySounds = 2;

        // Volume levels (base)
        private const float NearestVolume = 0.8f;
        private const float BackgroundVolume = 0.15f;

        // Per-type volume multipliers (applied on top of nearest/background)
        // Enemy and NPC: -4dB (~0.63x), Transition: +2dB (~1.26x)
        private const float EnemyVolumeMultiplier = 0.63f;
        private const float NpcVolumeMultiplier = 0.63f;
        private const float TransitionVolumeMultiplier = 1.26f;

        // Tracking settings
        private const float TrackingUpdateInterval = 0.5f;
        private float _lastTrackingScan = 0f;

        // State
        private bool _initialized = false;

        // Cached references
        private PlayerCtrl _playerCtrl;
        private NpcManager _npcManager;
        private EnemyManager _enemyManager;
        private float _lastSearchTime = 0f;

        // Per-target positional audio: every target in range gets its own audio
        private Dictionary<GameObject, (PositionalAudio audio, PositionalAudio.SoundType type)> _navAudio = new Dictionary<GameObject, (PositionalAudio, PositionalAudio.SoundType)>();

        // Compass direction (north angle per area, used for direction announcements)
        private float _currentNorthAngle = 0f;

        // Sound file path
        private string _soundsPath;
        private bool _facilityWavExists = false;

        // Track control state for refresh on return
        private bool _wasInControl;

        // Map/area change detection for immediate rescan
        private int _lastMapNo = -1;
        private int _lastAreaNo = -1;

        // Camera mode tracking for diagnostic logging
        private int _lastLoggedCameraMode = -99;

        // Cooldown after training result to allow evolution detection
        private const float PostTrainingCooldown = 0.5f;
        private float _lastTrainingResultSeenTime = 0f;

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                // Set up sounds path for wall detection
                string modPath = Path.GetDirectoryName(typeof(AudioNavigationHandler).Assembly.Location);
                _soundsPath = Path.Combine(Path.GetDirectoryName(modPath), "sounds");

                if (!Directory.Exists(_soundsPath))
                {
                    _soundsPath = Path.Combine(modPath, "sounds");
                }

                // Check if facility.wav exists (no tone fallback for this type)
                _facilityWavExists = File.Exists(Path.Combine(_soundsPath, "facility.wav"));
                if (_facilityWavExists)
                    DebugLogger.Log("[AudioNav] facility.wav found - facility/fishing/toilet audio enabled");

                _initialized = true;
                DebugLogger.Log("[AudioNav] Initialized - always-on audio navigation");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Initialize error: {ex.Message}");
            }
        }

        public void Update()
        {
            if (!_initialized)
            {
                Initialize();
            }

            // Find managers periodically
            float currentTime = Time.time;
            if (_playerCtrl == null || currentTime - _lastSearchTime > 2f)
            {
                _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                _npcManager = UnityEngine.Object.FindObjectOfType<NpcManager>();
                _enemyManager = UnityEngine.Object.FindObjectOfType<EnemyManager>();
                _lastSearchTime = currentTime;
            }

            // Update object tracking
            UpdatePositionalAudioTracking();

            // Drive environmental audio simulation (occlusion)
            UpdateEnvironmentSimulation();
        }

        private void UpdatePositionalAudioTracking()
        {
            // Stop if player is not in control
            bool inControl = IsPlayerInControl();
            if (!inControl)
            {
                _wasInControl = false;
                StopAllAudio();
                return;
            }

            // On return to control, refresh managers and force rescan.
            // This handles map/area transitions where old references became stale.
            if (!_wasInControl)
            {
                _wasInControl = true;

                // Force immediate manager refresh so FindAllTargetsInRange uses valid references
                _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                _npcManager = UnityEngine.Object.FindObjectOfType<NpcManager>();
                _enemyManager = UnityEngine.Object.FindObjectOfType<EnemyManager>();
                _lastSearchTime = Time.time;
                _lastTrackingScan = 0f; // Force immediate rescan
            }

            if (_playerCtrl == null) return;

            try
            {
                Vector3 playerPos = _playerCtrl.transform.position;

                // Use full camera orientation for audio (matching the game's AudioListener).
                // Includes pitch and zoom so HRTF matches what the player perceives.
                GetCameraVectors(out Vector3 camFwd, out Vector3 camUp);
                float currentTime = Time.time;

                // Detect map/area change and force immediate rescan
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    int mapNo = mgm.mapNo;
                    int areaNo = mgm.areaNo;
                    if (mapNo != _lastMapNo || areaNo != _lastAreaNo)
                    {
                        if (_lastMapNo >= 0)
                        {
                            StopAllAudio();
                            DebugLogger.Log($"[AudioNav] Area changed ({_lastMapNo}/{_lastAreaNo} -> {mapNo}/{areaNo}), refreshing managers");

                            // Force immediate manager refresh for the new scene
                            _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                            _npcManager = UnityEngine.Object.FindObjectOfType<NpcManager>();
                            _enemyManager = UnityEngine.Object.FindObjectOfType<EnemyManager>();
                            _lastSearchTime = Time.time;
                        }
                        _lastMapNo = mapNo;
                        _lastAreaNo = areaNo;
                        _lastTrackingScan = 0f; // Force immediate rescan

                        // Update north angle for compass direction announcements
                        _currentNorthAngle = GetNorthAngle(mapNo, areaNo);
                    }
                }

                // Rescan for targets periodically
                if (currentTime - _lastTrackingScan >= TrackingUpdateInterval)
                {
                    _lastTrackingScan = currentTime;

                    var targets = FindAllTargetsInRange(playerPos);
                    var currentTargets = new HashSet<GameObject>();

                    foreach (var (target, type, dist) in targets)
                    {
                        currentTargets.Add(target);

                        if (!_navAudio.ContainsKey(target))
                        {
                            // New target - create audio and start tracking
                            var audio = new PositionalAudio();
                            audio.UpdatePlayerPosition(
                                playerPos.x, playerPos.y, playerPos.z,
                                camFwd.x, camFwd.y, camFwd.z,
                                camUp.x, camUp.y, camUp.z);
                            Vector3 targetPos = target.transform.position;
                            audio.UpdateTargetPosition(targetPos.x, targetPos.y, targetPos.z);
                            audio.StartTracking(type, dist + 10f);
                            _navAudio[target] = (audio, type);
                        }
                    }

                    // Remove targets no longer in range
                    var toRemove = new List<GameObject>();
                    foreach (var kvp in _navAudio)
                    {
                        if (!currentTargets.Contains(kvp.Key))
                            toRemove.Add(kvp.Key);
                    }
                    foreach (var obj in toRemove)
                    {
                        _navAudio[obj].audio.Dispose();
                        _navAudio.Remove(obj);
                    }
                }

                // Update positions and collect distance info per type
                var toClean = new List<GameObject>();
                var distByType = new Dictionary<PositionalAudio.SoundType, List<(GameObject obj, float dist)>>();

                foreach (var kvp in _navAudio)
                {
                    var target = kvp.Key;
                    var (audio, type) = kvp.Value;

                    if (target == null || !target.activeInHierarchy)
                    {
                        audio.Stop();
                        toClean.Add(target);
                        continue;
                    }

                    if (!audio.IsPlaying) continue;

                    audio.UpdatePlayerPosition(
                        playerPos.x, playerPos.y, playerPos.z,
                        camFwd.x, camFwd.y, camFwd.z,
                        camUp.x, camUp.y, camUp.z);

                    Vector3 tPos = target.transform.position;
                    audio.UpdateTargetPosition(tPos.x, tPos.y, tPos.z);

                    float dist = Vector3.Distance(playerPos, tPos);
                    if (!distByType.ContainsKey(type))
                        distByType[type] = new List<(GameObject, float)>();
                    distByType[type].Add((target, dist));
                }

                // For each type: sort by distance, keep nearest N audible, silence the rest
                foreach (var kv in distByType)
                {
                    var type = kv.Key;
                    var list = kv.Value;
                    list.Sort((a, b) => a.dist.CompareTo(b.dist));

                    int maxSounds = type switch
                    {
                        PositionalAudio.SoundType.Item => MaxItemSounds,
                        PositionalAudio.SoundType.NPC => MaxNpcSounds,
                        PositionalAudio.SoundType.Enemy => MaxEnemySounds,
                        PositionalAudio.SoundType.Transition => MaxTransitionSounds,
                        PositionalAudio.SoundType.Facility => MaxFacilitySounds,
                        _ => 3
                    };

                    // Per-type volume multiplier
                    float typeMultiplier = type switch
                    {
                        PositionalAudio.SoundType.Enemy => EnemyVolumeMultiplier,
                        PositionalAudio.SoundType.NPC => NpcVolumeMultiplier,
                        PositionalAudio.SoundType.Transition => TransitionVolumeMultiplier,
                        _ => 1.0f
                    };

                    for (int i = 0; i < list.Count; i++)
                    {
                        var audio = _navAudio[list[i].obj].audio;
                        if (i >= maxSounds)
                        {
                            // Beyond limit - silence
                            audio.SetMaxVolume(0f);
                        }
                        else if (i == 0)
                        {
                            // Nearest of this type - full volume with type scaling
                            audio.SetMaxVolume(NearestVolume * typeMultiplier);
                        }
                        else
                        {
                            // Other audible sources - reduced volume with type scaling
                            audio.SetMaxVolume(BackgroundVolume * typeMultiplier);
                        }
                    }
                }

                // Clean up dead targets found during position updates
                foreach (var obj in toClean)
                {
                    if (_navAudio.ContainsKey(obj))
                    {
                        _navAudio[obj].audio.Dispose();
                        _navAudio.Remove(obj);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Find all targets of every sound type within range.
        /// Each target gets its own positional audio so you hear everything around you.
        /// </summary>
        private List<(GameObject target, PositionalAudio.SoundType type, float distance)> FindAllTargetsInRange(Vector3 playerPos)
        {
            var results = new List<(GameObject, PositionalAudio.SoundType, float)>();

            // Items
            try
            {
                var itemManager = ItemPickPointManager.m_instance;
                if (itemManager != null && itemManager.m_itemPickPoints != null)
                {
                    foreach (var point in itemManager.m_itemPickPoints)
                    {
                        if (point == null || point.gameObject == null || !point.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, point.transform.position);
                        if (dist < ItemRange && dist > 1f)
                            results.Add((point.gameObject, PositionalAudio.SoundType.Item, dist));
                    }
                }
            }
            catch { }

            // Transitions + Fishing spots + Toilets (all are MapTriggerScript subtypes)
            try
            {
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
                foreach (var trigger in mapTriggers)
                {
                    if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                        continue;

                    if (trigger.enterID == MapTriggerManager.EVENT.MapChange)
                    {
                        float dist = Vector3.Distance(playerPos, trigger.transform.position);
                        if (dist < TransitionRange && dist > 1f)
                            results.Add((trigger.gameObject, PositionalAudio.SoundType.Transition, dist));
                    }
                    else if (_facilityWavExists &&
                             (trigger.enterID == MapTriggerManager.EVENT.Fishing ||
                              trigger.enterID == MapTriggerManager.EVENT.Toilet))
                    {
                        float dist = Vector3.Distance(playerPos, trigger.transform.position);
                        if (dist < FacilityRange && dist > 1f)
                            results.Add((trigger.gameObject, PositionalAudio.SoundType.Facility, dist));
                    }
                }
            }
            catch { }

            // Enemies
            try
            {
                if (_enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
                {
                    foreach (var enemy in _enemyManager.m_EnemyCtrlArray)
                    {
                        if (!GameStateService.IsEnemyAlive(enemy))
                            continue;

                        float dist = Vector3.Distance(playerPos, enemy.transform.position);
                        if (dist < EnemyRange && dist > 1f)
                            results.Add((enemy.gameObject, PositionalAudio.SoundType.Enemy, dist));
                    }
                }
            }
            catch { }

            // NPCs
            try
            {
                if (_npcManager != null && _npcManager.m_NpcCtrlArray != null)
                {
                    foreach (var npc in _npcManager.m_NpcCtrlArray)
                    {
                        if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, npc.transform.position);
                        if (dist < NpcRange && dist > 1f)
                            results.Add((npc.gameObject, PositionalAudio.SoundType.NPC, dist));
                    }
                }
            }
            catch { }

            // Facilities (training, shops, restaurants, storage, etc.)
            if (_facilityWavExists)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    var eventTriggerMgr = mgm?.m_EventTriggerMgr;
                    if (eventTriggerMgr != null)
                    {
                        var triggerDict = eventTriggerMgr.m_TriggerDictionary;
                        var placementData = eventTriggerMgr.m_CsvbPlacementData;

                        if (triggerDict != null && placementData != null)
                        {
                            var enumerator = triggerDict.GetEnumerator();
                            while (enumerator.MoveNext())
                            {
                                var trigger = enumerator.Current.Value;
                                if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                                    continue;

                                try
                                {
                                    uint placementId = enumerator.Current.Key;
                                    var npcData = HashIdSearchClass<ParameterPlacementNpc>.GetParam(placementData, placementId);
                                    if (npcData == null) continue;

                                    var facilityType = (MainGameManager.Facility)npcData.m_Facility;
                                    if (facilityType == MainGameManager.Facility.None) continue;

                                    float dist = Vector3.Distance(playerPos, trigger.transform.position);
                                    if (dist < FacilityRange && dist > 1f)
                                        results.Add((trigger.gameObject, PositionalAudio.SoundType.Facility, dist));
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }
            }

            return results;
        }

        private void StopAllAudio()
        {
            foreach (var kvp in _navAudio)
            {
                try { kvp.Value.audio.Dispose(); } catch { }
            }
            _navAudio.Clear();
        }

        /// <summary>
        /// Get the north angle for the current map area from ParameterDigiviceAreaData.
        /// Returns degrees (0 = +Z is north).
        /// </summary>
        private float GetNorthAngle(int mapNo, int areaNo)
        {
            try
            {
                var areaData = ParameterDigiviceAreaData.GetParam((AppInfo.MAP)mapNo, areaNo);
                if (areaData != null)
                {
                    float angle = areaData.m_fieldNorthAngle;
                    DebugLogger.Log($"[AudioNav] North angle for map {mapNo} area {areaNo}: {angle:F1}°");
                    return angle;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"[AudioNav] GetNorthAngle error: {ex.Message}");
            }

            DebugLogger.Warning($"[AudioNav] No north angle for map {mapNo} area {areaNo}, defaulting to 0");
            return 0f;
        }

        /// <summary>
        /// Get the compass direction the camera is currently facing,
        /// relative to the map's north angle.
        /// </summary>
        public string GetCameraCompassDirection()
        {
            try
            {
                GetCameraVectors(out Vector3 camFwd, out Vector3 camUp);

                // Project camera forward to horizontal plane
                float fwdX = camFwd.x;
                float fwdZ = camFwd.z;
                float len = (float)Math.Sqrt(fwdX * fwdX + fwdZ * fwdZ);
                if (len < 0.001f) return "unknown";

                // World angle from +Z axis
                float worldAngleDeg = (float)Math.Atan2(fwdX, fwdZ) * (180f / (float)Math.PI);

                // Subtract north angle to get compass bearing
                float compassDeg = worldAngleDeg - _currentNorthAngle;
                compassDeg = ((compassDeg % 360f) + 360f) % 360f;

                // 8-direction compass with 45-degree sectors
                // N: 337.5-22.5, NE: 22.5-67.5, E: 67.5-112.5, etc.
                if (compassDeg >= 337.5f || compassDeg < 22.5f) return "North";
                if (compassDeg < 67.5f) return "Northeast";
                if (compassDeg < 112.5f) return "East";
                if (compassDeg < 157.5f) return "Southeast";
                if (compassDeg < 202.5f) return "South";
                if (compassDeg < 247.5f) return "Southwest";
                if (compassDeg < 292.5f) return "West";
                return "Northwest";
            }
            catch
            {
                return "unknown";
            }
        }

        private void UpdateEnvironmentSimulation()
        {
            if (!SteamAudioEnvironment.IsInitialized) return;

            try
            {
                // Check for area change and rebuild geometry
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    SteamAudioEnvironment.CheckAreaChange(mgm.mapNo, mgm.areaNo);
                }

                // Update listener position for simulation
                if (_playerCtrl != null)
                {
                    Vector3 pos = _playerCtrl.transform.position;
                    GetCameraVectors(out Vector3 envFwd, out Vector3 envUp);
                    SteamAudioEnvironment.UpdateListener(pos, envFwd, envUp);
                }

                // Run direct simulation for all registered sources
                SteamAudioEnvironment.RunDirectSimulation();
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[AudioNav] Env simulation error: {ex.Message}");
            }
        }

        private bool IsPlayerInControl()
        {
            try
            {
                if (_playerCtrl == null) return false;

                // actionState must be Idle (walking around). Everything else
                // (Event, Battle, Care, Dead, etc.) = not in control.
                if (_playerCtrl.actionState != UnitCtrlBase.ActionState.ActionState_Idle)
                    return false;

                // Game step may still be Battle while actionState is already Idle
                // (e.g. death recovery transition before teleport to town)
                if (GameStateService.IsInBattlePhase())
                    return false;

                // Pause is a system-level freeze that doesn't change actionState
                if (GameStateService.IsGamePaused())
                    return false;

                // Evolution keeps actionState as Idle but disables field objects.
                // IsMenuOpen() checks EvolutionBase, training result, digivice, etc.
                if (GameStateService.IsMenuOpen())
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the camera's full 3D orientation vectors for audio spatialization.
        /// Uses CameraManager.Ref (same object that holds the game's AudioListener).
        /// Returns forward and up vectors without any horizontal projection -
        /// HRTF needs the real camera orientation including pitch and zoom.
        /// </summary>
        private void GetCameraVectors(out Vector3 camForward, out Vector3 camUp)
        {
            camForward = Vector3.forward;
            camUp = Vector3.up;

            try
            {
                var camMgr = CameraManager.Ref;
                if (camMgr != null && camMgr.m_mainCameraObject != null)
                {
                    var t = camMgr.m_mainCameraObject.transform;
                    camForward = t.forward;
                    camUp = t.up;

                    // Log camera mode changes for diagnostics
                    int currentMode = (int)camMgr.modeID;
                    if (currentMode != _lastLoggedCameraMode)
                    {
                        _lastLoggedCameraMode = currentMode;
                        DebugLogger.Log($"[AudioNav] Camera mode: {camMgr.modeID}, fwd: ({camForward.x:F2}, {camForward.y:F2}, {camForward.z:F2}), up: ({camUp.x:F2}, {camUp.y:F2}, {camUp.z:F2})");
                    }
                }
                else
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        camForward = cam.transform.forward;
                        camUp = cam.transform.up;
                    }
                }
            }
            catch
            {
                try
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        camForward = cam.transform.forward;
                        camUp = cam.transform.up;
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Check if any menu is open. Delegates to GameStateService.IsMenuOpen()
        /// but adds AudioNav-specific training result cooldown logic.
        /// </summary>
        private bool IsMenuOpen()
        {
            // Check the standard menu open states
            if (GameStateService.IsMenuOpen())
            {
                // Track when we last saw the training result panel (for cooldown)
                var resultPanel = UnityEngine.Object.FindObjectOfType<uTrainingPanelResult>();
                if (resultPanel != null && resultPanel.gameObject.activeInHierarchy)
                {
                    _lastTrainingResultSeenTime = Time.time;
                }
                return true;
            }

            // Cooldown period after training result closes
            // This allows evolution to start before audio nav kicks in
            if (Time.time - _lastTrainingResultSeenTime < PostTrainingCooldown)
                return true;

            return false;
        }

        public void Cleanup()
        {
            _initialized = false;
            StopAllAudio();
        }
    }
}
