using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle HUD status announcements via keyboard.
    ///
    /// Keyboard:
    /// F3 = Partner 1 full status
    /// F4 = Partner 2 full status
    /// F6 = Partner 1 HP/MP
    /// F7 = Partner 2 HP/MP
    ///
    /// CONTROLLER ATTEMPTS (none worked - for future reference):
    /// - D-pad alone: Game consumes D-pad for targeting/camera
    /// - RB/LB + D-pad: RB/LB open Order Ring during battle
    /// - Select + D-pad: PadManager.IsInput didn't detect Select
    /// - B (Circle) + D-pad: PadManager.IsInput didn't detect it
    /// - L2/R2 triggers: Game uses Steam Input which doesn't expose triggers
    ///   (tried Unity Input.GetAxisRaw - axes not configured in Steam Input)
    /// - Right Stick (srUp/srDown/etc): PadManager.IsTrigger didn't fire
    ///
    /// The game uses Steam Input for controller, which intercepts all input.
    /// PadManager only exposes buttons the game explicitly mapped.
    /// Triggers (L2/R2) are not mapped in the game's Steam Input config.
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
            // F3 = Partner 1 full status
            if (Input.GetKeyDown(KeyCode.F3))
            {
                DebugLogger.Log("[BattleHudHandler] F3 pressed in battle");
                AnnouncePartnerFullStatus(0);
            }

            // F4 = Partner 2 full status
            if (Input.GetKeyDown(KeyCode.F4))
            {
                DebugLogger.Log("[BattleHudHandler] F4 pressed in battle");
                AnnouncePartnerFullStatus(1);
            }

            // F6 = Partner 1 HP/MP only (simpler)
            if (Input.GetKeyDown(KeyCode.F6))
            {
                DebugLogger.Log("[BattleHudHandler] F6 pressed - Partner 1 HP/MP");
                AnnouncePartnerHpMp(0);
            }

            // F7 = Partner 2 HP/MP only (simpler)
            if (Input.GetKeyDown(KeyCode.F7))
            {
                DebugLogger.Log("[BattleHudHandler] F7 pressed - Partner 2 HP/MP");
                AnnouncePartnerHpMp(1);
            }

            // F8 = Debug: log all joystick axes to find trigger mapping
            if (Input.GetKeyDown(KeyCode.F8))
            {
                DebugLogger.Log("[BattleHudHandler] F8 pressed - logging all joystick inputs");
                TriggerInput.DebugLogAllAxes();
                ScreenReader.Say("Logging joystick inputs to debug file");
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
