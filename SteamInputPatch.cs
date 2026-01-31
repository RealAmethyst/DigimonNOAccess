using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patches to disable Steam text input overlay for accessibility.
    /// Steam's text input overlay is not accessible with screen readers.
    /// </summary>
    public static class SteamInputPatch
    {
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize()
        {
            try
            {
                _harmony = new HarmonyLib.Harmony("com.accessibility.digimonno.steaminput");

                // Patch NameEntry.ShowSteamTextInput to always return false
                var originalMethod = typeof(NameEntry).GetMethod("ShowSteamTextInput");
                var prefixMethod = typeof(SteamInputPatch).GetMethod("ShowSteamTextInput_Prefix",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

                if (originalMethod != null && prefixMethod != null)
                {
                    _harmony.Patch(originalMethod, prefix: new HarmonyMethod(prefixMethod));
                    DebugLogger.Log("[SteamInputPatch] Successfully patched ShowSteamTextInput");
                }
                else
                {
                    DebugLogger.Log($"[SteamInputPatch] Failed to find methods: original={originalMethod != null}, prefix={prefixMethod != null}");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SteamInputPatch] Error initializing: {ex.Message}");
            }
        }

        /// <summary>
        /// Prefix patch that prevents ShowSteamTextInput from executing.
        /// Returns false via __result and skips the original method.
        /// </summary>
        private static bool ShowSteamTextInput_Prefix(ref bool __result)
        {
            // Set result to false (Steam text input not available)
            __result = false;
            DebugLogger.Log("[SteamInputPatch] Blocked Steam text input");
            // Return false to skip the original method
            return false;
        }

        public static void Shutdown()
        {
            try
            {
                _harmony?.UnpatchSelf();
                DebugLogger.Log("[SteamInputPatch] Unpatched");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SteamInputPatch] Error shutting down: {ex.Message}");
            }
        }
    }
}
