using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Digivolution Details panel (uGenealogy).
    /// This panel shows detailed evolution tree information when opened from
    /// the egg selection screen or other contexts.
    /// </summary>
    public class GenealogyHandler : HandlerBase<uGenealogy>
    {
        protected override string LogTag => "[Genealogy]";
        public override int Priority => 40;

        private uGenealogy.State _lastGenealogyState = uGenealogy.State.Main;
        private string _lastName = "";
        private string _lastNature = "";
        private string _lastAttr = "";

        public override bool IsOpen()
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

        protected override void OnOpen()
        {
            _lastGenealogyState = uGenealogy.State.Main;
            _lastName = "";
            _lastNature = "";
            _lastAttr = "";

            if (_panel == null)
                return;

            DebugLogger.Log($"{LogTag} === Panel Opened ===");
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
            DebugLogger.Log($"{LogTag} Announced: {announcement}");

            // Track last values
            UpdateLastValues();
        }

        protected override void OnClose()
        {
            _lastName = "";
            _lastNature = "";
            _lastAttr = "";
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckStateChange();
            CheckSelectionChange();
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            try
            {
                uGenealogy.State state = _panel.m_state;
                if (state != _lastGenealogyState)
                {
                    DebugLogger.Log($"{LogTag} State changed: {_lastGenealogyState} -> {state}");
                    _lastGenealogyState = state;

                    // If scrolled to new position, announce new selection
                    if (state == uGenealogy.State.Main)
                    {
                        string info = GetCurrentDigimonInfo();
                        if (!string.IsNullOrEmpty(info))
                        {
                            ScreenReader.Say(info);
                            DebugLogger.Log($"{LogTag} After scroll, announced: {info}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking state: {ex.Message}");
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
                    DebugLogger.Log($"{LogTag} Selection changed: '{_lastName}' -> '{currentName}'");

                    string info = GetCurrentDigimonInfo();
                    if (!string.IsNullOrEmpty(info))
                    {
                        ScreenReader.Say(info);
                        DebugLogger.Log($"{LogTag} Announced: {info}");
                    }

                    UpdateLastValues();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking selection: {ex.Message}");
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
                DebugLogger.Log($"{LogTag} m_state: {_panel.m_state}");

                if (_panel.m_name_text == null)
                    DebugLogger.Log($"{LogTag} m_name_text: NULL");
                else
                    DebugLogger.Log($"{LogTag} m_name_text: '{_panel.m_name_text.text ?? "(null)"}'");

                if (_panel.m_nature_text == null)
                    DebugLogger.Log($"{LogTag} m_nature_text: NULL");
                else
                    DebugLogger.Log($"{LogTag} m_nature_text: '{_panel.m_nature_text.text ?? "(null)"}'");

                if (_panel.m_attr_text == null)
                    DebugLogger.Log($"{LogTag} m_attr_text: NULL");
                else
                    DebugLogger.Log($"{LogTag} m_attr_text: '{_panel.m_attr_text.text ?? "(null)"}'");

                if (_panel.m_detail_text == null)
                    DebugLogger.Log($"{LogTag} m_detail_text: NULL");
                else
                    DebugLogger.Log($"{LogTag} m_detail_text: '{_panel.m_detail_text.text ?? "(null)"}'");

                // Log selection info
                DebugLogger.Log($"{LogTag} m_selectGlowth: {_panel.m_selectGlowth}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error in LogPanelDebug: {ex.Message}");
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
                DebugLogger.Log($"{LogTag} Error reading digimon info: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Announce current status with full details.
        /// </summary>
        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            DebugLogger.Log($"{LogTag} === Status Request ===");
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
