using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the storage panel (item storage management)
    /// </summary>
    public class StoragePanelHandler : IAccessibilityHandler
    {
        public int Priority => 65;

        private uStoragePanel _panel;
        private bool _wasActive = false;
        private int _lastCursorL = -1;
        private int _lastCursorR = -1;
        private ItemStorageData.StorageType _lastActivePanel = (ItemStorageData.StorageType)(-1);

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uStoragePanel>();
            }

            if (_panel == null)
                return false;

            return _panel.m_enabelPanel;
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
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursorL = -1;
            _lastCursorR = -1;
            _lastActivePanel = (ItemStorageData.StorageType)(-1);

            if (_panel == null)
                return;

            string announcement = BuildFullAnnouncement();
            ScreenReader.Say(announcement);

            UpdateLastCursors();
            DebugLogger.Log($"[StoragePanel] Opened");
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursorL = -1;
            _lastCursorR = -1;
            _lastActivePanel = (ItemStorageData.StorageType)(-1);
            DebugLogger.Log("[StoragePanel] Closed");
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            var leftPanel = _panel.m_itemPanelL;
            var rightPanel = _panel.m_itemPanelR;

            // Check which panel is active and announce changes
            if (leftPanel != null)
            {
                int cursorL = leftPanel.m_selectNo;
                if (cursorL != _lastCursorL)
                {
                    AnnounceItemChange(leftPanel, "Left");
                    _lastCursorL = cursorL;
                }
            }

            if (rightPanel != null)
            {
                int cursorR = rightPanel.m_selectNo;
                if (cursorR != _lastCursorR)
                {
                    AnnounceItemChange(rightPanel, "Right");
                    _lastCursorR = cursorR;
                }
            }
        }

        private void AnnounceItemChange(uStoragePanelItem panel, string side)
        {
            if (panel == null)
                return;

            try
            {
                int cursor = panel.m_selectNo;
                int total = GetItemCount(panel);
                string storageType = GetStorageTypeName(panel.m_storageType);
                string itemInfo = GetItemInfo(panel);

                string announcement;
                if (total == 0)
                {
                    announcement = $"{side}, {storageType}, empty";
                }
                else
                {
                    announcement = AnnouncementBuilder.CursorPosition(itemInfo, cursor, total);
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[StoragePanel] {side} cursor changed: {itemInfo}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[StoragePanel] Error announcing item change: {ex.Message}");
            }
        }

        private string BuildFullAnnouncement()
        {
            var leftPanel = _panel?.m_itemPanelL;
            var rightPanel = _panel?.m_itemPanelR;

            string leftInfo = GetPanelSummary(leftPanel, "Left");
            string rightInfo = GetPanelSummary(rightPanel, "Right");

            return $"Storage. {leftInfo}. {rightInfo}";
        }

        private string GetPanelSummary(uStoragePanelItem panel, string side)
        {
            if (panel == null)
                return $"{side} panel unavailable";

            try
            {
                string storageType = GetStorageTypeName(panel.m_storageType);
                int total = GetItemCount(panel);

                if (total == 0)
                {
                    return $"{side}: {storageType}, empty";
                }

                int cursor = panel.m_selectNo;
                string itemInfo = GetItemInfo(panel);
                return $"{side}: {storageType}, {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[StoragePanel] Error getting panel summary: {ex.Message}");
                return $"{side} panel";
            }
        }

        private string GetStorageTypeName(ItemStorageData.StorageType type)
        {
            return type switch
            {
                ItemStorageData.StorageType.PLAYER => "Inventory",
                ItemStorageData.StorageType.SHOP => "Storage",
                ItemStorageData.StorageType.MATERIAL => "Materials",
                ItemStorageData.StorageType.KEY_ITEM => "Key Items",
                _ => "Items"
            };
        }

        private int GetItemCount(uStoragePanelItem panel)
        {
            try
            {
                var itemList = panel?.m_itemList;
                if (itemList != null)
                {
                    return itemList.Count;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[StoragePanel] Error in GetItemCount: {ex.Message}");
            }
            return 0;
        }

        private string GetItemInfo(uStoragePanelItem panel)
        {
            try
            {
                var paramData = panel?.GetSelectItemParam();
                if (paramData != null)
                {
                    string name = paramData.GetName();
                    if (!string.IsNullOrEmpty(name))
                    {
                        return name;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[StoragePanel] Error getting item info: {ex.Message}");
            }

            return "Unknown Item";
        }

        private void UpdateLastCursors()
        {
            if (_panel?.m_itemPanelL != null)
                _lastCursorL = _panel.m_itemPanelL.m_selectNo;
            if (_panel?.m_itemPanelR != null)
                _lastCursorR = _panel.m_itemPanelR.m_selectNo;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            string announcement = BuildFullAnnouncement();
            ScreenReader.Say(announcement);
        }
    }
}
