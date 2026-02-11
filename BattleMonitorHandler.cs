using Il2Cpp;
using System;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Background battle monitor that polls battle state every frame and
    /// triggers audio cues and speech for important state changes.
    ///
    /// Monitors:
    /// - Enemy special attack charging (yellow aura) -> warning tone
    /// - Partner OP reaching 150 -> milestone chime + speech
    /// - Enemy target switching -> tone + speech
    /// - ExE and SP attack availability -> one-time speech
    ///
    /// Also stores last-known SP charging state for on-demand speech (F12 hotkey).
    /// </summary>
    public class BattleMonitorHandler : IAccessibilityHandler
    {
        // Background handler: high priority number, never "open" (doesn't block other handlers)
        public int Priority => 998;
        public bool IsOpen() => false;

        // State tracking
        private bool _wasBattleActive = false;
        private bool _enemyCountAnnounced = false;

        // SP attack tracking per enemy (up to 20 enemies max)
        private const int MaxEnemies = 20;
        private readonly float[] _lastSpTimer = new float[MaxEnemies];
        private readonly bool[] _spWarningPlayed = new bool[MaxEnemies];

        // OP milestone tracking per partner
        private readonly bool[] _opMilestoneAnnounced = new bool[2];
        private readonly int[] _lastOP = new int[2];

        // Last known SP charging info for on-demand query
        private static string _lastSPChargeInfo = "";

        // Cheer cooldown tracking (null = not yet initialized this battle)
        private bool? _cheerWasOnCooldown = null;

        // Cooldowns to prevent spam
        private float _lastSPWarningTime = 0f;
        private const float SPWarningCooldown = 1.0f;

        public void Update()
        {
            // Detect battle via m_CurStep (fires earlier than uBattlePanel.m_enabled).
            var mgc = MainGameComponent.m_instance;
            bool inBattleStep = mgc != null && mgc.m_CurStep == Il2CppMainGame.STEP.Battle;

            if (!inBattleStep)
            {
                if (_wasBattleActive)
                    ResetState();
                return;
            }

            if (!_wasBattleActive)
            {
                _wasBattleActive = true;
                BattleAudioCues.Initialize();
                DebugLogger.Log("[BattleMonitor] Battle started, monitoring active");
            }

            // Poll for enemy count each frame until announced.
            // m_enemy_hps becomes available before m_enabled, giving us an early
            // announcement window that fires before tutorial/event messages.
            if (!_enemyCountAnnounced)
            {
                TryAnnounceEnemyCount();
            }

            // Ongoing monitoring requires the battle panel to be fully enabled.
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
                return;

            try
            {
                MonitorEnemySPAttacks(battlePanel);
                MonitorOPMilestones(battlePanel);
                MonitorCheerAvailability(battlePanel);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BattleMonitor] Update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Monitor enemy m_spAttackTimer to detect special attack charging.
        /// When timer transitions from 0 to >0, the enemy is charging (yellow aura).
        /// </summary>
        private void MonitorEnemySPAttacks(uBattlePanel battlePanel)
        {
            var enemyBars = battlePanel.m_enemy_hps;
            if (enemyBars == null) return;

            float now = Time.time;
            bool anyCharging = false;
            string chargingInfo = "";

            for (int i = 0; i < enemyBars.Length && i < MaxEnemies; i++)
            {
                try
                {
                    var bar = enemyBars[i];
                    if (bar == null || bar.gameObject == null || !bar.gameObject.activeInHierarchy)
                        continue;

                    var unit = bar.m_unit;
                    if (unit == null) continue;

                    float spTimer = unit.m_spAttackTimer;

                    // Detect transition: was not charging -> now charging
                    if (spTimer > 0 && _lastSpTimer[i] <= 0 && !_spWarningPlayed[i])
                    {
                        _spWarningPlayed[i] = true;

                        // Play warning tone (with cooldown to prevent stacking)
                        if (now - _lastSPWarningTime > SPWarningCooldown)
                        {
                            BattleAudioCues.PlaySPWarning();
                            _lastSPWarningTime = now;
                        }

                        string enemyName = GetEnemyName(bar, unit);
                        DebugLogger.Log($"[BattleMonitor] Enemy {i} ({enemyName}) charging SP attack! Timer: {spTimer}");

                        anyCharging = true;
                        if (!string.IsNullOrEmpty(chargingInfo))
                            chargingInfo += ". ";
                        chargingInfo += $"{enemyName} charging special attack";
                    }

                    // Reset when timer goes back to 0
                    if (spTimer <= 0 && _lastSpTimer[i] > 0)
                    {
                        _spWarningPlayed[i] = false;
                    }

                    _lastSpTimer[i] = spTimer;
                }
                catch { }
            }

            if (anyCharging)
            {
                _lastSPChargeInfo = chargingInfo;
            }
        }

        /// <summary>
        /// Monitor partner Order Power and announce when reaching 150 (special attack ready).
        /// </summary>
        private void MonitorOPMilestones(uBattlePanel battlePanel)
        {
            var digimonPanels = battlePanel.m_digimon;
            if (digimonPanels == null) return;

            for (int i = 0; i < digimonPanels.Length && i < 2; i++)
            {
                try
                {
                    var panel = digimonPanels[i];
                    if (panel == null) continue;

                    int op = panel.m_dispOrderPower;

                    // Detect crossing 150 threshold
                    if (op >= 150 && _lastOP[i] < 150 && !_opMilestoneAnnounced[i])
                    {
                        _opMilestoneAnnounced[i] = true;
                        BattleAudioCues.PlayOPMilestone();
                        string partnerLabel = PartnerUtilities.GetPartnerLabel(i);
                        ScreenReader.SayQueued($"{partnerLabel}, 150 OP ready");
                        DebugLogger.Log($"[BattleMonitor] {partnerLabel} reached 150 OP");
                    }

                    // Reset when OP drops below 150 (after using special)
                    if (op < 150 && _lastOP[i] >= 150)
                    {
                        _opMilestoneAnnounced[i] = false;
                    }

                    _lastOP[i] = op;
                }
                catch { }
            }
        }

        /// <summary>
        /// Poll for battle enemy HP bars each frame. Once active bars are found,
        /// announce the count and stop polling. This fires as soon as the battle
        /// panel's m_enemy_hps array is populated (before m_enabled becomes true),
        /// giving us an early announcement before tutorial/event messages appear.
        /// </summary>
        private void TryAnnounceEnemyCount()
        {
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel == null) return;

                var enemyBars = battlePanel.m_enemy_hps;
                if (enemyBars == null || enemyBars.Length == 0) return;

                int count = 0;
                for (int i = 0; i < enemyBars.Length; i++)
                {
                    var bar = enemyBars[i];
                    if (bar != null && bar.gameObject != null && bar.gameObject.activeInHierarchy)
                        count++;
                }

                if (count > 0)
                {
                    _enemyCountAnnounced = true;
                    string msg = count == 1 ? "1 enemy" : $"{count} enemies";
                    ScreenReader.SayQueued(msg);
                    DebugLogger.Log($"[BattleMonitor] Battle start: {msg}");
                }
            }
            catch { }
        }

        /// <summary>
        /// Monitor cheer cooldown and announce when it becomes available.
        /// The beep tones handle per-hit timing; this gives a one-time speech cue.
        /// </summary>
        private void MonitorCheerAvailability(uBattlePanel battlePanel)
        {
            try
            {
                var cheerCmd = battlePanel.m_cheerCommand;
                if (cheerCmd == null) return;

                bool onCooldown = cheerCmd.DisableTimer > 0f;

                // First frame: just record the initial state, don't announce
                if (_cheerWasOnCooldown == null)
                {
                    _cheerWasOnCooldown = onCooldown;
                    return;
                }

                if (_cheerWasOnCooldown.Value && !onCooldown)
                {
                    ScreenReader.SayQueued("Cheer ready");
                    DebugLogger.Log("[BattleMonitor] Cheer came off cooldown");
                }

                _cheerWasOnCooldown = onCooldown;
            }
            catch { }
        }

        /// <summary>
        /// Get a human-readable name for an enemy from its HP bar and DigimonCtrl.
        /// </summary>
        public static string GetEnemyName(uEnemyHpBar bar, DigimonCtrl unit)
        {
            // Try commonData.m_name first (pre-localized, most reliable)
            try
            {
                var gameData = unit.gameData;
                if (gameData != null)
                {
                    string name = gameData.m_commonData?.m_name;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                        return TextUtilities.StripRichTextTags(name);

                    // Fallback: look up via ParameterDigimonData
                    int digimonId = gameData.m_no;
                    if (digimonId > 0)
                    {
                        var paramData = ParameterDigimonData.GetParam((uint)digimonId);
                        if (paramData != null)
                        {
                            name = paramData.GetDefaultName();
                            if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                return TextUtilities.StripRichTextTags(name);
                        }
                    }
                }
            }
            catch { }

            // Fallback: try the GameObject name
            try
            {
                string objName = unit.gameObject?.name;
                if (!string.IsNullOrEmpty(objName))
                    return objName;
            }
            catch { }

            return "Enemy";
        }

        /// <summary>
        /// Determine which partner an enemy is currently targeting.
        /// Returns "Partner 1", "Partner 2", or empty string if unknown.
        /// </summary>
        public static string GetEnemyTargetPartner(DigimonCtrl enemy)
        {
            try
            {
                var targetTransform = enemy.m_targetTransform;
                if (targetTransform == null) return "";

                for (int i = 0; i < 2; i++)
                {
                    var partner = MainGameManager.GetPartnerCtrl(i);
                    if (partner != null && partner.gameObject != null &&
                        partner.gameObject.transform.Pointer == targetTransform.Pointer)
                    {
                        // Try to get the partner's Digimon name
                        try
                        {
                            string name = partner.gameData?.m_commonData?.m_name;
                            if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                return TextUtilities.StripRichTextTags(name);
                        }
                        catch { }

                        return PartnerUtilities.GetPartnerLabel(i);
                    }
                }
            }
            catch { }

            return "";
        }

        /// <summary>
        /// Get information about all enemies currently in battle.
        /// Used by BattleHudHandler for on-demand enemy info hotkey.
        /// </summary>
        public static string GetAllEnemyInfo()
        {
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
                return "Not in battle";

            var enemyBars = battlePanel.m_enemy_hps;
            if (enemyBars == null || enemyBars.Length == 0)
                return "No enemies";

            string result = "";
            int count = 0;

            for (int i = 0; i < enemyBars.Length; i++)
            {
                try
                {
                    var bar = enemyBars[i];
                    if (bar == null || bar.gameObject == null || !bar.gameObject.activeInHierarchy)
                        continue;

                    var unit = bar.m_unit;
                    if (unit == null) continue;

                    string name = GetEnemyName(bar, unit);
                    string level = "";
                    try
                    {
                        level = bar.m_levelText?.text ?? "";
                    }
                    catch { }

                    float hpRate = bar.m_now_hp_rate;
                    int hpPercent = (int)(hpRate * 100);

                    string targeting = GetEnemyTargetPartner(unit);

                    if (count > 0) result += ". ";
                    result += $"{name}";
                    if (!string.IsNullOrEmpty(level))
                        result += $" level {level}";
                    result += $", HP {hpPercent} percent";
                    if (!string.IsNullOrEmpty(targeting))
                        result += $", targeting {targeting}";
                    count++;
                }
                catch { }
            }

            if (count == 0)
                return "No enemies visible";

            DebugLogger.Log($"[BattleMonitor] GetAllEnemyInfo result: {result}");
            return result;
        }

        /// <summary>
        /// Get information about a specific enemy by index (0-based).
        /// Used by BattleHudHandler for per-enemy hotkeys.
        /// </summary>
        public static string GetEnemyInfoByIndex(int index)
        {
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
                return "Not in battle";

            var enemyBars = battlePanel.m_enemy_hps;
            if (enemyBars == null || enemyBars.Length == 0)
                return "No enemies";

            // Find the Nth active enemy
            int count = 0;
            for (int i = 0; i < enemyBars.Length; i++)
            {
                try
                {
                    var bar = enemyBars[i];
                    if (bar == null || bar.gameObject == null || !bar.gameObject.activeInHierarchy)
                        continue;

                    var unit = bar.m_unit;
                    if (unit == null) continue;

                    if (count == index)
                    {
                        string name = GetEnemyName(bar, unit);
                        string level = "";
                        try
                        {
                            level = bar.m_levelText?.text ?? "";
                        }
                        catch { }

                        float hpRate = bar.m_now_hp_rate;
                        int hpPercent = (int)(hpRate * 100);

                        string targeting = GetEnemyTargetPartner(unit);

                        string result = name;
                        if (!string.IsNullOrEmpty(level))
                            result += $" level {level}";
                        result += $", HP {hpPercent} percent";
                        if (!string.IsNullOrEmpty(targeting))
                            result += $", targeting {targeting}";
                        return result;
                    }

                    count++;
                }
                catch { }
            }

            return $"No enemy {index + 1}";
        }

        /// <summary>
        /// Get the current combined Order Power for both partners.
        /// </summary>
        public static string GetOrderPower()
        {
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
                return "Not in battle";

            var digimonPanels = battlePanel.m_digimon;
            if (digimonPanels == null) return "No data";

            int totalOP = 0;
            for (int i = 0; i < digimonPanels.Length && i < 2; i++)
            {
                try
                {
                    var panel = digimonPanels[i];
                    if (panel != null)
                        totalOP += panel.m_dispOrderPower;
                }
                catch { }
            }

            return $"{totalOP} OP";
        }

        /// <summary>
        /// Get the last SP charge warning info for on-demand speech.
        /// </summary>
        public static string GetLastSPChargeInfo()
        {
            if (string.IsNullOrEmpty(_lastSPChargeInfo))
                return "No special attack detected";
            return _lastSPChargeInfo;
        }

        private void ResetState()
        {
            _wasBattleActive = false;
            _enemyCountAnnounced = false;
            _lastSPChargeInfo = "";
            _cheerWasOnCooldown = null;

            for (int i = 0; i < MaxEnemies; i++)
            {
                _lastSpTimer[i] = 0;
                _spWarningPlayed[i] = false;
            }

            for (int i = 0; i < 2; i++)
            {
                _opMilestoneAnnounced[i] = false;
                _lastOP[i] = 0;
            }

            DebugLogger.Log("[BattleMonitor] State reset (battle ended)");
        }

        public void AnnounceStatus()
        {
            // Background handler, doesn't announce status
        }
    }
}
