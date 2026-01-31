using Il2Cpp;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for yes/no dialogs.
    /// </summary>
    public class DialogHandler
    {
        private uDialogBase _dialog;
        private bool _wasActive = false;
        private uDialogBase.CursorIndex _lastCursor = uDialogBase.CursorIndex.Yes;

        /// <summary>
        /// Check if a dialog is currently open.
        /// </summary>
        public bool IsOpen()
        {
            _dialog = Object.FindObjectOfType<uDialogBase>();

            return _dialog != null &&
                   _dialog.gameObject != null &&
                   _dialog.gameObject.activeInHierarchy;
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
            _lastCursor = uDialogBase.CursorIndex.Yes;

            if (_dialog == null)
                return;

            // Try to get dialog text from child Text components
            string dialogText = GetDialogText();
            string currentChoice = GetCursorText(_dialog.m_cursorIndex);

            string announcement = "Dialog";
            if (!string.IsNullOrEmpty(dialogText))
                announcement += $". {dialogText}";
            announcement += $". {currentChoice} selected";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[Dialog] Opened: {dialogText}, cursor={_dialog.m_cursorIndex}");

            _lastCursor = _dialog.m_cursorIndex;
        }

        private void OnClose()
        {
            _dialog = null;
            _lastCursor = uDialogBase.CursorIndex.Yes;
            DebugLogger.Log("[Dialog] Closed");
        }

        private void CheckCursorChange()
        {
            if (_dialog == null)
                return;

            var cursor = _dialog.m_cursorIndex;
            if (cursor != _lastCursor)
            {
                string choice = GetCursorText(cursor);
                ScreenReader.Say(choice);
                DebugLogger.Log($"[Dialog] Cursor changed: {choice}");
                _lastCursor = cursor;
            }
        }

        private string GetDialogText()
        {
            if (_dialog == null)
                return "";

            try
            {
                // Try to find Text components in the dialog's children
                var textComponents = _dialog.GetComponentsInChildren<Text>();
                if (textComponents != null)
                {
                    foreach (var text in textComponents)
                    {
                        if (text != null && !string.IsNullOrEmpty(text.text))
                        {
                            string txt = text.text.Trim();
                            // Skip button labels like "Yes" "No"
                            if (txt.Length > 5 && txt != "Yes" && txt != "No")
                            {
                                return txt;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Dialog] Error getting text: {ex.Message}");
            }

            return "";
        }

        private string GetCursorText(uDialogBase.CursorIndex cursor)
        {
            switch (cursor)
            {
                case uDialogBase.CursorIndex.Yes:
                    return "Yes";
                case uDialogBase.CursorIndex.No:
                    return "No";
                default:
                    return $"Option {(int)cursor}";
            }
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            string dialogText = GetDialogText();
            string currentChoice = GetCursorText(_dialog.m_cursorIndex);

            string announcement = "Dialog";
            if (!string.IsNullOrEmpty(dialogText))
                announcement += $". {dialogText}";
            announcement += $". {currentChoice} selected";

            ScreenReader.Say(announcement);
        }
    }
}
