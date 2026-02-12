using Il2Cpp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using UnityEngine;
using UnityEngine.AI;

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
        private const float ItemRange = 100f;
        private const float NpcRange = 120f;
        private const float EnemyRange = 150f;
        private const float TransitionRange = 80f;

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

        // Per-type positional audio: each sound type gets its own audio instance and target
        private Dictionary<PositionalAudio.SoundType, PositionalAudio> _audioByType = new Dictionary<PositionalAudio.SoundType, PositionalAudio>();
        private Dictionary<PositionalAudio.SoundType, GameObject> _targetByType = new Dictionary<PositionalAudio.SoundType, GameObject>();

        // Wall detection configuration
        private const float WallDetectionDistance = 2f;
        private const float WallCheckInterval = 0.3f;
        private const float NavMeshSampleRadius = 0.5f;
        private float _lastWallCheckTime = 0f;

        // Wall states (to avoid repeating sounds)
        private bool _wallAhead = false;
        private bool _wallBehind = false;
        private bool _wallLeft = false;
        private bool _wallRight = false;

        // Sound file path
        private string _soundsPath;

        // Early enemy defeat detection
        private bool _wasInControl;
        private GameObject _defeatedEnemyObject;

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

                _initialized = true;
                DebugLogger.Log("[AudioNav] Initialized - always-on mode with wall detection");
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

            // When suspended (pathfinding beacon active), stop all audio and skip updates
            if (Suspended)
            {
                StopAllAudio();
                ResetWallStates();
                return;
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

            // Update wall detection
            UpdateWallDetection();
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

            // On return to control, check if we won a battle and mark the defeated enemy
            if (!_wasInControl)
            {
                _wasInControl = true;
                _defeatedEnemyObject = GameStateService.GetLastDefeatedEnemyObject();
            }

            // Clear defeated enemy once the game catches up and deactivates it
            if (_defeatedEnemyObject != null && !_defeatedEnemyObject.activeInHierarchy)
                _defeatedEnemyObject = null;

            if (_playerCtrl == null) return;

            try
            {
                Vector3 playerPos = _playerCtrl.transform.position;
                Vector3 playerForward = _playerCtrl.transform.forward;
                float currentTime = Time.time;

                // Rescan for targets periodically
                if (currentTime - _lastTrackingScan >= TrackingUpdateInterval)
                {
                    _lastTrackingScan = currentTime;

                    var targets = FindTargetsInRange(playerPos);

                    // Stop audio for types no longer in range
                    foreach (var type in _audioByType.Keys.ToList())
                    {
                        if (!targets.ContainsKey(type))
                        {
                            _audioByType[type].Stop();
                            _targetByType.Remove(type);
                        }
                    }

                    // Start or update audio for each type in range
                    foreach (var kvp in targets)
                    {
                        var type = kvp.Key;
                        var (target, dist) = kvp.Value;

                        // Create audio instance for this type if needed
                        if (!_audioByType.ContainsKey(type))
                            _audioByType[type] = new PositionalAudio();

                        var audio = _audioByType[type];

                        if (!_targetByType.ContainsKey(type) || _targetByType[type] != target)
                        {
                            // New or changed target for this type
                            _targetByType[type] = target;

                            if (audio.IsPlaying)
                            {
                                audio.ChangeSoundType(type, dist + 10f);
                            }
                            else
                            {
                                audio.UpdatePlayerPosition(
                                    playerPos.x, playerPos.y, playerPos.z,
                                    playerForward.x, playerForward.z);
                                Vector3 targetPos = target.transform.position;
                                audio.UpdateTargetPosition(targetPos.x, targetPos.y, targetPos.z);
                                audio.StartTracking(type, dist + 10f);
                            }
                        }
                    }
                }

                // Update positions for all active tracks
                foreach (var kvp in _targetByType)
                {
                    var type = kvp.Key;
                    var target = kvp.Value;

                    if (target == null || !target.activeInHierarchy)
                    {
                        if (_audioByType.ContainsKey(type))
                            _audioByType[type].Stop();
                        continue;
                    }

                    if (!_audioByType.ContainsKey(type) || !_audioByType[type].IsPlaying)
                        continue;

                    var audio = _audioByType[type];
                    audio.UpdatePlayerPosition(
                        playerPos.x, playerPos.y, playerPos.z,
                        playerForward.x, playerForward.z);

                    Vector3 tPos = target.transform.position;
                    audio.UpdateTargetPosition(tPos.x, tPos.y, tPos.z);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Find the closest target of each sound type within range.
        /// Returns one target per type - all types play simultaneously.
        /// </summary>
        private Dictionary<PositionalAudio.SoundType, (GameObject target, float distance)> FindTargetsInRange(Vector3 playerPos)
        {
            var results = new Dictionary<PositionalAudio.SoundType, (GameObject, float)>();

            // Items
            try
            {
                var itemManager = ItemPickPointManager.m_instance;
                if (itemManager != null && itemManager.m_itemPickPoints != null)
                {
                    GameObject best = null;
                    float bestDist = float.MaxValue;

                    foreach (var point in itemManager.m_itemPickPoints)
                    {
                        if (point == null || point.gameObject == null || !point.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, point.transform.position);
                        if (dist < ItemRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            best = point.gameObject;
                        }
                    }

                    if (best != null)
                        results[PositionalAudio.SoundType.Item] = (best, bestDist);
                }
            }
            catch { }

            // Transitions
            try
            {
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
                GameObject best = null;
                float bestDist = float.MaxValue;

                foreach (var trigger in mapTriggers)
                {
                    if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                        continue;

                    if (trigger.enterID != MapTriggerManager.EVENT.MapChange)
                        continue;

                    float dist = Vector3.Distance(playerPos, trigger.transform.position);
                    if (dist < TransitionRange && dist < bestDist && dist > 1f)
                    {
                        bestDist = dist;
                        best = trigger.gameObject;
                    }
                }

                if (best != null)
                    results[PositionalAudio.SoundType.Transition] = (best, bestDist);
            }
            catch { }

            // Enemies
            try
            {
                if (_enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
                {
                    GameObject best = null;
                    float bestDist = float.MaxValue;

                    foreach (var enemy in _enemyManager.m_EnemyCtrlArray)
                    {
                        if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
                            continue;
                        if (enemy.gameObject == _defeatedEnemyObject)
                            continue;

                        float dist = Vector3.Distance(playerPos, enemy.transform.position);
                        if (dist < EnemyRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            best = enemy.gameObject;
                        }
                    }

                    if (best != null)
                        results[PositionalAudio.SoundType.Enemy] = (best, bestDist);
                }
            }
            catch { }

            // NPCs
            try
            {
                if (_npcManager != null && _npcManager.m_NpcCtrlArray != null)
                {
                    GameObject best = null;
                    float bestDist = float.MaxValue;

                    foreach (var npc in _npcManager.m_NpcCtrlArray)
                    {
                        if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, npc.transform.position);
                        if (dist < NpcRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            best = npc.gameObject;
                        }
                    }

                    if (best != null)
                        results[PositionalAudio.SoundType.NPC] = (best, bestDist);
                }
            }
            catch { }

            return results;
        }

        private void StopAllAudio()
        {
            foreach (var audio in _audioByType.Values)
            {
                if (audio.IsPlaying)
                    audio.Stop();
            }
            _targetByType.Clear();
        }

        #region Wall Detection

        private void UpdateWallDetection()
        {
            // Stop if player is not in control
            if (!IsPlayerInControl())
            {
                ResetWallStates();
                return;
            }

            if (_playerCtrl == null) return;

            // Rate limit checking
            float currentTime = Time.time;
            if (currentTime - _lastWallCheckTime < WallCheckInterval) return;
            _lastWallCheckTime = currentTime;

            DetectWallsNavMesh();
        }

        private void DetectWallsNavMesh()
        {
            if (_playerCtrl == null) return;

            try
            {
                Vector3 playerPos = _playerCtrl.transform.position;
                Vector3 forward = _playerCtrl.transform.forward;
                Vector3 right = _playerCtrl.transform.right;

                bool wallAhead = !IsPositionWalkable(playerPos + forward * WallDetectionDistance);
                bool wallBehind = !IsPositionWalkable(playerPos - forward * WallDetectionDistance);
                bool wallLeft = !IsPositionWalkable(playerPos - right * WallDetectionDistance);
                bool wallRight = !IsPositionWalkable(playerPos + right * WallDetectionDistance);

                if (wallAhead && !_wallAhead)
                {
                    PlayWallSound("wall up.wav", 0f, 0.4f);
                }
                if (wallBehind && !_wallBehind)
                {
                    PlayWallSound("wall down.wav", 0f, 0.5f);
                }
                if (wallLeft && !_wallLeft)
                {
                    PlayWallSound("wall left.wav", -0.8f, 0.3f);
                }
                if (wallRight && !_wallRight)
                {
                    PlayWallSound("wall right.wav", 0.8f, 0.3f);
                }

                _wallAhead = wallAhead;
                _wallBehind = wallBehind;
                _wallLeft = wallLeft;
                _wallRight = wallRight;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Wall detection error: {ex.Message}");
            }
        }

        private bool IsPositionWalkable(Vector3 position)
        {
            try
            {
                NavMeshHit hit;
                bool foundNavMesh = NavMesh.SamplePosition(position, out hit, NavMeshSampleRadius, NavMesh.AllAreas);

                if (!foundNavMesh)
                {
                    return false;
                }

                float horizontalDistance = Vector2.Distance(
                    new Vector2(position.x, position.z),
                    new Vector2(hit.position.x, hit.position.z)
                );

                return horizontalDistance < NavMeshSampleRadius;
            }
            catch
            {
                // Fallback to raycast if NavMesh fails
                return !Physics.Raycast(
                    _playerCtrl.transform.position + UnityEngine.Vector3.up * 0.5f,
                    (position - _playerCtrl.transform.position).normalized,
                    WallDetectionDistance
                );
            }
        }

        private void ResetWallStates()
        {
            _wallAhead = false;
            _wallBehind = false;
            _wallLeft = false;
            _wallRight = false;
        }

        private void PlayWallSound(string filename, float pan, float volume = 0.5f)
        {
            try
            {
                string filePath = Path.Combine(_soundsPath, filename);
                if (!File.Exists(filePath)) return;

                var audioFile = new AudioFileReader(filePath);

                ISampleProvider sampleProvider;
                if (audioFile.WaveFormat.Channels == 2)
                {
                    sampleProvider = new StereoToMonoSampleProvider(audioFile);
                }
                else
                {
                    sampleProvider = audioFile;
                }

                var panner = new PanningSampleProvider(sampleProvider)
                {
                    Pan = pan
                };

                var volumeProvider = new VolumeSampleProvider(panner)
                {
                    Volume = volume
                };

                var waveOut = new WaveOutEvent();
                waveOut.Init(volumeProvider);

                waveOut.PlaybackStopped += (sender, args) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        audioFile.Dispose();
                    }
                    catch { }
                };

                waveOut.Play();
            }
            catch { }
        }

        #endregion

        private bool IsPlayerInControl()
        {
            try
            {
                // The player's actionState is the game's own check for whether
                // the player can move. Idle = walking around, everything else
                // (Event, Battle, Care, Dead, etc.) = not in control.
                if (_playerCtrl == null) return false;
                if (_playerCtrl.actionState != UnitCtrlBase.ActionState.ActionState_Idle)
                    return false;

                // Pause is a system-level freeze that doesn't change actionState
                if (GameStateService.IsGamePaused())
                    return false;

                return true;
            }
            catch
            {
                return false;
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

            foreach (var audio in _audioByType.Values)
            {
                try { audio.Dispose(); } catch { }
            }
            _audioByType.Clear();
            _targetByType.Clear();
        }
    }
}
