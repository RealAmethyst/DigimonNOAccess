using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the save/load menu.
    /// Uses the parent uSavePanel state as the primary IsOpen() authority because
    /// uSavePanelCommand.m_State goes to NONE during yes/no dialogs and save operations,
    /// while uSavePanel stays active through the entire flow.
    /// </summary>
    public class SavePanelHandler : IAccessibilityHandler
    {
        public int Priority => 58;

        private uSavePanelCommand _panel;
        private uSavePanel _parentPanel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uSavePanel.State _lastParentState = uSavePanel.State.NONE;
        private string _lastMessageWindowText = "";

        // Small delay so "Loading data" message can be detected first
        private const float OpenAnnouncementDelay = 0.05f;
        private float _openTime = 0f;
        private bool _pendingOpenAnnouncement = false;

        public bool IsOpen()
        {
            // Find parent panel - this is the authority for the save/load lifecycle.
            // The command panel (uSavePanelCommand) goes to NONE during yes/no dialogs
            // and save operations, but the parent stays active.
            if (_parentPanel == null)
                _parentPanel = Object.FindObjectOfType<uSavePanel>();

            if (_parentPanel != null)
            {
                var parentState = _parentPanel.m_State;
                if (parentState != uSavePanel.State.NONE &&
                    parentState != uSavePanel.State.CLOSE &&
                    parentState != uSavePanel.State.END)
                {
                    // Parent is active - get command panel from parent's direct reference.
                    // FindObjectOfType won't find it when its gameObject is deactivated
                    // during save operations, but the parent's reference persists.
                    if (_panel == null)
                        _panel = _parentPanel.m_uLoadPanelCommand;
                    return true;
                }
            }

            // Fallback: check command panel directly
            if (_panel == null)
                _panel = Object.FindObjectOfType<uSavePanelCommand>();

            if (_panel != null)
            {
                var state = _panel.m_State;
                if (state == uSavePanelCommand.State.MAIN ||
                    state == uSavePanelCommand.State.SAVE_CHECK ||
                    state == uSavePanelCommand.State.LOAD_CHECK ||
                    state == uSavePanelCommand.State.SAVE ||
                    state == uSavePanelCommand.State.LOAD)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// True when the player can navigate save slots.
        /// Uses the parent's MAIN_SETTING state as the authority - this is the parent's
        /// "slot list is active and accepting input" state. The command panel's m_State
        /// may stay in NONE after save operations even though the slot list is functional.
        /// </summary>
        private bool IsNavigable()
        {
            if (_panel == null) return false;
            if (_parentPanel == null) return false;
            return _parentPanel.m_State == uSavePanel.State.MAIN_SETTING;
        }

        private static bool IsParentSaveLoadState(uSavePanel.State state)
        {
            return state == uSavePanel.State.SAVE ||
                   state == uSavePanel.State.POST_WAIT ||
                   state == uSavePanel.State.SYSTEM_SAVE ||
                   state == uSavePanel.State.SAVE_END ||
                   state == uSavePanel.State.LOAD;
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
                // Check if pending open announcement is ready
                if (_pendingOpenAnnouncement && UnityEngine.Time.time - _openTime >= OpenAnnouncementDelay)
                {
                    AnnounceCurrentSlot();
                    _pendingOpenAnnouncement = false;
                }

                // Track parent panel state and message window text
                CheckParentState();

                // Only track cursor when navigable
                if (IsNavigable())
                {
                    CheckCursorChange();
                }
            }

            _wasActive = isActive;
        }

        /// <summary>
        /// Track the parent uSavePanel state machine and its message window.
        /// </summary>
        private void CheckParentState()
        {
            try
            {
                if (_parentPanel == null)
                    return;

                var parentState = _parentPanel.m_State;

                // Monitor message window text changes for announcements
                CheckParentMessageWindow();

                if (parentState != _lastParentState)
                {
                    DebugLogger.Log($"[SavePanel] Parent state: {_lastParentState} -> {parentState}");

                    // When parent exits a save/load operation back to MAIN_SETTING,
                    // force cursor re-announcement on the next navigable frame
                    if (parentState == uSavePanel.State.MAIN_SETTING &&
                        IsParentSaveLoadState(_lastParentState))
                    {
                        _lastCursor = -1; // Force re-announcement
                        DebugLogger.Log("[SavePanel] Save/load complete, will re-announce slot");
                    }

                    _lastParentState = parentState;
                }
            }
            catch { }
        }

        /// <summary>
        /// Monitor the parent panel's message window for text changes.
        /// Catches auto-closing messages like "Save complete" that may not go through SetMessage.
        /// </summary>
        private void CheckParentMessageWindow()
        {
            try
            {
                var msgWindow = _parentPanel.m_messageWindow;
                if (msgWindow == null)
                    return;

                if (!msgWindow.m_isOpend)
                {
                    _lastMessageWindowText = "";
                    return;
                }

                var label = msgWindow.m_label;
                if (label == null)
                    return;

                string text = label.text;
                if (string.IsNullOrEmpty(text) || text == _lastMessageWindowText)
                    return;

                _lastMessageWindowText = text;

                if (TextUtilities.IsPlaceholderText(text))
                    return;

                // Only announce if SetMessage patch didn't already catch it
                if (DialogTextPatch.WasRecentlyAnnounced(text))
                    return;

                string cleaned = TextUtilities.CleanText(text);
                if (!string.IsNullOrEmpty(cleaned))
                {
                    ScreenReader.Say(cleaned);
                    DebugLogger.Log($"[SavePanel] Message window: {cleaned}");
                }
            }
            catch { }
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastParentState = uSavePanel.State.NONE;
            _lastMessageWindowText = "";
            _openTime = UnityEngine.Time.time;
            _pendingOpenAnnouncement = true;

            // Cache parent panel
            if (_parentPanel == null)
                _parentPanel = Object.FindObjectOfType<uSavePanel>();
            if (_parentPanel != null)
                _lastParentState = _parentPanel.m_State;

            // Cache command panel and initial cursor
            if (_panel == null)
                _panel = Object.FindObjectOfType<uSavePanelCommand>();
            if (_panel != null)
                _lastCursor = GetCursorPosition();

            DebugLogger.Log($"[SavePanel] Opened, parent={_lastParentState}, cursor={_lastCursor}");
        }

        private void OnClose()
        {
            _panel = null;
            _parentPanel = null;
            _lastCursor = -1;
            _lastParentState = uSavePanel.State.NONE;
            _lastMessageWindowText = "";
            _pendingOpenAnnouncement = false;
            DebugLogger.Log("[SavePanel] Closed");
        }

        private void AnnounceCurrentSlot()
        {
            if (_panel == null)
                return;

            // Only announce slot info when parent is in navigable state
            if (_parentPanel == null || _parentPanel.m_State != uSavePanel.State.MAIN_SETTING)
                return;

            int cursor = GetCursorPosition();
            int total = GetSlotCount();

            string headline = GetHeadlineText();
            string slotInfo = GetSlotInfo(cursor);

            // Use SayQueued so any pre-open message speaks first
            string announcement = $"{headline}. {slotInfo}, slot {cursor + 1} of {total}";
            ScreenReader.SayQueued(announcement);
            DebugLogger.Log($"[SavePanel] Open announcement: {announcement}");

            _lastCursor = cursor;
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor)
            {
                // Cancel pending open announcement - cursor change will announce
                _pendingOpenAnnouncement = false;

                int total = GetSlotCount();
                string slotInfo = GetSlotInfo(cursor);

                string announcement = $"{slotInfo}, slot {cursor + 1} of {total}";
                ScreenReader.Say(announcement);

                DebugLogger.Log($"[SavePanel] Cursor: slot {cursor + 1}: {slotInfo}");
                _lastCursor = cursor;
            }
        }

        private string GetStateAnnouncement(uSavePanel.State state)
        {
            return state switch
            {
                uSavePanel.State.SAVE_CHECK => "Confirm save?",
                uSavePanel.State.LOAD_CHECK => "Confirm load?",
                uSavePanel.State.SAVE => "Saving...",
                uSavePanel.State.LOAD => "Loading...",
                _ => ""
            };
        }

        private int GetCursorPosition()
        {
            try
            {
                if (_panel != null)
                    return _panel.GetCorsorIndex();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SavePanel] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private int GetSlotCount()
        {
            try
            {
                var items = _panel?.m_items;
                if (items != null)
                    return items.Length;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SavePanel] Error in GetSlotCount: {ex.Message}");
            }
            return 3;
        }

        private string GetHeadlineText()
        {
            try
            {
                var headline = Object.FindObjectOfType<uSavePanelHeadLine>();
                if (headline?.m_HeadLine != null)
                {
                    string text = headline.m_HeadLine.text;
                    if (!string.IsNullOrEmpty(text))
                        return text;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SavePanel] Error getting headline: {ex.Message}");
            }

            return "Save/Load Menu";
        }

        private string GetSlotInfo(int slotIndex)
        {
            try
            {
                var items = _panel?.m_items;
                if (items != null && slotIndex >= 0 && slotIndex < items.Length)
                {
                    var item = items[slotIndex];
                    if (item != null)
                    {
                        var saveItem = item.TryCast<uSavePanelItemSaveItem>();
                        if (saveItem != null)
                        {
                            return GetSaveItemDetails(saveItem);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SavePanel] Error getting slot info: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Slot", slotIndex);
        }

        private string GetSaveItemDetails(uSavePanelItemSaveItem item)
        {
            try
            {
                // Check for empty slot
                var noDataText = item.m_NoDataText;
                if (noDataText != null && noDataText.gameObject.activeInHierarchy)
                {
                    string noData = noDataText.text;
                    if (!string.IsNullOrEmpty(noData))
                        return noData;
                    return "No Data";
                }

                // Build slot details from available text fields
                var parts = new System.Collections.Generic.List<string>();

                var playerName = item.m_playerNameText;
                if (playerName != null && !string.IsNullOrEmpty(playerName.text))
                    parts.Add(playerName.text);

                var level = item.m_tamarLavelText;
                if (level != null && !string.IsNullOrEmpty(level.text))
                    parts.Add($"Level {level.text}");

                var area = item.m_areaText;
                if (area != null && !string.IsNullOrEmpty(area.text))
                    parts.Add(area.text);

                var playTime = item.m_playTimeText;
                if (playTime != null && !string.IsNullOrEmpty(playTime.text))
                    parts.Add(playTime.text);

                var timestamp = item.m_timeStampText;
                if (timestamp != null && !string.IsNullOrEmpty(timestamp.text))
                    parts.Add(timestamp.text);

                if (parts.Count > 0)
                    return string.Join(", ", parts);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[SavePanel] Error reading save item: {ex.Message}");
            }

            return "Save Data";
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            if (_parentPanel == null || _parentPanel.m_State != uSavePanel.State.MAIN_SETTING)
            {
                // During save/load operation, announce the parent state
                if (_parentPanel != null)
                {
                    string stateMsg = GetStateAnnouncement(_parentPanel.m_State);
                    if (!string.IsNullOrEmpty(stateMsg))
                    {
                        ScreenReader.Say($"Save/Load Menu. {stateMsg}");
                        return;
                    }
                }
                ScreenReader.Say("Save/Load Menu");
                return;
            }

            int cursor = GetCursorPosition();
            int total = GetSlotCount();

            string headline = GetHeadlineText();
            string slotInfo = GetSlotInfo(cursor);

            string announcement = $"{headline}. {slotInfo}, slot {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
