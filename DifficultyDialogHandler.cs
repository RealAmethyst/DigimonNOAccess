using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the difficulty selection dialog in New Game.
    /// </summary>
    public class DifficultyDialogHandler : IAccessibilityHandler
    {
        public int Priority => 20;

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

            string announcement = AnnouncementBuilder.MenuOpen("Select Difficulty", difficultyName, cursor, total);
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
                string announcement = AnnouncementBuilder.CursorPosition(difficultyName, cursor, total);
                ScreenReader.Say(announcement);
                DebugLogger.Log($"[DifficultyDialog] Cursor changed: {cursor} = {difficultyName}");
                _lastCursor = cursor;
            }
        }

        private string GetDifficultyName(int cursor)
        {
            string fallback = cursor switch
            {
                0 => "Easy",
                1 => "Normal",
                2 => "Hard",
                3 => "Very Hard",
                _ => AnnouncementBuilder.FallbackItem("Option", cursor)
            };

            if (_dialog == null)
            {
                DebugLogger.Log("[DifficultyDialog] Difficulty name: m_dialog was null");
                return fallback;
            }

            try
            {
                var difficultItems = _dialog.m_difficlutItems;
                if (difficultItems == null)
                {
                    DebugLogger.Log("[DifficultyDialog] Difficulty name: m_difficlutItems was null");
                    return fallback;
                }

                var textArray = difficultItems.m_difficultText;
                if (textArray == null)
                {
                    DebugLogger.Log("[DifficultyDialog] Difficulty name: m_difficultText was null");
                    return fallback;
                }

                if (cursor < 0 || cursor >= textArray.Length)
                {
                    DebugLogger.Log($"[DifficultyDialog] Difficulty name: cursor {cursor} was outside m_difficultText length {textArray.Length}");
                    return fallback;
                }

                var text = textArray[cursor];
                if (text == null)
                {
                    DebugLogger.Log($"[DifficultyDialog] Difficulty name: m_difficultText[{cursor}] was null");
                    return fallback;
                }

                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(text.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"[DifficultyDialog] Difficulty name: m_difficultText[{cursor}].text unusable: {TextUtilities.DescribeUnusable(cleaned)}");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DifficultyDialog] Error getting difficulty name: {ex.Message}");
            }

            return fallback;
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

            string announcement = AnnouncementBuilder.MenuOpen("Difficulty selection", difficultyName, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
