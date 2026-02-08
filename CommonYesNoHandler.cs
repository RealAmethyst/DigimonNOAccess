using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the common Yes/No confirmation dialog (uCommonYesNoWindow).
    /// This dialog appears when confirming actions like selecting an egg.
    /// </summary>
    public class CommonYesNoHandler : IAccessibilityHandler
    {
        public int Priority => 10;

        private uCommonYesNoWindow _window;
        private bool _wasActive = false;
        private uCommonYesNoWindow.CursorIndex _lastCursorIndex = uCommonYesNoWindow.CursorIndex.Yes;
        private int _pollCounter = 0;
        private const int POLL_INTERVAL = 3;

        /// <summary>
        /// Check if the Yes/No dialog is currently open and interactive.
        /// Must have a callback set to be truly interactive.
        /// </summary>
        public bool IsOpen()
        {
            _window = Object.FindObjectOfType<uCommonYesNoWindow>();

            if (_window == null)
                return false;

            try
            {
                // Must be active AND have a callback to be truly interactive
                if (!_window.gameObject.activeInHierarchy)
                    return false;

                // Check if callback is set - this indicates the dialog is truly interactive
                var callback = _window.m_callback;
                if (callback == null)
                    return false;

                // Also verify the message text component exists and is active
                if (_window.m_message == null || !_window.m_message.gameObject.activeInHierarchy)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            bool isActive = IsOpen();

            // Dialog just opened
            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            // Dialog just closed
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            // Dialog is active, check for cursor changes
            else if (isActive)
            {
                _pollCounter++;
                if (_pollCounter >= POLL_INTERVAL)
                {
                    _pollCounter = 0;
                    CheckCursorChange();
                }
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursorIndex = uCommonYesNoWindow.CursorIndex.Yes;
            _pollCounter = 0;

            if (_window == null)
                return;

            DebugLogger.Log("[YesNo] === Dialog Opened ===");

            string message = GetMessage();
            string yesText = GetYesText();
            string noText = GetNoText();
            var currentCursor = GetCursorIndex();

            DebugLogger.Log($"[YesNo] Message: '{message}'");
            DebugLogger.Log($"[YesNo] Yes text: '{yesText}'");
            DebugLogger.Log($"[YesNo] No text: '{noText}'");
            DebugLogger.Log($"[YesNo] Cursor: {currentCursor}");

            // Build announcement
            string announcement = "";
            if (!string.IsNullOrEmpty(message))
            {
                announcement = message + ". ";
            }
            else
            {
                announcement = "Confirmation. ";
            }

            // Add options
            if (!string.IsNullOrEmpty(yesText) && !string.IsNullOrEmpty(noText))
            {
                announcement += $"{yesText} or {noText}. ";
            }

            // Announce current selection
            string currentOption = (currentCursor == uCommonYesNoWindow.CursorIndex.Yes) ? yesText : noText;
            if (!string.IsNullOrEmpty(currentOption))
            {
                announcement += $"Currently on: {currentOption}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[YesNo] Announced: {announcement}");

            _lastCursorIndex = currentCursor;
        }

        private void OnClose()
        {
            _window = null;
            DebugLogger.Log("[YesNo] Dialog closed");
        }

        private void CheckCursorChange()
        {
            if (_window == null)
                return;

            try
            {
                var currentCursor = GetCursorIndex();

                if (currentCursor != _lastCursorIndex)
                {
                    DebugLogger.Log($"[YesNo] Cursor changed: {_lastCursorIndex} -> {currentCursor}");

                    string optionText = (currentCursor == uCommonYesNoWindow.CursorIndex.Yes)
                        ? GetYesText()
                        : GetNoText();

                    if (!string.IsNullOrEmpty(optionText))
                    {
                        ScreenReader.Say(optionText);
                        DebugLogger.Log($"[YesNo] Announced: {optionText}");
                    }

                    _lastCursorIndex = currentCursor;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[YesNo] Error checking cursor: {ex.Message}");
            }
        }

        private string GetMessage()
        {
            if (_window == null || _window.m_message == null)
                return "";
            string text = TextUtilities.CleanText(_window.m_message.text);
            return TextUtilities.FormatItemMessage(text);
        }

        private string GetYesText()
        {
            if (_window == null || _window.m_yes == null)
                return "Yes";
            string text = TextUtilities.CleanText(_window.m_yes.text);
            return string.IsNullOrEmpty(text) ? "Yes" : text;
        }

        private string GetNoText()
        {
            if (_window == null || _window.m_no == null)
                return "No";
            string text = TextUtilities.CleanText(_window.m_no.text);
            return string.IsNullOrEmpty(text) ? "No" : text;
        }

        private uCommonYesNoWindow.CursorIndex GetCursorIndex()
        {
            if (_window == null)
                return uCommonYesNoWindow.CursorIndex.Yes;

            try
            {
                return _window.m_cursorIndex;
            }
            catch
            {
                return uCommonYesNoWindow.CursorIndex.Yes;
            }
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            DebugLogger.Log("[YesNo] === Status Request ===");

            string message = GetMessage();
            string yesText = GetYesText();
            string noText = GetNoText();
            var currentCursor = GetCursorIndex();

            string announcement = "";
            if (!string.IsNullOrEmpty(message))
            {
                announcement = "Confirmation: " + message + ". ";
            }
            else
            {
                announcement = "Confirmation. ";
            }

            announcement += $"{yesText} or {noText}. ";

            string currentOption = (currentCursor == uCommonYesNoWindow.CursorIndex.Yes) ? yesText : noText;
            announcement += $"Currently on: {currentOption}";

            ScreenReader.Say(announcement);
        }
    }
}
