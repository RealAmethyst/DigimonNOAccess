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
    }
}
