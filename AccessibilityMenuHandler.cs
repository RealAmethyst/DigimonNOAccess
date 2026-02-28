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
            ScreenReader.Say($"Accessibility Settings. {firstCat.Name}, 1 of {_categories.Length}");

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
                var item = _categories[_categoryIndex].Items[_settingIndex];
                item.OnLeft();
                string val = item.GetValueText();
                if (val != null)
                    ScreenReader.Say(val);
            }
        }

        private void HandleRight()
        {
            if (_level == MenuLevel.Settings)
            {
                var item = _categories[_categoryIndex].Items[_settingIndex];
                item.OnRight();
                string val = item.GetValueText();
                if (val != null)
                    ScreenReader.Say(val);
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
                ScreenReader.Say($"{cat.Name}. {cat.Items.Length} items.");
                AnnounceCurrentItem();
            }
            else
            {
                var item = _categories[_categoryIndex].Items[_settingIndex];
                item.OnConfirm();
                PlaySe(CriSoundManager.SE_OK);
                string val = item.GetValueText();
                if (val != null)
                    ScreenReader.Say(val);
            }
        }

        private void HandleCancel()
        {
            if (_level == MenuLevel.Settings)
            {
                // Back to categories
                _level = MenuLevel.Categories;
                PlaySe(CriSoundManager.SE_Cancel);
                AnnounceCurrentItem();
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

        private void AnnounceCurrentItem()
        {
            if (_level == MenuLevel.Categories)
            {
                var cat = _categories[_categoryIndex];
                ScreenReader.Say($"{cat.Name}, {_categoryIndex + 1} of {_categories.Length}");
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
                ScreenReader.Say(announcement);
            }
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
                BuildGeneralCategory(),
                BuildAudioRangesCategory(),
                BuildAudioVolumesCategory(),
                BuildAudioTogglesCategory(),
                BuildAudioLimitsCategory(),
                BuildKeybindingsCategory(),
                BuildAboutCategory(),
            };
        }

        private SettingsCategory BuildGeneralCategory()
        {
            return new SettingsCategory("General", new SettingItem[]
            {
                new ToggleSetting("Read Voiced Text",
                    () => ModSettings.ReadVoicedText,
                    v => { ModSettings.ReadVoicedText = v; ModSettings.Save(); }),
            });
        }

        private SettingsCategory BuildAudioRangesCategory()
        {
            return new SettingsCategory("Audio Ranges", new SettingItem[]
            {
                new IntSliderSetting("Item Range",
                    () => (int)ModSettings.ItemRange,
                    v => { ModSettings.ItemRange = v; ModSettings.Save(); },
                    10, 200, 10),
                new IntSliderSetting("NPC Range",
                    () => (int)ModSettings.NpcRange,
                    v => { ModSettings.NpcRange = v; ModSettings.Save(); },
                    10, 200, 10),
                new IntSliderSetting("Enemy Range",
                    () => (int)ModSettings.EnemyRange,
                    v => { ModSettings.EnemyRange = v; ModSettings.Save(); },
                    10, 200, 10),
                new IntSliderSetting("Transition Range",
                    () => (int)ModSettings.TransitionRange,
                    v => { ModSettings.TransitionRange = v; ModSettings.Save(); },
                    10, 200, 10),
                new IntSliderSetting("Facility Range",
                    () => (int)ModSettings.FacilityRange,
                    v => { ModSettings.FacilityRange = v; ModSettings.Save(); },
                    10, 200, 10),
            });
        }

        private SettingsCategory BuildAudioVolumesCategory()
        {
            return new SettingsCategory("Audio Volumes", new SettingItem[]
            {
                new PercentSliderSetting("Nearest Volume",
                    () => ModSettings.NearestVolume,
                    v => { ModSettings.NearestVolume = v; ModSettings.Save(); },
                    0f, 1f, 0.05f),
                new PercentSliderSetting("Background Volume",
                    () => ModSettings.BackgroundVolume,
                    v => { ModSettings.BackgroundVolume = v; ModSettings.Save(); },
                    0f, 1f, 0.05f),
                new PercentSliderSetting("Enemy Volume Multiplier",
                    () => ModSettings.EnemyVolumeMultiplier,
                    v => { ModSettings.EnemyVolumeMultiplier = v; ModSettings.Save(); },
                    0f, 2f, 0.05f),
                new PercentSliderSetting("NPC Volume Multiplier",
                    () => ModSettings.NpcVolumeMultiplier,
                    v => { ModSettings.NpcVolumeMultiplier = v; ModSettings.Save(); },
                    0f, 2f, 0.05f),
                new PercentSliderSetting("Transition Volume Multiplier",
                    () => ModSettings.TransitionVolumeMultiplier,
                    v => { ModSettings.TransitionVolumeMultiplier = v; ModSettings.Save(); },
                    0f, 2f, 0.05f),
            });
        }

        private SettingsCategory BuildAudioTogglesCategory()
        {
            return new SettingsCategory("Audio Toggles", new SettingItem[]
            {
                new ToggleSetting("Items",
                    () => ModSettings.ItemsEnabled,
                    v => { ModSettings.ItemsEnabled = v; ModSettings.Save(); }),
                new ToggleSetting("NPCs",
                    () => ModSettings.NpcsEnabled,
                    v => { ModSettings.NpcsEnabled = v; ModSettings.Save(); }),
                new ToggleSetting("Enemies",
                    () => ModSettings.EnemiesEnabled,
                    v => { ModSettings.EnemiesEnabled = v; ModSettings.Save(); }),
                new ToggleSetting("Transitions",
                    () => ModSettings.TransitionsEnabled,
                    v => { ModSettings.TransitionsEnabled = v; ModSettings.Save(); }),
                new ToggleSetting("Facilities",
                    () => ModSettings.FacilitiesEnabled,
                    v => { ModSettings.FacilitiesEnabled = v; ModSettings.Save(); }),
            });
        }

        private SettingsCategory BuildAudioLimitsCategory()
        {
            return new SettingsCategory("Audio Limits", new SettingItem[]
            {
                new IntSliderSetting("Max Item Sounds",
                    () => ModSettings.MaxItemSounds,
                    v => { ModSettings.MaxItemSounds = v; ModSettings.Save(); },
                    1, 10, 1),
                new IntSliderSetting("Max NPC Sounds",
                    () => ModSettings.MaxNpcSounds,
                    v => { ModSettings.MaxNpcSounds = v; ModSettings.Save(); },
                    1, 10, 1),
                new IntSliderSetting("Max Enemy Sounds",
                    () => ModSettings.MaxEnemySounds,
                    v => { ModSettings.MaxEnemySounds = v; ModSettings.Save(); },
                    1, 10, 1),
                new IntSliderSetting("Max Transition Sounds",
                    () => ModSettings.MaxTransitionSounds,
                    v => { ModSettings.MaxTransitionSounds = v; ModSettings.Save(); },
                    1, 10, 1),
                new IntSliderSetting("Max Facility Sounds",
                    () => ModSettings.MaxFacilitySounds,
                    v => { ModSettings.MaxFacilitySounds = v; ModSettings.Save(); },
                    1, 10, 1),
            });
        }

        private SettingsCategory BuildKeybindingsCategory()
        {
            // Read-only list of all registered keybindings
            string[] actions = new string[]
            {
                "RepeatLast",
                "AnnounceStatus",
                "Partner1Status",
                "Partner2Status",
                "CompassDirection",
                "NavNextCategory",
                "NavPrevCategory",
                "NavPrevEvent",
                "NavCurrentEvent",
                "NavNextEvent",
                "NavToEvent",
                "ToggleAutoWalk",
                "BattleEnemy1",
                "BattleEnemy2",
                "BattleEnemy3",
                "BattleOrderPower",
                "TrainingP1Info",
                "TrainingP2Info",
                "ShopCheckBits",
            };

            var items = new List<SettingItem>();
            foreach (var action in actions)
            {
                string binding = ModInputManager.GetBindingDisplayName(action);
                items.Add(new ReadOnlySetting(action, binding));
            }

            return new SettingsCategory("Keybindings", items.ToArray());
        }

        private SettingsCategory BuildAboutCategory()
        {
            return new SettingsCategory("About", new SettingItem[]
            {
                new ReadOnlySetting("Mod", "DigimonNOAccess"),
                new ReadOnlySetting("Version", "1.0.0"),
                new ReadOnlySetting("Purpose", "Accessibility mod for blind players"),
            });
        }

        // ========== Data Classes ==========

        private class SettingsCategory
        {
            public string Name { get; }
            public SettingItem[] Items { get; }

            public SettingsCategory(string name, SettingItem[] items)
            {
                Name = name;
                Items = items;
            }
        }

        private abstract class SettingItem
        {
            public string Name { get; }

            protected SettingItem(string name)
            {
                Name = name;
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

            public ToggleSetting(string name, Func<bool> getter, Action<bool> setter)
                : base(name)
            {
                _getter = getter;
                _setter = setter;
            }

            public override string GetValueText() => _getter() ? "On" : "Off";

            public override void OnConfirm()
            {
                _setter(!_getter());
            }

            public override void OnLeft() => OnConfirm();
            public override void OnRight() => OnConfirm();
        }

        private class IntSliderSetting : SettingItem
        {
            private readonly Func<int> _getter;
            private readonly Action<int> _setter;
            private readonly int _min;
            private readonly int _max;
            private readonly int _step;

            public IntSliderSetting(string name, Func<int> getter, Action<int> setter, int min, int max, int step)
                : base(name)
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

            public PercentSliderSetting(string name, Func<float> getter, Action<float> setter, float min, float max, float step)
                : base(name)
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

            public ReadOnlySetting(string name, string value)
                : base(name)
            {
                _value = value;
            }

            public override string GetValueText() => _value;
        }
    }
}
