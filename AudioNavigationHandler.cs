using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

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

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                _positionalAudio = new PositionalAudio();
                _initialized = true;
                DebugLogger.Log("[AudioNav] Initialized - always-on mode");
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

            // Update tracking
            UpdatePositionalAudioTracking();
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

        private bool IsInBattle()
        {
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null && battlePanel.m_enabled)
                {
                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool IsPlayerInControl()
        {
            try
            {
                // Check if in battle
                if (IsInBattle())
                    return false;

                // Check if any menu is open that blocks player control
                if (IsMenuOpen())
                    return false;

                if (_playerCtrl == null)
                {
                    _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                }

                if (_playerCtrl == null)
                    return false;

                // Check player action state
                var actionState = _playerCtrl.actionState;
                switch (actionState)
                {
                    case UnitCtrlBase.ActionState.ActionState_Event:
                    case UnitCtrlBase.ActionState.ActionState_Battle:
                    case UnitCtrlBase.ActionState.ActionState_Dead:
                    case UnitCtrlBase.ActionState.ActionState_DeadGataway:
                    case UnitCtrlBase.ActionState.ActionState_LiquidCrystallization:
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool IsMenuOpen()
        {
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    // Care menu check - reliable state-based detection
                    var careUI = mgm.careUI;
                    if (careUI != null && careUI.m_state != uCarePanel.State.None)
                        return true;
                }

                // Digivice panel check - has static instance with m_enabled flag
                var digivicePanel = uDigivicePanel.m_instance;
                if (digivicePanel != null && digivicePanel.m_enabled)
                    return true;

                // Save panel check - use state-based detection via uSavePanelCommand
                var savePanel = UnityEngine.Object.FindObjectOfType<uSavePanelCommand>();
                if (savePanel != null)
                {
                    var state = savePanel.m_State;
                    if (state == uSavePanelCommand.State.MAIN ||
                        state == uSavePanelCommand.State.SAVE_CHECK ||
                        state == uSavePanelCommand.State.LOAD_CHECK)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
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
