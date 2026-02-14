using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the map panel (world/area/minimap navigation)
    /// and the town jump panel (fast travel when in the city).
    /// Both open with the same key but are different UI systems.
    /// </summary>
    public class MapPanelHandler : HandlerBase<uDigiviceMapPanel>
    {
        protected override string LogTag => "[MapPanel]";
        public override int Priority => 65;

        // Field map state
        private uDigiviceMapPanel.State _lastState = uDigiviceMapPanel.State.NONE;
        private string _lastLocationName = "";

        // Town jump state
        private uTownJumpPanel _townJumpPanel;
        private uTownJumpPanelCommand _townJumpCommand;
        private bool _townJumpActive;
        private int _lastTownJumpCursor = -1;

        public override bool IsOpen()
        {
            // Check town jump first (asset-loaded via MainGameManager)
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    var tjPanel = mgm.townJumpUI;
                    if (tjPanel != null && tjPanel.m_state == uTownJumpPanel.State.CommandMain)
                    {
                        _townJumpPanel = tjPanel;
                        _townJumpActive = true;
                        return true;
                    }
                }
            }
            catch { }

            _townJumpActive = false;
            _townJumpPanel = null;

            // Then check field map (scene object)
            if (_panel == null)
                _panel = Object.FindObjectOfType<uDigiviceMapPanel>();

            if (_panel == null)
                return false;

            var state = _panel.m_state;
            return state != uDigiviceMapPanel.State.NONE && state != uDigiviceMapPanel.State.CLOSE;
        }

        protected override void OnOpen()
        {
            if (_townJumpActive)
            {
                OnTownJumpOpen();
                return;
            }

            _lastState = uDigiviceMapPanel.State.NONE;
            _lastLocationName = "";

            if (_panel == null)
                return;

            var state = _panel.m_state;
            _lastState = state;

            string mapLevel = GetMapLevelName(state);
            string locationName = GetCurrentLocationName();
            _lastLocationName = locationName;

            string announcement = !string.IsNullOrEmpty(locationName)
                ? $"Map, {mapLevel}. {locationName}"
                : $"Map, {mapLevel}";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened, state={state}, location={locationName}");
        }

        protected override void OnClose()
        {
            if (_townJumpActive || _townJumpPanel != null)
                OnTownJumpClose();

            _lastState = uDigiviceMapPanel.State.NONE;
            _lastLocationName = "";
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (_townJumpActive)
            {
                UpdateTownJump();
                return;
            }

            CheckStateChange();
            CheckLocationChange();
        }

        // ── Town Jump (Fast Travel) ──

        private void OnTownJumpOpen()
        {
            _lastTownJumpCursor = -1;

            try { _townJumpCommand = _townJumpPanel?.m_townJumpPanelCommand; }
            catch { _townJumpCommand = null; }

            int cursor = GetTownJumpCursor();
            string destination = GetTownJumpDestinationName(cursor);
            int total = GetTownJumpItemCount();

            string announcement = $"Fast Travel. {destination}. {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Town Jump opened, cursor={cursor}, items={total}");
            _lastTownJumpCursor = cursor;
        }

        private void OnTownJumpClose()
        {
            _townJumpPanel = null;
            _townJumpCommand = null;
            _townJumpActive = false;
            _lastTownJumpCursor = -1;
        }

        private void UpdateTownJump()
        {
            if (_townJumpCommand == null) return;

            int cursor = GetTownJumpCursor();
            if (cursor == _lastTownJumpCursor || cursor < 0) return;

            string destination = GetTownJumpDestinationName(cursor);
            int total = GetTownJumpItemCount();

            ScreenReader.Say($"{destination}. {cursor + 1} of {total}");
            DebugLogger.Log($"{LogTag} Town Jump cursor: {destination} ({cursor + 1}/{total})");
            _lastTownJumpCursor = cursor;
        }

        private int GetTownJumpCursor()
        {
            try
            {
                if (_townJumpCommand != null)
                    return _townJumpCommand.m_selectNo;
            }
            catch { }
            return 0;
        }

        private int GetTownJumpItemCount()
        {
            try
            {
                if (_townJumpCommand != null)
                    return _townJumpCommand.m_itemMaxNum;
            }
            catch { }
            return 1;
        }

        private string GetTownJumpDestinationName(int index)
        {
            // Primary: read from UI text
            try
            {
                if (_townJumpCommand != null)
                {
                    var parts = _townJumpCommand.GetSelectItemParts(index);
                    if (parts?.m_name != null)
                    {
                        string uiText = parts.m_name.text;
                        if (!string.IsNullOrEmpty(uiText))
                        {
                            string cleaned = TextUtilities.StripRichTextTags(uiText);
                            if (!string.IsNullOrEmpty(cleaned))
                                return cleaned;
                        }
                    }
                }
            }
            catch { }

            // Fallback: headline text
            try
            {
                var headline = _townJumpPanel?.m_townJumpPanelHeadLine;
                if (headline?.m_text != null)
                {
                    string text = headline.m_text.text;
                    if (!string.IsNullOrEmpty(text))
                        return TextUtilities.StripRichTextTags(text);
                }
            }
            catch { }

            return AnnouncementBuilder.FallbackItem("Destination", index);
        }

        // ── Field Map ──

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var currentState = _panel.m_state;

            if (currentState != _lastState && currentState != uDigiviceMapPanel.State.CLOSE)
            {
                _lastLocationName = "";
                string mapLevel = GetMapLevelName(currentState);
                string locationName = GetCurrentLocationName();
                _lastLocationName = locationName;

                string announcement = !string.IsNullOrEmpty(locationName)
                    ? $"{mapLevel}. {locationName}"
                    : mapLevel;

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} State changed to {mapLevel}");
                _lastState = currentState;
            }
        }

        private void CheckLocationChange()
        {
            if (_panel == null)
                return;

            string currentLocation = GetCurrentLocationName();

            if (!string.IsNullOrEmpty(currentLocation) && currentLocation != _lastLocationName)
            {
                ScreenReader.Say(currentLocation);
                DebugLogger.Log($"{LogTag} Location changed: {currentLocation}");
                _lastLocationName = currentLocation;
            }
        }

        private string GetMapLevelName(uDigiviceMapPanel.State state)
        {
            return state switch
            {
                uDigiviceMapPanel.State.WORLD => "World Map",
                uDigiviceMapPanel.State.AREA => "Area Map",
                uDigiviceMapPanel.State.MINI_AREA => "Local Map",
                _ => "Map"
            };
        }

        private string GetCurrentLocationName()
        {
            try
            {
                var headline = _panel?.m_uDigiviceMapPanelHeadLine;
                if (headline != null)
                {
                    var currentLookingText = headline.m_CurrentLookingName;
                    if (currentLookingText != null)
                    {
                        string text = currentLookingText.text;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }

                    var mapNameText = headline.m_MapName;
                    if (mapNameText != null)
                    {
                        string text = mapNameText.text;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting location name: {ex.Message}");
            }

            return "";
        }

        // ── Status ──

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            if (_townJumpActive)
            {
                int cursor = GetTownJumpCursor();
                string destination = GetTownJumpDestinationName(cursor);
                int total = GetTownJumpItemCount();
                ScreenReader.Say($"Fast Travel. {destination}. {cursor + 1} of {total}");
                return;
            }

            var state = _panel.m_state;
            string mapLevel = GetMapLevelName(state);
            string locationName = GetCurrentLocationName();

            string announcement = !string.IsNullOrEmpty(locationName)
                ? $"Map, {mapLevel}. {locationName}"
                : $"Map, {mapLevel}";

            ScreenReader.Say(announcement);
        }
    }
}
