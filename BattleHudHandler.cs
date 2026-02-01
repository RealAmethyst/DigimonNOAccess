using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle HUD status announcements via controller combos.
    /// Uses the same D-pad pattern as FieldHudHandler for consistency.
    ///
    /// Controller layout (D-pad only, no modifiers):
    /// D-Up = Partner 1 HP and MP
    /// D-Down = Partner 2 HP and MP
    /// D-Left = Partner 1 current order
    /// D-Right = Partner 2 current order
    ///
    /// Keyboard fallback:
    /// F3 = Partner 1 full status
    /// F4 = Partner 2 full status
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

            // Only respond if we're not in a sub-menu (item, dialog, etc.)
            if (IsInSubMenu())
            {
                return;
            }

            // Handle keyboard input
            HandleKeyboardInput();

            // Handle controller input
            HandleControllerInput();
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
                AnnouncePartnerFullStatus(0);
            }

            // F4 = Partner 2 full status
            if (Input.GetKeyDown(KeyCode.F4))
            {
                AnnouncePartnerFullStatus(1);
            }
        }

        private void HandleControllerInput()
        {
            // D-pad only controls (no shoulder buttons needed in battle)
            // D-Up = Partner 1 HP/MP
            // D-Down = Partner 2 HP/MP
            // D-Left = Partner 1 order
            // D-Right = Partner 2 order

            // Check all D-pad buttons at once to capture the state
            bool dUp = PadManager.IsTrigger(PadManager.BUTTON.dUp);
            bool dDown = PadManager.IsTrigger(PadManager.BUTTON.dDown);
            bool dLeft = PadManager.IsTrigger(PadManager.BUTTON.dLeft);
            bool dRight = PadManager.IsTrigger(PadManager.BUTTON.dRight);

            if (dUp)
            {
                DebugLogger.Log("[BattleHudHandler] D-Up pressed - Partner 1 HP/MP");
                AnnouncePartnerHpMp(0);
            }
            else if (dDown)
            {
                DebugLogger.Log("[BattleHudHandler] D-Down pressed - Partner 2 HP/MP");
                AnnouncePartnerHpMp(1);
            }
            else if (dLeft)
            {
                DebugLogger.Log("[BattleHudHandler] D-Left pressed - Partner 1 order");
                AnnouncePartnerOrder(0);
            }
            else if (dRight)
            {
                DebugLogger.Log("[BattleHudHandler] D-Right pressed - Partner 2 order");
                AnnouncePartnerOrder(1);
            }
        }

        private void HandlePartnerInput(int partnerIndex)
        {
            // D-Up = HP and MP
            if (PadManager.IsTrigger(PadManager.BUTTON.dUp))
            {
                DebugLogger.Log($"[BattleHudHandler] D-Up triggered for partner {partnerIndex}");
                AnnouncePartnerHpMp(partnerIndex);
            }
            // D-Right = Current order/command
            else if (PadManager.IsTrigger(PadManager.BUTTON.dRight))
            {
                DebugLogger.Log($"[BattleHudHandler] D-Right triggered for partner {partnerIndex}");
                AnnouncePartnerOrder(partnerIndex);
            }
            // D-Down = Order Power level
            else if (PadManager.IsTrigger(PadManager.BUTTON.dDown))
            {
                DebugLogger.Log($"[BattleHudHandler] D-Down triggered for partner {partnerIndex}");
                AnnouncePartnerOrderPower(partnerIndex);
            }
            // D-Left = Name
            else if (PadManager.IsTrigger(PadManager.BUTTON.dLeft))
            {
                DebugLogger.Log($"[BattleHudHandler] D-Left triggered for partner {partnerIndex}");
                AnnouncePartnerName(partnerIndex);
            }
        }

        private uBattlePanelDigimon GetDigimonPanel(int index)
        {
            try
            {
                var panels = _cachedBattlePanel?.m_digimon;
                if (panels != null && index < panels.Length)
                {
                    return panels[index];
                }
            }
            catch { }

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

            // Get HP and MP from text fields or numeric values
            string hpText = "0";
            string mpText = "0";

            try
            {
                hpText = panel.m_hpText?.text ?? panel.m_now_hp.ToString();
                mpText = panel.m_mpText?.text ?? panel.m_now_mp.ToString();
                DebugLogger.Log($"[BattleHudHandler] Partner {partnerIndex}: HP={hpText}, MP={mpText}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleHudHandler] Exception reading HP/MP: {ex.Message}");
                hpText = panel.m_now_hp.ToString();
                mpText = panel.m_now_mp.ToString();
            }

            string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
            ScreenReader.Say($"{partnerLabel}: HP {hpText}, MP {mpText}");
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
