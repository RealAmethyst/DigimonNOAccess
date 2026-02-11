using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles Order Ring (command wheel) accessibility.
    /// Announces the currently selected command with attack name and OP cost
    /// as the player navigates the ring.
    ///
    /// Uses multiple sources for command info:
    /// 1. Game's UI text fields (m_infomation_label, OP labels)
    /// 2. Partner's attack data (ParameterAttackData) for name + OP cost
    /// 3. Hardcoded tactical command names as fallback
    /// </summary>
    public class BattleOrderRingHandler : IAccessibilityHandler
    {
        public int Priority => 84;

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
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                ResetState();
                return;
            }

            var cmdPanel = Object.FindObjectOfType<uBattlePanelCommand>();
            if (cmdPanel == null || !cmdPanel.gameObject.activeInHierarchy)
            {
                if (_wasActive)
                    ResetState();
                return;
            }

            if (cmdPanel.m_selectMode == uBattlePanelCommand.SelectMode.None)
            {
                if (_wasActive)
                    ResetState();
                return;
            }

            _cachedCmdPanel = cmdPanel;

            if (!_wasActive)
            {
                _wasActive = true;
                _lastSelectIndex = cmdPanel.m_selectIndex;
                _lastSelectDigimon = cmdPanel.m_selectDigimon;
                AnnounceCurrentSelection(true);
                return;
            }

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
            if (_cachedCmdPanel == null) return;

            int partner = _cachedCmdPanel.m_selectDigimon;
            string cmdInfo = GetCurrentCommandInfo();

            if (includePartner)
            {
                string partnerName = GetPartnerName(partner);
                ScreenReader.Say($"{partnerName}: {cmdInfo}");
            }
            else
            {
                ScreenReader.Say(cmdInfo);
            }
        }

        private string GetPartnerName(int partnerIndex)
        {
            try
            {
                var partner = MainGameManager.GetPartnerCtrl(partnerIndex);
                if (partner != null)
                {
                    string name = partner.gameData?.m_commonData?.m_name;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                        return TextUtilities.StripRichTextTags(name);
                }
            }
            catch { }

            return PartnerUtilities.GetPartnerLabel(partnerIndex);
        }

        private string GetCurrentCommandInfo()
        {
            if (_cachedCmdPanel == null) return "Unknown";

            int index = _cachedCmdPanel.m_selectIndex;

            // Get the command enum
            PartnerAIManager.PartnerCommand cmd = PartnerAIManager.PartnerCommand.None;
            try
            {
                var commands = _cachedCmdPanel.m_command_tbl;
                if (commands != null && index >= 0 && index < commands.Length)
                    cmd = commands[index];
            }
            catch { }

            // For attack/power commands, get name from UI label + OP from attack data
            int attackSlot = GetAttackSlot(cmd);
            if (attackSlot >= 0)
            {
                string attackInfo = GetAttackInfo(attackSlot);
                if (attackInfo != null) return attackInfo;
            }

            // For non-attack commands, read the UI info label for the name
            string uiInfo = ReadUIInfoLabel();
            if (!string.IsNullOrWhiteSpace(uiInfo))
                return StripAttackTypePrefix(TextUtilities.StripRichTextTags(uiInfo));

            // Fallback: hardcoded command names
            return GetTacticalCommandName(cmd);
        }

        /// <summary>
        /// Read the game's info/description label for the selected command.
        /// </summary>
        private string ReadUIInfoLabel()
        {
            try
            {
                var label = _cachedCmdPanel.m_infomation_label;
                if (label != null)
                {
                    string text = label.text;
                    if (!string.IsNullOrWhiteSpace(text) && !text.Contains("ランゲージ"))
                        return text;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OrderRing] m_infomation_label error: {ex.Message}");
            }

            return null;
        }

        private int GetAttackSlot(PartnerAIManager.PartnerCommand cmd)
        {
            return cmd switch
            {
                // Order ring uses PowerCommand for the 4 attack slots
                PartnerAIManager.PartnerCommand.PowerCommandA => 0,
                PartnerAIManager.PartnerCommand.PowerCommandB => 1,
                PartnerAIManager.PartnerCommand.PowerCommandC => 2,
                PartnerAIManager.PartnerCommand.PowerCommandD => 3,
                PartnerAIManager.PartnerCommand.SpAttack => 4,
                PartnerAIManager.PartnerCommand.HighTensionA => 6,
                // Also map direct attack commands just in case
                PartnerAIManager.PartnerCommand.Attack1 => 0,
                PartnerAIManager.PartnerCommand.Attack2 => 1,
                PartnerAIManager.PartnerCommand.Attack3 => 2,
                PartnerAIManager.PartnerCommand.Attack4 => 3,
                _ => -1
            };
        }

        private string GetAttackInfo(int attackSlot)
        {
            try
            {
                // Get the attack name from UI label (most reliable)
                string name = null;
                string uiLabel = ReadUIInfoLabel();
                if (!string.IsNullOrWhiteSpace(uiLabel))
                    name = StripAttackTypePrefix(TextUtilities.StripRichTextTags(uiLabel));

                // Get OP cost from attack data
                // partner=2 means PartnerAll, use partner 0 for data lookup
                int partnerIndex = _cachedCmdPanel.m_selectDigimon;
                if (partnerIndex > 1) partnerIndex = 0;

                int opCost = 0;
                var partner = MainGameManager.GetPartnerCtrl(partnerIndex);
                if (partner != null)
                {
                    var attackData = partner.gameData?.m_attackData;
                    if (attackData != null && attackSlot < attackData.Length)
                    {
                        var attack = attackData[attackSlot];
                        if (attack != null)
                        {
                            opCost = attack.m_consumptionOP;

                            // If UI label didn't work, try attack data name
                            if (string.IsNullOrEmpty(name))
                            {
                                string dataName = attack.GetName();
                                if (!string.IsNullOrEmpty(dataName) && !dataName.Contains("ランゲージ"))
                                    name = StripAttackTypePrefix(TextUtilities.StripRichTextTags(dataName));
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(name))
                    return null;

                return $"{name}, {opCost} OP";
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Strip the [Type] prefix from attack names, e.g. "[Area]Firewall" -> "Firewall"
        /// </summary>
        private string StripAttackTypePrefix(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;

            // Strip leading [Type] prefix like [Area], [Shot], [Front], etc.
            if (name.StartsWith("["))
            {
                int end = name.IndexOf(']');
                if (end >= 0 && end < name.Length - 1)
                    return name.Substring(end + 1);
            }

            return name;
        }

        private string GetTacticalCommandName(PartnerAIManager.PartnerCommand cmd)
        {
            return cmd switch
            {
                PartnerAIManager.PartnerCommand.CrossfireAttack => "Crossfire Attack",
                PartnerAIManager.PartnerCommand.ScatteredAttack => "Scattered Attack",
                PartnerAIManager.PartnerCommand.Free => "Fight Freely",
                PartnerAIManager.PartnerCommand.FreeAll => "Both Fight Freely",
                PartnerAIManager.PartnerCommand.Target => "Focus Target",
                PartnerAIManager.PartnerCommand.TargetAll => "Both Focus Target",

                PartnerAIManager.PartnerCommand.SpAttackAll => "Both Special Attack",

                PartnerAIManager.PartnerCommand.ModeratelyAttack => "Conserve MP",
                PartnerAIManager.PartnerCommand.ModeratelyAttackAll => "Both Conserve MP",
                PartnerAIManager.PartnerCommand.UtmostAttack => "Use MP Freely",
                PartnerAIManager.PartnerCommand.UtmostAttackAll => "Both Use MP Freely",

                PartnerAIManager.PartnerCommand.Guard => "Guard",
                PartnerAIManager.PartnerCommand.GuardAll => "Both Guard",

                PartnerAIManager.PartnerCommand.Approach => "Move Closer",
                PartnerAIManager.PartnerCommand.ApproachAll => "Both Move Closer",
                PartnerAIManager.PartnerCommand.Leave => "Move Away",
                PartnerAIManager.PartnerCommand.LeaveAll => "Both Move Away",

                PartnerAIManager.PartnerCommand.FormationRFrontLBack => "Right Front, Left Back",
                PartnerAIManager.PartnerCommand.FormationLFrontRBack => "Left Front, Right Back",
                PartnerAIManager.PartnerCommand.FormationFree => "Free Formation",

                PartnerAIManager.PartnerCommand.Escape => "Escape",
                PartnerAIManager.PartnerCommand.Cheer => "Cheer",
                PartnerAIManager.PartnerCommand.Exe => "ExE Attack",

                PartnerAIManager.PartnerCommand.PowerCommandA => "Power Command A",
                PartnerAIManager.PartnerCommand.PowerCommandB => "Power Command B",
                PartnerAIManager.PartnerCommand.PowerCommandC => "Power Command C",
                PartnerAIManager.PartnerCommand.PowerCommandD => "Power Command D",
                PartnerAIManager.PartnerCommand.HighTensionA => "High Tension",

                PartnerAIManager.PartnerCommand.None => "None",
                _ => cmd.ToString()
            };
        }

        public bool IsActive()
        {
            return _wasActive && _cachedCmdPanel != null;
        }
    }
}
