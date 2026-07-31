using System;
using System.Collections.Generic;
using System.Text;
using Il2Cpp;

namespace DigimonNOAccess
{
    /// <summary>
    /// Reads the stat buffs active on a Digimon in battle.
    ///
    /// A sighted player sees these as icons on the HP bar, so they are part of the
    /// sighted view and we speak them - but the game has no localized name for any
    /// of the four stats on this path, so the wording below is ours. It matches the
    /// wording the training menu already uses (Strength, Stamina, Wisdom, Speed) so
    /// the same stat is never called two different things.
    ///
    /// The data itself needs no reflection: DigimonGameData exposes three public
    /// predicates over ParameterUpType, plus per-source totals.
    /// </summary>
    public static class BattleBuffReader
    {
        /// <summary>
        /// The mod's wording for the four stats. The game calls them Forcefulness,
        /// Robustness, Cleverness and Rapidity internally and has no display string
        /// for them here; these are the words TrainingPanelHandler already uses.
        /// </summary>
        public static string StatName(DigimonGameData.ParameterUpType type)
        {
            return type switch
            {
                DigimonGameData.ParameterUpType.ParameterUpType_Forcefulness => "Strength",
                DigimonGameData.ParameterUpType.ParameterUpType_Robustness   => "Stamina",
                DigimonGameData.ParameterUpType.ParameterUpType_Cleverness   => "Wisdom",
                DigimonGameData.ParameterUpType.ParameterUpType_Rapidity     => "Speed",
                _ => null
            };
        }

        private static readonly DigimonGameData.ParameterUpType[] Stats =
        {
            DigimonGameData.ParameterUpType.ParameterUpType_Forcefulness,
            DigimonGameData.ParameterUpType.ParameterUpType_Robustness,
            DigimonGameData.ParameterUpType.ParameterUpType_Cleverness,
            DigimonGameData.ParameterUpType.ParameterUpType_Rapidity,
        };

        /// <summary>
        /// A snapshot of which stats are boosted, and by what. Comparing two of these
        /// is how the automatic announcement detects a change without polling speech.
        /// </summary>
        public struct BuffState : IEquatable<BuffState>
        {
            // One bit per stat per source. Cheap to compare, cheap to store.
            public int AttackMask;
            public int TensionMask;
            public int ItemMask;

            public bool Any => (AttackMask | TensionMask | ItemMask) != 0;

            public bool Equals(BuffState other) =>
                AttackMask == other.AttackMask
                && TensionMask == other.TensionMask
                && ItemMask == other.ItemMask;

            public override bool Equals(object obj) => obj is BuffState s && Equals(s);

            public override int GetHashCode() =>
                AttackMask | (TensionMask << 8) | (ItemMask << 16);
        }

        /// <summary>
        /// Read the buffs currently active on a partner.
        ///
        /// Returns false when the partner or its game data is not reachable, which is
        /// deliberately NOT the same as "no buffs are active". The battle panel can
        /// come up a frame before gameData exists; treating that as an empty state
        /// would make an already-running buff announce itself as newly applied the
        /// moment the data appeared, and a transient failure mid-battle would say
        /// "back to normal" and then "up" again. Callers must keep their previous
        /// state when this returns false.
        ///
        /// Each failure logs its own distinct reason rather than collapsing into one
        /// silent null chain.
        /// </summary>
        public static bool TryRead(int partnerIndex, out BuffState state)
        {
            state = new BuffState();

            try
            {
                var ctrl = MainGameManager.GetPartnerCtrl(partnerIndex);
                if (ctrl == null)
                    return false;   // between battles or partner absent - normal, not logged

                var data = ctrl.gameData;
                if (data == null)
                {
                    DebugLogger.Log($"[BattleBuffReader] Partner {partnerIndex} has no gameData yet");
                    return false;
                }

                for (int i = 0; i < Stats.Length; i++)
                {
                    int bit = 1 << i;
                    if (data.IsParameterUp(Stats[i]))     state.AttackMask  |= bit;
                    if (data.IsHighTensionUp(Stats[i]))   state.TensionMask |= bit;
                    if (data.IsItemParameterUp(Stats[i])) state.ItemMask    |= bit;
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BattleBuffReader] Buff predicates failed for partner {partnerIndex}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Convenience read for one-shot callers such as the status hotkey, where a
        /// missing partner and no active buffs read the same to the player anyway.
        /// </summary>
        public static BuffState Read(int partnerIndex)
        {
            TryRead(partnerIndex, out var state);
            return state;
        }

        /// <summary>
        /// Describes a state as speech, e.g. "Strength and Speed up". Returns null when
        /// nothing is active, so callers can skip the line entirely rather than saying
        /// "no buffs" every time.
        /// </summary>
        public static string Describe(BuffState state)
        {
            if (!state.Any)
                return null;

            // A stat boosted from more than one source is still just "up" to the
            // player - the sources stack into one icon-level fact.
            int combined = state.AttackMask | state.TensionMask | state.ItemMask;

            var names = new List<string>();
            for (int i = 0; i < Stats.Length; i++)
            {
                if ((combined & (1 << i)) != 0)
                    names.Add(StatName(Stats[i]));
            }

            if (names.Count == 0)
                return null;

            return JoinNaturally(names) + " up";
        }

        /// <summary>
        /// Describes only what changed between two states, so the automatic
        /// announcement says "Speed up" rather than re-reading everything active.
        /// </summary>
        public static string DescribeChange(BuffState before, BuffState after)
        {
            int oldMask = before.AttackMask | before.TensionMask | before.ItemMask;
            int newMask = after.AttackMask | after.TensionMask | after.ItemMask;

            var gained = new List<string>();
            var lost = new List<string>();

            for (int i = 0; i < Stats.Length; i++)
            {
                int bit = 1 << i;
                bool had = (oldMask & bit) != 0;
                bool has = (newMask & bit) != 0;
                if (!had && has) gained.Add(StatName(Stats[i]));
                else if (had && !has) lost.Add(StatName(Stats[i]));
            }

            if (gained.Count == 0 && lost.Count == 0)
                return null;

            var sb = new StringBuilder();
            if (gained.Count > 0)
                sb.Append($"{JoinNaturally(gained)} up");
            if (lost.Count > 0)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append($"{JoinNaturally(lost)} back to normal");
            }
            return sb.ToString();
        }

        private static string JoinNaturally(List<string> names)
        {
            if (names.Count == 1) return names[0];
            if (names.Count == 2) return $"{names[0]} and {names[1]}";
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }
    }
}
