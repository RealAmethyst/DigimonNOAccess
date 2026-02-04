using Il2Cpp;
using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using UnityEngine;
using UnityEngine.AI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides always-on audio navigation for blind players.
    /// Continuously tracks nearest objects with positional audio.
    /// No toggle keys, no speech - just audio cues.
    /// </summary>
    public class AudioNavigationHandler
    {
        // Detection ranges (in Unity units - roughly 1 unit = 1 step)
        private const float ItemRange = 100f;
        private const float NpcRange = 120f;
        private const float EnemyRange = 150f;
        private const float TransitionRange = 80f;
        private const float PartnerRange = 200f;

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

        // NAudio positional audio system
        private PositionalAudio _positionalAudio;
        private GameObject _trackedTarget;
        private string _trackedTargetType;

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

        // Cooldown after training result to allow evolution detection
        private const float PostTrainingCooldown = 0.5f;
        private float _lastTrainingResultSeenTime = 0f;

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                _positionalAudio = new PositionalAudio();

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
            if (!IsPlayerInControl())
            {
                if (_positionalAudio != null && _positionalAudio.IsPlaying)
                {
                    _positionalAudio.Stop();
                    _trackedTarget = null;
                }
                return;
            }

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

                    var (newTarget, newType, newSoundType, newDist) = FindNearestTarget(playerPos);

                    if (newTarget != null)
                    {
                        // Start or switch tracking
                        if (_trackedTarget != newTarget)
                        {
                            _trackedTarget = newTarget;
                            _trackedTargetType = newType;

                            if (_positionalAudio.IsPlaying)
                            {
                                _positionalAudio.ChangeSoundType(newSoundType, newDist + 10f);
                            }
                            else
                            {
                                _positionalAudio.UpdatePlayerPosition(
                                    playerPos.x, playerPos.y, playerPos.z,
                                    playerForward.x, playerForward.z
                                );
                                Vector3 targetPos = newTarget.transform.position;
                                _positionalAudio.UpdateTargetPosition(targetPos.x, targetPos.y, targetPos.z);
                                _positionalAudio.StartTracking(newSoundType, newDist + 10f);
                            }
                        }
                    }
                    else if (_positionalAudio.IsPlaying)
                    {
                        // No targets - stop
                        _positionalAudio.Stop();
                        _trackedTarget = null;
                    }
                }

                // Update positions if tracking
                if (_trackedTarget != null && _positionalAudio.IsPlaying)
                {
                    if (!_trackedTarget.activeInHierarchy)
                    {
                        _trackedTarget = null;
                        return;
                    }

                    _positionalAudio.UpdatePlayerPosition(
                        playerPos.x, playerPos.y, playerPos.z,
                        playerForward.x, playerForward.z
                    );

                    Vector3 targetPos = _trackedTarget.transform.position;
                    _positionalAudio.UpdateTargetPosition(targetPos.x, targetPos.y, targetPos.z);

                    // Check if reached target
                    float dist = _positionalAudio.GetCurrentDistance();
                    if (dist < 3f)
                    {
                        _trackedTarget = null;
                        // Will find new target on next scan
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Update error: {ex.Message}");
            }
        }

        private (GameObject target, string type, PositionalAudio.SoundType soundType, float distance) FindNearestTarget(Vector3 playerPos)
        {
            GameObject bestTarget = null;
            float bestDist = float.MaxValue;
            string bestType = "";
            PositionalAudio.SoundType bestSoundType = PositionalAudio.SoundType.Item;

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
                        if (dist < ItemRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            bestTarget = point.gameObject;
                            bestType = "Item";
                            bestSoundType = PositionalAudio.SoundType.Item;
                        }
                    }
                }
            }
            catch { }

            // Transitions
            try
            {
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
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
                        bestTarget = trigger.gameObject;
                        bestType = "Transition";
                        bestSoundType = PositionalAudio.SoundType.Transition;
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
                        if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, enemy.transform.position);
                        if (dist < EnemyRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            bestTarget = enemy.gameObject;
                            bestType = "Enemy";
                            bestSoundType = PositionalAudio.SoundType.Enemy;
                        }
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
                        if (dist < NpcRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            bestTarget = npc.gameObject;
                            bestType = "NPC";
                            bestSoundType = PositionalAudio.SoundType.NPC;
                        }
                    }
                }
            }
            catch { }

            // Partners (fallback)
            if (bestTarget == null)
            {
                try
                {
                    var partnerCtrls = UnityEngine.Object.FindObjectsOfType<PartnerCtrl>();
                    foreach (var partner in partnerCtrls)
                    {
                        if (partner == null || partner.gameObject == null || !partner.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, partner.transform.position);
                        if (dist < PartnerRange && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            bestTarget = partner.gameObject;
                            bestType = "Partner";
                            bestSoundType = PositionalAudio.SoundType.NPC;
                        }
                    }
                }
                catch { }
            }

            return (bestTarget, bestType, bestSoundType, bestDist);
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
                    _playerCtrl.transform.position + Vector3.up * 0.5f,
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
                // Check if game is paused
                if (GameStateService.IsGamePaused())
                    return false;

                // Check if in any battle phase (includes tutorial battles, battle dialogs, result screen)
                if (GameStateService.IsInBattlePhase())
                    return false;

                // Check if in death recovery (after losing battle)
                if (GameStateService.IsInDeathRecovery())
                    return false;

                // Check player action state (catches death recovery, events, etc.)
                if (GameStateService.IsPlayerInNonControllableState(_playerCtrl))
                    return false;

                // Check if any menu is open that blocks player control
                if (IsMenuOpen())
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

            if (_positionalAudio != null)
            {
                try
                {
                    _positionalAudio.Dispose();
                }
                catch { }
                _positionalAudio = null;
            }

            _trackedTarget = null;
        }
    }
}
