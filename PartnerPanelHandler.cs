using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the partner status panel (Digimon stats, moves, tactics, history)
    /// </summary>
    public class PartnerPanelHandler : IAccessibilityHandler
    {
        public int Priority => 68;

        private uPartnerPanel _panel;
        private bool _wasActive = false;
        private uPartnerPanel.State _lastState = uPartnerPanel.State.None;
        private int _lastSelectPartner = -1;
        private int _lastTopCommand = -1;

        public bool IsOpen()
        {
            try
            {
                return uPartnerPanel.IsActive();
            }
            catch
            {
                return false;
            }
        }

        public void Update()
        {
            bool isActive = IsOpen();

            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            else if (isActive)
            {
                CheckStateChange();
                CheckPartnerChange();
                CheckTopCommandChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = uPartnerPanel.State.None;
            _lastSelectPartner = -1;
            _lastTopCommand = -1;

            _panel = uPartnerPanel.Ref;
            if (_panel == null)
                return;

            var state = _panel.m_State;
            _lastState = state;
            int partner = _panel.m_SelectPartner;
            _lastSelectPartner = partner;

            string tabName = GetTabName(state);
            string partnerName = GetPartnerName();

            string announcement = $"Partner, {tabName}";
            if (!string.IsNullOrEmpty(partnerName))
            {
                announcement += $". {partnerName}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[PartnerPanel] Opened, state={state}, partner={partner}");
        }

        private void OnClose()
        {
            _panel = null;
            _lastState = uPartnerPanel.State.None;
            _lastSelectPartner = -1;
            _lastTopCommand = -1;
            DebugLogger.Log("[PartnerPanel] Closed");
        }

        private void CheckStateChange()
        {
            _panel = uPartnerPanel.Ref;
            if (_panel == null)
                return;

            var currentState = _panel.m_State;

            if (currentState != _lastState && currentState != uPartnerPanel.State.None)
            {
                string tabName = GetTabName(currentState);
                string partnerName = GetPartnerName();

                string announcement = tabName;
                if (currentState != uPartnerPanel.State.Top && !string.IsNullOrEmpty(partnerName))
                {
                    announcement += $", {partnerName}";
                }

                // Add content summary for each tab
                string content = GetTabContent(currentState);
                if (!string.IsNullOrEmpty(content))
                {
                    announcement += $". {content}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[PartnerPanel] State changed to {tabName}");
                _lastState = currentState;
                _lastTopCommand = -1; // Reset command tracking when state changes
            }
        }

        private string GetTabContent(uPartnerPanel.State state)
        {
            try
            {
                switch (state)
                {
                    case uPartnerPanel.State.Status:
                        return GetStatusContent();
                    case uPartnerPanel.State.Attack:
                        return GetAttackContent();
                    default:
                        return null;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error getting tab content: {ex.Message}");
                return null;
            }
        }

        private string GetStatusContent()
        {
            var statusPanel = _panel?.m_StatusPanel;
            if (statusPanel == null)
                return null;

            var status = statusPanel.m_Status;
            if (status == null)
                return null;

            var parts = new System.Collections.Generic.List<string>();

            // HP
            string hpCurrent = status.m_HPCurrent?.text;
            string hpMax = status.m_HPMax?.text;
            if (!string.IsNullOrEmpty(hpCurrent) && !string.IsNullOrEmpty(hpMax))
            {
                parts.Add($"HP {hpCurrent} of {hpMax}");
            }

            // MP
            string mpCurrent = status.m_MPCurrent?.text;
            string mpMax = status.m_MPMax?.text;
            if (!string.IsNullOrEmpty(mpCurrent) && !string.IsNullOrEmpty(mpMax))
            {
                parts.Add($"MP {mpCurrent} of {mpMax}");
            }

            // Stats
            string attack = status.m_Attack?.text;
            string defense = status.m_Defense?.text;
            string speed = status.m_Speed?.text;
            string wisdom = status.m_Wisdom?.text;

            if (!string.IsNullOrEmpty(attack))
                parts.Add($"ATK {attack}");
            if (!string.IsNullOrEmpty(defense))
                parts.Add($"DEF {defense}");
            if (!string.IsNullOrEmpty(speed))
                parts.Add($"SPD {speed}");
            if (!string.IsNullOrEmpty(wisdom))
                parts.Add($"WIS {wisdom}");

            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private string GetAttackContent()
        {
            var attackPanel = _panel?.m_AttackPanel;
            if (attackPanel == null)
                return null;

            var command = attackPanel.m_Command;
            if (command == null)
                return null;

            // Try to get caption text which shows skill description
            var caption = command.m_Caption;
            if (caption != null)
            {
                var captionText = caption.m_CaptionText?.text;
                if (!string.IsNullOrEmpty(captionText))
                {
                    return captionText;
                }
            }

            return "Select a skill slot";
        }

        private void CheckPartnerChange()
        {
            _panel = uPartnerPanel.Ref;
            if (_panel == null)
                return;

            int currentPartner = _panel.m_SelectPartner;

            if (currentPartner != _lastSelectPartner && _lastSelectPartner >= 0)
            {
                string partnerName = GetPartnerName();
                string partnerLabel = currentPartner == 0 ? "Partner 1" : "Partner 2";

                string announcement = !string.IsNullOrEmpty(partnerName) ? partnerName : partnerLabel;
                ScreenReader.Say(announcement);
                DebugLogger.Log($"[PartnerPanel] Partner changed to {currentPartner}");
                _lastSelectPartner = currentPartner;
            }
            else if (_lastSelectPartner < 0)
            {
                _lastSelectPartner = currentPartner;
            }
        }

        private void CheckTopCommandChange()
        {
            _panel = uPartnerPanel.Ref;
            if (_panel == null || _panel.m_State != uPartnerPanel.State.Top)
                return;

            var topPanel = _panel.m_TopPanel;
            if (topPanel == null)
                return;

            try
            {
                int currentCommand = topPanel.GetSelectCommand();

                if (currentCommand != _lastTopCommand && _lastTopCommand >= 0)
                {
                    string commandName = GetCommandName(currentCommand);
                    ScreenReader.Say(commandName);
                    DebugLogger.Log($"[PartnerPanel] Command changed to {commandName}");
                }
                _lastTopCommand = currentCommand;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking command: {ex.Message}");
            }
        }

        private string GetTabName(uPartnerPanel.State state)
        {
            return state switch
            {
                uPartnerPanel.State.Top => "Overview",
                uPartnerPanel.State.Status => "Status",
                uPartnerPanel.State.Attack => "Moves",
                uPartnerPanel.State.Tactics => "Tactics",
                uPartnerPanel.State.History => "History",
                _ => "Partner"
            };
        }

        private string GetCommandName(int commandIndex)
        {
            return commandIndex switch
            {
                0 => "Status",
                1 => "Moves",
                2 => "Tactics",
                3 => "History",
                _ => AnnouncementBuilder.FallbackItem("Option", commandIndex)
            };
        }

        private string GetPartnerName()
        {
            try
            {
                // Try to get name from status panel headline
                var statusPanel = _panel?.m_StatusPanel;
                if (statusPanel != null)
                {
                    var headline = statusPanel.m_HeadLine;
                    if (headline != null)
                    {
                        var nameText = headline.m_PartnerNameText;
                        if (nameText != null)
                        {
                            string name = nameText.text;
                            if (!string.IsNullOrEmpty(name))
                            {
                                return name;
                            }
                        }
                    }
                }

                // Fallback to partner number
                int partner = _panel?.m_SelectPartner ?? 0;
                return partner == 0 ? "Partner 1" : "Partner 2";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error getting partner name: {ex.Message}");
                return "";
            }
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            _panel = uPartnerPanel.Ref;
            if (_panel == null)
                return;

            var state = _panel.m_State;
            string tabName = GetTabName(state);
            string partnerName = GetPartnerName();

            string announcement = $"Partner, {tabName}";
            if (!string.IsNullOrEmpty(partnerName))
            {
                announcement += $". {partnerName}";
            }

            ScreenReader.Say(announcement);
        }
    }
}
