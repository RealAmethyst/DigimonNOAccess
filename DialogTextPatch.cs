using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using System;
using System.Collections.Generic;

namespace DigimonNOAccess
{
    /// <summary>
    /// Harmony patch to intercept dialog text at the moment it's set,
    /// before the typewriter animation starts. This gives us the full text
    /// immediately without any delay.
    ///
    /// We patch EventWindowPanel.TextShrink(string text) which receives the
    /// actual localized text ready for display (not the localization key).
    ///
    /// We also patch TalkMain.PlayVoiceText to detect voiced dialog and skip TTS for it.
    /// </summary>
    public static class DialogTextPatch
    {
        // Store the latest intercepted text for announcement
        public static string LatestText { get; private set; } = "";
        public static string LatestName { get; private set; } = "";
        public static bool HasNewText { get; private set; } = false;

        // Event that fires when new text is intercepted
        public static event Action<string, string> OnTextIntercepted;

        // Track last announced text to avoid duplicates
        private static string _lastAnnouncedText = "";

        // Track voiced dialog - these texts will be spoken by the game, so we skip TTS
        private static HashSet<string> _voicedTextKeys = new HashSet<string>();
        private static DateTime _lastVoiceTime = DateTime.MinValue;

        /// <summary>
        /// Mark the text as consumed (after announcement).
        /// </summary>
        public static void ConsumeText()
        {
            HasNewText = false;
        }

        /// <summary>
        /// Apply the Harmony patches.
        /// </summary>
        public static void Apply(HarmonyLib.Harmony harmony)
        {
            try
            {
                // Patch EventWindowPanel.TextShrink - this receives the actual localized text
                var textShrinkMethod = AccessTools.Method(typeof(EventWindowPanel), "TextShrink", new Type[] { typeof(string) });
                if (textShrinkMethod != null)
                {
                    var prefix = AccessTools.Method(typeof(DialogTextPatch), nameof(TextShrinkPrefix));
                    harmony.Patch(textShrinkMethod, prefix: new HarmonyMethod(prefix));
                    DebugLogger.Log("[DialogTextPatch] Patched EventWindowPanel.TextShrink");
                }
                else
                {
                    DebugLogger.Log("[DialogTextPatch] WARNING: Could not find EventWindowPanel.TextShrink method");
                }

                // Patch TalkMain.PlayVoiceText - this is called for voiced dialog
                // We track these to skip TTS since the game will play voice audio
                var playVoiceTextMethod = AccessTools.Method(typeof(TalkMain), "PlayVoiceText",
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
                if (playVoiceTextMethod != null)
                {
                    var voicePrefix = AccessTools.Method(typeof(DialogTextPatch), nameof(PlayVoiceTextPrefix));
                    harmony.Patch(playVoiceTextMethod, prefix: new HarmonyMethod(voicePrefix));
                    DebugLogger.Log("[DialogTextPatch] Patched TalkMain.PlayVoiceText");
                }
                else
                {
                    DebugLogger.Log("[DialogTextPatch] WARNING: Could not find TalkMain.PlayVoiceText method");
                }

                DebugLogger.Log("[DialogTextPatch] Patches applied successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error applying patches: {ex.Message}");
                DebugLogger.Log($"[DialogTextPatch] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Prefix patch for TalkMain.PlayVoiceText - marks dialog as voiced so we skip TTS
        /// </summary>
        private static void PlayVoiceTextPrefix(string name, string text, string id)
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    // Store the localization key - voiced dialog will use this
                    _voicedTextKeys.Add(text);
                    _lastVoiceTime = DateTime.Now;
                    DebugLogger.Log($"[DialogTextPatch] PlayVoiceText: name='{name}', key='{text}', id='{id}' - marking as voiced");

                    // Clean up old entries (keep only recent ones)
                    if (_voicedTextKeys.Count > 20)
                    {
                        _voicedTextKeys.Clear();
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in PlayVoiceTextPrefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if text was recently marked as voiced (has voice audio playing)
        /// </summary>
        private static bool IsVoicedDialog()
        {
            // If voice was triggered within the last 500ms, consider this voiced dialog
            return (DateTime.Now - _lastVoiceTime).TotalMilliseconds < 500;
        }

        /// <summary>
        /// Prefix patch for EventWindowPanel.TextShrink(string text)
        /// This method receives the actual localized text ready for display.
        /// We read the speaker name from the panel's m_nameText component.
        /// </summary>
        private static void TextShrinkPrefix(EventWindowPanel __instance, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return;

                // Skip if we already announced this exact text (avoid duplicates)
                if (text == _lastAnnouncedText)
                    return;

                _lastAnnouncedText = text;

                DebugLogger.Log($"[DialogTextPatch] TextShrink intercepted: text='{TruncateForLog(text)}'");

                // Check if this is voiced dialog - skip TTS since game will play voice
                if (IsVoicedDialog())
                {
                    DebugLogger.Log($"[DialogTextPatch] Skipping TTS - voiced dialog detected");
                    return;
                }

                // Get the speaker name from the panel's name text component
                string speakerName = "";
                try
                {
                    if (__instance != null && __instance.m_nameText != null)
                    {
                        speakerName = __instance.m_nameText.text ?? "";
                        DebugLogger.Log($"[DialogTextPatch] Speaker name from panel: '{speakerName}'");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[DialogTextPatch] Error reading speaker name: {ex.Message}");
                }

                // Skip placeholder or system text
                if (IsPlaceholderText(text))
                {
                    DebugLogger.Log($"[DialogTextPatch] Skipping placeholder text");
                    return;
                }

                LatestName = speakerName;
                LatestText = text;
                HasNewText = true;

                DebugLogger.Log($"[DialogTextPatch] Announcing: name='{speakerName}', text='{TruncateForLog(text)}'");

                // Fire the event so MessageWindowHandler can announce immediately
                OnTextIntercepted?.Invoke(speakerName, text);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in TextShrinkPrefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if the text is a placeholder or system text that shouldn't be announced.
        /// </summary>
        private static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            // Skip Japanese placeholder characters
            if (text.Contains("■") || text.Contains("□"))
                return true;

            // Skip if it looks like an unresolved localization key
            if (text.StartsWith("EV_") || text.StartsWith("SYS_") || text.StartsWith("MSG_"))
                return true;

            // Skip text that is only punctuation (like "!", "?!", "?!?!", "...", etc.)
            // These are reaction expressions that don't need to be announced
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

        private static string TruncateForLog(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\n", " ").Replace("\r", "");
            return text.Length > 60 ? text.Substring(0, 60) + "..." : text;
        }
    }
}
