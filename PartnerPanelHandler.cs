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
        private uPartnerTopPanel.PartnerTopState _lastTopPanelState = uPartnerTopPanel.PartnerTopState.None;

        // Cursor tracking for subpanels
        private int _lastAttackCursor = -1;
        private int _lastTacticsCursorX = -1;
        private int _lastTacticsCursorY = -1;
        private int _lastHistoryCursorX = -1;
        private int _lastHistoryCursorY = -1;

        // Skill select submenu tracking
        private uPartnerAttackPanel.PartnerAttackState _lastAttackPanelState = uPartnerAttackPanel.PartnerAttackState.None;
        private int _lastSelectSkillX = -1;
        private int _lastSelectSkillY = -1;

        private const int TOP_COMMAND_COUNT = 4; // Status, Moves, Tactics, History

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
                CheckTopPanelStateChange();
                CheckTopCommandChange();
                CheckSubpanelCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = uPartnerPanel.State.None;
            _lastSelectPartner = -1;
            ResetSubpanelCursors();

            _panel = uPartnerPanel.Ref;
            if (_panel == null)
                return;

            var state = _panel.m_State;
            _lastState = state;
            int partner = _panel.m_SelectPartner;
            _lastSelectPartner = partner;

            // Initialize top command and top panel state so checks
            // don't immediately interrupt this announcement
            _lastTopCommand = -1;
            _lastTopPanelState = uPartnerTopPanel.PartnerTopState.None;
            if (state == uPartnerPanel.State.Top)
            {
                try
                {
                    var topPanel = _panel.m_TopPanel;
                    if (topPanel != null)
                    {
                        _lastTopCommand = topPanel.GetSelectCommand();
                        _lastTopPanelState = topPanel.m_State;
                    }
                }
                catch { }
            }

            string announcement;
            if (state == uPartnerPanel.State.Top)
            {
                // At Top level, announce panel name and current cursor position
                string cmdName = GetCommandName(_lastTopCommand >= 0 ? _lastTopCommand : 0);
                announcement = $"Partner. {cmdName}, {(_lastTopCommand >= 0 ? _lastTopCommand : 0) + 1} of {TOP_COMMAND_COUNT}";
            }
            else
            {
                // Opening directly into a subpanel - include digimon name
                string tabName = GetTabName(state);
                string partnerName = GetPartnerName();
                announcement = $"Partner, {tabName}";
                if (!string.IsNullOrEmpty(partnerName))
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
            _lastTopPanelState = uPartnerTopPanel.PartnerTopState.None;
            ResetSubpanelCursors();
            DebugLogger.Log("[PartnerPanel] Closed");
        }

        private void ResetSubpanelCursors()
        {
            _lastAttackCursor = -1;
            _lastTacticsCursorX = -1;
            _lastTacticsCursorY = -1;
            _lastHistoryCursorX = -1;
            _lastHistoryCursorY = -1;
            _lastAttackPanelState = uPartnerAttackPanel.PartnerAttackState.None;
            _lastSelectSkillX = -1;
            _lastSelectSkillY = -1;
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

                string announcement;

                if (_lastState == uPartnerPanel.State.Top && currentState != uPartnerPanel.State.Top)
                {
                    // Entering a subpanel from Top - announce digimon name and tab
                    if (!string.IsNullOrEmpty(partnerName))
                        announcement = $"{partnerName}, {tabName}";
                    else
                        announcement = tabName;

                    // Add content summary for the tab
                    string content = GetTabContent(currentState);
                    if (!string.IsNullOrEmpty(content))
                        announcement += $". {content}";
                }
                else if (currentState == uPartnerPanel.State.Top)
                {
                    // Returning to Top from a subpanel - user is at partner selection
                    if (!string.IsNullOrEmpty(partnerName))
                        announcement = partnerName;
                    else
                        announcement = "Partner 1";

                    // Sync top panel state so CheckTopPanelStateChange doesn't double-announce
                    try
                    {
                        var topPanel = _panel.m_TopPanel;
                        if (topPanel != null)
                        {
                            _lastTopCommand = topPanel.GetSelectCommand();
                            _lastTopPanelState = topPanel.m_State;
                        }
                    }
                    catch { }
                }
                else
                {
                    // Switching between subpanels
                    announcement = tabName;
                    if (!string.IsNullOrEmpty(partnerName))
                        announcement += $", {partnerName}";

                    string content = GetTabContent(currentState);
                    if (!string.IsNullOrEmpty(content))
                        announcement += $". {content}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[PartnerPanel] State changed to {tabName}, announcement: {announcement}");
                _lastState = currentState;
                if (currentState != uPartnerPanel.State.Top)
                    _lastTopCommand = -1;
                ResetSubpanelCursors();
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
                    case uPartnerPanel.State.Tactics:
                        return GetTacticsContent();
                    case uPartnerPanel.State.History:
                        return GetHistoryContent();
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

            // Initialize cursor tracking
            int cursorIndex = 0;
            try
            {
                cursorIndex = command.m_SelectNo;
                _lastAttackCursor = cursorIndex;
            }
            catch { }

            // Get full info for current slot including Power/MP/Range
            var skillCtrl = command?.m_SkillCtrl;
            if (skillCtrl?.m_Skill != null && cursorIndex >= 0 && cursorIndex < skillCtrl.m_Skill.Length)
            {
                var skillUI = skillCtrl.m_Skill[cursorIndex];
                string fullInfo = GetSkillFullInfo(skillUI);
                if (!string.IsNullOrEmpty(fullInfo))
                {
                    // Also include description from caption
                    string description = null;
                    var caption = command.m_Caption;
                    if (caption != null)
                    {
                        var captionText = caption.m_CaptionText?.text;
                        if (!string.IsNullOrEmpty(captionText))
                            description = TextUtilities.StripRichTextTags(captionText);
                    }
                    if (!string.IsNullOrEmpty(description))
                        return $"{fullInfo}. {description}";
                    return fullInfo;
                }
            }

            return "No skill set";
        }

        private string GetSkillName(Il2Cpp.uPartnerAttackPanelCommand command, int cursorIndex)
        {
            try
            {
                var skillCtrl = command?.m_SkillCtrl;
                if (skillCtrl == null)
                    return null;

                var skillArray = skillCtrl.m_Skill;
                if (skillArray == null || cursorIndex < 0 || cursorIndex >= skillArray.Length)
                    return null;

                var skillUI = skillArray[cursorIndex];
                if (skillUI == null)
                    return null;

                // Check if this slot actually has a skill assigned
                if (skillUI.m_SkillCode == 0)
                    return null;

                var nameText = skillUI.m_SkillName;
                if (nameText != null)
                {
                    string name = nameText.text;
                    if (!string.IsNullOrEmpty(name))
                    {
                        return TextUtilities.StripRichTextTags(name);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error getting skill name: {ex.Message}");
            }
            return null;
        }

        private string GetSkillFullInfo(Il2Cpp.uPartnerAttackPanelSkill skillUI)
        {
            try
            {
                if (skillUI == null)
                    return null;

                uint code = skillUI.m_SkillCode;
                if (code == 0)
                    return null;

                string name = skillUI.m_SkillName?.text;
                if (string.IsNullOrEmpty(name))
                    return null;
                name = TextUtilities.StripRichTextTags(name);

                var parts = new System.Collections.Generic.List<string>();
                parts.Add(name);

                string power = skillUI.m_Power?.text;
                if (!string.IsNullOrEmpty(power))
                    parts.Add($"Power {power}");

                string mp = skillUI.m_MP?.text;
                if (!string.IsNullOrEmpty(mp))
                    parts.Add($"MP {mp}");

                // Get range from game data since it's displayed as an image
                try
                {
                    var attackData = Il2Cpp.ParameterAttackData.GetParam(code);
                    if (attackData != null)
                    {
                        string range = GetRangeLetter(attackData.m_range);
                        if (!string.IsNullOrEmpty(range))
                            parts.Add($"Range {range}");
                    }
                }
                catch { }

                return string.Join(", ", parts);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error getting skill full info: {ex.Message}");
                return null;
            }
        }

        private string GetRangeLetter(int rangeIndex)
        {
            return rangeIndex switch
            {
                0 => "E",
                1 => "D",
                2 => "C",
                3 => "B",
                4 => "A",
                5 => "S",
                _ => ""
            };
        }

        private string GetTacticsContent()
        {
            var tacticsPanel = _panel?.m_Tactics;
            if (tacticsPanel == null)
                return null;

            var order = tacticsPanel.m_Order;
            if (order == null)
                return null;

            // Initialize cursor tracking
            try
            {
                _lastTacticsCursorX = order.m_selectCursorX;
                _lastTacticsCursorY = order.m_selectCursorY;
            }
            catch { }

            // Get the description of the currently selected tactic
            var description = order.m_description;
            if (description != null)
            {
                string text = description.text;
                if (!string.IsNullOrEmpty(text))
                {
                    return TextUtilities.StripRichTextTags(text);
                }
            }

            return "Select a battle tactic";
        }

        private string GetHistoryContent()
        {
            var historyPanel = _panel?.m_HistoryPanel;
            if (historyPanel == null)
                return null;

            var genealogy = historyPanel.m_Genelogy;
            if (genealogy == null)
                return null;

            // Initialize cursor tracking
            try
            {
                _lastHistoryCursorX = genealogy.m_CursorX;
                _lastHistoryCursorY = genealogy.m_CursorY;
            }
            catch { }

            var parts = new System.Collections.Generic.List<string>();

            // Get current digimon info from the info panel
            string digimonInfo = GetHistoryDigimonInfo(genealogy);
            if (!string.IsNullOrEmpty(digimonInfo))
                parts.Add(digimonInfo);

            // Get generation info
            var generationText = genealogy.m_GenerationText;
            if (generationText != null)
            {
                string gen = generationText.text;
                if (!string.IsNullOrEmpty(gen))
                {
                    parts.Add($"Generation {gen}");
                }
            }

            // Get time spent info
            var spendDayText = genealogy.m_SpendDayText;
            if (spendDayText != null)
            {
                string days = spendDayText.text;
                if (!string.IsNullOrEmpty(days))
                {
                    parts.Add($"{days} days");
                }
            }

            // Get evolution count
            int historyCount = genealogy.m_HistoryCnt;
            if (historyCount > 0)
            {
                parts.Add($"{historyCount} evolutions");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : "Evolution history";
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

                // When in a subpanel (not Top), also re-announce content
                var state = _panel.m_State;
                if (state != uPartnerPanel.State.Top && state != uPartnerPanel.State.None)
                {
                    string content = GetTabContent(state);
                    if (!string.IsNullOrEmpty(content))
                    {
                        announcement += $". {content}";
                    }
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[PartnerPanel] Partner changed to {currentPartner}");
                _lastSelectPartner = currentPartner;
            }
            else if (_lastSelectPartner < 0)
            {
                _lastSelectPartner = currentPartner;
            }
        }

        private void CheckTopPanelStateChange()
        {
            _panel = uPartnerPanel.Ref;
            if (_panel == null || _panel.m_State != uPartnerPanel.State.Top)
                return;

            var topPanel = _panel.m_TopPanel;
            if (topPanel == null)
                return;

            try
            {
                var currentTopState = topPanel.m_State;

                if (currentTopState != _lastTopPanelState)
                {
                    // CommandSelect → PartnerSelect: user confirmed a command, now choosing partner
                    if (currentTopState == uPartnerTopPanel.PartnerTopState.PartnerSelect)
                    {
                        string partnerName = GetPartnerName();
                        if (!string.IsNullOrEmpty(partnerName))
                        {
                            ScreenReader.Say(partnerName);
                            DebugLogger.Log($"[PartnerPanel] Entered partner select: {partnerName}");
                        }
                    }
                    // PartnerSelect → CommandSelect: user backed out to command selection
                    else if (currentTopState == uPartnerTopPanel.PartnerTopState.CommandSelect &&
                             _lastTopPanelState == uPartnerTopPanel.PartnerTopState.PartnerSelect)
                    {
                        int currentCommand = topPanel.GetSelectCommand();
                        string commandName = GetCommandName(currentCommand);
                        string announcement = AnnouncementBuilder.CursorPosition(commandName, currentCommand, TOP_COMMAND_COUNT);
                        ScreenReader.Say(announcement);
                        DebugLogger.Log($"[PartnerPanel] Returned to command select: {commandName}");
                        // Sync to prevent CheckTopCommandChange from double-announcing
                        _lastTopCommand = currentCommand;
                    }

                    _lastTopPanelState = currentTopState;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking top panel state: {ex.Message}");
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

                if (currentCommand != _lastTopCommand)
                {
                    string commandName = GetCommandName(currentCommand);
                    // Add position announcement
                    string announcement = AnnouncementBuilder.CursorPosition(commandName, currentCommand, TOP_COMMAND_COUNT);
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"[PartnerPanel] Command changed to {commandName}");
                }
                _lastTopCommand = currentCommand;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking command: {ex.Message}");
            }
        }

        private void CheckSubpanelCursorChange()
        {
            _panel = uPartnerPanel.Ref;
            if (_panel == null)
                return;

            var state = _panel.m_State;

            switch (state)
            {
                case uPartnerPanel.State.Attack:
                    CheckAttackCursorChange();
                    break;
                case uPartnerPanel.State.Tactics:
                    CheckTacticsCursorChange();
                    break;
                case uPartnerPanel.State.History:
                    CheckHistoryCursorChange();
                    break;
            }
        }

        private void CheckAttackCursorChange()
        {
            var attackPanel = _panel?.m_AttackPanel;
            if (attackPanel == null)
                return;

            var command = attackPanel.m_Command;
            if (command == null)
                return;

            try
            {
                // Check for attack panel state changes (Normal vs SelectSkill)
                var currentAttackState = attackPanel.m_State;
                if (currentAttackState != _lastAttackPanelState)
                {
                    if (currentAttackState == uPartnerAttackPanel.PartnerAttackState.SelectSkill)
                    {
                        // Entered skill select mode - initialize cursor tracking
                        try
                        {
                            _lastSelectSkillX = command.m_SelectSkillX;
                            _lastSelectSkillY = command.m_SelectSkillY;
                        }
                        catch { _lastSelectSkillX = 0; _lastSelectSkillY = 0; }

                        // Force caption update and read initial skill name
                        try { command.SetSelectSkillCaption(); } catch { }
                        string skillName = GetSelectSkillName(command);

                        // Check if current slot already has a skill (replacing) or is empty (assigning)
                        bool isReplacing = false;
                        try
                        {
                            int slotIndex = command.m_SelectNo;
                            var skillCtrl = command.m_SkillCtrl;
                            if (skillCtrl != null)
                            {
                                uint slotCode = skillCtrl.GetSkillCode(slotIndex);
                                isReplacing = slotCode > 0;
                            }
                        }
                        catch { }

                        string announcement = isReplacing ? "Select replacement skill" : "Select skill";
                        if (!string.IsNullOrEmpty(skillName))
                        {
                            int entryCol = _lastSelectSkillX + 1;
                            int entryRow = _lastSelectSkillY + 1;
                            announcement += $". {skillName}, row {entryRow} of 7, column {entryCol} of 9";
                        }
                        ScreenReader.Say(announcement);
                        DebugLogger.Log($"[PartnerPanel] Entered skill select mode: {skillName}");
                    }
                    else if (_lastAttackPanelState == uPartnerAttackPanel.PartnerAttackState.SelectSkill &&
                             (currentAttackState == uPartnerAttackPanel.PartnerAttackState.Nomal ||
                              currentAttackState == uPartnerAttackPanel.PartnerAttackState.NomalPrepare))
                    {
                        // Exited skill select mode back to normal - re-announce current slot
                        _lastSelectSkillX = -1;
                        _lastSelectSkillY = -1;

                        try
                        {
                            int slotIndex = command.m_SelectNo;
                            var skillCtrl = command.m_SkillCtrl;
                            int totalSlots = 4;
                            try
                            {
                                if (skillCtrl?.m_Skill != null)
                                    totalSlots = skillCtrl.m_Skill.Length;
                            }
                            catch { }

                            string fullInfo = null;
                            if (skillCtrl?.m_Skill != null && slotIndex >= 0 && slotIndex < skillCtrl.m_Skill.Length)
                                fullInfo = GetSkillFullInfo(skillCtrl.m_Skill[slotIndex]);

                            string slotAnnouncement;
                            if (!string.IsNullOrEmpty(fullInfo))
                            {
                                slotAnnouncement = $"{fullInfo}, {slotIndex + 1} of {totalSlots}";
                                string desc = null;
                                var caption = command.m_Caption;
                                if (caption != null)
                                {
                                    var captionText = caption.m_CaptionText?.text;
                                    if (!string.IsNullOrEmpty(captionText))
                                        desc = TextUtilities.StripRichTextTags(captionText);
                                }
                                if (!string.IsNullOrEmpty(desc))
                                    slotAnnouncement += $". {desc}";
                            }
                            else
                            {
                                slotAnnouncement = $"No skill set, {slotIndex + 1} of {totalSlots}";
                            }
                            ScreenReader.Say(slotAnnouncement);
                        }
                        catch { }

                        DebugLogger.Log("[PartnerPanel] Exited skill select mode");
                    }
                    _lastAttackPanelState = currentAttackState;
                }

                // If in SelectSkill state, track cursor position changes
                if (currentAttackState == uPartnerAttackPanel.PartnerAttackState.SelectSkill)
                {
                    int curX = command.m_SelectSkillX;
                    int curY = command.m_SelectSkillY;

                    if (curX != _lastSelectSkillX || curY != _lastSelectSkillY)
                    {
                        if (_lastSelectSkillX >= 0)
                        {
                            // Force the game to update the caption for the new cursor position
                            try { command.SetSelectSkillCaption(); } catch { }

                            string currentSkillName = GetSelectSkillName(command) ?? "";
                            if (!string.IsNullOrEmpty(currentSkillName))
                            {
                                string posInfo = $"row {curY + 1} of 7, column {curX + 1} of 9";
                                ScreenReader.Say($"{currentSkillName}, {posInfo}");
                                DebugLogger.Log($"[PartnerPanel] Skill select cursor ({curX},{curY}): {currentSkillName}");
                            }
                        }
                        _lastSelectSkillX = curX;
                        _lastSelectSkillY = curY;
                    }
                    return; // Don't check normal cursor when in skill select mode
                }

                // Normal mode cursor tracking
                int currentCursor = command.m_SelectNo;

                if (currentCursor != _lastAttackCursor && _lastAttackCursor >= 0)
                {
                    // Get total slots
                    int totalSlots = 4;
                    var skillCtrl = command.m_SkillCtrl;
                    try
                    {
                        if (skillCtrl?.m_Skill != null)
                            totalSlots = skillCtrl.m_Skill.Length;
                    }
                    catch { }

                    // Get full skill info with Power/MP/Range
                    string fullInfo = null;
                    if (skillCtrl?.m_Skill != null && currentCursor >= 0 && currentCursor < skillCtrl.m_Skill.Length)
                    {
                        fullInfo = GetSkillFullInfo(skillCtrl.m_Skill[currentCursor]);
                    }

                    string content;
                    if (!string.IsNullOrEmpty(fullInfo))
                    {
                        content = $"{fullInfo}, {currentCursor + 1} of {totalSlots}";

                        // Add caption description
                        string description = null;
                        var caption = command.m_Caption;
                        if (caption != null)
                        {
                            var captionText = caption.m_CaptionText?.text;
                            if (!string.IsNullOrEmpty(captionText))
                                description = TextUtilities.StripRichTextTags(captionText);
                        }
                        if (!string.IsNullOrEmpty(description))
                            content += $". {description}";
                    }
                    else
                    {
                        // Check if slot has a lock message (like "Unlocked by Acquiring Third Order")
                        string lockMsg = null;
                        var caption = command.m_Caption;
                        if (caption != null)
                        {
                            var captionText = caption.m_CaptionText?.text;
                            if (!string.IsNullOrEmpty(captionText))
                                lockMsg = TextUtilities.StripRichTextTags(captionText);
                        }
                        if (!string.IsNullOrEmpty(lockMsg))
                            content = $"No skill set, {currentCursor + 1} of {totalSlots}. {lockMsg}";
                        else
                            content = $"No skill set, {currentCursor + 1} of {totalSlots}";
                    }

                    ScreenReader.Say(content);
                    DebugLogger.Log($"[PartnerPanel] Attack cursor changed to {currentCursor}: {content}");
                }
                _lastAttackCursor = currentCursor;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking attack cursor: {ex.Message}");
            }
        }

        private string GetSelectSkillName(Il2Cpp.uPartnerAttackPanelCommand command)
        {
            try
            {
                // m_SelectSkillCaption holds the selected skill details in the selection grid
                var selectSkillCaption = command?.m_SelectSkillCaption;
                if (selectSkillCaption == null) return null;

                uint skillCode = selectSkillCaption.m_SkillCode;
                if (skillCode == 0) return "Empty";

                // Read the caption description - the game shows "???" for locked/unknown skills
                // and the actual description for available ones
                string captionDesc = null;
                try
                {
                    var caption = command.m_Caption;
                    if (caption != null)
                    {
                        var captionText = caption.m_CaptionText?.text;
                        if (!string.IsNullOrEmpty(captionText))
                            captionDesc = TextUtilities.StripRichTextTags(captionText);
                    }
                }
                catch { }

                // "???" means the skill is locked/unknown
                if (string.IsNullOrEmpty(captionDesc) || captionDesc == "???")
                    return "Locked";

                var skillNameText = selectSkillCaption.m_SkillName;
                if (skillNameText == null) return null;
                string name = skillNameText.text;
                if (string.IsNullOrEmpty(name)) return null;
                name = TextUtilities.StripRichTextTags(name);

                // Check if the current digimon can use this skill
                // The attack flag is indexed by grid position in column-major order (column * 7 + row)
                bool canUse = false;
                try
                {
                    int partner = command.m_SelectPartner;
                    Il2Cpp.CBitFlag attackFlag = partner == 0 ? command.m_L_AttackFlag : command.m_R_AttackFlag;
                    if (attackFlag != null)
                    {
                        uint gridIndex = (uint)(command.m_SelectSkillX * 7 + command.m_SelectSkillY);
                        canUse = attackFlag[gridIndex];
                    }
                }
                catch { }

                if (!canUse)
                    return $"{name}, not usable. {captionDesc}";

                return $"{name}. {captionDesc}";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error getting select skill name: {ex.Message}");
            }
            return null;
        }

        private void CheckTacticsCursorChange()
        {
            var tacticsPanel = _panel?.m_Tactics;
            if (tacticsPanel == null)
                return;

            var order = tacticsPanel.m_Order;
            if (order == null)
                return;

            try
            {
                int currentX = order.m_selectCursorX;
                int currentY = order.m_selectCursorY;

                if ((currentX != _lastTacticsCursorX || currentY != _lastTacticsCursorY) &&
                    _lastTacticsCursorX >= 0 && _lastTacticsCursorY >= 0)
                {
                    // Get the tactics description for the new cursor position
                    string content = GetTacticsContent();
                    if (!string.IsNullOrEmpty(content))
                    {
                        ScreenReader.Say(content);
                        DebugLogger.Log($"[PartnerPanel] Tactics cursor changed to ({currentX},{currentY}): {content}");
                    }
                }
                _lastTacticsCursorX = currentX;
                _lastTacticsCursorY = currentY;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking tactics cursor: {ex.Message}");
            }
        }

        private void CheckHistoryCursorChange()
        {
            var historyPanel = _panel?.m_HistoryPanel;
            if (historyPanel == null)
                return;

            var genealogy = historyPanel.m_Genelogy;
            if (genealogy == null)
                return;

            try
            {
                int currentX = genealogy.m_CursorX;
                int currentY = genealogy.m_CursorY;

                if ((currentX != _lastHistoryCursorX || currentY != _lastHistoryCursorY) &&
                    _lastHistoryCursorX >= 0 && _lastHistoryCursorY >= 0)
                {
                    // Get full digimon info (name + nature + attribute + description)
                    string digimonInfo = GetHistoryDigimonInfo(genealogy);

                    // X is generation (column), Y is the evolution branch (row)
                    int generation = currentX + 1;
                    int maxGen = genealogy.m_CursorX_Max + 1;
                    int branch = currentY + 1;
                    int maxBranch = genealogy.m_CursorY_Max + 1;

                    var parts = new System.Collections.Generic.List<string>();

                    if (!string.IsNullOrEmpty(digimonInfo))
                    {
                        parts.Add(digimonInfo);
                    }

                    if (maxBranch > 1)
                    {
                        parts.Add($"Generation {generation} of {maxGen}, Branch {branch} of {maxBranch}");
                    }
                    else
                    {
                        parts.Add($"Generation {generation} of {maxGen}");
                    }

                    string announcement = string.Join(", ", parts);
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"[PartnerPanel] History cursor changed to ({currentX},{currentY}): {digimonInfo}");
                }
                _lastHistoryCursorX = currentX;
                _lastHistoryCursorY = currentY;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking history cursor: {ex.Message}");
            }
        }

        private bool IsValidHistoryPosition(Il2Cpp.uHistoryUI genealogy, out string name)
        {
            name = null;
            try
            {
                int cursorX = genealogy.m_CursorX;
                int historyCnt = genealogy.m_HistoryCnt;

                // Only positions 0..historyCnt-1 are valid (includes current digimon at historyCnt-1)
                // Positions >= historyCnt are future/unknown slots
                var history = genealogy.m_History;
                if (history != null && cursorX >= 0 && cursorX < historyCnt && cursorX < history.Length)
                {
                    uint digimonId = history[cursorX].m_DigimonID;
                    if (digimonId > 0)
                    {
                        var param = ParameterDigimonData.GetParam(digimonId);
                        if (param != null)
                        {
                            name = param.GetDefaultName();
                            return true;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error checking history position: {ex.Message}");
            }
            return false;
        }

        private string GetHistoryDigimonInfo(Il2Cpp.uHistoryUI genealogy)
        {
            try
            {
                // Check if the game itself considers this position unknown
                try
                {
                    var infoUI = genealogy?.m_GenelogyInfo;
                    if (infoUI?.m_Unknown != null && infoUI.m_Unknown.activeInHierarchy)
                        return "Unknown";
                }
                catch { }

                // Only show full info for positions on the actual evolution path
                if (!IsValidHistoryPosition(genealogy, out string digimonName))
                    return "Unknown";

                var genelogyInfo = genealogy?.m_GenelogyInfo;

                var parts = new System.Collections.Generic.List<string>();

                if (!string.IsNullOrEmpty(digimonName))
                    parts.Add(digimonName);

                if (genelogyInfo != null)
                {
                    // Get nature (e.g. "Dark")
                    var natureText = genelogyInfo.m_Nature;
                    if (natureText != null)
                    {
                        string nature = natureText.text;
                        if (!string.IsNullOrEmpty(nature) && !nature.Contains("ランゲージ"))
                            parts.Add($"Nature {TextUtilities.StripRichTextTags(nature)}");
                    }

                    // Get attribute (e.g. "Virus")
                    var propertyText = genelogyInfo.m_Property;
                    if (propertyText != null)
                    {
                        string property = propertyText.text;
                        if (!string.IsNullOrEmpty(property) && !property.Contains("ランゲージ"))
                            parts.Add($"Attribute {TextUtilities.StripRichTextTags(property)}");
                    }

                    // Get description
                    var descText = genelogyInfo.m_Description;
                    if (descText != null)
                    {
                        string desc = descText.text;
                        if (!string.IsNullOrEmpty(desc) && !desc.Contains("ランゲージ"))
                        {
                            string cleanDesc = TextUtilities.StripRichTextTags(desc);
                            if (!string.IsNullOrEmpty(cleanDesc))
                                parts.Add(cleanDesc);
                        }
                    }
                }

                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[PartnerPanel] Error getting history digimon info: {ex.Message}");
                return null;
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
                // m_SelectPartner index is inverted relative to GetPartnerCtrl
                int partner = _panel?.m_SelectPartner ?? 0;
                int partnerCtrlIndex = 1 - partner;
                var partnerCtrl = MainGameManager.GetPartnerCtrl(partnerCtrlIndex);
                if (partnerCtrl != null)
                {
                    var commonData = partnerCtrl.gameData?.m_commonData;
                    if (commonData != null)
                    {
                        string name = commonData.m_name;
                        if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                        {
                            return name;
                        }
                    }
                }

                // Fallback to partner number
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
