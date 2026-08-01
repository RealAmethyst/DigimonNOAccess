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
                return GetTradeTitleText();

            var state = _panel.m_state;
            string prefix = includeMenuTitle ? $"{GetTradeTitleText()}. " : "";

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
                    string action = GetTradeActionText(state);
                    return string.IsNullOrEmpty(quantity)
                        ? $"{prefix}{action} {item}"
                        : $"{prefix}{action} {item}. {quantity}";
                }
                case uTradePanelCommand.State.Sale:
                {
                    string item = GetSelectedItemName(uTradePanelCommand.State.Sale);
                    string action = GetTradeActionText(state);
                    return $"{prefix}{action} {item}";
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
            {
                DebugLogger.Log($"{LogTag} Item name: logical index {index} was invalid");
                return AnnouncementBuilder.FallbackItem("Item", 0);
            }

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: _panel was null");
                    return AnnouncementBuilder.FallbackItem("Item", index);
                }

                var contents = _panel.m_tradeContents;
                if (contents == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: m_tradeContents was null");
                    return AnnouncementBuilder.FallbackItem("Item", index);
                }

                if (index >= contents.Length)
                {
                    DebugLogger.Log($"{LogTag} Item name: m_tradeContents index {index} was out of range");
                    return AnnouncementBuilder.FallbackItem("Item", index);
                }

                var content = contents[index];
                string name = GetContentName(content, $"m_tradeContents[{index}]");
                if (string.IsNullOrEmpty(name) && content != null)
                {
                    name = GetParameterItemName(content.id, $"m_tradeContents[{index}].id");
                }

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
                    if (!string.IsNullOrEmpty(GetContentName(c, $"m_tradeContents[{i}]")))
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
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Selected item: _panel was null");
                    return "item";
                }

                uint selectId = 0;
                if (state == uTradePanelCommand.State.Buy)
                {
                    var buyPanel = _panel.m_tradePanelBuy;
                    if (buyPanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Selected item: m_tradePanelBuy was null");
                        return "item";
                    }
                    selectId = buyPanel.m_selectId;
                }
                else if (state == uTradePanelCommand.State.Sale)
                {
                    var salePanel = _panel.m_tradePanelSale;
                    if (salePanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Selected item: m_tradePanelSale was null");
                        return "item";
                    }
                    selectId = salePanel.m_selectId;
                }

                var contents = _panel.m_tradeContents;
                if (contents != null)
                {
                    for (int i = 0; i < contents.Length; i++)
                    {
                        var c = contents[i];
                        if (c != null && c.id == selectId)
                        {
                            string name = GetContentName(c, $"m_tradeContents[{i}]");
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }

                    string selectedParameterName = GetParameterItemName(selectId, "selected item ID");
                    if (!string.IsNullOrEmpty(selectedParameterName))
                        return selectedParameterName;

                    int cursorIdx = GetMainCursor();
                    if (cursorIdx >= 0 && cursorIdx < contents.Length)
                    {
                        string name = GetContentName(contents[cursorIdx], $"m_tradeContents[{cursorIdx}]");
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
                else
                {
                    DebugLogger.Log($"{LogTag} Selected item: m_tradeContents was null");
                    string selectedParameterName = GetParameterItemName(selectId, "selected item ID");
                    if (!string.IsNullOrEmpty(selectedParameterName))
                        return selectedParameterName;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting selected item: {ex.Message}");
            }
            DebugLogger.Log($"{LogTag} Selected item: localized name unavailable; using English fallback");
            return "item";
        }

        private string GetParameterItemName(uint itemId, string context)
        {
            var itemParam = ParameterItemData.GetParam(itemId);
            if (itemParam == null)
            {
                DebugLogger.Log($"{LogTag} Item name: ParameterItemData.GetParam({itemId}) returned null for {context}");
                return null;
            }

            string name = (TextUtilities.StripRichTextTags(itemParam.GetName()) ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            DebugLogger.Log($"{LogTag} Item name: ParameterItemData.GetName() was empty for {context}");
            return null;
        }

        private string GetContentName(uTradeContents content, string context)
        {
            if (content == null)
            {
                DebugLogger.Log($"{LogTag} Item name: {context} was null");
                return null;
            }

            var nameText = content.m_name;
            if (nameText == null)
            {
                DebugLogger.Log($"{LogTag} Item name: {context}.m_name was null");
                return null;
            }

            string name = (TextUtilities.StripRichTextTags(nameText.text) ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            DebugLogger.Log($"{LogTag} Item name: {context}.m_name.text was empty");
            return null;
        }

        private string GetTradeTitleText()
        {
            const string fallback = "Stock Market";

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: _panel was null");
                    return fallback;
                }

                var tradePanel = _panel.m_tradePanel;
                if (tradePanel == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: m_tradePanel was null");
                    return fallback;
                }

                var headline = tradePanel.m_tradePanelHeadLine;
                if (headline == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: m_tradePanel.m_tradePanelHeadLine was null");
                    return fallback;
                }

                var text = headline.m_headLineText;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: m_tradePanelHeadLine.m_headLineText was null");
                    return fallback;
                }

                string title = (TextUtilities.StripRichTextTags(text.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    return title;

                DebugLogger.Log($"{LogTag} Headline: m_tradePanelHeadLine.m_headLineText.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading m_tradePanelHeadLine.m_headLineText.text: {ex.Message}");
            }

            return fallback;
        }

        private string GetTradeActionText(uTradePanelCommand.State state)
        {
            string fallback = state == uTradePanelCommand.State.Sale ? "Sell" : "Buy";

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Action caption: _panel was null");
                    return fallback;
                }

                var caption = _panel.m_tradePanelCaption;
                if (caption == null)
                {
                    DebugLogger.Log($"{LogTag} Action caption: m_tradePanelCaption was null");
                    return fallback;
                }

                var text = caption.m_text;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} Action caption: m_tradePanelCaption.m_text was null");
                    return fallback;
                }

                string action = (TextUtilities.StripRichTextTags(text.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(action))
                    return action;

                DebugLogger.Log($"{LogTag} Action caption: m_tradePanelCaption.m_text.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading m_tradePanelCaption.m_text.text: {ex.Message}");
            }

            return fallback;
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            ScreenReader.Say(BuildAnnouncement(includeMenuTitle: true));
        }
    }
}
