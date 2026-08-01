using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the item shop panel (uShopPanel).
    /// Covers buy/sell item browsing, quantity selection, and price announcements.
    /// This is separate from TradePanelHandler which handles the trade market (uTradePanelCommand).
    /// </summary>
    public class ShopHandler : IAccessibilityHandler
    {
        private const string LogTag = "[Shop]";
        public int Priority => 58;

        private uShopPanel _panel;
        private bool _wasActive;
        private int _lastCursor = -1;
        private uShopPanel.ShopType _lastShopType;
        private uShopPanelItem.ShopState _lastShopState = uShopPanelItem.ShopState.ITEM_SELECT;
        private int _lastQuantity = -1;

        public bool IsOpen()
        {
            try
            {
                if (_panel == null)
                    _panel = Object.FindObjectOfType<uShopPanel>();
                return _panel != null && _panel.m_enabelPanel;
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
            _lastCursor = -1;
            _lastQuantity = -1;
            _lastShopState = uShopPanelItem.ShopState.ITEM_SELECT;

            var shopType = _panel.m_openShopType;
            _lastShopType = shopType;
            string typeText = GetShopTypeText(shopType);

            int cursor = GetCursorPosition();
            int total = GetItemCount();
            string itemInfo = GetCurrentItemWithPrice();
            string money = GetMoneyText();

            string announcement;
            if (total > 0)
            {
                string desc = GetCurrentItemDescription();
                announcement = $"Shop, {typeText}. {itemInfo}, {cursor + 1} of {total}";
                if (!string.IsNullOrEmpty(desc))
                    announcement += $". {desc}";
                announcement += $". {money}";
            }
            else
            {
                announcement = $"Shop, {typeText}. No items. {money}";
            }

            ScreenReader.Say(announcement);

            _lastCursor = cursor;
            DebugLogger.Log($"{LogTag} Opened - {typeText}, cursor={cursor}, total={total}");
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastQuantity = -1;
            _wasActive = false;
            DebugLogger.Log($"{LogTag} Closed");
        }

        private void OnUpdate()
        {
            if (ModInputManager.IsActionTriggered("ShopCheckBits"))
            {
                string money = GetMoneyText();
                ScreenReader.Say(!string.IsNullOrEmpty(money) ? money : "Bits unknown");
                return;
            }

            var itemPanel = GetItemPanel();
            if (itemPanel == null) return;

            CheckShopStateChange(itemPanel);

            if (itemPanel.m_shopState == uShopPanelItem.ShopState.ITEM_SELECT)
                CheckCursorChange(itemPanel);
            else if (itemPanel.m_shopState == uShopPanelItem.ShopState.ITEM_NUM_CHANGE)
                CheckQuantityChange(itemPanel);
        }

        private void CheckShopStateChange(uShopPanelItem itemPanel)
        {
            var state = itemPanel.m_shopState;
            if (state == _lastShopState)
                return;

            _lastShopState = state;

            if (state == uShopPanelItem.ShopState.ITEM_NUM_CHANGE)
            {
                string itemName = GetCurrentItemName(itemPanel);
                var numChange = itemPanel.m_shopPanelNumChange;
                if (numChange != null)
                {
                    int qty = numChange.m_num;
                    int unitPrice = numChange.m_onePrice;
                    string quantityLabel = GetQuantityLabel(numChange);
                    string quantityValue = GetQuantityValueText(numChange);
                    string totalLabel = GetTotalLabel(numChange);
                    string totalPrice = GetTotalPriceText(numChange);
                    _lastQuantity = qty;
                    ScreenReader.Say($"{quantityLabel}, {itemName}. {quantityValue} at {unitPrice} each, {totalLabel} {totalPrice} bits");
                }
                else
                {
                    DebugLogger.Log($"{LogTag} Quantity: m_shopPanelNumChange was null");
                    ScreenReader.Say($"Quantity select, {itemName}");
                }
            }
            else
            {
                // Back to ITEM_SELECT
                _lastQuantity = -1;
                _lastCursor = -1; // Force re-announce of current item
                int cursor = GetCursorPosition();
                int total = GetItemCount();
                string itemInfo = GetCurrentItemWithPrice();

                if (total > 0)
                {
                    string desc = GetCurrentItemDescription();
                    string msg = $"Item list. {itemInfo}, {cursor + 1} of {total}";
                    if (!string.IsNullOrEmpty(desc))
                        msg += $". {desc}";
                    ScreenReader.Say(msg);
                }
                else
                {
                    ScreenReader.Say("Item list. No items");
                }

                _lastCursor = cursor;
            }

            DebugLogger.Log($"{LogTag} State changed to {state}");
        }

        private void CheckCursorChange(uShopPanelItem itemPanel)
        {
            try
            {
                int cursor = itemPanel.selectNo;
                if (cursor != _lastCursor && cursor >= 0)
                {
                    string itemInfo = GetCurrentItemWithPrice();
                    int total = GetItemCount();
                    string desc = GetCurrentItemDescription();
                    string announcement = AnnouncementBuilder.CursorPosition(itemInfo, cursor, total);
                    if (!string.IsNullOrEmpty(desc))
                        announcement += $". {desc}";
                    ScreenReader.Say(announcement);
                    _lastCursor = cursor;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking cursor: {ex.Message}");
            }
        }

        private void CheckQuantityChange(uShopPanelItem itemPanel)
        {
            try
            {
                var numChange = itemPanel.m_shopPanelNumChange;
                if (numChange == null) return;

                int qty = numChange.m_num;
                if (qty != _lastQuantity)
                {
                    string quantityValue = GetQuantityValueText(numChange);
                    string totalPrice = GetTotalPriceText(numChange);
                    ScreenReader.Say($"{quantityValue}, {totalPrice} bits");
                    _lastQuantity = qty;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking quantity: {ex.Message}");
            }
        }

        public void AnnounceStatus()
        {
            if (!IsOpen()) return;

            var shopType = _panel.m_openShopType;
            string typeText = GetShopTypeText(shopType);

            var itemPanel = GetItemPanel();
            if (itemPanel == null) return;

            var state = itemPanel.m_shopState;
            string money = GetMoneyText();

            if (state == uShopPanelItem.ShopState.ITEM_NUM_CHANGE)
            {
                string itemName = GetCurrentItemName(itemPanel);
                var numChange = itemPanel.m_shopPanelNumChange;
                if (numChange != null)
                {
                    int qty = numChange.m_num;
                    int unitPrice = numChange.m_onePrice;
                    string quantityLabel = GetQuantityLabel(numChange);
                    string quantityValue = GetQuantityValueText(numChange);
                    string totalLabel = GetTotalLabel(numChange);
                    string totalPrice = GetTotalPriceText(numChange);
                    ScreenReader.Say($"Shop, {typeText}, {quantityLabel}. {itemName}, {quantityValue} at {unitPrice} each, {totalLabel} {totalPrice} bits. {money}");
                }
                else
                {
                    DebugLogger.Log($"{LogTag} Quantity: m_shopPanelNumChange was null");
                    ScreenReader.Say($"Shop, {typeText}, Quantity select. {itemName}. {money}");
                }
            }
            else
            {
                string itemInfo = GetCurrentItemWithPrice();
                int cursor = GetCursorPosition();
                int total = GetItemCount();

                if (total > 0)
                    ScreenReader.Say($"Shop, {typeText}. {itemInfo}, {cursor + 1} of {total}. {money}");
                else
                    ScreenReader.Say($"Shop, {typeText}. No items. {money}");
            }
        }

        // --- Helper methods ---

        private uShopPanelItem GetItemPanel()
        {
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Item panel: _panel was null");
                    return null;
                }

                var itemPanel = _panel.m_itemPanel;
                if (itemPanel == null)
                    DebugLogger.Log($"{LogTag} Item panel: m_itemPanel was null");
                return itemPanel;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Item panel: m_itemPanel read failed: {ex.Message}");
                return null;
            }
        }

        private int GetCursorPosition()
        {
            try { return GetItemPanel()?.selectNo ?? 0; }
            catch { return 0; }
        }

        private int GetItemCount()
        {
            try
            {
                var itemList = GetItemPanel()?.m_itemList;
                if (itemList != null)
                    return itemList.Count;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting item count: {ex.Message}");
            }
            return 0;
        }

        private string GetCurrentItemName(uShopPanelItem itemPanel = null)
        {
            try
            {
                itemPanel ??= GetItemPanel();
                if (itemPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: m_itemPanel was null");
                    return AnnouncementBuilder.FallbackItem("Item", GetCursorPosition());
                }

                var paramData = itemPanel.GetSelectItemParam();
                if (paramData != null)
                {
                    string name = (TextUtilities.StripRichTextTags(paramData.GetName()) ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;

                    DebugLogger.Log($"{LogTag} Item name: ParameterItemData.GetName() was empty");
                }
                else
                {
                    DebugLogger.Log($"{LogTag} Item name: GetSelectItemParam() returned null");
                }

                int logicalIndex = itemPanel.selectNo;
                var itemParts = itemPanel.GetSelectItemParts(logicalIndex);
                if (itemParts == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: GetSelectItemParts({logicalIndex}) returned null");
                    return AnnouncementBuilder.FallbackItem("Item", logicalIndex);
                }

                var nameText = itemParts.m_name;
                if (nameText == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: selected uItemParts.m_name was null");
                    return AnnouncementBuilder.FallbackItem("Item", logicalIndex);
                }

                string renderedName = (TextUtilities.StripRichTextTags(nameText.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(renderedName))
                    return renderedName;

                DebugLogger.Log($"{LogTag} Item name: selected uItemParts.m_name.text was empty");
                return AnnouncementBuilder.FallbackItem("Item", logicalIndex);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting item name: {ex.Message}");
            }
            return AnnouncementBuilder.FallbackItem("Item", GetCursorPosition());
        }

        private string GetCurrentItemDescription()
        {
            try
            {
                var itemPanel = GetItemPanel();
                var paramData = itemPanel?.GetSelectItemParam();
                if (paramData != null)
                {
                    string desc = paramData.GetDescription();
                    if (!string.IsNullOrEmpty(desc))
                        return TextUtilities.StripRichTextTags(desc);
                }
            }
            catch { }
            return "";
        }

        private string GetCurrentItemWithPrice()
        {
            string name = GetCurrentItemName();
            try
            {
                var itemPanel = GetItemPanel();
                var paramData = itemPanel?.GetSelectItemParam();
                if (paramData != null)
                {
                    int price = paramData.m_price;
                    if (price > 0)
                        return $"{name}, {price} bits";
                }
            }
            catch { }
            return name;
        }

        private string GetMoneyText()
        {
            try
            {
                var text = GetItemPanel()?.m_haveMoneyVauleText;
                if (text != null)
                {
                    string money = text.text;
                    if (!string.IsNullOrEmpty(money))
                        return $"You have {TextUtilities.StripRichTextTags(money)} bits";
                }
            }
            catch { }
            return "";
        }

        private string GetTotalPriceText(uShopPanelNumChange numChange)
        {
            try
            {
                if (numChange == null)
                {
                    DebugLogger.Log($"{LogTag} Total value: m_shopPanelNumChange was null");
                    return "0";
                }

                var text = numChange.m_totalText;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} Total value: m_totalText was null");
                    return (numChange.m_num * numChange.m_onePrice).ToString();
                }

                string total = (TextUtilities.StripRichTextTags(text.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(total))
                    return total;

                DebugLogger.Log($"{LogTag} Total value: m_totalText.text was empty");
                // Fallback: calculate manually
                return (numChange.m_num * numChange.m_onePrice).ToString();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading m_totalText.text: {ex.Message}");
                return "0";
            }
        }

        private string GetShopTypeText(uShopPanel.ShopType type)
        {
            string fallback = type == uShopPanel.ShopType.BUY ? "Buy" : "Sell";

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Action caption: _panel was null");
                    return fallback;
                }

                var captionPanel = _panel.m_captionPanel;
                if (captionPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Action caption: m_captionPanel was null");
                    return fallback;
                }

                var text = captionPanel.m_text;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} Action caption: m_captionPanel.m_text was null");
                    return fallback;
                }

                string caption = (TextUtilities.StripRichTextTags(text.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(caption))
                    return caption;

                DebugLogger.Log($"{LogTag} Action caption: m_captionPanel.m_text.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading m_captionPanel.m_text.text: {ex.Message}");
            }

            return fallback;
        }

        private string GetQuantityLabel(uShopPanelNumChange numChange)
        {
            return GetNumChangeText(
                numChange,
                "m_itemNumTextName",
                "Quantity select");
        }

        private string GetQuantityValueText(uShopPanelNumChange numChange)
        {
            string fallback = "0";
            try
            {
                if (numChange != null)
                    fallback = numChange.m_num.ToString();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} m_num fallback read failed: {ex.Message}");
            }

            return GetNumChangeText(
                numChange,
                "m_numText",
                fallback);
        }

        private string GetTotalLabel(uShopPanelNumChange numChange)
        {
            return GetNumChangeText(
                numChange,
                "m_priceTotalText",
                "total");
        }

        private string GetNumChangeText(
            uShopPanelNumChange numChange,
            string fieldName,
            string fallback)
        {
            try
            {
                if (numChange == null)
                {
                    DebugLogger.Log($"{LogTag} {fieldName}: m_shopPanelNumChange was null");
                    return fallback;
                }

                UnityEngine.UI.Text text = fieldName switch
                {
                    "m_itemNumTextName" => numChange.m_itemNumTextName,
                    "m_numText" => numChange.m_numText,
                    "m_priceTotalText" => numChange.m_priceTotalText,
                    _ => null
                };

                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} {fieldName}: field was null");
                    return fallback;
                }

                string value = (TextUtilities.StripRichTextTags(text.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;

                DebugLogger.Log($"{LogTag} {fieldName}.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading {fieldName}.text: {ex.Message}");
            }

            return fallback;
        }
    }
}
