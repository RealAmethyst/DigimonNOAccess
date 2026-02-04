using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the camp command menu (rest, feed, etc.)
    /// </summary>
    public class CampCommandHandler
    {
        private CampCommandPanel _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<CampCommandPanel>();
            }

            return _panel != null &&
                   _panel.gameObject != null &&
                   _panel.gameObject.activeInHierarchy;
        }

        public void Update()
        {
            bool isActive = IsOpen();

            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            else if (isActive)
            {
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;

            if (_panel == null)
                return;

            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpen("Camp Menu", itemText, cursor, total);
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[CampCommand] Menu opened, cursor={cursor}");
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            DebugLogger.Log("[CampCommand] Menu closed");
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = AnnouncementBuilder.CursorPosition(itemText, cursor, total);
                ScreenReader.Say(announcement);

                DebugLogger.Log($"[CampCommand] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                if (_panel?.m_cusror != null)
                {
                    var selectNo = _panel.m_cusror.m_selectNo;
                    if (selectNo != null && selectNo.Length > 0)
                    {
                        return selectNo[0];
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CampCommand] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                var textArray = _panel?.m_commandText;
                if (textArray != null && index >= 0 && index < textArray.Length)
                {
                    var textComponent = textArray[index];
                    if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
                    {
                        return textComponent.text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CampCommand] Error reading text: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Option", index);
        }

        private int GetMenuItemCount()
        {
            try
            {
                var textArray = _panel?.m_commandText;
                if (textArray != null)
                {
                    return textArray.Length;
                }
            }
            catch { }
            return 4;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpen("Camp Menu", itemText, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
