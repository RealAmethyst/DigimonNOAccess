using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the title/main menu.
    /// Uses m_playVoice to detect when menu voice has played - this indicates the menu
    /// is truly ready for input (after "press any button" phase completes).
    /// Also checks MainTitle.m_State == Idle to ensure no dialogs/loading are active.
    /// Uses Localization.isActive to ensure text is ready before reading.
    /// </summary>
    public class TitleMenuHandler
    {
        private uTitlePanel _titlePanel;
        private MainTitle _mainTitle;
        private bool _wasUsable = false;
        private int _lastCursor = -1;
        private bool _voiceHasFinished = false;
        private bool _wasOpen = false;
        private bool _wasPlayingVoice = false;

        /// <summary>
        /// Check if title menu panel exists and is ready for input.
        /// Waits for m_playVoice to go from True back to False (voice finished playing).
        /// </summary>
        private bool IsPanelReady()
        {
            if (_titlePanel == null)
            {
                _titlePanel = Object.FindObjectOfType<uTitlePanel>();
            }

            if (_titlePanel == null)
            {
                _voiceHasFinished = false;
                _wasOpen = false;
                _wasPlayingVoice = false;
                return false;
            }

            bool isOpen = _titlePanel.m_isOpend;
            bool playVoice = _titlePanel.m_playVoice;

            // Reset flags when panel freshly opens
            if (isOpen && !_wasOpen)
            {
                _voiceHasFinished = false;
                _wasPlayingVoice = false;
                DebugLogger.Log($"[TitleMenu] Panel opened, resetting voice tracking. playVoice={playVoice}");
            }
            _wasOpen = isOpen;

            if (!isOpen)
            {
                return false;
            }

            // Track voice state transitions: True -> False means voice finished
            if (playVoice && !_wasPlayingVoice)
            {
                // Voice just started playing
                _wasPlayingVoice = true;
                DebugLogger.Log("[TitleMenu] Voice started playing");
            }
            else if (!playVoice && _wasPlayingVoice && !_voiceHasFinished)
            {
                // Voice just finished (was playing, now stopped)
                _voiceHasFinished = true;
                DebugLogger.Log("[TitleMenu] Voice finished playing, menu now ready");
            }

            return _voiceHasFinished;
        }

        /// <summary>
        /// Check if title menu is actually usable (can accept input).
        /// Panel must be ready (voice has played) AND MainTitle must be in Idle state.
        /// </summary>
        public bool IsUsable()
        {
            if (!IsPanelReady())
                return false;

            // Find MainTitle if not cached
            if (_mainTitle == null)
            {
                _mainTitle = Object.FindObjectOfType<MainTitle>();
            }

            if (_mainTitle == null)
                return false;

            // Menu is only truly usable when MainTitle state is Idle
            // Other states: LoadWait, ErrorMsg, Option, SelectDifficulty, etc.
            return _mainTitle.m_State == MainTitle.State.Idle;
        }

        /// <summary>
        /// Check if localization is loaded and ready.
        /// </summary>
        private bool IsLocalizationReady()
        {
            try
            {
                return Localization.isActive;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called every frame to track menu state.
        /// </summary>
        public void Update()
        {
            bool isUsable = IsUsable();

            // Menu just became usable
            if (isUsable && !_wasUsable)
            {
                OnBecameUsable();
            }
            // Menu just became unusable
            else if (!isUsable && _wasUsable)
            {
                OnBecameUnusable();
            }
            // Menu is usable, check for cursor changes
            else if (isUsable)
            {
                CheckCursorChange();
            }

            _wasUsable = isUsable;
        }

        private void OnBecameUsable()
        {
            _lastCursor = -1; // Reset to force announcement

            if (_titlePanel == null)
                return;

            // Wait for localization to be ready
            if (!IsLocalizationReady())
            {
                DebugLogger.Log("[TitleMenu] Menu usable but localization not ready yet");
                return;
            }

            int cursor = _titlePanel.cursorPosition;
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Title Menu. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[TitleMenu] Menu now usable (Idle state), cursor={cursor}, text={itemText}");
            _lastCursor = cursor;
        }

        private void OnBecameUnusable()
        {
            _titlePanel = null;
            _mainTitle = null;
            _lastCursor = -1;
            _voiceHasFinished = false;
            _wasOpen = false;
            _wasPlayingVoice = false;
            DebugLogger.Log("[TitleMenu] Menu no longer usable");
        }

        private void CheckCursorChange()
        {
            if (_titlePanel == null)
                return;

            // Don't announce if localization isn't ready
            if (!IsLocalizationReady())
                return;

            int cursor = _titlePanel.cursorPosition;

            if (cursor != _lastCursor)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = $"{itemText}, {cursor + 1} of {total}";
                ScreenReader.Say(announcement);

                DebugLogger.Log($"[TitleMenu] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private string GetMenuItemText(int index)
        {
            if (_titlePanel == null)
                return GetFallbackText(index);

            // Only try to read from UI if localization is ready
            if (!IsLocalizationReady())
                return GetFallbackText(index);

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
                DebugLogger.Log($"[TitleMenu] Error reading text: {ex.Message}");
            }

            return GetFallbackText(index);
        }

        private string GetFallbackText(int index)
        {
            // Fallback to known menu items
            return index switch
            {
                0 => "New Game",
                1 => "Load Game",
                2 => "System Settings",
                3 => "Quit Game",
                _ => $"Item {index + 1}"
            };
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
            if (!IsUsable())
                return;

            if (!IsLocalizationReady())
                return;

            int cursor = _titlePanel.cursorPosition;
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Title Menu. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }

        /// <summary>
        /// Check if title menu is currently open (for external callers).
        /// Returns true only when menu is actually usable.
        /// </summary>
        public bool IsOpen()
        {
            return IsUsable();
        }
    }
}
