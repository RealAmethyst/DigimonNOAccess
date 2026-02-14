using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for all message/dialog systems in the game.
    /// Monitors: EventWindowPanel (story), uCommonMessageWindow (field messages),
    /// and uDigimonMessagePanel (partner status).
    ///
    /// Uses Harmony patches to intercept dialog text the moment it's set,
    /// announcing the full text immediately without waiting for typewriter animation.
    /// </summary>
    public class MessageWindowHandler : IAccessibilityHandler
    {
        public int Priority => 45;

        // EventWindowPanel tracking (main story dialog - used by TalkMain)
        private EventWindowPanel _eventPanel;
        private bool _wasEventPanelActive = false;
        private string _lastAnnouncedEventText = "";
        private string _lastAnnouncedEventName = "";

        // Immediate text interception tracking
        private bool _subscribedToPatch = false;

        // Common message window tracking (field messages)
        private uCommonMessageWindow _commonMessageWindow;

        // Digimon message panel tracking (partner messages)
        private uDigimonMessagePanel _digimonMessagePanel;



        // General tracking
        private float _lastAnnouncementTime = 0f;
        private const float MIN_ANNOUNCEMENT_INTERVAL = 0.1f;

        // Placeholder/system text patterns to ignore
        private static readonly string[] IGNORED_TEXT_PATTERNS = new string[]
        {
            "メッセージ入力欄",      // Japanese placeholder "message input field"
            "Warning",               // Copyright warning start
            "Transmitting",          // Copyright warning content
            "prohibited",            // Copyright warning content
            "©",                     // Copyright symbol
            "BANDAI NAMCO",          // Publisher name in system text
        };

        /// <summary>
        /// Check if any message window is currently open.
        /// </summary>
        public bool IsOpen()
        {
            return IsEventPanelOpen() || IsCommonWindowOpen() || IsDigimonPanelOpen();
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            // Subscribe to the patch event if not already subscribed
            if (!_subscribedToPatch)
            {
                DialogTextPatch.OnTextIntercepted += OnDialogTextIntercepted;
                _subscribedToPatch = true;
            }

            // Story dialog is now handled by the patch callback (OnDialogTextIntercepted)
            // We still track panel state for status queries
            UpdateEventPanelState();

        }

        /// <summary>
        /// Called when the Harmony patch intercepts dialog text.
        /// Announces immediately without waiting for typewriter animation.
        /// </summary>
        private void OnDialogTextIntercepted(string name, string text)
        {
            try
            {
                // Clean the text
                string cleanedText = CleanText(text);
                string cleanedName = CleanText(name);

                // Skip if already announced (dedup)
                if (cleanedText == _lastAnnouncedEventText && cleanedName == _lastAnnouncedEventName)
                {
                    DebugLogger.Log($"[MessageWindow] Skipping duplicate text");
                    return;
                }

                // Skip ignored text
                if (IsIgnoredText(cleanedText))
                {
                    DebugLogger.Log($"[MessageWindow] Skipping ignored text: {TruncateText(cleanedText, 40)}");
                    return;
                }

                // Build announcement with speaker name if different from last
                string announcement = BuildAnnouncement(cleanedName, cleanedText, _lastAnnouncedEventName);

                // Announce immediately
                AnnounceMessage(announcement, "Story");

                _lastAnnouncedEventText = cleanedText;
                _lastAnnouncedEventName = cleanedName;

            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MessageWindow] Error in OnDialogTextIntercepted: {ex.Message}");
            }
        }

        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\n", " ").Replace("\r", "");
            return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
        }

        #region EventWindowPanel (Story Dialog)

        private bool IsEventPanelOpen()
        {
            try
            {
                // First try through TalkMain which is the proper accessor
                var talkMains = Object.FindObjectsOfType<TalkMain>();
                if (talkMains != null)
                {
                    foreach (var talk in talkMains)
                    {
                        if (talk != null && talk.m_ui_root != null)
                        {
                            foreach (var panel in talk.m_ui_root)
                            {
                                if (panel != null && panel.m_isOpend)
                                {
                                    _eventPanel = panel;
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Fallback: direct search
                var panels = Object.FindObjectsOfType<EventWindowPanel>();
                if (panels != null)
                {
                    foreach (var panel in panels)
                    {
                        if (panel != null && panel.m_isOpend)
                        {
                            _eventPanel = panel;
                            return true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[EventPanel] Error checking: {ex.Message}");
            }

            _eventPanel = null;
            return false;
        }

        /// <summary>
        /// Track EventWindowPanel state for status queries.
        /// Text announcement is handled by the Harmony patch callback.
        /// </summary>
        private void UpdateEventPanelState()
        {
            bool isActive = IsEventPanelOpen();

            if (isActive && !_wasEventPanelActive)
            {
                DebugLogger.Log("[EventPanel] Opened - text will be announced via patch");
            }
            else if (!isActive && _wasEventPanelActive)
            {
                _eventPanel = null;
                _lastAnnouncedEventText = "";
                _lastAnnouncedEventName = "";
                DialogTextPatch.ResetLastAnnouncedText();
                DebugLogger.Log("[EventPanel] Closed");
            }

            _wasEventPanelActive = isActive;
        }

        /// <summary>
        /// Build announcement string with optional speaker name.
        /// </summary>
        private string BuildAnnouncement(string name, string text, string lastName)
        {
            if (!string.IsNullOrEmpty(name) && name != lastName)
                return $"{name}: {text}";
            return text;
        }

        private string GetEventPanelName()
        {
            if (_eventPanel == null)
                return "";

            try
            {
                if (_eventPanel.m_nameText != null)
                {
                    string name = _eventPanel.m_nameText.text;
                    if (!string.IsNullOrEmpty(name))
                        return CleanText(name);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MessageWindow] Error in GetEventPanelName: {ex.Message}");
            }

            return "";
        }

        private string GetEventPanelText()
        {
            if (_eventPanel == null)
                return "";

            try
            {
                if (_eventPanel.m_normalText != null)
                {
                    string text = _eventPanel.m_normalText.text;
                    if (!string.IsNullOrEmpty(text))
                        return CleanText(text);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MessageWindow] Error in GetEventPanelText: {ex.Message}");
            }

            return "";
        }

        #endregion

        #region uCommonMessageWindow (Field Messages)

        private bool IsCommonWindowOpen()
        {
            try
            {
                // Use the game's official message manager to check if a message is actually visible
                var mgm = MainGameManager.m_instance;
                if (mgm == null)
                    return false;

                var msgMgr = mgm.MessageManager;
                if (msgMgr == null)
                    return false;

                // IsFindActive() is the official way to check if any message is being displayed
                if (!msgMgr.IsFindActive())
                {
                    _commonMessageWindow = null;
                    return false;
                }

                // Check each message window - only consider it open if m_isOpend is true
                // (prevents reading stale label text from closed windows)
                var centerWindow = msgMgr.GetCenter();
                if (IsWindowOpen(centerWindow))
                {
                    _commonMessageWindow = centerWindow;
                    return true;
                }

                var partner0 = msgMgr.Get00();
                if (IsWindowOpen(partner0))
                {
                    _commonMessageWindow = partner0;
                    return true;
                }

                var partner1 = msgMgr.Get01();
                if (IsWindowOpen(partner1))
                {
                    _commonMessageWindow = partner1;
                    return true;
                }

                var rightUp = msgMgr.GetRightUp();
                if (IsWindowOpen(rightUp))
                {
                    _commonMessageWindow = rightUp;
                    return true;
                }
            }
            catch { }

            _commonMessageWindow = null;
            return false;
        }

        private string GetCommonMessageText()
        {
            if (_commonMessageWindow == null)
                return "";

            try
            {
                if (_commonMessageWindow.m_label != null)
                {
                    string text = _commonMessageWindow.m_label.text;
                    if (!string.IsNullOrEmpty(text))
                        return CleanText(text);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MessageWindow] Error in GetCommonMessageText: {ex.Message}");
            }

            return "";
        }

        #endregion

        #region uDigimonMessagePanel (Partner Status)

        private bool IsDigimonPanelOpen()
        {
            try
            {
                var panels = Object.FindObjectsOfType<uDigimonMessagePanel>();
                if (panels != null)
                {
                    foreach (var panel in panels)
                    {
                        if (panel != null && panel.m_isOpend)
                        {
                            _digimonMessagePanel = panel;
                            return true;
                        }
                    }
                }
            }
            catch { }

            _digimonMessagePanel = null;
            return false;
        }

        private string GetDigimonPanelText()
        {
            if (_digimonMessagePanel == null)
                return "";

            try
            {
                if (_digimonMessagePanel.m_text != null)
                {
                    string text = _digimonMessagePanel.m_text.text;
                    if (!string.IsNullOrEmpty(text))
                        return CleanText(text);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MessageWindow] Error in GetDigimonPanelText: {ex.Message}");
            }

            return "";
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Check if a common message window is actually open and has displayable text.
        /// Uses the game's m_isOpend flag to avoid reading stale label text from closed windows.
        /// </summary>
        private bool IsWindowOpen(uCommonMessageWindow window)
        {
            if (window == null || !window.m_isOpend)
                return false;

            try
            {
                if (window.m_label != null)
                {
                    string text = window.m_label.text ?? "";
                    if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
                        return true;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Check if text matches any ignored placeholder/system text patterns.
        /// </summary>
        private bool IsIgnoredText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            foreach (var pattern in IGNORED_TEXT_PATTERNS)
            {
                if (text.Contains(pattern))
                    return true;
            }

            return false;
        }

        private void AnnounceMessage(string message, string source)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // Filter out placeholder/system text
            if (IsIgnoredText(message))
                return;

            float currentTime = Time.time;
            if (currentTime - _lastAnnouncementTime < MIN_ANNOUNCEMENT_INTERVAL)
                return;

            DebugLogger.Log($"[MessageWindow:{source}] {message}");
            ScreenReader.Say(CleanText(message));
            _lastAnnouncementTime = currentTime;
        }

        private string CleanText(string text)
        {
            return TextUtilities.CleanText(text);
        }

        public void AnnounceStatus()
        {
            string announcement = "";

            if (IsEventPanelOpen())
            {
                string name = GetEventPanelName();
                string text = GetEventPanelText();
                if (!string.IsNullOrEmpty(text))
                {
                    announcement = string.IsNullOrEmpty(name) ? text : $"{name}: {text}";
                }
            }
            else if (IsCommonWindowOpen())
            {
                announcement = GetCommonMessageText();
            }
            else if (IsDigimonPanelOpen())
            {
                announcement = GetDigimonPanelText();
            }
            if (!string.IsNullOrEmpty(announcement))
            {
                ScreenReader.Say(CleanText(announcement));
            }
            else
            {
                ScreenReader.Say("No active dialog");
            }
        }

        #endregion
    }
}
