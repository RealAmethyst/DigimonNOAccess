using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Stock Market panel (uTradePanelCommand).
    /// Main state = item list (Pot, Lithograph, Bone Ornament, Rug, Hanging Scroll).
    /// Buy state = quantity popup via uTradePanelBuy.
    /// Sale state = sell confirmation via uTradePanelSale.
    /// </summary>
    public class TradePanelHandler : HandlerBase<uTradePanelCommand>
    {
        protected override string LogTag => "[TradePanel]";
        public override int Priority => 60;

        private uTradePanelCommand.State _lastState = uTradePanelCommand.State.None;
        private int _lastBuyNum = -1;

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
            _lastBuyNum = -1;
            _lastState = uTradePanelCommand.State.None;

            if (_panel == null)
                return;

            ScreenReader.Say(BuildAnnouncement(includeMenuTitle: true));
            _lastState = _panel.m_state;
            _lastCursor = GetMainCursor();
            _lastBuyNum = GetBuyNum();

            DebugLogger.Log($"{LogTag} Panel opened, state={_panel.m_state}");
        }

        protected override void OnClose()
        {
            _lastState = uTradePanelCommand.State.None;
            _lastBuyNum = -1;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (ModInputManager.IsActionTriggered("ShopCheckBits"))
            {
                AnnounceBits();
                return;
            }

            if (_panel == null)
                return;

            var state = _panel.m_state;

            if (state != _lastState)
            {
                ScreenReader.Say(BuildAnnouncement(includeMenuTitle: false));
                _lastState = state;
                _lastCursor = GetMainCursor();
                _lastBuyNum = GetBuyNum();
                return;
            }

            if (state == uTradePanelCommand.State.Main)
            {
                int cursor = GetMainCursor();
                if (cursor != _lastCursor && cursor >= 0)
                {
                    string itemText = GetMainItemText(cursor);
                    int total = GetMainItemCount();
                    ScreenReader.Say(AnnouncementBuilder.CursorPosition(itemText, cursor, total));
                    _lastCursor = cursor;
                }
            }
            else if (state == uTradePanelCommand.State.Buy)
            {
                int num = GetBuyNum();
                if (num != _lastBuyNum && num >= 0)
                {
                    ScreenReader.Say(BuildBuyQuantityText(num));
                    _lastBuyNum = num;
                }
            }
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

        private string BuildAnnouncement(bool includeMenuTitle)
        {
            if (_panel == null)
                return "Stock Market";

            var state = _panel.m_state;
            string prefix = includeMenuTitle ? "Stock Market. " : "";

            switch (state)
            {
                case uTradePanelCommand.State.Main:
                {
                    int cursor = GetMainCursor();
                    int total = GetMainItemCount();
                    string itemText = GetMainItemText(cursor);
                    return $"{prefix}{itemText}, {cursor + 1} of {total}";
                }
                case uTradePanelCommand.State.Buy:
                {
                    string item = GetSelectedItemName(uTradePanelCommand.State.Buy);
                    int num = GetBuyNum();
                    string quantity = num >= 0 ? BuildBuyQuantityText(num) : "";
                    return string.IsNullOrEmpty(quantity)
                        ? $"{prefix}Buy {item}"
                        : $"{prefix}Buy {item}. {quantity}";
                }
                case uTradePanelCommand.State.Sale:
                {
                    string item = GetSelectedItemName(uTradePanelCommand.State.Sale);
                    return $"{prefix}Sell {item}";
                }
                case uTradePanelCommand.State.Wait:
                    return $"{prefix}Please wait";
                default:
                    return prefix.TrimEnd(' ', '.');
            }
        }

        private string BuildBuyQuantityText(int num)
        {
            try
            {
                var buy = _panel?.m_tradePanelBuy;
                var cursor = buy?.m_tradeBuyCursor;
                int max = cursor?.max ?? 0;
                if (max > 0)
                    return $"Quantity {num} of {max}";
                return $"Quantity {num}";
            }
            catch
            {
                return $"Quantity {num}";
            }
        }

        private int GetMainCursor()
        {
            try
            {
                var cursor = _panel?.m_tradeCursor;
                if (cursor != null)
                    return cursor.index;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting main cursor: {ex.Message}");
            }
            return -1;
        }

        private int GetBuyNum()
        {
            try
            {
                var buy = _panel?.m_tradePanelBuy;
                var cursor = buy?.m_tradeBuyCursor;
                if (cursor != null)
                    return cursor.num;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting buy num: {ex.Message}");
            }
            return -1;
        }

        private string GetMainItemText(int index)
        {
            if (index < 0)
                return AnnouncementBuilder.FallbackItem("Item", 0);

            try
            {
                var contents = _panel?.m_tradeContents;
                if (contents != null && index < contents.Length)
                {
                    var content = contents[index];
                    string name = GetContentName(content);
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (content.m_today != null && !string.IsNullOrEmpty(content.m_today.text))
                        {
                            string price = TextUtilities.StripRichTextTags(content.m_today.text);
                            return $"{name}, {price} bits";
                        }
                        return name;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading main item text: {ex.Message}");
            }
            return AnnouncementBuilder.FallbackItem("Item", index);
        }

        private int GetMainItemCount()
        {
            try
            {
                var cursor = _panel?.m_tradeCursor;
                if (cursor != null && cursor.m_max > 0)
                    return cursor.m_max + 1;

                var contents = _panel?.m_tradeContents;
                if (contents == null)
                    return 0;

                int visible = 0;
                for (int i = 0; i < contents.Length; i++)
                {
                    var c = contents[i];
                    if (c == null)
                        continue;
                    if (c.gameObject != null && !c.gameObject.activeInHierarchy)
                        continue;
                    if (!string.IsNullOrEmpty(GetContentName(c)))
                        visible++;
                }
                return visible > 0 ? visible : contents.Length;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error in GetMainItemCount: {ex.Message}");
            }
            return 0;
        }

        private string GetSelectedItemName(uTradePanelCommand.State state)
        {
            try
            {
                uint selectId = 0;
                if (state == uTradePanelCommand.State.Buy && _panel?.m_tradePanelBuy != null)
                    selectId = _panel.m_tradePanelBuy.m_selectId;
                else if (state == uTradePanelCommand.State.Sale && _panel?.m_tradePanelSale != null)
                    selectId = _panel.m_tradePanelSale.m_selectId;

                var contents = _panel?.m_tradeContents;
                if (contents != null)
                {
                    for (int i = 0; i < contents.Length; i++)
                    {
                        var c = contents[i];
                        if (c != null && c.id == selectId)
                        {
                            string name = GetContentName(c);
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                    int cursorIdx = GetMainCursor();
                    if (cursorIdx >= 0 && cursorIdx < contents.Length)
                    {
                        string name = GetContentName(contents[cursorIdx]);
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting selected item: {ex.Message}");
            }
            return "item";
        }

        private static string GetContentName(uTradeContents content)
        {
            if (content == null || content.m_name == null)
                return null;
            string name = content.m_name.text;
            if (string.IsNullOrEmpty(name))
                return null;
            return TextUtilities.StripRichTextTags(name);
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            ScreenReader.Say(BuildAnnouncement(includeMenuTitle: true));
        }
    }
}
