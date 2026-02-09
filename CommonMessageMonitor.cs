using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Monitors specific uCommonMessageWindow slots for text changes and announces them.
    /// Uses AppMainScript.MessageManager.GetCenter() for general gameplay notifications
    /// and TalkMain.m_common_message_window for NPC conversation messages (item rewards, etc.).
    /// Avoids FindObjectsOfType to prevent picking up unrelated windows (save panel, system messages).
    /// </summary>
    public class CommonMessageMonitor : IAccessibilityHandler
    {
        public int Priority => 48;

        /// <summary>
        /// CommonMessageMonitor runs in the background and never "owns" status.
        /// </summary>
        public bool IsOpen() => false;

        /// <summary>
        /// No-op: this monitor doesn't announce status via the priority chain.
        /// </summary>
        public void AnnounceStatus() { }

        // Track last seen text per window instance to detect changes
        // When window closes, tracking is cleared, allowing re-announcement when reopened
        private Dictionary<int, string> _lastTextPerWindow = new Dictionary<int, string>();
        // Track if window has already announced its first message (use SayQueued for subsequent)
        private HashSet<int> _windowHasAnnounced = new HashSet<int>();
        // Persistent tracking of announced text to prevent re-announcing stale messages
        // after returning to field from battles/events. Cleared only on map change.
        private string _lastAnnouncedText = "";

        // Dedicated window ID for TalkMain's message window (avoids collision with managed windows)
        private const int TalkMainWindowId = -1000;

        public void Update()
        {
            try
            {
                // Skip if localization isn't ready (avoids placeholder text)
                if (!IsLocalizationReady())
                    return;

                // Skip if game is still loading
                if (IsGameLoading())
                    return;

                // Monitor the center gameplay window from the message manager
                try
                {
                    var app = AppMainScript.m_instance;
                    if (app != null)
                    {
                        var manager = app.MessageManager;
                        if (manager != null)
                        {
                            var center = manager.GetCenter();
                            if (center != null)
                                CheckWindowForNewText(center, center.GetHashCode());
                        }
                    }
                }
                catch { }

                // Monitor TalkMain's message window for NPC conversation messages
                // (item notifications, recruitment, etc. that use a non-center window)
                CheckTalkMainWindow();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CommonMessageMonitor] Error in Update: {ex.Message}");
            }
        }

        /// <summary>
        /// Check TalkMain's m_common_message_window for NPC conversation messages.
        /// This catches item notifications during NPC talks (e.g. Koromon giving items)
        /// without picking up save system or other unrelated windows.
        /// </summary>
        private void CheckTalkMainWindow()
        {
            try
            {
                var talkMains = UnityEngine.Object.FindObjectsOfType<TalkMain>();
                if (talkMains == null) return;

                bool foundActiveWindow = false;
                foreach (var talk in talkMains)
                {
                    if (talk == null) continue;

                    var window = talk.m_common_message_window;
                    if (window == null) continue;

                    // Use a dedicated ID so it doesn't collide with managed window tracking
                    CheckWindowForNewText(window, TalkMainWindowId);
                    foundActiveWindow = true;
                    break;
                }

                // If no TalkMain window found, clear its tracking
                if (!foundActiveWindow)
                {
                    _lastTextPerWindow.Remove(TalkMainWindowId);
                    _windowHasAnnounced.Remove(TalkMainWindowId);
                }
            }
            catch { }
        }

        private void CheckWindowForNewText(uCommonMessageWindow window, int windowIndex)
        {
            try
            {
                // Check if window is visible using the game's own m_isOpend flag
                // (set by enablePanel). Don't use activeInHierarchy as fallback -
                // closed windows can remain in hierarchy with stale label text.
                bool isVisible = window.m_isOpend;

                if (!isVisible)
                {
                    // Window not opened, clear tracking
                    _lastTextPerWindow.Remove(windowIndex);
                    _windowHasAnnounced.Remove(windowIndex);
                    return;
                }

                // Get the text from the label
                var label = window.m_label;
                if (label == null)
                    return;

                string text = label.text;
                if (string.IsNullOrEmpty(text))
                    return;

                // Check if text changed for this window
                _lastTextPerWindow.TryGetValue(windowIndex, out string lastText);
                if (text == lastText)
                    return;

                _lastTextPerWindow[windowIndex] = text;

                // Filter out placeholder/garbage text
                if (ShouldSkipText(text))
                    return;

                // Skip if DialogTextPatch already announced this text via SetMessage patch
                if (DialogTextPatch.WasRecentlyAnnounced(text))
                {
                    DebugLogger.Log($"[CommonMessageMonitor] Skipping duplicate (already announced by SetMessage): {text}");
                    return;
                }

                // Skip stale text that was already announced by this monitor
                // (prevents re-announcing old "X received" text after returning from battles)
                string cleanText = DialogTextPatch.StripRichTextTags(text).Trim();
                if (cleanText == _lastAnnouncedText)
                {
                    DebugLogger.Log($"[CommonMessageMonitor] Skipping stale text: {text}");
                    return;
                }

                // Announce the text
                DebugLogger.Log($"[CommonMessageMonitor] {text}");
                _lastAnnouncedText = cleanText;

                // Reformat item messages for cleaner output
                string announcement = TextUtilities.FormatItemMessage(cleanText);

                // First message for this window uses Say(), subsequent use SayQueued()
                if (_windowHasAnnounced.Contains(windowIndex))
                {
                    ScreenReader.SayQueued(announcement);
                }
                else
                {
                    ScreenReader.Say(announcement);
                    _windowHasAnnounced.Add(windowIndex);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CommonMessageMonitor] Error in CheckWindowForNewText: {ex.Message}");
            }
        }

        private bool IsLocalizationReady()
        {
            return TextUtilities.IsLocalizationReady();
        }

        private bool IsGameLoading()
        {
            return TextUtilities.IsGameLoading();
        }

        // System/copyright text that should never be re-announced during gameplay.
        // The initial startup announcement is handled by SetMessagePrefix in DialogTextPatch.
        private static readonly string[] _systemTextPatterns = new string[]
        {
            "Warning",
            "Transmitting",
            "prohibited",
            "\u00a9",           // © copyright symbol
            "BANDAI NAMCO",
            "Saving system save data",
        };

        private bool ShouldSkipText(string text)
        {
            if (TextUtilities.IsPlaceholderText(text))
                return true;

            foreach (var pattern in _systemTextPatterns)
            {
                if (text.Contains(pattern))
                    return true;
            }

            return false;
        }
    }
}
