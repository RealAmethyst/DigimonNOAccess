using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the colosseum panel (battle arena selection).
    /// </summary>
    public class ColosseumPanelHandler
    {
        private uColosseumPanelCommand _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uColosseumPanelCommand.State _lastState = uColosseumPanelCommand.State.None;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uColosseumPanelCommand>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_state;
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy &&
                       state != uColosseumPanelCommand.State.None &&
                       state != uColosseumPanelCommand.State.Close;
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
            _lastState = uColosseumPanelCommand.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Colosseum. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[ColosseumPanel] Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastState = uColosseumPanelCommand.State.None;
            DebugLogger.Log("[ColosseumPanel] Panel closed");
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
                DebugLogger.Log($"[ColosseumPanel] State changed to {state}");
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

                DebugLogger.Log($"[ColosseumPanel] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                var scrollView = _panel?.m_colosseumScrollView;
                if (scrollView != null)
                {
                    // ColosseumScrollView extends uItemBase which has m_selectNo
                    return scrollView.m_selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ColosseumPanel] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                // Try to get the description caption text
                var description = _panel?.m_colosseumDescription;
                if (description != null)
                {
                    var captionText = description.m_caption;
                    if (captionText != null && !string.IsNullOrEmpty(captionText.text))
                    {
                        string text = captionText.text;

                        // Also try to get rule info
                        var ruleValue = description.m_ruleValue;
                        if (ruleValue != null && !string.IsNullOrEmpty(ruleValue.text))
                        {
                            return $"{text}, {ruleValue.text}";
                        }
                        return text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ColosseumPanel] Error reading text: {ex.Message}");
            }

            return $"Battle {index + 1}";
        }

        private int GetMenuItemCount()
        {
            try
            {
                var scrollView = _panel?.m_colosseumScrollView;
                if (scrollView != null)
                {
                    // Use m_itemList from uItemBase parent class
                    var itemList = scrollView.m_itemList;
                    if (itemList != null)
                    {
                        return itemList.Count;
                    }
                }
            }
            catch { }
            return 1;
        }

        private string GetStateText(uColosseumPanelCommand.State state)
        {
            switch (state)
            {
                case uColosseumPanelCommand.State.Main:
                    return "Select battle";
                case uColosseumPanelCommand.State.Confirm:
                    return "Confirm entry";
                case uColosseumPanelCommand.State.Warning:
                    return "Warning";
                case uColosseumPanelCommand.State.Wait:
                    return "Please wait";
                default:
                    return "Colosseum";
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

            string announcement = $"Colosseum. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
