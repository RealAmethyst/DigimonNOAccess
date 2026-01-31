using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides audio navigation for blind players.
    /// F3 = Toggle navigation sounds
    /// F4 = Test beep (uses game's CRI audio system)
    /// F5 = Announce nearest objects
    /// F6 = Toggle 3D positional audio tracking
    /// </summary>
    public class AudioNavigationHandler
    {
        // Configuration - ranges for different object types
        private const float ItemRange = 20f;
        private const float NpcRange = 25f;
        private const float EnemyRange = 30f;

        // State
        private bool _enabled = false;
        private bool _initialized = false;

        // Cached references
        private PlayerCtrl _playerCtrl;
        private NpcManager _npcManager;
        private EnemyManager _enemyManager;
        private float _lastSearchTime = 0f;

        // Sound test state
        private int _testSoundIndex = 0;

        // NAudio positional audio system
        private PositionalAudio _positionalAudio;
        private GameObject _trackedTarget;
        private string _trackedTargetType;

        // Legacy CRI 3D Audio (kept for reference)
        private GameObject _3dSoundObj = null;
        private SoundSource _3dSoundSource = null;

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                DebugLogger.Log("[AudioNav] === Initializing Audio Navigation ===");

                // Initialize NAudio positional audio system
                _positionalAudio = new PositionalAudio();
                DebugLogger.Log("[AudioNav] NAudio positional audio system initialized");

                _initialized = true;
                DebugLogger.Log("[AudioNav] Initialization complete");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Initialize error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void Update()
        {
            // F4 = Test beep using game's CRI audio system
            if (Input.GetKeyDown(KeyCode.F4))
            {
                PlayTestSound();
            }

            // F5 = Announce nearest objects
            if (Input.GetKeyDown(KeyCode.F5))
            {
                AnnounceNearbyObjects();
            }

            // F6 = Toggle 3D positional audio tracking
            if (Input.GetKeyDown(KeyCode.F6))
            {
                Toggle3DAudioTracking();
            }

            // F3 = Toggle audio navigation
            if (Input.GetKeyDown(KeyCode.F3))
            {
                Toggle();
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

            // Update positional audio tracking if active
            UpdatePositionalAudioTracking();
        }

        /// <summary>
        /// Update the positional audio with current player and target positions
        /// </summary>
        private void UpdatePositionalAudioTracking()
        {
            if (_positionalAudio == null || !_positionalAudio.IsPlaying)
                return;

            if (_playerCtrl == null || _trackedTarget == null)
            {
                // Lost tracking - stop audio
                _positionalAudio.Stop();
                _trackedTarget = null;
                return;
            }

            try
            {
                // Check if target is still valid
                if (!_trackedTarget.activeInHierarchy)
                {
                    _positionalAudio.Stop();
                    ScreenReader.Say($"{_trackedTargetType} lost");
                    _trackedTarget = null;
                    return;
                }

                // Update player position and forward direction
                Vector3 playerPos = _playerCtrl.transform.position;
                Vector3 playerForward = _playerCtrl.transform.forward;
                _positionalAudio.UpdatePlayerPosition(
                    playerPos.x, playerPos.y, playerPos.z,
                    playerForward.x, playerForward.z
                );

                // Update target position
                Vector3 targetPos = _trackedTarget.transform.position;
                _positionalAudio.UpdateTargetPosition(targetPos.x, targetPos.y, targetPos.z);

                // Check if reached target (within 2 meters)
                float dist = _positionalAudio.GetCurrentDistance();
                if (dist < 2f)
                {
                    _positionalAudio.Stop();
                    ScreenReader.Say($"Reached {_trackedTargetType}");
                    _trackedTarget = null;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Position update error: {ex.Message}");
            }
        }

        private void PlayTestSound()
        {
            DebugLogger.Log("[AudioNav] === Playing Test Sound ===");

            if (!_initialized)
            {
                Initialize();
            }

            try
            {
                // All 27 sound effects from CriSoundManager
                string cueName;
                string displayName;
                const int totalSounds = 27;

                switch (_testSoundIndex)
                {
                    case 0:
                        cueName = CriSoundManager.SE_OpenWindow1;
                        displayName = "Open Window";
                        break;
                    case 1:
                        cueName = CriSoundManager.SE_CloseWindow1;
                        displayName = "Close Window";
                        break;
                    case 2:
                        cueName = CriSoundManager.SE_MoveCursor1;
                        displayName = "Move Cursor";
                        break;
                    case 3:
                        cueName = CriSoundManager.SE_OK;
                        displayName = "OK";
                        break;
                    case 4:
                        cueName = CriSoundManager.SE_Cancel;
                        displayName = "Cancel";
                        break;
                    case 5:
                        cueName = CriSoundManager.SE_Error;
                        displayName = "Error";
                        break;
                    case 6:
                        cueName = CriSoundManager.SE_ItemSort;
                        displayName = "Item Sort";
                        break;
                    case 7:
                        cueName = CriSoundManager.SE_TargetChange;
                        displayName = "Target Change";
                        break;
                    case 8:
                        cueName = CriSoundManager.SE_BattleStart;
                        displayName = "Battle Start";
                        break;
                    case 9:
                        cueName = CriSoundManager.SE_ZoneStart;
                        displayName = "Zone Start";
                        break;
                    case 10:
                        cueName = CriSoundManager.SE_HITItem;
                        displayName = "Hit Item";
                        break;
                    case 11:
                        cueName = CriSoundManager.SE_forcefulnessUp;
                        displayName = "Forcefulness Up";
                        break;
                    case 12:
                        cueName = CriSoundManager.SE_RobustnessUp;
                        displayName = "Robustness Up";
                        break;
                    case 13:
                        cueName = CriSoundManager.SE_ClevernessUp;
                        displayName = "Cleverness Up";
                        break;
                    case 14:
                        cueName = CriSoundManager.SE_RapidityUp;
                        displayName = "Rapidity Up";
                        break;
                    case 15:
                        cueName = CriSoundManager.SE_PowerUp;
                        displayName = "Power Up";
                        break;
                    case 16:
                        cueName = CriSoundManager.SE_RecoveryItem;
                        displayName = "Recovery Item";
                        break;
                    case 17:
                        cueName = CriSoundManager.SE_Resurrection;
                        displayName = "Resurrection";
                        break;
                    case 18:
                        cueName = CriSoundManager.SE_RecoveryHp;
                        displayName = "Recovery HP";
                        break;
                    case 19:
                        cueName = CriSoundManager.SE_RecoveryMp;
                        displayName = "Recovery MP";
                        break;
                    case 20:
                        cueName = CriSoundManager.SE_Recovery;
                        displayName = "Recovery";
                        break;
                    case 21:
                        cueName = CriSoundManager.SE_Poison;
                        displayName = "Poison";
                        break;
                    case 22:
                        cueName = CriSoundManager.SE_Numbness;
                        displayName = "Numbness";
                        break;
                    case 23:
                        cueName = CriSoundManager.SE_Slow;
                        displayName = "Slow";
                        break;
                    case 24:
                        cueName = CriSoundManager.SE_Confusion;
                        displayName = "Confusion";
                        break;
                    case 25:
                        cueName = CriSoundManager.SE_LiquidCrystal;
                        displayName = "Liquid Crystal";
                        break;
                    case 26:
                        cueName = CriSoundManager.SE_Anger;
                        displayName = "Anger";
                        break;
                    default:
                        cueName = CriSoundManager.SE_OK;
                        displayName = "OK";
                        break;
                }

                DebugLogger.Log($"[AudioNav] Playing CRI sound: {displayName} (cue: {cueName})");

                // Use the game's CRI audio system
                CriSoundManager.PlayCommonSe(cueName);

                ScreenReader.Say($"{_testSoundIndex + 1} of {totalSounds}: {displayName}");

                // Move to next sound for next test
                _testSoundIndex = (_testSoundIndex + 1) % totalSounds;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] PlayTestSound error: {ex.Message}\n{ex.StackTrace}");
                ScreenReader.Say("Audio test failed - check debug log");
            }
        }

        private void AnnounceNearbyObjects()
        {
            DebugLogger.Log("[AudioNav] === Announcing Nearby Objects ===");

            if (!_initialized)
            {
                Initialize();
            }

            // Find player
            if (_playerCtrl == null)
            {
                _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
            }

            if (_playerCtrl == null)
            {
                ScreenReader.Say("Cannot find player position");
                return;
            }

            Vector3 playerPos = _playerCtrl.transform.position;
            DebugLogger.Log($"[AudioNav] Player at: ({playerPos.x:F1}, {playerPos.y:F1}, {playerPos.z:F1})");

            List<string> announcements = new List<string>();

            // Check for items
            try
            {
                var itemManager = ItemPickPointManager.m_instance;
                if (itemManager != null && itemManager.m_itemPickPoints != null)
                {
                    float nearestItemDist = float.MaxValue;
                    string nearestItemName = null;

                    foreach (var point in itemManager.m_itemPickPoints)
                    {
                        if (point == null || point.gameObject == null || !point.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, point.transform.position);
                        if (dist < ItemRange && dist < nearestItemDist)
                        {
                            nearestItemDist = dist;
                            nearestItemName = "Item";
                        }
                    }

                    if (nearestItemName != null)
                    {
                        string direction = GetDirection(playerPos, _playerCtrl.transform.forward,
                            itemManager.m_itemPickPoints[0].transform.position);
                        announcements.Add($"{nearestItemName} {nearestItemDist:F0} meters {direction}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Item scan error: {ex.Message}");
            }

            // Check for NPCs
            try
            {
                if (_npcManager == null)
                    _npcManager = UnityEngine.Object.FindObjectOfType<NpcManager>();

                if (_npcManager != null && _npcManager.m_NpcCtrlArray != null)
                {
                    float nearestNpcDist = float.MaxValue;
                    string nearestNpcName = null;
                    Vector3 nearestNpcPos = Vector3.zero;

                    foreach (var npc in _npcManager.m_NpcCtrlArray)
                    {
                        if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, npc.transform.position);
                        if (dist < NpcRange && dist < nearestNpcDist)
                        {
                            nearestNpcDist = dist;
                            nearestNpcName = "NPC";
                            nearestNpcPos = npc.transform.position;
                        }
                    }

                    if (nearestNpcName != null)
                    {
                        string direction = GetDirection(playerPos, _playerCtrl.transform.forward, nearestNpcPos);
                        announcements.Add($"{nearestNpcName} {nearestNpcDist:F0} meters {direction}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] NPC scan error: {ex.Message}");
            }

            // Check for enemies
            try
            {
                if (_enemyManager == null)
                    _enemyManager = UnityEngine.Object.FindObjectOfType<EnemyManager>();

                if (_enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
                {
                    float nearestEnemyDist = float.MaxValue;
                    string nearestEnemyName = null;
                    Vector3 nearestEnemyPos = Vector3.zero;

                    foreach (var enemy in _enemyManager.m_EnemyCtrlArray)
                    {
                        if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, enemy.transform.position);
                        if (dist < EnemyRange && dist < nearestEnemyDist)
                        {
                            nearestEnemyDist = dist;
                            nearestEnemyName = "Enemy";
                            nearestEnemyPos = enemy.transform.position;
                        }
                    }

                    if (nearestEnemyName != null)
                    {
                        string direction = GetDirection(playerPos, _playerCtrl.transform.forward, nearestEnemyPos);
                        announcements.Add($"{nearestEnemyName} {nearestEnemyDist:F0} meters {direction}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Enemy scan error: {ex.Message}");
            }

            // Announce results
            if (announcements.Count > 0)
            {
                string fullAnnouncement = string.Join(". ", announcements);
                ScreenReader.Say(fullAnnouncement);
                DebugLogger.Log($"[AudioNav] Announced: {fullAnnouncement}");

                // Play a sound to confirm
                try
                {
                    CriSoundManager.PlayCommonSe(CriSoundManager.SE_OK);
                }
                catch { }
            }
            else
            {
                ScreenReader.Say("No objects nearby");
                DebugLogger.Log("[AudioNav] No objects found in range");
            }
        }

        private string GetDirection(Vector3 playerPos, Vector3 playerForward, Vector3 targetPos)
        {
            Vector3 toTarget = (targetPos - playerPos).normalized;
            toTarget.y = 0;
            playerForward.y = 0;

            if (toTarget.magnitude < 0.01f || playerForward.magnitude < 0.01f)
                return "nearby";

            toTarget.Normalize();
            playerForward.Normalize();

            // Calculate angle
            float dot = Vector3.Dot(playerForward, toTarget);
            float angle = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;

            // Calculate side (left or right)
            Vector3 cross = Vector3.Cross(playerForward, toTarget);
            bool isRight = cross.y > 0;

            if (angle < 30f)
                return "ahead";
            else if (angle > 150f)
                return "behind";
            else if (angle < 60f)
                return isRight ? "ahead right" : "ahead left";
            else if (angle > 120f)
                return isRight ? "behind right" : "behind left";
            else
                return isRight ? "right" : "left";
        }

        private void Toggle()
        {
            _enabled = !_enabled;

            if (_enabled)
            {
                if (!_initialized)
                    Initialize();
                ScreenReader.Say("Audio navigation enabled. F4 tests sounds. F5 announces nearby objects.");
                DebugLogger.Log("[AudioNav] Enabled");

                // Play confirmation sound
                try
                {
                    CriSoundManager.PlayCommonSe(CriSoundManager.SE_OK);
                }
                catch { }
            }
            else
            {
                ScreenReader.Say("Audio navigation disabled");
                DebugLogger.Log("[AudioNav] Disabled");

                // Play confirmation sound
                try
                {
                    CriSoundManager.PlayCommonSe(CriSoundManager.SE_Cancel);
                }
                catch { }
            }
        }

        public bool IsEnabled()
        {
            return _enabled;
        }

        public void Cleanup()
        {
            _initialized = false;

            // Clean up NAudio positional audio
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

            // Clean up legacy CRI SoundSource
            if (_3dSoundSource != null)
            {
                try
                {
                    CriSoundManager.RemoveComponentSource(_3dSoundSource);
                }
                catch { }
                _3dSoundSource = null;
            }

            if (_3dSoundObj != null)
            {
                UnityEngine.Object.Destroy(_3dSoundObj);
                _3dSoundObj = null;
            }
        }

        /// <summary>
        /// Toggle 3D positional audio tracking to nearest object
        /// </summary>
        private void Toggle3DAudioTracking()
        {
            DebugLogger.Log("[AudioNav] === Toggle 3D Audio Tracking ===");

            if (!_initialized)
            {
                Initialize();
            }

            // If already tracking, stop
            if (_positionalAudio != null && _positionalAudio.IsPlaying)
            {
                _positionalAudio.Stop();
                _trackedTarget = null;
                ScreenReader.Say("Tracking stopped");
                DebugLogger.Log("[AudioNav] Tracking stopped by user");
                return;
            }

            // Find player
            if (_playerCtrl == null)
            {
                _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
            }

            if (_playerCtrl == null)
            {
                ScreenReader.Say("Cannot find player");
                return;
            }

            Vector3 playerPos = _playerCtrl.transform.position;
            DebugLogger.Log($"[AudioNav] Player at ({playerPos.x:F1}, {playerPos.y:F1}, {playerPos.z:F1})");

            // Find nearest trackable object (prioritize: Item > NPC > Enemy > Partner)
            GameObject bestTarget = null;
            float bestDist = float.MaxValue;
            string bestType = "";
            PositionalAudio.SoundType bestSoundType = PositionalAudio.SoundType.Beep;

            // Check for items
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
                            bestSoundType = PositionalAudio.SoundType.Beep;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Item scan error: {ex.Message}");
            }

            // Check for NPCs (only if no closer item)
            try
            {
                if (_npcManager == null)
                    _npcManager = UnityEngine.Object.FindObjectOfType<NpcManager>();

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
                            bestSoundType = PositionalAudio.SoundType.Pulse;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] NPC scan error: {ex.Message}");
            }

            // Check for enemies
            try
            {
                if (_enemyManager == null)
                    _enemyManager = UnityEngine.Object.FindObjectOfType<EnemyManager>();

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
                            bestSoundType = PositionalAudio.SoundType.Warning;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AudioNav] Enemy scan error: {ex.Message}");
            }

            // Check for partner Digimon (as fallback)
            if (bestTarget == null)
            {
                try
                {
                    var digimonCtrls = UnityEngine.Object.FindObjectsOfType<DigimonCtrl>();
                    foreach (var digimon in digimonCtrls)
                    {
                        if (digimon == null || digimon.gameObject == null || !digimon.gameObject.activeInHierarchy)
                            continue;

                        float dist = Vector3.Distance(playerPos, digimon.transform.position);
                        if (dist < 50f && dist < bestDist && dist > 1f)
                        {
                            bestDist = dist;
                            bestTarget = digimon.gameObject;
                            bestType = "Partner";
                            bestSoundType = PositionalAudio.SoundType.Pulse;
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[AudioNav] Partner scan error: {ex.Message}");
                }
            }

            // Start tracking if we found something
            if (bestTarget != null)
            {
                _trackedTarget = bestTarget;
                _trackedTargetType = bestType;

                // Get direction for announcement
                string direction = GetDirection(playerPos, _playerCtrl.transform.forward, bestTarget.transform.position);

                DebugLogger.Log($"[AudioNav] Tracking {bestType} at distance {bestDist:F1}m, {direction}");
                DebugLogger.Log($"[AudioNav] Target position: ({bestTarget.transform.position.x:F1}, {bestTarget.transform.position.y:F1}, {bestTarget.transform.position.z:F1})");

                // Initialize positions
                Vector3 playerForward = _playerCtrl.transform.forward;
                _positionalAudio.UpdatePlayerPosition(
                    playerPos.x, playerPos.y, playerPos.z,
                    playerForward.x, playerForward.z
                );

                Vector3 targetPos = bestTarget.transform.position;
                _positionalAudio.UpdateTargetPosition(targetPos.x, targetPos.y, targetPos.z);

                // Start tracking with appropriate sound type
                _positionalAudio.StartTracking(bestSoundType, bestDist + 10f);

                ScreenReader.Say($"Tracking {bestType}, {bestDist:F0} meters {direction}. Press F6 to stop.");
            }
            else
            {
                ScreenReader.Say("No objects nearby to track");
                DebugLogger.Log("[AudioNav] No trackable objects found");
            }
        }
    }
}
