using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the character/gender selection screen in New Game.
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
            string genderName = GetGenderName(gender);

            // Use hardcoded English - the game's caption is in Japanese even for English version
            string announcement = $"Choose your character. Left and right to change gender. {genderName} selected";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened: gender={gender}");

            _lastGender = gender;
        }

        protected override void OnClose()
        {
            _lastGender = -1;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckSelectionChange();
        }

        private void CheckSelectionChange()
        {
            if (_panel == null)
                return;

            int gender = _panel.Gender;
            if (gender != _lastGender)
            {
                string genderName = GetGenderName(gender);
                ScreenReader.Say(genderName);
                DebugLogger.Log($"{LogTag} Selection changed: {gender} = {genderName}");
                _lastGender = gender;
            }
        }

        private string GetGenderName(int gender)
        {
            switch (gender)
            {
                case 0: return "Male";
                case 1: return "Female";
                default: return AnnouncementBuilder.FallbackItem("Option", gender);
            }
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int gender = _panel.Gender;
            string genderName = GetGenderName(gender);

            string announcement = $"Character Selection. {genderName} selected";
            ScreenReader.Say(announcement);
        }
    }
}
