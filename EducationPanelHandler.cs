using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Education panel (Praise/Scold interaction).
    /// Main state: emotion message first, then Praise/Scold/Do Nothing choices appear
    /// when the cursor object becomes active. Education state: digimon reaction message
    /// after selecting an option.
    /// </summary>
    public class EducationPanelHandler : IAccessibilityHandler
    {
        public int Priority => 61;

        private uEducationPanel _educationPanel;
        private uCarePanelCommand _commandPanel;
        private uEducationPanelEducationMessage _messagePanel;
        private bool _wasActive;
        private int _lastCursor = -1;
        private string _lastEmotionMessage;
        private bool _commandMenuAnnounced;
        private bool _lastCursorObjActive;

        // Education completion state - used by SetMessagePrefix to queue second partner messages
        private static bool _inEducationCompletion;
        private static bool _educationHasFirstMessage;

        /// <summary>
        /// True when the education panel is in its completion sequence (after Praise/Scold selection).
        /// </summary>
        public static bool IsInEducationCompletion => _inEducationCompletion;

        /// <summary>
        /// Returns true if a subsequent education completion message should be queued.
        /// Called by SetMessagePrefix to decide Say vs SayQueued.
        /// </summary>
        public static bool ShouldQueueNextMessage()
        {
            if (!_inEducationCompletion)
                return false;

            if (!_educationHasFirstMessage)
            {
                _educationHasFirstMessage = true;
                return false;
            }

            // Second message queued — pair complete, stop queuing further messages
            _inEducationCompletion = false;
            _educationHasFirstMessage = false;
            return true;
        }

        public bool IsOpen()
        {
            _educationPanel = GetEducationPanel();
            if (_educationPanel == null)
                return false;

            try
            {
                if (_educationPanel.gameObject == null || !_educationPanel.gameObject.activeInHierarchy)
                    return false;
            }
            catch
            {
                return false;
            }

            // Only handle Main state for IsOpen() - emotion message + choices
            // Education state is monitored by MonitorEducationCompletion() for message queuing
            if (_educationPanel.m_state != uCarePanel.State.Main)
                return false;

            _commandPanel = _educationPanel.m_commandPanel;
            _messagePanel = _educationPanel.m_educationMessage;

            return true;
        }

        private uEducationPanel GetEducationPanel()
        {
            try
            {
                var mgr = MainGameManager.m_instance;
                if (mgr != null)
                {
                    var eduUI = mgr.educationUI;
                    if (eduUI != null)
                        return eduUI;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Education] Error getting educationUI: {ex.Message}");
            }

            return null;
        }

        public void Update()
        {
            bool isActive = IsOpen();

            if (isActive && !_wasActive)
                OnOpen();
            else if (!isActive && _wasActive)
                OnClose();
            else if (isActive)
                OnUpdate();

            _wasActive = isActive;

            // Track education completion state for message queuing
            MonitorEducationCompletion();
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastEmotionMessage = null;
            _commandMenuAnnounced = false;
            _lastCursorObjActive = false;

            DebugLogger.Log("[Education] Opened");

            // Read the initial emotion message
            string emotionText = GetEmotionMessage();
            if (!string.IsNullOrEmpty(emotionText))
            {
                _lastEmotionMessage = emotionText;
                ScreenReader.Say(emotionText);
                DebugLogger.Log($"[Education] Emotion message: {emotionText}");
            }

            // Check if cursor is already active (choices already visible)
            _lastCursorObjActive = IsCursorActive();
            if (_lastCursorObjActive)
                AnnounceCommandMenu();
        }

        private void OnClose()
        {
            DebugLogger.Log("[Education] Closed");
            _educationPanel = null;
            _commandPanel = null;
            _messagePanel = null;
            _lastCursor = -1;
            _lastEmotionMessage = null;
            _commandMenuAnnounced = false;
            _lastCursorObjActive = false;
        }

        /// <summary>
        /// Tracks the education panel's state independently of IsOpen() (which only handles Main).
        /// When state enters Education (post Praise/Scold), sets a flag so SetMessagePrefix
        /// queues the second partner's message instead of interrupting the first.
        /// The flag stays active through post-Education states (Message, Wait, Close) since
        /// the stat SetMessage calls arrive after the state leaves Education.
        /// Only clears when the panel fully closes or returns to None/Main.
        /// </summary>
        private void MonitorEducationCompletion()
        {
            try
            {
                var panel = GetEducationPanel();
                if (panel == null || panel.gameObject == null || !panel.gameObject.activeInHierarchy)
                {
                    if (_inEducationCompletion)
                    {
                        _inEducationCompletion = false;
                        _educationHasFirstMessage = false;
                        DebugLogger.Log("[Education] Completion ended (panel closed)");
                    }
                    return;
                }

                var state = panel.m_state;

                if (state == uCarePanel.State.Education && !_inEducationCompletion)
                {
                    _inEducationCompletion = true;
                    _educationHasFirstMessage = false;
                    DebugLogger.Log("[Education] Entered completion state");
                }
                else if (_inEducationCompletion &&
                         (state == uCarePanel.State.None || state == uCarePanel.State.Main))
                {
                    _inEducationCompletion = false;
                    _educationHasFirstMessage = false;
                    DebugLogger.Log("[Education] Completion ended (returned to idle)");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Education] Error monitoring completion: {ex.Message}");
            }
        }

        private void OnUpdate()
        {
            // Poll for emotion message if we haven't seen one yet
            if (_lastEmotionMessage == null)
                CheckEmotionMessage();

            // Detect cursor object becoming active = choices are now visible
            bool cursorActive = IsCursorActive();
            if (cursorActive && !_lastCursorObjActive && !_commandMenuAnnounced)
            {
                DebugLogger.Log("[Education] Choices now visible (cursor active)");
                AnnounceCommandMenu();
            }
            _lastCursorObjActive = cursorActive;

            // Track cursor changes while choices are visible
            if (cursorActive)
                CheckCursorChange();
        }

        /// <summary>
        /// The cursor object on m_commandPanel becomes active when choices are navigable.
        /// </summary>
        private bool IsCursorActive()
        {
            if (_commandPanel == null)
                return false;

            try
            {
                var cursor = _commandPanel.m_cursor;
                return cursor != null && cursor.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        private void CheckEmotionMessage()
        {
            string emotionText = GetEmotionMessage();
            if (string.IsNullOrEmpty(emotionText))
                return;

            if (emotionText != _lastEmotionMessage)
            {
                _lastEmotionMessage = emotionText;
                ScreenReader.Say(emotionText);
                DebugLogger.Log($"[Education] Emotion message: {emotionText}");
            }
        }

        private void AnnounceCommandMenu()
        {
            _commandMenuAnnounced = true;

            if (_commandPanel == null)
            {
                ScreenReader.Say("Praise or Scold");
                return;
            }

            int cursor = _commandPanel.m_selectNo;
            int total = _commandPanel.m_selectMax;
            string itemText = GetCommandName(cursor);

            string announcement = total > 0
                ? AnnouncementBuilder.MenuOpen("Praise or Scold", itemText, cursor, total)
                : $"Praise or Scold. {itemText}";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[Education] Command menu: {itemText} ({cursor + 1}/{total})");
            _lastCursor = cursor;
        }

        private void CheckCursorChange()
        {
            if (_commandPanel == null)
                return;

            int cursor = _commandPanel.m_selectNo;
            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetCommandName(cursor);
                int total = _commandPanel.m_selectMax;

                string announcement = total > 0
                    ? AnnouncementBuilder.CursorPosition(itemText, cursor, total)
                    : itemText;

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[Education] Cursor: {itemText} ({cursor + 1}/{total})");
                _lastCursor = cursor;
            }
        }

        private string GetEmotionMessage()
        {
            // Try the message panel UI component first
            string panelText = GetMessagePanelText();
            if (!string.IsNullOrEmpty(panelText))
                return panelText;

            // Fallback to EducationManager
            try
            {
                var eduMgr = EducationManager.Ref;
                if (eduMgr != null)
                {
                    string msg = eduMgr.emotionMessage;
                    if (!string.IsNullOrEmpty(msg))
                        return TextUtilities.StripRichTextTags(msg);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Education] Error reading EducationManager: {ex.Message}");
            }

            return null;
        }

        private string GetMessagePanelText()
        {
            try
            {
                if (_messagePanel != null &&
                    _messagePanel.gameObject != null &&
                    _messagePanel.gameObject.activeInHierarchy)
                {
                    var msgComponent = _messagePanel.m_message;
                    if (msgComponent != null)
                    {
                        string text = msgComponent.text;
                        if (!string.IsNullOrEmpty(text))
                            return TextUtilities.StripRichTextTags(text);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Education] Error reading message panel: {ex.Message}");
            }

            return null;
        }

        private string GetCommandName(int index)
        {
            try
            {
                if (_commandPanel == null || index < 0)
                    return "Option";

                var choiceText = _commandPanel.m_choiceText;
                if (choiceText != null && index < choiceText.Length)
                {
                    var textComponent = choiceText[index];
                    if (textComponent != null)
                    {
                        string text = textComponent.text;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }

                var commandNames = _commandPanel.m_command_name;
                if (commandNames != null && index < commandNames.Length)
                {
                    string cmdName = commandNames[index];
                    if (!string.IsNullOrEmpty(cmdName))
                        return cmdName;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Education] Error getting command name: {ex.Message}");
            }

            return "Option";
        }

        public void AnnounceStatus()
        {
            if (_educationPanel == null)
            {
                ScreenReader.Say("Praise or Scold");
                return;
            }

            var state = _educationPanel.m_state;

            if (state == uCarePanel.State.Main && IsCursorActive() && _commandPanel != null)
            {
                int cursor = _commandPanel.m_selectNo;
                int total = _commandPanel.m_selectMax;
                string itemText = GetCommandName(cursor);

                string announcement = total > 0
                    ? AnnouncementBuilder.MenuOpen("Praise or Scold", itemText, cursor, total)
                    : $"Praise or Scold. {itemText}";

                ScreenReader.Say(announcement);
            }
            else
            {
                string text = GetEmotionMessage();
                if (!string.IsNullOrEmpty(text))
                    ScreenReader.Say(text);
                else
                    ScreenReader.Say("Praise or Scold");
            }
        }
    }
}
