using Il2CppInterop.Runtime;
using MelonLoader;
using MelonLoader.NativeUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// Native hook on MainGameManager.EnableAreaChangeTrigger to intercept
    /// the game's enable/disable calls for area change transitions.
    ///
    /// In IL2CPP games, internal native code calls compiled functions directly,
    /// bypassing managed wrappers. We hook the actual native function using
    /// MelonLoader's NativeHook at the compiled function address.
    /// </summary>
    public static class AreaChangePatch
    {
        // RVA of the ACTUAL EnableAreaChangeTrigger implementation in GameAssembly.dll.
        // 0x25DA20 is the managed wrapper (just a thunk, never called by game code).
        // 0x25DAA0 is the real implementation called by CmdExeEnableAreaChange in CSVB scripts.
        private const int EnableAreaChangeTrigger_RVA = 0x25DAA0;

        // Tracks enabled/disabled state per transition ID.
        // Key = transition GameObject name (e.g. "AC0110_0160")
        // Value = true if enabled, false if disabled
        private static readonly Dictionary<string, bool> _transitionStates = new Dictionary<string, bool>();

        // IL2CPP instance method calling convention on Windows x64:
        // void Func(thisPtr, Il2CppString* _id, bool _isEnable, MethodInfo* method)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void d_EnableAreaChangeTrigger(IntPtr thisPtr, IntPtr id, byte isEnable, IntPtr methodInfo);

        private static NativeHook<d_EnableAreaChangeTrigger> _hook;

        // Static ref to prevent GC of the detour delegate
        private static d_EnableAreaChangeTrigger _detourDelegate;

        /// <summary>
        /// Returns true if the transition is known to be disabled.
        /// Returns false if enabled or unknown (unknown = allow by default).
        /// </summary>
        public static bool IsDisabled(string transitionId)
        {
            if (string.IsNullOrEmpty(transitionId))
                return false;
            return _transitionStates.TryGetValue(transitionId, out bool enabled) && !enabled;
        }

        /// <summary>
        /// Returns true if the transition has been seen by the hook (regardless of enabled/disabled).
        /// </summary>
        public static bool IsTracked(string transitionId)
        {
            if (string.IsNullOrEmpty(transitionId))
                return false;
            return _transitionStates.ContainsKey(transitionId);
        }

        /// <summary>
        /// Returns the number of tracked transitions (for debug logging).
        /// </summary>
        public static int TrackedCount => _transitionStates.Count;

        /// <summary>
        /// Clears all tracked state.
        /// </summary>
        public static void Clear()
        {
            _transitionStates.Clear();
        }

        /// <summary>
        /// Apply the native hook.
        /// </summary>
        public static void Apply()
        {
            try
            {
                // Find GameAssembly.dll base address
                var module = Process.GetCurrentProcess().Modules
                    .Cast<ProcessModule>()
                    .FirstOrDefault(m => m.ModuleName == "GameAssembly.dll");

                if (module == null)
                {
                    DebugLogger.Warning("[AreaChangePatch] Could not find GameAssembly.dll module");
                    return;
                }

                IntPtr funcAddr = module.BaseAddress + EnableAreaChangeTrigger_RVA;

                // Create detour delegate and pin it
                _detourDelegate = new d_EnableAreaChangeTrigger(Hook_EnableAreaChangeTrigger);
                IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(_detourDelegate);

                // Create and attach the native hook
                _hook = new NativeHook<d_EnableAreaChangeTrigger>(funcAddr, detourPtr);
                _hook.Attach();

                DebugLogger.Log($"[AreaChangePatch] Native hook applied at GameAssembly.dll+0x{EnableAreaChangeTrigger_RVA:X}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AreaChangePatch] Error applying native hook: {ex.Message}");
            }
        }

        private static void Hook_EnableAreaChangeTrigger(IntPtr thisPtr, IntPtr id, byte isEnable, IntPtr methodInfo)
        {
            try
            {
                if (id != IntPtr.Zero)
                {
                    string idStr = IL2CPP.Il2CppStringToManaged(id);
                    if (!string.IsNullOrEmpty(idStr))
                    {
                        bool enabled = isEnable != 0;
                        _transitionStates[idStr] = enabled;
                        DebugLogger.Log($"[AreaChangePatch] {idStr} -> {(enabled ? "ENABLED" : "DISABLED")} (tracking {_transitionStates.Count})");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AreaChangePatch] Hook error: {ex.Message}");
            }

            // Always call the original function
            _hook.Trampoline(thisPtr, id, isEnable, methodInfo);
        }
    }
}
