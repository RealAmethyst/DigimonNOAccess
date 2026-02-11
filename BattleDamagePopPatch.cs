using HarmonyLib;
using Il2Cpp;
using System;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patch on uBattlePanel.DispDamagePop to detect when damage numbers
    /// appear on enemies. This is the optimal moment to press X (cheer) for max OP.
    ///
    /// Plays two different tones:
    /// - Quiet tick: when cheer is on cooldown (fight rhythm feedback)
    /// - Loud beep: when cheer is available (signals "press X NOW")
    /// </summary>
    public static class BattleDamagePopPatch
    {
        /// <summary>
        /// Apply the Harmony patch for damage popup interception.
        /// </summary>
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch the overload that includes UNITID (identifies who took damage)
                var method = AccessTools.Method(typeof(uBattlePanel), "DispDamagePop",
                    new Type[] { typeof(MainGameManager.UNITID), typeof(int), typeof(uDamagePop.Type), typeof(uDamagePop.NatureRateInfo) });

                if (method != null)
                {
                    harmony.Patch(method, prefix: new HarmonyMethod(
                        AccessTools.Method(typeof(BattleDamagePopPatch), nameof(DispDamagePopPrefix))));
                    DebugLogger.Log("[BattleDamagePopPatch] Patched uBattlePanel.DispDamagePop (UNITID overload)");
                }
                else
                {
                    DebugLogger.Warning("[BattleDamagePopPatch] Could not find DispDamagePop method");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[BattleDamagePopPatch] Patch error: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix: fires before the damage popup is displayed.
        /// </summary>
        private static void DispDamagePopPrefix(MainGameManager.UNITID unitId, int damage, uDamagePop.Type type)
        {
            try
            {
                // Only care about damage types on enemies (not recovery, buffs, OP gains, etc.)
                if (type != uDamagePop.Type.Damage &&
                    type != uDamagePop.Type.NormalDamage &&
                    type != uDamagePop.Type.Critical &&
                    type != uDamagePop.Type.Break)
                    return;

                // Only trigger for enemy units (UNITID >= EnemyIdBegin which is 19)
                if ((int)unitId < (int)MainGameManager.UNITID.EnemyIdBegin)
                    return;

                // Check if cheer is available (off cooldown)
                bool cheerAvailable = IsCheerAvailable();

                if (cheerAvailable)
                {
                    BattleAudioCues.PlayCheerTickLoud();
                }
                else
                {
                    BattleAudioCues.PlayCheerTickQuiet();
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BattleDamagePopPatch] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the cheer command is available (not on cooldown).
        /// </summary>
        private static bool IsCheerAvailable()
        {
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel == null) return false;

                var cheerCmd = battlePanel.m_cheerCommand;
                if (cheerCmd == null) return false;

                // DisableTimer counts down to 0. When <= 0, cheer is available.
                return cheerCmd.DisableTimer <= 0f;
            }
            catch
            {
                return false;
            }
        }
    }
}
