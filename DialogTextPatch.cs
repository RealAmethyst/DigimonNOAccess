using HarmonyLib;
using Il2Cpp;
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
    /// - uDigimonMessagePanel.StartMessage - Digimon-specific field messages
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

        // Event for common message window notifications
        public static event Action<string> OnCommonMessageIntercepted;

        // Track last messages to avoid duplicates
        private static string _lastCommonMessage = "";
        private static DateTime _lastCommonMessageTime = DateTime.MinValue;
        private static string _lastDigimonMessage = "";
        private static DateTime _lastDigimonMessageTime = DateTime.MinValue;
        private static string _lastFieldDigimonMessage = "";
        private static DateTime _lastFieldDigimonMessageTime = DateTime.MinValue;

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
                // Patch EventWindowPanel.TextShrink
                var textShrinkMethod = AccessTools.Method(typeof(EventWindowPanel), "TextShrink", new Type[] { typeof(string) });
                if (textShrinkMethod != null)
                {
                    harmony.Patch(textShrinkMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(TextShrinkPrefix))));
                }

                // Patch TalkMain.PlayVoiceText
                var playVoiceTextMethod = AccessTools.Method(typeof(TalkMain), "PlayVoiceText",
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
                if (playVoiceTextMethod != null)
                {
                    harmony.Patch(playVoiceTextMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(PlayVoiceTextPrefix))));
                }

                // Patch uCommonMessageWindow.SetMessage
                var setMessageMethod = AccessTools.Method(typeof(uCommonMessageWindow), "SetMessage",
                    new Type[] { typeof(string), typeof(uCommonMessageWindow.Pos) });
                if (setMessageMethod != null)
                {
                    harmony.Patch(setMessageMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(SetMessagePrefix))));
                }

                // Patch uDigimonMessagePanel.StartMessage
                var digimonMsgMethod = AccessTools.Method(typeof(uDigimonMessagePanel), "StartMessage",
                    new Type[] { typeof(string), typeof(float) });
                if (digimonMsgMethod != null)
                {
                    harmony.Patch(digimonMsgMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(DigimonMessagePrefix))));
                }

                // Patch uFieldPanel.StartDigimonMessage
                var fieldDigimonMsgMethod = AccessTools.Method(typeof(uFieldPanel), "StartDigimonMessage",
                    new Type[] { typeof(MainGameManager.UNITID), typeof(string), typeof(float) });
                if (fieldDigimonMsgMethod != null)
                {
                    harmony.Patch(fieldDigimonMsgMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(FieldDigimonMessagePrefix))));
                }
            }
            catch { }
        }

        private static void PlayVoiceTextPrefix(string name, string text, string id)
        {
            try
            {
                if (!string.IsNullOrEmpty(text))
                {
                    _voicedTextKeys.Add(text);
                    _lastVoiceTime = DateTime.Now;
                    if (_voicedTextKeys.Count > 20)
                        _voicedTextKeys.Clear();
                }
            }
            catch { }
        }

        private static bool IsVoicedDialog()
        {
            return (DateTime.Now - _lastVoiceTime).TotalMilliseconds < 500;
        }

        private static void SetMessagePrefix(uCommonMessageWindow __instance, string str, uCommonMessageWindow.Pos window_pos)
        {
            try
            {
                if (string.IsNullOrEmpty(str))
                    return;

                if (str == _lastCommonMessage && (DateTime.Now - _lastCommonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastCommonMessage = str;
                _lastCommonMessageTime = DateTime.Now;

                // Skip "Language not found" error - the real text will be caught by CommonMessageMonitor
                if (str.Contains("ランゲージが見つかりません"))
                    return;

                if (IsPlaceholderText(str))
                    return;

                ScreenReader.Say(str);
                OnCommonMessageIntercepted?.Invoke(str);
            }
            catch { }
        }

        private static void DigimonMessagePrefix(uDigimonMessagePanel __instance, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                if (message == _lastDigimonMessage && (DateTime.Now - _lastDigimonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastDigimonMessage = message;
                _lastDigimonMessageTime = DateTime.Now;

                if (IsPlaceholderText(message))
                    return;

                ScreenReader.Say(message);
            }
            catch { }
        }

        private static void FieldDigimonMessagePrefix(MainGameManager.UNITID id, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                if (message == _lastFieldDigimonMessage && (DateTime.Now - _lastFieldDigimonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastFieldDigimonMessage = message;
                _lastFieldDigimonMessageTime = DateTime.Now;

                if (IsPlaceholderText(message))
                    return;

                ScreenReader.Say(message);
            }
            catch { }
        }

        private static void TextShrinkPrefix(EventWindowPanel __instance, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text))
                    return;

                if (text == _lastAnnouncedText)
                    return;

                _lastAnnouncedText = text;

                // Skip TTS for voiced dialog unless AlwaysReadText is enabled
                if (IsVoicedDialog() && !AlwaysReadText)
                    return;

                // Get speaker name
                string speakerName = "";
                try
                {
                    if (__instance != null && __instance.m_nameText != null)
                        speakerName = __instance.m_nameText.text ?? "";
                }
                catch { }

                if (IsPlaceholderText(text))
                    return;

                LatestName = speakerName;
                LatestText = text;
                HasNewText = true;

                OnTextIntercepted?.Invoke(speakerName, text);
            }
            catch { }
        }

        private static bool IsPlaceholderText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            if (text.Contains("■") || text.Contains("□"))
                return true;

            if (text.StartsWith("EV_") || text.StartsWith("SYS_") || text.StartsWith("MSG_"))
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
    }
}
