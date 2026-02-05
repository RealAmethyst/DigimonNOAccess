using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Care menu (Square button in field)
    /// </summary>
    public class CarePanelHandler : IAccessibilityHandler
    {
        public int Priority => 62;

        private uCarePanel _carePanel;
        private uCarePanelCommand _commandPanel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uCarePanel.State _lastState = uCarePanel.State.None;
        private MainGameManager.ORDER_UNIT _lastTarget = (MainGameManager.ORDER_UNIT)(-1);

        public bool IsOpen()
        {
            // Access care panel through MainGameManager (the correct way)
            _carePanel = GetCarePanel();

            if (_carePanel == null)
                return false;

            // Check if the care panel is in Main state (command selection)
            if (_carePanel.m_state != uCarePanel.State.Main)
                return false;

            // Get the command panel from the care panel
            _commandPanel = _carePanel.m_commandPanel;

            if (_commandPanel == null)
                return false;

            // Verify the command panel is active
            if (_commandPanel.gameObject == null || !_commandPanel.gameObject.activeInHierarchy)
                return false;

            return true;
        }

        private uCarePanel GetCarePanel()
        {
            try
            {
                // Try via MainGameManager.m_instance.careUI first
                var mgr = MainGameManager.m_instance;
                if (mgr != null)
                {
                    var careUI = mgr.careUI;
                    if (careUI != null)
                    {
                        return careUI;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CarePanel] Error getting careUI: {ex.Message}");
            }

            // Fallback to FindObjectOfType
            return Object.FindObjectOfType<uCarePanel>();
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
                CheckCursorChange();
                CheckTargetChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastTarget = (MainGameManager.ORDER_UNIT)(-1);

            if (_commandPanel == null)
            {
                ScreenReader.Say("Care menu");
                DebugLogger.Log("[CarePanel] Opened but command panel is null");
                return;
            }

            // Get initial target partner
            var currentTarget = GetCurrentTarget();
            _lastTarget = currentTarget;
            string partnerName = GetTargetPartnerName(currentTarget);

            int cursor = _commandPanel.m_selectNo;
            int total = _commandPanel.m_selectMax;
            string itemText = GetCommandName(cursor);

            string announcement;
            if (total > 0)
            {
                announcement = AnnouncementBuilder.MenuOpen("Care menu", itemText, cursor, total);
            }
            else
            {
                announcement = $"Care menu. {itemText}";
            }

            // Add partner name to the announcement
            if (!string.IsNullOrEmpty(partnerName))
            {
                announcement += $", {partnerName}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[CarePanel] Opened, state={_carePanel?.m_state}, cursor={cursor}, total={total}, item={itemText}, target={currentTarget}");
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _carePanel = null;
            _commandPanel = null;
            _lastCursor = -1;
            _lastTarget = (MainGameManager.ORDER_UNIT)(-1);
            DebugLogger.Log("[CarePanel] Closed");
        }

        private void CheckCursorChange()
        {
            if (_commandPanel == null)
                return;

            int cursor = _commandPanel.m_selectNo;

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetCommandName(cursor);
                int total = _commandPanel.m_selectMax;

                string announcement;
                if (total > 0)
                {
                    announcement = AnnouncementBuilder.CursorPosition(itemText, cursor, total);
                }
                else
                {
                    announcement = itemText;
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[CarePanel] Cursor: {itemText} ({cursor + 1}/{total})");
                _lastCursor = cursor;
            }
        }

        private void CheckTargetChange()
        {
            var currentTarget = GetCurrentTarget();

            if (currentTarget != _lastTarget && (int)_lastTarget >= 0)
            {
                string partnerName = GetTargetPartnerName(currentTarget);
                if (!string.IsNullOrEmpty(partnerName))
                {
                    ScreenReader.Say(partnerName);
                    DebugLogger.Log($"[CarePanel] Target changed to {currentTarget}: {partnerName}");
                }
            }

            _lastTarget = currentTarget;
        }

        private MainGameManager.ORDER_UNIT GetCurrentTarget()
        {
            try
            {
                if (_carePanel != null)
                {
                    return _carePanel.m_target;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CarePanel] Error getting target: {ex.Message}");
            }
            return MainGameManager.ORDER_UNIT.Partner00;
        }

        private string GetTargetPartnerName(MainGameManager.ORDER_UNIT target)
        {
            try
            {
                // Get the partner's actual Digimon name from the game
                Il2Cpp.PartnerCtrl partner = null;
                if (target == MainGameManager.ORDER_UNIT.Partner00)
                {
                    partner = MainGameManager.GetPartnerCtrl(0);
                }
                else if (target == MainGameManager.ORDER_UNIT.Partner01)
                {
                    partner = MainGameManager.GetPartnerCtrl(1);
                }

                if (partner != null)
                {
                    // Use gameData.m_commonData.m_name for the actual localized name
                    var commonData = partner.gameData?.m_commonData;
                    if (commonData != null)
                    {
                        var name = commonData.m_name;
                        if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                        {
                            return name;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CarePanel] Error getting partner name: {ex.Message}");
            }

            // Fallback to generic partner label
            return target switch
            {
                MainGameManager.ORDER_UNIT.Partner00 => "Partner 1",
                MainGameManager.ORDER_UNIT.Partner01 => "Partner 2",
                MainGameManager.ORDER_UNIT.PartnerAll => "Both Partners",
                _ => "Partner"
            };
        }

        private string GetCommandName(int index)
        {
            try
            {
                if (_commandPanel == null || index < 0)
                    return "Option";

                // Try to get text from the choice text array
                var choiceText = _commandPanel.m_choiceText;
                if (choiceText != null && index < choiceText.Length)
                {
                    var textComponent = choiceText[index];
                    if (textComponent != null)
                    {
                        string text = textComponent.text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }
                }

                // Fallback to command name array
                var commandNames = _commandPanel.m_command_name;
                if (commandNames != null && index < commandNames.Length)
                {
                    string cmdName = commandNames[index];
                    if (!string.IsNullOrEmpty(cmdName))
                    {
                        return cmdName;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[CarePanel] Error getting command name: {ex.Message}");
            }

            return "Option";
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
            {
                ScreenReader.Say("Care menu");
                return;
            }

            int cursor = _commandPanel.m_selectNo;
            int total = _commandPanel.m_selectMax;
            string itemText = GetCommandName(cursor);

            string announcement = total > 0
                ? AnnouncementBuilder.MenuOpen("Care menu", itemText, cursor, total)
                : $"Care menu. {itemText}";

            ScreenReader.Say(announcement);
        }
    }
}
