using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the field item panel (inventory during gameplay)
    /// </summary>
    public class FieldItemPanelHandler : HandlerBase<uFieldItemPanel>
    {
        protected override string LogTag => "[FieldItemPanel]";
        public override int Priority => 55;

        private uFieldItemPanel.Type _lastType = (uFieldItemPanel.Type)(-1);
        private int _lastInternalTab = -1;
        private MainGameManager.ORDER_UNIT _lastTarget = (MainGameManager.ORDER_UNIT)(-1);

        protected override void OnOpen()
        {
            _lastCursor = -1;
            _lastType = (uFieldItemPanel.Type)(-1);
            _lastInternalTab = -1;
            _lastTarget = (MainGameManager.ORDER_UNIT)(-1);

            if (_panel == null)
                return;

            _lastType = _panel.m_type;
            _lastInternalTab = GetInternalTab();
            _lastTarget = GetCurrentTarget();
            int cursor = GetCursorPosition();
            int total = GetItemCount();

            string tabName = GetTabName(_lastType);
            string internalTabName = GetInternalTabName(_lastInternalTab);
            string itemInfo = GetItemInfo();
            string partnerName = GetTargetPartnerName(_lastTarget);

            string announcement;
            if (total == 0)
            {
                announcement = $"Items, {tabName}, {internalTabName} tab, empty";
            }
            else
            {
                announcement = $"Items, {tabName}, {internalTabName} tab. {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}";
                string desc = GetItemDescription();
                if (!string.IsNullOrEmpty(desc))
                    announcement += $". {desc}";
            }

            // Add partner name for Care-related item types
            if (_lastType == uFieldItemPanel.Type.Care && !string.IsNullOrEmpty(partnerName))
            {
                announcement += $", {partnerName}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened, type={tabName}, tab={internalTabName}, cursor={cursor}, total={total}, target={_lastTarget}");
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
            _lastType = (uFieldItemPanel.Type)(-1);
            _lastInternalTab = -1;
            _lastTarget = (MainGameManager.ORDER_UNIT)(-1);
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckCursorChange();
            CheckTabChange();
            CheckTargetChange();
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor)
            {
                int total = GetItemCount();
                string itemInfo = GetItemInfo();

                string announcement;
                if (total == 0)
                {
                    announcement = "Empty";
                }
                else
                {
                    announcement = AnnouncementBuilder.CursorPosition(itemInfo, cursor, total);
                    string desc = GetItemDescription();
                    if (!string.IsNullOrEmpty(desc))
                        announcement += $". {desc}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Cursor changed: {itemInfo}");
                _lastCursor = cursor;
            }
        }

        private void CheckTabChange()
        {
            if (_panel == null)
                return;

            var currentType = _panel.m_type;
            int currentInternalTab = GetInternalTab();

            // Check if the outer type (Care/Camp/Battle) changed
            if (currentType != _lastType)
            {
                _lastCursor = -1; // Reset cursor tracking for new tab
                _lastInternalTab = currentInternalTab;
                int cursor = GetCursorPosition();
                int total = GetItemCount();

                string tabName = GetTabName(currentType);
                string internalTabName = GetInternalTabName(currentInternalTab);
                string itemInfo = GetItemInfo();

                string announcement;
                if (total == 0)
                {
                    announcement = $"{tabName}, {internalTabName} tab, empty";
                }
                else
                {
                    announcement = $"{tabName}, {internalTabName} tab. {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}";
                    string desc = GetItemDescription();
                    if (!string.IsNullOrEmpty(desc))
                        announcement += $". {desc}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Type changed to {tabName}, tab={internalTabName}");
                _lastType = currentType;
                _lastCursor = cursor;
            }
            // Check if the internal tab (Consumption/Foodstuff/etc.) changed
            else if (currentInternalTab != _lastInternalTab)
            {
                _lastCursor = -1; // Reset cursor tracking for new tab
                int cursor = GetCursorPosition();
                int total = GetItemCount();

                string internalTabName = GetInternalTabName(currentInternalTab);
                string itemInfo = GetItemInfo();

                string announcement;
                if (total == 0)
                {
                    announcement = $"{internalTabName} tab, empty";
                }
                else
                {
                    announcement = $"{internalTabName} tab. {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}";
                    string desc = GetItemDescription();
                    if (!string.IsNullOrEmpty(desc))
                        announcement += $". {desc}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Internal tab changed to {internalTabName}");
                _lastInternalTab = currentInternalTab;
                _lastCursor = cursor;
            }
        }

        private string GetTabName(uFieldItemPanel.Type type)
        {
            return type switch
            {
                uFieldItemPanel.Type.Care => "Care",
                uFieldItemPanel.Type.Camp => "Camp",
                uFieldItemPanel.Type.Digivice => "Digivice",
                uFieldItemPanel.Type.Event => "Event",
                uFieldItemPanel.Type.Battle => "Battle",
                uFieldItemPanel.Type.Oyatsu => "Snacks",
                _ => "Items"
            };
        }

        private int GetInternalTab()
        {
            try
            {
                if (_panel != null)
                {
                    return _panel.m_tab;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting internal tab: {ex.Message}");
            }
            return -1;
        }

        private string GetInternalTabName(int tab)
        {
            // Based on uCarePanelItem.Tab enum:
            // None = -1, Consumption = 0, Foodstuff = 1, Evolution = 2, Material = 3, KeyItem = 4
            return tab switch
            {
                0 => "Consumption",
                1 => "Foodstuff",
                2 => "Evolution",
                3 => "Material",
                4 => "Key Items",
                _ => "Items"
            };
        }

        private int GetCursorPosition()
        {
            try
            {
                if (_panel != null)
                {
                    return _panel.m_selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private int GetItemCount()
        {
            try
            {
                var itemList = _panel?.m_itemList;
                if (itemList != null)
                {
                    return itemList.Count;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting item count: {ex.Message}");
            }
            return 0;
        }

        private string GetItemInfo()
        {
            try
            {
                var paramData = _panel?.GetSelectItemParam();
                if (paramData != null)
                {
                    string name = paramData.GetName();
                    if (!string.IsNullOrEmpty(name))
                    {
                        int qty = GetSelectedItemQuantity();
                        if (qty > 0)
                            return $"{qty} {name}";
                        return name;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting item info: {ex.Message}");
            }

            return "Unknown Item";
        }

        private int GetSelectedItemQuantity()
        {
            try
            {
                var item = _panel?.GetSelectItemData();
                if (item != null)
                    return item.m_itemNum;
            }
            catch { }
            return 0;
        }

        private string GetItemDescription()
        {
            try
            {
                var paramData = _panel?.GetSelectItemParam();
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

        private void CheckTargetChange()
        {
            // Only track target changes for Care-related item types
            if (_panel == null || _panel.m_type != uFieldItemPanel.Type.Care)
                return;

            var currentTarget = GetCurrentTarget();

            if (currentTarget != _lastTarget && (int)_lastTarget >= 0)
            {
                string partnerName = GetTargetPartnerName(currentTarget);
                if (!string.IsNullOrEmpty(partnerName))
                {
                    ScreenReader.Say(partnerName);
                    DebugLogger.Log($"{LogTag} Target changed to {currentTarget}: {partnerName}");
                }
            }

            _lastTarget = currentTarget;
        }

        private MainGameManager.ORDER_UNIT GetCurrentTarget()
        {
            try
            {
                // Get the target from the parent Care panel
                var mgr = MainGameManager.m_instance;
                if (mgr != null)
                {
                    var careUI = mgr.careUI;
                    if (careUI != null)
                    {
                        return careUI.m_target;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting target: {ex.Message}");
            }
            return MainGameManager.ORDER_UNIT.Partner00;
        }

        private string GetTargetPartnerName(MainGameManager.ORDER_UNIT target)
        {
            try
            {
                // Get the partner's actual Digimon name from the game
                Il2Cpp.PartnerCtrl partner = null;
                if (target == MainGameManager.ORDER_UNIT.Partner00)
                {
                    partner = MainGameManager.GetPartnerCtrl(0);
                }
                else if (target == MainGameManager.ORDER_UNIT.Partner01)
                {
                    partner = MainGameManager.GetPartnerCtrl(1);
                }

                if (partner != null)
                {
                    // Use gameData.m_commonData.m_name for the actual localized name
                    var commonData = partner.gameData?.m_commonData;
                    if (commonData != null)
                    {
                        var name = commonData.m_name;
                        if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                        {
                            return name;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting partner name: {ex.Message}");
            }

            // Fallback to generic partner label
            return target switch
            {
                MainGameManager.ORDER_UNIT.Partner00 => "Partner 1",
                MainGameManager.ORDER_UNIT.Partner01 => "Partner 2",
                MainGameManager.ORDER_UNIT.PartnerAll => "Both Partners",
                _ => "Partner"
            };
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var type = _panel.m_type;
            int internalTab = GetInternalTab();
            int cursor = GetCursorPosition();
            int total = GetItemCount();

            string tabName = GetTabName(type);
            string internalTabName = GetInternalTabName(internalTab);
            string itemInfo = GetItemInfo();

            string announcement;
            if (total == 0)
            {
                announcement = $"Items, {tabName}, {internalTabName} tab, empty";
            }
            else
            {
                announcement = $"Items, {tabName}, {internalTabName} tab. {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}";
                string desc = GetItemDescription();
                if (!string.IsNullOrEmpty(desc))
                    announcement += $". {desc}";
            }

            ScreenReader.Say(announcement);
        }
    }
}
