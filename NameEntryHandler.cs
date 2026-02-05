using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the name entry screen.
    /// Used for entering player name (new game) and Digimon names (egg hatching).
    /// Note: Users should disable Steam Big Picture mode as its on-screen keyboard is inaccessible.
    /// </summary>
    public class NameEntryHandler : IAccessibilityHandler
    {
        public int Priority => 25;

        private NameEntry _nameEntry;
        private uNameInput _nameInput;
        private bool _wasActive;
        private NameEntry.eState _lastState = NameEntry.eState.NONE;
        private string _lastText = "";

        public bool IsOpen()
        {
            try
            {
                _nameEntry = Object.FindObjectOfType<NameEntry>();
                if (_nameEntry == null)
                    return false;

                if (_nameEntry.gameObject == null || !_nameEntry.gameObject.activeInHierarchy)
                    return false;

                var state = _nameEntry.m_state;
                if (state == NameEntry.eState.NONE)
                    return false;

                _nameInput = _nameEntry.m_uNameInput;
                return true;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"[NameEntry] IsOpen exception: {ex.Message}");
                return false;
            }
        }

        public void Update()
        {
            bool isActive = IsOpen();

            if (isActive && !_wasActive)
                OnOpen();
            else if (!isActive && _wasActive)
                OnClose();
            else if (isActive)
                OnUpdate();

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = NameEntry.eState.NONE;
            _lastText = "";

            if (_nameEntry == null)
                return;

            _lastState = _nameEntry.m_state;

            string entryType = GetEntryTypeName(_nameEntry.Type);
            string currentText = GetCurrentText();

            string announcement = $"Name Entry, {entryType}";
            if (!string.IsNullOrEmpty(currentText))
                announcement += $", current: {currentText}";
            announcement += ". Type to enter name.";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[NameEntry] Opened: type={_nameEntry.Type}");

            _lastText = currentText;
        }

        private void OnClose()
        {
            _nameEntry = null;
            _nameInput = null;
            _lastState = NameEntry.eState.NONE;
            _lastText = "";
            DebugLogger.Log("[NameEntry] Closed");
        }

        private void OnUpdate()
        {
            if (_nameEntry == null)
                return;

            CheckStateChange();
            CheckTextChange();
        }

        private void CheckStateChange()
        {
            var state = _nameEntry.m_state;
            if (state == _lastState)
                return;

            DebugLogger.Log($"[NameEntry] State: {_lastState} -> {state}");
            _lastState = state;

            if (state == NameEntry.eState.INPUT_END)
            {
                string name = GetCurrentText();
                string announcement = string.IsNullOrEmpty(name) ? "Name confirmed" : $"Name confirmed: {name}";
                ScreenReader.Say(announcement);
                DebugLogger.Log($"[NameEntry] Confirmed name: {name}");
            }
        }

        private void CheckTextChange()
        {
            string currentText = GetCurrentText();
            if (currentText == _lastText)
                return;

            // Determine what changed
            if (string.IsNullOrEmpty(currentText) && !string.IsNullOrEmpty(_lastText))
            {
                // Text was cleared or last character deleted
                if (_lastText.Length == 1)
                {
                    ScreenReader.Say($"{_lastText} deleted, no text");
                }
                else
                {
                    ScreenReader.Say("No text");
                }
                DebugLogger.Log("[NameEntry] Text cleared");
            }
            else if (currentText.Length < _lastText.Length)
            {
                // Character(s) deleted
                string deleted = GetDeletedCharacters(_lastText, currentText);
                if (deleted.Length == 1)
                {
                    ScreenReader.Say($"{deleted} deleted");
                }
                else
                {
                    ScreenReader.Say($"{deleted.Length} characters deleted");
                }
                DebugLogger.Log($"[NameEntry] Deleted: {deleted}");
            }
            else if (currentText.Length > _lastText.Length)
            {
                // Character(s) added
                string added = GetAddedCharacters(_lastText, currentText);
                if (added.Length == 1)
                {
                    ScreenReader.Say(added);
                }
                else
                {
                    ScreenReader.Say(added);
                }
                DebugLogger.Log($"[NameEntry] Added: {added}");
            }
            else
            {
                // Same length but different - replacement
                ScreenReader.Say(currentText);
                DebugLogger.Log($"[NameEntry] Text changed: {currentText}");
            }

            _lastText = currentText;
        }

        private string GetDeletedCharacters(string oldText, string newText)
        {
            // Simple case: deletion from end
            if (oldText.StartsWith(newText))
            {
                return oldText.Substring(newText.Length);
            }
            // Deletion from start
            if (oldText.EndsWith(newText))
            {
                return oldText.Substring(0, oldText.Length - newText.Length);
            }
            // Complex case: return what was removed
            return oldText.Substring(newText.Length);
        }

        private string GetAddedCharacters(string oldText, string newText)
        {
            // Simple case: addition at end
            if (newText.StartsWith(oldText))
            {
                return newText.Substring(oldText.Length);
            }
            // Addition at start
            if (newText.EndsWith(oldText))
            {
                return newText.Substring(0, newText.Length - oldText.Length);
            }
            // Complex case: return the new characters
            return newText.Substring(oldText.Length);
        }

        private string GetEntryTypeName(NameEntry.eType type)
        {
            return type == NameEntry.eType.Player ? "Player name" : "Digimon name";
        }

        private string GetCurrentText()
        {
            try
            {
                // Read from InputField first - this has live text during typing
                if (_nameInput?.m_InputField != null)
                {
                    string text = _nameInput.m_InputField.text;
                    if (text != null)
                        return text;
                }

                // Fallback to NameEntry.Name (only updates on confirm)
                if (_nameEntry != null)
                {
                    string name = _nameEntry.Name;
                    if (name != null)
                        return name;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"[NameEntry] Error getting text: {ex.Message}");
            }

            return "";
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            string entryType = GetEntryTypeName(_nameEntry.Type);
            string currentText = GetCurrentText();

            string announcement = $"Name Entry, {entryType}";
            if (!string.IsNullOrEmpty(currentText))
                announcement += $", current name: {currentText}";
            else
                announcement += ", no text";

            ScreenReader.Say(announcement);
        }
    }
}
