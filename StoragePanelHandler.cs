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
        private uStoragePanelInfo.Type _lastArrowType = uStoragePanelInfo.Type.MAX;

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
            _lastArrowType = uStoragePanelInfo.Type.MAX;

            if (_panel == null)
                return;

            ScreenReader.Say("Storage");

            // Determine which side is initially focused and announce it
            try
            {
                var infoPanel = _panel.m_infoPanel;
                if (infoPanel != null)
                    _lastArrowType = infoPanel.m_arrowType;
            }
            catch { }

            bool leftFocused = _lastArrowType == uStoragePanelInfo.Type.RIGHT;
            var focusedPanel = leftFocused ? _panel.m_itemPanelL : _panel.m_itemPanelR;
            if (focusedPanel != null)
            {
                string sectionName = GetStorageTypeName(focusedPanel.m_storageType);
                string itemInfo = GetItemInfo(focusedPanel);
                int cursor = focusedPanel.m_selectNo;
                int total = GetItemCount(focusedPanel);
                string itemAnnouncement = total > 0
                    ? $"{sectionName}, {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}"
                    : $"{sectionName}, empty";
                ScreenReader.SayQueued(itemAnnouncement);
            }

            UpdateLastCursors();
            DebugLogger.Log($"[StoragePanel] Opened");
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursorL = -1;
            _lastCursorR = -1;
            _lastArrowType = uStoragePanelInfo.Type.MAX;
            DebugLogger.Log("[StoragePanel] Closed");
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            var leftPanel = _panel.m_itemPanelL;
            var rightPanel = _panel.m_itemPanelR;

            // Detect side switch via info panel arrow direction
            bool sideSwitched = false;
            try
            {
                var infoPanel = _panel.m_infoPanel;
                if (infoPanel != null)
                {
                    var arrowType = infoPanel.m_arrowType;
                    if (arrowType != _lastArrowType)
                    {
                        sideSwitched = true;
                        _lastArrowType = arrowType;
                    }
                }
            }
            catch { }

            // Check left panel
            if (leftPanel != null)
            {
                int cursorL = leftPanel.m_selectNo;
                bool leftChanged = cursorL != _lastCursorL;
                bool leftJustFocused = sideSwitched && _lastArrowType == uStoragePanelInfo.Type.RIGHT;

                if (leftChanged || leftJustFocused)
                {
                    AnnounceItemChange(leftPanel, "Left", leftJustFocused);
                    _lastCursorL = cursorL;
                }
            }

            // Check right panel
            if (rightPanel != null)
            {
                int cursorR = rightPanel.m_selectNo;
                bool rightChanged = cursorR != _lastCursorR;
                bool rightJustFocused = sideSwitched && _lastArrowType == uStoragePanelInfo.Type.LEFT;

                if (rightChanged || rightJustFocused)
                {
                    AnnounceItemChange(rightPanel, "Right", rightJustFocused);
                    _lastCursorR = cursorR;
                }
            }
        }

        private void AnnounceItemChange(uStoragePanelItem panel, string side, bool includeSectionName)
        {
            if (panel == null)
                return;

            try
            {
                int cursor = panel.m_selectNo;
                int total = GetItemCount(panel);
                string sectionName = GetStorageTypeName(panel.m_storageType);
                string itemInfo = GetItemInfo(panel);

                string announcement;
                if (total == 0)
                {
                    announcement = $"{sectionName}, empty";
                }
                else if (includeSectionName)
                {
                    announcement = $"{sectionName}, {AnnouncementBuilder.CursorPosition(itemInfo, cursor, total)}";
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
