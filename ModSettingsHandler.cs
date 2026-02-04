using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles the mod settings menu.
    /// Opens with F10, provides accessible settings configuration.
    /// </summary>
    public class ModSettingsHandler : IAccessibilityHandler
    {
        private bool _isOpen = false;
        private int _currentIndex = 0;
        private bool _wasUpPressed = false;
        private bool _wasDownPressed = false;
        private bool _wasConfirmPressed = false;
        private bool _wasCancelPressed = false;

        public int Priority => 5;

        // Settings items
        private readonly SettingItem[] _settings;

        public ModSettingsHandler()
        {
            // Define our settings
            _settings = new SettingItem[]
            {
                new ToggleSetting(
                    "Read Voiced Text",
                    "When enabled, text-to-speech will read dialog even when the game is playing voice audio. Useful for non-English voice users.",
                    () => DialogTextPatch.AlwaysReadText,
                    (value) => DialogTextPatch.AlwaysReadText = value
                ),
                // Add more settings here as needed
            };
        }

        public bool IsOpen() => _isOpen;

        public void Update()
        {
            // F10 to toggle menu
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (_isOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
                return;
            }

            if (!_isOpen)
                return;

            // Handle navigation
            HandleInput();
        }

        private void Open()
        {
            _isOpen = true;
            _currentIndex = 0;

            ScreenReader.Say($"Mod Settings. {_settings.Length} items. Use up and down to navigate, confirm to change, cancel to close.");

            // Announce first item
            AnnounceCurrentItem();

            DebugLogger.Log("[ModSettings] Menu opened");
        }

        private void Close()
        {
            _isOpen = false;
            ScreenReader.Say("Mod Settings closed");
            DebugLogger.Log("[ModSettings] Menu closed");
        }

        private void HandleInput()
        {
            // Get current input state
            bool upPressed = Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W);
            bool downPressed = Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
            bool confirmPressed = Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space);
            bool cancelPressed = Input.GetKey(KeyCode.Escape) || Input.GetKey(KeyCode.Backspace);

            // Also check controller input via SDL3 if available
            if (ModInputManager.IsUsingSDL2)
            {
                upPressed = upPressed || SDL2Controller.IsButtonHeld(SDL2Controller.SDL_GameControllerButton.DPadUp);
                downPressed = downPressed || SDL2Controller.IsButtonHeld(SDL2Controller.SDL_GameControllerButton.DPadDown);
                confirmPressed = confirmPressed || SDL2Controller.IsButtonHeld(SDL2Controller.SDL_GameControllerButton.A);
                cancelPressed = cancelPressed || SDL2Controller.IsButtonHeld(SDL2Controller.SDL_GameControllerButton.B);
            }

            // Up - previous item (on key down)
            if (upPressed && !_wasUpPressed)
            {
                if (_currentIndex > 0)
                {
                    _currentIndex--;
                    AnnounceCurrentItem();
                }
                else
                {
                    ScreenReader.Say("Top of list");
                }
            }

            // Down - next item (on key down)
            if (downPressed && !_wasDownPressed)
            {
                if (_currentIndex < _settings.Length - 1)
                {
                    _currentIndex++;
                    AnnounceCurrentItem();
                }
                else
                {
                    ScreenReader.Say("Bottom of list");
                }
            }

            // Confirm - activate/toggle current item (on key down)
            if (confirmPressed && !_wasConfirmPressed)
            {
                var setting = _settings[_currentIndex];
                setting.Activate();
                AnnounceCurrentItem();
            }

            // Cancel - close menu (on key down)
            if (cancelPressed && !_wasCancelPressed)
            {
                Close();
            }

            // Update previous state
            _wasUpPressed = upPressed;
            _wasDownPressed = downPressed;
            _wasConfirmPressed = confirmPressed;
            _wasCancelPressed = cancelPressed;
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _settings.Length)
                return;

            var setting = _settings[_currentIndex];
            string announcement = $"{setting.Name}: {setting.GetValueText()}. {_currentIndex + 1} of {_settings.Length}";
            ScreenReader.Say(announcement);
        }

        public void AnnounceStatus()
        {
            if (!_isOpen)
            {
                ScreenReader.Say("Press F10 to open Mod Settings");
                return;
            }

            AnnounceCurrentItem();
        }

        // Base class for settings
        private abstract class SettingItem
        {
            public string Name { get; }
            public string Description { get; }

            protected SettingItem(string name, string description)
            {
                Name = name;
                Description = description;
            }

            public abstract string GetValueText();
            public abstract void Activate();
        }

        // Toggle setting (on/off)
        private class ToggleSetting : SettingItem
        {
            private readonly System.Func<bool> _getter;
            private readonly System.Action<bool> _setter;

            public ToggleSetting(string name, string description, System.Func<bool> getter, System.Action<bool> setter)
                : base(name, description)
            {
                _getter = getter;
                _setter = setter;
            }

            public override string GetValueText()
            {
                return _getter() ? "On" : "Off";
            }

            public override void Activate()
            {
                bool newValue = !_getter();
                _setter(newValue);
                DebugLogger.Log($"[ModSettings] {Name} set to {(newValue ? "On" : "Off")}");
            }
        }
    }
}
