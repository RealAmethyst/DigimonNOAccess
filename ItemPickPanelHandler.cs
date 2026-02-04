using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for item pickup panel (collecting items/materials in the field)
    /// </summary>
    public class ItemPickPanelHandler : HandlerBase<uItemPickPanel>
    {
        protected override string LogTag => "[ItemPickPanel]";
        public override int Priority => 90;

        private uItemPickPanel.State _lastState = uItemPickPanel.State.None;

        public override bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uItemPickPanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                return _panel.isOpen;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnOpen()
        {
            _lastState = uItemPickPanel.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            _lastState = state;

            string stateName = GetStateName(state);
            ScreenReader.Say($"Item pickup, {stateName}");
            DebugLogger.Log($"{LogTag} Opened, state={state}");
        }

        protected override void OnClose()
        {
            _lastState = uItemPickPanel.State.None;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckStateChange();
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var currentState = _panel.m_state;

            if (currentState != _lastState)
            {
                string announcement = GetStateAnnouncement(currentState);
                if (!string.IsNullOrEmpty(announcement))
                {
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"{LogTag} State changed to {currentState}");
                }
                _lastState = currentState;
            }
        }

        private string GetStateName(uItemPickPanel.State state)
        {
            return state switch
            {
                uItemPickPanel.State.CommandMain => "Menu",
                uItemPickPanel.State.ItemPick => "Picking item",
                uItemPickPanel.State.MaterialPick => "Picking material",
                uItemPickPanel.State.Result => "Result",
                uItemPickPanel.State.Close => "Closing",
                uItemPickPanel.State.Wait => "Please wait",
                _ => "Ready"
            };
        }

        private string GetStateAnnouncement(uItemPickPanel.State state)
        {
            switch (state)
            {
                case uItemPickPanel.State.CommandMain:
                    return "Choose action";

                case uItemPickPanel.State.ItemPick:
                    return "Picking up item";

                case uItemPickPanel.State.MaterialPick:
                    return "Picking up material";

                case uItemPickPanel.State.Result:
                    return GetResultAnnouncement();

                case uItemPickPanel.State.Close:
                case uItemPickPanel.State.None:
                case uItemPickPanel.State.Wait:
                    return null;

                default:
                    return GetStateName(state);
            }
        }

        private string GetResultAnnouncement()
        {
            try
            {
                var resultPanel = _panel?.m_itemPickPanelResult;
                if (resultPanel != null)
                {
                    var itemIds = _panel.m_itemIds;
                    if (itemIds != null)
                    {
                        string message = resultPanel.GetResultMessage(itemIds);
                        if (!string.IsNullOrEmpty(message))
                        {
                            return message;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting result: {ex.Message}");
            }

            return "Item obtained";
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateName = GetStateName(state);
            ScreenReader.Say($"Item pickup, {stateName}");
        }
    }
}
