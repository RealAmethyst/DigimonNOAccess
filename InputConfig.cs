using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Parses input binding strings from the config file.
    /// Supports readable button names for both keyboard and controller.
    /// </summary>
    public static class InputConfig
    {
        // Keyboard key name mappings
        private static readonly Dictionary<string, KeyCode> KeyboardMap = new Dictionary<string, KeyCode>(StringComparer.OrdinalIgnoreCase)
        {
            // Function keys
            { "F1", KeyCode.F1 }, { "F2", KeyCode.F2 }, { "F3", KeyCode.F3 },
            { "F4", KeyCode.F4 }, { "F5", KeyCode.F5 }, { "F6", KeyCode.F6 },
            { "F7", KeyCode.F7 }, { "F8", KeyCode.F8 }, { "F9", KeyCode.F9 },
            { "F10", KeyCode.F10 }, { "F11", KeyCode.F11 }, { "F12", KeyCode.F12 },

            // Number keys
            { "0", KeyCode.Alpha0 }, { "1", KeyCode.Alpha1 }, { "2", KeyCode.Alpha2 },
            { "3", KeyCode.Alpha3 }, { "4", KeyCode.Alpha4 }, { "5", KeyCode.Alpha5 },
            { "6", KeyCode.Alpha6 }, { "7", KeyCode.Alpha7 }, { "8", KeyCode.Alpha8 },
            { "9", KeyCode.Alpha9 },

            // Letter keys
            { "A", KeyCode.A }, { "B", KeyCode.B }, { "C", KeyCode.C }, { "D", KeyCode.D },
            { "E", KeyCode.E }, { "F", KeyCode.F }, { "G", KeyCode.G }, { "H", KeyCode.H },
            { "I", KeyCode.I }, { "J", KeyCode.J }, { "K", KeyCode.K }, { "L", KeyCode.L },
            { "M", KeyCode.M }, { "N", KeyCode.N }, { "O", KeyCode.O }, { "P", KeyCode.P },
            { "Q", KeyCode.Q }, { "R", KeyCode.R }, { "S", KeyCode.S }, { "T", KeyCode.T },
            { "U", KeyCode.U }, { "V", KeyCode.V }, { "W", KeyCode.W }, { "X", KeyCode.X },
            { "Y", KeyCode.Y }, { "Z", KeyCode.Z },

            // Special keys
            { "Space", KeyCode.Space },
            { "Enter", KeyCode.Return }, { "Return", KeyCode.Return },
            { "Escape", KeyCode.Escape }, { "Esc", KeyCode.Escape },
            { "Tab", KeyCode.Tab },
            { "Backspace", KeyCode.Backspace },
            { "Delete", KeyCode.Delete }, { "Del", KeyCode.Delete },
            { "Insert", KeyCode.Insert }, { "Ins", KeyCode.Insert },
            { "Home", KeyCode.Home },
            { "End", KeyCode.End },
            { "PageUp", KeyCode.PageUp }, { "PgUp", KeyCode.PageUp },
            { "PageDown", KeyCode.PageDown }, { "PgDn", KeyCode.PageDown },

            // Arrow keys
            { "Up", KeyCode.UpArrow }, { "UpArrow", KeyCode.UpArrow },
            { "Down", KeyCode.DownArrow }, { "DownArrow", KeyCode.DownArrow },
            { "Left", KeyCode.LeftArrow }, { "LeftArrow", KeyCode.LeftArrow },
            { "Right", KeyCode.RightArrow }, { "RightArrow", KeyCode.RightArrow },

            // Numpad
            { "Numpad0", KeyCode.Keypad0 }, { "Num0", KeyCode.Keypad0 },
            { "Numpad1", KeyCode.Keypad1 }, { "Num1", KeyCode.Keypad1 },
            { "Numpad2", KeyCode.Keypad2 }, { "Num2", KeyCode.Keypad2 },
            { "Numpad3", KeyCode.Keypad3 }, { "Num3", KeyCode.Keypad3 },
            { "Numpad4", KeyCode.Keypad4 }, { "Num4", KeyCode.Keypad4 },
            { "Numpad5", KeyCode.Keypad5 }, { "Num5", KeyCode.Keypad5 },
            { "Numpad6", KeyCode.Keypad6 }, { "Num6", KeyCode.Keypad6 },
            { "Numpad7", KeyCode.Keypad7 }, { "Num7", KeyCode.Keypad7 },
            { "Numpad8", KeyCode.Keypad8 }, { "Num8", KeyCode.Keypad8 },
            { "Numpad9", KeyCode.Keypad9 }, { "Num9", KeyCode.Keypad9 },
            { "NumpadPlus", KeyCode.KeypadPlus }, { "NumPlus", KeyCode.KeypadPlus },
            { "NumpadMinus", KeyCode.KeypadMinus }, { "NumMinus", KeyCode.KeypadMinus },
            { "NumpadMultiply", KeyCode.KeypadMultiply }, { "NumMul", KeyCode.KeypadMultiply },
            { "NumpadDivide", KeyCode.KeypadDivide }, { "NumDiv", KeyCode.KeypadDivide },
            { "NumpadEnter", KeyCode.KeypadEnter }, { "NumEnter", KeyCode.KeypadEnter },

            // Punctuation
            { "Minus", KeyCode.Minus }, { "-", KeyCode.Minus },
            { "Equals", KeyCode.Equals }, { "=", KeyCode.Equals },
            { "LeftBracket", KeyCode.LeftBracket }, { "[", KeyCode.LeftBracket },
            { "RightBracket", KeyCode.RightBracket }, { "]", KeyCode.RightBracket },
            { "Semicolon", KeyCode.Semicolon }, { ";", KeyCode.Semicolon },
            { "Quote", KeyCode.Quote }, { "'", KeyCode.Quote },
            { "Comma", KeyCode.Comma }, { ",", KeyCode.Comma },
            { "Period", KeyCode.Period }, { ".", KeyCode.Period },
            { "Slash", KeyCode.Slash }, { "/", KeyCode.Slash },
            { "Backslash", KeyCode.Backslash }, { "\\", KeyCode.Backslash },
            { "Tilde", KeyCode.BackQuote }, { "`", KeyCode.BackQuote },
        };

        // Controller button name mappings
        private static readonly Dictionary<string, ControllerButton> ControllerMap = new Dictionary<string, ControllerButton>(StringComparer.OrdinalIgnoreCase)
        {
            // Face buttons - Xbox names
            { "A", ControllerButton.A },
            { "B", ControllerButton.B },
            { "X", ControllerButton.X },
            { "Y", ControllerButton.Y },

            // Face buttons - PlayStation names
            { "Cross", ControllerButton.A },
            { "Circle", ControllerButton.B },
            { "Square", ControllerButton.X },
            { "Triangle", ControllerButton.Y },

            // Shoulder buttons - Xbox names
            { "LB", ControllerButton.LB },
            { "RB", ControllerButton.RB },
            { "LeftBumper", ControllerButton.LB },
            { "RightBumper", ControllerButton.RB },

            // Shoulder buttons - PlayStation names
            { "L1", ControllerButton.LB },
            { "R1", ControllerButton.RB },

            // Triggers - Xbox names
            { "LT", ControllerButton.LT },
            { "RT", ControllerButton.RT },
            { "LeftTrigger", ControllerButton.LT },
            { "RightTrigger", ControllerButton.RT },

            // Triggers - PlayStation names
            { "L2", ControllerButton.LT },
            { "R2", ControllerButton.RT },

            // D-Pad
            { "DPadUp", ControllerButton.DPadUp },
            { "DPadDown", ControllerButton.DPadDown },
            { "DPadLeft", ControllerButton.DPadLeft },
            { "DPadRight", ControllerButton.DPadRight },
            { "DUp", ControllerButton.DPadUp },
            { "DDown", ControllerButton.DPadDown },
            { "DLeft", ControllerButton.DPadLeft },
            { "DRight", ControllerButton.DPadRight },
            { "Up", ControllerButton.DPadUp },
            { "Down", ControllerButton.DPadDown },
            { "Left", ControllerButton.DPadLeft },
            { "Right", ControllerButton.DPadRight },

            // Special buttons
            { "Start", ControllerButton.Start },
            { "Select", ControllerButton.Select },
            { "Back", ControllerButton.Select },
            { "Options", ControllerButton.Start },
            { "Share", ControllerButton.Select },
            { "Menu", ControllerButton.Start },
            { "View", ControllerButton.Select },

            // Left Stick
            { "LStickUp", ControllerButton.LStickUp },
            { "LStickDown", ControllerButton.LStickDown },
            { "LStickLeft", ControllerButton.LStickLeft },
            { "LStickRight", ControllerButton.LStickRight },
            { "LeftStickUp", ControllerButton.LStickUp },
            { "LeftStickDown", ControllerButton.LStickDown },
            { "LeftStickLeft", ControllerButton.LStickLeft },
            { "LeftStickRight", ControllerButton.LStickRight },
            { "LSUp", ControllerButton.LStickUp },
            { "LSDown", ControllerButton.LStickDown },
            { "LSLeft", ControllerButton.LStickLeft },
            { "LSRight", ControllerButton.LStickRight },

            // Right Stick
            { "RStickUp", ControllerButton.RStickUp },
            { "RStickDown", ControllerButton.RStickDown },
            { "RStickLeft", ControllerButton.RStickLeft },
            { "RStickRight", ControllerButton.RStickRight },
            { "RightStickUp", ControllerButton.RStickUp },
            { "RightStickDown", ControllerButton.RStickDown },
            { "RightStickLeft", ControllerButton.RStickLeft },
            { "RightStickRight", ControllerButton.RStickRight },
            { "RSUp", ControllerButton.RStickUp },
            { "RSDown", ControllerButton.RStickDown },
            { "RSLeft", ControllerButton.RStickLeft },
            { "RSRight", ControllerButton.RStickRight },
        };

        /// <summary>
        /// Parse a binding string from the config file.
        /// Examples: "F1", "Ctrl+F1", "RB+DPadUp", "RStickUp"
        /// </summary>
        public static InputBinding ParseBinding(string bindingStr)
        {
            if (string.IsNullOrWhiteSpace(bindingStr))
                return null;

            bindingStr = bindingStr.Trim();

            // Split by + for combos
            var parts = bindingStr.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();

            // Try to determine if this is keyboard or controller
            // Check if any part is a keyboard modifier
            bool hasKeyboardModifier = false;
            bool hasCtrl = false, hasAlt = false, hasShift = false;

            foreach (var part in parts)
            {
                var lower = part.ToLower();
                if (lower == "ctrl" || lower == "control")
                {
                    hasKeyboardModifier = true;
                    hasCtrl = true;
                }
                else if (lower == "alt")
                {
                    hasKeyboardModifier = true;
                    hasAlt = true;
                }
                else if (lower == "shift")
                {
                    hasKeyboardModifier = true;
                    hasShift = true;
                }
            }

            // If we have keyboard modifiers, treat as keyboard binding
            if (hasKeyboardModifier)
            {
                // Find the main key (last non-modifier part)
                foreach (var part in parts)
                {
                    var lower = part.ToLower();
                    if (lower == "ctrl" || lower == "control" || lower == "alt" || lower == "shift")
                        continue;

                    if (KeyboardMap.TryGetValue(part, out var keyCode))
                    {
                        return new InputBinding(keyCode, hasCtrl, hasAlt, hasShift);
                    }
                }
                return null;
            }

            // Try as keyboard single key first
            if (parts.Length == 1 && KeyboardMap.TryGetValue(parts[0], out var singleKey))
            {
                return new InputBinding(singleKey);
            }

            // Try as controller binding
            if (parts.Length == 1)
            {
                // Single controller button
                if (ControllerMap.TryGetValue(parts[0], out var button))
                {
                    return new InputBinding(button);
                }
            }
            else if (parts.Length == 2)
            {
                // Controller combo: modifier + button
                if (ControllerMap.TryGetValue(parts[0], out var modifier) &&
                    ControllerMap.TryGetValue(parts[1], out var mainButton))
                {
                    return new InputBinding(modifier, mainButton);
                }
            }

            return null;
        }

        /// <summary>
        /// Get all valid controller button names for documentation.
        /// </summary>
        public static IEnumerable<string> GetControllerButtonNames()
        {
            return ControllerMap.Keys;
        }

        /// <summary>
        /// Get all valid keyboard key names for documentation.
        /// </summary>
        public static IEnumerable<string> GetKeyboardKeyNames()
        {
            return KeyboardMap.Keys;
        }
    }
}
