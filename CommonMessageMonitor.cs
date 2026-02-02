using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Monitors uCommonMessageWindow instances for text changes and announces them.
    /// This catches notifications like "Tentomon joined the city!" that don't go
    /// through the normal SetMessage path with proper localization.
    /// </summary>
    public class CommonMessageMonitor
    {
        // Track announced messages to avoid duplicates
        private HashSet<string> _recentlyAnnounced = new HashSet<string>();
        private DateTime _lastCleanup = DateTime.Now;

        // Track last seen text per window instance to detect changes
        private Dictionary<int, string> _lastTextPerWindow = new Dictionary<int, string>();

        public void Update()
        {
            try
            {
                // Cleanup old announced messages periodically
                if ((DateTime.Now - _lastCleanup).TotalSeconds > 5)
                {
                    _recentlyAnnounced.Clear();
                    _lastCleanup = DateTime.Now;
                }

                // Find ALL uCommonMessageWindow instances in the scene
                var commonWindows = UnityEngine.Object.FindObjectsOfType<uCommonMessageWindow>();
                if (commonWindows == null || commonWindows.Length == 0)
                    return;

                // Check each window
                foreach (var window in commonWindows)
                {
                    if (window == null)
                        continue;

                    // Use instance hash code as window identifier
                    int windowId = window.GetHashCode();
                    CheckWindowForNewText(window, windowId);
                }
            }
            catch { }
        }

        private void CheckWindowForNewText(uCommonMessageWindow window, int windowIndex)
        {
            try
            {
                // Check if window is active
                if (window.gameObject == null || !window.gameObject.activeInHierarchy)
                {
                    // Window closed, clear tracking
                    _lastTextPerWindow.Remove(windowIndex);
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

                // Skip if we recently announced this exact text
                if (_recentlyAnnounced.Contains(text))
                    return;

                _recentlyAnnounced.Add(text);

                // Announce the text
                ScreenReader.Say(text);
            }
            catch { }
        }

        private bool ShouldSkipText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            // Skip Japanese placeholder text
            if (text.Contains("メッセージ入力欄"))
                return true;

            // Skip "Language not found" error
            if (text.Contains("ランゲージが見つかりません"))
                return true;

            // Skip Japanese placeholder characters
            if (text.Contains("■") || text.Contains("□"))
                return true;

            // Skip color-tagged warning messages (already announced via SetMessage)
            if (text.StartsWith("<color=#ff0000ff>Warning"))
                return true;

            // Skip if it looks like an unresolved localization key
            if (text.StartsWith("EV_") || text.StartsWith("SYS_") || text.StartsWith("MSG_"))
                return true;

            return false;
        }
    }
}
