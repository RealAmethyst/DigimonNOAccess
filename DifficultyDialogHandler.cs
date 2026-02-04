using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the difficulty selection dialog in New Game.
    /// </summary>
    public class DifficultyDialogHandler
    {
        private uDifficultyDialog _dialog;
        private bool _wasActive = false;
        private int _lastCursor = -1;

        /// <summary>
        /// Check if the difficulty dialog is currently open.
        /// </summary>
        public bool IsOpen()
        {
            _dialog = Object.FindObjectOfType<uDifficultyDialog>();

            return _dialog != null &&
                   _dialog.gameObject != null &&
                   _dialog.gameObject.activeInHierarchy &&
                   _dialog.m_State == uDifficultyDialog.State.Main;
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            bool isActive = IsOpen();

            // Dialog just opened
            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            // Dialog just closed
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            // Dialog is active, check for cursor changes
            else if (isActive)
            {
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;

            if (_dialog == null)
                return;

            int cursor = _dialog.CursorPosition;
            string difficultyName = GetDifficultyName(cursor);
            int total = GetTotalOptions();

            string announcement = $"Select Difficulty. {difficultyName}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
            DebugLogger.Log($"[DifficultyDialog] Opened: cursor={cursor}, total={total}");

            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _dialog = null;
            _lastCursor = -1;
            DebugLogger.Log("[DifficultyDialog] Closed");
        }

        private void CheckCursorChange()
        {
            if (_dialog == null)
                return;

            int cursor = _dialog.CursorPosition;
            if (cursor != _lastCursor)
            {
                string difficultyName = GetDifficultyName(cursor);
                int total = GetTotalOptions();
                string announcement = $"{difficultyName}, {cursor + 1} of {total}";
                ScreenReader.Say(announcement);
                DebugLogger.Log($"[DifficultyDialog] Cursor changed: {cursor} = {difficultyName}");
                _lastCursor = cursor;
            }
        }

        private string GetDifficultyName(int cursor)
        {
            if (_dialog == null)
                return $"Option {cursor + 1}";

            try
            {
                // Try to read from m_difficlutItems.m_difficultText array
                var difficultItems = _dialog.m_difficlutItems;
                if (difficultItems != null)
                {
                    var textArray = difficultItems.m_difficultText;
                    if (textArray != null && cursor < textArray.Length)
                    {
                        var text = textArray[cursor];
                        if (text != null && !string.IsNullOrEmpty(text.text))
                        {
                            return text.text.Trim();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DifficultyDialog] Error getting difficulty name: {ex.Message}");
            }

            // Fallback: common difficulty names
            switch (cursor)
            {
                case 0: return "Easy";
                case 1: return "Normal";
                case 2: return "Hard";
                case 3: return "Very Hard";
                default: return $"Option {cursor + 1}";
            }
        }

        private int GetTotalOptions()
        {
            if (_dialog == null)
                return 4;

            try
            {
                var difficultItems = _dialog.m_difficlutItems;
                if (difficultItems != null)
                {
                    var textArray = difficultItems.m_difficultText;
                    if (textArray != null)
                    {
                        // Count non-null active options
                        int count = 0;
                        for (int i = 0; i < textArray.Length; i++)
                        {
                            if (textArray[i] != null && textArray[i].gameObject.activeInHierarchy)
                                count++;
                        }
                        if (count > 0)
                            return count;
                        return textArray.Length;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DifficultyDialog] Error getting total options: {ex.Message}");
            }

            // Fallback based on DifficultyType
            return (int)_dialog.m_type;
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = _dialog.CursorPosition;
            string difficultyName = GetDifficultyName(cursor);
            int total = GetTotalOptions();

            string announcement = $"Difficulty selection. {difficultyName}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
