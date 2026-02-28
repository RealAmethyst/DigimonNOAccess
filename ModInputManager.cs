using System;
using System.Collections.Generic;
using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Central input manager for the accessibility mod.
    /// Handles keyboard and controller input with configurable hotkeys.
    ///
    /// Design principles:
    /// - Each action can have BOTH a keyboard binding AND a controller binding
    /// - Controller hotkeys MUST use combos (modifier + button) to avoid conflicts with game
    /// - Keyboard can use single keys (F1-F12) or combos (Ctrl+key)
    /// - All bindings are configurable via hotkeys.ini with separate [Keyboard] and [Controller] sections
    ///
    /// Controller Support:
    /// - SDL3 (preferred): Full support for DualSense, DualShock, Xbox, and many others
    ///   Requires SDL3.dll in game folder - download from https://github.com/libsdl-org/SDL/releases
    /// - Fallback (PadManager): Uses game's built-in input, limited to what game supports
    /// </summary>
    public static class ModInputManager
    {
        // SDL3 availability
        private static bool _useSDL = false;

        // Registered actions with both keyboard and controller bindings
        private static Dictionary<string, ActionBindings> _actions = new Dictionary<string, ActionBindings>();

        // Track which actions fired this frame (for IsTrigger behavior)
        private static HashSet<string> _triggeredThisFrame = new HashSet<string>();
        private static HashSet<string> _triggeredLastFrame = new HashSet<string>();

        // Config file path
        private static string _configPath;
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the input manager and load config.
        /// </summary>
        public static void Initialize(string modFolderPath)
        {
            _configPath = System.IO.Path.Combine(modFolderPath, "hotkeys.ini");

            // Try to initialize SDL3 for PlayStation controller support
            _useSDL = SDLController.Initialize();
            if (_useSDL)
            {
                DebugLogger.Log("[ModInputManager] SDL3 available - PlayStation controller support enabled");
            }
            else
            {
                DebugLogger.Log("[ModInputManager] SDL3 not available - using game's PadManager (Xbox support only)");
            }

            // Register all mod actions with defaults
            RegisterDefaultBindings();

            // Load user config (overrides defaults)
            LoadConfig();

            _initialized = true;
            DebugLogger.Log("[ModInputManager] Initialized");
        }

        /// <summary>
        /// Must be called every frame to update input state.
        /// </summary>
        public static void Update()
        {
            if (!_initialized) return;

            // Update SDL3 controller state if available
            // Always pump events even when unfocused to avoid stale state on refocus
            if (_useSDL)
            {
                SDLController.Update();
            }

            // Skip all input processing when game window is not focused
            if (!Application.isFocused)
            {
                _triggeredLastFrame = _triggeredThisFrame;
                _triggeredThisFrame = new HashSet<string>();
                return;
            }

            // Track which input device was last used (for button icon resolution)
            ButtonIconResolver.UpdateInputDevice();

            // Swap frame buffers
            _triggeredLastFrame = _triggeredThisFrame;
            _triggeredThisFrame = new HashSet<string>();

            // Check all actions (both keyboard and controller bindings)
            foreach (var kvp in _actions)
            {
                if (CheckAction(kvp.Value))
                {
                    _triggeredThisFrame.Add(kvp.Key);
                }
            }
        }

        /// <summary>
        /// Check if SDL3 is being used for controller input.
        /// </summary>
        public static bool IsUsingSDL => _useSDL;

        /// <summary>
        /// Get the name of the connected controller (SDL3 only).
        /// </summary>
        public static string GetControllerName()
        {
            if (_useSDL && SDLController.IsAvailable)
                return SDLController.ControllerName;
            return "Game Default (PadManager)";
        }

        /// <summary>
        /// Check if an action was just triggered this frame (like GetKeyDown).
        /// Checks both keyboard and controller bindings.
        /// </summary>
        public static bool IsActionTriggered(string actionName)
        {
            return _triggeredThisFrame.Contains(actionName) &&
                   !_triggeredLastFrame.Contains(actionName);
        }

        /// <summary>
        /// Check if an action is currently held (like GetKey).
        /// </summary>
        public static bool IsActionHeld(string actionName)
        {
            return _triggeredThisFrame.Contains(actionName);
        }

        /// <summary>
        /// Get the display name for an action's bindings (for announcements).
        /// </summary>
        public static string GetBindingDisplayName(string actionName)
        {
            if (_actions.TryGetValue(actionName, out var bindings))
            {
                var parts = new List<string>();
                if (bindings.KeyboardBinding != null)
                    parts.Add(bindings.KeyboardBinding.DisplayName);
                if (bindings.ControllerBinding != null)
                    parts.Add(bindings.ControllerBinding.DisplayName);
                return string.Join(" or ", parts);
            }
            return "Unbound";
        }

        /// <summary>
        /// Get only the keyboard or controller display name for an action.
        /// </summary>
        public static string GetBindingDisplayName(string actionName, bool controller)
        {
            if (_actions.TryGetValue(actionName, out var bindings))
            {
                var binding = controller ? bindings.ControllerBinding : bindings.KeyboardBinding;
                if (binding != null)
                    return binding.DisplayName;
            }
            return "None";
        }

        /// <summary>
        /// Register all default bindings. These can be overridden by config.
        /// </summary>
        private static void RegisterDefaultBindings()
        {
            // === Global Hotkeys ===
            RegisterAction("RepeatLast",
                keyboard: new InputBinding(KeyCode.F1),
                controller: null);  // No default controller binding

            RegisterAction("AnnounceStatus",
                keyboard: new InputBinding(KeyCode.F2),
                controller: null);

            RegisterAction("ToggleVoicedText",
                keyboard: new InputBinding(KeyCode.F5),
                controller: null);

            // === Field - Partner Status ===
            RegisterAction("Partner1Status",
                keyboard: new InputBinding(KeyCode.F3),
                controller: new InputBinding(ControllerButton.RB, ControllerButton.DPadUp));

            RegisterAction("Partner2Status",
                keyboard: new InputBinding(KeyCode.F4),
                controller: new InputBinding(ControllerButton.LB, ControllerButton.DPadUp));

            // === Battle - Per-Enemy Info ===
            RegisterAction("BattleEnemy1",
                keyboard: new InputBinding(KeyCode.F6),
                controller: new InputBinding(ControllerButton.RStickUp));

            RegisterAction("BattleEnemy2",
                keyboard: new InputBinding(KeyCode.F7),
                controller: new InputBinding(ControllerButton.RStickDown));

            RegisterAction("BattleEnemy3",
                keyboard: new InputBinding(KeyCode.F11),
                controller: new InputBinding(ControllerButton.RStickLeft));

            // === Battle - Order Power & SP Details ===
            RegisterAction("BattleOrderPower",
                keyboard: new InputBinding(KeyCode.F12),
                controller: new InputBinding(ControllerButton.RStickRight));

            // === Navigation List ===
            RegisterAction("NavNextCategory",
                keyboard: new InputBinding(KeyCode.O),
                controller: null);

            RegisterAction("NavPrevCategory",
                keyboard: new InputBinding(KeyCode.I),
                controller: null);

            RegisterAction("NavPrevEvent",
                keyboard: new InputBinding(KeyCode.J),
                controller: null);

            RegisterAction("NavCurrentEvent",
                keyboard: new InputBinding(KeyCode.K),
                controller: null);

            RegisterAction("NavNextEvent",
                keyboard: new InputBinding(KeyCode.L),
                controller: null);

            RegisterAction("NavToEvent",
                keyboard: new InputBinding(KeyCode.P),
                controller: null);

            RegisterAction("ToggleAutoWalk",
                keyboard: new InputBinding(KeyCode.P, false, false, true),
                controller: null);

            // === Compass Direction ===
            RegisterAction("CompassDirection",
                keyboard: new InputBinding(KeyCode.C),
                controller: new InputBinding(ControllerButton.R3));

            // === Shop/Trade/Restaurant Menus ===
            RegisterAction("ShopCheckBits",
                keyboard: new InputBinding(KeyCode.F10),
                controller: new InputBinding(ControllerButton.RT, ControllerButton.DPadDown));

            // === Training Menu ===
            RegisterAction("TrainingP1Info",
                keyboard: new InputBinding(KeyCode.F3),
                controller: new InputBinding(ControllerButton.RB, ControllerButton.DPadUp));

            RegisterAction("TrainingP2Info",
                keyboard: new InputBinding(KeyCode.F4),
                controller: new InputBinding(ControllerButton.LB, ControllerButton.DPadUp));
        }

        private static void RegisterAction(string actionName, InputBinding keyboard, InputBinding controller)
        {
            _actions[actionName] = new ActionBindings
            {
                KeyboardBinding = keyboard,
                ControllerBinding = controller
            };
        }

        /// <summary>
        /// Check if an action's conditions are met this frame.
        /// Checks both keyboard and controller bindings.
        /// </summary>
        private static bool CheckAction(ActionBindings bindings)
        {
            // Check keyboard binding
            if (bindings.KeyboardBinding != null && CheckKeyboardBinding(bindings.KeyboardBinding))
                return true;

            // Check controller binding
            if (bindings.ControllerBinding != null && CheckControllerBinding(bindings.ControllerBinding))
                return true;

            return false;
        }

        private static bool CheckKeyboardBinding(InputBinding binding)
        {
            // Check required modifiers are held
            if (binding.RequiresCtrl && !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl))
                return false;
            if (binding.RequiresAlt && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt))
                return false;
            if (binding.RequiresShift && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                return false;

            // Reject if extra modifiers are held that aren't required
            // (e.g. Shift+P should not trigger an action bound to just P)
            if (!binding.RequiresCtrl && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
                return false;
            if (!binding.RequiresAlt && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
                return false;
            if (!binding.RequiresShift && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
                return false;

            // Check main key (trigger detection)
            return Input.GetKeyDown(binding.Key);
        }

        private static bool CheckControllerBinding(InputBinding binding)
        {
            // Check modifier button is held
            if (binding.ModifierButton != ControllerButton.None)
            {
                if (!IsControllerButtonHeld(binding.ModifierButton))
                    return false;
            }

            // Check main button (trigger detection - just pressed)
            return IsControllerButtonTriggered(binding.MainButton);
        }

        /// <summary>
        /// Check if a controller button is currently held.
        /// Uses SDL3 if available, falls back to PadManager.
        /// </summary>
        private static bool IsControllerButtonHeld(ControllerButton button)
        {
            if (_useSDL && SDLController.IsAvailable)
            {
                return IsControllerButtonHeldSDL(button);
            }
            return IsControllerButtonHeldPadManager(button);
        }

        private static bool IsControllerButtonHeldSDL(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.DPadUp:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadUp);
                case ControllerButton.DPadDown:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadDown);
                case ControllerButton.DPadLeft:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadLeft);
                case ControllerButton.DPadRight:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadRight);
                case ControllerButton.A:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.A);
                case ControllerButton.B:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.B);
                case ControllerButton.X:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.X);
                case ControllerButton.Y:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.Y);
                case ControllerButton.LB:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.LeftShoulder);
                case ControllerButton.RB:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.RightShoulder);
                case ControllerButton.LT:
                    return SDLController.IsLeftTriggerHeld();
                case ControllerButton.RT:
                    return SDLController.IsRightTriggerHeld();
                case ControllerButton.Start:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.Start);
                case ControllerButton.Select:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.Back);
                case ControllerButton.RStickUp:
                    return SDLController.IsRightStickUp();
                case ControllerButton.RStickDown:
                    return SDLController.IsRightStickDown();
                case ControllerButton.RStickLeft:
                    return SDLController.IsRightStickLeft();
                case ControllerButton.RStickRight:
                    return SDLController.IsRightStickRight();
                case ControllerButton.LStickUp:
                case ControllerButton.LStickDown:
                case ControllerButton.LStickLeft:
                case ControllerButton.LStickRight:
                    return IsControllerButtonHeldPadManager(button);
                case ControllerButton.L3:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.LeftStick);
                case ControllerButton.R3:
                    return SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.RightStick);
                default:
                    return false;
            }
        }

        private static bool IsControllerButtonHeldPadManager(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.DPadUp:
                    return PadManager.IsInput(PadManager.BUTTON.dUp);
                case ControllerButton.DPadDown:
                    return PadManager.IsInput(PadManager.BUTTON.dDown);
                case ControllerButton.DPadLeft:
                    return PadManager.IsInput(PadManager.BUTTON.dLeft);
                case ControllerButton.DPadRight:
                    return PadManager.IsInput(PadManager.BUTTON.dRight);
                case ControllerButton.A:
                    return PadManager.IsInput(PadManager.BUTTON.bCross);
                case ControllerButton.B:
                    return PadManager.IsInput(PadManager.BUTTON.bCircle);
                case ControllerButton.X:
                    return PadManager.IsInput(PadManager.BUTTON.bSquare);
                case ControllerButton.Y:
                    return PadManager.IsInput(PadManager.BUTTON.bTriangle);
                case ControllerButton.LB:
                    return PadManager.IsInput(PadManager.BUTTON.bL);
                case ControllerButton.RB:
                    return PadManager.IsInput(PadManager.BUTTON.bR);
                case ControllerButton.LT:
                    return IsUnityTriggerHeld(true);
                case ControllerButton.RT:
                    return IsUnityTriggerHeld(false);
                case ControllerButton.Start:
                    return PadManager.IsInput(PadManager.BUTTON.bStart);
                case ControllerButton.Select:
                    return PadManager.IsInput(PadManager.BUTTON.bSelect);
                case ControllerButton.LStickUp:
                    return PadManager.IsInput(PadManager.BUTTON.slUp);
                case ControllerButton.LStickDown:
                    return PadManager.IsInput(PadManager.BUTTON.slDown);
                case ControllerButton.LStickLeft:
                    return PadManager.IsInput(PadManager.BUTTON.slLeft);
                case ControllerButton.LStickRight:
                    return PadManager.IsInput(PadManager.BUTTON.slRight);
                case ControllerButton.RStickUp:
                    return PadManager.IsInput(PadManager.BUTTON.srUp);
                case ControllerButton.RStickDown:
                    return PadManager.IsInput(PadManager.BUTTON.srDown);
                case ControllerButton.RStickLeft:
                    return PadManager.IsInput(PadManager.BUTTON.srLeft);
                case ControllerButton.RStickRight:
                    return PadManager.IsInput(PadManager.BUTTON.srRight);
                case ControllerButton.L3:
                case ControllerButton.R3:
                    return false;  // Game has no L3/R3 flags, SDL3 only
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if a controller button was just pressed this frame.
        /// </summary>
        private static bool IsControllerButtonTriggered(ControllerButton button)
        {
            if (_useSDL && SDLController.IsAvailable)
            {
                return IsControllerButtonTriggeredSDL(button);
            }
            return IsControllerButtonTriggeredPadManager(button);
        }

        private static bool IsControllerButtonTriggeredSDL(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.DPadUp:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadUp);
                case ControllerButton.DPadDown:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadDown);
                case ControllerButton.DPadLeft:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadLeft);
                case ControllerButton.DPadRight:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadRight);
                case ControllerButton.A:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.A);
                case ControllerButton.B:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.B);
                case ControllerButton.X:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.X);
                case ControllerButton.Y:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.Y);
                case ControllerButton.LB:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.LeftShoulder);
                case ControllerButton.RB:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.RightShoulder);
                case ControllerButton.LT:
                    return SDLController.IsLeftTriggerTriggered();
                case ControllerButton.RT:
                    return SDLController.IsRightTriggerTriggered();
                case ControllerButton.Start:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.Start);
                case ControllerButton.Select:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.Back);
                case ControllerButton.RStickUp:
                    return SDLController.IsRightStickUpTriggered();
                case ControllerButton.RStickDown:
                    return SDLController.IsRightStickDownTriggered();
                case ControllerButton.RStickLeft:
                    return SDLController.IsRightStickLeftTriggered();
                case ControllerButton.RStickRight:
                    return SDLController.IsRightStickRightTriggered();
                case ControllerButton.LStickUp:
                case ControllerButton.LStickDown:
                case ControllerButton.LStickLeft:
                case ControllerButton.LStickRight:
                    return IsControllerButtonTriggeredPadManager(button);
                case ControllerButton.L3:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.LeftStick);
                case ControllerButton.R3:
                    return SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.RightStick);
                default:
                    return false;
            }
        }

        private static bool IsControllerButtonTriggeredPadManager(ControllerButton button)
        {
            switch (button)
            {
                case ControllerButton.DPadUp:
                    return PadManager.IsTrigger(PadManager.BUTTON.dUp);
                case ControllerButton.DPadDown:
                    return PadManager.IsTrigger(PadManager.BUTTON.dDown);
                case ControllerButton.DPadLeft:
                    return PadManager.IsTrigger(PadManager.BUTTON.dLeft);
                case ControllerButton.DPadRight:
                    return PadManager.IsTrigger(PadManager.BUTTON.dRight);
                case ControllerButton.A:
                    return PadManager.IsTrigger(PadManager.BUTTON.bCross);
                case ControllerButton.B:
                    return PadManager.IsTrigger(PadManager.BUTTON.bCircle);
                case ControllerButton.X:
                    return PadManager.IsTrigger(PadManager.BUTTON.bSquare);
                case ControllerButton.Y:
                    return PadManager.IsTrigger(PadManager.BUTTON.bTriangle);
                case ControllerButton.LB:
                    return PadManager.IsTrigger(PadManager.BUTTON.bL);
                case ControllerButton.RB:
                    return PadManager.IsTrigger(PadManager.BUTTON.bR);
                case ControllerButton.LT:
                case ControllerButton.RT:
                    return false;  // PadManager doesn't support triggers
                case ControllerButton.Start:
                    return PadManager.IsTrigger(PadManager.BUTTON.bStart);
                case ControllerButton.Select:
                    return PadManager.IsTrigger(PadManager.BUTTON.bSelect);
                case ControllerButton.LStickUp:
                    return PadManager.IsTrigger(PadManager.BUTTON.slUp);
                case ControllerButton.LStickDown:
                    return PadManager.IsTrigger(PadManager.BUTTON.slDown);
                case ControllerButton.LStickLeft:
                    return PadManager.IsTrigger(PadManager.BUTTON.slLeft);
                case ControllerButton.LStickRight:
                    return PadManager.IsTrigger(PadManager.BUTTON.slRight);
                case ControllerButton.RStickUp:
                    return PadManager.IsTrigger(PadManager.BUTTON.srUp);
                case ControllerButton.RStickDown:
                    return PadManager.IsTrigger(PadManager.BUTTON.srDown);
                case ControllerButton.RStickLeft:
                    return PadManager.IsTrigger(PadManager.BUTTON.srLeft);
                case ControllerButton.RStickRight:
                    return PadManager.IsTrigger(PadManager.BUTTON.srRight);
                case ControllerButton.L3:
                case ControllerButton.R3:
                    return false;  // Game has no L3/R3 flags, SDL3 only
                default:
                    return false;
            }
        }

        // Unity trigger axis names to try (for PadManager fallback when SDL3 is unavailable)
        private static readonly string[] LeftTriggerAxes = { "Axis 9", "Axis 3", "JoystickAxis9", "JoystickAxis3" };
        private static readonly string[] RightTriggerAxes = { "Axis 10", "Axis 3", "JoystickAxis10", "JoystickAxis3" };
        private static string _workingLeftAxis = null;
        private static string _workingRightAxis = null;
        private const float UnityTriggerThreshold = 0.3f;

        /// <summary>
        /// Check if a trigger is held using Unity's Input system (PadManager fallback).
        /// </summary>
        private static bool IsUnityTriggerHeld(bool isLeft)
        {
            // Try cached axis first
            string cachedAxis = isLeft ? _workingLeftAxis : _workingRightAxis;
            if (cachedAxis != null)
            {
                try
                {
                    return Mathf.Abs(Input.GetAxisRaw(cachedAxis)) > UnityTriggerThreshold;
                }
                catch
                {
                    if (isLeft) _workingLeftAxis = null;
                    else _workingRightAxis = null;
                }
            }

            // Try to find a working axis
            string[] axesToTry = isLeft ? LeftTriggerAxes : RightTriggerAxes;
            foreach (var axisName in axesToTry)
            {
                try
                {
                    float value = Mathf.Abs(Input.GetAxisRaw(axisName));
                    if (value > 0.01f)
                    {
                        if (isLeft) _workingLeftAxis = axisName;
                        else _workingRightAxis = axisName;
                    }
                    if (value > UnityTriggerThreshold) return true;
                }
                catch { }
            }

            // Fallback: try joystick button approach
            try
            {
                int buttonIndex = isLeft ? 4 : 5;
                if (Input.GetKey((KeyCode)(KeyCode.JoystickButton0 + buttonIndex)))
                    return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Load bindings from config file.
        /// </summary>
        private static void LoadConfig()
        {
            if (!System.IO.File.Exists(_configPath))
            {
                DebugLogger.Log("[ModInputManager] Config file not found, creating default");
                SaveDefaultConfig();
                return;
            }

            try
            {
                var lines = System.IO.File.ReadAllLines(_configPath);
                string currentSection = "";

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Skip comments and empty lines
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        continue;

                    // Handle section headers
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.Substring(1, trimmed.Length - 2).ToLower();
                        continue;
                    }

                    // Parse key=value
                    var equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex < 0) continue;

                    var actionName = trimmed.Substring(0, equalsIndex).Trim();
                    var bindingStr = trimmed.Substring(equalsIndex + 1).Trim();

                    // Ensure action exists
                    if (!_actions.ContainsKey(actionName))
                    {
                        _actions[actionName] = new ActionBindings();
                    }

                    // Parse binding based on section
                    if (string.IsNullOrEmpty(bindingStr) || bindingStr.ToLower() == "none")
                    {
                        // Unbind this action for this input type
                        if (currentSection == "keyboard")
                            _actions[actionName].KeyboardBinding = null;
                        else if (currentSection == "controller")
                            _actions[actionName].ControllerBinding = null;
                        continue;
                    }

                    var binding = InputConfig.ParseBinding(bindingStr);
                    if (binding != null)
                    {
                        if (currentSection == "keyboard" && binding.IsKeyboard)
                        {
                            _actions[actionName].KeyboardBinding = binding;
                        }
                        else if (currentSection == "controller" && !binding.IsKeyboard)
                        {
                            // Validate controller bindings require modifiers for game buttons
                            if (ValidateControllerBinding(binding, actionName))
                            {
                                _actions[actionName].ControllerBinding = binding;
                            }
                        }
                        else if (currentSection == "keyboard" && !binding.IsKeyboard)
                        {
                            DebugLogger.Log($"[ModInputManager] Warning: {actionName} in [Keyboard] section has controller binding, skipping");
                        }
                        else if (currentSection == "controller" && binding.IsKeyboard)
                        {
                            DebugLogger.Log($"[ModInputManager] Warning: {actionName} in [Controller] section has keyboard binding, skipping");
                        }
                    }
                    else
                    {
                        DebugLogger.Log($"[ModInputManager] Warning: Could not parse binding for {actionName}: {bindingStr}");
                    }
                }

                DebugLogger.Log($"[ModInputManager] Loaded config with {_actions.Count} actions");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ModInputManager] Error loading config: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate that controller bindings don't conflict with game controls.
        /// </summary>
        private static bool ValidateControllerBinding(InputBinding binding, string actionName)
        {
            if (binding.IsKeyboard) return true;

            // These buttons are safe without modifiers
            var safeButtons = new HashSet<ControllerButton>
            {
                ControllerButton.LT, ControllerButton.RT,
                ControllerButton.RStickUp, ControllerButton.RStickDown,
                ControllerButton.RStickLeft, ControllerButton.RStickRight,
                ControllerButton.L3, ControllerButton.R3,
                ControllerButton.None
            };

            if (safeButtons.Contains(binding.MainButton))
                return true;

            // Otherwise, require a modifier
            if (binding.ModifierButton == ControllerButton.None)
            {
                DebugLogger.Log($"[ModInputManager] Warning: {actionName} uses a game button without modifier, skipping");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Save the default config file.
        /// </summary>
        private static void SaveDefaultConfig()
        {
            var content = @"; DigimonNOAccess Hotkey Configuration
; =====================================
;
; This file has two sections:
;   [Keyboard] - Keyboard bindings for each action
;   [Controller] - Controller bindings for each action
;
; KEYBOARD FORMAT:
;   Single key: F1, F2, A, B, Space, etc.
;   With modifiers: Ctrl+F1, Alt+S, Shift+Tab
;
; CONTROLLER FORMAT:
;   Buttons: A, B, X, Y, LB, RB, LT, RT, Start, Select
;   D-Pad: DPadUp, DPadDown, DPadLeft, DPadRight
;   Sticks: LStickUp, LStickDown, RStickUp, RStickDown, etc.
;   Stick press: L3, R3 (mod-only, game doesn't use these)
;   Combos: LB+DPadUp, RT+A, RB+DPadDown, etc.
;
; IMPORTANT: Controller bindings using game buttons (A, B, X, Y, D-Pad,
; LB, RB, Start, Select) MUST include a modifier to avoid conflicts.
; Safe without modifier: Right Stick directions, LT, RT
;
; Set to 'None' to disable a binding.
; Lines starting with ; or # are comments.
;
; FIXED KEYS (not configurable):
;   F8 = Reload this config file
;   F9 = Toggle input debug mode

[Keyboard]
; === Global ===
RepeatLast = F1
AnnounceStatus = F2
ToggleVoicedText = F5

; === Compass Direction ===
; Announce which compass direction the camera is facing
CompassDirection = U

; === Field Partner Status ===
Partner1Status = F3
Partner2Status = F4

; === Battle Enemy Info (per-enemy) ===
BattleEnemy1 = F6
BattleEnemy2 = F7
BattleEnemy3 = F11

; === Battle Order Power ===
BattleOrderPower = F12

; === Navigation List ===
; Cycle through categories (NPCs, Items, Transitions, Enemies)
NavNextCategory = O
NavPrevCategory = I
; Cycle through events within the selected category
NavPrevEvent = J
NavCurrentEvent = K
NavNextEvent = L
; Announce path to the selected event
NavToEvent = P
; Toggle auto-walk (when enabled, pathfinding also walks the player)
ToggleAutoWalk = Shift+P

; === Training Menu ===
; Announce stats or bonus for each partner (reads whichever tab is active)
TrainingP1Info = F3
TrainingP2Info = F4

; === Shop/Trade Menu ===
; Announce current bits (works in shop, trade, restaurant, and selection menus)
ShopCheckBits = B

[Controller]
; === Global ===
RepeatLast = None
AnnounceStatus = None
ToggleVoicedText = None

; === Compass Direction ===
; Announce which compass direction the camera is facing
CompassDirection = RT+DPadUp

; === Field Partner Status ===
Partner1Status = RT+DPadRight
Partner2Status = RT+DPadLeft

; === Battle Enemy Info (per-enemy) ===
BattleEnemy1 = RStickUp
BattleEnemy2 = RStickDown
BattleEnemy3 = RStickLeft

; === Battle Order Power ===
BattleOrderPower = RStickRight

; === Navigation List ===
; Cycle through categories (NPCs, Items, Transitions, Enemies)
NavNextCategory = LT+DPadRight
NavPrevCategory = LT+DPadLeft
; Cycle through events within the selected category
NavPrevEvent = LT+DPadUp
NavCurrentEvent = None
NavNextEvent = LT+DPadDown
; Start path to the selected event
NavToEvent = L3
; Toggle auto-walk (when enabled, pathfinding also walks the player)
ToggleAutoWalk = R3

; === Training Menu ===
; Announce stats or bonus for each partner (reads whichever tab is active)
TrainingP1Info = RT+DPadUp
TrainingP2Info = LT+DPadUp

; === Shop/Trade Menu ===
; Announce current bits (works in shop, trade, restaurant, and selection menus)
ShopCheckBits = L3
";

            try
            {
                System.IO.File.WriteAllText(_configPath, content);
                DebugLogger.Log("[ModInputManager] Created default config file");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ModInputManager] Error creating config: {ex.Message}");
            }
        }

        /// <summary>
        /// Reload config from file (for runtime updates).
        /// </summary>
        public static void ReloadConfig()
        {
            RegisterDefaultBindings();  // Reset to defaults first
            LoadConfig();
            ScreenReader.Say("Hotkey configuration reloaded");
        }

        // ========== Rebinding Support ==========

        /// <summary>
        /// Context determines which actions can share bindings.
        /// Actions in the same context or Global context cannot share bindings.
        /// </summary>
        public enum ActionContext { Global, Field, Battle, Training, Shop }

        private static readonly Dictionary<string, ActionContext> _actionContexts = new Dictionary<string, ActionContext>
        {
            {"RepeatLast", ActionContext.Global},
            {"AnnounceStatus", ActionContext.Global},
            {"ToggleVoicedText", ActionContext.Global},
            {"CompassDirection", ActionContext.Field},
            {"Partner1Status", ActionContext.Field},
            {"Partner2Status", ActionContext.Field},
            {"NavNextCategory", ActionContext.Field},
            {"NavPrevCategory", ActionContext.Field},
            {"NavPrevEvent", ActionContext.Field},
            {"NavCurrentEvent", ActionContext.Field},
            {"NavNextEvent", ActionContext.Field},
            {"NavToEvent", ActionContext.Field},
            {"ToggleAutoWalk", ActionContext.Field},
            {"BattleEnemy1", ActionContext.Battle},
            {"BattleEnemy2", ActionContext.Battle},
            {"BattleEnemy3", ActionContext.Battle},
            {"BattleOrderPower", ActionContext.Battle},
            {"TrainingP1Info", ActionContext.Training},
            {"TrainingP2Info", ActionContext.Training},
            {"ShopCheckBits", ActionContext.Shop},
        };

        // Keys the game uses natively - never offer these as plain keyboard bindings
        private static readonly HashSet<KeyCode> _blockedKeys = new HashSet<KeyCode>
        {
            KeyCode.Space, KeyCode.Backspace, KeyCode.Return, KeyCode.Escape, KeyCode.Tab,
            KeyCode.C, KeyCode.V, KeyCode.Q, KeyCode.E,
            KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
        };

        // Controller buttons safe without a modifier
        private static readonly HashSet<ControllerButton> _safeControllerButtons = new HashSet<ControllerButton>
        {
            ControllerButton.RStickUp, ControllerButton.RStickDown,
            ControllerButton.RStickLeft, ControllerButton.RStickRight,
            ControllerButton.L3, ControllerButton.R3,
        };

        // Only LT/RT are valid modifiers (they suppress game input when held)
        private static readonly ControllerButton[] _modifiers =
        {
            ControllerButton.LT, ControllerButton.RT,
        };

        // Buttons that can be used as combo targets with LT/RT
        private static readonly ControllerButton[] _comboTargets =
        {
            ControllerButton.DPadUp, ControllerButton.DPadDown, ControllerButton.DPadLeft, ControllerButton.DPadRight,
            ControllerButton.A, ControllerButton.B, ControllerButton.X, ControllerButton.Y,
            ControllerButton.LB, ControllerButton.RB,
        };

        // Cached allowed binding lists (built once)
        private static List<InputBinding> _allowedKeyboard;
        private static List<InputBinding> _allowedController;

        public static ActionContext GetActionContext(string actionName)
        {
            return _actionContexts.TryGetValue(actionName, out var ctx) ? ctx : ActionContext.Global;
        }

        /// <summary>
        /// Get the current binding for an action (keyboard or controller).
        /// </summary>
        public static InputBinding GetBinding(string actionName, bool controller)
        {
            if (_actions.TryGetValue(actionName, out var bindings))
                return controller ? bindings.ControllerBinding : bindings.KeyboardBinding;
            return null;
        }

        /// <summary>
        /// Update a binding at runtime and apply immediately.
        /// </summary>
        public static void SetBinding(string actionName, bool controller, InputBinding binding)
        {
            if (!_actions.ContainsKey(actionName))
                _actions[actionName] = new ActionBindings();

            if (controller)
                _actions[actionName].ControllerBinding = binding;
            else
                _actions[actionName].KeyboardBinding = binding;
        }

        /// <summary>
        /// Compare two bindings for equality. Both null = equal.
        /// </summary>
        public static bool BindingsEqual(InputBinding a, InputBinding b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            if (a.IsKeyboard != b.IsKeyboard) return false;

            if (a.IsKeyboard)
                return a.Key == b.Key && a.RequiresCtrl == b.RequiresCtrl
                    && a.RequiresAlt == b.RequiresAlt && a.RequiresShift == b.RequiresShift;

            return a.MainButton == b.MainButton && a.ModifierButton == b.ModifierButton;
        }

        /// <summary>
        /// Check if a proposed binding would conflict with another action
        /// in the same or overlapping context.
        /// </summary>
        public static bool WouldConflict(string actionName, bool controller, InputBinding proposed)
        {
            if (proposed == null) return false; // None never conflicts

            var myContext = GetActionContext(actionName);

            foreach (var kvp in _actions)
            {
                if (kvp.Key == actionName) continue;

                var otherBinding = controller ? kvp.Value.ControllerBinding : kvp.Value.KeyboardBinding;
                if (otherBinding == null) continue;
                if (!BindingsEqual(proposed, otherBinding)) continue;

                var otherContext = GetActionContext(kvp.Key);
                // Conflict if either is Global, or same context
                if (myContext == ActionContext.Global || otherContext == ActionContext.Global || myContext == otherContext)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Get all valid keyboard binding options for the pick-from-list UI.
        /// Index 0 is null (None). Built once and cached.
        /// </summary>
        public static List<InputBinding> GetAllowedKeyboardBindings()
        {
            if (_allowedKeyboard != null) return _allowedKeyboard;

            _allowedKeyboard = new List<InputBinding>();
            _allowedKeyboard.Add(null); // None

            // F1-F12
            for (int i = 0; i < 12; i++)
                _allowedKeyboard.Add(new InputBinding(KeyCode.F1 + i));

            // Safe plain letters
            KeyCode[] safeLetters =
            {
                KeyCode.B, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.I, KeyCode.J,
                KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.N, KeyCode.O, KeyCode.P,
                KeyCode.R, KeyCode.T, KeyCode.U, KeyCode.X, KeyCode.Y, KeyCode.Z,
            };

            foreach (var key in safeLetters)
                _allowedKeyboard.Add(new InputBinding(key));

            // Shift + same letters
            foreach (var key in safeLetters)
                _allowedKeyboard.Add(new InputBinding(key, false, false, true));

            return _allowedKeyboard;
        }

        /// <summary>
        /// Get all valid controller binding options for the pick-from-list UI.
        /// Index 0 is null (None). Built once and cached.
        /// </summary>
        public static List<InputBinding> GetAllowedControllerBindings()
        {
            if (_allowedController != null) return _allowedController;

            _allowedController = new List<InputBinding>();
            _allowedController.Add(null); // None

            // Safe buttons (no modifier needed)
            foreach (var btn in _safeControllerButtons)
                _allowedController.Add(new InputBinding(btn));

            // LT/RT + combo targets (face, DPad, LB, RB - all suppressed when LT/RT held)
            foreach (var mod in _modifiers)
                foreach (var btn in _comboTargets)
                    _allowedController.Add(new InputBinding(mod, btn));

            return _allowedController;
        }

        /// <summary>
        /// Save current bindings to hotkeys.ini.
        /// </summary>
        public static void SaveConfig()
        {
            try
            {
                // Ordered list of actions for consistent output
                string[] actionOrder =
                {
                    "RepeatLast", "AnnounceStatus", "ToggleVoicedText",
                    "CompassDirection",
                    "Partner1Status", "Partner2Status",
                    "BattleEnemy1", "BattleEnemy2", "BattleEnemy3", "BattleOrderPower",
                    "NavNextCategory", "NavPrevCategory",
                    "NavPrevEvent", "NavCurrentEvent", "NavNextEvent",
                    "NavToEvent", "ToggleAutoWalk",
                    "TrainingP1Info", "TrainingP2Info",
                    "ShopCheckBits",
                };

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("; DigimonNOAccess Hotkey Configuration");
                sb.AppendLine("; Modified by in-game settings menu");
                sb.AppendLine(";");
                sb.AppendLine("; KEYBOARD: F1-F12, letters (not game keys), Shift+key");
                sb.AppendLine("; CONTROLLER: RStick, L3, R3 (solo), or modifier+button (LT/RT/LB/RB + DPad/face)");
                sb.AppendLine("; Set to 'None' to disable. F8 reloads this file in-game.");
                sb.AppendLine();

                sb.AppendLine("[Keyboard]");
                foreach (var action in actionOrder)
                {
                    if (!_actions.ContainsKey(action)) continue;
                    var binding = _actions[action].KeyboardBinding;
                    sb.AppendLine($"{action} = {(binding != null ? binding.DisplayName : "None")}");
                }

                sb.AppendLine();
                sb.AppendLine("[Controller]");
                foreach (var action in actionOrder)
                {
                    if (!_actions.ContainsKey(action)) continue;
                    var binding = _actions[action].ControllerBinding;
                    sb.AppendLine($"{action} = {(binding != null ? binding.DisplayName : "None")}");
                }

                System.IO.File.WriteAllText(_configPath, sb.ToString());
                DebugLogger.Log("[ModInputManager] Config saved from menu");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ModInputManager] Error saving config: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Holds both keyboard and controller bindings for an action.
    /// </summary>
    public class ActionBindings
    {
        public InputBinding KeyboardBinding { get; set; }
        public InputBinding ControllerBinding { get; set; }
    }

    /// <summary>
    /// Controller button enumeration with readable names.
    /// </summary>
    public enum ControllerButton
    {
        None,

        // Face buttons
        A,      // Cross on PlayStation
        B,      // Circle on PlayStation
        X,      // Square on PlayStation
        Y,      // Triangle on PlayStation

        // Shoulder buttons
        LB,     // L1 on PlayStation
        RB,     // R1 on PlayStation

        // Triggers
        LT,     // L2 on PlayStation
        RT,     // R2 on PlayStation

        // D-Pad
        DPadUp,
        DPadDown,
        DPadLeft,
        DPadRight,

        // Special buttons
        Start,
        Select,

        // Left Stick directions
        LStickUp,
        LStickDown,
        LStickLeft,
        LStickRight,

        // Right Stick directions
        RStickUp,
        RStickDown,
        RStickLeft,
        RStickRight,

        // Stick press buttons (L3/R3) - mod-only, game doesn't use these
        L3,     // Left stick press
        R3      // Right stick press
    }

    /// <summary>
    /// Represents a single input binding (keyboard or controller).
    /// </summary>
    public class InputBinding
    {
        public bool IsKeyboard { get; private set; }

        // Keyboard binding
        public KeyCode Key { get; private set; }
        public bool RequiresCtrl { get; private set; }
        public bool RequiresAlt { get; private set; }
        public bool RequiresShift { get; private set; }

        // Controller binding
        public ControllerButton MainButton { get; private set; }
        public ControllerButton ModifierButton { get; private set; }

        /// <summary>
        /// Human-readable display name for this binding.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (IsKeyboard)
                {
                    var parts = new List<string>();
                    if (RequiresCtrl) parts.Add("Ctrl");
                    if (RequiresAlt) parts.Add("Alt");
                    if (RequiresShift) parts.Add("Shift");
                    parts.Add(Key.ToString());
                    return string.Join("+", parts);
                }
                else
                {
                    if (ModifierButton != ControllerButton.None)
                        return $"{ModifierButton}+{MainButton}";
                    else
                        return MainButton.ToString();
                }
            }
        }

        // Keyboard constructor (single key)
        public InputBinding(KeyCode key)
        {
            IsKeyboard = true;
            Key = key;
        }

        // Keyboard constructor (with modifiers)
        public InputBinding(KeyCode key, bool ctrl, bool alt, bool shift)
        {
            IsKeyboard = true;
            Key = key;
            RequiresCtrl = ctrl;
            RequiresAlt = alt;
            RequiresShift = shift;
        }

        // Controller constructor (single button)
        public InputBinding(ControllerButton button)
        {
            IsKeyboard = false;
            MainButton = button;
            ModifierButton = ControllerButton.None;
        }

        // Controller constructor (modifier + button)
        public InputBinding(ControllerButton modifier, ControllerButton button)
        {
            IsKeyboard = false;
            ModifierButton = modifier;
            MainButton = button;
        }
    }
}
