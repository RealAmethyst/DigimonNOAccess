using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the field item panel (inventory during gameplay)
    /// </summary>
    public class FieldItemPanelHandler
    {
        private uFieldItemPanel _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uFieldItemPanel.Type _lastType = (uFieldItemPanel.Type)(-1);
        private int _lastInternalTab = -1;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uFieldItemPanel>();
            }

            return _panel != null &&
                   _panel.gameObject != null &&
                   _panel.gameObject.activeInHierarchy;
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
                CheckTabChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastType = (uFieldItemPanel.Type)(-1);
            _lastInternalTab = -1;

            if (_panel == null)
                return;

            _lastType = _panel.m_type;
            _lastInternalTab = GetInternalTab();
            int cursor = GetCursorPosition();
            int total = GetItemCount();

            string tabName = GetTabName(_lastType);
            string internalTabName = GetInternalTabName(_lastInternalTab);
            string itemInfo = GetItemInfo();

            string announcement;
            if (total == 0)
            {
                announcement = $"Items, {tabName}, {internalTabName} tab, empty";
            }
            else
            {
                announcement = $"Items, {tabName}, {internalTabName} tab. {itemInfo}, {cursor + 1} of {total}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[FieldItemPanel] Opened, type={tabName}, tab={internalTabName}, cursor={cursor}, total={total}");
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastType = (uFieldItemPanel.Type)(-1);
            _lastInternalTab = -1;
            DebugLogger.Log("[FieldItemPanel] Closed");
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
                    announcement = $"{itemInfo}, {cursor + 1} of {total}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[FieldItemPanel] Cursor changed: {itemInfo}");
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
                    announcement = $"{tabName}, {internalTabName} tab. {itemInfo}, {cursor + 1} of {total}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[FieldItemPanel] Type changed to {tabName}, tab={internalTabName}");
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
                    announcement = $"{internalTabName} tab. {itemInfo}, {cursor + 1} of {total}";
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[FieldItemPanel] Internal tab changed to {internalTabName}");
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
                DebugLogger.Log($"[FieldItemPanel] Error getting internal tab: {ex.Message}");
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
                DebugLogger.Log($"[FieldItemPanel] Error getting cursor: {ex.Message}");
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
                DebugLogger.Log($"[FieldItemPanel] Error getting item count: {ex.Message}");
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
                        return name;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[FieldItemPanel] Error getting item info: {ex.Message}");
            }

            return "Unknown Item";
        }

        public void AnnounceStatus()
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
                announcement = $"Items, {tabName}, {internalTabName} tab. {itemInfo}, {cursor + 1} of {total}";
            }

            ScreenReader.Say(announcement);
        }
    }
}
