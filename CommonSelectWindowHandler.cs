using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the common selection window (generic NPC info menus).
    /// This is used by various NPCs to display selectable options.
    /// </summary>
    public class CommonSelectWindowHandler
    {
        private uCommonSelectWindowPanel _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uCommonSelectWindowPanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                return _panel.isEnabelPanel();
            }
            catch
            {
                return _panel.gameObject != null && _panel.gameObject.activeInHierarchy;
            }
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

            string announcement = $"Selection Menu. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[CommonSelectWindow] Menu opened, cursor={cursor}, total={total}");
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            DebugLogger.Log("[CommonSelectWindow] Menu closed");
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = $"{itemText}, {cursor + 1} of {total}";
                ScreenReader.Say(announcement);

                DebugLogger.Log($"[CommonSelectWindow] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                var itemPanel = _panel?.m_itemPanel;
                if (itemPanel != null)
                {
                    return itemPanel.m_selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CommonSelectWindow] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                var paramList = _panel?.m_paramCommonSelectWindowList;
                if (paramList != null && index >= 0 && index < paramList.Count)
                {
                    var param = paramList[index];
                    if (param != null)
                    {
                        string text = param.GetLanguageString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CommonSelectWindow] Error reading text: {ex.Message}");
            }

            return $"Option {index + 1}";
        }

        private int GetMenuItemCount()
        {
            try
            {
                var paramList = _panel?.m_paramCommonSelectWindowList;
                if (paramList != null)
                {
                    return paramList.Count;
                }
            }
            catch { }
            return 1;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Selection Menu. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
