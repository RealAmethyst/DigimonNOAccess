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
    /// We patch:
    /// - EventWindowPanel.TextShrink(string text) - story dialog text
    /// - TalkMain.PlayVoiceText - to detect voiced dialog and skip TTS
    /// - uCommonMessageWindow.SetMessage - field notifications (recruitment, etc.)
    /// - uDigimonMessagePanel.StartMessage - Digimon-specific field messages (recruitment notifications)
    /// - uFieldPanel.StartDigimonMessage - static method for field Digimon messages
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

        // Toggle: When true, read ALL text including voiced dialog (for non-English voice users)
        public static bool AlwaysReadText { get; set; } = false;

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

                // Patch uCommonMessageWindow.SetMessage - this is used for field notifications
                // like "Digimon joined the city" after NPC dialog
                var setMessageMethod = AccessTools.Method(typeof(uCommonMessageWindow), "SetMessage",
                    new Type[] { typeof(string), typeof(uCommonMessageWindow.Pos) });
                if (setMessageMethod != null)
                {
                    var msgPrefix = AccessTools.Method(typeof(DialogTextPatch), nameof(SetMessagePrefix));
                    harmony.Patch(setMessageMethod, prefix: new HarmonyMethod(msgPrefix));
                    DebugLogger.Log("[DialogTextPatch] Patched uCommonMessageWindow.SetMessage");
                }
                else
                {
                    DebugLogger.Log("[DialogTextPatch] WARNING: Could not find uCommonMessageWindow.SetMessage method");
                }

                // Patch uDigimonMessagePanel.StartMessage - this is used for Digimon-specific field messages
                // like recruitment notifications after talking to NPC Digimon
                var digimonMsgMethod = AccessTools.Method(typeof(uDigimonMessagePanel), "StartMessage",
                    new Type[] { typeof(string), typeof(float) });
                if (digimonMsgMethod != null)
                {
                    var digimonMsgPrefix = AccessTools.Method(typeof(DialogTextPatch), nameof(DigimonMessagePrefix));
                    harmony.Patch(digimonMsgMethod, prefix: new HarmonyMethod(digimonMsgPrefix));
                    DebugLogger.Log("[DialogTextPatch] Patched uDigimonMessagePanel.StartMessage");
                }
                else
                {
                    DebugLogger.Log("[DialogTextPatch] WARNING: Could not find uDigimonMessagePanel.StartMessage method");
                }

                // Patch uFieldPanel.StartDigimonMessage - static method for field Digimon messages
                // This is called with UNITID, message string, and display time
                var fieldDigimonMsgMethod = AccessTools.Method(typeof(uFieldPanel), "StartDigimonMessage",
                    new Type[] { typeof(MainGameManager.UNITID), typeof(string), typeof(float) });
                if (fieldDigimonMsgMethod != null)
                {
                    var fieldDigimonMsgPrefix = AccessTools.Method(typeof(DialogTextPatch), nameof(FieldDigimonMessagePrefix));
                    harmony.Patch(fieldDigimonMsgMethod, prefix: new HarmonyMethod(fieldDigimonMsgPrefix));
                    DebugLogger.Log("[DialogTextPatch] Patched uFieldPanel.StartDigimonMessage");
                }
                else
                {
                    DebugLogger.Log("[DialogTextPatch] WARNING: Could not find uFieldPanel.StartDigimonMessage method");
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

        // Track last common message to avoid duplicates
        private static string _lastCommonMessage = "";
        private static DateTime _lastCommonMessageTime = DateTime.MinValue;

        // Event for common message window notifications
        public static event Action<string> OnCommonMessageIntercepted;

        /// <summary>
        /// Prefix patch for uCommonMessageWindow.SetMessage(string str, Pos window_pos)
        /// This catches field notifications like "Digimon joined the city"
        /// </summary>
        private static void SetMessagePrefix(uCommonMessageWindow __instance, string str, uCommonMessageWindow.Pos window_pos)
        {
            try
            {
                if (string.IsNullOrEmpty(str))
                    return;

                // Skip if we just announced this (avoid duplicates within 500ms)
                if (str == _lastCommonMessage && (DateTime.Now - _lastCommonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastCommonMessage = str;
                _lastCommonMessageTime = DateTime.Now;

                DebugLogger.Log($"[DialogTextPatch] SetMessage intercepted: pos={window_pos}, text='{TruncateForLog(str)}'");

                // Skip "Language not found" error - the real text will be caught by CommonMessageMonitor
                if (str.Contains("ランゲージが見つかりません"))
                {
                    DebugLogger.Log($"[DialogTextPatch] Skipping 'Language not found' error - monitor will catch real text");
                    return;
                }

                // Skip placeholder or system text
                if (IsPlaceholderText(str))
                {
                    DebugLogger.Log($"[DialogTextPatch] Skipping placeholder text in SetMessage");
                    return;
                }

                // Announce directly via ScreenReader
                ScreenReader.Say(str);
                DebugLogger.Log($"[DialogTextPatch] Announced common message: '{TruncateForLog(str)}'");

                // Fire event for any handlers that want to track this
                OnCommonMessageIntercepted?.Invoke(str);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in SetMessagePrefix: {ex.Message}");
            }
        }

        // Track last Digimon message to avoid duplicates
        private static string _lastDigimonMessage = "";
        private static DateTime _lastDigimonMessageTime = DateTime.MinValue;

        /// <summary>
        /// Prefix patch for uDigimonMessagePanel.StartMessage(string message, float time)
        /// This catches Digimon-specific field messages like recruitment notifications
        /// </summary>
        private static void DigimonMessagePrefix(uDigimonMessagePanel __instance, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                // Skip if we just announced this (avoid duplicates within 500ms)
                if (message == _lastDigimonMessage && (DateTime.Now - _lastDigimonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastDigimonMessage = message;
                _lastDigimonMessageTime = DateTime.Now;

                DebugLogger.Log($"[DialogTextPatch] DigimonMessage intercepted: time={time}, text='{TruncateForLog(message)}'");

                // Skip placeholder or system text
                if (IsPlaceholderText(message))
                {
                    DebugLogger.Log($"[DialogTextPatch] Skipping placeholder text in DigimonMessage");
                    return;
                }

                // Announce directly via ScreenReader
                ScreenReader.Say(message);
                DebugLogger.Log($"[DialogTextPatch] Announced Digimon message: '{TruncateForLog(message)}'");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in DigimonMessagePrefix: {ex.Message}");
            }
        }

        // Track last field Digimon message to avoid duplicates
        private static string _lastFieldDigimonMessage = "";
        private static DateTime _lastFieldDigimonMessageTime = DateTime.MinValue;

        /// <summary>
        /// Prefix patch for uFieldPanel.StartDigimonMessage(UNITID id, string message, float time)
        /// This is a static method that triggers Digimon-specific field messages
        /// </summary>
        private static void FieldDigimonMessagePrefix(MainGameManager.UNITID id, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                // Skip if we just announced this (avoid duplicates within 500ms)
                if (message == _lastFieldDigimonMessage && (DateTime.Now - _lastFieldDigimonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastFieldDigimonMessage = message;
                _lastFieldDigimonMessageTime = DateTime.Now;

                DebugLogger.Log($"[DialogTextPatch] FieldDigimonMessage intercepted: id={id}, time={time}, text='{TruncateForLog(message)}'");

                // Skip placeholder or system text
                if (IsPlaceholderText(message))
                {
                    DebugLogger.Log($"[DialogTextPatch] Skipping placeholder text in FieldDigimonMessage");
                    return;
                }

                // Announce directly via ScreenReader
                ScreenReader.Say(message);
                DebugLogger.Log($"[DialogTextPatch] Announced field Digimon message: '{TruncateForLog(message)}'");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in FieldDigimonMessagePrefix: {ex.Message}");
            }
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
                // Unless AlwaysReadText is enabled (for users playing with non-English voice)
                if (IsVoicedDialog() && !AlwaysReadText)
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
