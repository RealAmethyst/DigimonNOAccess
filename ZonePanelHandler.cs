using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for zone change notifications (announces area name when entering new zones)
    /// </summary>
    public class ZonePanelHandler
    {
        private uZonePanel _panel;
        private bool _wasActive = false;
        private string _lastAnnouncedZone = "";

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uZonePanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                return _panel.gameObject != null && _panel.gameObject.activeInHierarchy;
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

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            if (_panel == null)
                return;

            string zoneName = GetZoneName();

            // Only announce if we have a valid name and it's different from last announced
            if (!string.IsNullOrEmpty(zoneName) && zoneName != _lastAnnouncedZone)
            {
                ScreenReader.Say(zoneName);
                _lastAnnouncedZone = zoneName;
                DebugLogger.Log($"[ZonePanel] Entered zone: {zoneName}");
            }
        }

        private void OnClose()
        {
            _panel = null;
            DebugLogger.Log("[ZonePanel] Closed");
        }

        private string GetZoneName()
        {
            try
            {
                var label = _panel?.m_zone_label;
                if (label != null)
                {
                    return label.text;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ZonePanel] Error getting zone name: {ex.Message}");
            }
            return null;
        }
    }
}
