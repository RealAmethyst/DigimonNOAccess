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
            // Replace <icon>...</icon> with readable button names based on active input device
            text = Regex.Replace(text, @"<icon>([^<]*)</icon>", m => ButtonIconResolver.ResolveIconTag(m.Groups[1].Value));
            // Strip remaining rich text tags
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

            // Replace <icon>...</icon> with readable button names before stripping other tags
            string cleaned = Regex.Replace(text, @"<icon>([^<]*)</icon>", m => ButtonIconResolver.ResolveIconTag(m.Groups[1].Value));
            // Remove remaining Unity rich text tags
            cleaned = Regex.Replace(cleaned, @"<[^>]+>", "");
            // Normalize whitespace
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
        }

        /// <summary>
        /// Reformats game item messages for more natural screen reader output.
        /// Handles multiple game formats:
        /// - "Duty Fruit x 1 received." → "Received 1 Duty Fruit."
        /// - "You got Medicine x 2!" → "You got 2 Medicine!"
        /// </summary>
        public static string FormatItemMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Match "ItemName x N received." pattern (field pickups)
            var match = Regex.Match(text, @"^(.+?)\s+x\s+(\d+)\s+received\.\s*$");
            if (match.Success)
            {
                string itemName = match.Groups[1].Value.Trim();
                string count = match.Groups[2].Value;
                return $"Received {count} {itemName}.";
            }

            // Match "You got ItemName x N!" pattern (NPC gifts)
            match = Regex.Match(text, @"^(You got)\s+(.+?)\s+x\s+(\d+)!\s*$");
            if (match.Success)
            {
                string prefix = match.Groups[1].Value;
                string itemName = match.Groups[2].Value.Trim();
                string count = match.Groups[3].Value;
                return $"{prefix} {count} {itemName}!";
            }

            // General: swap any remaining "ItemName x N" to "N ItemName"
            // Handles patterns like "Recovery Disk x 7 will all be discarded."
            if (text.Contains(" x "))
                return Regex.Replace(text, @"([\w'-]+(?:\s+[\w'-]+)*?)\s+x\s+(\d+)", "$2 $1");

            return text;
        }

        /// <summary>
        /// Check if text is a placeholder (localization keys, placeholder chars, system messages).
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
        /// Returns true only when MainGameManager exists and _IsLoad() is true.
        /// Returns false when MainGameManager is null (title/copyright screen)
        /// so that pre-title text can be announced. Other filters (IsPlaceholderText,
        /// IsLocalizationReady) handle garbage text at that stage.
        /// </summary>
        public static bool IsGameLoading()
        {
            try
            {
                var mgr = MainGameManager.m_instance;
                if (mgr == null)
                    return false;
                return mgr._IsLoad();
            }
            catch
            {
                return false;
            }
        }
    }
}
