using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the farm panel (farm goods management).
    /// Tracks both the outer uFarmPanel (confirmation flow) and inner
    /// uFarmPanelCommand (slot selection + item selection).
    /// </summary>
    public class FarmPanelHandler : IAccessibilityHandler
    {
        private const string LogTag = "[FarmPanel]";
        public int Priority => 60;

        // Debug flag for slot cursor detection (set true to diagnose)
        private static bool _debugSlotCursor = false;

        private uFarmPanel _farmPanel;
        private uFarmPanelCommand _command;
        private bool _wasActive;

        // Outer panel state tracking
        private uFarmPanel.State _lastFarmState = uFarmPanel.State.None;

        // Command panel state tracking
        private uFarmPanelCommand.State _lastCommandState = uFarmPanelCommand.State.None;
        private int _lastSlotCursor = -1;

        // Item selection tracking (when in Item state)
        private int _lastItemSelectNo = -1;

        public bool IsOpen()
        {
            try
            {
                // Find uFarmPanelCommand first (reliable via FindObjectOfType),
                // then get the parent uFarmPanel from it
                if (_command == null)
                    _command = Object.FindObjectOfType<uFarmPanelCommand>();
                if (_command == null)
                    return false;

                if (_command.gameObject == null || !_command.gameObject.activeInHierarchy)
                    return false;

                // Get the parent uFarmPanel from the command
                if (_farmPanel == null)
                    _farmPanel = _command.m_farmPanel;

                // Check command state for basic open detection
                var cmdState = _command.m_state;
                if (cmdState != uFarmPanelCommand.State.None)
                    return true;

                // Also check parent panel state (covers confirmation phases
                // before the command panel activates)
                if (_farmPanel != null)
                {
                    var farmState = _farmPanel.m_state;
                    return farmState != uFarmPanel.State.None &&
                           farmState != uFarmPanel.State.Close;
                }

                return false;
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
                OnOpen();
            else if (!isActive && _wasActive)
                OnClose();
            else if (isActive)
                OnUpdate();
            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCommandState = uFarmPanelCommand.State.None;
            _lastSlotCursor = -1;
            _lastItemSelectNo = -1;

            // Record current states but don't announce yet.
            // The panel typically opens in Wait state before quickly transitioning
            // to the interactive state. Let OnUpdate() handle the first real announcement
            // so we don't say "Please wait" for a split second.
            _lastFarmState = _farmPanel != null ? _farmPanel.m_state : uFarmPanel.State.None;
            _lastCommandState = _command != null ? _command.m_state : uFarmPanelCommand.State.None;

            DebugLogger.Log($"{LogTag} Panel opened, farmState={_lastFarmState}, cmdState={_lastCommandState}");
        }

        private void OnClose()
        {
            _farmPanel = null;
            _command = null;
            _wasActive = false;
            _lastFarmState = uFarmPanel.State.None;
            _lastCommandState = uFarmPanelCommand.State.None;
            _lastSlotCursor = -1;
            _lastItemSelectNo = -1;
            DebugLogger.Log($"{LogTag} Panel closed");
        }

        private void OnUpdate()
        {
            if (_command == null)
                return;

            // Track outer panel state if available
            if (_farmPanel != null)
            {
                var farmState = _farmPanel.m_state;

                if (farmState != _lastFarmState)
                {
                    var prevState = _lastFarmState;
                    _lastFarmState = farmState;
                    _lastCommandState = uFarmPanelCommand.State.None;
                    _lastSlotCursor = -1;
                    _lastItemSelectNo = -1;

                    if (farmState == uFarmPanel.State.CommandMain)
                    {
                        var cmdState = _command.m_state;
                        _lastCommandState = cmdState;
                        // Include "Farm." prefix on first real interactive state
                        AnnounceCommandState(cmdState, true);
                    }
                    else if (farmState != uFarmPanel.State.Wait)
                    {
                        // Don't announce transient Wait states
                        string stateText = GetFarmStateText(farmState);
                        ScreenReader.Say(stateText);
                    }
                    DebugLogger.Log($"{LogTag} Farm state changed to {farmState}");
                    return;
                }

                // Only track command panel in CommandMain
                if (farmState != uFarmPanel.State.CommandMain &&
                    farmState != uFarmPanel.State.None)
                    return;
            }

            // Track command panel state changes
            if (_command == null)
                return;

            var currentCmdState = _command.m_state;

            // Command state changed
            if (currentCmdState != _lastCommandState)
            {
                _lastCommandState = currentCmdState;
                _lastSlotCursor = -1;
                _lastItemSelectNo = -1;
                // Skip announcing transient Wait states between Main↔Item
                if (currentCmdState != uFarmPanelCommand.State.Wait)
                    AnnounceCommandState(currentCmdState, false);
                DebugLogger.Log($"{LogTag} Command state changed to {currentCmdState}");
                return;
            }

            // Track cursor depending on current command state
            if (currentCmdState == uFarmPanelCommand.State.Main)
                CheckSlotCursorChange();
            else if (currentCmdState == uFarmPanelCommand.State.Item)
                CheckItemSelectionChange();
        }

        private void AnnounceCommandState(uFarmPanelCommand.State cmdState, bool includeMenuName)
        {
            string prefix = includeMenuName ? "Farm. " : "";

            if (cmdState == uFarmPanelCommand.State.Main)
            {
                int cursor = GetSlotCursor();
                string slotText = GetSlotDescription(cursor);
                int total = GetSlotCount();
                _lastSlotCursor = cursor;
                ScreenReader.Say($"{prefix}Select farm slot. {slotText}, {cursor + 1} of {total}");
            }
            else if (cmdState == uFarmPanelCommand.State.Item)
            {
                int selectNo = GetItemSelectNo();
                string itemText = GetSelectedItemName();
                int total = GetItemCount();
                _lastItemSelectNo = selectNo;
                ScreenReader.Say($"{prefix}Select seed. {itemText}, {selectNo + 1} of {total}");
            }
            else if (cmdState == uFarmPanelCommand.State.Wait)
            {
                ScreenReader.Say($"{prefix}Please wait");
            }
        }

        private void CheckSlotCursorChange()
        {
            int cursor = GetSlotCursor();
            if (cursor == _lastSlotCursor || cursor < 0)
                return;

            string slotText = GetSlotDescription(cursor);
            int total = GetSlotCount();
            ScreenReader.Say(AnnouncementBuilder.CursorPosition(slotText, cursor, total));
            DebugLogger.Log($"{LogTag} Slot cursor: {slotText}");
            _lastSlotCursor = cursor;
        }

        private void CheckItemSelectionChange()
        {
            int selectNo = GetItemSelectNo();
            if (selectNo == _lastItemSelectNo || selectNo < 0)
                return;

            _lastItemSelectNo = selectNo;
            string itemText = GetSelectedItemName();
            int total = GetItemCount();
            ScreenReader.Say(AnnouncementBuilder.CursorPosition(itemText, selectNo, total));
            DebugLogger.Log($"{LogTag} Item selection: {itemText}");
        }

        // --- Slot data reading ---

        private int GetSlotCursor()
        {
            try
            {
                var contents = _command?.m_farmContents;
                int activeCursorSlot = -1;
                int farmCursorIndex = -1;

                // Method 1: scan m_farmContents for active m_cursor GameObject
                if (contents != null)
                {
                    for (int i = 0; i < contents.Length; i++)
                    {
                        var content = contents[i];
                        if (content?.m_cursor != null && content.m_cursor.activeSelf)
                        {
                            activeCursorSlot = i;
                            break;
                        }
                    }
                }

                // Method 2: FarmCursor.index
                var cursor = _command?.m_farmCursor;
                if (cursor != null)
                    farmCursorIndex = cursor.index;

                // Log both values so we can see which one works
                if (_debugSlotCursor)
                    DebugLogger.Log($"{LogTag} SlotCursor debug: activeCursorSlot={activeCursorSlot}, farmCursorIndex={farmCursorIndex}");

                // Prefer the active cursor scan, fall back to FarmCursor
                if (activeCursorSlot >= 0)
                    return activeCursorSlot;
                if (farmCursorIndex >= 0)
                    return farmCursorIndex;
            }
            catch { }
            return 0;
        }

        private int GetSlotCount()
        {
            try
            {
                var contents = _command?.m_farmContents;
                if (contents != null)
                    return contents.Length;
            }
            catch { }
            return 1;
        }

        private string GetSlotDescription(int index)
        {
            try
            {
                // Try reading from FarmData for reliable info
                var farmData = uFarmPanel.GetFarmData(index);
                if (farmData != null && farmData.m_id != 0)
                {
                    // Slot has something planted - get name from ParameterFarmData
                    string name = GetFarmItemName(farmData.m_id);
                    string condition = ParameterFarmData.GetCondition(farmData.m_condition);
                    int harvestCount = farmData.m_pick_num;

                    // Build description with all available info
                    string desc = name;
                    if (!string.IsNullOrEmpty(condition))
                        desc += $", {condition}";

                    // Add time/day info from UI (game formats this)
                    string dayText = GetSlotDayText(index);
                    if (!string.IsNullOrEmpty(dayText))
                        desc += $", {dayText}";

                    if (harvestCount > 0)
                        desc += $", harvest {harvestCount}";

                    return desc;
                }
                else
                {
                    // Empty slot
                    string emptyName = ParameterFarmData.GetEmptyName();
                    return !string.IsNullOrEmpty(emptyName) ? emptyName : "Empty";
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading slot {index}: {ex.Message}");
            }

            // Fallback: try UI text
            return GetSlotTextFromUI(index);
        }

        private string GetFarmItemName(uint itemId)
        {
            try
            {
                if (_farmPanel != null)
                {
                    var paramData = _farmPanel.GetParameterFarmData(itemId);
                    if (paramData != null)
                        return paramData.GetName();
                }
            }
            catch { }

            return $"Item {itemId}";
        }

        private string GetSlotDayText(int index)
        {
            try
            {
                var contents = _command?.m_farmContents;
                if (contents != null && index >= 0 && index < contents.Length)
                {
                    var content = contents[index];
                    string dayText = null;
                    string timeText = null;

                    if (content?.m_day != null && !string.IsNullOrEmpty(content.m_day.text))
                        dayText = content.m_day.text.Trim();
                    if (content?.m_time != null && !string.IsNullOrEmpty(content.m_time.text))
                        timeText = content.m_time.text.Trim();

                    // Format days with proper singular/plural
                    string formattedDays = FormatDays(dayText);

                    if (!string.IsNullOrEmpty(formattedDays) && !string.IsNullOrEmpty(timeText))
                        return $"{formattedDays} at {timeText}";
                    if (!string.IsNullOrEmpty(formattedDays))
                        return formattedDays;
                    if (!string.IsNullOrEmpty(timeText))
                        return timeText;
                }
            }
            catch { }
            return null;
        }

        private string FormatDays(string dayText)
        {
            if (string.IsNullOrEmpty(dayText))
                return null;

            if (int.TryParse(dayText, out int days))
                return days == 1 ? "1 day" : $"{days} days";

            return $"{dayText} days";
        }


        private string GetSlotTextFromUI(int index)
        {
            try
            {
                var contents = _command?.m_farmContents;
                if (contents != null && index >= 0 && index < contents.Length)
                {
                    var content = contents[index];
                    if (content?.m_name != null && !string.IsNullOrEmpty(content.m_name.text))
                        return content.m_name.text;
                }
            }
            catch { }
            return AnnouncementBuilder.FallbackItem("Farm slot", index);
        }

        // --- Item selection reading (seed list) ---

        private FarmItem GetScrollView()
        {
            try
            {
                return _command?.m_farmPanelItem?.m_itemScrollView;
            }
            catch { }
            return null;
        }

        private int GetItemSelectNo()
        {
            try
            {
                var scrollView = GetScrollView();
                if (scrollView != null)
                    return scrollView.m_selectNo;
            }
            catch { }
            return -1;
        }

        private int GetItemCount()
        {
            try
            {
                var scrollView = GetScrollView();
                if (scrollView?.m_itemList != null)
                    return scrollView.m_itemList.Count;
            }
            catch { }
            return 1;
        }

        private string GetSelectedItemName()
        {
            try
            {
                var scrollView = GetScrollView();
                if (scrollView != null)
                {
                    // Use GetSelectItemParam (from uItemBase) for the name
                    var paramItem = scrollView.GetSelectItemParam();
                    if (paramItem != null)
                    {
                        string name = paramItem.GetName();
                        if (!string.IsNullOrEmpty(name))
                        {
                            // Get quantity from the selected ItemData
                            var selectItem = scrollView.m_selectItem;
                            if (selectItem != null)
                            {
                                int count = selectItem.m_itemNum;
                                return count > 0 ? $"{name}, have {count}" : name;
                            }
                            return name;
                        }
                    }

                    // Fallback: try reading from uItemParts UI text
                    int selectNo = scrollView.m_selectNo;
                    var parts = scrollView.GetSelectItemParts(selectNo);
                    if (parts?.m_name != null && !string.IsNullOrEmpty(parts.m_name.text))
                        return TextUtilities.StripRichTextTags(parts.m_name.text);
                }

                // Last fallback: try caption text
                var caption = _command?.m_farmPanelItem?.m_itemCaption;
                if (caption?.m_caption != null && !string.IsNullOrEmpty(caption.m_caption.text))
                    return caption.m_caption.text;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading item selection: {ex.Message}");
            }
            return "Unknown item";
        }

        // --- Helpers ---

        private string GetFarmStateText(uFarmPanel.State state)
        {
            switch (state)
            {
                case uFarmPanel.State.ConfirmationInit:
                case uFarmPanel.State.ConfirmationMain:
                    return "Confirm";
                case uFarmPanel.State.CommandMain:
                    return "Select farm slot";
                case uFarmPanel.State.Wait:
                    return "Please wait";
                default:
                    return "Farm";
            }
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            if (_command != null && _command.m_state != uFarmPanelCommand.State.None)
            {
                AnnounceCommandState(_command.m_state, true);
                return;
            }

            if (_farmPanel != null)
            {
                string stateText = GetFarmStateText(_farmPanel.m_state);
                ScreenReader.Say($"Farm. {stateText}");
            }
            else
            {
                ScreenReader.Say("Farm");
            }
        }
    }
}
