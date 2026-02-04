using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the restaurant/cooking panel.
    /// </summary>
    public class RestaurantPanelHandler : HandlerBase<uRestaurantPanel>
    {
        protected override string LogTag => "[RestaurantPanel]";
        public override int Priority => 60;

        private uRestaurantPanel.State _lastState = uRestaurantPanel.State.None;

        public override bool IsOpen()
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

        protected override void OnOpen()
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

            string announcement = AnnouncementBuilder.MenuOpenWithState("Restaurant", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);

            DebugLogger.Log($"{LogTag} Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
            _lastState = uRestaurantPanel.State.None;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckStateChange();
            CheckCursorChange();
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
                // Find the restaurant panel item which extends uItemBase
                var itemPanel = Object.FindObjectOfType<uRestaurantPanelItem>();
                if (itemPanel != null)
                {
                    return itemPanel.m_selectNo;
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
                DebugLogger.Log($"{LogTag} Error reading text: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Item", index);
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

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpenWithState("Restaurant", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
