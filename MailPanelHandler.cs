using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the mail/digital messenger panel.
    /// Tracks the uDigiviceDigimePanel state machine to handle folder selection,
    /// mail list browsing (with title/sender/status), and body reading.
    /// </summary>
    public class MailPanelHandler : IAccessibilityHandler
    {
        private const string LogTag = "[MailPanel]";
        public int Priority => 65;

        private uDigiviceDigimePanel _digimePanel;
        private uMailPanel _mailPanel;
        private bool _wasActive;
        private uDigiviceDigimePanel.State _lastState = uDigiviceDigimePanel.State.NONE;
        private int _lastCursor = -1;
        private int _lastTab = -1;
        private bool _bodyAnnounced;
        private bool _pendingFirstMail;
        private DigitalMessengerManager.SortType _lastSortType = DigitalMessengerManager.SortType.NEW;

        public bool IsOpen()
        {
            try
            {
                if (_digimePanel == null)
                    return false;
                var state = _digimePanel.m_CurrentState;
                return state != uDigiviceDigimePanel.State.NONE
                    && state != uDigiviceDigimePanel.State.CLOSE
                    && state != uDigiviceDigimePanel.State.MAX;
            }
            catch
            {
                _digimePanel = null;
                return false;
            }
        }

        public void Update()
        {
            if (_digimePanel == null)
            {
                try
                {
                    _digimePanel = Object.FindObjectOfType<uDigiviceDigimePanel>();
                    if (_digimePanel != null)
                        _mailPanel = _digimePanel.m_mailPanel;
                }
                catch { return; }
            }

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
            _lastState = uDigiviceDigimePanel.State.NONE;
            _lastCursor = -1;
            _lastTab = -1;
            _bodyAnnounced = false;
            _pendingFirstMail = false;
            _lastSortType = _mailPanel?.m_SortType ?? DigitalMessengerManager.SortType.NEW;

            if (_digimePanel == null)
                return;

            _mailPanel = _digimePanel.m_mailPanel;
            var state = _digimePanel.m_CurrentState;
            _lastState = state;

            string announcement = "Mail";

            if (state == uDigiviceDigimePanel.State.SELECT_FOLDER && _mailPanel != null)
            {
                int tab = _mailPanel.m_SelectTab;
                _lastTab = tab;
                var (pos, total) = GetTabPosition(tab);
                string tabName = GetTabName(tab);
                announcement = $"Mail, {tabName}, {pos} of {total}";
            }
            else if (state == uDigiviceDigimePanel.State.SELECT_MAIL && _mailPanel != null)
            {
                int tab = _mailPanel.m_SelectTab;
                _lastTab = tab;
                _pendingFirstMail = true;
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened, state={state}");
        }

        private void OnClose()
        {
            _mailPanel = null;
            _lastState = uDigiviceDigimePanel.State.NONE;
            _lastCursor = -1;
            _lastTab = -1;
            _bodyAnnounced = false;
            _pendingFirstMail = false;
            _lastSortType = DigitalMessengerManager.SortType.NEW;
            DebugLogger.Log($"{LogTag} Closed");
        }

        private void OnUpdate()
        {
            CheckStateChange();

            var state = _digimePanel?.m_CurrentState ?? uDigiviceDigimePanel.State.NONE;

            if (state == uDigiviceDigimePanel.State.SELECT_FOLDER)
            {
                CheckTabChange();
            }
            else if (state == uDigiviceDigimePanel.State.SELECT_MAIL)
            {
                CheckTabChange();
                CheckSortChange();
                if (_pendingFirstMail)
                    TryAnnounceFirstMail();
                else
                    CheckCursorChange();
            }
            else if (state == uDigiviceDigimePanel.State.LOOK_BODY && !_bodyAnnounced)
            {
                TryAnnounceBody();
            }
        }

        private void CheckStateChange()
        {
            if (_digimePanel == null)
                return;

            var currentState = _digimePanel.m_CurrentState;
            if (currentState == _lastState)
                return;

            var prevState = _lastState;
            _lastState = currentState;

            switch (currentState)
            {
                case uDigiviceDigimePanel.State.SELECT_FOLDER:
                    _pendingFirstMail = false;
                    if (_mailPanel != null)
                    {
                        int tab = _mailPanel.m_SelectTab;
                        _lastTab = tab;
                        var (pos, total) = GetTabPosition(tab);
                        ScreenReader.Say($"{GetTabName(tab)}, {pos} of {total}");
                    }
                    break;

                case uDigiviceDigimePanel.State.SELECT_MAIL:
                    _bodyAnnounced = false;
                    _pendingFirstMail = true;
                    _lastCursor = -1;
                    break;

                case uDigiviceDigimePanel.State.LOOK_BODY:
                    _bodyAnnounced = false;
                    _pendingFirstMail = false;
                    TryAnnounceBody();
                    break;

                case uDigiviceDigimePanel.State.GET_ITEM:
                    _pendingFirstMail = false;
                    ScreenReader.Say("Claiming attachment");
                    break;
            }

            DebugLogger.Log($"{LogTag} State: {prevState} -> {currentState}");
        }

        private void CheckSortChange()
        {
            if (_mailPanel == null)
                return;

            var currentSort = _mailPanel.m_SortType;
            if (currentSort == _lastSortType)
                return;

            _lastSortType = currentSort;
            string sortName = GetSortName();

            int total = GetMailCount();
            if (total > 0)
            {
                int cursor = _mailPanel.m_selectNo;
                _lastCursor = cursor;
                _pendingFirstMail = false;
                string mailInfo = GetCurrentMailInfo();
                if (!string.IsNullOrEmpty(mailInfo))
                    ScreenReader.Say($"Sorted by {sortName}. {mailInfo}, {cursor + 1} of {total}");
                else
                    ScreenReader.Say($"Sorted by {sortName}. {cursor + 1} of {total}");
            }
            else
            {
                ScreenReader.Say($"Sorted by {sortName}");
            }

            DebugLogger.Log($"{LogTag} Sort changed to {sortName}");
        }

        private string GetSortName()
        {
            try
            {
                var sortText = _mailPanel?.m_SortText;
                if (sortText != null)
                {
                    string text = sortText.text;
                    if (!string.IsNullOrEmpty(text))
                        return TextUtilities.StripRichTextTags(text);
                }
            }
            catch { }

            return _mailPanel?.m_SortType switch
            {
                DigitalMessengerManager.SortType.NEW => "Newest",
                DigitalMessengerManager.SortType.OLD => "Oldest",
                DigitalMessengerManager.SortType.CHARACTER => "Sender",
                _ => "Sort"
            };
        }

        private void TryAnnounceFirstMail()
        {
            if (_mailPanel == null)
                return;

            int total = GetMailCount();
            if (total == 0)
                return; // List not populated yet, retry next frame

            _pendingFirstMail = false;
            int cursor = _mailPanel.m_selectNo;
            _lastCursor = cursor;

            string mailInfo = GetCurrentMailInfo();
            if (!string.IsNullOrEmpty(mailInfo))
                ScreenReader.Say($"{mailInfo}, {cursor + 1} of {total}");
            else
                ScreenReader.Say($"{cursor + 1} of {total}");
        }

        private void TryAnnounceBody()
        {
            try
            {
                var msgWindow = _digimePanel?.m_messageWindow;
                if (msgWindow == null)
                    return;

                var texts = msgWindow.m_text;
                if (texts == null)
                    return;

                string sender = GetTextAt(texts, (int)uDigiviceDigimePanelMessage_Window.TextIndex.SENDER_NAME);
                string title = GetTextAt(texts, (int)uDigiviceDigimePanelMessage_Window.TextIndex.TITLE);
                string body = GetTextAt(texts, (int)uDigiviceDigimePanelMessage_Window.TextIndex.BODY);

                if (!string.IsNullOrEmpty(body))
                {
                    var parts = new System.Collections.Generic.List<string>();
                    if (!string.IsNullOrEmpty(sender))
                        parts.Add($"From {sender}");
                    if (!string.IsNullOrEmpty(title))
                        parts.Add(title);
                    parts.Add(body);

                    ScreenReader.Say(string.Join(". ", parts));
                    _bodyAnnounced = true;
                    DebugLogger.Log($"{LogTag} Body announced: {title}");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading body: {ex.Message}");
            }
        }

        private string GetTextAt(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityEngine.UI.Text> texts, int index)
        {
            if (texts == null || index < 0 || index >= texts.Length)
                return null;
            var textComp = texts[index];
            if (textComp == null)
                return null;
            string raw = textComp.text;
            if (string.IsNullOrEmpty(raw))
                return null;
            return TextUtilities.StripRichTextTags(raw);
        }

        private void CheckTabChange()
        {
            if (_mailPanel == null)
                return;

            int currentTab = _mailPanel.m_SelectTab;

            if (currentTab != _lastTab && _lastTab >= 0)
            {
                _lastCursor = -1;
                _pendingFirstMail = false;
                string tabName = GetTabName(currentTab);
                var digimeState = _digimePanel?.m_CurrentState ?? uDigiviceDigimePanel.State.NONE;

                if (digimeState == uDigiviceDigimePanel.State.SELECT_FOLDER)
                {
                    var (pos, total) = GetTabPosition(currentTab);
                    ScreenReader.Say($"{tabName}, {pos} of {total}");
                }
                else
                {
                    int total = GetMailCount();
                    if (total == 0)
                    {
                        ScreenReader.Say($"{tabName}, empty");
                    }
                    else
                    {
                        int cursor = _mailPanel.m_selectNo;
                        _lastCursor = cursor;
                        string mailInfo = GetCurrentMailInfo();
                        if (!string.IsNullOrEmpty(mailInfo))
                            ScreenReader.Say($"{tabName}. {mailInfo}, {cursor + 1} of {total}");
                        else
                            ScreenReader.Say($"{tabName}. {cursor + 1} of {total}");
                    }
                }

                DebugLogger.Log($"{LogTag} Tab changed to {tabName}");
                _lastTab = currentTab;
            }
            else if (_lastTab < 0)
            {
                _lastTab = currentTab;
            }
        }

        private void CheckCursorChange()
        {
            if (_mailPanel == null)
                return;

            int currentCursor = _mailPanel.m_selectNo;

            if (currentCursor != _lastCursor)
            {
                if (_lastCursor >= 0)
                {
                    int total = GetMailCount();
                    if (total == 0)
                    {
                        ScreenReader.Say("Empty");
                    }
                    else
                    {
                        string mailInfo = GetCurrentMailInfo();
                        if (!string.IsNullOrEmpty(mailInfo))
                            ScreenReader.Say($"{mailInfo}, {currentCursor + 1} of {total}");
                        else
                            ScreenReader.Say($"{currentCursor + 1} of {total}");
                    }

                }
                _lastCursor = currentCursor;
            }
        }

        private string GetCurrentMailInfo()
        {
            try
            {
                if (_mailPanel == null)
                    return null;

                var parts = new System.Collections.Generic.List<string>();

                // Read title directly from UI text (most reliable - this is what the game renders)
                try
                {
                    var nameText = _mailPanel.m_panel3ItemNameText;
                    if (nameText != null)
                    {
                        string title = nameText.text;
                        if (!string.IsNullOrEmpty(title))
                            parts.Add(TextUtilities.StripRichTextTags(title));
                    }
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Log($"{LogTag} Title from UI error: {ex.Message}");
                }

                // Get mail ID from the selected item for sender/status lookups
                int mailId = -1;
                try
                {
                    var selectItem = _mailPanel.m_selectItem;
                    if (selectItem != null)
                        mailId = selectItem.m_mailID;
                }
                catch { }

                // Fallback: try GetSelectMail
                if (mailId < 0)
                {
                    try { mailId = _mailPanel.GetSelectMail(); }
                    catch { }
                }

                if (mailId >= 0)
                {
                    var manager = DigitalMessengerManager.Ref;
                    if (manager != null)
                    {
                        // If we didn't get a title from UI, try deserializing
                        if (parts.Count == 0)
                        {
                            try
                            {
                                manager.DeserializeMail(mailId);
                                var deserializer = manager.m_MailTextDeserialize;
                                if (deserializer != null)
                                {
                                    string title = deserializer.title;
                                    if (!string.IsNullOrEmpty(title))
                                        parts.Add(TextUtilities.StripRichTextTags(title));
                                }
                            }
                            catch { }
                        }

                        // Sender
                        try
                        {
                            string sender = manager.GetSenderName(mailId);
                            if (!string.IsNullOrEmpty(sender))
                                parts.Add($"from {TextUtilities.StripRichTextTags(sender)}");
                        }
                        catch { }

                        // Unread
                        try
                        {
                            if (!manager.IsReadMail(mailId))
                                parts.Add("new");
                        }
                        catch { }

                        // Attachment
                        try
                        {
                            if (manager.IsRemainingAttachedData(mailId))
                                parts.Add("has attachment");
                        }
                        catch { }
                    }
                }

                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting mail info: {ex.Message}");
                return null;
            }
        }

        private (int pos, int total) GetTabPosition(int tabIndex)
        {
            try
            {
                var tabInfoTbl = _mailPanel?.m_SelectTabInfoTbl;
                if (tabInfoTbl == null)
                    return (tabIndex + 1, 4);

                int total = 0;
                int pos = 0;
                for (int i = 0; i < tabInfoTbl.Length; i++)
                {
                    var tabInfo = tabInfoTbl[i];
                    if (tabInfo != null && tabInfo.m_isEnable)
                    {
                        total++;
                        if (i == tabIndex)
                            pos = total;
                    }
                }

                return (pos > 0 ? pos : tabIndex + 1, total > 0 ? total : tabInfoTbl.Length);
            }
            catch
            {
                return (tabIndex + 1, 4);
            }
        }

        private string GetTabName(int tabIndex)
        {
            try
            {
                var tabInfoTbl = _mailPanel?.m_SelectTabInfoTbl;
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
                                return TextUtilities.StripRichTextTags(text);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting tab name: {ex.Message}");
            }

            return tabIndex switch
            {
                0 => "Main",
                1 => "Sub",
                2 => "DLC",
                3 => "Daily Quest",
                _ => AnnouncementBuilder.FallbackItem("Folder", tabIndex)
            };
        }

        private int GetMailCount()
        {
            try
            {
                // Use m_itemList from uItemBase - this is the actual display list
                var itemList = _mailPanel?.m_itemList;
                if (itemList != null && itemList.Count > 0)
                    return itemList.Count;

                // Fallback to mail-specific list
                var mailList = _mailPanel?.m_ListMailData;
                if (mailList != null)
                    return mailList.Count;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting mail count: {ex.Message}");
            }
            return 0;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _digimePanel?.m_CurrentState ?? uDigiviceDigimePanel.State.NONE;

            if (state == uDigiviceDigimePanel.State.LOOK_BODY)
            {
                ScreenReader.Say("Mail, reading message");
                return;
            }

            if (_mailPanel == null)
            {
                ScreenReader.Say("Mail");
                return;
            }

            int tab = _mailPanel.m_SelectTab;
            string tabName = GetTabName(tab);

            if (state == uDigiviceDigimePanel.State.SELECT_FOLDER)
            {
                var (pos, total) = GetTabPosition(tab);
                ScreenReader.Say($"Mail, {tabName}, {pos} of {total}");
                return;
            }

            int cursor = _mailPanel.m_selectNo;
            int mailTotal = GetMailCount();

            if (mailTotal == 0)
            {
                ScreenReader.Say($"Mail, {tabName}, empty");
            }
            else
            {
                string mailInfo = GetCurrentMailInfo();
                if (!string.IsNullOrEmpty(mailInfo))
                    ScreenReader.Say($"Mail, {tabName}. {mailInfo}, {cursor + 1} of {mailTotal}");
                else
                    ScreenReader.Say($"Mail, {tabName}. {cursor + 1} of {mailTotal}");
            }
        }
    }
}
