using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the title/main menu.
    /// </summary>
    public class TitleMenuHandler
    {
        private uTitlePanel _titlePanel;
        private bool _wasActive = false;
        private int _lastCursor = -1;

        /// <summary>
        /// Check if title menu is currently open.
        /// </summary>
        public bool IsOpen()
        {
            if (_titlePanel == null)
            {
                _titlePanel = Object.FindObjectOfType<uTitlePanel>();
            }

            return _titlePanel != null &&
                   _titlePanel.gameObject != null &&
                   _titlePanel.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Called every frame to track menu state.
        /// </summary>
        public void Update()
        {
            bool isActive = IsOpen();

            // Menu just opened
            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            // Menu just closed
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            // Menu is active, check for cursor changes
            else if (isActive)
            {
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1; // Reset to force announcement

            if (_titlePanel == null)
                return;

            int cursor = _titlePanel.cursorPosition;
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Title Menu. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            Melon<Main>.Logger.Msg($"[TitleMenu] Menu opened, cursor={cursor}");
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _titlePanel = null;
            _lastCursor = -1;
            Melon<Main>.Logger.Msg("[TitleMenu] Menu closed");
        }

        private void CheckCursorChange()
        {
            if (_titlePanel == null)
                return;

            int cursor = _titlePanel.cursorPosition;

            if (cursor != _lastCursor)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = $"{itemText}, {cursor + 1} of {total}";
                ScreenReader.Say(announcement);

                Melon<Main>.Logger.Msg($"[TitleMenu] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private string GetMenuItemText(int index)
        {
            if (_titlePanel == null)
                return $"Item {index + 1}";

            try
            {
                var textArray = _titlePanel.m_Text;
                if (textArray != null && index >= 0 && index < textArray.Length)
                {
                    var textComponent = textArray[index];
                    if (textComponent != null && !string.IsNullOrEmpty(textComponent.text))
                    {
                        return textComponent.text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Melon<Main>.Logger.Warning($"[TitleMenu] Error reading text: {ex.Message}");
            }

            // Fallback to known menu items
            switch (index)
            {
                case 0: return "New Game";
                case 1: return "Load Game";
                case 2: return "System Settings";
                case 3: return "Quit Game";
                default: return $"Item {index + 1}";
            }
        }

        private int GetMenuItemCount()
        {
            // Title menu has 4 items: New Game, Load Game, System Settings, Quit
            return 4;
        }

        /// <summary>
        /// Announce current menu state (for repeat key).
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = _titlePanel.cursorPosition;
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Title Menu. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
