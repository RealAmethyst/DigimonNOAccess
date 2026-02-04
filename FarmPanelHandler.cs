using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the farm panel (farm goods management).
    /// </summary>
    public class FarmPanelHandler : HandlerBase<uFarmPanelCommand>
    {
        protected override string LogTag => "[FarmPanel]";
        public override int Priority => 60;

        private uFarmPanelCommand.State _lastState = uFarmPanelCommand.State.None;

        public override bool IsOpen()
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

        protected override void OnOpen()
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

            string announcement = AnnouncementBuilder.MenuOpenWithState("Farm", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);

            DebugLogger.Log($"{LogTag} Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
            _lastState = uFarmPanelCommand.State.None;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckStateChange();
            CheckCursorChange();
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
                DebugLogger.Log($"{LogTag} State changed to {state}");
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

                string announcement = AnnouncementBuilder.CursorPosition(itemText, cursor, total);
                ScreenReader.Say(announcement);

                DebugLogger.Log($"{LogTag} Cursor changed: {itemText}");
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
                DebugLogger.Log($"{LogTag} Error getting cursor: {ex.Message}");
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
                DebugLogger.Log($"{LogTag} Error reading text: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Farm slot", index);
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

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpenWithState("Farm", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
