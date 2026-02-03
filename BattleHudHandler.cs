using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle HUD status announcements via keyboard and controller.
    ///
    /// Default bindings (configurable in hotkeys.ini):
    /// Keyboard:
    ///   F3 = Partner 1 full status
    ///   F4 = Partner 2 full status
    ///   F6 = Partner 1 HP/MP
    ///   F7 = Partner 2 HP/MP
    ///
    /// Controller (via SDL3):
    ///   RStickUp = Partner 1 HP/MP
    ///   RStickDown = Partner 2 HP/MP
    ///   RStickLeft = Partner 1 Order
    ///   RStickRight = Partner 2 Order
    /// </summary>
    public class BattleHudHandler
    {
        private uBattlePanel _cachedBattlePanel;

        private bool _loggedBattleActive = false;

        public void Update()
        {
            // Check if battle is active
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                _cachedBattlePanel = null;
                _loggedBattleActive = false;
                return;
            }

            _cachedBattlePanel = battlePanel;

            // Log once when battle becomes active
            if (!_loggedBattleActive)
            {
                _loggedBattleActive = true;
                DebugLogger.Log("[BattleHudHandler] Battle panel active, D-pad status checks enabled");
            }

            // Handle keyboard input (F3/F4/F6/F7)
            HandleKeyboardInput();
        }

        private bool IsInSubMenu()
        {
            // Check if item menu is open
            try
            {
                if (_cachedBattlePanel?.m_itemBox != null &&
                    _cachedBattlePanel.m_itemBox.gameObject.activeInHierarchy &&
                    _cachedBattlePanel.m_itemBox.m_isVisible)
                {
                    return true;
                }
            }
            catch { }

            // Check if Order Ring is open
            try
            {
                var cmdPanel = Object.FindObjectOfType<uBattlePanelCommand>();
                if (cmdPanel != null &&
                    cmdPanel.gameObject.activeInHierarchy &&
                    cmdPanel.m_selectMode != uBattlePanelCommand.SelectMode.None)
                {
                    return true;
                }
            }
            catch { }

            // Check if dialog is open
            try
            {
                var dialog = Object.FindObjectOfType<uBattlePanelDialog>();
                if (dialog != null && dialog.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            catch { }

            // Check if tactics menu is open
            try
            {
                if (_cachedBattlePanel?.m_tactics != null &&
                    _cachedBattlePanel.m_tactics.gameObject.activeInHierarchy &&
                    _cachedBattlePanel.m_tactics.m_mode != uBattlePanelTactics.InternalMode.None)
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private void HandleKeyboardInput()
        {
            // Use configurable input system for all battle inputs
            // Each action checks both keyboard and controller bindings automatically

            // Partner 1 full status (F3 or controller binding)
            if (ModInputManager.IsActionTriggered("Partner1Status"))
            {
                DebugLogger.Log("[BattleHudHandler] Partner1Status triggered in battle");
                AnnouncePartnerFullStatus(0);
            }

            // Partner 2 full status (F4 or controller binding)
            if (ModInputManager.IsActionTriggered("Partner2Status"))
            {
                DebugLogger.Log("[BattleHudHandler] Partner2Status triggered in battle");
                AnnouncePartnerFullStatus(1);
            }

            // Partner 1 HP/MP only (F6 or RStickUp)
            if (ModInputManager.IsActionTriggered("BattlePartner1HP"))
            {
                DebugLogger.Log("[BattleHudHandler] BattlePartner1HP triggered");
                AnnouncePartnerHpMp(0);
            }

            // Partner 2 HP/MP only (F7 or RStickDown)
            if (ModInputManager.IsActionTriggered("BattlePartner2HP"))
            {
                DebugLogger.Log("[BattleHudHandler] BattlePartner2HP triggered");
                AnnouncePartnerHpMp(1);
            }

            // Partner 1 Order (RStickLeft)
            if (ModInputManager.IsActionTriggered("BattlePartner1Order"))
            {
                DebugLogger.Log("[BattleHudHandler] BattlePartner1Order triggered");
                AnnouncePartnerOrder(0);
            }

            // Partner 2 Order (RStickRight)
            if (ModInputManager.IsActionTriggered("BattlePartner2Order"))
            {
                DebugLogger.Log("[BattleHudHandler] BattlePartner2Order triggered");
                AnnouncePartnerOrder(1);
            }
        }

        private uBattlePanelDigimon GetDigimonPanel(int index)
        {
            try
            {
                if (_cachedBattlePanel == null)
                {
                    DebugLogger.Log($"[BattleHudHandler] GetDigimonPanel: _cachedBattlePanel is null");
                    return null;
                }

                var panels = _cachedBattlePanel.m_digimon;
                if (panels == null)
                {
                    DebugLogger.Log($"[BattleHudHandler] GetDigimonPanel: m_digimon array is null");
                    return null;
                }

                DebugLogger.Log($"[BattleHudHandler] GetDigimonPanel: m_digimon.Length = {panels.Length}, requesting index {index}");

                if (index >= 0 && index < panels.Length)
                {
                    var panel = panels[index];
                    if (panel == null)
                    {
                        DebugLogger.Log($"[BattleHudHandler] GetDigimonPanel: panel at index {index} is null");
                    }
                    return panel;
                }
                else
                {
                    DebugLogger.Log($"[BattleHudHandler] GetDigimonPanel: index {index} out of range");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleHudHandler] GetDigimonPanel exception: {ex.Message}");
            }

            return null;
        }

        private void AnnouncePartnerName(int partnerIndex)
        {
            var panel = GetDigimonPanel(partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            // Partner name isn't stored in a simple text field in battle panel
            // Use the partner label which is sufficient for gameplay
            string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            ScreenReader.Say($"{partnerLabel} status panel");
        }

        private void AnnouncePartnerHpMp(int partnerIndex)
        {
            DebugLogger.Log($"[BattleHudHandler] AnnouncePartnerHpMp called for partner {partnerIndex}");

            var panel = GetDigimonPanel(partnerIndex);
            if (panel == null)
            {
                DebugLogger.Log($"[BattleHudHandler] Panel is null for partner {partnerIndex}");
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            // Get HP and MP - try multiple sources
            int hp = 0;
            int mp = 0;
            string hpSource = "unknown";
            string mpSource = "unknown";

            // Method 1: Try m_now_hp/m_now_mp (integer fields from base class)
            try
            {
                hp = panel.m_now_hp;
                mp = panel.m_now_mp;
                hpSource = "m_now_hp";
                mpSource = "m_now_mp";
                DebugLogger.Log($"[BattleHudHandler] From m_now fields: HP={hp}, MP={mp}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleHudHandler] Failed to read m_now fields: {ex.Message}");
            }

            // Method 2: Try text fields if integers failed or were 0
            if (hp == 0 || mp == 0)
            {
                try
                {
                    var hpTextField = panel.m_hpText;
                    var mpTextField = panel.m_mpText;

                    if (hpTextField != null && !string.IsNullOrEmpty(hpTextField.text))
                    {
                        if (int.TryParse(hpTextField.text, out int parsedHp))
                        {
                            hp = parsedHp;
                            hpSource = "m_hpText";
                        }
                    }
                    if (mpTextField != null && !string.IsNullOrEmpty(mpTextField.text))
                    {
                        if (int.TryParse(mpTextField.text, out int parsedMp))
                        {
                            mp = parsedMp;
                            mpSource = "m_mpText";
                        }
                    }
                    DebugLogger.Log($"[BattleHudHandler] From text fields: HP={hp} ({hpSource}), MP={mp} ({mpSource})");
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Log($"[BattleHudHandler] Failed to read text fields: {ex.Message}");
                }
            }

            // Method 3: Try PartnerCtrl reference
            if (hp == 0 || mp == 0)
            {
                try
                {
                    var partner = panel.m_partner;
                    if (partner != null)
                    {
                        DebugLogger.Log($"[BattleHudHandler] Trying PartnerCtrl reference");
                        // PartnerCtrl extends DigimonCtrl which has Hp property
                        hp = partner.Hp;
                        hpSource = "PartnerCtrl.Hp";
                        DebugLogger.Log($"[BattleHudHandler] From PartnerCtrl: HP={hp}");
                    }
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Log($"[BattleHudHandler] Failed to read from PartnerCtrl: {ex.Message}");
                }
            }

            string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            DebugLogger.Log($"[BattleHudHandler] Final values: HP={hp}, MP={mp}");
            ScreenReader.Say($"{partnerLabel}: HP {hp}, MP {mp}");
        }

        private void AnnouncePartnerOrder(int partnerIndex)
        {
            var panel = GetDigimonPanel(partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string order = "None";
            try
            {
                order = panel.m_orderLabel?.text ?? "None";
                if (string.IsNullOrWhiteSpace(order))
                    order = "None";
            }
            catch { }

            string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            ScreenReader.Say($"{partnerLabel} current order: {order}");
        }

        private void AnnouncePartnerOrderPower(int partnerIndex)
        {
            var panel = GetDigimonPanel(partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            int orderPower = 0;
            try
            {
                orderPower = panel.m_dispOrderPower;
            }
            catch { }

            string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            ScreenReader.Say($"{partnerLabel} Order Power: {orderPower}");
        }

        private void AnnouncePartnerFullStatus(int partnerIndex)
        {
            var panel = GetDigimonPanel(partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say($"Partner {partnerIndex + 1} not available");
                return;
            }

            string name = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            string hpText = "0";
            string mpText = "0";
            string order = "";
            int orderPower = 0;

            try
            {
                hpText = panel.m_hpText?.text ?? panel.m_now_hp.ToString();
                mpText = panel.m_mpText?.text ?? panel.m_now_mp.ToString();
            }
            catch
            {
                hpText = panel.m_now_hp.ToString();
                mpText = panel.m_now_mp.ToString();
            }

            try
            {
                order = panel.m_orderLabel?.text ?? "";
                orderPower = panel.m_dispOrderPower;
            }
            catch { }

            string announcement = $"{name}: HP {hpText}, MP {mpText}";
            if (!string.IsNullOrWhiteSpace(order))
            {
                announcement += $", Order: {order}";
            }
            announcement += $", Power: {orderPower}";

            ScreenReader.Say(announcement);
        }

        public bool IsActive()
        {
            return _cachedBattlePanel != null && _cachedBattlePanel.m_enabled && !IsInSubMenu();
        }
    }
}
