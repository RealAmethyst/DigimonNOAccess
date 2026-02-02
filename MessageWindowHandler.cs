using Il2Cpp;
using MelonLoader;
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
        // Uses a shorter delay since these aren't animated with typewriter
        private uCommonMessageWindow _commonMessageWindow;
        private bool _wasCommonActive = false;
        private string _lastCommonMessage = "";
        private string _currentCommonText = "";
        private float _commonTextLastChangeTime = 0f;
        private const float COMMON_TEXT_DELAY = 0.05f; // Very short delay for common messages

        // Digimon message panel tracking (partner messages)
        private uDigimonMessagePanel _digimonMessagePanel;
        private bool _wasDigimonPanelActive = false;
        private string _lastDigimonMessage = "";

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
        private int _frameCounter = 0;
        private const int DEBUG_LOG_INTERVAL = 300; // Log every 5 seconds at 60fps

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
                DebugLogger.Log("[MessageWindow] Subscribed to DialogTextPatch events");
            }

            _frameCounter++;

            // Periodic debug logging
            if (_frameCounter >= DEBUG_LOG_INTERVAL)
            {
                _frameCounter = 0;
                LogActiveUIState();
            }

            // Story dialog is now handled by the patch callback (OnDialogTextIntercepted)
            // We still track panel state for status queries
            UpdateEventPanelState();

            UpdateCommonMessageWindow();
            UpdateDigimonMessagePanel();
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

                DebugLogger.Log($"[MessageWindow] Immediate announcement: name='{cleanedName}', text='{TruncateText(cleanedText, 50)}'");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MessageWindow] Error in OnDialogTextIntercepted: {ex.Message}");
            }
        }

        /// <summary>
        /// Log what UI elements are currently active for debugging.
        /// </summary>
        private void LogActiveUIState()
        {
            try
            {
                // Check EventWindowPanel
                var eventPanels = Object.FindObjectsOfType<EventWindowPanel>();
                if (eventPanels != null && eventPanels.Length > 0)
                {
                    foreach (var panel in eventPanels)
                    {
                        if (panel != null && panel.gameObject != null)
                        {
                            bool active = panel.gameObject.activeInHierarchy;
                            string text = "";
                            string name = "";
                            try
                            {
                                if (panel.m_normalText != null)
                                    text = panel.m_normalText.text ?? "";
                                if (panel.m_nameText != null)
                                    name = panel.m_nameText.text ?? "";
                            }
                            catch { }
                            if (active && !string.IsNullOrEmpty(text))
                            {
                                DebugLogger.Log($"[Debug] EventWindowPanel: name='{name}', text='{TruncateText(text, 50)}'");
                            }
                        }
                    }
                }

                // Check uCommonMessageWindow
                var commonWindows = Object.FindObjectsOfType<uCommonMessageWindow>();
                if (commonWindows != null && commonWindows.Length > 0)
                {
                    foreach (var window in commonWindows)
                    {
                        if (window != null && window.gameObject != null)
                        {
                            bool active = window.gameObject.activeInHierarchy;
                            string text = "";
                            try
                            {
                                if (window.m_label != null)
                                    text = window.m_label.text ?? "";
                            }
                            catch { }
                            DebugLogger.Log($"[Debug] uCommonMessageWindow: active={active}, text='{TruncateText(text, 50)}'");
                        }
                    }
                }

                // Check MainGameManager
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null)
                    {
                        var msgMgr = mgm.MessageManager;
                        if (msgMgr != null)
                        {
                            bool hasActive = msgMgr.IsFindActive();
                            DebugLogger.Log($"[Debug] MainGameManager.MessageManager.IsFindActive() = {hasActive}");

                            if (hasActive)
                            {
                                var centerWindow = msgMgr.GetCenter();
                                if (centerWindow != null && centerWindow.m_label != null)
                                {
                                    DebugLogger.Log($"[Debug] Center window text: '{TruncateText(centerWindow.m_label.text, 50)}'");
                                }
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Log($"[Debug] MainGameManager access error: {ex.Message}");
                }

                // Check TalkMain instances
                var talkMains = Object.FindObjectsOfType<TalkMain>();
                if (talkMains != null && talkMains.Length > 0)
                {
                    foreach (var talk in talkMains)
                    {
                        if (talk != null)
                        {
                            DebugLogger.Log($"[Debug] TalkMain found, m_isGameActive={talk.m_isGameActive}");

                            // Check ui_root (EventWindowPanel array)
                            var uiRoot = talk.m_ui_root;
                            if (uiRoot != null)
                            {
                                for (int i = 0; i < uiRoot.Length; i++)
                                {
                                    var panel = uiRoot[i];
                                    if (panel != null && panel.gameObject != null && panel.gameObject.activeInHierarchy)
                                    {
                                        string txt = panel.m_normalText?.text ?? "";
                                        string nm = panel.m_nameText?.text ?? "";
                                        DebugLogger.Log($"[Debug] TalkMain.m_ui_root[{i}]: name='{nm}', text='{TruncateText(txt, 50)}'");
                                    }
                                }
                            }
                        }
                    }
                }

                // Check TypewriterEffect
                try
                {
                    var typewriter = TypewriterEffect.current;
                    if (typewriter != null)
                    {
                        bool isActive = typewriter.isActive;
                        string fullText = typewriter.mFullText ?? "";
                        int offset = typewriter.mCurrentOffset;
                        DebugLogger.Log($"[Debug] TypewriterEffect: isActive={isActive}, offset={offset}/{fullText.Length}, fullText='{TruncateText(fullText, 50)}'");
                    }
                }
                catch { }

                // Check uCaptionBase
                var captions = Object.FindObjectsOfType<uCaptionBase>();
                if (captions != null && captions.Length > 0)
                {
                    foreach (var caption in captions)
                    {
                        if (caption != null && caption.gameObject != null && caption.gameObject.activeInHierarchy)
                        {
                            string txt = "";
                            try { if (caption.m_text != null) txt = caption.m_text.text ?? ""; } catch { }
                            if (!string.IsNullOrEmpty(txt))
                            {
                                DebugLogger.Log($"[Debug] uCaptionBase: active=True, text='{TruncateText(txt, 50)}'");
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Debug] Error in LogActiveUIState: {ex.Message}");
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
                                if (panel != null &&
                                    panel.gameObject != null &&
                                    panel.gameObject.activeInHierarchy)
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
                        if (panel != null &&
                            panel.gameObject != null &&
                            panel.gameObject.activeInHierarchy)
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
            }
            catch { }

            _commonMessageWindow = null;
            return false;
        }

        private void UpdateCommonMessageWindow()
        {
            bool isActive = IsCommonWindowOpen();

            if (isActive && !_wasCommonActive)
            {
                OnCommonWindowOpen();
            }
            else if (!isActive && _wasCommonActive)
            {
                OnCommonWindowClose();
            }
            else if (isActive)
            {
                CheckCommonMessageChange();
            }

            _wasCommonActive = isActive;
        }

        private void OnCommonWindowOpen()
        {
            _lastCommonMessage = "";
            _currentCommonText = "";
            _commonTextLastChangeTime = Time.time;

            DebugLogger.Log("[CommonMessage] Opened, waiting for text to stabilize");
        }

        private void OnCommonWindowClose()
        {
            _commonMessageWindow = null;
            _lastCommonMessage = "";
            _currentCommonText = "";
            DebugLogger.Log("[CommonMessage] Closed");
        }

        private void CheckCommonMessageChange()
        {
            if (_commonMessageWindow == null)
                return;

            string message = GetCommonMessageText();

            // Skip ignored text
            if (IsIgnoredText(message))
                return;

            // Check if text is still changing
            if (message != _currentCommonText)
            {
                _currentCommonText = message;
                _commonTextLastChangeTime = Time.time;
                return;
            }

            // Announce when text has been stable (very short delay for common messages)
            float timeSinceChange = Time.time - _commonTextLastChangeTime;
            if (timeSinceChange >= COMMON_TEXT_DELAY &&
                !string.IsNullOrEmpty(_currentCommonText) &&
                _currentCommonText != _lastCommonMessage)
            {
                AnnounceMessage(_currentCommonText, "Message");
                _lastCommonMessage = _currentCommonText;
            }
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
                        if (panel != null &&
                            panel.gameObject != null &&
                            panel.gameObject.activeInHierarchy)
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

        private void UpdateDigimonMessagePanel()
        {
            bool isActive = IsDigimonPanelOpen();

            if (isActive && !_wasDigimonPanelActive)
            {
                OnDigimonPanelOpen();
            }
            else if (!isActive && _wasDigimonPanelActive)
            {
                OnDigimonPanelClose();
            }
            else if (isActive)
            {
                CheckDigimonMessageChange();
            }

            _wasDigimonPanelActive = isActive;
        }

        private void OnDigimonPanelOpen()
        {
            _lastDigimonMessage = "";

            if (_digimonMessagePanel == null)
                return;

            string message = GetDigimonPanelText();
            if (!string.IsNullOrEmpty(message))
            {
                AnnounceMessage(message, "Digimon");
                _lastDigimonMessage = message;
            }
        }

        private void OnDigimonPanelClose()
        {
            _digimonMessagePanel = null;
            _lastDigimonMessage = "";
        }

        private void CheckDigimonMessageChange()
        {
            if (_digimonMessagePanel == null)
                return;

            string message = GetDigimonPanelText();
            if (!string.IsNullOrEmpty(message) && message != _lastDigimonMessage)
            {
                AnnounceMessage(message, "Digimon");
                _lastDigimonMessage = message;
            }
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
                        if (dialog != null &&
                            dialog.gameObject != null &&
                            dialog.gameObject.activeInHierarchy)
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
                        if (caption != null &&
                            caption.gameObject != null &&
                            caption.gameObject.activeInHierarchy)
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
            {
                DebugLogger.Log($"[{source}] IGNORED (placeholder/system): {TruncateText(message, 40)}");
                return;
            }

            float currentTime = Time.time;
            if (currentTime - _lastAnnouncementTime < MIN_ANNOUNCEMENT_INTERVAL)
            {
                return;
            }

            ScreenReader.Say(message);
            DebugLogger.Log($"[{source}] {message}");
            _lastAnnouncementTime = currentTime;
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Remove Unity rich text tags
            string cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
            // Normalize whitespace
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
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
                ScreenReader.Say(announcement);
            }
            else
            {
                ScreenReader.Say("No active dialog");
            }
        }

        #endregion
    }
}
