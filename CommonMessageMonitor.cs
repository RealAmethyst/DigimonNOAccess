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
        // Track last seen text per window instance to detect changes
        // When window closes, tracking is cleared, allowing re-announcement when reopened
        private Dictionary<int, string> _lastTextPerWindow = new Dictionary<int, string>();

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
                // Check if window is visible - use m_isOpend OR activeInHierarchy with text
                // Some windows may have text set before m_isOpend is true
                bool isVisible = window.m_isOpend;
                if (!isVisible && window.gameObject != null && window.gameObject.activeInHierarchy)
                {
                    // Fallback: check if there's actually text in the label
                    if (window.m_label != null && !string.IsNullOrEmpty(window.m_label.text))
                        isVisible = true;
                }

                if (!isVisible)
                {
                    // Window not opened, clear tracking
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

                // Announce the text (strip rich text tags for clean screen reader output)
                DebugLogger.Log($"[CommonMessageMonitor] {text}");
                ScreenReader.Say(DialogTextPatch.StripRichTextTags(text));
            }
            catch { }
        }

        private bool IsLocalizationReady()
        {
            return TextUtilities.IsLocalizationReady();
        }

        private bool IsGameLoading()
        {
            return TextUtilities.IsGameLoading();
        }

        private bool ShouldSkipText(string text)
        {
            return TextUtilities.IsPlaceholderText(text);
        }
    }
}
