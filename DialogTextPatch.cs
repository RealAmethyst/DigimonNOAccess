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
    /// - uCommonMessageWindow.SetMessage - field notifications (direct text)
    /// - uCommonMessageWindow.SetLangMessage - field notifications (localization key)
    /// - uCommonMessageWindow.SetItemMessage - item use/pickup messages
    /// - TalkMain.CommonMessageWindow - NPC talk script message popups (item rewards, etc.)
    /// - TalkMain.ItemWindow - NPC talk script item display popups
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

        // Track last announced text to avoid duplicates within a single conversation.
        // Reset when dialog panel closes so repeated talks to the same NPC work.
        private static string _lastAnnouncedText = "";

        /// <summary>
        /// Reset the last announced text tracker. Call when dialog panel closes
        /// so the same NPC dialog can be re-announced on the next interaction.
        /// </summary>
        public static void ResetLastAnnouncedText()
        {
            _lastAnnouncedText = "";
        }

        // Track voiced dialog - these texts will be spoken by the game, so we skip TTS
        private static HashSet<string> _voicedTextKeys = new HashSet<string>();
        private static DateTime _lastVoiceTime = DateTime.MinValue;

        // Toggle: When true, read ALL text including voiced dialog (for non-English voice users)
        public static bool AlwaysReadText { get; set; } = false;

        // Track last messages to avoid duplicates (back-to-back identical calls)
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

                // Patch uCommonMessageWindow.SetLangMessage (postfix to read resolved text)
                var setLangMessageMethod = AccessTools.Method(typeof(uCommonMessageWindow), "SetLangMessage",
                    new Type[] { typeof(string), typeof(uCommonMessageWindow.Pos) });
                if (setLangMessageMethod != null)
                {
                    harmony.Patch(setLangMessageMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(SetLangMessagePostfix))));
                }

                // Patch uCommonMessageWindow.SetItemMessage (postfix to read built message)
                var setItemMessageMethod = AccessTools.Method(typeof(uCommonMessageWindow), "SetItemMessage",
                    new Type[] { typeof(ItemData), typeof(uCommonMessageWindow.Pos), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                if (setItemMessageMethod != null)
                {
                    harmony.Patch(setItemMessageMethod, postfix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(SetItemMessagePostfix))));
                }

                // Patch TalkMain.CommonMessageWindow - NPC talk script command that shows
                // popup messages during NPC conversations (item rewards, notifications, etc.)
                var talkCommonMsgMethod = AccessTools.Method(typeof(TalkMain), "CommonMessageWindow",
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
                if (talkCommonMsgMethod != null)
                {
                    harmony.Patch(talkCommonMsgMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(TalkCommonMessageWindowPrefix))));
                    DebugLogger.Log("[DialogTextPatch] Patched TalkMain.CommonMessageWindow");
                }
                else
                {
                    DebugLogger.Warning("[DialogTextPatch] Could not find TalkMain.CommonMessageWindow");
                }

                // Patch TalkMain.ItemWindow - NPC talk script command that shows
                // item popup windows during NPC conversations
                var talkItemWindowMethod = AccessTools.Method(typeof(TalkMain), "ItemWindow",
                    new Type[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string) });
                if (talkItemWindowMethod != null)
                {
                    harmony.Patch(talkItemWindowMethod, prefix: new HarmonyMethod(AccessTools.Method(typeof(DialogTextPatch), nameof(TalkItemWindowPrefix))));
                    DebugLogger.Log("[DialogTextPatch] Patched TalkMain.ItemWindow");
                }
                else
                {
                    DebugLogger.Warning("[DialogTextPatch] Could not find TalkMain.ItemWindow");
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
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in Apply: {ex.Message}");
            }
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
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in PlayVoiceTextPrefix: {ex.Message}");
            }
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

                // Skip "Language not found" error - SetLangMessage postfix will catch the resolved text
                if (str.Contains("ランゲージが見つかりません"))
                    return;

                if (IsPlaceholderText(str))
                    return;

                DebugLogger.Log($"[SetMessage] {str}");
                string cleaned = StripRichTextTags(str);
                cleaned = TextUtilities.FormatItemMessage(cleaned);

                // Partner01 (second partner) messages queue after Partner00 (first partner)
                // so both are heard (e.g. sleep stat recovery for each partner)
                // Education completion also queues subsequent messages for both partners
                if (window_pos == uCommonMessageWindow.Pos.Partner01 ||
                    EducationPanelHandler.ShouldQueueNextMessage())
                    ScreenReader.SayQueued(cleaned);
                else
                    ScreenReader.Say(cleaned);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in SetMessagePrefix: {ex.Message}");
            }
        }

        private static void SetLangMessagePostfix(uCommonMessageWindow __instance, string lang_code, uCommonMessageWindow.Pos window_pos)
        {
            try
            {
                // Skip during game loading
                if (IsGameLoading())
                    return;

                // Read the label text that was resolved from the lang code
                if (__instance == null || __instance.m_label == null)
                    return;

                string text = __instance.m_label.text;
                if (string.IsNullOrEmpty(text))
                    return;

                // Dedup against SetMessage (SetLangMessage may internally call SetMessage)
                if (text == _lastCommonMessage && (DateTime.Now - _lastCommonMessageTime).TotalMilliseconds < 500)
                    return;

                _lastCommonMessage = text;
                _lastCommonMessageTime = DateTime.Now;

                if (IsPlaceholderText(text))
                    return;

                DebugLogger.Log($"[SetLangMessage] {text} (key: {lang_code})");
                string cleaned = StripRichTextTags(text);
                cleaned = TextUtilities.FormatItemMessage(cleaned);

                if (window_pos == uCommonMessageWindow.Pos.Partner01 ||
                    EducationPanelHandler.ShouldQueueNextMessage())
                    ScreenReader.SayQueued(cleaned);
                else
                    ScreenReader.Say(cleaned);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in SetLangMessagePostfix: {ex.Message}");
            }
        }

        private static void SetItemMessagePostfix(uCommonMessageWindow __instance, uCommonMessageWindow.Pos pos)
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

                _lastCommonMessage = text;
                _lastCommonMessageTime = DateTime.Now;

                string cleanText = StripRichTextTags(text);
                DebugLogger.Log($"[SetItemMessage] {text}");

                // Partner01 (second partner) messages queue after Partner00 (first partner)
                // so both are heard (e.g. using items on both partners)
                // Education completion also queues subsequent messages for both partners
                if (pos == uCommonMessageWindow.Pos.Partner01 ||
                    EducationPanelHandler.ShouldQueueNextMessage())
                    ScreenReader.SayQueued(cleanText);
                else
                    ScreenReader.Say(cleanText);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in SetItemMessagePostfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Intercepts TalkMain.CommonMessageWindow coroutine command.
        /// This fires when NPC talk scripts show popup messages (item rewards, etc.).
        /// The coroutine stores arg0 and arg1 - arg0 is likely the lang code or text.
        /// </summary>
        private static void TalkCommonMessageWindowPrefix(string arg0, string arg1, string arg2, string arg3, string arg4, string arg5)
        {
            try
            {
                ResolveAndAnnounce(arg0, "TalkCommonMsg");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in TalkCommonMessageWindowPrefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Intercepts TalkMain.ItemWindow coroutine command.
        /// This fires when NPC talk scripts show item popup windows.
        /// The coroutine stores only arg0.
        /// </summary>
        private static void TalkItemWindowPrefix(string arg0, string arg1, string arg2, string arg3, string arg4, string arg5)
        {
            try
            {
                ResolveAndAnnounce(arg0, "TalkItemWindow");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in TalkItemWindowPrefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to resolve a string as a Language key, then announce it.
        /// Talk scripts use Language.GetString (separate from Localization).
        /// Returns the resolved text if successful, null otherwise.
        /// </summary>
        private static string ResolveAndAnnounce(string value, string source)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            // Talk scripts use Language.GetString, not Localization.Get
            string resolved = null;
            try
            {
                resolved = Language.GetString(value);
            }
            catch { }

            // Use resolved text if valid and different from key
            string text = null;
            if (!string.IsNullOrEmpty(resolved) && resolved != value && !IsPlaceholderText(resolved))
                text = resolved;

            if (text == null)
                return null;

            // Dedup against recent common messages
            if (text == _lastCommonMessage && (DateTime.Now - _lastCommonMessageTime).TotalMilliseconds < 500)
                return text;

            _lastCommonMessage = text;
            _lastCommonMessageTime = DateTime.Now;

            string cleaned = StripRichTextTags(text);
            cleaned = TextUtilities.FormatItemMessage(cleaned);

            if (!string.IsNullOrEmpty(cleaned))
            {
                DebugLogger.Log($"[{source}] {cleaned}");
                ScreenReader.Say(cleaned);
            }

            return text;
        }

        private static void DigimonMessagePrefix(uDigimonMessagePanel __instance, string message, float time)
        {
            try
            {
                if (string.IsNullOrEmpty(message))
                    return;

                // Skip if panel isn't already opened (means it's being initialized, not updated)
                if (__instance == null || !__instance.m_isOpend)
                    return;

                // Also skip during game loading
                if (IsGameLoading())
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
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in DigimonMessagePrefix: {ex.Message}");
            }
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

                // Also skip during game loading
                if (IsGameLoading())
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
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in FieldDigimonMessagePrefix: {ex.Message}");
            }
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
                catch (System.Exception ex)
                {
                    DebugLogger.Log($"[DialogTextPatch] Error getting speaker name: {ex.Message}");
                }

                if (IsPlaceholderText(text))
                    return;

                LatestName = speakerName;
                LatestText = text;
                HasNewText = true;

                OnTextIntercepted?.Invoke(speakerName, text);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DialogTextPatch] Error in TextShrinkPrefix: {ex.Message}");
            }
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
