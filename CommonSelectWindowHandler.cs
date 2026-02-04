using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the common selection window (generic NPC info menus).
    /// This is used by various NPCs to display selectable options.
    /// </summary>
    public class CommonSelectWindowHandler : HandlerBase<uCommonSelectWindowPanel>
    {
        protected override string LogTag => "[CommonSelectWindow]";
        public override int Priority => 35;

        public override bool IsOpen()
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

        protected override void OnOpen()
        {
            _lastCursor = -1;

            if (_panel == null)
                return;

            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpen("Selection Menu", itemText, cursor, total);
            ScreenReader.Say(announcement);

            DebugLogger.Log($"{LogTag} Menu opened, cursor={cursor}, total={total}");
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
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

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = AnnouncementBuilder.CursorPosition(itemText, cursor, total);
                ScreenReader.Say(announcement);

                DebugLogger.Log($"{LogTag} Cursor changed: {itemText}");
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
                DebugLogger.Log($"{LogTag} Error getting cursor: {ex.Message}");
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
                DebugLogger.Log($"{LogTag} Error reading text: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Option", index);
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

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpen("Selection Menu", itemText, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
