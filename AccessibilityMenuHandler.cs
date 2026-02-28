using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Full accessibility settings menu with category-based navigation.
    /// Activated from the game's options menu via OptionPanelPatch.
    /// TTS-only output with game sound effects for native feel.
    /// </summary>
    public class AccessibilityMenuHandler : IAccessibilityHandler
    {
        public int Priority => 4;

        /// <summary>
        /// Singleton instance, set in constructor. Used by OptionPanelPatch.
        /// </summary>
        public static AccessibilityMenuHandler Instance { get; private set; }

        private bool _isActive;
        private bool _pendingDeactivate;
        private MenuLevel _level = MenuLevel.Categories;
        private int _categoryIndex;
        private int _settingIndex;
        private bool _showControllerBindings = false;

        // Keybind listen mode
        private bool _isListening;
        private string _listeningAction;
        private bool _listeningController;
        private bool _waitForRelease; // blocks menu input until all buttons released after listen mode

        // Input state with repeat timing to match game feel
        // Game uses 16-frame initial delay + 4-frame repeat interval at 60fps
        private const float RepeatFirstDelay = 16f / 60f; // ~267ms
        private const float RepeatInterval = 4f / 60f;    // ~67ms

        private bool _wasUpPressed;
        private bool _wasDownPressed;
        private bool _wasLeftPressed;
        private bool _wasRightPressed;
        private bool _wasConfirmPressed;
        private bool _wasCancelPressed;

        private float _upHoldTime;
        private float _downHoldTime;
        private float _leftHoldTime;
        private float _rightHoldTime;
        private float _upNextRepeat;
        private float _downNextRepeat;
        private float _leftNextRepeat;
        private float _rightNextRepeat;

        private readonly SettingsCategory[] _categories;

        private enum MenuLevel
        {
            Categories,
            Settings
        }

        public AccessibilityMenuHandler()
        {
            Instance = this;
            _categories = BuildCategories();
        }

        public bool IsOpen() => _isActive;

        public void Activate()
        {
            _isActive = true;
            _level = MenuLevel.Categories;
            _categoryIndex = 0;
            _settingIndex = 0;

            // Mark confirm/cancel as already pressed to consume the button press
            // that triggered this activation (prevents immediately entering first category)
            _wasConfirmPressed = true;
            _wasCancelPressed = true;
            _wasUpPressed = _wasDownPressed = _wasLeftPressed = _wasRightPressed = false;

            PlaySe(CriSoundManager.SE_OpenWindow1);
            // Match game menu announcement pattern: "MenuName. ItemName, index of total"
            var firstCat = _categories[0];
            ScreenReader.Say($"Accessibility Settings. {firstCat.Name}, 1 of {_categories.Length}. {firstCat.Description}");

            DebugLogger.Log("[AccessibilityMenu] Activated");
        }

        public void Deactivate()
        {
            _isActive = false;
            PlaySe(CriSoundManager.SE_CloseWindow1);
            DebugLogger.Log("[AccessibilityMenu] Deactivated");
        }

        public void Update()
        {
            if (!_isActive)
                return;

            // Deferred deactivation: we stayed "open" for one extra frame so that
            // CheckInputKey_Prefix blocked the game's input (consuming the cancel press).
            // Now the cancel trigger has expired and we can safely return to TOP.
            if (_pendingDeactivate)
            {
                _pendingDeactivate = false;
                _isActive = false;
                DebugLogger.Log("[AccessibilityMenu] Deactivated (deferred)");
                OptionPanelPatch.ReturnToTopMenu();
                return;
            }

            HandleInput();
        }

        public void AnnounceStatus()
        {
            if (!_isActive)
                return;

            AnnounceCurrentItem();
        }

        private void HandleInput()
        {
            if (_isListening)
            {
                ListenForBinding();
                return;
            }

            bool upPressed = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            bool downPressed = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            bool leftPressed = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            bool rightPressed = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
            bool confirmPressed = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space);
            bool cancelPressed = Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Backspace);

            if (ModInputManager.IsUsingSDL)
            {
                upPressed = upPressed || SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadUp);
                downPressed = downPressed || SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadDown);
                leftPressed = leftPressed || SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadLeft);
                rightPressed = rightPressed || SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.DPadRight);
                confirmPressed = confirmPressed || SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.A);
                cancelPressed = cancelPressed || SDLController.IsButtonHeld(SDLController.SDL_GameControllerButton.B);
            }

            // After exiting listen mode, block all input until every button is released.
            // This prevents held DPad/face buttons from leaking into menu navigation.
            if (_waitForRelease)
            {
                if (!upPressed && !downPressed && !leftPressed && !rightPressed && !confirmPressed && !cancelPressed)
                {
                    _waitForRelease = false;
                    _wasUpPressed = _wasDownPressed = _wasLeftPressed = _wasRightPressed = false;
                    _wasConfirmPressed = _wasCancelPressed = false;
                }
                return;
            }

            float dt = Time.unscaledDeltaTime;

            // Directional inputs use repeat timing to match game feel
            if (CheckRepeat(upPressed, ref _wasUpPressed, ref _upHoldTime, ref _upNextRepeat, dt))
                HandleUp();
            if (CheckRepeat(downPressed, ref _wasDownPressed, ref _downHoldTime, ref _downNextRepeat, dt))
                HandleDown();
            if (CheckRepeat(leftPressed, ref _wasLeftPressed, ref _leftHoldTime, ref _leftNextRepeat, dt))
                HandleLeft();
            if (CheckRepeat(rightPressed, ref _wasRightPressed, ref _rightHoldTime, ref _rightNextRepeat, dt))
                HandleRight();

            // Confirm and cancel are edge-only (no repeat)
            if (confirmPressed && !_wasConfirmPressed)
                HandleConfirm();
            if (cancelPressed && !_wasCancelPressed)
                HandleCancel();

            _wasConfirmPressed = confirmPressed;
            _wasCancelPressed = cancelPressed;
        }

        /// <summary>
        /// Checks if a directional input should fire, matching the game's repeat timing:
        /// fires on first press, waits RepeatFirstDelay, then repeats every RepeatInterval.
        /// </summary>
        private bool CheckRepeat(bool pressed, ref bool wasPressed, ref float holdTime, ref float nextRepeat, float dt)
        {
            if (!pressed)
            {
                wasPressed = false;
                holdTime = 0f;
                return false;
            }

            // First frame pressed — fire immediately
            if (!wasPressed)
            {
                wasPressed = true;
                holdTime = 0f;
                nextRepeat = RepeatFirstDelay;
                return true;
            }

            // Held — accumulate time and check for repeat
            holdTime += dt;
            if (holdTime >= nextRepeat)
            {
                nextRepeat += RepeatInterval;
                return true;
            }

            return false;
        }

        private void HandleUp()
        {
            if (_level == MenuLevel.Categories)
            {
                _categoryIndex--;
                if (_categoryIndex < 0)
                    _categoryIndex = _categories.Length - 1;
            }
            else
            {
                var items = _categories[_categoryIndex].Items;
                if (items.Length <= 1) return;
                ClampSettingIndex(items);
                _settingIndex--;
                if (_settingIndex < 0)
                    _settingIndex = items.Length - 1;
            }
            PlaySe(CriSoundManager.SE_MoveCursor1);
            AnnounceCurrentItem();
        }

        private void HandleDown()
        {
            if (_level == MenuLevel.Categories)
            {
                _categoryIndex++;
                if (_categoryIndex >= _categories.Length)
                    _categoryIndex = 0;
            }
            else
            {
                var items = _categories[_categoryIndex].Items;
                if (items.Length <= 1) return;
                ClampSettingIndex(items);
                _settingIndex++;
                if (_settingIndex >= items.Length)
                    _settingIndex = 0;
            }
            PlaySe(CriSoundManager.SE_MoveCursor1);
            AnnounceCurrentItem();
        }

        private void HandleLeft()
        {
            if (_level == MenuLevel.Settings)
            {
                var items = _categories[_categoryIndex].Items;
                ClampSettingIndex(items);
                var item = items[_settingIndex];
                if (item is ToggleSetting || item is ReadOnlySetting || item is KeybindSetting) return;
                item.OnLeft();
                AnnounceAfterChange(item);
            }
        }

        private void HandleRight()
        {
            if (_level == MenuLevel.Settings)
            {
                var items = _categories[_categoryIndex].Items;
                ClampSettingIndex(items);
                var item = items[_settingIndex];
                if (item is ToggleSetting || item is ReadOnlySetting || item is KeybindSetting) return;
                item.OnRight();
                AnnounceAfterChange(item);
            }
        }

        private void HandleConfirm()
        {
            if (_level == MenuLevel.Categories)
            {
                _level = MenuLevel.Settings;
                _settingIndex = 0;
                PlaySe(CriSoundManager.SE_OK);

                var cat = _categories[_categoryIndex];
                var items = cat.Items;
                ScreenReader.Say(cat.Name);
                QueueAnnounceCurrentItem();
            }
            else
            {
                var items = _categories[_categoryIndex].Items;
                ClampSettingIndex(items);
                var item = items[_settingIndex];

                if (item is KeybindSetting keybind)
                {
                    _isListening = true;
                    _listeningAction = keybind.ActionName;
                    _listeningController = keybind.IsController;
                    PlaySe(CriSoundManager.SE_OK);
                    ScreenReader.Say($"Press new binding for {keybind.ActionName}, or cancel to go back");
                    return;
                }

                item.OnConfirm();
                PlaySe(CriSoundManager.SE_OK);
                AnnounceAfterChange(item);
            }
        }

        private void AnnounceAfterChange(SettingItem changedItem)
        {
            // Re-fetch items in case a toggle changed the list
            var items = _categories[_categoryIndex].Items;
            ClampSettingIndex(items);
            var currentItem = items[_settingIndex];
            string val = currentItem.GetValueText();
            if (val != null)
                ScreenReader.Say(val);
        }

        private void ClampSettingIndex(SettingItem[] items)
        {
            if (_settingIndex >= items.Length)
                _settingIndex = items.Length - 1;
            if (_settingIndex < 0)
                _settingIndex = 0;
        }

        private void HandleCancel()
        {
            if (_level == MenuLevel.Settings)
            {
                // Back to categories
                _level = MenuLevel.Categories;
                PlaySe(CriSoundManager.SE_Cancel);
                var cat = _categories[_categoryIndex];
                ScreenReader.Say("Accessibility Settings.");
                QueueAnnounceCurrentItem();
            }
            else
            {
                // Back to game's options TOP menu.
                // Don't deactivate immediately — stay "open" for one more frame
                // so CheckInputKey_Prefix keeps blocking the game's input.
                // The actual deactivation happens next frame in Update().
                PlaySe(CriSoundManager.SE_Cancel);
                _pendingDeactivate = true;
            }
        }

        // ========== Keybind Listen Mode ==========

        /// <summary>
        /// Exits listen mode and blocks all menu input until every button is released.
        /// This prevents held DPad/face buttons from leaking into menu navigation.
        /// </summary>
        private void ExitListenMode()
        {
            _isListening = false;
            _waitForRelease = true;
        }

        private void ListenForBinding()
        {
            // Cancel: plain B/Escape/Backspace (no modifier held) exits listen mode
            bool cancelPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);
            if (ModInputManager.IsUsingSDL)
            {
                // Only treat B as cancel if LT/RT are NOT held (otherwise it's a combo attempt)
                bool ltHeld = SDLController.IsLeftTriggerHeld();
                bool rtHeld = SDLController.IsRightTriggerHeld();
                if (!ltHeld && !rtHeld)
                    cancelPressed = cancelPressed || SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.East);
            }

            if (cancelPressed)
            {
                ExitListenMode();
                PlaySe(CriSoundManager.SE_Cancel);
                AnnounceCurrentItem();
                return;
            }

            if (_listeningController)
                ListenForControllerBinding();
            else
                ListenForKeyboardBinding();
        }

        private void ListenForKeyboardBinding()
        {
            // Check modifier state
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // Scan for a main key press (skip modifier keys themselves)
            KeyCode pressed = KeyCode.None;

            // F1-F12
            for (int i = 0; i < 12; i++)
            {
                KeyCode fk = KeyCode.F1 + i;
                if (Input.GetKeyDown(fk)) { pressed = fk; break; }
            }

            // A-Z
            if (pressed == KeyCode.None)
            {
                for (int i = 0; i < 26; i++)
                {
                    KeyCode lk = KeyCode.A + i;
                    if (Input.GetKeyDown(lk)) { pressed = lk; break; }
                }
            }

            if (pressed == KeyCode.None) return;

            // Build the binding
            var binding = new InputBinding(pressed, ctrl, alt, shift);

            // Validate: check if it's in the allowed list
            if (!IsBindingAllowed(binding, false))
            {
                ExitListenMode();
                PlaySe(CriSoundManager.SE_Cancel);
                ScreenReader.Say($"{binding.DisplayName} is not allowed. Try a function key or a safe letter.");
                QueueAnnounceCurrentItem();
                return;
            }

            // Check conflicts with other actions
            if (ModInputManager.WouldConflict(_listeningAction, false, binding))
            {
                string conflict = FindConflictingAction(_listeningAction, false, binding);
                ExitListenMode();
                PlaySe(CriSoundManager.SE_Cancel);
                ScreenReader.Say($"{binding.DisplayName} is already used by {conflict}.");
                QueueAnnounceCurrentItem();
                return;
            }

            // Apply
            ModInputManager.SetBinding(_listeningAction, false, binding);
            ModInputManager.SaveConfig();
            ExitListenMode();
            PlaySe(CriSoundManager.SE_OK);
            ScreenReader.Say($"{_listeningAction} set to {binding.DisplayName}");
            QueueAnnounceCurrentItem();
        }

        private void ListenForControllerBinding()
        {
            if (!ModInputManager.IsUsingSDL || !SDLController.IsAvailable) return;

            // Check modifier state (LT/RT)
            bool ltHeld = SDLController.IsLeftTriggerHeld();
            bool rtHeld = SDLController.IsRightTriggerHeld();
            ControllerButton modifier = ControllerButton.None;
            if (ltHeld) modifier = ControllerButton.LT;
            else if (rtHeld) modifier = ControllerButton.RT;

            // Scan for a button press
            ControllerButton mainButton = ControllerButton.None;

            // Face buttons
            if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.A))
                mainButton = ControllerButton.A;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.B))
                mainButton = ControllerButton.B;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.X))
                mainButton = ControllerButton.X;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.Y))
                mainButton = ControllerButton.Y;
            // D-pad
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadUp))
                mainButton = ControllerButton.DPadUp;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadDown))
                mainButton = ControllerButton.DPadDown;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadLeft))
                mainButton = ControllerButton.DPadLeft;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.DPadRight))
                mainButton = ControllerButton.DPadRight;
            // Shoulder buttons (as combo targets)
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.LeftShoulder))
                mainButton = ControllerButton.LB;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.RightShoulder))
                mainButton = ControllerButton.RB;
            // Right stick
            else if (SDLController.IsRightStickUpTriggered())
                mainButton = ControllerButton.RStickUp;
            else if (SDLController.IsRightStickDownTriggered())
                mainButton = ControllerButton.RStickDown;
            else if (SDLController.IsRightStickLeftTriggered())
                mainButton = ControllerButton.RStickLeft;
            else if (SDLController.IsRightStickRightTriggered())
                mainButton = ControllerButton.RStickRight;
            // Stick press
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.LeftStick))
                mainButton = ControllerButton.L3;
            else if (SDLController.IsButtonTriggered(SDLController.SDL_GameControllerButton.RightStick))
                mainButton = ControllerButton.R3;

            if (mainButton == ControllerButton.None) return;

            // Build the binding
            var binding = (modifier != ControllerButton.None)
                ? new InputBinding(modifier, mainButton)
                : new InputBinding(mainButton);

            // Validate: check if it's in the allowed list
            if (!IsBindingAllowed(binding, true))
            {
                ExitListenMode();
                PlaySe(CriSoundManager.SE_Cancel);
                ScreenReader.Say($"{binding.DisplayName} is not allowed. Use right stick, L3, R3, or hold LT or RT with another button.");
                QueueAnnounceCurrentItem();
                return;
            }

            // Check conflicts
            if (ModInputManager.WouldConflict(_listeningAction, true, binding))
            {
                string conflict = FindConflictingAction(_listeningAction, true, binding);
                ExitListenMode();
                PlaySe(CriSoundManager.SE_Cancel);
                ScreenReader.Say($"{binding.DisplayName} is already used by {conflict}.");
                QueueAnnounceCurrentItem();
                return;
            }

            // Apply
            ModInputManager.SetBinding(_listeningAction, true, binding);
            ModInputManager.SaveConfig();
            ExitListenMode();
            PlaySe(CriSoundManager.SE_OK);
            ScreenReader.Say($"{_listeningAction} set to {binding.DisplayName}");
            QueueAnnounceCurrentItem();
        }

        private static bool IsBindingAllowed(InputBinding proposed, bool controller)
        {
            var allowedList = controller
                ? ModInputManager.GetAllowedControllerBindings()
                : ModInputManager.GetAllowedKeyboardBindings();

            foreach (var allowed in allowedList)
            {
                if (allowed != null && ModInputManager.BindingsEqual(proposed, allowed))
                    return true;
            }
            return false;
        }

        private static string FindConflictingAction(string actionName, bool controller, InputBinding proposed)
        {
            // List of all rebindable actions
            string[] actions =
            {
                "RepeatLast", "AnnounceStatus", "Partner1Status", "Partner2Status",
                "CompassDirection", "NavNextCategory", "NavPrevCategory",
                "NavPrevEvent", "NavCurrentEvent", "NavNextEvent", "NavToEvent",
                "ToggleAutoWalk", "BattleEnemy1", "BattleEnemy2", "BattleEnemy3",
                "BattleOrderPower", "TrainingP1Info", "TrainingP2Info", "ShopCheckBits",
            };

            var myContext = ModInputManager.GetActionContext(actionName);
            foreach (var other in actions)
            {
                if (other == actionName) continue;
                var otherBinding = ModInputManager.GetBinding(other, controller);
                if (otherBinding == null) continue;
                if (!ModInputManager.BindingsEqual(proposed, otherBinding)) continue;

                var otherContext = ModInputManager.GetActionContext(other);
                if (myContext == ModInputManager.ActionContext.Global ||
                    otherContext == ModInputManager.ActionContext.Global ||
                    myContext == otherContext)
                    return other;
            }
            return "another action";
        }

        private string BuildCurrentAnnouncement()
        {
            if (_level == MenuLevel.Categories)
            {
                var cat = _categories[_categoryIndex];
                string text = $"{cat.Name}, {_categoryIndex + 1} of {_categories.Length}";
                if (cat.Description != null)
                    text += $". {cat.Description}";
                return text;
            }
            else
            {
                var items = _categories[_categoryIndex].Items;
                var item = items[_settingIndex];
                string val = item.GetValueText();
                string announcement = item.Name;
                if (val != null)
                    announcement += $": {val}";
                announcement += $", {_settingIndex + 1} of {items.Length}";
                if (item.Description != null)
                    announcement += $". {item.Description}";
                return announcement;
            }
        }

        private void AnnounceCurrentItem()
        {
            ScreenReader.Say(BuildCurrentAnnouncement());
        }

        private void QueueAnnounceCurrentItem()
        {
            ScreenReader.SayQueued(BuildCurrentAnnouncement());
        }

        private static void PlaySe(string seName)
        {
            try
            {
                CriSoundManager.PlayCommonSe(seName);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AccessibilityMenu] Failed to play SE: {ex.Message}");
            }
        }

        // ========== Category/Setting Definitions ==========

        private SettingsCategory[] BuildCategories()
        {
            return new SettingsCategory[]
            {
                new SettingsCategory("Gameplay", "Gameplay settings", BuildGameplayItems),
                new SettingsCategory("Audio", "audio navigation sounds for nearby objects", BuildAudioItems),
                new SettingsCategory("Speech", "Screen reader and text to speech settings", BuildSpeechItems),
                new SettingsCategory("Keybindings", "View and change mod hotkeys", BuildKeybindingsItems),
            };
        }

        private SettingItem[] BuildGameplayItems()
        {
            return new SettingItem[]
            {
                new ReadOnlySetting("No settings yet", "More coming soon", "Gameplay settings will be added in a future update"),
            };
        }

        private SettingItem[] BuildAudioItems()
        {
            var items = new List<SettingItem>();

            // Toggles always at top
            items.Add(new ToggleSetting("Items",
                () => ModSettings.ItemsEnabled,
                v => { ModSettings.ItemsEnabled = v; ModSettings.Save(); },
                description: "Enable or disable audio for collectible items on the ground"));
            items.Add(new ToggleSetting("NPCs",
                () => ModSettings.NpcsEnabled,
                v => { ModSettings.NpcsEnabled = v; ModSettings.Save(); },
                description: "Enable or disable audio for nearby NPCs"));
            items.Add(new ToggleSetting("Enemies",
                () => ModSettings.EnemiesEnabled,
                v => { ModSettings.EnemiesEnabled = v; ModSettings.Save(); },
                description: "Enable or disable audio for enemies in the field"));
            items.Add(new ToggleSetting("Transitions",
                () => ModSettings.TransitionsEnabled,
                v => { ModSettings.TransitionsEnabled = v; ModSettings.Save(); },
                description: "Enable or disable audio for area exits and entrances"));
            // General volumes (always visible)
            items.Add(new PercentSliderSetting("Nearest Volume",
                () => ModSettings.NearestVolume,
                v => { ModSettings.NearestVolume = v; ModSettings.Save(); },
                0f, 1f, 0.05f,
                "Volume of the closest object. Use left and right to adjust"));
            items.Add(new PercentSliderSetting("Background Volume",
                () => ModSettings.BackgroundVolume,
                v => { ModSettings.BackgroundVolume = v; ModSettings.Save(); },
                0f, 1f, 0.05f,
                "Volume of objects beyond the nearest one"));

            // Per-type settings only shown when that type is enabled
            if (ModSettings.ItemsEnabled)
            {
                items.Add(new PercentSliderSetting("Item Volume",
                    () => ModSettings.ItemVolume,
                    v => { ModSettings.ItemVolume = v; ModSettings.Save(); },
                    0f, 1f, 0.05f,
                    "Volume for item sounds"));
                items.Add(new IntSliderSetting("Item Range",
                    () => (int)ModSettings.ItemRange,
                    v => { ModSettings.ItemRange = v; ModSettings.Save(); },
                    10, 200, 10,
                    "How far away items can be detected, in game units"));
                items.Add(new IntSliderSetting("Max Item Sounds",
                    () => ModSettings.MaxItemSounds,
                    v => { ModSettings.MaxItemSounds = v; ModSettings.Save(); },
                    1, 10, 1,
                    "Maximum number of item sounds that can play at once"));
            }

            if (ModSettings.NpcsEnabled)
            {
                items.Add(new PercentSliderSetting("NPC Volume",
                    () => ModSettings.NpcVolume,
                    v => { ModSettings.NpcVolume = v; ModSettings.Save(); },
                    0f, 1f, 0.05f,
                    "Volume for NPC sounds"));
                items.Add(new IntSliderSetting("NPC Range",
                    () => (int)ModSettings.NpcRange,
                    v => { ModSettings.NpcRange = v; ModSettings.Save(); },
                    10, 200, 10,
                    "How far away NPCs can be detected, in game units"));
                items.Add(new IntSliderSetting("Max NPC Sounds",
                    () => ModSettings.MaxNpcSounds,
                    v => { ModSettings.MaxNpcSounds = v; ModSettings.Save(); },
                    1, 10, 1,
                    "Maximum number of NPC sounds that can play at once"));
            }

            if (ModSettings.EnemiesEnabled)
            {
                items.Add(new PercentSliderSetting("Enemy Volume",
                    () => ModSettings.EnemyVolume,
                    v => { ModSettings.EnemyVolume = v; ModSettings.Save(); },
                    0f, 1f, 0.05f,
                    "Volume for enemy sounds"));
                items.Add(new IntSliderSetting("Enemy Range",
                    () => (int)ModSettings.EnemyRange,
                    v => { ModSettings.EnemyRange = v; ModSettings.Save(); },
                    10, 200, 10,
                    "How far away enemies can be detected, in game units"));
                items.Add(new IntSliderSetting("Max Enemy Sounds",
                    () => ModSettings.MaxEnemySounds,
                    v => { ModSettings.MaxEnemySounds = v; ModSettings.Save(); },
                    1, 10, 1,
                    "Maximum number of enemy sounds that can play at once"));
            }

            if (ModSettings.TransitionsEnabled)
            {
                items.Add(new PercentSliderSetting("Transition Volume",
                    () => ModSettings.TransitionVolume,
                    v => { ModSettings.TransitionVolume = v; ModSettings.Save(); },
                    0f, 1f, 0.05f,
                    "Volume for transition sounds"));
                items.Add(new IntSliderSetting("Transition Range",
                    () => (int)ModSettings.TransitionRange,
                    v => { ModSettings.TransitionRange = v; ModSettings.Save(); },
                    10, 200, 10,
                    "How far away transitions can be detected, in game units"));
                items.Add(new IntSliderSetting("Max Transition Sounds",
                    () => ModSettings.MaxTransitionSounds,
                    v => { ModSettings.MaxTransitionSounds = v; ModSettings.Save(); },
                    1, 10, 1,
                    "Maximum number of transition sounds that can play at once"));
            }

            return items.ToArray();
        }

        private SettingItem[] BuildSpeechItems()
        {
            return new SettingItem[]
            {
                new ToggleSetting("Read Voiced Text",
                    () => ModSettings.ReadVoicedText,
                    v => { ModSettings.ReadVoicedText = v; ModSettings.Save(); },
                    description: "When on, the screen reader also reads dialog text that has voice acting"),
            };
        }

        private SettingItem[] BuildKeybindingsItems()
        {
            var items = new List<SettingItem>();

            items.Add(new ToggleSetting("Input Type",
                () => _showControllerBindings,
                v => { _showControllerBindings = v; },
                "Controller", "Keyboard",
                "Switch between viewing keyboard or controller bindings"));

            items.Add(new KeybindSetting("RepeatLast", _showControllerBindings,
                "Repeat the last screen reader announcement"));
            items.Add(new KeybindSetting("AnnounceStatus", _showControllerBindings,
                "Announce your current location and partner info"));
            items.Add(new KeybindSetting("Partner1Status", _showControllerBindings,
                "Announce detailed stats for partner 1 in field and battles"));
            items.Add(new KeybindSetting("Partner2Status", _showControllerBindings,
                "Announce detailed stats for partner 2 in field and battles"));
            items.Add(new KeybindSetting("CompassDirection", _showControllerBindings,
                "Announce the direction the camera is facing"));
            items.Add(new KeybindSetting("NavNextCategory", _showControllerBindings,
                "Switch to the next navigation category"));
            items.Add(new KeybindSetting("NavPrevCategory", _showControllerBindings,
                "Switch to the previous navigation category"));
            items.Add(new KeybindSetting("NavPrevEvent", _showControllerBindings,
                "Select the previous item in the navigation list"));
            items.Add(new KeybindSetting("NavCurrentEvent", _showControllerBindings,
                "Announce the currently selected navigation target"));
            items.Add(new KeybindSetting("NavNextEvent", _showControllerBindings,
                "Select the next item in the navigation list"));
            items.Add(new KeybindSetting("NavToEvent", _showControllerBindings,
                "Start path finding to the selected navigation target, auto walks if enabled"));
            items.Add(new KeybindSetting("ToggleAutoWalk", _showControllerBindings,
                "Toggle auto walk on or off"));
            items.Add(new KeybindSetting("BattleEnemy1", _showControllerBindings,
                "Announce info about enemy 1 during battle"));
            items.Add(new KeybindSetting("BattleEnemy2", _showControllerBindings,
                "Announce info about enemy 2 during battle"));
            items.Add(new KeybindSetting("BattleEnemy3", _showControllerBindings,
                "Announce info about enemy 3 during battle"));
            items.Add(new KeybindSetting("BattleOrderPower", _showControllerBindings,
                "Announce the current order power gauge level during battle"));
            items.Add(new KeybindSetting("TrainingP1Info", _showControllerBindings,
                "Announce partner 1 training stats during training"));
            items.Add(new KeybindSetting("TrainingP2Info", _showControllerBindings,
                "Announce partner 2 training stats during training"));
            items.Add(new KeybindSetting("ShopCheckBits", _showControllerBindings,
                "Announce your current bit balance while shopping. It also speaks amount of bits you have in other relevant menus"));

            return items.ToArray();
        }

        // ========== Data Classes ==========

        private class SettingsCategory
        {
            public string Name { get; }
            public string Description { get; }
            private readonly Func<SettingItem[]> _itemsBuilder;

            public SettingItem[] Items => _itemsBuilder();

            public SettingsCategory(string name, string description, Func<SettingItem[]> itemsBuilder)
            {
                Name = name;
                Description = description;
                _itemsBuilder = itemsBuilder;
            }
        }

        private abstract class SettingItem
        {
            public string Name { get; }
            public string Description { get; }

            protected SettingItem(string name, string description = null)
            {
                Name = name;
                Description = description;
            }

            public abstract string GetValueText();
            public virtual void OnConfirm() { }
            public virtual void OnLeft() { }
            public virtual void OnRight() { }
        }

        private class ToggleSetting : SettingItem
        {
            private readonly Func<bool> _getter;
            private readonly Action<bool> _setter;
            private readonly string _trueLabel;
            private readonly string _falseLabel;

            public ToggleSetting(string name, Func<bool> getter, Action<bool> setter,
                string trueLabel = "On", string falseLabel = "Off", string description = null)
                : base(name, description)
            {
                _getter = getter;
                _setter = setter;
                _trueLabel = trueLabel;
                _falseLabel = falseLabel;
            }

            public override string GetValueText() => _getter() ? _trueLabel : _falseLabel;

            public override void OnConfirm()
            {
                _setter(!_getter());
            }
        }

        private class IntSliderSetting : SettingItem
        {
            private readonly Func<int> _getter;
            private readonly Action<int> _setter;
            private readonly int _min;
            private readonly int _max;
            private readonly int _step;

            public IntSliderSetting(string name, Func<int> getter, Action<int> setter, int min, int max, int step, string description = null)
                : base(name, description)
            {
                _getter = getter;
                _setter = setter;
                _min = min;
                _max = max;
                _step = step;
            }

            public override string GetValueText() => _getter().ToString();

            public override void OnLeft()
            {
                int val = Math.Max(_min, _getter() - _step);
                _setter(val);
            }

            public override void OnRight()
            {
                int val = Math.Min(_max, _getter() + _step);
                _setter(val);
            }

            public override void OnConfirm() { }
        }

        private class PercentSliderSetting : SettingItem
        {
            private readonly Func<float> _getter;
            private readonly Action<float> _setter;
            private readonly float _min;
            private readonly float _max;
            private readonly float _step;

            public PercentSliderSetting(string name, Func<float> getter, Action<float> setter, float min, float max, float step, string description = null)
                : base(name, description)
            {
                _getter = getter;
                _setter = setter;
                _min = min;
                _max = max;
                _step = step;
            }

            public override string GetValueText()
            {
                return $"{(int)Math.Round(_getter() * 100)}%";
            }

            public override void OnLeft()
            {
                float val = Math.Max(_min, _getter() - _step);
                _setter((float)Math.Round(val, 2));
            }

            public override void OnRight()
            {
                float val = Math.Min(_max, _getter() + _step);
                _setter((float)Math.Round(val, 2));
            }

            public override void OnConfirm() { }
        }

        private class ReadOnlySetting : SettingItem
        {
            private readonly string _value;

            public ReadOnlySetting(string name, string value, string description = null)
                : base(name, description)
            {
                _value = value;
            }

            public override string GetValueText() => _value;
        }

        private class KeybindSetting : SettingItem
        {
            public string ActionName { get; }
            public bool IsController { get; }

            public KeybindSetting(string actionName, bool isController, string description = null)
                : base(actionName, description)
            {
                ActionName = actionName;
                IsController = isController;
            }

            public override string GetValueText()
            {
                var current = ModInputManager.GetBinding(ActionName, IsController);
                return current != null ? current.DisplayName : "None";
            }
        }
    }
}
