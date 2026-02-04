using System.Text.RegularExpressions;
using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Centralized text utility methods shared across all handlers.
    /// Provides rich text stripping, text cleaning, placeholder detection,
    /// localization readiness checks, and game loading state checks.
    /// </summary>
    public static class TextUtilities
    {
        /// <summary>
        /// Strips Unity rich text tags like &lt;color&gt;, &lt;b&gt;, etc. for clean screen reader output.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return Regex.Replace(text, @"<[^>]+>", "");
        }

        /// <summary>
        /// Strip rich text tags, normalize whitespace, and trim.
        /// Use this for any text that will be announced via screen reader.
        /// </summary>
        public static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // Remove Unity rich text tags
            string cleaned = Regex.Replace(text, @"<[^>]+>", "");
            // Normalize whitespace
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
        }

        /// <summary>
        /// Check if text is a placeholder (localization keys, placeholder chars, system messages).
        /// This is a superset of all checks from DialogTextPatch.IsPlaceholderText
        /// and CommonMessageMonitor.ShouldSkipText.
        /// </summary>
        public static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            // Skip Japanese placeholder characters (black/white squares)
            if (text.Contains("■") || text.Contains("□"))
                return true;

            // Skip unresolved localization keys
            if (text.StartsWith("EV_") || text.StartsWith("SYS_") || text.StartsWith("MSG_"))
                return true;

            // Skip "Language not found" error messages (Japanese)
            if (text.Contains("ランゲージ"))
                return true;

            // Skip Japanese placeholder text "メッセージ入力欄" (Message input field)
            if (text.Contains("メッセージ入力欄"))
                return true;

            // Skip color-tagged warning messages (already announced via SetMessage)
            if (text.StartsWith("<color=#ff0000ff>Warning"))
                return true;

            // Skip punctuation-only text
            string trimmed = text.Trim();
            bool onlyPunctuation = true;
            foreach (char c in trimmed)
            {
                if (!char.IsPunctuation(c) && !char.IsWhiteSpace(c))
                {
                    onlyPunctuation = false;
                    break;
                }
            }
            if (onlyPunctuation && trimmed.Length > 0)
                return true;

            return false;
        }

        /// <summary>
        /// Check if game localization system is ready.
        /// Returns false if Localization is not yet active or on any exception.
        /// </summary>
        public static bool IsLocalizationReady()
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
        /// Check if game is in a loading state.
        /// Returns true if MainGameManager is null (title screen) or _IsLoad() is true.
        /// </summary>
        public static bool IsGameLoading()
        {
            try
            {
                var mgr = MainGameManager.m_instance;
                // If MainGameManager doesn't exist yet, we're probably at title screen
                // Skip monitoring during this phase
                if (mgr == null)
                    return true;
                return mgr._IsLoad();
            }
            catch
            {
                // On exception, assume loading to be safe
                return true;
            }
        }
    }
}
