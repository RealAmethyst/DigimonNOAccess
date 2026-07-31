using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the agreement windows (EULA, Privacy Policy, KPI/Data Analysis consent).
    /// These appear when starting a new game for the first time.
    /// </summary>
    public class AgreeWindowHandler : IAccessibilityHandler
    {
        public int Priority => 15;

        private uAgreeWindow _window;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uAgreeWindow.AgreeWindowType _lastWindowType;

        public bool IsOpen()
        {
            try
            {
                _window = Object.FindObjectOfType<uAgreeWindow>();
                return _window != null &&
                       _window.gameObject != null &&
                       _window.gameObject.activeInHierarchy &&
                       _window.IsOpen;
            }
            catch
            {
                return false;
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
                CheckChanges();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;

            if (_window == null)
                return;

            var windowType = _window.m_currentWindowType;
            _lastWindowType = windowType;
            int cursor = (int)_window.m_cursorIndex;
            string windowName = GetWindowTypeName(windowType);
            string selection = GetSelectionName(cursor);

            string announcement = AnnouncementBuilder.MenuOpen(windowName, selection, cursor, 2);
            ScreenReader.Say(announcement);
            DebugLogger.Log($"[AgreeWindow] Opened: type={windowType}, cursor={cursor}");

            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _window = null;
            _lastCursor = -1;
            DebugLogger.Log("[AgreeWindow] Closed");
        }

        private void CheckChanges()
        {
            if (_window == null)
                return;

            // Check if window type changed (shouldn't happen often, but handle it)
            var windowType = _window.m_currentWindowType;
            if (windowType != _lastWindowType)
            {
                _lastWindowType = windowType;
                _lastCursor = -1;
                string windowName = GetWindowTypeName(windowType);
                int cursor = (int)_window.m_cursorIndex;
                string selection = GetSelectionName(cursor);

                string announcement = AnnouncementBuilder.MenuOpen(windowName, selection, cursor, 2);
                ScreenReader.Say(announcement);
                DebugLogger.Log($"[AgreeWindow] Type changed: {windowType}");
                _lastCursor = cursor;
                return;
            }

            // Check cursor change
            int currentCursor = (int)_window.m_cursorIndex;
            if (currentCursor != _lastCursor)
            {
                string selection = GetSelectionName(currentCursor);
                string announcement = AnnouncementBuilder.CursorPosition(selection, currentCursor, 2);
                ScreenReader.Say(announcement);
                DebugLogger.Log($"[AgreeWindow] Cursor changed: {currentCursor} = {selection}");
                _lastCursor = currentCursor;
            }
        }

        /// <summary>
        /// The title of the agreement being shown.
        ///
        /// Read from the window's own header, so it matches what is on screen in
        /// whatever language the player is running. The English names are a fallback
        /// for the frame before the header is populated - these screens appear before
        /// the player has chosen a language, so getting them right matters.
        /// </summary>
        private string GetWindowTypeName(uAgreeWindow.AgreeWindowType type)
        {
            string header = GetHeaderText();
            if (!string.IsNullOrWhiteSpace(header))
                return TextUtilities.StripRichTextTags(header).Trim();

            switch (type)
            {
                case uAgreeWindow.AgreeWindowType.Eula:
                    return "End User License Agreement";
                case uAgreeWindow.AgreeWindowType.PP:
                    return "Privacy Policy";
                case uAgreeWindow.AgreeWindowType.KPI:
                    return "Data Analysis Consent";
                default:
                    return "Agreement";
            }
        }

        /// <summary>
        /// The rendered header text, or null if it is not reachable yet. Each hop logs
        /// its own reason so a chain broken by a game update is visible in the log
        /// rather than silently reverting everyone to English.
        /// </summary>
        private string GetHeaderText()
        {
            try
            {
                if (_window == null)
                    return null;

                var header = _window.m_Header;
                if (header == null)
                {
                    DebugLogger.Log("[AgreeWindow] Header: window has no header component");
                    return null;
                }

                var text = header.m_headerText;
                if (text == null)
                {
                    DebugLogger.Log("[AgreeWindow] Header: header component has no Text");
                    return null;
                }

                return text.text;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[AgreeWindow] Header read failed: {ex.Message}");
                return null;
            }
        }

        private string GetSelectionName(int cursor)
        {
            if (_window == null)
                return cursor == 0 ? "Yes" : "No";

            try
            {
                // Try to get text from the UI components
                if (cursor == 0 && _window.m_yes != null && !string.IsNullOrEmpty(_window.m_yes.text))
                {
                    return TextUtilities.CleanText(_window.m_yes.text);
                }
                else if (cursor == 1 && _window.m_no != null && !string.IsNullOrEmpty(_window.m_no.text))
                {
                    return TextUtilities.CleanText(_window.m_no.text);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[AgreeWindow] Error getting selection text: {ex.Message}");
            }

            // Fallback to Yes/No based on CursorIndex enum
            return cursor == (int)uAgreeWindow.CursorIndex.Yes ? "Yes" : "No";
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var windowType = _window.m_currentWindowType;
            int cursor = (int)_window.m_cursorIndex;
            string windowName = GetWindowTypeName(windowType);
            string selection = GetSelectionName(cursor);

            string announcement = AnnouncementBuilder.MenuOpen(windowName, selection, cursor, 2);
            ScreenReader.Say(announcement);
        }
    }
}
