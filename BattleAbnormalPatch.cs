using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;

namespace DigimonNOAccess
{
    /// <summary>
    /// Announces battle status conditions as they are applied and cleared.
    ///
    /// Poison, paralysis, confusion and the rest change how your Digimon behave on
    /// their own and whether they survive, and nothing told you about any of them
    /// before this. A sighted player reads them off the icons on the battle HUD, so
    /// they are part of the sighted view.
    ///
    /// The wording is ours. The game has no localized name for any abnormal state -
    /// the icon classes expose only sprites, child indices and timers - so these are
    /// plain English descriptions of what each enum member actually is, approved on
    /// that basis. They are deliberately the state ("poisoned") rather than the
    /// mechanic, because that is what the icon communicates.
    ///
    /// Anchored on uBattlePanel.enableAbnormalSign, which is the moment the game
    /// turns a sign on or off and carries the unit, the condition and the on/off
    /// state as arguments - so we never have to infer any of the three.
    /// </summary>
    public static class BattleAbnormalPatch
    {
        // Last announced on/off state per (unit, condition). The game can re-request a
        // sign that is already showing when an effect is refreshed or restacked, and
        // announcing that every time would be unusable.
        private static readonly Dictionary<(int unit, int abnormal), bool> _shown
            = new Dictionary<(int, int), bool>();

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Explicit parameter types: resolve uniquely or fail closed, rather than
                // silently binding a different overload after a game update.
                var target = AccessTools.Method(
                    typeof(uBattlePanel),
                    "enableAbnormalSign",
                    new Type[]
                    {
                        typeof(MainGameManager.UNITID),
                        typeof(ParameterAttackData.AbnormalIndex),
                        typeof(bool)
                    });

                if (target == null)
                {
                    DebugLogger.Error("[BattleAbnormal] uBattlePanel.enableAbnormalSign(UNITID, AbnormalIndex, bool) not found - status conditions will not be announced");
                    return;
                }

                harmony.Patch(target, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(BattleAbnormalPatch), nameof(Postfix))));

                DebugLogger.Log("[BattleAbnormal] Patched enableAbnormalSign");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[BattleAbnormal] Apply failed: {ex.Message}");
            }
        }

        /// <summary>Clears remembered sign state. Call when a battle ends.</summary>
        public static void Reset()
        {
            _shown.Clear();
        }

        private static void Postfix(MainGameManager.UNITID unitId,
            ParameterAttackData.AbnormalIndex abnomaly, bool sw)
        {
            try
            {
                string condition = ConditionName(abnomaly);
                if (condition == null)
                    return;

                var key = ((int)unitId, (int)abnomaly);
                bool seenBefore = _shown.TryGetValue(key, out bool wasShown);

                if (seenBefore && wasShown == sw)
                    return;

                _shown[key] = sw;

                // A first callback of false is the game clearing a sign that was never
                // showing - a baseline, not a recovery. Record it, say nothing, or the
                // start of every battle would announce conditions nobody had.
                if (!seenBefore && !sw)
                    return;

                string who = UnitName(unitId);
                if (who == null)
                    return;

                ScreenReader.SayQueued(sw
                    ? $"{who} {condition}"
                    : $"{who} no longer {condition}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BattleAbnormal] Postfix error: {ex.Message}");
            }
        }

        /// <summary>
        /// Plain English for each abnormal state. These are the mod's words - the game
        /// has no localized string for any of them - so each one describes exactly what
        /// the enum member is, nothing more.
        /// </summary>
        private static string ConditionName(ParameterAttackData.AbnormalIndex abnormal)
        {
            return abnormal switch
            {
                ParameterAttackData.AbnormalIndex.AbnormalIndex_Poison       => "poisoned",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_Slow         => "slowed",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_Paralysis    => "paralysed",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_Confusion    => "confused",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_LiquidCrystal => "crystallised",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_Anger        => "angry",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_AngerToYour  => "angry at you",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_AngerToAlly  => "angry at its ally",
                ParameterAttackData.AbnormalIndex.AbnormalIndex_PoisonSlow   => "poisoned and slowed",
                _ => null
            };
        }

        /// <summary>
        /// Who the sign is on. Partners resolve to their real names; anything else is
        /// left alone rather than guessed at, since enemy identity is the game's to
        /// reveal through uEnemyName and inventing a label here could leak it.
        /// </summary>
        private static string UnitName(MainGameManager.UNITID unitId)
        {
            try
            {
                if (unitId == MainGameManager.UNITID.Partner00)
                    return MainGameManager.GetPartnerCtrl(0)?.Name ?? PartnerUtilities.GetPartnerLabel(0);

                if (unitId == MainGameManager.UNITID.Partner01)
                    return MainGameManager.GetPartnerCtrl(1)?.Name ?? PartnerUtilities.GetPartnerLabel(1);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BattleAbnormal] Could not resolve unit {unitId}: {ex.Message}");
            }

            // Not one of our partners. Enemies get their status announced with the
            // neutral word the battle HUD already uses for them elsewhere.
            return "Enemy";
        }
    }
}
