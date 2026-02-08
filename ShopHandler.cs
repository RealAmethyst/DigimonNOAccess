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
                    string totalPrice = GetTotalPriceText(numChange);
                    _lastQuantity = qty;
                    ScreenReader.Say($"Quantity select, {itemName}. {qty} at {unitPrice} each, total {totalPrice} bits");
                }
                else
                {
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
                    string totalPrice = GetTotalPriceText(numChange);
                    ScreenReader.Say($"{qty}, {totalPrice} bits");
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
                    string totalPrice = GetTotalPriceText(numChange);
                    ScreenReader.Say($"Shop, {typeText}, Quantity select. {itemName}, {qty} at {unitPrice} each, total {totalPrice} bits. {money}");
                }
                else
                {
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
            try { return _panel?.m_itemPanel; }
            catch { return null; }
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
                var paramData = itemPanel?.GetSelectItemParam();
                if (paramData != null)
                {
                    string name = paramData.GetName();
                    if (!string.IsNullOrEmpty(name))
                        return TextUtilities.StripRichTextTags(name);
                }
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
                var text = numChange?.m_totalText;
                if (text != null)
                {
                    string total = text.text;
                    if (!string.IsNullOrEmpty(total))
                        return TextUtilities.StripRichTextTags(total);
                }
                // Fallback: calculate manually
                return (numChange.m_num * numChange.m_onePrice).ToString();
            }
            catch { return "0"; }
        }

        private static string GetShopTypeText(uShopPanel.ShopType type)
        {
            return type == uShopPanel.ShopType.BUY ? "Buy" : "Sell";
        }
    }
}
