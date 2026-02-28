using MelonLoader.NativeUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// Native hooks on CScenarioScript.CmdExeStartPickItemCounter and
    /// CmdExeCancelItemCounter - the actual CSVB command handlers that fire
    /// when a PICK_ quest is accepted or canceled.
    ///
    /// The CSVB dispatch system is entirely native - it calls native function
    /// pointers from a command table, bypassing all managed IL2CPP wrappers.
    /// Neither Harmony nor managed-level hooks can intercept these calls.
    /// NativeHook on the actual function entry points is required.
    ///
    /// RVAs confirmed via Ghidra decompilation:
    /// - CmdExeStartPickItemCounter: FUN_180286350 at RVA 0x286350 (668 bytes)
    /// - CmdExeCancelItemCounter: FUN_180278b60 at RVA 0x278B60 (180 bytes)
    /// </summary>
    public static class QuestItemCounterPatch
    {
        // Native RVAs (from Ghidra, NOT from script.json which has managed wrapper offsets)
        private const int StartPick_RVA = 0x286350;
        private const int CancelPick_RVA = 0x278B60;

        // Tracking active quest item IDs
        private static readonly HashSet<uint> _activeItemIds = new HashSet<uint>();
        private static bool _pickCounterStarted = false;

        // Native signatures from Ghidra decompilation:
        // undefined8 FUN_180286350(longlong param_1, undefined8 param_2)
        // undefined8 FUN_180278b60(undefined8 param_1, undefined8 param_2)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long d_CmdExe(IntPtr thisPtr, long vrec);

        private static NativeHook<d_CmdExe> _startHook;
        private static NativeHook<d_CmdExe> _cancelHook;

        // Pin delegates to prevent GC
        private static d_CmdExe _startDetour;
        private static d_CmdExe _cancelDetour;

        /// <summary>
        /// Returns true if a pick item counter has been started this session.
        /// </summary>
        public static bool HasActivePickCounter => _pickCounterStarted;

        /// <summary>
        /// Returns the set of all currently active quest item IDs.
        /// </summary>
        public static IReadOnlyCollection<uint> ActiveItemIds => _activeItemIds;

        /// <summary>
        /// Returns true if the given item ID is currently an active quest pick item.
        /// </summary>
        public static bool IsQuestItemActive(uint itemId)
        {
            return _activeItemIds.Contains(itemId);
        }

        /// <summary>
        /// Apply native hooks on the CSVB command handler functions.
        /// </summary>
        public static void Apply()
        {
            try
            {
                var module = Process.GetCurrentProcess().Modules
                    .Cast<ProcessModule>()
                    .FirstOrDefault(m => m.ModuleName == "GameAssembly.dll");

                if (module == null)
                {
                    DebugLogger.Warning("[QuestItemCounterPatch] Could not find GameAssembly.dll module");
                    return;
                }

                IntPtr baseAddr = module.BaseAddress;

                // Hook CmdExeStartPickItemCounter
                _startDetour = new d_CmdExe(Hook_StartPick);
                IntPtr startPtr = Marshal.GetFunctionPointerForDelegate(_startDetour);
                _startHook = new NativeHook<d_CmdExe>(baseAddr + StartPick_RVA, startPtr);
                _startHook.Attach();

                // Hook CmdExeCancelItemCounter
                _cancelDetour = new d_CmdExe(Hook_CancelPick);
                IntPtr cancelPtr = Marshal.GetFunctionPointerForDelegate(_cancelDetour);
                _cancelHook = new NativeHook<d_CmdExe>(baseAddr + CancelPick_RVA, cancelPtr);
                _cancelHook.Attach();

                DebugLogger.Log($"[QuestItemCounterPatch] Native hooks applied: Start=0x{StartPick_RVA:X}, Cancel=0x{CancelPick_RVA:X}");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[QuestItemCounterPatch] Error applying hooks: {ex.Message}");
            }
        }

        private static long Hook_StartPick(IntPtr thisPtr, long vrec)
        {
            try
            {
                _pickCounterStarted = true;
                DebugLogger.Log($"[QuestItemCounterPatch] === StartPickItemCounter FIRED === thisPtr=0x{thisPtr.ToInt64():X}");

                // Read active quest items from StorageData after the original executes
                // (we'll log before and after to see what changes)
                LogQuestItemCounterState("BEFORE start");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[QuestItemCounterPatch] Start hook error: {ex.Message}");
            }

            long result = _startHook.Trampoline(thisPtr, vrec);

            try
            {
                LogQuestItemCounterState("AFTER start");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[QuestItemCounterPatch] Post-start error: {ex.Message}");
            }

            return result;
        }

        private static long Hook_CancelPick(IntPtr thisPtr, long vrec)
        {
            try
            {
                DebugLogger.Log($"[QuestItemCounterPatch] === CancelItemCounter FIRED ===");
                LogQuestItemCounterState("BEFORE cancel");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[QuestItemCounterPatch] Cancel hook error: {ex.Message}");
            }

            long result = _cancelHook.Trampoline(thisPtr, vrec);

            try
            {
                // Rebuild active set after cancel
                _activeItemIds.Clear();
                LogQuestItemCounterState("AFTER cancel");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[QuestItemCounterPatch] Post-cancel error: {ex.Message}");
            }

            return result;
        }

        private static void LogQuestItemCounterState(string context)
        {
            try
            {
                var counter = Il2Cpp.StorageData.m_QuestItemCounter;
                if (counter == null || counter.m_QuestItemCount == null)
                {
                    DebugLogger.Log($"[QuestItemCounterPatch] {context}: counter or list is null");
                    return;
                }

                int count = counter.m_QuestItemCount.Count;
                DebugLogger.Log($"[QuestItemCounterPatch] {context}: {count} entries");
                for (int i = 0; i < count; i++)
                {
                    var info = counter.m_QuestItemCount[i];
                    if (info != null)
                    {
                        _activeItemIds.Add(info.m_ItemId);
                        DebugLogger.Log($"[QuestItemCounterPatch]   [{i}] itemId={info.m_ItemId}, count={info.m_Count}/{info.m_Quota}, flagSetId={info.m_FlagSetId}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[QuestItemCounterPatch] LogState error ({context}): {ex.Message}");
            }
        }
    }
}
