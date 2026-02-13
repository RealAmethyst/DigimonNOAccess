using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles field HUD status announcements via hotkey.
    /// One key per partner, announces all relevant info.
    ///
    /// Keyboard: F3 = Partner 1, F4 = Partner 2
    /// Controller: RB+DPadUp = Partner 1, LB+DPadUp = Partner 2
    /// </summary>
    public class FieldHudHandler : IAccessibilityHandler
    {
        public int Priority => 997;

        public bool IsOpen() => false;
        public void AnnounceStatus() { }

        // Fishing prompt tracking
        private bool _wasFishingPromptActive = false;
        private string _lastFishingText = "";

        public void Update()
        {
            var fieldPanel = uFieldPanel.m_instance;
            if (fieldPanel == null || !fieldPanel.m_enabled)
                return;

            UpdateFishingPrompt(fieldPanel);

            if (!GameStateService.IsPlayerInFieldControl())
                return;

            if (ModInputManager.IsActionTriggered("Partner1Status"))
                AnnouncePartnerStatus(0);

            if (ModInputManager.IsActionTriggered("Partner2Status"))
                AnnouncePartnerStatus(1);
        }

        private void AnnouncePartnerStatus(int partnerIndex)
        {
            try
            {
                var partnerNo = partnerIndex == 0
                    ? AppInfo.PARTNER_NO.Right
                    : AppInfo.PARTNER_NO.Left;

                var mgr = MainGameManager.Ref;
                if (mgr == null)
                {
                    ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                    return;
                }

                var ss = mgr.scenarioScript;
                if (ss == null)
                {
                    ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                    return;
                }

                // Get name from PartnerCtrl
                var partnerCtrl = MainGameManager.GetPartnerCtrl(partnerIndex);
                string name = partnerCtrl?.Name ?? PartnerUtilities.GetPartnerLabel(partnerIndex);

                // Read all stats from CScenarioScript (live game data)
                uint hp = ss._GetPartnerHp(partnerNo);
                uint hpMax = ss._GetPartnerHpMax(partnerNo);
                uint mp = ss._GetPartnerMp(partnerNo);
                uint mpMax = ss._GetPartnerMpMax(partnerNo);

                var sb = new System.Text.StringBuilder();
                sb.Append($"{name}, HP {hp} of {hpMax}, MP {mp} of {mpMax}");

                // Status effect from PartnerCtrl
                if (partnerCtrl != null)
                {
                    string statusText = PartnerUtilities.GetStatusEffectText(partnerCtrl.FSEffect, "", "");
                    if (!string.IsNullOrEmpty(statusText))
                        sb.Append($", {statusText}");
                }

                // Care stats
                uint mood = ss._GetPartnerMood(partnerNo);
                sb.Append($", Mood {mood}");

                uint discipline = ss._GetPartnerUpbringing(partnerNo);
                sb.Append($", Discipline {discipline}");

                uint tiredness = ss._GetPartnerFatigue(partnerNo);
                sb.Append($", Tiredness {tiredness}");

                uint curse = ss._GetPartnerCurse(partnerNo);
                sb.Append($", Curse {curse}");

                uint weight = ss._GetPartnerWeight(partnerNo);
                sb.Append($", Weight {weight}");

                uint age = ss._GetPartnerAge(partnerNo);
                sb.Append($", Age {age}");

                ScreenReader.Say(sb.ToString());
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FieldHud] Partner status error: {ex.Message}");
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
            }
        }

        private void UpdateFishingPrompt(uFieldPanel fieldPanel)
        {
            try
            {
                var fishingPanel = fieldPanel.m_fishing_ok;
                bool isActive = fishingPanel != null && fishingPanel.gameObject != null && fishingPanel.gameObject.activeInHierarchy;

                if (isActive && !_wasFishingPromptActive)
                {
                    string text = fieldPanel.m_fishing_ok_text?.text?.Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        ScreenReader.Say(text);
                        _lastFishingText = text;
                    }
                }
                else if (isActive && _wasFishingPromptActive)
                {
                    string text = fieldPanel.m_fishing_ok_text?.text?.Trim();
                    if (!string.IsNullOrEmpty(text) && text != _lastFishingText)
                    {
                        ScreenReader.Say(text);
                        _lastFishingText = text;
                    }
                }
                else if (!isActive && _wasFishingPromptActive)
                {
                    _lastFishingText = "";
                }

                _wasFishingPromptActive = isActive;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FieldHud] Fishing prompt error: {ex.Message}");
            }
        }

        public bool IsActive()
        {
            var fieldPanel = uFieldPanel.m_instance;
            return fieldPanel != null && fieldPanel.m_enabled && GameStateService.IsPlayerInFieldControl();
        }
    }
}
