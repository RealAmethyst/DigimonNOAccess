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
    /// Native hooks on event trigger activation functions to intercept
    /// the game's enable/disable calls for event triggers (quests, pitfalls, NPCs, etc.).
    ///
    /// Hooks two functions that both toggle triggers by placement ID:
    /// - EventTriggerManager.ActiveTalkEventTrigger (talk/quest triggers via CmdExeActiveTalkTrigger)
    /// - MainGameManager.ActiveTriggerObject (general triggers via CmdExeActiveNpc)
    /// Both feed into the same state dictionary keyed by placement ID.
    /// </summary>
    public static class EventTriggerPatch
    {
        // RVAs from Il2CppDumper script.json:
        private const int ActiveTalkEventTrigger_RVA = 0x41EB90;  // EventTriggerManager$$ActiveTalkEventTrigger
        private const int ActiveTriggerObject_RVA = 0x25CCF0;     // MainGameManager$$ActiveTriggerObject

        // Tracks active/inactive state per placement ID.
        // Key = placement ID (EventTriggerScript.m_EventParamId)
        // Value = true if active, false if inactive
        private static readonly Dictionary<uint, bool> _triggerStates = new Dictionary<uint, bool>();

        // Both functions share the same signature on x64:
        // void Func(thisPtr, uint32_t _placementId, bool _isActive, MethodInfo* method)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void d_ActiveTrigger(IntPtr thisPtr, uint placementId, byte isActive, IntPtr methodInfo);

        private static NativeHook<d_ActiveTrigger> _talkHook;
        private static NativeHook<d_ActiveTrigger> _objectHook;

        // Static refs to prevent GC of the detour delegates
        private static d_ActiveTrigger _talkDetour;
        private static d_ActiveTrigger _objectDetour;

        /// <summary>
        /// Returns true if the trigger is known to be inactive.
        /// Returns false if active or unknown (unknown = allow by default).
        /// </summary>
        public static bool IsDisabled(uint placementId)
        {
            return _triggerStates.TryGetValue(placementId, out bool active) && !active;
        }

        /// <summary>
        /// Returns true if the trigger has been seen by either hook.
        /// </summary>
        public static bool IsTracked(uint placementId)
        {
            return _triggerStates.ContainsKey(placementId);
        }

        /// <summary>
        /// Returns the number of tracked triggers.
        /// </summary>
        public static int TrackedCount => _triggerStates.Count;

        /// <summary>
        /// Apply both native hooks.
        /// </summary>
        public static void Apply()
        {
            var module = Process.GetCurrentProcess().Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(m => m.ModuleName == "GameAssembly.dll");

            if (module == null)
            {
                DebugLogger.Warning("[EventTriggerPatch] Could not find GameAssembly.dll module");
                return;
            }

            // Hook EventTriggerManager.ActiveTalkEventTrigger
            try
            {
                IntPtr talkAddr = module.BaseAddress + ActiveTalkEventTrigger_RVA;
                _talkDetour = new d_ActiveTrigger(Hook_ActiveTalkEventTrigger);
                IntPtr talkPtr = Marshal.GetFunctionPointerForDelegate(_talkDetour);
                _talkHook = new NativeHook<d_ActiveTrigger>(talkAddr, talkPtr);
                _talkHook.Attach();
                DebugLogger.Log($"[EventTriggerPatch] ActiveTalkEventTrigger hook at GameAssembly.dll+0x{ActiveTalkEventTrigger_RVA:X}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[EventTriggerPatch] ActiveTalkEventTrigger hook error: {ex.Message}");
            }

            // Hook MainGameManager.ActiveTriggerObject
            try
            {
                IntPtr objAddr = module.BaseAddress + ActiveTriggerObject_RVA;
                _objectDetour = new d_ActiveTrigger(Hook_ActiveTriggerObject);
                IntPtr objPtr = Marshal.GetFunctionPointerForDelegate(_objectDetour);
                _objectHook = new NativeHook<d_ActiveTrigger>(objAddr, objPtr);
                _objectHook.Attach();
                DebugLogger.Log($"[EventTriggerPatch] ActiveTriggerObject hook at GameAssembly.dll+0x{ActiveTriggerObject_RVA:X}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[EventTriggerPatch] ActiveTriggerObject hook error: {ex.Message}");
            }
        }

        private static void Hook_ActiveTalkEventTrigger(IntPtr thisPtr, uint placementId, byte isActive, IntPtr methodInfo)
        {
            try
            {
                _triggerStates[placementId] = isActive != 0;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[EventTriggerPatch] Talk hook error: {ex.Message}");
            }
            _talkHook.Trampoline(thisPtr, placementId, isActive, methodInfo);
        }

        private static void Hook_ActiveTriggerObject(IntPtr thisPtr, uint placementId, byte isActive, IntPtr methodInfo)
        {
            try
            {
                _triggerStates[placementId] = isActive != 0;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[EventTriggerPatch] Object hook error: {ex.Message}");
            }
            _objectHook.Trampoline(thisPtr, placementId, isActive, methodInfo);
        }
    }
}
