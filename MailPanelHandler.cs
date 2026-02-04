using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the mail/digital messenger panel
    /// </summary>
    public class MailPanelHandler
    {
        private uMailPanel _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private int _lastTab = -1;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uMailPanel>();
            }

            if (_panel == null)
                return false;

            return _panel.gameObject != null && _panel.gameObject.activeInHierarchy;
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
                CheckTabChange();
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastTab = -1;

            if (_panel == null)
                return;

            int tab = _panel.m_SelectTab;
            _lastTab = tab;
            int cursor = _panel.m_selectNo;
            _lastCursor = cursor;

            string tabName = GetTabName(tab);
            int total = GetMailCount();

            string announcement;
            if (total == 0)
            {
                announcement = $"Mail, {tabName}, empty";
            }
            else
            {
                announcement = $"Mail, {tabName}. {cursor + 1} of {total}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[MailPanel] Opened, tab={tab}, cursor={cursor}");
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastTab = -1;
            DebugLogger.Log("[MailPanel] Closed");
        }

        private void CheckTabChange()
        {
            if (_panel == null)
                return;

            int currentTab = _panel.m_SelectTab;

            if (currentTab != _lastTab && _lastTab >= 0)
            {
                _lastCursor = -1; // Reset cursor tracking for new tab
                string tabName = GetTabName(currentTab);
                int total = GetMailCount();

                string announcement;
                if (total == 0)
                {
                    announcement = $"{tabName}, empty";
                }
                else
                {
                    int cursor = _panel.m_selectNo;
                    _lastCursor = cursor;
                    announcement = $"{tabName}. {cursor + 1} of {total}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[MailPanel] Tab changed to {tabName}");
                _lastTab = currentTab;
            }
            else if (_lastTab < 0)
            {
                _lastTab = currentTab;
            }
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int currentCursor = _panel.m_selectNo;

            if (currentCursor != _lastCursor)
            {
                int total = GetMailCount();

                if (total == 0)
                {
                    ScreenReader.Say("Empty");
                }
                else
                {
                    ScreenReader.Say($"{currentCursor + 1} of {total}");
                }

                DebugLogger.Log($"[MailPanel] Cursor changed to {currentCursor}");
                _lastCursor = currentCursor;
            }
        }

        private string GetTabName(int tabIndex)
        {
            try
            {
                var tabInfoTbl = _panel?.m_SelectTabInfoTbl;
                if (tabInfoTbl != null && tabIndex >= 0 && tabIndex < tabInfoTbl.Length)
                {
                    var tabInfo = tabInfoTbl[tabIndex];
                    if (tabInfo != null)
                    {
                        var tabText = tabInfo.m_TabText;
                        if (tabText != null)
                        {
                            string text = tabText.text;
                            if (!string.IsNullOrEmpty(text))
                            {
                                return text;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MailPanel] Error getting tab name: {ex.Message}");
            }

            // Fallback tab names
            return tabIndex switch
            {
                0 => "Inbox",
                1 => "Main Quest",
                2 => "Sub Quest",
                3 => "Information",
                _ => AnnouncementBuilder.FallbackItem("Folder", tabIndex)
            };
        }

        private int GetMailCount()
        {
            try
            {
                var mailList = _panel?.m_ListMailData;
                if (mailList != null)
                {
                    return mailList.Count;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MailPanel] Error getting mail count: {ex.Message}");
            }
            return 0;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int tab = _panel.m_SelectTab;
            string tabName = GetTabName(tab);
            int cursor = _panel.m_selectNo;
            int total = GetMailCount();

            string announcement;
            if (total == 0)
            {
                announcement = $"Mail, {tabName}, empty";
            }
            else
            {
                announcement = $"Mail, {tabName}. {cursor + 1} of {total}";
            }

            ScreenReader.Say(announcement);
        }
    }
}
