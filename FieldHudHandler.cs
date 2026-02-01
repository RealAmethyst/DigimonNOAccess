using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles field HUD status announcements via controller combos or keyboard.
    ///
    /// Controller layout:
    /// Hold RB + face button = Partner 1 info
    /// Hold LB + face button = Partner 2 info
    ///
    /// Face buttons:
    /// A/Cross = HP and MP
    /// B/Circle = Status effects (Injury, Disease, etc.)
    /// X/Square = Current mood/condition
    /// Y/Triangle = Name and basic info
    ///
    /// Keyboard fallback:
    /// F3 = Partner 1 full status
    /// F4 = Partner 2 full status
    /// </summary>
    public class FieldHudHandler
    {
        private bool _initialized = false;

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

            // Check player action state
            try
            {
                var player = PlayerCtrl.Ref;
                if (player != null)
                {
                    var state = player.actionState;
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

            // Check if Digivice menu is open (uses same buttons)
            try
            {
                var digiviceTop = Object.FindObjectOfType<uDigiviceTopPanel>();
                if (digiviceTop != null && digiviceTop.gameObject.activeInHierarchy)
                    return false;
            }
            catch { }

            // Check if any common menus are open
            try
            {
                var optionPanel = Object.FindObjectOfType<uOptionPanel>();
                if (optionPanel != null && optionPanel.m_State == uOptionPanel.State.MAIN_SETTING)
                    return false;
            }
            catch { }

            // Check for camp/NPC menus
            try
            {
                var campPanel = Object.FindObjectOfType<uCampPanelCommand>();
                if (campPanel != null && campPanel.m_state == uCampPanelCommand.State.Main)
                    return false;
            }
            catch { }

            // Check for training menu
            try
            {
                var trainingPanel = Object.FindObjectOfType<uTrainingPanelCommand>();
                if (trainingPanel != null && trainingPanel.m_state == uTrainingPanelCommand.State.Main)
                    return false;
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
            // A/Cross = HP and MP
            if (PadManager.IsTrigger(PadManager.BUTTON.bCross))
            {
                AnnouncePartnerHpMp(fieldPanel, partnerIndex);
            }
            // B/Circle = Status effects
            else if (PadManager.IsTrigger(PadManager.BUTTON.bCircle))
            {
                AnnouncePartnerStatusEffects(fieldPanel, partnerIndex);
            }
            // X/Square = Mood/condition
            else if (PadManager.IsTrigger(PadManager.BUTTON.bSquare))
            {
                AnnouncePartnerMood(fieldPanel, partnerIndex);
            }
            // Y/Triangle = Name and basic info
            else if (PadManager.IsTrigger(PadManager.BUTTON.bTriangle))
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

            // Try to get mood info from the mood panel
            var moodPanel = panel.m_moodPanel;
            string moodText = "Normal";

            if (moodPanel != null)
            {
                // The mood panel shows mood level via visual indicator
                // For now, report that mood info is available
                moodText = "Check mood panel";
            }

            // Try to get additional status from partner controller
            try
            {
                var partners = GetPartnerControllers();
                if (partners != null && partnerIndex < partners.Length && partners[partnerIndex] != null)
                {
                    var partner = partners[partnerIndex];
                    var fsEffect = partner.FSEffect;

                    if (fsEffect != PartnerCtrl.FieldStatusEffect.None)
                    {
                        moodText = fsEffect switch
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
                }
            }
            catch
            {
                // Fall back to basic info
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

        private PartnerCtrl[] GetPartnerControllers()
        {
            try
            {
                // Try to get partner controllers from MainGameManager
                var mgm = MainGameManager.Ref;
                if (mgm != null)
                {
                    var partners = new PartnerCtrl[2];
                    partners[0] = mgm.GetPartnerCtrl(MainGameManager.PARTNER_NO.NO_0);
                    partners[1] = mgm.GetPartnerCtrl(MainGameManager.PARTNER_NO.NO_1);
                    return partners;
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
