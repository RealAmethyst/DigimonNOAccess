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
        private string MenuName => GetMenuName();

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
            string fallback = index < 0
                ? "Option"
                : AnnouncementBuilder.FallbackItem("Option", index);

            try
            {
                if (index < 0)
                    return fallback;

                if (IsCampMode)
                {
                    // Read text from the command GameObject directly.
                    // m_commandText array doesn't align with GetSelectNo() indices
                    // since SimpleCursor uses a 2D grid layout.
                    if (_campPanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_campPanel was null");
                        return fallback;
                    }

                    var commandPanel = _campPanel.m_command;
                    if (commandPanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_command was null");
                        return fallback;
                    }

                    var commands = commandPanel.m_commands;
                    if (commands == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_commands was null");
                        return fallback;
                    }

                    if (index >= commands.Length)
                    {
                        DebugLogger.Log($"{LogTag} Command name: index {index} was outside m_commands length {commands.Length}");
                        return fallback;
                    }

                    var cmdObj = commands[index];
                    if (cmdObj == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_commands[{index}] was null");
                        return fallback;
                    }

                    var textComp = cmdObj.GetComponentInChildren<Text>();
                    return GetRenderedText(textComp, $"m_commands[{index}] child Text.text", fallback);
                }
                else
                {
                    if (_carePanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_carePanel was null");
                        return fallback;
                    }

                    var commandPanel = _carePanel.m_commandPanel;
                    if (commandPanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_commandPanel was null");
                        return fallback;
                    }

                    var choiceText = commandPanel.m_choiceText;
                    if (choiceText == null)
                    {
                        DebugLogger.Log($"{LogTag} Command name: m_choiceText was null");
                        return fallback;
                    }

                    if (index >= choiceText.Length)
                    {
                        DebugLogger.Log($"{LogTag} Command name: index {index} was outside m_choiceText length {choiceText.Length}");
                        return fallback;
                    }

                    return GetRenderedText(choiceText[index], $"m_choiceText[{index}].text", fallback);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting command name: {ex.Message}");
            }

            return fallback;
        }

        private string GetMenuName()
        {
            string fallback = IsCampMode ? "Camp Menu" : "Care Menu";
            try
            {
                if (_carePanel == null)
                {
                    DebugLogger.Log($"{LogTag} Menu name: m_carePanel was null");
                    return fallback;
                }

                var captionPanel = _carePanel.m_captionPanel;
                if (captionPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Menu name: m_captionPanel was null");
                    return fallback;
                }

                return GetRenderedText(captionPanel.m_text, "m_captionPanel.m_text.text", fallback);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Menu name read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetRenderedText(Text textComponent, string fieldName, string fallback)
        {
            if (textComponent == null)
            {
                DebugLogger.Log($"{LogTag} {fieldName} was null");
                return fallback;
            }

            try
            {
                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(textComponent.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"{LogTag} {fieldName} unusable: {TextUtilities.DescribeUnusable(cleaned)}");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} {fieldName} read failed: {ex.Message}");
                return fallback;
            }
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
