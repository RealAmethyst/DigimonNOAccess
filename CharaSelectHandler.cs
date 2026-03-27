using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the character/gender selection screen in New Game.
    /// Reads gender label and character name from the game's own UI text components,
    /// and reads caption/instructions from the scene's uCaptionBase panel.
    /// </summary>
    public class CharaSelectHandler : HandlerBase<uCharaSelectPanel>
    {
        protected override string LogTag => "[CharaSelect]";
        public override int Priority => 40;

        private int _lastGender = -1;

        public override bool IsOpen()
        {
            _panel = Object.FindObjectOfType<uCharaSelectPanel>();

            return _panel != null &&
                   _panel.gameObject != null &&
                   _panel.gameObject.activeInHierarchy;
        }

        protected override void OnOpen()
        {
            _lastGender = -1;

            if (_panel == null)
                return;

            int gender = _panel.Gender;
            string characterInfo = GetCharacterInfo();

            ScreenReader.Say(HEADER);
            ScreenReader.SayQueued(characterInfo);

            DebugLogger.Log($"{LogTag} Opened: gender={gender}, info={characterInfo}");
            _lastGender = gender;
        }

        protected override void OnClose()
        {
            _lastGender = -1;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (_panel == null) return;

            int gender = _panel.Gender;
            if (gender != _lastGender)
            {
                string characterInfo = GetCharacterInfo();
                ScreenReader.Say(characterInfo);
                DebugLogger.Log($"{LogTag} Selection changed: {gender}, info={characterInfo}");
                _lastGender = gender;
            }
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen()) return;

            string characterInfo = GetCharacterInfo();
            ScreenReader.Say(HEADER);
            ScreenReader.SayQueued(characterInfo);
        }

        // Caption text is a texture in the title screen, not a readable text component.
        private const string HEADER = "Choose your character. Left and right to change";

        /// <summary>
        /// Builds character info string from child Text components.
        /// </summary>
        private string GetCharacterInfo()
        {
            try
            {
                var texts = _panel.GetComponentsInChildren<Text>(true);
                if (texts == null) return GetFallbackInfo();

                string panelCaptionText = _panel.m_captionText?.text?.Trim() ?? "";
                string genderLabel = null;
                string characterName = null;

                foreach (var text in texts)
                {
                    if (text == null) continue;
                    string val = text.text;
                    if (string.IsNullOrEmpty(val)) continue;

                    val = val.Trim();
                    if (val.Length < 2) continue;

                    // Skip the panel's own Japanese caption text
                    if (!string.IsNullOrEmpty(panelCaptionText) && val == panelCaptionText)
                        continue;

                    if (genderLabel == null)
                        genderLabel = TextUtilities.StripRichTextTags(val).Trim();
                    else if (characterName == null)
                        characterName = TextUtilities.StripRichTextTags(val).Trim().Trim('"', '\u201C', '\u201D', ' ');
                }

                if (genderLabel != null && characterName != null)
                    return $"{genderLabel}, {characterName}";
                if (genderLabel != null)
                    return genderLabel;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading character info: {ex.Message}");
            }

            return GetFallbackInfo();
        }

        private string GetFallbackInfo()
        {
            int gender = _panel?.Gender ?? 0;
            return gender == 0 ? "Male, Takuto" : "Female, Shiki";
        }

    }
}
