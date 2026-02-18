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
    ///   F6 = Enemy 1 info
    ///   F7 = Enemy 2 info
    ///   F11 = Enemy 3 info
    ///   F12 = Order Power
    ///
    /// Controller (via SDL3):
    ///   RStickUp = Enemy 1 info
    ///   RStickDown = Enemy 2 info
    ///   RStickLeft = Enemy 3 info
    ///   RStickRight = Order Power
    ///   RT+DPadLeft = Last SP charge warning
    /// </summary>
    public class BattleHudHandler : IAccessibilityHandler
    {
        public int Priority => 90;

        /// <summary>
        /// IAccessibilityHandler.IsOpen() - delegates to IsActive().
        /// </summary>
        public bool IsOpen() => IsActive();

        public void AnnounceStatus()
        {
            ScreenReader.Say("In battle. Hold RB plus D-pad for Partner 1, LB plus D-pad for Partner 2");
        }

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
            // Partner 1 full status (F3 or controller binding)
            if (ModInputManager.IsActionTriggered("Partner1Status"))
            {
                AnnouncePartnerFullStatus(0);
            }

            // Partner 2 full status (F4 or controller binding)
            if (ModInputManager.IsActionTriggered("Partner2Status"))
            {
                AnnouncePartnerFullStatus(1);
            }

            // Per-enemy info (F6/F7/F11 or RStick directions)
            if (ModInputManager.IsActionTriggered("BattleEnemy1"))
            {
                string info = BattleMonitorHandler.GetEnemyInfoByIndex(0);
                ScreenReader.Say(info);
            }

            if (ModInputManager.IsActionTriggered("BattleEnemy2"))
            {
                string info = BattleMonitorHandler.GetEnemyInfoByIndex(1);
                ScreenReader.Say(info);
            }

            if (ModInputManager.IsActionTriggered("BattleEnemy3"))
            {
                string info = BattleMonitorHandler.GetEnemyInfoByIndex(2);
                ScreenReader.Say(info);
            }

            // Order Power (F12 or RStickRight)
            if (ModInputManager.IsActionTriggered("BattleOrderPower"))
            {
                string info = BattleMonitorHandler.GetOrderPower();
                ScreenReader.Say(info);
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

        private void AnnouncePartnerFullStatus(int partnerIndex)
        {
            var panel = GetDigimonPanel(partnerIndex);
            if (panel == null)
            {
                ScreenReader.Say(PartnerUtilities.GetPartnerNotAvailableMessage(partnerIndex));
                return;
            }

            string name = MainGameManager.GetPartnerCtrl(partnerIndex)?.Name
                          ?? PartnerUtilities.GetPartnerLabel(partnerIndex);
            string hpText = "0";
            string mpText = "0";
            string order = "";
            int orderPower = 0;

            try
            {
                hpText = panel.m_hpText?.text ?? panel.m_now_hp.ToString();
                mpText = panel.m_mpText?.text ?? panel.m_now_mp.ToString();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleHudHandler] Error reading HP/MP text: {ex.Message}");
                hpText = panel.m_now_hp.ToString();
                mpText = panel.m_now_mp.ToString();
            }

            try
            {
                order = panel.m_orderLabel?.text ?? "";
                orderPower = panel.m_dispOrderPower;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleHudHandler] Error reading order/power: {ex.Message}");
            }

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
