using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Resolves game button icon tags (e.g. &lt;icon&gt;○&lt;/icon&gt;) to readable button names
    /// based on the player's last active input device (keyboard, PlayStation, or Xbox controller).
    /// </summary>
    public static class ButtonIconResolver
    {
        public enum InputDevice
        {
            Keyboard,
            PlayStationController,
            XboxController
        }

        /// <summary>
        /// The last detected input device. Defaults to Keyboard.
        /// </summary>
        public static InputDevice LastInput { get; private set; } = InputDevice.Keyboard;

        // PlayStation symbol → KeyConfigDataKind mapping
        // Game uses Japanese convention: ○=confirm, ×=cancel in icons,
        // but Western players press Cross/A for confirm, Circle/B for cancel.
        private static readonly Dictionary<string, OptionData.KeyConfigDataKind> _symbolToConfigKind = new Dictionary<string, OptionData.KeyConfigDataKind>
        {
            { "\u25CB", OptionData.KeyConfigDataKind.A },   // ○ (confirm icon) → A (Cross/A button)
            { "\u00D7", OptionData.KeyConfigDataKind.B },   // × (cancel icon) → B (Circle/B button)
            { "\u25B3", OptionData.KeyConfigDataKind.Y },   // △ Triangle → Y
            { "\u25A1", OptionData.KeyConfigDataKind.X },   // □ Square → X
            { "L", OptionData.KeyConfigDataKind.L },
            { "R", OptionData.KeyConfigDataKind.R },
        };

        // PlayStation controller display names
        private static readonly Dictionary<string, string> _psNames = new Dictionary<string, string>
        {
            { "\u25CB", "Cross" },     // ○ confirm icon → Cross button
            { "\u00D7", "Circle" },    // × cancel icon → Circle button
            { "\u25B3", "Triangle" },
            { "\u25A1", "Square" },
            { "L", "L" },
            { "R", "R" },
        };

        // Xbox controller display names
        private static readonly Dictionary<string, string> _xboxNames = new Dictionary<string, string>
        {
            { "\u25CB", "A" },         // ○ confirm icon → A button
            { "\u00D7", "B" },         // × cancel icon → B button
            { "\u25B3", "Y" },
            { "\u25A1", "X" },
            { "L", "LB" },
            { "R", "RB" },
        };

        // Cached keyboard key names (symbol → readable key name)
        private static Dictionary<string, string> _cachedKeyboardNames = null;

        /// <summary>
        /// Update last active input device. Call once per frame from ModInputManager.Update().
        /// </summary>
        public static void UpdateInputDevice()
        {
            // Check SDL controller first
            if (SDLController.IsAvailable)
            {
                // Check if any controller button is held this frame
                if (IsAnyControllerButtonActive())
                {
                    LastInput = IsPlayStationController() ? InputDevice.PlayStationController : InputDevice.XboxController;
                    return;
                }
            }

            // Check keyboard input
            if (Input.anyKeyDown)
            {
                // Input.anyKeyDown includes mouse buttons and controller buttons via Unity,
                // so only set keyboard if SDL controller didn't claim the input
                if (!SDLController.IsAvailable || !IsAnyControllerButtonActive())
                {
                    LastInput = InputDevice.Keyboard;
                }
            }
        }

        /// <summary>
        /// Resolve an icon tag's content to a readable button name.
        /// </summary>
        public static string ResolveIconTag(string iconContent)
        {
            if (string.IsNullOrEmpty(iconContent))
                return "";

            string trimmed = iconContent.Trim();

            switch (LastInput)
            {
                case InputDevice.PlayStationController:
                    if (_psNames.TryGetValue(trimmed, out string psName))
                        return psName;
                    break;

                case InputDevice.XboxController:
                    if (_xboxNames.TryGetValue(trimmed, out string xboxName))
                        return xboxName;
                    break;

                case InputDevice.Keyboard:
                    return ResolveKeyboardName(trimmed);
            }

            // Unknown symbol - log and return as-is
            DebugLogger.Log($"[ButtonIconResolver] Unknown icon symbol: '{trimmed}' (U+{((int)trimmed[0]):X4})");
            return trimmed;
        }

        private static string ResolveKeyboardName(string symbol)
        {
            // Try cached first
            if (_cachedKeyboardNames != null && _cachedKeyboardNames.TryGetValue(symbol, out string cached))
                return cached;

            // Look up from game's key config
            if (!_symbolToConfigKind.TryGetValue(symbol, out var configKind))
            {
                DebugLogger.Log($"[ButtonIconResolver] Unknown icon symbol for keyboard lookup: '{symbol}'");
                return symbol;
            }

            string keyName = GetKeyboardKeyName(configKind);
            if (keyName != null)
            {
                // Cache it
                if (_cachedKeyboardNames == null)
                    _cachedKeyboardNames = new Dictionary<string, string>();
                _cachedKeyboardNames[symbol] = keyName;
                return keyName;
            }

            // Fallback to PlayStation name if keyboard lookup fails
            if (_psNames.TryGetValue(symbol, out string fallback))
                return fallback;

            return symbol;
        }

        private static string GetKeyboardKeyName(OptionData.KeyConfigDataKind configKind)
        {
            try
            {
                var padManager = PadManager.m_instance;
                if (padManager == null) return null;

                var padArray = padManager.m_PadArray;
                if (padArray == null || padArray.Length == 0) return null;

                var pad = padArray[0];
                if (pad == null) return null;

                // Try user-configured keys first, fall back to fixed defaults
                var configData = pad.m_keyboardConfigData ?? pad.m_fixKeyboardConfigData;
                if (configData == null) return null;

                int index = (int)configKind;
                if (index < 0 || index >= configData.Length) return null;

                short keyCodeValue = configData[index];
                if (keyCodeValue == 0) return null;

                KeyCode keyCode = (KeyCode)keyCodeValue;
                return FormatKeyCodeName(keyCode);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ButtonIconResolver] Error reading keyboard config: {ex.Message}");
                return null;
            }
        }

        private static string FormatKeyCodeName(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Escape";
                case KeyCode.Space: return "Space";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Delete: return "Delete";
                case KeyCode.Tab: return "Tab";
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.UpArrow: return "Up Arrow";
                case KeyCode.DownArrow: return "Down Arrow";
                case KeyCode.LeftArrow: return "Left Arrow";
                case KeyCode.RightArrow: return "Right Arrow";
                case KeyCode.Alpha0: return "0";
                case KeyCode.Alpha1: return "1";
                case KeyCode.Alpha2: return "2";
                case KeyCode.Alpha3: return "3";
                case KeyCode.Alpha4: return "4";
                case KeyCode.Alpha5: return "5";
                case KeyCode.Alpha6: return "6";
                case KeyCode.Alpha7: return "7";
                case KeyCode.Alpha8: return "8";
                case KeyCode.Alpha9: return "9";
                case KeyCode.Keypad0: return "Numpad 0";
                case KeyCode.Keypad1: return "Numpad 1";
                case KeyCode.Keypad2: return "Numpad 2";
                case KeyCode.Keypad3: return "Numpad 3";
                case KeyCode.Keypad4: return "Numpad 4";
                case KeyCode.Keypad5: return "Numpad 5";
                case KeyCode.Keypad6: return "Numpad 6";
                case KeyCode.Keypad7: return "Numpad 7";
                case KeyCode.Keypad8: return "Numpad 8";
                case KeyCode.Keypad9: return "Numpad 9";
                case KeyCode.KeypadEnter: return "Numpad Enter";
                case KeyCode.KeypadPlus: return "Numpad Plus";
                case KeyCode.KeypadMinus: return "Numpad Minus";
                case KeyCode.KeypadMultiply: return "Numpad Multiply";
                case KeyCode.KeypadDivide: return "Numpad Divide";
                case KeyCode.KeypadPeriod: return "Numpad Period";
                case KeyCode.CapsLock: return "Caps Lock";
                case KeyCode.Numlock: return "Num Lock";
                case KeyCode.ScrollLock: return "Scroll Lock";
                case KeyCode.Insert: return "Insert";
                case KeyCode.Home: return "Home";
                case KeyCode.End: return "End";
                case KeyCode.PageUp: return "Page Up";
                case KeyCode.PageDown: return "Page Down";
                case KeyCode.Mouse0: return "Left Click";
                case KeyCode.Mouse1: return "Right Click";
                case KeyCode.Mouse2: return "Middle Click";
                default: return keyCode.ToString();
            }
        }

        private static bool IsAnyControllerButtonActive()
        {
            // Check face buttons, shoulders, dpad, start/back
            for (int i = 0; i <= (int)SDLController.SDL_GameControllerButton.DPadRight; i++)
            {
                if (SDLController.IsButtonHeld((SDLController.SDL_GameControllerButton)i))
                    return true;
            }

            // Check triggers
            if (SDLController.IsLeftTriggerHeld() || SDLController.IsRightTriggerHeld())
                return true;

            return false;
        }

        private static bool IsPlayStationController()
        {
            string name = SDLController.ControllerName;
            if (string.IsNullOrEmpty(name))
                return false;

            string lower = name.ToLowerInvariant();
            return lower.Contains("ps") ||
                   lower.Contains("playstation") ||
                   lower.Contains("dualsense") ||
                   lower.Contains("dualshock") ||
                   lower.Contains("sony");
        }

        /// <summary>
        /// Clear cached keyboard names. Call if the user changes key config.
        /// </summary>
        public static void InvalidateKeyboardCache()
        {
            _cachedKeyboardNames = null;
        }
    }
}
