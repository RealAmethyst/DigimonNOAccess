using Il2Cpp;
using UnityEngine;
using UnityEngine.UI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Care menu (Square button in field)
    /// and Camp menu (rest at camp sites). Both share uCarePanel as base class.
    /// Care panel lives on MainGameManager.careUI.
    /// Camp panel lives on MainGameTent.m_campPanel (separate instance).
    /// Camp cooking is handled by RestaurantPanelHandler (same uRestaurantPanel class).
    /// </summary>
    public class CarePanelHandler : IAccessibilityHandler
    {
        public int Priority => 62;

        private const string LogTag = "[CarePanel]";

        private uCarePanel _carePanel;
        private uCampPanel _campPanel;
        private bool _wasActive;
        private int _lastCursor = -1;
        private MainGameManager.ORDER_UNIT _lastTarget = (MainGameManager.ORDER_UNIT)(-1);

        private bool IsCampMode => _campPanel != null;
        private string MenuName => IsCampMode ? "Camp Menu" : "Care Menu";

        public bool IsOpen()
        {
            _carePanel = null;
            _campPanel = null;

            // Try camp panel first via FindObjectOfType (it lives on MainGameTent, not careUI)
            try
            {
                var campPanel = Object.FindObjectOfType<uCampPanel>();
                if (campPanel != null)
                {
                    var state = campPanel.m_state;
                    if (state == uCarePanel.State.Main)
                    {
                        var cmd = campPanel.m_command;
                        if (cmd?.gameObject != null && cmd.gameObject.activeInHierarchy)
                        {
                            _campPanel = campPanel;
                            _carePanel = campPanel;
                            return true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error finding camp panel: {ex.Message}");
            }

            // Try care panel via MainGameManager.careUI
            try
            {
                var mgr = MainGameManager.m_instance;
                if (mgr != null)
                {
                    var careUI = mgr.careUI;
                    if (careUI != null && careUI.TryCast<uCampPanel>() == null)
                        _carePanel = careUI;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting careUI: {ex.Message}");
            }

            if (_carePanel == null)
            {
                var found = Object.FindObjectOfType<uCarePanel>();
                if (found != null && found.TryCast<uCampPanel>() == null)
                    _carePanel = found;
            }

            if (_carePanel == null)
                return false;

            if (_carePanel.m_state != uCarePanel.State.Main)
                return false;

            var cmdPanel = _carePanel.m_commandPanel;
            if (cmdPanel?.gameObject == null || !cmdPanel.gameObject.activeInHierarchy)
                return false;

            return true;
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
            int cursor = GetCursorPosition();
            int total = GetMenuItemCount();
            string itemText = GetCommandName(cursor);

            var currentTarget = GetCurrentTarget();
            _lastTarget = currentTarget;
            string partnerName = GetTargetPartnerName(currentTarget);

            string announcement = total > 0
                ? AnnouncementBuilder.MenuOpen(MenuName, itemText, cursor, total)
                : $"{MenuName}. {itemText}";

            if (!string.IsNullOrEmpty(partnerName))
                announcement += $", {partnerName}";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} {MenuName} opened, cursor={cursor}, total={total}, item={itemText}, target={currentTarget}");
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _carePanel = null;
            _campPanel = null;
            _lastCursor = -1;
            _lastTarget = (MainGameManager.ORDER_UNIT)(-1);
            DebugLogger.Log($"{LogTag} Closed");
        }

        private void CheckCursorChange()
        {
            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetCommandName(cursor);
                int total = GetMenuItemCount();

                string announcement = total > 0
                    ? AnnouncementBuilder.CursorPosition(itemText, cursor, total)
                    : itemText;

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Cursor: {itemText} ({cursor + 1}/{total})");
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
                    DebugLogger.Log($"{LogTag} Target changed to {currentTarget}: {partnerName}");
                }
            }

            _lastTarget = currentTarget;
        }

        private int GetCursorPosition()
        {
            try
            {
                if (IsCampMode)
                {
                    // SimpleCursor is a 2D grid - use GetSelectNo() for the flattened index
                    var cursor = _campPanel.m_command?.m_cusror;
                    if (cursor != null)
                        return cursor.GetSelectNo();
                }
                else
                {
                    var cmd = _carePanel?.m_commandPanel;
                    if (cmd != null)
                        return cmd.m_selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private int GetMenuItemCount()
        {
            try
            {
                if (IsCampMode)
                {
                    // Count visible commands (active GameObjects)
                    var commands = _campPanel.m_command?.m_commands;
                    if (commands != null)
                    {
                        int count = 0;
                        for (int i = 0; i < commands.Length; i++)
                        {
                            if (commands[i] != null && commands[i].activeSelf)
                                count++;
                        }
                        return count;
                    }
                }
                else
                {
                    var cmd = _carePanel?.m_commandPanel;
                    if (cmd != null)
                        return cmd.m_selectMax;
                }
            }
            catch { }
            return 0;
        }

        private string GetCommandName(int index)
        {
            try
            {
                if (index < 0)
                    return "Option";

                if (IsCampMode)
                {
                    // Read text from the command GameObject directly.
                    // m_commandText array doesn't align with GetSelectNo() indices
                    // since SimpleCursor uses a 2D grid layout.
                    var commands = _campPanel.m_command?.m_commands;
                    if (commands != null && index < commands.Length)
                    {
                        var cmdObj = commands[index];
                        if (cmdObj != null)
                        {
                            var textComp = cmdObj.GetComponentInChildren<Text>();
                            if (textComp != null && !string.IsNullOrEmpty(textComp.text))
                                return textComp.text;
                        }
                    }
                }
                else
                {
                    var cmd = _carePanel?.m_commandPanel;
                    if (cmd != null)
                    {
                        var choiceText = cmd.m_choiceText;
                        if (choiceText != null && index < choiceText.Length)
                        {
                            var text = choiceText[index]?.text;
                            if (!string.IsNullOrEmpty(text))
                                return text;
                        }

                        var commandNames = cmd.m_command_name;
                        if (commandNames != null && index < commandNames.Length)
                        {
                            string cmdName = commandNames[index];
                            if (!string.IsNullOrEmpty(cmdName))
                                return cmdName;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting command name: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Option", index);
        }

        private MainGameManager.ORDER_UNIT GetCurrentTarget()
        {
            try
            {
                if (_carePanel != null)
                    return _carePanel.m_target;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting target: {ex.Message}");
            }
            return MainGameManager.ORDER_UNIT.Partner00;
        }

        private string GetTargetPartnerName(MainGameManager.ORDER_UNIT target)
        {
            try
            {
                int partnerIndex = target == MainGameManager.ORDER_UNIT.Partner01 ? 1 : 0;
                if (target == MainGameManager.ORDER_UNIT.PartnerAll)
                    return "Both Partners";

                var partner = MainGameManager.GetPartnerCtrl(partnerIndex);
                if (partner != null)
                {
                    var name = partner.gameData?.m_commonData?.m_name;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("\u30E9\u30F3\u30B2\u30FC\u30B8"))
                        return name;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting partner name: {ex.Message}");
            }

            return target switch
            {
                MainGameManager.ORDER_UNIT.Partner00 => "Partner 1",
                MainGameManager.ORDER_UNIT.Partner01 => "Partner 2",
                MainGameManager.ORDER_UNIT.PartnerAll => "Both Partners",
                _ => "Partner"
            };
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = GetCursorPosition();
            int total = GetMenuItemCount();
            string itemText = GetCommandName(cursor);
            string partnerName = GetTargetPartnerName(GetCurrentTarget());

            string announcement = total > 0
                ? AnnouncementBuilder.MenuOpen(MenuName, itemText, cursor, total)
                : $"{MenuName}. {itemText}";

            if (!string.IsNullOrEmpty(partnerName))
                announcement += $", {partnerName}";

            ScreenReader.Say(announcement);
        }
    }
}
