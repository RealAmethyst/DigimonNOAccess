using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Digivolution Details panel (uGenealogy).
    /// This panel shows detailed evolution tree information when opened from
    /// the egg selection screen or other contexts.
    /// </summary>
    public class GenealogyHandler
    {
        private uGenealogy _panel;
        private bool _wasActive = false;
        private uGenealogy.State _lastState = uGenealogy.State.Main;
        private string _lastName = "";
        private string _lastNature = "";
        private string _lastAttr = "";

        /// <summary>
        /// Check if the genealogy panel is currently open and active.
        /// </summary>
        public bool IsOpen()
        {
            _panel = Object.FindObjectOfType<uGenealogy>();

            if (_panel == null)
                return false;

            try
            {
                return _panel.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called every frame to track state.
        /// </summary>
        public void Update()
        {
            bool isActive = IsOpen();

            // Panel just opened
            if (isActive && !_wasActive)
            {
                OnOpen();
            }
            // Panel just closed
            else if (!isActive && _wasActive)
            {
                OnClose();
            }
            // Panel is active, check for changes
            else if (isActive)
            {
                CheckStateChange();
                CheckSelectionChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = uGenealogy.State.Main;
            _lastName = "";
            _lastNature = "";
            _lastAttr = "";

            if (_panel == null)
                return;

            DebugLogger.Log("[Genealogy] === Panel Opened ===");
            LogPanelDebug();

            // Get current selection info
            string digimonInfo = GetCurrentDigimonInfo();

            string announcement = "Digivolution Details";
            if (!string.IsNullOrEmpty(digimonInfo))
            {
                announcement += ". " + digimonInfo;
            }
            announcement += ". Navigate with arrow keys.";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[Genealogy] Announced: {announcement}");

            // Track last values
            UpdateLastValues();
        }

        private void OnClose()
        {
            _panel = null;
            _lastName = "";
            _lastNature = "";
            _lastAttr = "";
            DebugLogger.Log("[Genealogy] Closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            try
            {
                uGenealogy.State state = _panel.m_state;
                if (state != _lastState)
                {
                    DebugLogger.Log($"[Genealogy] State changed: {_lastState} -> {state}");
                    _lastState = state;

                    // If scrolled to new position, announce new selection
                    if (state == uGenealogy.State.Main)
                    {
                        string info = GetCurrentDigimonInfo();
                        if (!string.IsNullOrEmpty(info))
                        {
                            ScreenReader.Say(info);
                            DebugLogger.Log($"[Genealogy] After scroll, announced: {info}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Genealogy] Error checking state: {ex.Message}");
            }
        }

        private void CheckSelectionChange()
        {
            if (_panel == null)
                return;

            try
            {
                // Only check when in Main state (not scrolling)
                if (_panel.m_state != uGenealogy.State.Main)
                    return;

                string currentName = GetNameText();
                string currentNature = GetNatureText();
                string currentAttr = GetAttrText();

                // Check if selection changed (name changed)
                if (currentName != _lastName && !string.IsNullOrEmpty(currentName))
                {
                    DebugLogger.Log($"[Genealogy] Selection changed: '{_lastName}' -> '{currentName}'");

                    string info = GetCurrentDigimonInfo();
                    if (!string.IsNullOrEmpty(info))
                    {
                        ScreenReader.Say(info);
                        DebugLogger.Log($"[Genealogy] Announced: {info}");
                    }

                    UpdateLastValues();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Genealogy] Error checking selection: {ex.Message}");
            }
        }

        private void UpdateLastValues()
        {
            if (_panel == null)
                return;

            _lastName = GetNameText();
            _lastNature = GetNatureText();
            _lastAttr = GetAttrText();
        }

        private string GetNameText()
        {
            if (_panel == null || _panel.m_name_text == null)
                return "";
            return _panel.m_name_text.text ?? "";
        }

        private string GetNatureText()
        {
            if (_panel == null || _panel.m_nature_text == null)
                return "";
            return _panel.m_nature_text.text ?? "";
        }

        private string GetAttrText()
        {
            if (_panel == null || _panel.m_attr_text == null)
                return "";
            return _panel.m_attr_text.text ?? "";
        }

        private string GetDetailText()
        {
            if (_panel == null || _panel.m_detail_text == null)
                return "";
            return _panel.m_detail_text.text ?? "";
        }

        private void LogPanelDebug()
        {
            if (_panel == null)
                return;

            try
            {
                DebugLogger.Log($"[Genealogy] m_state: {_panel.m_state}");

                if (_panel.m_name_text == null)
                    DebugLogger.Log("[Genealogy] m_name_text: NULL");
                else
                    DebugLogger.Log($"[Genealogy] m_name_text: '{_panel.m_name_text.text ?? "(null)"}'");

                if (_panel.m_nature_text == null)
                    DebugLogger.Log("[Genealogy] m_nature_text: NULL");
                else
                    DebugLogger.Log($"[Genealogy] m_nature_text: '{_panel.m_nature_text.text ?? "(null)"}'");

                if (_panel.m_attr_text == null)
                    DebugLogger.Log("[Genealogy] m_attr_text: NULL");
                else
                    DebugLogger.Log($"[Genealogy] m_attr_text: '{_panel.m_attr_text.text ?? "(null)"}'");

                if (_panel.m_detail_text == null)
                    DebugLogger.Log("[Genealogy] m_detail_text: NULL");
                else
                    DebugLogger.Log($"[Genealogy] m_detail_text: '{_panel.m_detail_text.text ?? "(null)"}'");

                // Log selection info
                DebugLogger.Log($"[Genealogy] m_selectGlowth: {_panel.m_selectGlowth}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Genealogy] Error in LogPanelDebug: {ex.Message}");
            }
        }

        private string GetCurrentDigimonInfo()
        {
            if (_panel == null)
                return "";

            string info = "";

            try
            {
                // Name
                string name = GetNameText();
                if (!string.IsNullOrEmpty(name))
                {
                    info = name;
                }

                // Nature
                string nature = GetNatureText();
                if (!string.IsNullOrEmpty(nature))
                {
                    if (!string.IsNullOrEmpty(info)) info += ". ";
                    info += "Nature: " + nature;
                }

                // Attribute
                string attr = GetAttrText();
                if (!string.IsNullOrEmpty(attr))
                {
                    if (!string.IsNullOrEmpty(info)) info += ". ";
                    info += "Attribute: " + attr;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Genealogy] Error reading digimon info: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Announce current status with full details.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            DebugLogger.Log("[Genealogy] === Status Request ===");
            LogPanelDebug();

            string info = GetCurrentDigimonInfo();

            // Also get the detail/description text
            string detail = GetDetailText();

            string announcement = "Digivolution Details";
            if (!string.IsNullOrEmpty(info))
            {
                announcement += ". " + info;
            }
            if (!string.IsNullOrEmpty(detail))
            {
                announcement += ". Description: " + detail;
            }

            ScreenReader.Say(announcement);
        }
    }
}
