using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles field HUD status announcements via controller combos or keyboard.
    ///
    /// Controller layout:
    /// Hold RB + D-pad = Partner 1 info
    /// Hold LB + D-pad = Partner 2 info
    ///
    /// D-pad directions:
    /// D-Up = HP and MP
    /// D-Right = Status effects (Injury, Disease, etc.)
    /// D-Down = Current mood/condition
    /// D-Left = Name and basic info
    ///
    /// Keyboard fallback:
    /// F3 = Partner 1 full status
    /// F4 = Partner 2 full status
    /// </summary>
    public class FieldHudHandler
    {
        public void Update()
        {
            // Check if field panel is available
            var fieldPanel = uFieldPanel.m_instance;
            if (fieldPanel == null || !fieldPanel.m_enabled)
                return;

            // Check if we're in a state where we should respond (not in menus, battles, etc.)
            if (!IsPlayerInFieldControl())
                return;

            // Handle keyboard input
            HandleKeyboardInput(fieldPanel);

            // Handle controller input
            HandleControllerInput(fieldPanel);
        }

        private bool IsPlayerInFieldControl()
        {
            // Check if battle is active
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null && battlePanel.m_enabled)
                    return false;
            }
            catch { }

            // Check player action state - exclude states where we shouldn't respond
            try
            {
                var player = Object.FindObjectOfType<PlayerCtrl>();
                if (player != null)
                {
                    var state = player.actionState;
                    // Exclude states where player is not in normal field control
                    if (state == PlayerCtrl.ActionState.ActionState_Event ||
                        state == PlayerCtrl.ActionState.ActionState_Battle ||
                        state == PlayerCtrl.ActionState.ActionState_Dead ||
                        state == PlayerCtrl.ActionState.ActionState_DeadGataway ||
                        state == PlayerCtrl.ActionState.ActionState_LiquidCrystallization)
                    {
                        return false;
                    }
                }
            }
            catch { }

            return true;
        }

        private void HandleKeyboardInput(uFieldPanel fieldPanel)
        {
            // F3 = Partner 1 full status
            if (Input.GetKeyDown(KeyCode.F3))
            {
                AnnouncePartnerFullStatus(fieldPanel, 0);
            }

            // F4 = Partner 2 full status
            if (Input.GetKeyDown(KeyCode.F4))
            {
                AnnouncePartnerFullStatus(fieldPanel, 1);
            }
        }

        private void HandleControllerInput(uFieldPanel fieldPanel)
        {
            // Check if RB is held (Partner 1)
            bool rbHeld = PadManager.IsInput(PadManager.BUTTON.bR);
            // Check if LB is held (Partner 2)
            bool lbHeld = PadManager.IsInput(PadManager.BUTTON.bL);

            // Only process if one modifier is held (not both)
            if (rbHeld && !lbHeld)
            {
                HandlePartnerInput(fieldPanel, 0);
            }
            else if (lbHeld && !rbHeld)
            {
                HandlePartnerInput(fieldPanel, 1);
            }
        }

        private void HandlePartnerInput(uFieldPanel fieldPanel, int partnerIndex)
        {
            // D-Up = HP and MP
            if (PadManager.IsTrigger(PadManager.BUTTON.dUp))
            {
                AnnouncePartnerHpMp(fieldPanel, partnerIndex);
            }
            // D-Right = Status effects
            else if (PadManager.IsTrigger(PadManager.BUTTON.dRight))
            {
                AnnouncePartnerStatusEffects(fieldPanel, partnerIndex);
            }
            // D-Down = Mood/condition
            else if (PadManager.IsTrigger(PadManager.BUTTON.dDown))
            {
                AnnouncePartnerMood(fieldPanel, partnerIndex);
            }
            // D-Left = Name and basic info
            else if (PadManager.IsTrigger(PadManager.BUTTON.dLeft))
            {
                AnnouncePartnerName(fieldPanel, partnerIndex);
            }
        }

        private void AnnouncePartnerName(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Unknown";
            string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            ScreenReader.Say($"{partnerLabel}: {name}");
        }

        private void AnnouncePartnerHpMp(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";

            // Try to get HP/MP from text fields first, fall back to numeric values
            string hpText = panel.m_hpText?.text ?? panel.m_now_hp.ToString();
            string mpText = panel.m_mpText?.text ?? panel.m_now_mp.ToString();

            ScreenReader.Say($"{name}: HP {hpText}, MP {mpText}");
        }

        private void AnnouncePartnerStatusEffects(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";
            var statusEffect = panel.m_statusEffect;

            string statusText = statusEffect switch
            {
                PartnerCtrl.FieldStatusEffect.None => "Healthy",
                PartnerCtrl.FieldStatusEffect.Injury => "Injured",
                PartnerCtrl.FieldStatusEffect.SeriousInjury => "Seriously Injured",
                PartnerCtrl.FieldStatusEffect.Disease => "Sick",
                _ => "Unknown status"
            };

            ScreenReader.Say($"{name}: {statusText}");
        }

        private void AnnouncePartnerMood(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";

            // Get status effect from the panel
            var statusEffect = panel.m_statusEffect;
            string moodText;

            if (statusEffect != PartnerCtrl.FieldStatusEffect.None)
            {
                moodText = statusEffect switch
                {
                    PartnerCtrl.FieldStatusEffect.Injury => "Injured",
                    PartnerCtrl.FieldStatusEffect.SeriousInjury => "Seriously injured",
                    PartnerCtrl.FieldStatusEffect.Disease => "Sick",
                    _ => "Has condition"
                };
            }
            else
            {
                moodText = "Feeling fine";
            }

            ScreenReader.Say($"{name}: {moodText}");
        }

        private void AnnouncePartnerFullStatus(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";
            string hpText = panel.m_hpText?.text ?? panel.m_now_hp.ToString();
            string mpText = panel.m_mpText?.text ?? panel.m_now_mp.ToString();

            var statusEffect = panel.m_statusEffect;
            string statusText = statusEffect switch
            {
                PartnerCtrl.FieldStatusEffect.None => "Healthy",
                PartnerCtrl.FieldStatusEffect.Injury => "Injured",
                PartnerCtrl.FieldStatusEffect.SeriousInjury => "Seriously Injured",
                PartnerCtrl.FieldStatusEffect.Disease => "Sick",
                _ => ""
            };

            string announcement = $"{name}: HP {hpText}, MP {mpText}";
            if (!string.IsNullOrEmpty(statusText) && statusText != "Healthy")
            {
                announcement += $", {statusText}";
            }

            ScreenReader.Say(announcement);
        }

        private uFieldDigimonPanel GetDigimonPanel(uFieldPanel fieldPanel, int index)
        {
            try
            {
                var panels = fieldPanel.m_digimon_panels;
                if (panels != null && index < panels.Length)
                {
                    return panels[index];
                }
            }
            catch { }

            return null;
        }

        public bool IsActive()
        {
            var fieldPanel = uFieldPanel.m_instance;
            return fieldPanel != null && fieldPanel.m_enabled && IsPlayerInFieldControl();
        }
    }
}
