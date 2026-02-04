using HarmonyLib;
using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;

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

                // Patch uCommonMessageWindow.SetItemMessage (postfix to read built message)
                var setItemMessageMethod = AccessTools.Method(typeof(uCommonMessageWindow), "SetItemMessage",
                    new Type[] { typeof(ItemData), typeof(uCommonMessageWindow.Pos), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                if (setItemMessageMethod != null)
                {
                    harmony.Patch(setItemMessageMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(SetItemMessagePostfix))));
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

        /// <summary>
        /// Checks if the game is still loading (shouldn't announce during load).
        /// </summary>
        private static bool IsGameLoading()
        {
            return TextUtilities.IsGameLoading();
        }

        /// <summary>
        /// Checks if a panel is actually visible (active in hierarchy).
        /// </summary>
        private static bool IsPanelVisible(UnityEngine.Component panel)
        {
            try
            {
                if (panel == null || panel.gameObject == null)
                    return false;
                return panel.gameObject.activeInHierarchy;
            }
            catch { }
            return false;
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

                // Skip during game loading to avoid initialization text
                if (IsGameLoading())
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

                DebugLogger.Log($"[SetMessage] {str}");
                ScreenReader.Say(StripRichTextTags(str));
                OnCommonMessageIntercepted?.Invoke(str);
            }
            catch { }
        }

        private static void SetItemMessagePostfix(uCommonMessageWindow __instance)
        {
            try
            {
                // Skip during game loading
                if (IsGameLoading())
                    return;

                // Read the label text that was just set by SetItemMessage
                if (__instance == null || __instance.m_label == null)
                    return;

                string text = __instance.m_label.text;
                if (string.IsNullOrEmpty(text))
                    return;

                if (IsPlaceholderText(text))
                    return;

                // Always announce - SetItemMessage is called each time user tries to use an item
                // No duplicate check here because repeated attempts should be announced
                DebugLogger.Log($"[SetItemMessage] {text}");
                ScreenReader.Say(StripRichTextTags(text));
            }
            catch { }
        }

        private static void DigimonMessagePrefix(uDigimonMessagePanel __instance, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                // Skip if panel isn't already opened (means it's being initialized, not updated)
                // m_isOpend is inherited from UiDispBase
                if (__instance == null || !__instance.m_isOpend)
                    return;

                if (message == _lastDigimonMessage && (DateTime.Now - _lastDigimonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastDigimonMessage = message;
                _lastDigimonMessageTime = DateTime.Now;

                if (IsPlaceholderText(message))
                    return;

                DebugLogger.Log($"[DigimonMessage] {message}");
                ScreenReader.SayQueued(StripRichTextTags(message));
            }
            catch { }
        }

        private static void FieldDigimonMessagePrefix(MainGameManager.UNITID id, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                // Skip if field panel isn't active yet (means game is still initializing)
                var fieldPanel = uFieldPanel.m_instance;
                if (fieldPanel == null || !fieldPanel.gameObject.activeInHierarchy)
                    return;

                if (message == _lastFieldDigimonMessage && (DateTime.Now - _lastFieldDigimonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastFieldDigimonMessage = message;
                _lastFieldDigimonMessageTime = DateTime.Now;

                if (IsPlaceholderText(message))
                    return;

                DebugLogger.Log($"[FieldDigimonMessage] {message}");
                ScreenReader.SayQueued(StripRichTextTags(message));
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

        /// <summary>
        /// Strips Unity rich text tags like <color>, <b>, etc. for clean screen reader output.
        /// </summary>
        public static string StripRichTextTags(string text)
        {
            return TextUtilities.StripRichTextTags(text);
        }

        private static bool IsPlaceholderText(string text)
        {
            return TextUtilities.IsPlaceholderText(text);
        }
    }
}
