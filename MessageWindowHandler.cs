using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for all message/dialog systems in the game.
    /// Monitors: EventWindowPanel (story), uCommonMessageWindow (field messages),
    /// uDigimonMessagePanel (partner status), battle dialogs, and captions.
    ///
    /// Uses Harmony patches to intercept dialog text the moment it's set,
    /// announcing the full text immediately without waiting for typewriter animation.
    /// </summary>
    public class MessageWindowHandler
    {
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

        // Battle dialog tracking
        private uBattlePanelDialog _battleDialog;
        private bool _wasBattleDialogActive = false;
        private string _lastBattleMessage = "";

        // Caption tracking (field hints/instructions)
        private uCaptionBase _captionPanel;
        private bool _wasCaptionActive = false;
        private string _lastCaptionText = "";

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
            return IsEventPanelOpen() || IsCommonWindowOpen() || IsDigimonPanelOpen() || IsBattleDialogOpen() || IsCaptionOpen();
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

            UpdateBattleDialog();
            UpdateCaption();
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
            catch { }
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
            catch { }

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
            catch { }

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

                // Get the center message window (main field messages)
                var centerWindow = msgMgr.GetCenter();
                if (centerWindow != null)
                {
                    string text = "";
                    try
                    {
                        if (centerWindow.m_label != null)
                            text = centerWindow.m_label.text ?? "";
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
                    {
                        _commonMessageWindow = centerWindow;
                        return true;
                    }
                }

                // Also check partner message windows (Get00, Get01)
                var partner0 = msgMgr.Get00();
                if (partner0 != null)
                {
                    string text = "";
                    try
                    {
                        if (partner0.m_label != null)
                            text = partner0.m_label.text ?? "";
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
                    {
                        _commonMessageWindow = partner0;
                        return true;
                    }
                }

                var partner1 = msgMgr.Get01();
                if (partner1 != null)
                {
                    string text = "";
                    try
                    {
                        if (partner1.m_label != null)
                            text = partner1.m_label.text ?? "";
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
                    {
                        _commonMessageWindow = partner1;
                        return true;
                    }
                }

                // Also check RightUp window (recruitment notifications, etc.)
                var rightUp = msgMgr.GetRightUp();
                if (rightUp != null)
                {
                    string text = "";
                    try
                    {
                        if (rightUp.m_label != null)
                            text = rightUp.m_label.text ?? "";
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
                    {
                        _commonMessageWindow = rightUp;
                        return true;
                    }
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
            catch { }

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
            catch { }

            return "";
        }

        #endregion

        #region uBattlePanelDialog (Battle Messages)

        private bool IsBattleDialogOpen()
        {
            try
            {
                var dialogs = Object.FindObjectsOfType<uBattlePanelDialog>();
                if (dialogs != null)
                {
                    foreach (var dialog in dialogs)
                    {
                        if (dialog != null && dialog.m_isOpend)
                        {
                            _battleDialog = dialog;
                            return true;
                        }
                    }
                }
            }
            catch { }

            _battleDialog = null;
            return false;
        }

        private void UpdateBattleDialog()
        {
            bool isActive = IsBattleDialogOpen();

            if (isActive && !_wasBattleDialogActive)
            {
                OnBattleDialogOpen();
            }
            else if (!isActive && _wasBattleDialogActive)
            {
                OnBattleDialogClose();
            }
            else if (isActive)
            {
                CheckBattleDialogChange();
            }

            _wasBattleDialogActive = isActive;
        }

        private void OnBattleDialogOpen()
        {
            _lastBattleMessage = "";

            if (_battleDialog == null)
                return;

            string message = GetBattleDialogText();
            if (!string.IsNullOrEmpty(message) && !IsIgnoredText(message))
            {
                AnnounceMessage(message, "Battle");
                _lastBattleMessage = message;
            }
        }

        private void OnBattleDialogClose()
        {
            _battleDialog = null;
            _lastBattleMessage = "";
        }

        private void CheckBattleDialogChange()
        {
            if (_battleDialog == null)
                return;

            string message = GetBattleDialogText();

            // Skip ignored text
            if (IsIgnoredText(message))
                return;

            if (!string.IsNullOrEmpty(message) && message != _lastBattleMessage)
            {
                AnnounceMessage(message, "Battle");
                _lastBattleMessage = message;
            }
        }

        private string GetBattleDialogText()
        {
            if (_battleDialog == null)
                return "";

            try
            {
                // Search all Text components in the battle dialog
                var texts = _battleDialog.GetComponentsInChildren<Text>();
                if (texts != null)
                {
                    foreach (var text in texts)
                    {
                        if (text != null && !string.IsNullOrEmpty(text.text))
                        {
                            string txt = CleanText(text.text);
                            if (txt.Length > 5)
                                return txt;
                        }
                    }
                }
            }
            catch { }

            return "";
        }

        #endregion

        #region uCaptionBase (Field Hints/Instructions)

        private bool IsCaptionOpen()
        {
            try
            {
                // Search for any active caption panels (tutorial hints, field instructions)
                var captions = Object.FindObjectsOfType<uCaptionBase>();
                if (captions != null)
                {
                    foreach (var caption in captions)
                    {
                        if (caption != null && caption.m_isOpend)
                        {
                            // Check if it has text
                            string text = GetCaptionTextFromPanel(caption);
                            if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
                            {
                                _captionPanel = caption;
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }

            _captionPanel = null;
            return false;
        }

        private void UpdateCaption()
        {
            // Skip caption announcements during battle result screen
            // BattleResultHandler handles those announcements
            if (IsBattleResultActive())
            {
                _wasCaptionActive = false;
                return;
            }

            // Skip caption announcements during training panel
            // The button hint captions ("or Bonus", "or Skill") aren't useful
            // TrainingPanelHandler handles its own tab announcements
            if (IsTrainingPanelActive())
            {
                _wasCaptionActive = false;
                return;
            }

            bool isActive = IsCaptionOpen();

            if (isActive && !_wasCaptionActive)
            {
                OnCaptionOpen();
            }
            else if (!isActive && _wasCaptionActive)
            {
                OnCaptionClose();
            }
            else if (isActive)
            {
                CheckCaptionChange();
            }

            _wasCaptionActive = isActive;
        }

        /// <summary>
        /// Check if the battle result panel is currently active.
        /// Used to suppress caption announcements during result screen.
        /// </summary>
        private bool IsBattleResultActive()
        {
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null)
                {
                    var resultPanel = battlePanel.m_result;
                    if (resultPanel != null && resultPanel.m_enabled)
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if the training panel is currently active.
        /// Used to suppress caption announcements during training.
        /// </summary>
        private bool IsTrainingPanelActive()
        {
            try
            {
                var panel = Object.FindObjectOfType<uTrainingPanelCommand>();
                if (panel != null && panel.gameObject != null && panel.gameObject.activeInHierarchy)
                {
                    var state = panel.m_state;
                    return state != uTrainingPanelCommand.State.None &&
                           state != uTrainingPanelCommand.State.Close;
                }
            }
            catch { }
            return false;
        }

        private void OnCaptionOpen()
        {
            _lastCaptionText = "";

            if (_captionPanel == null)
                return;

            string text = GetCaptionText();
            if (!string.IsNullOrEmpty(text) && !IsIgnoredText(text))
            {
                AnnounceMessage(text, "Caption");
                _lastCaptionText = text;
            }
        }

        private void OnCaptionClose()
        {
            _captionPanel = null;
            _lastCaptionText = "";
        }

        private void CheckCaptionChange()
        {
            if (_captionPanel == null)
                return;

            string text = GetCaptionText();

            if (IsIgnoredText(text))
                return;

            if (!string.IsNullOrEmpty(text) && text != _lastCaptionText)
            {
                AnnounceMessage(text, "Caption");
                _lastCaptionText = text;
            }
        }

        private string GetCaptionText()
        {
            return GetCaptionTextFromPanel(_captionPanel);
        }

        private string GetCaptionTextFromPanel(uCaptionBase caption)
        {
            if (caption == null)
                return "";

            try
            {
                if (caption.m_text != null)
                {
                    string text = caption.m_text.text;
                    if (!string.IsNullOrEmpty(text))
                        return CleanText(text);
                }
            }
            catch { }

            return "";
        }

        #endregion

        #region Utility Methods

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
            else if (IsBattleDialogOpen())
            {
                announcement = GetBattleDialogText();
            }
            else if (IsCaptionOpen())
            {
                announcement = GetCaptionText();
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
