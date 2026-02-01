using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the map panel (world/area/minimap navigation)
    /// </summary>
    public class MapPanelHandler
    {
        private uDigiviceMapPanel _panel;
        private bool _wasActive = false;
        private uDigiviceMapPanel.State _lastState = uDigiviceMapPanel.State.NONE;
        private string _lastLocationName = "";

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uDigiviceMapPanel>();
            }

            if (_panel == null)
                return false;

            var state = _panel.m_state;
            return state != uDigiviceMapPanel.State.NONE && state != uDigiviceMapPanel.State.CLOSE;
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
                CheckLocationChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = uDigiviceMapPanel.State.NONE;
            _lastLocationName = "";

            if (_panel == null)
                return;

            var state = _panel.m_state;
            _lastState = state;

            string mapLevel = GetMapLevelName(state);
            string locationName = GetCurrentLocationName();
            _lastLocationName = locationName;

            string announcement;
            if (!string.IsNullOrEmpty(locationName))
            {
                announcement = $"Map, {mapLevel}. {locationName}";
            }
            else
            {
                announcement = $"Map, {mapLevel}";
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[MapPanel] Opened, state={state}, location={locationName}");
        }

        private void OnClose()
        {
            _panel = null;
            _lastState = uDigiviceMapPanel.State.NONE;
            _lastLocationName = "";
            DebugLogger.Log("[MapPanel] Closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var currentState = _panel.m_state;

            if (currentState != _lastState && currentState != uDigiviceMapPanel.State.CLOSE)
            {
                _lastLocationName = ""; // Reset location tracking for new map level
                string mapLevel = GetMapLevelName(currentState);
                string locationName = GetCurrentLocationName();
                _lastLocationName = locationName;

                string announcement;
                if (!string.IsNullOrEmpty(locationName))
                {
                    announcement = $"{mapLevel}. {locationName}";
                }
                else
                {
                    announcement = mapLevel;
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[MapPanel] State changed to {mapLevel}");
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
                DebugLogger.Log($"[MapPanel] Location changed: {currentLocation}");
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
                    // Try to get the currently selected location name
                    var currentLookingText = headline.m_CurrentLookingName;
                    if (currentLookingText != null)
                    {
                        string text = currentLookingText.text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }

                    // Fallback to map name
                    var mapNameText = headline.m_MapName;
                    if (mapNameText != null)
                    {
                        string text = mapNameText.text;
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[MapPanel] Error getting location name: {ex.Message}");
            }

            return "";
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string mapLevel = GetMapLevelName(state);
            string locationName = GetCurrentLocationName();

            string announcement;
            if (!string.IsNullOrEmpty(locationName))
            {
                announcement = $"Map, {mapLevel}. {locationName}";
            }
            else
            {
                announcement = $"Map, {mapLevel}";
            }

            ScreenReader.Say(announcement);
        }
    }
}
