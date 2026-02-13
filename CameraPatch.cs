using HarmonyLib;
using Il2Cpp;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patch to force the field camera to look horizontally instead of slightly downward.
    /// This improves audio navigation accuracy since HRTF uses the camera forward vector.
    /// Only affects CameraScriptField (exploration areas), not town fixed cameras.
    /// </summary>
    public static class CameraPatch
    {
        private static bool _enabled = true;

        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var method = AccessTools.Method(typeof(CameraScriptField), "LateUpdate");
                if (method != null)
                {
                    harmony.Patch(method,
                        postfix: new HarmonyMethod(typeof(CameraPatch), nameof(LateUpdate_Postfix)));
                    DebugLogger.Log("[CameraPatch] Patched CameraScriptField.LateUpdate - camera forced horizontal");
                }
                else
                {
                    DebugLogger.Warning("[CameraPatch] Could not find CameraScriptField.LateUpdate");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"[CameraPatch] Apply error: {ex.Message}");
            }
        }

        private static void LateUpdate_Postfix(CameraScriptField __instance)
        {
            if (!_enabled) return;

            try
            {
                __instance.verticalAngle = 0f;
            }
            catch { }
        }
    }
}
