using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the farm panel (farm goods management).
    /// </summary>
    public class FarmPanelHandler
    {
        private uFarmPanelCommand _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uFarmPanelCommand.State _lastState = uFarmPanelCommand.State.None;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uFarmPanelCommand>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_state;
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy &&
                       state != uFarmPanelCommand.State.None;
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
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastState = uFarmPanelCommand.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Farm. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[FarmPanel] Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastState = uFarmPanelCommand.State.None;
            DebugLogger.Log("[FarmPanel] Panel closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var state = _panel.m_state;
            if (state != _lastState)
            {
                string stateText = GetStateText(state);
                ScreenReader.Say(stateText);
                DebugLogger.Log($"[FarmPanel] State changed to {state}");
                _lastState = state;
                _lastCursor = -1; // Reset cursor on state change
            }
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = $"{itemText}, {cursor + 1} of {total}";
                ScreenReader.Say(announcement);

                DebugLogger.Log($"[FarmPanel] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                var cursor = _panel?.m_farmCursor;
                if (cursor != null)
                {
                    return cursor.index;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FarmPanel] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                var contents = _panel?.m_farmContents;
                if (contents != null && index >= 0 && index < contents.Length)
                {
                    var content = contents[index];
                    if (content != null)
                    {
                        // Get farm item name
                        var nameText = content.m_name;
                        if (nameText != null && !string.IsNullOrEmpty(nameText.text))
                        {
                            string name = nameText.text;

                            // Include condition if available
                            var conditionText = content.m_condition;
                            if (conditionText != null && !string.IsNullOrEmpty(conditionText.text))
                            {
                                return $"{name}, {conditionText.text}";
                            }

                            // Include day info if available
                            var dayText = content.m_day;
                            if (dayText != null && !string.IsNullOrEmpty(dayText.text))
                            {
                                return $"{name}, {dayText.text}";
                            }

                            return name;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FarmPanel] Error reading text: {ex.Message}");
            }

            return $"Farm slot {index + 1}";
        }

        private int GetMenuItemCount()
        {
            try
            {
                var contents = _panel?.m_farmContents;
                if (contents != null)
                {
                    return contents.Length;
                }
            }
            catch { }
            return 1;
        }

        private string GetStateText(uFarmPanelCommand.State state)
        {
            switch (state)
            {
                case uFarmPanelCommand.State.Main:
                    return "Select farm slot";
                case uFarmPanelCommand.State.Item:
                    return "Select item";
                case uFarmPanelCommand.State.Wait:
                    return "Please wait";
                default:
                    return "Farm";
            }
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Farm. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
