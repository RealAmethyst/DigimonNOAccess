using HarmonyLib;
using Il2Cpp;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patches for uCommonYesNoWindow.Open/Close/OnDisable to reliably track
    /// when the yes/no dialog is shown. This is needed because m_callback is not always
    /// visible to Il2Cpp interop (e.g. first-playthrough egg selection, training prompts).
    /// </summary>
    public static class CommonYesNoPatch
    {
        /// <summary>
        /// True when uCommonYesNoWindow.Open() has been called and not yet closed.
        /// </summary>
        public static bool IsWindowOpen { get; private set; }

        [HarmonyPatch(typeof(uCommonYesNoWindow), nameof(uCommonYesNoWindow.Open))]
        public static class Open_Patch
        {
            public static void Postfix()
            {
                IsWindowOpen = true;
            }
        }

        [HarmonyPatch(typeof(uCommonYesNoWindow), nameof(uCommonYesNoWindow.Close))]
        public static class Close_Patch
        {
            public static void Postfix()
            {
                IsWindowOpen = false;
            }
        }

        [HarmonyPatch(typeof(uCommonYesNoWindow), nameof(uCommonYesNoWindow.OnDisable))]
        public static class OnDisable_Patch
        {
            public static void Postfix()
            {
                IsWindowOpen = false;
            }
        }
    }
}
