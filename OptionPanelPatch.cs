using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patches to inject an "Accessibility" item into the game's options TOP panel.
    /// When selected, activates AccessibilityMenuHandler instead of a game subpanel.
    ///
    /// Strategy: extend m_CommandInfoArray and m_DataMax for cursor range, AND add a
    /// dummy uOptionPanelItemVoid to m_items so the game's native confirm path works.
    /// Use PREFIX/POSTFIX sandwich on ItemSetUp: PREFIX removes our m_items entry before
    /// the game rebuilds, POSTFIX adds it back. This prevents our entry from being
    /// treated as a visual item (which would displace Agreement/Quit).
    /// SetMainSettingState_Prefix intercepts the confirm and activates our menu.
    /// </summary>
    public static class OptionPanelPatch
    {
        public static int AccessibilityItemIndex { get; private set; } = -1;

        private static uOptionPanel _optionPanel;
        private static uOptionTopPanelCommand _topPanel;
        private static GameObject _accessibilityGo;
        private static uOptionPanelItemVoid _dummyItem;

        /// <summary>
        /// Time-based input suppression after returning from accessibility menu.
        /// Blocks CheckInputKey until the timer expires, ensuring the cancel press
        /// doesn't propagate regardless of script execution order.
        /// </summary>
        private static float _suppressUntilTime;

        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                var itemSetUpMethod = AccessTools.Method(typeof(uOptionTopPanelCommand), "ItemSetUp");
                if (itemSetUpMethod != null)
                {
                    harmony.Patch(itemSetUpMethod,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(OptionPanelPatch), nameof(ItemSetUp_Prefix))),
                        postfix: new HarmonyMethod(AccessTools.Method(typeof(OptionPanelPatch), nameof(ItemSetUp_Postfix))));
                    DebugLogger.Log("[OptionPanelPatch] Patched uOptionTopPanelCommand.ItemSetUp (prefix+postfix)");
                }
                else
                {
                    DebugLogger.Warning("[OptionPanelPatch] Could not find uOptionTopPanelCommand.ItemSetUp");
                }

                var setStateMethod = AccessTools.Method(typeof(uOptionPanel), "SetMainSettingState",
                    new Type[] { typeof(uOptionPanel.MainSettingState) });
                if (setStateMethod != null)
                {
                    harmony.Patch(setStateMethod,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(OptionPanelPatch), nameof(SetMainSettingState_Prefix))));
                    DebugLogger.Log("[OptionPanelPatch] Patched uOptionPanel.SetMainSettingState");
                }
                else
                {
                    DebugLogger.Warning("[OptionPanelPatch] Could not find uOptionPanel.SetMainSettingState");
                }

                var checkInputMethod = AccessTools.Method(typeof(uOptionPanel), "CheckInputKey");
                if (checkInputMethod != null)
                {
                    harmony.Patch(checkInputMethod,
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(OptionPanelPatch), nameof(CheckInputKey_Prefix))));
                    DebugLogger.Log("[OptionPanelPatch] Patched uOptionPanel.CheckInputKey");
                }
                else
                {
                    DebugLogger.Warning("[OptionPanelPatch] Could not find uOptionPanel.CheckInputKey");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[OptionPanelPatch] Apply error: {ex.Message}");
            }
        }

        /// <summary>
        /// Before the game rebuilds its item list, remove our dummy entry from m_items.
        /// This prevents the game from treating our entry as a visual item.
        /// </summary>
        private static void ItemSetUp_Prefix(uOptionTopPanelCommand __instance)
        {
            try
            {
                if (_dummyItem == null)
                    return;

                var items = __instance.m_items;
                if (items == null)
                    return;

                // Remove our dummy item if present
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    var item = items[i];
                    if (item != null && item.Pointer == _dummyItem.Pointer)
                    {
                        items.RemoveAt(i);
                        break;
                    }
                }

                // Shrink CommandInfoArray back to game-only size
                // (our postfix extended it last time; the game reuses it otherwise)
                int gameItemCount = items.Count;
                var array = __instance.m_CommandInfoArray;
                if (array != null && array.Length > gameItemCount)
                {
                    var trimmed = new Il2CppReferenceArray<uOptionTopPanelCommand.CommandInfo>(gameItemCount);
                    for (int i = 0; i < gameItemCount; i++)
                        trimmed[i] = array[i];
                    __instance.m_CommandInfoArray = trimmed;
                }

                // Reset m_DataMax to match
                var cursorCtrl = __instance.m_KeyCursorController;
                if (cursorCtrl != null)
                    cursorCtrl.m_DataMax = gameItemCount;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[OptionPanelPatch] ItemSetUp_Prefix error: {ex.Message}");
            }
        }

        /// <summary>
        /// After the game builds its items, extend the array with our entry
        /// and add a dummy uOptionPanelItemVoid to m_items for the confirm path.
        /// </summary>
        private static void ItemSetUp_Postfix(uOptionTopPanelCommand __instance)
        {
            try
            {
                _topPanel = __instance;

                var cursorCtrl = __instance.m_KeyCursorController;
                if (cursorCtrl == null)
                {
                    DebugLogger.Warning("[OptionPanelPatch] No KeyCursorController");
                    return;
                }

                var array = __instance.m_CommandInfoArray;
                if (array == null || array.Length == 0)
                {
                    DebugLogger.Warning("[OptionPanelPatch] m_CommandInfoArray is null/empty");
                    return;
                }

                // Create standalone GO and dummy item once
                if (_accessibilityGo == null)
                {
                    _accessibilityGo = new GameObject("AccessibilityOptionStandalone");
                    UnityEngine.Object.DontDestroyOnLoad(_accessibilityGo);
                    _accessibilityGo.SetActive(false);
                    DebugLogger.Log("[OptionPanelPatch] Created standalone Accessibility GO");
                }

                if (_dummyItem == null)
                {
                    _dummyItem = _accessibilityGo.AddComponent<uOptionPanelItemVoid>();
                    // State doesn't matter — SetMainSettingState_Prefix intercepts
                    // based on cursor position, not the state value
                    _dummyItem.m_settingState = uOptionPanel.MainSettingState.TOP;
                    DebugLogger.Log("[OptionPanelPatch] Created dummy uOptionPanelItemVoid");
                }

                int gameArrayLen = array.Length;

                // Calculate cursor position from last game item
                int lastIndex = gameArrayLen - 1;
                var lastInfo = array[lastIndex];
                var cursorPos = lastInfo.m_CommandCursorPos;
                if (gameArrayLen >= 2)
                {
                    var prevCursorPos = array[lastIndex - 1].m_CommandCursorPos;
                    float spacing = lastInfo.m_CommandCursorPos.y - prevCursorPos.y;
                    cursorPos.y += spacing;
                }

                var newInfo = new uOptionTopPanelCommand.CommandInfo();
                newInfo.m_CommandObject = _accessibilityGo;
                newInfo.m_CommandCursorPos = cursorPos;

                // Extend CommandInfoArray with our item at the end
                var newArray = new Il2CppReferenceArray<uOptionTopPanelCommand.CommandInfo>(gameArrayLen + 1);
                for (int i = 0; i < gameArrayLen; i++)
                    newArray[i] = array[i];
                newArray[gameArrayLen] = newInfo;

                __instance.m_CommandInfoArray = newArray;
                AccessibilityItemIndex = gameArrayLen;
                cursorCtrl.m_DataMax = gameArrayLen + 1;

                // Add dummy item to m_items so native CheckInputKey can read it on confirm
                var items = __instance.m_items;
                if (items != null)
                {
                    items.Add(_dummyItem.Cast<uOptionPanelItemBase>());
                    DebugLogger.Log($"[OptionPanelPatch] POSTFIX: Added dummy to m_items (count: {items.Count}), array index {AccessibilityItemIndex}, m_DataMax: {gameArrayLen + 1}");
                }

                DebugLogger.Log($"[OptionPanelPatch] Appended Accessibility at index {AccessibilityItemIndex} (game array: {gameArrayLen}, m_DataMax: {gameArrayLen + 1})");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[OptionPanelPatch] ItemSetUp_Postfix error: {ex.Message}");
            }
        }

        /// <summary>
        /// Intercept SetMainSettingState when cursor is on our index.
        /// The game's native CheckInputKey reads m_items[ourIndex].m_settingState → calls this.
        /// We suppress the state change and activate the accessibility menu instead.
        /// </summary>
        private static bool SetMainSettingState_Prefix(uOptionPanel __instance, uOptionPanel.MainSettingState state)
        {
            try
            {
                _optionPanel = __instance;

                if (__instance.m_MainSettingState != uOptionPanel.MainSettingState.TOP)
                    return true;

                if (AccessibilityItemIndex < 0)
                    return true;

                var topPanel = GetTopPanel(__instance);
                if (topPanel == null)
                    return true;

                var cursorCtrl = topPanel.m_KeyCursorController;
                if (cursorCtrl == null)
                    return true;

                if (cursorCtrl.m_DataIndex != AccessibilityItemIndex)
                    return true;

                // Cursor is on our item — suppress the game's state change and open our menu
                DebugLogger.Log("[OptionPanelPatch] SetMainSettingState intercepted for Accessibility index");

                topPanel.disablePanel();

                var handler = AccessibilityMenuHandler.Instance;
                if (handler != null)
                    handler.Activate();
                else
                    DebugLogger.Error("[OptionPanelPatch] AccessibilityMenuHandler.Instance is null!");

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[OptionPanelPatch] SetMainSettingState_Prefix error: {ex.Message}");
                return true;
            }
        }

        /// <summary>
        /// Blocks the game's CheckInputKey when:
        /// 1. Our accessibility menu is open
        /// 2. One-frame suppress after returning from our menu
        /// </summary>
        private static bool CheckInputKey_Prefix(uOptionPanel __instance)
        {
            try
            {
                _optionPanel = __instance;

                // Block all game input while our menu is open
                if (AccessibilityMenuHandler.Instance != null && AccessibilityMenuHandler.Instance.IsOpen())
                    return false;

                // Suppress input briefly after returning from our menu
                // (prevents the cancel press from closing the whole options panel)
                if (Time.unscaledTime < _suppressUntilTime)
                    return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[OptionPanelPatch] CheckInputKey_Prefix error: {ex.Message}");
            }
            return true;
        }

        /// <summary>
        /// Called by AccessibilityMenuHandler when the user presses Cancel at the category level.
        /// Returns to the game's TOP menu and suppresses one frame of input.
        /// </summary>
        public static void ReturnToTopMenu()
        {
            try
            {
                if (_optionPanel == null)
                    _optionPanel = UnityEngine.Object.FindObjectOfType<uOptionPanel>();

                if (_optionPanel == null)
                {
                    DebugLogger.Warning("[OptionPanelPatch] Cannot return to TOP - no uOptionPanel found");
                    return;
                }

                var topPanel = GetTopPanel(_optionPanel);
                if (topPanel != null)
                {
                    topPanel.enablePanel(true, false);

                    if (AccessibilityItemIndex >= 0)
                        topPanel.SetCursor(AccessibilityItemIndex);
                }

                // Suppress CheckInputKey briefly as extra safety
                _suppressUntilTime = Time.unscaledTime + 0.2f;

                // Announce return to TOP menu (matches game's announcement when
                // backing out of Key Config/Graphics/etc.)
                int total = topPanel?.m_KeyCursorController != null ? topPanel.m_KeyCursorController.m_DataMax : 0;
                int index = AccessibilityItemIndex + 1; // 1-based for speech
                ScreenReader.Say($"System Menu. Accessibility, {index} of {total}");

                DebugLogger.Log("[OptionPanelPatch] Returned to TOP menu");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[OptionPanelPatch] ReturnToTopMenu error: {ex.Message}");
            }
        }

        private static uOptionTopPanelCommand GetTopPanel(uOptionPanel optionPanel)
        {
            try
            {
                if (_topPanel != null)
                    return _topPanel;

                var panels = optionPanel.m_uOptionPanelCommand;
                if (panels == null || panels.Length == 0)
                    return null;

                var topBase = panels[(int)uOptionPanel.MainSettingState.TOP];
                return topBase?.TryCast<uOptionTopPanelCommand>();
            }
            catch
            {
                return null;
            }
        }
    }
}
