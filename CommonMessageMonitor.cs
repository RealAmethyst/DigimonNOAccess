using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Monitors specific uCommonMessageWindow slots for text changes and announces them.
    /// Uses AppMainScript.MessageManager to target only gameplay-relevant windows
    /// (Center for notifications, partner windows for partner messages) and ignores
    /// other windows (save panel, system messages) that shouldn't be announced.
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

        /// <summary>
        /// Collect the specific message windows we want to monitor.
        /// Uses CommonMessageWindowManager slots rather than FindObjectsOfType
        /// to avoid picking up unrelated windows (save panel, system messages).
        /// To monitor additional windows, add them here.
        /// </summary>
        private List<uCommonMessageWindow> GetMonitoredWindows()
        {
            var windows = new List<uCommonMessageWindow>();

            try
            {
                var app = AppMainScript.m_instance;
                if (app == null) return windows;

                var manager = app.MessageManager;
                if (manager == null) return windows;

                // Center window: main gameplay notifications (item rewards, recruitment, etc.)
                var center = manager.GetCenter();
                if (center != null)
                    windows.Add(center);

                // Add more windows here if needed, e.g.:
                // var partnerL = manager.Get01();
                // if (partnerL != null) windows.Add(partnerL);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CommonMessageMonitor] Error getting managed windows: {ex.Message}");
            }

            return windows;
        }

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

                // Monitor only specific gameplay windows from the message manager
                var windows = GetMonitoredWindows();

                foreach (var window in windows)
                {
                    int windowId = window.GetHashCode();
                    CheckWindowForNewText(window, windowId);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CommonMessageMonitor] Error in Update: {ex.Message}");
            }
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
