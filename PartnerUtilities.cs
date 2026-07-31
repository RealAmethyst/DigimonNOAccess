namespace DigimonNOAccess
{
    public static class PartnerUtilities
    {
        public static readonly string[] StatNames = { "HP", "MP", "STR", "STA", "WIS", "SPD" };
        public static readonly string[] StatNamesWithFatigue = { "HP", "MP", "STR", "STA", "WIS", "SPD", "Fatigue" };

        public static string GetPartnerLabel(int partnerIndex)
        {
            return partnerIndex == 0 ? "Partner 1" : "Partner 2";
        }

        public static string GetPartnerNotAvailableMessage(int partnerIndex)
        {
            return $"Partner {partnerIndex + 1} not available";
        }

        /// <summary>
        /// Converts a FieldStatusEffect enum to a human-readable string.
        /// </summary>
        /// <param name="effect">The status effect to describe.</param>
        /// <param name="noneText">Text for the None/healthy state (default "Healthy").</param>
        /// <param name="unknownText">Text for unrecognized effects (default "Unknown status").</param>
        public static string GetStatusEffectText(Il2Cpp.PartnerCtrl.FieldStatusEffect effect, string noneText = "Healthy", string unknownText = "Unknown status")
        {
            return effect switch
            {
                Il2Cpp.PartnerCtrl.FieldStatusEffect.None => noneText,
                Il2Cpp.PartnerCtrl.FieldStatusEffect.Injury => "Injured",
                Il2Cpp.PartnerCtrl.FieldStatusEffect.SeriousInjury => "Seriously Injured",
                Il2Cpp.PartnerCtrl.FieldStatusEffect.Disease => "Sick",
                _ => unknownText
            };
        }

        /// <summary>
        /// The real clamp ranges for the care gauges, read out of the game's own
        /// setters in Ghidra rather than inferred from the MIN_/MAX_ constant names.
        ///
        /// That distinction mattered: Curse clamps to 0..16 rather than any
        /// hundred-based scale, and Mood and Discipline both floor at 1, not 0 - none of
        /// which is guessable from the MIN_/MAX_ names. Only gauges the game actually
        /// draws a slider for are listed here; hunger and bond are not spoken at all.
        ///
        /// Evidence (GameAssembly.dll, DWNO Ghidra project):
        ///   SetFatigue  @ 1805957c0 - clamp 0..100,  field +0x6c
        ///   SetMood     @ 180595870 - clamp 1..100,  field +0x64
        ///   SetBreeding @ 180595170 - clamp 1..100,  field +0x60  (this is Upbringing;
        ///                             CScenarioScript._SetPartnerUpbringing calls it)
        ///   SetCurse    @ ...       - clamp 0..16,   field +0x58
        /// Addresses are evidence of where this was read, not anchors - nothing
        /// resolves by address at runtime.
        /// </summary>
        public const int FatigueMin = 0,    FatigueMax = 100;
        public const int MoodMin = 1,       MoodMax = 100;
        public const int DisciplineMin = 1, DisciplineMax = 100;
        public const int CurseMin = 0,      CurseMax = 16;

        /// <summary>
        /// Formats a care gauge the way a sighted player reads it - as a proportion of
        /// its bar rather than a raw number. The game shows these as gauges and has no
        /// text for them, so the percentage is ours, but the bounds are the game's own.
        ///
        /// Fails closed: if the value falls outside the bounds we assumed, or the range
        /// is degenerate, this returns the raw number instead of a nonsense percentage
        /// and logs why. A wrong maximum would otherwise silently report "Hunger 4
        /// percent" for a full Digimon.
        /// </summary>
        /// <param name="label">Spoken label, e.g. "Hunger".</param>
        /// <param name="value">Current value straight from the game.</param>
        /// <param name="min">The game's MIN_ constant for this gauge.</param>
        /// <param name="max">The game's MAX_ constant for this gauge.</param>
        public static string FormatGauge(string label, float value, float min, float max)
        {
            if (max <= min)
            {
                DebugLogger.Warning($"[PartnerUtilities] {label}: degenerate range {min}..{max}, speaking the raw value");
                return $"{label} {value:0.#}";
            }

            if (value < min || value > max)
            {
                DebugLogger.Warning($"[PartnerUtilities] {label}: value {value} outside assumed range {min}..{max}, speaking the raw value");
                return $"{label} {value:0.#}";
            }

            int percent = (int)System.Math.Round((value - min) / (max - min) * 100f);
            return $"{label} {percent} percent";
        }
    }
}
