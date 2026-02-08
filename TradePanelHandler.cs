using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the trade/shop panel (buy and sell items).
    /// </summary>
    public class TradePanelHandler : HandlerBase<uTradePanelCommand>
    {
        protected override string LogTag => "[TradePanel]";
        public override int Priority => 60;

        private uTradePanelCommand.State _lastState = uTradePanelCommand.State.None;

        public override bool IsOpen()
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

        protected override void OnOpen()
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

            string announcement = AnnouncementBuilder.MenuOpenWithState("Shop", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);

            DebugLogger.Log($"{LogTag} Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
            _lastState = uTradePanelCommand.State.None;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (ModInputManager.IsActionTriggered("ShopCheckBits"))
            {
                AnnounceBits();
                return;
            }

            CheckStateChange();
            CheckCursorChange();
        }

        private void AnnounceBits()
        {
            try
            {
                var bitPanel = UnityEngine.Object.FindObjectOfType<uTradePanelBit>();
                if (bitPanel?.m_bit != null)
                {
                    string bits = bitPanel.m_bit.text;
                    if (!string.IsNullOrEmpty(bits))
                    {
                        ScreenReader.Say($"You have {TextUtilities.StripRichTextTags(bits)} bits");
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading bits: {ex.Message}");
            }
            ScreenReader.Say("Bits unknown");
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
                var cursor = _panel?.m_tradeCursor;
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
                var state = _panel?.m_state;

                // In Main state, show Buy/Sell options
                if (state == uTradePanelCommand.State.Main)
                {
                    switch (index)
                    {
                        case 0: return "Buy";
                        case 1: return "Sell";
                        default: return AnnouncementBuilder.FallbackItem("Option", index);
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
                DebugLogger.Log($"{LogTag} Error reading text: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Item", index);
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
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error in GetMenuItemCount: {ex.Message}");
            }
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

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpenWithState("Shop", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
