using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle dialog accessibility (escape confirmation, etc.).
    /// Announces dialog messages and Yes/No selection.
    /// </summary>
    public class BattleDialogHandler
    {
        private uBattlePanelDialog _cachedDialog;
        private int _lastCursorIndex = -1;
        private bool _wasActive = false;
        private string _lastMessage = "";

        public void Update()
        {
            // Check if battle is active first
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                ResetState();
                return;
            }

            // Find the dialog
            var dialog = Object.FindObjectOfType<uBattlePanelDialog>();
            if (dialog == null || !dialog.gameObject.activeInHierarchy)
            {
                if (_wasActive)
                {
                    ResetState();
                }
                return;
            }

            _cachedDialog = dialog;

            // Dialog just opened
            if (!_wasActive)
            {
                _wasActive = true;
                _lastCursorIndex = dialog.m_cursorIndex;
                _lastMessage = dialog.m_messageText?.text ?? "";
                AnnounceDialog();
                return;
            }

            // Check for cursor change
            if (dialog.m_cursorIndex != _lastCursorIndex)
            {
                _lastCursorIndex = dialog.m_cursorIndex;
                AnnounceSelection();
            }

            // Check for message change (shouldn't happen but just in case)
            string currentMessage = dialog.m_messageText?.text ?? "";
            if (currentMessage != _lastMessage && !string.IsNullOrEmpty(currentMessage))
            {
                _lastMessage = currentMessage;
                AnnounceDialog();
            }
        }

        private void ResetState()
        {
            _cachedDialog = null;
            _lastCursorIndex = -1;
            _wasActive = false;
            _lastMessage = "";
        }

        private void AnnounceDialog()
        {
            if (_cachedDialog == null)
                return;

            string message = _cachedDialog.m_messageText?.text ?? "Confirm?";
            string selection = GetSelectionName(_cachedDialog.m_cursorIndex);

            // Clean up message text (remove rich text tags if any)
            message = CleanText(message);

            ScreenReader.Say($"{message} {selection}");
        }

        private void AnnounceSelection()
        {
            if (_cachedDialog == null)
                return;

            string selection = GetSelectionName(_cachedDialog.m_cursorIndex);
            ScreenReader.Say(selection);
        }

        private string GetSelectionName(int cursorIndex)
        {
            return cursorIndex == 0 ? "Yes" : "No";
        }

        private string CleanText(string text)
        {
            return TextUtilities.CleanText(text);
        }

        public bool IsActive()
        {
            return _wasActive && _cachedDialog != null;
        }
    }
}
