using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Digi-Egg selection screen (rebirth/new game).
    /// Uses uRebirthPanel which displays egg selection UI.
    /// </summary>
    public class DigiEggHandler
    {
        private uRebirthPanel _panel;
        private bool _wasActive = false;
        private int _lastEggIndex = -1;
        private int _lastState = -1;
        private string _lastCaption1 = "";
        private string _lastCaption2 = "";

        /// <summary>
        /// Check if the egg selection panel is currently open.
        /// </summary>
        public bool IsOpen()
        {
            _panel = Object.FindObjectOfType<uRebirthPanel>();

            if (_panel == null)
                return false;

            try
            {
                return _panel.IsOpened();
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
            // Panel is active, check for selection changes
            else if (isActive)
            {
                CheckSelectionChange();
                CheckStateChange();
                CheckCaptionChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastEggIndex = -1;
            _lastState = -1;
            _lastCaption1 = "";
            _lastCaption2 = "";

            if (_panel == null)
                return;

            int eggIndex = _panel.selectEgg;
            int eggMax = _panel.eggMax;

            // Debug: Log all panel state
            DebugLogger.Log($"[DigiEgg] === Panel Opened ===");
            DebugLogger.Log($"[DigiEgg] selectEgg={eggIndex}, eggMax={eggMax}");
            DebugLogger.Log($"[DigiEgg] m_state={_panel.m_state}");

            // Debug: Log text field availability and values
            LogTextFieldDebug();

            // Read the egg info from panel text fields
            string eggInfo = GetCurrentEggInfo();

            string announcement = $"Choose your Digi-Egg. Left and right to browse. Egg {eggIndex + 1} of {eggMax}";
            if (!string.IsNullOrEmpty(eggInfo))
            {
                announcement += ". " + eggInfo;
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[DigiEgg] Announced: {announcement}");

            _lastEggIndex = eggIndex;
            _lastState = _panel.m_state;
        }

        private void OnClose()
        {
            _panel = null;
            _lastEggIndex = -1;
            _lastState = -1;
            _lastCaption1 = "";
            _lastCaption2 = "";
            DebugLogger.Log("[DigiEgg] Closed");
        }

        private void CheckSelectionChange()
        {
            if (_panel == null)
                return;

            int eggIndex = _panel.selectEgg;
            if (eggIndex != _lastEggIndex)
            {
                int eggMax = _panel.eggMax;

                // Debug: Log text field values on each change
                DebugLogger.Log($"[DigiEgg] === Selection Changed ===");
                LogTextFieldDebug();

                string eggInfo = GetCurrentEggInfo();

                string announcement = $"Egg {eggIndex + 1} of {eggMax}";
                if (!string.IsNullOrEmpty(eggInfo))
                {
                    announcement += ". " + eggInfo;
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[DigiEgg] Announced: {announcement}");

                _lastEggIndex = eggIndex;
            }
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            int state = _panel.m_state;
            if (state != _lastState)
            {
                DebugLogger.Log($"[DigiEgg] State changed: {_lastState} -> {state}");
                _lastState = state;
            }
        }

        private void CheckCaptionChange()
        {
            if (_panel == null)
                return;

            try
            {
                // Check caption1
                if (_panel.m_caption1 != null && _panel.m_caption1.m_text != null)
                {
                    string caption1 = _panel.m_caption1.m_text.text ?? "";
                    if (caption1 != _lastCaption1 && !string.IsNullOrEmpty(caption1))
                    {
                        DebugLogger.Log($"[DigiEgg] Caption1 changed: '{caption1}'");
                        _lastCaption1 = caption1;
                    }
                }

                // Check caption2
                if (_panel.m_caption2 != null && _panel.m_caption2.m_text != null)
                {
                    string caption2 = _panel.m_caption2.m_text.text ?? "";
                    if (caption2 != _lastCaption2 && !string.IsNullOrEmpty(caption2))
                    {
                        DebugLogger.Log($"[DigiEgg] Caption2 changed: '{caption2}'");
                        _lastCaption2 = caption2;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DigiEgg] Error checking captions: {ex.Message}");
            }
        }

        private void LogTextFieldDebug()
        {
            if (_panel == null)
                return;

            try
            {
                // Headline text
                if (_panel.m_headLineText == null)
                    DebugLogger.Log("[DigiEgg] m_headLineText: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_headLineText: '{_panel.m_headLineText.text ?? "(null text)"}'");

                // Nature text
                if (_panel.m_natureText == null)
                    DebugLogger.Log("[DigiEgg] m_natureText: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_natureText: '{_panel.m_natureText.text ?? "(null text)"}'");

                // Attribute text
                if (_panel.m_attrText == null)
                    DebugLogger.Log("[DigiEgg] m_attrText: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_attrText: '{_panel.m_attrText.text ?? "(null text)"}'");

                // Jijimon text
                if (_panel.m_jijimonText == null)
                    DebugLogger.Log("[DigiEgg] m_jijimonText: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_jijimonText: '{_panel.m_jijimonText.text ?? "(null text)"}'");

                // Also check m_text field
                if (_panel.m_text == null)
                    DebugLogger.Log("[DigiEgg] m_text: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_text: '{_panel.m_text.text ?? "(null text)"}'");

                // Check simple genealogy
                if (_panel.m_simple_genealogy == null)
                    DebugLogger.Log("[DigiEgg] m_simple_genealogy: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_simple_genealogy: exists, active={_panel.m_simple_genealogy.gameObject.activeInHierarchy}");

                // Check full genealogy
                if (_panel.m_genealogy == null)
                    DebugLogger.Log("[DigiEgg] m_genealogy: NULL");
                else
                {
                    DebugLogger.Log($"[DigiEgg] m_genealogy: exists, active={_panel.m_genealogy.gameObject.activeInHierarchy}");
                    // If genealogy has text fields, log them
                    if (_panel.m_genealogy.m_name_text != null)
                        DebugLogger.Log($"[DigiEgg] m_genealogy.m_name_text: '{_panel.m_genealogy.m_name_text.text ?? "(null)"}'");
                    if (_panel.m_genealogy.m_nature_text != null)
                        DebugLogger.Log($"[DigiEgg] m_genealogy.m_nature_text: '{_panel.m_genealogy.m_nature_text.text ?? "(null)"}'");
                    if (_panel.m_genealogy.m_attr_text != null)
                        DebugLogger.Log($"[DigiEgg] m_genealogy.m_attr_text: '{_panel.m_genealogy.m_attr_text.text ?? "(null)"}'");
                }

                // Check rebirth message
                if (_panel.m_rebirth_message == null)
                    DebugLogger.Log("[DigiEgg] m_rebirth_message: NULL");
                else
                    DebugLogger.Log($"[DigiEgg] m_rebirth_message: exists, active={_panel.m_rebirth_message.gameObject.activeInHierarchy}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DigiEgg] Error in LogTextFieldDebug: {ex.Message}");
            }
        }

        private string GetCurrentEggInfo()
        {
            if (_panel == null)
                return "";

            string info = "";

            try
            {
                // NOTE: The text fields m_headLineText, m_natureText, m_attrText, m_jijimonText
                // appear to be LABELS (showing "L Partner", "Nature", "Attr", "Jijimon") not actual data.
                // The actual content is in m_text (Jijimon's comment about the egg).

                // Get Jijimon's actual comment from m_text
                if (_panel.m_text != null && !string.IsNullOrEmpty(_panel.m_text.text))
                {
                    info = _panel.m_text.text;
                }

                // If m_genealogy is active, get the Digimon info from there
                // (This shows when the details panel is open)
                if (_panel.m_genealogy != null && _panel.m_genealogy.gameObject.activeInHierarchy)
                {
                    string digiName = "";
                    string nature = "";
                    string attr = "";

                    if (_panel.m_genealogy.m_name_text != null)
                        digiName = _panel.m_genealogy.m_name_text.text ?? "";

                    if (_panel.m_genealogy.m_nature_text != null)
                        nature = _panel.m_genealogy.m_nature_text.text ?? "";

                    if (_panel.m_genealogy.m_attr_text != null)
                        attr = _panel.m_genealogy.m_attr_text.text ?? "";

                    if (!string.IsNullOrEmpty(digiName))
                    {
                        info = digiName;
                        if (!string.IsNullOrEmpty(nature))
                            info += ". Nature: " + nature;
                        if (!string.IsNullOrEmpty(attr))
                            info += ". Attribute: " + attr;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[DigiEgg] Error reading egg info: {ex.Message}");
            }

            return info;
        }

        /// <summary>
        /// Announce current status.
        /// </summary>
        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            // Force debug logging
            DebugLogger.Log("[DigiEgg] === Status Request ===");
            LogTextFieldDebug();

            int eggIndex = _panel.selectEgg;
            int eggMax = _panel.eggMax;
            string eggInfo = GetCurrentEggInfo();

            string announcement = $"Digi-Egg Selection. Egg {eggIndex + 1} of {eggMax}";
            if (!string.IsNullOrEmpty(eggInfo))
            {
                announcement += ". " + eggInfo;
            }
            ScreenReader.Say(announcement);
        }
    }
}
