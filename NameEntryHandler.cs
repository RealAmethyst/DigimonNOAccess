using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the name entry screen (New Game player name input).
    /// </summary>
    public class NameEntryHandler
    {
        private NameEntry _nameEntry;
        private uNameInput _nameInput;
        private bool _wasActive = false;
        private NameEntry.eState _lastState = NameEntry.eState.NONE;
        private string _lastInputText = "";
        private bool _lastInputFieldSelect = false;
        private static bool _steamInputDisabledGlobally = false;

        /// <summary>
        /// Check if name entry screen is currently open.
        /// </summary>
        public bool IsOpen()
        {
            _nameEntry = Object.FindObjectOfType<NameEntry>();

            if (_nameEntry != null &&
                _nameEntry.gameObject != null &&
                _nameEntry.gameObject.activeInHierarchy &&
                _nameEntry.m_state != NameEntry.eState.NONE)
            {
                _nameInput = _nameEntry.m_uNameInput;
                return true;
            }

            _nameInput = null;
            return false;
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            // ALWAYS try to disable Steam text input on ALL uNameInput objects
            // This must happen BEFORE the game can trigger Steam overlay
            DisableSteamTextInputGlobally();

            bool isActive = IsOpen();

            // Screen just opened
            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            // Screen just closed
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            // Screen is active, check for changes
            else if (isActive)
            {
                CheckStateChange();
                CheckInputFieldFocusChange();
                CheckInputChange();
            }

            _wasActive = isActive;
        }

        /// <summary>
        /// Find ALL uNameInput objects in the scene and disable Steam text input on them.
        /// This prevents Steam's inaccessible overlay from ever appearing.
        /// </summary>
        private void DisableSteamTextInputGlobally()
        {
            try
            {
                var allNameInputs = Object.FindObjectsOfType<uNameInput>();
                if (allNameInputs != null && allNameInputs.Length > 0)
                {
                    foreach (var nameInput in allNameInputs)
                    {
                        if (nameInput != null && nameInput.m_canShowSteamTextInput)
                        {
                            nameInput.m_canShowSteamTextInput = false;
                            if (!_steamInputDisabledGlobally)
                            {
                                DebugLogger.Log("[NameEntry] Disabled Steam text input globally on uNameInput");
                                _steamInputDisabledGlobally = true;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[NameEntry] Error disabling Steam text input globally: {ex.Message}");
            }
        }

        private void OnOpen()
        {
            _lastState = NameEntry.eState.NONE;
            _lastInputText = "";
            _lastInputFieldSelect = false;

            if (_nameEntry == null)
                return;

            // Steam text input is already disabled globally in Update()

            string title = GetTitle();
            string currentText = GetCurrentInputText();
            string typeStr = _nameEntry.Type == NameEntry.eType.Player ? "Player Name" : "Digimon Name";

            string announcement = $"Name Entry. {typeStr}";
            if (!string.IsNullOrEmpty(title))
                announcement += $". {title}";
            if (!string.IsNullOrEmpty(currentText))
                announcement += $". Current name: {currentText}";

            // Add navigation hint
            announcement += ". Type to enter name, press Space to confirm or Backspace to cancel";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[NameEntry] Opened: {typeStr}, title={title}, text={currentText}");

            _lastState = _nameEntry.m_state;
            _lastInputText = currentText;

            // Track initial focus state
            if (_nameInput != null)
            {
                _lastInputFieldSelect = _nameInput.isInputFieldSelect;
            }
        }

        private void OnClose()
        {
            _nameEntry = null;
            _nameInput = null;
            _lastState = NameEntry.eState.NONE;
            _lastInputText = "";
            _lastInputFieldSelect = false;
            DebugLogger.Log("[NameEntry] Closed");
        }

        private void CheckStateChange()
        {
            if (_nameEntry == null)
                return;

            var state = _nameEntry.m_state;
            if (state != _lastState)
            {
                // State changed - announce new state
                string stateStr = GetStateName(state);
                DebugLogger.Log($"[NameEntry] State changed: {stateStr}");

                // When entering INPUT state, re-announce
                if (state == NameEntry.eState.INPUT)
                {
                    string currentText = GetCurrentInputText();
                    string announcement = "Input field active";
                    if (!string.IsNullOrEmpty(currentText))
                        announcement += $", current name: {currentText}";
                    ScreenReader.Say(announcement);
                }
                else if (state == NameEntry.eState.INPUT_END)
                {
                    string name = GetCurrentInputText();
                    ScreenReader.Say($"Name confirmed: {name}");
                }
                else if (state == NameEntry.eState.STEAM_TEXTINPUT)
                {
                    // Steam overlay was triggered - warn user
                    ScreenReader.Say("Steam text input opened. Press Escape to close it, then try again.");
                    DebugLogger.Log("[NameEntry] WARNING: Steam text input overlay was triggered!");
                }

                _lastState = state;
            }
        }

        private void CheckInputFieldFocusChange()
        {
            if (_nameInput == null)
                return;

            bool isInputFieldSelect = _nameInput.isInputFieldSelect;
            if (isInputFieldSelect != _lastInputFieldSelect)
            {
                if (isInputFieldSelect)
                {
                    // Focus moved to input field
                    string currentText = GetCurrentInputText();
                    string announcement = "Name input field";
                    if (!string.IsNullOrEmpty(currentText))
                        announcement += $": {currentText}";
                    ScreenReader.Say(announcement);
                    DebugLogger.Log("[NameEntry] Focus: Input field");
                }
                else
                {
                    // Focus moved away from input field (to buttons)
                    string buttonName = GetCurrentButtonName();
                    if (!string.IsNullOrEmpty(buttonName))
                    {
                        ScreenReader.Say(buttonName);
                        DebugLogger.Log($"[NameEntry] Focus: Button - {buttonName}");
                    }
                    else
                    {
                        ScreenReader.Say("Confirm button");
                        DebugLogger.Log("[NameEntry] Focus: Button area (unknown)");
                    }
                }
                _lastInputFieldSelect = isInputFieldSelect;
            }
        }

        private void CheckInputChange()
        {
            if (_nameEntry == null)
                return;

            string currentText = GetCurrentInputText();
            if (currentText != _lastInputText)
            {
                // Only announce if text actually changed and we're in input mode
                if (_nameEntry.m_state == NameEntry.eState.INPUT ||
                    _nameEntry.m_state == NameEntry.eState.STEAM_TEXTINPUT)
                {
                    // Don't flood announcements - just track the change
                    DebugLogger.Log($"[NameEntry] Text changed: {_lastInputText} -> {currentText}");
                }
                _lastInputText = currentText;
            }
        }

        private string GetTitle()
        {
            if (_nameEntry == null)
                return "";

            try
            {
                // Try to get title from NameEntry
                string title = _nameEntry.Title;
                if (!string.IsNullOrEmpty(title))
                    return title;

                // Try to get label from uNameInput
                if (_nameInput?.m_label != null)
                {
                    string labelText = _nameInput.m_label.text;
                    if (!string.IsNullOrEmpty(labelText))
                        return labelText;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[NameEntry] Error getting title: {ex.Message}");
            }

            return "";
        }

        private string GetCurrentInputText()
        {
            if (_nameEntry == null)
                return "";

            try
            {
                // Try to get from Name property
                string name = _nameEntry.Name;
                if (!string.IsNullOrEmpty(name))
                    return name;

                // Try to get from uNameInput's InputField
                if (_nameInput?.m_InputField != null)
                {
                    string inputText = _nameInput.m_InputField.text;
                    if (!string.IsNullOrEmpty(inputText))
                        return inputText;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[NameEntry] Error getting input text: {ex.Message}");
            }

            return "";
        }

        private string GetCurrentButtonName()
        {
            if (_nameInput == null)
                return "";

            try
            {
                // Try to find Button components and get their text labels
                var buttons = _nameInput.GetComponentsInChildren<Button>();
                if (buttons != null)
                {
                    foreach (var button in buttons)
                    {
                        if (button != null &&
                            button.gameObject.activeInHierarchy &&
                            button.interactable)
                        {
                            // Get the Text component from the button
                            var textComponent = button.GetComponentInChildren<Text>();
                            if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
                            {
                                return textComponent.text.Trim();
                            }
                        }
                    }
                }

                // Fallback: search for any active Text components that might be button labels
                var texts = _nameInput.GetComponentsInChildren<Text>();
                if (texts != null)
                {
                    foreach (var text in texts)
                    {
                        if (text != null &&
                            text.gameObject.activeInHierarchy &&
                            !string.IsNullOrEmpty(text.text))
                        {
                            string txt = text.text.Trim().ToLower();
                            // Look for common button labels
                            if (txt == "ok" || txt == "confirm" || txt == "decide" ||
                                txt == "cancel" || txt == "back" || txt == "yes" || txt == "no")
                            {
                                return text.text.Trim();
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[NameEntry] Error getting button name: {ex.Message}");
            }

            return "";
        }

        private string GetStateName(NameEntry.eState state)
        {
            switch (state)
            {
                case NameEntry.eState.NONE: return "None";
                case NameEntry.eState.INIT: return "Initializing";
                case NameEntry.eState.REQUEST: return "Requesting";
                case NameEntry.eState.INPUT: return "Input";
                case NameEntry.eState.INPUT_END: return "Input Complete";
                case NameEntry.eState.STEAM_TEXTINPUT: return "Steam Text Input";
                case NameEntry.eState.STEAM_TEXTINPUT_END: return "Steam Input Complete";
                default: return $"State {(int)state}";
            }
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            string typeStr = _nameEntry.Type == NameEntry.eType.Player ? "Player Name" : "Digimon Name";
            string title = GetTitle();
            string currentText = GetCurrentInputText();

            string announcement = $"Name Entry, {typeStr}";
            if (!string.IsNullOrEmpty(title))
                announcement += $". {title}";
            if (!string.IsNullOrEmpty(currentText))
                announcement += $". Current name: {currentText}";

            // Check focus
            if (_nameInput != null)
            {
                if (_nameInput.isInputFieldSelect)
                {
                    announcement += ". In input field";
                }
                else
                {
                    string buttonName = GetCurrentButtonName();
                    if (!string.IsNullOrEmpty(buttonName))
                    {
                        announcement += $". On {buttonName} button";
                    }
                    else
                    {
                        announcement += ". On button";
                    }
                }
            }

            announcement += ". Press Space to confirm, Backspace to cancel";

            ScreenReader.Say(announcement);
        }
    }
}
