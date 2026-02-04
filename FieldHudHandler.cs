using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles field HUD status announcements via controller combos or keyboard.
    /// Also monitors contextual prompts like fishing notifications.
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
    public class FieldHudHandler : IAccessibilityHandler
    {
        public int Priority => 997;

        /// <summary>
        /// Background handler - never owns the status announce.
        /// </summary>
        public bool IsOpen() => false;

        /// <summary>
        /// Background handler - never announces status.
        /// </summary>
        public void AnnounceStatus() { }

        // Fishing prompt tracking
        private bool _wasFishingPromptActive = false;
        private string _lastFishingText = "";

        public void Update()
        {
            // Check if field panel is available
            var fieldPanel = uFieldPanel.m_instance;
            if (fieldPanel == null || !fieldPanel.m_enabled)
                return;

            // Always check fishing prompts (they can appear during various states)
            UpdateFishingPrompt(fieldPanel);

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
            return GameStateService.IsPlayerInFieldControl();
        }

        private void HandleKeyboardInput(uFieldPanel fieldPanel)
        {
            // Use configurable input system
            if (ModInputManager.IsActionTriggered("Partner1Status"))
            {
                AnnouncePartnerFullStatus(fieldPanel, 0);
            }

            if (ModInputManager.IsActionTriggered("Partner2Status"))
            {
                AnnouncePartnerFullStatus(fieldPanel, 1);
            }
        }

        private void HandleControllerInput(uFieldPanel fieldPanel)
        {
            // Partner 1 controller inputs (RB + D-Pad by default)
            // Note: Partner1Status is handled in keyboard section (both keyboard and controller bindings)
            if (ModInputManager.IsActionTriggered("Partner1Effects"))
            {
                AnnouncePartnerStatusEffects(fieldPanel, 0);
            }
            else if (ModInputManager.IsActionTriggered("Partner1Mood"))
            {
                AnnouncePartnerMood(fieldPanel, 0);
            }
            else if (ModInputManager.IsActionTriggered("Partner1Info"))
            {
                AnnouncePartnerName(fieldPanel, 0);
            }

            // Partner 2 controller inputs (LB + D-Pad by default)
            // Note: Partner2Status is handled in keyboard section (both keyboard and controller bindings)
            if (ModInputManager.IsActionTriggered("Partner2Effects"))
            {
                AnnouncePartnerStatusEffects(fieldPanel, 1);
            }
            else if (ModInputManager.IsActionTriggered("Partner2Mood"))
            {
                AnnouncePartnerMood(fieldPanel, 1);
            }
            else if (ModInputManager.IsActionTriggered("Partner2Info"))
            {
                AnnouncePartnerName(fieldPanel, 1);
            }
        }

        private void AnnouncePartnerName(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Unknown";
            string partnerLabel = PartnerUtilities.GetPartnerLabel(partnerIndex);
            ScreenReader.Say($"{partnerLabel}: {name}");
        }

        private void AnnouncePartnerHpMp(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
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
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";
            var statusEffect = panel.m_statusEffect;

            string statusText = PartnerUtilities.GetStatusEffectText(statusEffect);

            ScreenReader.Say($"{name}: {statusText}");
        }

        private void AnnouncePartnerMood(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";

            // Get status effect from the panel
            var statusEffect = panel.m_statusEffect;
            string moodText = PartnerUtilities.GetStatusEffectText(statusEffect, "Feeling fine", "Has condition");

            ScreenReader.Say($"{name}: {moodText}");
        }

        private void AnnouncePartnerFullStatus(uFieldPanel fieldPanel, int partnerIndex)
        {
            var panel = GetDigimonPanel(fieldPanel, partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                return;
            }

            string name = panel.m_digimon_name?.text ?? "Partner";
            string hpText = panel.m_hpText?.text ?? panel.m_now_hp.ToString();
            string mpText = panel.m_mpText?.text ?? panel.m_now_mp.ToString();

            var statusEffect = panel.m_statusEffect;
            string statusText = PartnerUtilities.GetStatusEffectText(statusEffect, "Healthy", "");

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
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FieldHudHandler] Error in GetDigimonPanel: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check and announce fishing prompts when they appear.
        /// Fishing prompts appear on uFieldPanel.m_fishing_ok panel with text in m_fishing_ok_text.
        /// </summary>
        private void UpdateFishingPrompt(uFieldPanel fieldPanel)
        {
            try
            {
                var fishingPanel = fieldPanel.m_fishing_ok;
                bool isActive = fishingPanel != null && fishingPanel.gameObject != null && fishingPanel.gameObject.activeInHierarchy;

                if (isActive && !_wasFishingPromptActive)
                {
                    // Fishing prompt just appeared
                    string text = GetFishingPromptText(fieldPanel);
                    if (!string.IsNullOrEmpty(text))
                    {
                        ScreenReader.Say(text);
                        _lastFishingText = text;
                    }
                }
                else if (isActive && _wasFishingPromptActive)
                {
                    // Check for text changes while prompt is visible
                    string text = GetFishingPromptText(fieldPanel);
                    if (!string.IsNullOrEmpty(text) && text != _lastFishingText)
                    {
                        ScreenReader.Say(text);
                        _lastFishingText = text;
                    }
                }
                else if (!isActive && _wasFishingPromptActive)
                {
                    // Prompt closed
                    _lastFishingText = "";
                }

                _wasFishingPromptActive = isActive;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FieldHudHandler] Error in UpdateFishingPrompt: {ex.Message}");
            }
        }

        private string GetFishingPromptText(uFieldPanel fieldPanel)
        {
            try
            {
                var textComponent = fieldPanel.m_fishing_ok_text;
                if (textComponent != null)
                {
                    string text = textComponent.text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text.Trim();
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FieldHudHandler] Error in GetFishingPromptText: {ex.Message}");
            }

            return "";
        }

        public bool IsActive()
        {
            var fieldPanel = uFieldPanel.m_instance;
            return fieldPanel != null && fieldPanel.m_enabled && IsPlayerInFieldControl();
        }
    }
}
