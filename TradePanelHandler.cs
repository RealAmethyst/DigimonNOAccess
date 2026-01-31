using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the trade/shop panel (buy and sell items).
    /// </summary>
    public class TradePanelHandler
    {
        private uTradePanelCommand _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uTradePanelCommand.State _lastState = uTradePanelCommand.State.None;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uTradePanelCommand>();
            }

            return _panel != null &&
                   _panel.gameObject != null &&
                   _panel.gameObject.activeInHierarchy &&
                   _panel.m_state != uTradePanelCommand.State.None &&
                   _panel.m_state != uTradePanelCommand.State.Close;
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
            _lastState = uTradePanelCommand.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Shop. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[TradePanel] Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastState = uTradePanelCommand.State.None;
            DebugLogger.Log("[TradePanel] Panel closed");
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
                DebugLogger.Log($"[TradePanel] State changed to {state}");
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

                DebugLogger.Log($"[TradePanel] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                var cursor = _panel?.m_tradeCursor;
                if (cursor != null)
                {
                    return cursor.index;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TradePanel] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                var state = _panel?.m_state;

                // In Main state, show Buy/Sell options
                if (state == uTradePanelCommand.State.Main)
                {
                    switch (index)
                    {
                        case 0: return "Buy";
                        case 1: return "Sell";
                        default: return $"Option {index + 1}";
                    }
                }

                // In Buy/Sell state, show item names from trade contents
                var contents = _panel?.m_tradeContents;
                if (contents != null && index >= 0 && index < contents.Length)
                {
                    var content = contents[index];
                    if (content != null && content.m_name != null)
                    {
                        string name = content.m_name.text;
                        if (!string.IsNullOrEmpty(name))
                        {
                            // Include price if available
                            if (content.m_today != null && !string.IsNullOrEmpty(content.m_today.text))
                            {
                                return $"{name}, {content.m_today.text} bits";
                            }
                            return name;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TradePanel] Error reading text: {ex.Message}");
            }

            return $"Item {index + 1}";
        }

        private int GetMenuItemCount()
        {
            try
            {
                var state = _panel?.m_state;
                if (state == uTradePanelCommand.State.Main)
                {
                    return 2; // Buy, Sell
                }

                var contents = _panel?.m_tradeContents;
                if (contents != null)
                {
                    return contents.Length;
                }
            }
            catch { }
            return 1;
        }

        private string GetStateText(uTradePanelCommand.State state)
        {
            switch (state)
            {
                case uTradePanelCommand.State.Main:
                    return "Select Buy or Sell";
                case uTradePanelCommand.State.Buy:
                    return "Buy items";
                case uTradePanelCommand.State.Sale:
                    return "Sell items";
                case uTradePanelCommand.State.Wait:
                    return "Please wait";
                default:
                    return "Shop";
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

            string announcement = $"Shop. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
