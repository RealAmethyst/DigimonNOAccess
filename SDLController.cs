using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// SDL3 controller handler for PlayStation and other controller support.
    /// SDL3 provides proper support for DualSense, DualShock, Xbox, and many other controllers.
    ///
    /// SETUP REQUIRED:
    /// 1. Download SDL3.dll from https://github.com/libsdl-org/SDL/releases (Latest 3.x, Windows x64)
    /// 2. Place SDL3.dll in the game's root folder (same folder as the .exe)
    ///
    /// This class wraps SDL3's Gamepad API which provides:
    /// - Automatic controller detection and hotplug support
    /// - Standardized button mapping across all controller types
    /// - Proper DualSense/DualShock support that the game lacks
    /// </summary>
    public static class SDLController
    {
        // SDL3 initialization flags
        private const uint SDL_INIT_GAMEPAD = 0x00002000;
        private const uint SDL_INIT_JOYSTICK = 0x00000200;

        // SDL3 Gamepad button enum (matches SDL_GamepadButton)
        public enum SDL_GameControllerButton
        {
            Invalid = -1,
            South = 0,          // A / Cross
            East = 1,           // B / Circle
            West = 2,           // X / Square
            North = 3,          // Y / Triangle
            Back = 4,
            Guide = 5,
            Start = 6,
            LeftStick = 7,
            RightStick = 8,
            LeftShoulder = 9,
            RightShoulder = 10,
            DPadUp = 11,
            DPadDown = 12,
            DPadLeft = 13,
            DPadRight = 14,
            Misc1 = 15,
            RightPaddle1 = 16,
            LeftPaddle1 = 17,
            RightPaddle2 = 18,
            LeftPaddle2 = 19,
            Touchpad = 20,
            Misc2 = 21,
            Misc3 = 22,
            Misc4 = 23,
            Misc5 = 24,
            Misc6 = 25,
            Max = 26,

            // Aliases for compatibility
            A = South,
            B = East,
            X = West,
            Y = North
        }

        // SDL3 Gamepad axis enum (matches SDL_GamepadAxis)
        public enum SDL_GameControllerAxis
        {
            Invalid = -1,
            LeftX = 0,
            LeftY = 1,
            RightX = 2,
            RightY = 3,
            LeftTrigger = 4,
            RightTrigger = 5,
            Max = 6
        }

        // P/Invoke declarations for SDL3
        private const string SDL3_DLL = "SDL3.dll";

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SDL_Init(uint flags);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_Quit();

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_PumpEvents();

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetGamepads(out int count);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_OpenGamepad(uint instance_id);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_CloseGamepad(IntPtr gamepad);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SDL_GamepadConnected(IntPtr gamepad);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SDL_GetGamepadButton(IntPtr gamepad, SDL_GameControllerButton button);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern short SDL_GetGamepadAxis(IntPtr gamepad, SDL_GameControllerAxis axis);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetGamepadName(IntPtr gamepad);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr SDL_GetError();

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool SDL_SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

        [DllImport(SDL3_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void SDL_free(IntPtr mem);

        // State tracking
        private static bool _initialized = false;
        private static bool _sdlAvailable = false;
        private static IntPtr _gamepad = IntPtr.Zero;
        private static string _controllerName = "";

        // Button state tracking for trigger detection
        private static Dictionary<SDL_GameControllerButton, bool> _buttonStates = new Dictionary<SDL_GameControllerButton, bool>();
        private static Dictionary<SDL_GameControllerButton, bool> _lastButtonStates = new Dictionary<SDL_GameControllerButton, bool>();

        // Axis state tracking
        private static Dictionary<SDL_GameControllerAxis, short> _axisStates = new Dictionary<SDL_GameControllerAxis, short>();
        private static Dictionary<SDL_GameControllerAxis, short> _lastAxisStates = new Dictionary<SDL_GameControllerAxis, short>();

        // Axis thresholds
        private const short TriggerThreshold = 8000;  // ~25% for triggers (0-32767)
        private const short StickThreshold = 16000;   // ~50% for stick directions

        /// <summary>
        /// Initialize SDL3 controller support.
        /// Returns true if SDL3 is available, false otherwise.
        /// </summary>
        public static bool Initialize()
        {
            if (_initialized) return _sdlAvailable;

            _initialized = true;

            try
            {
                // Disable thread naming to avoid Visual Studio debugger issues
                SDL_SetHint("SDL_WINDOWS_DISABLE_THREAD_NAMING", "1");

                // Initialize SDL3 with gamepad subsystem
                bool result = SDL_Init(SDL_INIT_GAMEPAD | SDL_INIT_JOYSTICK);
                if (!result)
                {
                    string error = GetSDLError();
                    DebugLogger.Log($"[SDL3Controller] SDL_Init failed: {error}");
                    return false;
                }

                _sdlAvailable = true;
                DebugLogger.Log("[SDL3Controller] SDL3 initialized successfully");

                // Initialize button state dictionaries
                foreach (SDL_GameControllerButton button in Enum.GetValues(typeof(SDL_GameControllerButton)))
                {
                    if (button != SDL_GameControllerButton.Invalid && button != SDL_GameControllerButton.Max &&
                        (int)button >= 0 && (int)button < 26)
                    {
                        _buttonStates[button] = false;
                        _lastButtonStates[button] = false;
                    }
                }

                foreach (SDL_GameControllerAxis axis in Enum.GetValues(typeof(SDL_GameControllerAxis)))
                {
                    if (axis != SDL_GameControllerAxis.Invalid && axis != SDL_GameControllerAxis.Max)
                    {
                        _axisStates[axis] = 0;
                        _lastAxisStates[axis] = 0;
                    }
                }

                // Try to open the first available gamepad
                OpenFirstGamepad();

                return true;
            }
            catch (DllNotFoundException)
            {
                DebugLogger.Log("[SDL3Controller] SDL3.dll not found - PlayStation controller support disabled");
                DebugLogger.Log("[SDL3Controller] To enable: Download SDL3.dll from https://github.com/libsdl-org/SDL/releases and place in game folder");
                return false;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SDL3Controller] Initialization error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if SDL3 is available and a controller is connected.
        /// </summary>
        public static bool IsAvailable => _sdlAvailable && _gamepad != IntPtr.Zero;

        /// <summary>
        /// Get the name of the connected controller.
        /// </summary>
        public static string ControllerName => _controllerName;

        /// <summary>
        /// Update controller state. Call this every frame.
        /// </summary>
        public static void Update()
        {
            if (!_sdlAvailable) return;

            try
            {
                // Pump SDL events to update controller state
                SDL_PumpEvents();

                // Check if controller is still connected
                if (_gamepad != IntPtr.Zero)
                {
                    if (!SDL_GamepadConnected(_gamepad))
                    {
                        // Controller disconnected
                        DebugLogger.Log($"[SDL3Controller] Controller disconnected: {_controllerName}");
                        ScreenReader.Say($"Controller disconnected: {_controllerName}");
                        SDL_CloseGamepad(_gamepad);
                        _gamepad = IntPtr.Zero;
                        _controllerName = "";
                    }
                }

                // Try to find a controller if we don't have one
                if (_gamepad == IntPtr.Zero)
                {
                    OpenFirstGamepad();
                }

                // Update button and axis states
                if (_gamepad != IntPtr.Zero)
                {
                    // Swap state buffers
                    _lastButtonStates = new Dictionary<SDL_GameControllerButton, bool>(_buttonStates);

                    _lastAxisStates = new Dictionary<SDL_GameControllerAxis, short>(_axisStates);

                    // Read current button states
                    foreach (var button in new List<SDL_GameControllerButton>(_buttonStates.Keys))
                    {
                        _buttonStates[button] = SDL_GetGamepadButton(_gamepad, button);
                    }

                    // Read current axis states
                    foreach (var axis in new List<SDL_GameControllerAxis>(_axisStates.Keys))
                    {
                        _axisStates[axis] = SDL_GetGamepadAxis(_gamepad, axis);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SDL3Controller] Update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a button is currently held.
        /// </summary>
        public static bool IsButtonHeld(SDL_GameControllerButton button)
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return false;

            // Handle aliases
            if (button == SDL_GameControllerButton.A) button = SDL_GameControllerButton.South;
            if (button == SDL_GameControllerButton.B) button = SDL_GameControllerButton.East;
            if (button == SDL_GameControllerButton.X) button = SDL_GameControllerButton.West;
            if (button == SDL_GameControllerButton.Y) button = SDL_GameControllerButton.North;

            return _buttonStates.TryGetValue(button, out bool held) && held;
        }

        /// <summary>
        /// Check if a button was just pressed this frame.
        /// </summary>
        public static bool IsButtonTriggered(SDL_GameControllerButton button)
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return false;

            // Handle aliases
            if (button == SDL_GameControllerButton.A) button = SDL_GameControllerButton.South;
            if (button == SDL_GameControllerButton.B) button = SDL_GameControllerButton.East;
            if (button == SDL_GameControllerButton.X) button = SDL_GameControllerButton.West;
            if (button == SDL_GameControllerButton.Y) button = SDL_GameControllerButton.North;

            bool currentlyHeld = _buttonStates.TryGetValue(button, out bool held) && held;
            bool wasHeld = _lastButtonStates.TryGetValue(button, out bool lastHeld) && lastHeld;

            return currentlyHeld && !wasHeld;
        }

        /// <summary>
        /// Check if left trigger (L2) is held.
        /// </summary>
        public static bool IsLeftTriggerHeld()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return false;
            return _axisStates.TryGetValue(SDL_GameControllerAxis.LeftTrigger, out short value) && value > TriggerThreshold;
        }

        /// <summary>
        /// Check if right trigger (R2) is held.
        /// </summary>
        public static bool IsRightTriggerHeld()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return false;
            return _axisStates.TryGetValue(SDL_GameControllerAxis.RightTrigger, out short value) && value > TriggerThreshold;
        }

        /// <summary>
        /// Check if left trigger was just pressed this frame.
        /// </summary>
        public static bool IsLeftTriggerTriggered()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return false;

            short current = _axisStates.TryGetValue(SDL_GameControllerAxis.LeftTrigger, out short c) ? c : (short)0;
            short last = _lastAxisStates.TryGetValue(SDL_GameControllerAxis.LeftTrigger, out short l) ? l : (short)0;

            return current > TriggerThreshold && last <= TriggerThreshold;
        }

        /// <summary>
        /// Check if right trigger was just pressed this frame.
        /// </summary>
        public static bool IsRightTriggerTriggered()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return false;

            short current = _axisStates.TryGetValue(SDL_GameControllerAxis.RightTrigger, out short c) ? c : (short)0;
            short last = _lastAxisStates.TryGetValue(SDL_GameControllerAxis.RightTrigger, out short l) ? l : (short)0;

            return current > TriggerThreshold && last <= TriggerThreshold;
        }

        /// <summary>
        /// Get left stick X axis (-32768 to 32767).
        /// </summary>
        public static short GetLeftStickX()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return 0;
            return _axisStates.TryGetValue(SDL_GameControllerAxis.LeftX, out short value) ? value : (short)0;
        }

        /// <summary>
        /// Get left stick Y axis (-32768 to 32767).
        /// </summary>
        public static short GetLeftStickY()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return 0;
            return _axisStates.TryGetValue(SDL_GameControllerAxis.LeftY, out short value) ? value : (short)0;
        }

        /// <summary>
        /// Get right stick X axis (-32768 to 32767).
        /// </summary>
        public static short GetRightStickX()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return 0;
            return _axisStates.TryGetValue(SDL_GameControllerAxis.RightX, out short value) ? value : (short)0;
        }

        /// <summary>
        /// Get right stick Y axis (-32768 to 32767).
        /// </summary>
        public static short GetRightStickY()
        {
            if (!_sdlAvailable || _gamepad == IntPtr.Zero) return 0;
            return _axisStates.TryGetValue(SDL_GameControllerAxis.RightY, out short value) ? value : (short)0;
        }

        /// <summary>
        /// Check if right stick is pushed in a direction (for trigger detection).
        /// </summary>
        public static bool IsRightStickUp() => GetRightStickY() < -StickThreshold;
        public static bool IsRightStickDown() => GetRightStickY() > StickThreshold;
        public static bool IsRightStickLeft() => GetRightStickX() < -StickThreshold;
        public static bool IsRightStickRight() => GetRightStickX() > StickThreshold;

        /// <summary>
        /// Check if right stick direction was just triggered this frame.
        /// </summary>
        public static bool IsRightStickUpTriggered()
        {
            short current = GetRightStickY();
            short last = _lastAxisStates.TryGetValue(SDL_GameControllerAxis.RightY, out short l) ? l : (short)0;
            return current < -StickThreshold && last >= -StickThreshold;
        }

        public static bool IsRightStickDownTriggered()
        {
            short current = GetRightStickY();
            short last = _lastAxisStates.TryGetValue(SDL_GameControllerAxis.RightY, out short l) ? l : (short)0;
            return current > StickThreshold && last <= StickThreshold;
        }

        public static bool IsRightStickLeftTriggered()
        {
            short current = GetRightStickX();
            short last = _lastAxisStates.TryGetValue(SDL_GameControllerAxis.RightX, out short l) ? l : (short)0;
            return current < -StickThreshold && last >= -StickThreshold;
        }

        public static bool IsRightStickRightTriggered()
        {
            short current = GetRightStickX();
            short last = _lastAxisStates.TryGetValue(SDL_GameControllerAxis.RightX, out short l) ? l : (short)0;
            return current > StickThreshold && last <= StickThreshold;
        }

        /// <summary>
        /// Shutdown SDL3.
        /// </summary>
        public static void Shutdown()
        {
            if (!_sdlAvailable) return;

            try
            {
                if (_gamepad != IntPtr.Zero)
                {
                    SDL_CloseGamepad(_gamepad);
                    _gamepad = IntPtr.Zero;
                }

                SDL_Quit();
                _sdlAvailable = false;
                DebugLogger.Log("[SDL3Controller] SDL3 shutdown complete");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SDL3Controller] Shutdown error: {ex.Message}");
            }
        }

        private static void OpenFirstGamepad()
        {
            try
            {
                int count;
                IntPtr gamepadsPtr = SDL_GetGamepads(out count);

                if (gamepadsPtr == IntPtr.Zero || count == 0)
                {
                    return;
                }

                // Read the first gamepad instance ID from the array
                uint instanceId = (uint)Marshal.ReadInt32(gamepadsPtr);
                SDL_free(gamepadsPtr);

                _gamepad = SDL_OpenGamepad(instanceId);
                if (_gamepad != IntPtr.Zero)
                {
                    IntPtr namePtr = SDL_GetGamepadName(_gamepad);
                    _controllerName = namePtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(namePtr) : "Unknown Controller";
                    DebugLogger.Log($"[SDL3Controller] Controller connected: {_controllerName}");
                    ScreenReader.Say($"Controller connected: {_controllerName}");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SDL3Controller] Error opening gamepad: {ex.Message}");
            }
        }

        private static string GetSDLError()
        {
            try
            {
                IntPtr errorPtr = SDL_GetError();
                return errorPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(errorPtr) : "Unknown error";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SDL3Controller] Error in GetSDLError: {ex.Message}");
                return "Could not get error message";
            }
        }
    }
}
