using Il2Cpp;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for dialog choices shown via EventWindowPanel.
    /// When a conversation presents multiple choice options, this handler
    /// detects them and announces the current selection.
    /// Uses TalkMain.m_cursor for accurate cursor tracking.
    /// </summary>
    public class DialogChoiceHandler
    {
        private EventWindowPanel _panel;
        private TalkMain _talkMain;
        private bool _wasChoicesActive = false;
        private int _lastChoiceCount = 0;
        private string _lastChoice0 = "";
        private string _lastChoice1 = "";
        private string _lastChoice2 = "";
        private int _lastCursorPosition = -1;
        private int _pollCounter = 0;
        private const int POLL_INTERVAL = 3; // Check every N frames when choices active

        /// <summary>
        /// Check if dialog choices are currently displayed.
        /// Uses TalkMain.m_maxChoiceNum to verify choices are active.
        /// </summary>
        public bool IsChoicesActive()
        {
            // Find TalkMain
            _talkMain = Object.FindObjectOfType<TalkMain>();
            if (_talkMain == null)
            {
                _panel = null;
                return false;
            }

            try
            {
                // Check if TalkMain has active choices
                int maxChoices = _talkMain.m_maxChoiceNum;
                if (maxChoices <= 0)
                {
                    _panel = null;
                    return false;
                }

                // Find an active EventWindowPanel with visible choices
                var panels = Object.FindObjectsOfType<EventWindowPanel>();
                _panel = null;

                foreach (var panel in panels)
                {
                    if (panel == null || !panel.gameObject.activeInHierarchy)
                        continue;

                    // Check if this panel has active choices
                    if (HasActiveChoices(panel))
                    {
                        _panel = panel;
                        return true;
                    }
                }
            }
            catch
            {
                _panel = null;
                return false;
            }

            return false;
        }

        private bool HasActiveChoices(EventWindowPanel panel)
        {
            if (panel == null)
                return false;

            try
            {
                var choicesText = panel.m_choicesText;
                if (choicesText == null || choicesText.Length == 0)
                    return false;

                // Check if at least one choice text is active and has content
                for (int i = 0; i < choicesText.Length; i++)
                {
                    var choiceText = choicesText[i];
                    if (choiceText != null &&
                        choiceText.gameObject.activeInHierarchy &&
                        !string.IsNullOrEmpty(choiceText.text))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            bool isActive = IsChoicesActive();

            // Choices just appeared
            if (isActive && !_wasChoicesActive)
            {
                OnChoicesOpened();
            }
            // Choices just closed
            else if (!isActive && _wasChoicesActive)
            {
                OnChoicesClosed();
            }
            // Choices are active, check for cursor changes
            else if (isActive)
            {
                // Poll periodically to check for cursor position changes
                _pollCounter++;
                if (_pollCounter >= POLL_INTERVAL)
                {
                    _pollCounter = 0;
                    CheckCursorChange();
                }
            }

            _wasChoicesActive = isActive;
        }

        private void OnChoicesOpened()
        {
            _lastCursorPosition = -1;
            _lastChoice0 = "";
            _lastChoice1 = "";
            _lastChoice2 = "";
            _lastChoiceCount = 0;
            _pollCounter = 0;

            if (_panel == null || _talkMain == null)
                return;

            DebugLogger.Log("[DialogChoice] === Choices Opened ===");
            DebugLogger.Log($"[DialogChoice] TalkMain.m_maxChoiceNum: {_talkMain.m_maxChoiceNum}");
            DebugLogger.Log($"[DialogChoice] TalkMain.m_cursor: {_talkMain.m_cursor}");

            // Get all choice texts
            var choices = GetChoiceTexts();
            _lastChoiceCount = choices.Length;

            DebugLogger.Log($"[DialogChoice] Found {choices.Length} visible choices");
            for (int i = 0; i < choices.Length; i++)
            {
                DebugLogger.Log($"[DialogChoice] Choice {i + 1}: '{choices[i]}'");
            }

            // Cache choice texts
            if (choices.Length > 0) _lastChoice0 = choices[0];
            if (choices.Length > 1) _lastChoice1 = choices[1];
            if (choices.Length > 2) _lastChoice2 = choices[2];

            // Get cursor position from TalkMain
            int cursorPos = GetCursorPosition();
            _lastCursorPosition = cursorPos;

            // Build announcement
            string announcement = "Dialog choices. ";
            for (int i = 0; i < choices.Length; i++)
            {
                announcement += $"{i + 1}: {choices[i]}. ";
            }

            if (cursorPos >= 0 && cursorPos < choices.Length)
            {
                announcement += $"Currently on: {choices[cursorPos]}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[DialogChoice] Announced: {announcement}");
        }

        private void OnChoicesClosed()
        {
            _panel = null;
            _talkMain = null;
            _lastCursorPosition = -1;
            _lastChoiceCount = 0;
            DebugLogger.Log("[DialogChoice] Choices closed");
        }

        private void CheckCursorChange()
        {
            if (_talkMain == null)
                return;

            try
            {
                int cursorPos = GetCursorPosition();

                if (cursorPos != _lastCursorPosition && cursorPos >= 0)
                {
                    DebugLogger.Log($"[DialogChoice] Cursor changed: {_lastCursorPosition} -> {cursorPos} (TalkMain.m_cursor)");

                    var choices = GetChoiceTexts();
                    if (cursorPos < choices.Length)
                    {
                        string announcement = $"{cursorPos + 1}: {choices[cursorPos]}";
                        ScreenReader.Say(announcement);
                        DebugLogger.Log($"[DialogChoice] Announced: {announcement}");
                    }
                    else
                    {
                        DebugLogger.Log($"[DialogChoice] Cursor {cursorPos} out of range for {choices.Length} choices");
                    }

                    _lastCursorPosition = cursorPos;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogChoice] Error checking cursor: {ex.Message}");
            }
        }

        private int GetCursorPosition()
        {
            if (_talkMain == null)
                return -1;

            try
            {
                // Use TalkMain.m_cursor directly - this is the definitive cursor position (0, 1, or 2)
                return _talkMain.m_cursor;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogChoice] Error getting cursor position: {ex.Message}");
                return -1;
            }
        }

        private string[] GetChoiceTexts()
        {
            if (_panel == null)
                return new string[0];

            try
            {
                var choicesText = _panel.m_choicesText;
                if (choicesText == null)
                    return new string[0];

                // Count active choices
                var activeChoices = new System.Collections.Generic.List<string>();
                for (int i = 0; i < choicesText.Length; i++)
                {
                    var choiceText = choicesText[i];
                    if (choiceText != null &&
                        choiceText.gameObject.activeInHierarchy &&
                        !string.IsNullOrEmpty(choiceText.text))
                    {
                        activeChoices.Add(choiceText.text);
                    }
                }

                return activeChoices.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsChoicesActive())
                return;

            DebugLogger.Log("[DialogChoice] === Status Request ===");

            var choices = GetChoiceTexts();
            int cursorPos = GetCursorPosition();

            string announcement = $"Dialog choices. {choices.Length} options. ";
            for (int i = 0; i < choices.Length; i++)
            {
                announcement += $"{i + 1}: {choices[i]}. ";
            }

            if (cursorPos >= 0 && cursorPos < choices.Length)
            {
                announcement += $"Currently on: {choices[cursorPos]}";
            }

            ScreenReader.Say(announcement);
        }
    }
}
