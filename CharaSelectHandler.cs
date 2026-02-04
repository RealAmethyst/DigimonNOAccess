using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the character/gender selection screen in New Game.
    /// </summary>
    public class CharaSelectHandler
    {
        private uCharaSelectPanel _panel;
        private bool _wasActive = false;
        private int _lastGender = -1;

        /// <summary>
        /// Check if the character select panel is currently open.
        /// </summary>
        public bool IsOpen()
        {
            _panel = Object.FindObjectOfType<uCharaSelectPanel>();

            return _panel != null &&
                   _panel.gameObject != null &&
                   _panel.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            bool isActive = IsOpen();

            // Panel just opened
            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            // Panel just closed
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            // Panel is active, check for selection changes
            else if (isActive)
            {
                CheckSelectionChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastGender = -1;

            if (_panel == null)
                return;

            int gender = _panel.Gender;
            string genderName = GetGenderName(gender);

            // Use hardcoded English - the game's caption is in Japanese even for English version
            string announcement = $"Choose your character. Left and right to change gender. {genderName} selected";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[CharaSelect] Opened: gender={gender}");

            _lastGender = gender;
        }

        private void OnClose()
        {
            _panel = null;
            _lastGender = -1;
            DebugLogger.Log("[CharaSelect] Closed");
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
                DebugLogger.Log($"[CharaSelect] Selection changed: {gender} = {genderName}");
                _lastGender = gender;
            }
        }

        private string GetGenderName(int gender)
        {
            switch (gender)
            {
                case 0: return "Male";
                case 1: return "Female";
                default: return $"Option {gender + 1}";
            }
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
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
