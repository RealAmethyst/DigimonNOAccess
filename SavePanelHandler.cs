using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the save/load menu
    /// </summary>
    public class SavePanelHandler
    {
        private uSavePanelCommand _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uSavePanelCommand.State _lastState = uSavePanelCommand.State.NONE;

        // Small delay so "Loading data" message can be detected first
        private const float OpenAnnouncementDelay = 0.05f;
        private float _openTime = 0f;
        private bool _pendingOpenAnnouncement = false;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uSavePanelCommand>();
            }

            if (_panel == null)
                return false;

            var state = _panel.m_State;
            return state == uSavePanelCommand.State.MAIN ||
                   state == uSavePanelCommand.State.SAVE_CHECK ||
                   state == uSavePanelCommand.State.LOAD_CHECK;
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

                CheckCursorChange();
                CheckStateChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastState = uSavePanelCommand.State.NONE;
            _openTime = UnityEngine.Time.time;
            _pendingOpenAnnouncement = true;

            if (_panel == null)
                return;

            _lastState = _panel.m_State;
            _lastCursor = GetCursorPosition();

            DebugLogger.Log($"[SavePanel] Menu opened, state={_lastState}, cursor={_lastCursor}");
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastState = uSavePanelCommand.State.NONE;
            _pendingOpenAnnouncement = false;
            DebugLogger.Log("[SavePanel] Menu closed");
        }

        private void AnnounceCurrentSlot()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();
            int total = GetSlotCount();

            string headline = GetHeadlineText();
            string slotInfo = GetSlotInfo(cursor);

            // Use SayQueued so "Loading data" message speaks first
            string announcement = $"{headline}. {slotInfo}, slot {cursor + 1} of {total}";
            ScreenReader.SayQueued(announcement);

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

                DebugLogger.Log($"[SavePanel] Cursor changed to slot {cursor + 1}: {slotInfo}");
                _lastCursor = cursor;
            }
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var currentState = _panel.m_State;

            if (currentState != _lastState)
            {
                string stateAnnouncement = GetStateAnnouncement(currentState);
                if (!string.IsNullOrEmpty(stateAnnouncement))
                {
                    ScreenReader.Say(stateAnnouncement);
                    DebugLogger.Log($"[SavePanel] State changed to {currentState}: {stateAnnouncement}");
                }
                _lastState = currentState;
            }
        }

        private string GetStateAnnouncement(uSavePanelCommand.State state)
        {
            return state switch
            {
                uSavePanelCommand.State.SAVE_CHECK => "Confirm save?",
                uSavePanelCommand.State.LOAD_CHECK => "Confirm load?",
                uSavePanelCommand.State.SAVE => "Saving...",
                uSavePanelCommand.State.LOAD => "Loading...",
                _ => ""
            };
        }

        private int GetCursorPosition()
        {
            try
            {
                if (_panel?.m_KeyCursorController != null)
                {
                    return _panel.m_KeyCursorController.m_DataIndex;
                }
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
                if (_panel?.m_KeyCursorController != null)
                {
                    return _panel.m_KeyCursorController.m_DataMax;
                }
            }
            catch { }
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

            // Fallback based on state
            if (_panel != null)
            {
                var state = _panel.m_State;
                if (state == uSavePanelCommand.State.SAVE_CHECK || state == uSavePanelCommand.State.SAVE)
                    return "Save Game";
                if (state == uSavePanelCommand.State.LOAD_CHECK || state == uSavePanelCommand.State.LOAD)
                    return "Load Game";
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
                        // Try to cast to uSavePanelItemSaveItem for detailed info
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

                // Player name
                var playerName = item.m_playerNameText;
                if (playerName != null && !string.IsNullOrEmpty(playerName.text))
                {
                    parts.Add(playerName.text);
                }

                // Tamer level
                var level = item.m_tamarLavelText;
                if (level != null && !string.IsNullOrEmpty(level.text))
                {
                    parts.Add($"Level {level.text}");
                }

                // Area/location
                var area = item.m_areaText;
                if (area != null && !string.IsNullOrEmpty(area.text))
                {
                    parts.Add(area.text);
                }

                // Play time
                var playTime = item.m_playTimeText;
                if (playTime != null && !string.IsNullOrEmpty(playTime.text))
                {
                    parts.Add(playTime.text);
                }

                // Timestamp
                var timestamp = item.m_timeStampText;
                if (timestamp != null && !string.IsNullOrEmpty(timestamp.text))
                {
                    parts.Add(timestamp.text);
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }
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

            int cursor = GetCursorPosition();
            int total = GetSlotCount();

            string headline = GetHeadlineText();
            string slotInfo = GetSlotInfo(cursor);

            string announcement = $"{headline}. {slotInfo}, slot {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
