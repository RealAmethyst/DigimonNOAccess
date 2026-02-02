using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for zone change notifications (announces area name when entering new zones).
    ///
    /// The uZonePanel briefly appears when entering a new area, showing the zone name.
    /// We need to detect when the text changes since the panel object may persist in scene.
    /// </summary>
    public class ZonePanelHandler
    {
        private string _lastAnnouncedZone = "";
        private string _lastSeenText = "";
        private float _lastCheckTime = 0f;
        private const float CheckInterval = 0.1f; // Check 10 times per second

        public bool IsOpen()
        {
            var panel = FindZonePanel();
            if (panel == null)
                return false;

            try
            {
                // Check if panel's gameObject is active
                if (!panel.gameObject.activeInHierarchy)
                    return false;

                // Also check if there's actual text showing
                var label = panel.m_zone_label;
                if (label == null || string.IsNullOrWhiteSpace(label.text))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Update()
        {
            // Rate limit checks
            if (Time.time - _lastCheckTime < CheckInterval)
                return;
            _lastCheckTime = Time.time;

            var panel = FindZonePanel();
            if (panel == null)
                return;

            try
            {
                // Check if panel is active
                if (!panel.gameObject.activeInHierarchy)
                {
                    _lastSeenText = "";
                    return;
                }

                // Get current zone text
                var label = panel.m_zone_label;
                if (label == null)
                    return;

                string currentText = label.text;

                // Check if text changed and is valid
                if (!string.IsNullOrWhiteSpace(currentText) && currentText != _lastSeenText)
                {
                    _lastSeenText = currentText;

                    // Only announce if different from last announced zone
                    // (prevents re-announcing when panel just fades in/out with same text)
                    if (currentText != _lastAnnouncedZone)
                    {
                        _lastAnnouncedZone = currentText;
                        ScreenReader.Say(currentText);
                        DebugLogger.Log($"[ZonePanel] Entered zone: {currentText}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ZonePanel] Error in Update: {ex.Message}");
            }
        }

        private uZonePanel FindZonePanel()
        {
            try
            {
                // Prefer getting zone panel from uFieldPanel.m_instance (more reliable)
                var fieldPanel = uFieldPanel.m_instance;
                if (fieldPanel != null)
                {
                    var zonePanel = fieldPanel.m_zone_panel;
                    if (zonePanel != null)
                        return zonePanel;
                }

                // Fallback to FindObjectOfType
                return Object.FindObjectOfType<uZonePanel>();
            }
            catch
            {
                return null;
            }
        }
    }
}
