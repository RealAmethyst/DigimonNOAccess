using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles Order Ring (command wheel) accessibility.
    /// Announces the currently selected command as the player navigates.
    /// </summary>
    public class BattleOrderRingHandler : IAccessibilityHandler
    {
        public int Priority => 84;

        /// <summary>
        /// IAccessibilityHandler.IsOpen() - delegates to IsActive().
        /// </summary>
        public bool IsOpen() => IsActive();

        public void AnnounceStatus()
        {
            ScreenReader.Say("Order Ring");
        }

        private uBattlePanelCommand _cachedCmdPanel;
        private int _lastSelectIndex = -1;
        private int _lastSelectDigimon = -1;
        private bool _wasActive = false;

        public void Update()
        {
            // Check if battle is active first
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                ResetState();
                return;
            }

            // Find the command panel
            var cmdPanel = Object.FindObjectOfType<uBattlePanelCommand>();
            if (cmdPanel == null || !cmdPanel.gameObject.activeInHierarchy)
            {
                if (_wasActive)
                {
                    // Panel just closed
                    ResetState();
                }
                return;
            }

            // Check if actually in selection mode
            if (cmdPanel.m_selectMode == uBattlePanelCommand.SelectMode.None)
            {
                if (_wasActive)
                {
                    ResetState();
                }
                return;
            }

            _cachedCmdPanel = cmdPanel;

            // Panel just opened
            if (!_wasActive)
            {
                _wasActive = true;
                _lastSelectIndex = cmdPanel.m_selectIndex;
                _lastSelectDigimon = cmdPanel.m_selectDigimon;
                AnnounceCurrentSelection(true);
                return;
            }

            // Check for cursor movement
            bool indexChanged = cmdPanel.m_selectIndex != _lastSelectIndex;
            bool partnerChanged = cmdPanel.m_selectDigimon != _lastSelectDigimon;

            if (indexChanged || partnerChanged)
            {
                _lastSelectIndex = cmdPanel.m_selectIndex;
                _lastSelectDigimon = cmdPanel.m_selectDigimon;
                AnnounceCurrentSelection(partnerChanged);
            }
        }

        private void ResetState()
        {
            _cachedCmdPanel = null;
            _lastSelectIndex = -1;
            _lastSelectDigimon = -1;
            _wasActive = false;
        }

        private void AnnounceCurrentSelection(bool includePartner)
        {
            if (_cachedCmdPanel == null)
                return;

            int index = _cachedCmdPanel.m_selectIndex;
            int partner = _cachedCmdPanel.m_selectDigimon;

            string cmdName = GetCurrentCommandName();
            string partnerLabel = partner == 0 ? "Partner 1" : "Partner 2";

            string announcement;
            if (includePartner)
            {
                announcement = $"{partnerLabel}: {cmdName}";
            }
            else
            {
                announcement = cmdName;
            }

            ScreenReader.Say(announcement);
        }

        private string GetCurrentCommandName()
        {
            if (_cachedCmdPanel == null)
                return "Unknown";

            try
            {
                var commands = _cachedCmdPanel.m_command_tbl;
                int index = _cachedCmdPanel.m_selectIndex;

                if (commands != null && index >= 0 && index < commands.Length)
                {
                    var cmd = commands[index];
                    return GetCommandName(cmd);
                }
            }
            catch { }

            return "Unknown Command";
        }

        private string GetCommandName(PartnerAIManager.PartnerCommand cmd)
        {
            return cmd switch
            {
                // Tactical commands
                PartnerAIManager.PartnerCommand.CrossfireAttack => "Crossfire Attack",
                PartnerAIManager.PartnerCommand.ScatteredAttack => "Scattered Attack",
                PartnerAIManager.PartnerCommand.Free => "Fight Freely",
                PartnerAIManager.PartnerCommand.FreeAll => "Both Fight Freely",
                PartnerAIManager.PartnerCommand.Target => "Focus Target",
                PartnerAIManager.PartnerCommand.TargetAll => "Both Focus Target",

                // Direct attacks
                PartnerAIManager.PartnerCommand.Attack1 => "Attack 1",
                PartnerAIManager.PartnerCommand.Attack2 => "Attack 2",
                PartnerAIManager.PartnerCommand.Attack3 => "Attack 3",
                PartnerAIManager.PartnerCommand.Attack4 => "Attack 4",

                // Special attacks
                PartnerAIManager.PartnerCommand.SpAttack => "Special Attack",
                PartnerAIManager.PartnerCommand.SpAttackAll => "Both Special Attack",

                // MP management
                PartnerAIManager.PartnerCommand.ModeratelyAttack => "Conserve MP",
                PartnerAIManager.PartnerCommand.ModeratelyAttackAll => "Both Conserve MP",
                PartnerAIManager.PartnerCommand.UtmostAttack => "Use MP Freely",
                PartnerAIManager.PartnerCommand.UtmostAttackAll => "Both Use MP Freely",

                // Defensive
                PartnerAIManager.PartnerCommand.Guard => "Guard",
                PartnerAIManager.PartnerCommand.GuardAll => "Both Guard",

                // Movement
                PartnerAIManager.PartnerCommand.Approach => "Move Closer",
                PartnerAIManager.PartnerCommand.ApproachAll => "Both Move Closer",
                PartnerAIManager.PartnerCommand.Leave => "Move Away",
                PartnerAIManager.PartnerCommand.LeaveAll => "Both Move Away",

                // Formation
                PartnerAIManager.PartnerCommand.FormationRFrontLBack => "Right Front, Left Back",
                PartnerAIManager.PartnerCommand.FormationLFrontRBack => "Left Front, Right Back",
                PartnerAIManager.PartnerCommand.FormationFree => "Free Formation",

                // Special commands
                PartnerAIManager.PartnerCommand.Escape => "Escape",
                PartnerAIManager.PartnerCommand.Cheer => "Cheer",
                PartnerAIManager.PartnerCommand.Exe => "ExE Attack",

                // Power commands
                PartnerAIManager.PartnerCommand.PowerCommandA => "Power Command A",
                PartnerAIManager.PartnerCommand.PowerCommandB => "Power Command B",
                PartnerAIManager.PartnerCommand.PowerCommandC => "Power Command C",
                PartnerAIManager.PartnerCommand.PowerCommandD => "Power Command D",
                PartnerAIManager.PartnerCommand.HighTensionA => "High Tension",

                // Care commands (shouldn't appear in battle but just in case)
                PartnerAIManager.PartnerCommand.Present => "Give Item",
                PartnerAIManager.PartnerCommand.Praise => "Praise",
                PartnerAIManager.PartnerCommand.Scold => "Scold",
                PartnerAIManager.PartnerCommand.PutToSleep => "Put to Sleep",
                PartnerAIManager.PartnerCommand.Rest => "Rest",

                // None/default
                PartnerAIManager.PartnerCommand.None => "None",

                // Fallback - use enum name
                _ => cmd.ToString().Replace("All", " All")
            };
        }

        public bool IsActive()
        {
            return _wasActive && _cachedCmdPanel != null;
        }
    }
}
