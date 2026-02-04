using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patch to inject SDL3 controller input into the game's input system.
    /// This allows PlayStation controllers (DualSense, DualShock) to work for gameplay,
    /// not just mod hotkeys.
    /// </summary>
    public static class GamepadInputPatch
    {
        private static bool _enabled = true;
        private static bool _debugMode = false;
        private static PadManager.BUTTON _lastInjectedButtons = PadManager.BUTTON._Non;
        private static PadManager.BUTTON _currentInjectedButtons = PadManager.BUTTON._Non;

        // Repeat timing (matches game defaults)
        private static int _repeatCounter = 0;
        private static PadManager.BUTTON _heldButtons = PadManager.BUTTON._Non;
        private static PadManager.BUTTON _repeatButtons = PadManager.BUTTON._Non;
        private const int RepeatFirstDelay = 16;  // Frames before first repeat
        private const int RepeatInterval = 4;     // Frames between repeats

        /// <summary>
        /// Enable or disable SDL3 input injection.
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// Enable debug logging for button presses.
        /// </summary>
        public static bool DebugMode
        {
            get => _debugMode;
            set => _debugMode = value;
        }

        /// <summary>
        /// Apply the Harmony patches.
        /// </summary>
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            // Patch Pad._Update to inject SDL3 input after the game reads from Steam Input
            var padUpdateMethod = AccessTools.Method(typeof(Pad), "_Update");
            var postfix = new HarmonyMethod(typeof(GamepadInputPatch), nameof(Pad_Update_Postfix));

            if (padUpdateMethod != null)
            {
                harmony.Patch(padUpdateMethod, postfix: postfix);
                DebugLogger.Log("[GamepadInputPatch] Patched Pad._Update for SDL3 input injection");
            }
            else
            {
                DebugLogger.Log("[GamepadInputPatch] WARNING: Could not find Pad._Update method");
            }

            // Also patch the static PadManager methods that games often use directly
            // This ensures our input is read even if the game bypasses the Pad instance
            PatchPadManagerMethods(harmony);
        }

        /// <summary>
        /// Patch PadManager static methods to include SDL3 input.
        /// </summary>
        private static void PatchPadManagerMethods(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch GetInput - returns currently held buttons
                var getInputMethod = AccessTools.Method(typeof(PadManager), "GetInput");
                if (getInputMethod != null)
                {
                    harmony.Patch(getInputMethod,
                        postfix: new HarmonyMethod(typeof(GamepadInputPatch), nameof(GetInput_Postfix)));
                    DebugLogger.Log("[GamepadInputPatch] Patched PadManager.GetInput");
                }

                // Patch GetTrigger - returns just-pressed buttons
                var getTriggerMethod = AccessTools.Method(typeof(PadManager), "GetTrigger");
                if (getTriggerMethod != null)
                {
                    harmony.Patch(getTriggerMethod,
                        postfix: new HarmonyMethod(typeof(GamepadInputPatch), nameof(GetTrigger_Postfix)));
                    DebugLogger.Log("[GamepadInputPatch] Patched PadManager.GetTrigger");
                }

                // Patch GetRepeat - returns repeat-triggered buttons
                var getRepeatMethod = AccessTools.Method(typeof(PadManager), "GetRepeat");
                if (getRepeatMethod != null)
                {
                    harmony.Patch(getRepeatMethod,
                        postfix: new HarmonyMethod(typeof(GamepadInputPatch), nameof(GetRepeat_Postfix)));
                    DebugLogger.Log("[GamepadInputPatch] Patched PadManager.GetRepeat");
                }

                // Patch IsTrigger - checks if specific button was just pressed
                var isTriggerMethod = AccessTools.Method(typeof(PadManager), "IsTrigger",
                    new[] { typeof(PadManager.BUTTON) });
                if (isTriggerMethod != null)
                {
                    harmony.Patch(isTriggerMethod,
                        postfix: new HarmonyMethod(typeof(GamepadInputPatch), nameof(IsTrigger_Postfix)));
                    DebugLogger.Log("[GamepadInputPatch] Patched PadManager.IsTrigger");
                }

                // Patch IsRepeat - checks if specific button is repeating
                var isRepeatMethod = AccessTools.Method(typeof(PadManager), "IsRepeat",
                    new[] { typeof(PadManager.BUTTON) });
                if (isRepeatMethod != null)
                {
                    harmony.Patch(isRepeatMethod,
                        postfix: new HarmonyMethod(typeof(GamepadInputPatch), nameof(IsRepeat_Postfix)));
                    DebugLogger.Log("[GamepadInputPatch] Patched PadManager.IsRepeat");
                }

                // Patch IsInput - checks if specific button is held
                var isInputMethod = AccessTools.Method(typeof(PadManager), "IsInput",
                    new[] { typeof(PadManager.BUTTON) });
                if (isInputMethod != null)
                {
                    harmony.Patch(isInputMethod,
                        postfix: new HarmonyMethod(typeof(GamepadInputPatch), nameof(IsInput_Postfix)));
                    DebugLogger.Log("[GamepadInputPatch] Patched PadManager.IsInput");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[GamepadInputPatch] Error patching PadManager methods: {ex.Message}");
            }
        }

        // Postfixes for PadManager static methods
        private static void GetInput_Postfix(ref PadManager.BUTTON __result)
        {
            if (_enabled && SDLController.IsAvailable)
            {
                __result |= _currentInjectedButtons;
            }
        }

        private static void GetTrigger_Postfix(ref PadManager.BUTTON __result)
        {
            if (_enabled && SDLController.IsAvailable)
            {
                PadManager.BUTTON triggerButtons = _currentInjectedButtons & ~_lastInjectedButtons;
                __result |= triggerButtons;
            }
        }

        private static void GetRepeat_Postfix(ref PadManager.BUTTON __result)
        {
            if (_enabled && SDLController.IsAvailable && _repeatButtons != PadManager.BUTTON._Non)
            {
                __result |= _repeatButtons;
            }
        }

        private static void IsTrigger_Postfix(PadManager.BUTTON button, ref bool __result)
        {
            if (!__result && _enabled && SDLController.IsAvailable)
            {
                PadManager.BUTTON triggerButtons = _currentInjectedButtons & ~_lastInjectedButtons;
                __result = (triggerButtons & button) != 0;
            }
        }

        private static void IsRepeat_Postfix(PadManager.BUTTON button, ref bool __result)
        {
            if (!__result && _enabled && SDLController.IsAvailable)
            {
                // Check if this button should repeat
                PadManager.BUTTON triggerButtons = _currentInjectedButtons & ~_lastInjectedButtons;
                bool justTriggered = (triggerButtons & button) != 0;
                bool isRepeating = (_repeatButtons & button) != 0;
                __result = justTriggered || isRepeating;
            }
        }

        private static void IsInput_Postfix(PadManager.BUTTON button, ref bool __result)
        {
            if (!__result && _enabled && SDLController.IsAvailable)
            {
                __result = (_currentInjectedButtons & button) != 0;
            }
        }

        /// <summary>
        /// Postfix for Pad._Update - injects SDL3 controller input.
        /// </summary>
        private static void Pad_Update_Postfix(Pad __instance)
        {
            if (!_enabled || !SDLController.IsAvailable)
                return;

            try
            {
                // Store previous state for trigger detection
                _lastInjectedButtons = _currentInjectedButtons;

                // Read current button state from SDL3
                _currentInjectedButtons = ReadSDL3Buttons();

                // Debug logging
                if (_debugMode && _currentInjectedButtons != PadManager.BUTTON._Non)
                {
                    DebugLogger.Log($"[GamepadInputPatch] Buttons: {_currentInjectedButtons}");
                }

                // Get the current buffer
                var buffer = __instance.m_Buffer;
                if (buffer != null && buffer.Length > 0)
                {
                    int curBufferIndex = __instance.m_CurBuffer;
                    if (curBufferIndex >= 0 && curBufferIndex < buffer.Length)
                    {
                        var padData = buffer[curBufferIndex];
                        if (padData != null)
                        {
                            // ALWAYS inject SDL3 buttons (OR with any existing)
                            // This ensures our input is always registered
                            padData.button |= _currentInjectedButtons;

                            // Also inject stick data
                            InjectStickData(padData);
                        }
                    }
                }

                // Calculate trigger buttons (just pressed this frame)
                PadManager.BUTTON triggerButtons = _currentInjectedButtons & ~_lastInjectedButtons;
                if (triggerButtons != PadManager.BUTTON._Non)
                {
                    __instance.m_ButtonTrigger |= triggerButtons;
                    _repeatCounter = 0;  // Reset repeat counter on new press
                    _heldButtons = _currentInjectedButtons;

                    if (_debugMode)
                    {
                        DebugLogger.Log($"[GamepadInputPatch] Triggered: {triggerButtons}");
                    }
                }

                // Calculate release buttons (just released this frame)
                PadManager.BUTTON releaseButtons = _lastInjectedButtons & ~_currentInjectedButtons;
                if (releaseButtons != PadManager.BUTTON._Non)
                {
                    __instance.m_ButtonRelease |= releaseButtons;
                    // Clear held buttons that were released
                    _heldButtons &= ~releaseButtons;
                }

                // Handle button repeat for held buttons (important for D-pad navigation)
                _repeatButtons = PadManager.BUTTON._Non;  // Reset each frame

                if (_currentInjectedButtons != PadManager.BUTTON._Non)
                {
                    _repeatCounter++;

                    // Check if we should fire a repeat
                    bool shouldRepeat = false;
                    if (_repeatCounter == RepeatFirstDelay)
                    {
                        shouldRepeat = true;
                    }
                    else if (_repeatCounter > RepeatFirstDelay)
                    {
                        int framesSinceFirst = _repeatCounter - RepeatFirstDelay;
                        if (framesSinceFirst % RepeatInterval == 0)
                        {
                            shouldRepeat = true;
                        }
                    }

                    if (shouldRepeat)
                    {
                        // Only repeat D-pad and stick directions
                        _repeatButtons = _currentInjectedButtons & (
                            PadManager.BUTTON.dUp | PadManager.BUTTON.dDown |
                            PadManager.BUTTON.dLeft | PadManager.BUTTON.dRight |
                            PadManager.BUTTON.slUp | PadManager.BUTTON.slDown |
                            PadManager.BUTTON.slLeft | PadManager.BUTTON.slRight |
                            PadManager.BUTTON.srUp | PadManager.BUTTON.srDown |
                            PadManager.BUTTON.srLeft | PadManager.BUTTON.srRight
                        );

                        if (_repeatButtons != PadManager.BUTTON._Non)
                        {
                            __instance.m_ButtonRepeat |= _repeatButtons;
                        }
                    }
                }
                else
                {
                    // No buttons held, reset repeat
                    _repeatCounter = 0;
                    _heldButtons = PadManager.BUTTON._Non;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[GamepadInputPatch] Error in Pad_Update_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Read the current SDL3 button state and convert to game's BUTTON flags.
        ///
        /// IMPORTANT: The game uses Japanese PlayStation convention internally:
        /// - bCircle = OK/Confirm (bOK alias)
        /// - bCross = Cancel (bCANCEL alias)
        ///
        /// But Western users expect (PS5 default, Xbox standard):
        /// - Cross/A = Confirm
        /// - Circle/B = Cancel
        ///
        /// So we SWAP Cross and Circle to match Western convention:
        /// - Physical Cross (South) → bCircle (triggers Confirm in game)
        /// - Physical Circle (East) → bCross (triggers Cancel in game)
        /// </summary>
        private static PadManager.BUTTON ReadSDL3Buttons()
        {
            PadManager.BUTTON buttons = PadManager.BUTTON._Non;

            // Face buttons - SWAPPED for Western convention
            // Physical Cross (A position) → bCircle (Confirm function in game)
            // Physical Circle (B position) → bCross (Cancel function in game)
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.South))
                buttons |= PadManager.BUTTON.bCircle;  // Cross → Confirm (bOK)

            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.East))
                buttons |= PadManager.BUTTON.bCross;   // Circle → Cancel (bCANCEL)

            // Square and Triangle - no swap needed, same position as Xbox X/Y
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.West))
                buttons |= PadManager.BUTTON.bSquare;  // Square/X

            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.North))
                buttons |= PadManager.BUTTON.bTriangle; // Triangle/Y

            // Shoulder buttons
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.LeftShoulder))
                buttons |= PadManager.BUTTON.bL;  // LB/L1

            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.RightShoulder))
                buttons |= PadManager.BUTTON.bR;  // RB/R1

            // D-pad
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadUp))
                buttons |= PadManager.BUTTON.dUp;

            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadDown))
                buttons |= PadManager.BUTTON.dDown;

            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadLeft))
                buttons |= PadManager.BUTTON.dLeft;

            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadRight))
                buttons |= PadManager.BUTTON.dRight;

            // Start/Select (Options/Share on PlayStation)
            // Options button (right side, 3 lines) = Start
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.Start))
                buttons |= PadManager.BUTTON.bStart;

            // Share/Create button (left side) = Select
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.Back))
                buttons |= PadManager.BUTTON.bSelect;

            // Touchpad button (DualSense/DualShock 4) - map to Select as it's often used similarly
            // Some games use this as an additional menu button
            if (SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.Touchpad))
                buttons |= PadManager.BUTTON.bSelect;

            // Convert left stick to D-pad for menu navigation
            short leftX = SDLController.GetLeftStickX();
            short leftY = SDLController.GetLeftStickY();
            const short stickDeadzone = 16000;

            if (leftY < -stickDeadzone)
                buttons |= PadManager.BUTTON.slUp;
            if (leftY > stickDeadzone)
                buttons |= PadManager.BUTTON.slDown;
            if (leftX < -stickDeadzone)
                buttons |= PadManager.BUTTON.slLeft;
            if (leftX > stickDeadzone)
                buttons |= PadManager.BUTTON.slRight;

            // Convert right stick as well
            short rightX = SDLController.GetRightStickX();
            short rightY = SDLController.GetRightStickY();

            if (rightY < -stickDeadzone)
                buttons |= PadManager.BUTTON.srUp;
            if (rightY > stickDeadzone)
                buttons |= PadManager.BUTTON.srDown;
            if (rightX < -stickDeadzone)
                buttons |= PadManager.BUTTON.srLeft;
            if (rightX > stickDeadzone)
                buttons |= PadManager.BUTTON.srRight;

            return buttons;
        }

        /// <summary>
        /// Inject analog stick data into the PadData.
        /// </summary>
        private static void InjectStickData(Pad.PadData padData)
        {
            try
            {
                var stickArray = padData.stick;
                if (stickArray == null || stickArray.Length < 2)
                    return;

                // Get SDL3 stick values and convert to Unity Vector2 (normalized -1 to 1)
                // Note: Do NOT invert Y - the game handles stick input correctly already
                float leftX = SDLController.GetLeftStickX() / 32767f;
                float leftY = SDLController.GetLeftStickY() / 32767f;

                float rightX = SDLController.GetRightStickX() / 32767f;
                float rightY = SDLController.GetRightStickY() / 32767f;

                // Only overwrite if SDL3 has significant input
                const float deadzone = 0.15f;

                if (Mathf.Abs(leftX) > deadzone || Mathf.Abs(leftY) > deadzone)
                {
                    stickArray[0] = new Vector2(leftX, leftY);
                }

                if (Mathf.Abs(rightX) > deadzone || Mathf.Abs(rightY) > deadzone)
                {
                    stickArray[1] = new Vector2(rightX, rightY);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[GamepadInputPatch] Error injecting stick data: {ex.Message}");
            }
        }
    }
}
