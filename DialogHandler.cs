using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for yes/no dialogs.
    /// </summary>
    public class DialogHandler : HandlerBase<uDialogBase>
    {
        protected override string LogTag => "[Dialog]";
        public override int Priority => 30;

        private uDialogBase.CursorIndex _lastDialogCursor = uDialogBase.CursorIndex.Yes;

        public override bool IsOpen()
        {
            _panel = Object.FindObjectOfType<uDialogBase>();

            return _panel != null && _panel.m_isOpend;
        }

        protected override void OnOpen()
        {
            _lastDialogCursor = uDialogBase.CursorIndex.Yes;

            if (_panel == null)
                return;

            // Try to get dialog text from child Text components
            string dialogText = GetDialogText();
            string currentChoice = GetCursorText(_panel.m_cursorIndex);

            string announcement = "Dialog";
            if (!string.IsNullOrEmpty(dialogText))
                announcement += $". {dialogText}";
            announcement += $". {currentChoice} selected";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened: {dialogText}, cursor={_panel.m_cursorIndex}");

            _lastDialogCursor = _panel.m_cursorIndex;
        }

        protected override void OnClose()
        {
            _lastDialogCursor = uDialogBase.CursorIndex.Yes;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckCursorChange();
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            var cursor = _panel.m_cursorIndex;
            if (cursor != _lastDialogCursor)
            {
                string choice = GetCursorText(cursor);
                ScreenReader.Say(choice);
                DebugLogger.Log($"{LogTag} Cursor changed: {choice}");
                _lastDialogCursor = cursor;
            }
        }

        private string GetDialogText()
        {
            if (_panel == null)
                return "";

            try
            {
                // Try to find Text components in the dialog's children
                var textComponents = _panel.GetComponentsInChildren<Text>();
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
                DebugLogger.Log($"{LogTag} Error getting text: {ex.Message}");
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
        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            string dialogText = GetDialogText();
            string currentChoice = GetCursorText(_panel.m_cursorIndex);

            string announcement = "Dialog";
            if (!string.IsNullOrEmpty(dialogText))
                announcement += $". {dialogText}";
            announcement += $". {currentChoice} selected";

            ScreenReader.Say(announcement);
        }
    }
}
