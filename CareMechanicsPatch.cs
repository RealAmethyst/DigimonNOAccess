using HarmonyLib;
using Il2Cpp;
using MelonLoader.NativeUtils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony PREFIX patches and NativeHooks to disable care mechanics
    /// (hunger, toilet, fatigue, sickness) when ModSettings toggles are enabled.
    ///
    /// Most patches use Harmony, but training fatigue requires NativeHooks because
    /// the CSVB script engine calls native code directly, bypassing managed wrappers.
    /// </summary>
    public static class CareMechanicsPatch
    {
        // Real native implementation RVA for _AddPartnerFatigue (found via Ghidra).
        // 0x291420 is the managed wrapper thunk; 0x593260 is the actual function it calls.
        // Only hooking Add (35 bytes, safe trampoline). NOT hooking Set at 0x5957C0 (only 24 bytes,
        // trampoline corrupts m_lifetime causing instant death / rebirth loops).
        // Signature: void(IntPtr partnerDataPtr, int value)
        private const int AddPartnerFatigue_RVA = 0x593260;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void d_PartnerFatigueReal(IntPtr partnerDataPtr, int value);

        private static NativeHook<d_PartnerFatigueReal> _addFatigueHook;
        private static d_PartnerFatigueReal _addFatigueDetour;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            int applied = 0;

            // Hunger: block satiety decrease (negative AddSatiety calls)
            applied += TryPatch(harmony, typeof(PlayerData.PartnerData), "AddSatiety",
                new Type[] { typeof(int) }, nameof(AddSatiety_Prefix), "PartnerData.AddSatiety");

            // Hunger: block meal request updates
            applied += TryPatch(harmony, typeof(PartnerCtrl), "_UpdateReqMeal",
                null, nameof(UpdateReqMeal_Prefix), "PartnerCtrl._UpdateReqMeal");

            // Toilet: block toilet request updates
            applied += TryPatch(harmony, typeof(PartnerCtrl), "_UpdateReqToilet",
                null, nameof(UpdateReqToilet_Prefix), "PartnerCtrl._UpdateReqToilet");

            // Fatigue: block on PartnerCtrl (field/battle path via Harmony)
            applied += TryPatch(harmony, typeof(PartnerCtrl), "AddFatigue",
                new Type[] { typeof(int) }, nameof(AddFatigue_Prefix), "PartnerCtrl.AddFatigue");

            // Fatigue: block sleep request updates
            applied += TryPatch(harmony, typeof(PartnerCtrl), "_UpdateReqSleep",
                null, nameof(UpdateReqSleep_Prefix), "PartnerCtrl._UpdateReqSleep");

            // Sickness: block all status effect requests (Injury, SeriousInjury, Disease)
            applied += TryPatch(harmony, typeof(PartnerCtrl), "RequestBeginFSEffect",
                new Type[] { typeof(PartnerCtrl.FieldStatusEffect) },
                nameof(RequestBeginFSEffect_Prefix), "PartnerCtrl.RequestBeginFSEffect");

            DebugLogger.Log($"[CareMechanicsPatch] {applied}/6 Harmony patches applied");

            // Fatigue: NativeHooks for training/scenario path (CSVB bypasses managed code)
            ApplyNativeHooks();
        }

        /// <summary>
        /// Resets care values for both partners when a setting is toggled ON.
        /// Without this, existing hunger/fatigue/toilet/sickness values would persist
        /// even though new increases are blocked.
        /// </summary>
        public static void ResetHunger()
        {
            ForEachPartnerData(pd =>
            {
                pd.m_satiety = 100;
                pd.m_isReqMeal = false;
            });
            DebugLogger.Log("[CareMechanicsPatch] Reset hunger for all partners");
        }

        public static void ResetToilet()
        {
            ForEachPartnerData(pd =>
            {
                pd.m_toiletTime = 0f;
                pd.m_isReqToilet = false;
            });
            DebugLogger.Log("[CareMechanicsPatch] Reset toilet for all partners");
        }

        public static void ResetFatigue()
        {
            ForEachPartnerData(pd =>
            {
                pd.m_fatigue = 0;
                pd.m_isReqSleep = false;
            });
            DebugLogger.Log("[CareMechanicsPatch] Reset fatigue for all partners");
        }

        public static void ResetSickness()
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var ctrl = MainGameManager.GetPartnerCtrl(i);
                    if (ctrl == null) continue;
                    ctrl.ImmediateEndFSEffect(PartnerCtrl.FieldStatusEffect.Injury);
                    ctrl.ImmediateEndFSEffect(PartnerCtrl.FieldStatusEffect.SeriousInjury);
                    ctrl.ImmediateEndFSEffect(PartnerCtrl.FieldStatusEffect.Disease);
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[CareMechanicsPatch] Could not reset sickness for partner {i}: {ex.Message}");
                }
            }
            DebugLogger.Log("[CareMechanicsPatch] Cleared sickness/injury for all partners");
        }

        private static void ForEachPartnerData(Action<PlayerData.PartnerData> action)
        {
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    var pd = StorageData.GetPartnerData((AppInfo.PARTNER_NO)i);
                    if (pd != null) action(pd);
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[CareMechanicsPatch] Could not access partner data {i}: {ex.Message}");
                }
            }
        }

        private static void ApplyNativeHooks()
        {
            try
            {
                var module = Process.GetCurrentProcess().Modules
                    .Cast<ProcessModule>()
                    .FirstOrDefault(m => m.ModuleName == "GameAssembly.dll");

                if (module == null)
                {
                    DebugLogger.Warning("[CareMechanicsPatch] Could not find GameAssembly.dll for native hooks");
                    return;
                }

                IntPtr addAddr = module.BaseAddress + AddPartnerFatigue_RVA;
                _addFatigueDetour = new d_PartnerFatigueReal(Hook_AddPartnerFatigue);
                IntPtr addDetourPtr = Marshal.GetFunctionPointerForDelegate(_addFatigueDetour);
                _addFatigueHook = new NativeHook<d_PartnerFatigueReal>(addAddr, addDetourPtr);
                _addFatigueHook.Attach();
                DebugLogger.Log($"[CareMechanicsPatch] Native hook: _AddPartnerFatigue_REAL at GameAssembly.dll+0x{AddPartnerFatigue_RVA:X}");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[CareMechanicsPatch] Error applying native fatigue hooks: {ex.Message}");
            }
        }

        private static void Hook_AddPartnerFatigue(IntPtr partnerDataPtr, int value)
        {
            if (ModSettings.DisableFatigue && value > 0)
                return;
            _addFatigueHook.Trampoline(partnerDataPtr, value);
        }

        private static int TryPatch(HarmonyLib.Harmony harmony, Type targetType, string methodName,
            Type[] paramTypes, string prefixName, string label)
        {
            try
            {
                var method = paramTypes != null
                    ? AccessTools.Method(targetType, methodName, paramTypes)
                    : AccessTools.Method(targetType, methodName);
                if (method == null)
                {
                    DebugLogger.Warning($"[CareMechanicsPatch] Could not find {label}");
                    return 0;
                }
                harmony.Patch(method, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(CareMechanicsPatch), prefixName)));
                DebugLogger.Log($"[CareMechanicsPatch] Patched {label}");
                return 1;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[CareMechanicsPatch] Failed to patch {label}: {ex.Message}");
                return 0;
            }
        }

        private static bool AddSatiety_Prefix(int _value)
        {
            if (ModSettings.DisableHunger && _value < 0)
                return false;
            return true;
        }

        private static bool UpdateReqMeal_Prefix()
        {
            return !ModSettings.DisableHunger;
        }

        private static bool UpdateReqToilet_Prefix()
        {
            return !ModSettings.DisableToilet;
        }

        private static bool AddFatigue_Prefix(int addValue)
        {
            if (ModSettings.DisableFatigue && addValue > 0)
                return false;
            return true;
        }

        private static bool UpdateReqSleep_Prefix()
        {
            return !ModSettings.DisableFatigue;
        }

        private static bool RequestBeginFSEffect_Prefix(PartnerCtrl.FieldStatusEffect _effect)
        {
            if (ModSettings.DisableSickness && _effect != PartnerCtrl.FieldStatusEffect.None)
                return false;
            return true;
        }
    }
}
