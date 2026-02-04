using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the restaurant/cooking panel.
    /// </summary>
    public class RestaurantPanelHandler
    {
        private uRestaurantPanel _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uRestaurantPanel.State _lastState = uRestaurantPanel.State.None;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uRestaurantPanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_state;
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy &&
                       state != uRestaurantPanel.State.None;
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
            _lastState = uRestaurantPanel.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = $"Restaurant. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[RestaurantPanel] Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastState = uRestaurantPanel.State.None;
            DebugLogger.Log("[RestaurantPanel] Panel closed");
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
                DebugLogger.Log($"[RestaurantPanel] State changed to {state}");
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

                DebugLogger.Log($"[RestaurantPanel] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                // Find the restaurant panel item which extends uItemBase
                var itemPanel = Object.FindObjectOfType<uRestaurantPanelItem>();
                if (itemPanel != null)
                {
                    return itemPanel.m_selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[RestaurantPanel] Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                var itemPanel = Object.FindObjectOfType<uRestaurantPanelItem>();
                if (itemPanel != null)
                {
                    // Use GetSelectItemParam() to get ParameterItemData which has GetName()
                    var paramData = itemPanel.GetSelectItemParam();
                    if (paramData != null)
                    {
                        string name = paramData.GetName();
                        if (!string.IsNullOrEmpty(name))
                        {
                            return name;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[RestaurantPanel] Error reading text: {ex.Message}");
            }

            return $"Item {index + 1}";
        }

        private int GetMenuItemCount()
        {
            try
            {
                var itemPanel = Object.FindObjectOfType<uRestaurantPanelItem>();
                if (itemPanel != null)
                {
                    return itemPanel.m_maxListNum;
                }
            }
            catch { }
            return 1;
        }

        private string GetStateText(uRestaurantPanel.State state)
        {
            switch (state)
            {
                case uRestaurantPanel.State.ItemWait:
                    return "Select food";
                case uRestaurantPanel.State.UseItemMessageWait:
                    return "Confirm selection";
                case uRestaurantPanel.State.ItemEatCheck:
                    return "Eating";
                case uRestaurantPanel.State.CampCookingSEWait:
                    return "Cooking";
                case uRestaurantPanel.State.CampCookingFadeInWait:
                    return "Loading";
                case uRestaurantPanel.State.CampCookingSelectDigimonUpdate:
                    return "Select Digimon";
                default:
                    return "Restaurant";
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

            string announcement = $"Restaurant. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
