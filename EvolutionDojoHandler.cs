using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    public class EvolutionDojoHandler : IAccessibilityHandler
    {
        private const string LogTag = "[EvoDojo]";
        public int Priority => 70;

        private uEvolutionDojoPanel _panel;
        private bool _wasOpen;
        private int _lastCursorX = -1;
        private int _lastCursorY = -1;
        private int _lastSelectPartner = -1;
        private uGenelogyUI.Type _lastType = uGenelogyUI.Type.None;
        private string _lastAnnouncedName = "";

        private static readonly string[] GrowthNames =
            { "Baby 1", "Baby 2", "Rookie", "Champion", "Ultimate", "Mega", "Ultra" };

        public bool IsOpen()
        {
            try
            {
                _panel = uEvolutionDojoPanel.Ref;
                if (_panel == null) return false;
                return _panel.m_State == uEvolutionDojoPanel.State.Normal;
            }
            catch
            {
                _panel = null;
                return false;
            }
        }

        public void Update()
        {
            bool isOpen = IsOpen();

            if (isOpen && !_wasOpen)
                OnOpen();
            else if (!isOpen && _wasOpen)
                OnClose();
            else if (isOpen)
                OnUpdate();

            _wasOpen = isOpen;
        }

        private void OnOpen()
        {
            _lastCursorX = -1;
            _lastCursorY = -1;
            _lastSelectPartner = -1;
            _lastType = uGenelogyUI.Type.None;
            _lastAnnouncedName = "";

            var genelogy = GetGenelogy();
            if (genelogy == null)
            {
                ScreenReader.Say("Evolution Dojo");
                return;
            }

            _lastSelectPartner = _panel.m_SelectPartner;
            _lastType = genelogy.m_Type;

            string generation = GetGenerationText();
            string announcement = "Evolution Dojo";
            if (!string.IsNullOrEmpty(generation))
                announcement += ", Generation " + generation;

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened: {announcement}");

            // Announce cursor position (name, stage, probability, index, description)
            _lastCursorX = genelogy.m_CursorX;
            _lastCursorY = genelogy.m_CursorY;
            AnnounceCursorItem(genelogy, queued: true);
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursorX = -1;
            _lastCursorY = -1;
            _lastSelectPartner = -1;
            _lastAnnouncedName = "";
            DebugLogger.Log($"{LogTag} Closed");
        }

        private void OnUpdate()
        {
            if (_panel == null) return;

            var genelogy = GetGenelogy();
            if (genelogy == null) return;

            try
            {
                // Check partner switch
                int currentPartner = _panel.m_SelectPartner;
                if (currentPartner != _lastSelectPartner && _lastSelectPartner >= 0)
                {
                    _lastSelectPartner = currentPartner;
                    _lastAnnouncedName = "";
                    _lastCursorX = genelogy.m_CursorX;
                    _lastCursorY = genelogy.m_CursorY;
                    AnnounceCursorItem(genelogy);
                    return;
                }
                _lastSelectPartner = currentPartner;

                // Check type change (Dojo vs History)
                var currentType = genelogy.m_Type;
                if (currentType != _lastType)
                {
                    DebugLogger.Log($"{LogTag} Type changed: {_lastType} -> {currentType}");
                    _lastType = currentType;
                }

                // Check cursor movement
                int cx = genelogy.m_CursorX;
                int cy = genelogy.m_CursorY;
                if (cx != _lastCursorX || cy != _lastCursorY)
                {
                    _lastCursorX = cx;
                    _lastCursorY = cy;
                    AnnounceCursorItem(genelogy);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"{LogTag} Update error: {ex.Message}");
            }
        }

        private void AnnounceCursorItem(uGenelogyUI genelogy, bool queued = false)
        {
            string name = GetInfoPanelName();
            if (string.IsNullOrEmpty(name))
                name = GetInfoPanelEvoName();

            // Avoid repeating the exact same announcement
            if (name == _lastAnnouncedName && !string.IsNullOrEmpty(name))
                return;
            _lastAnnouncedName = name ?? "";

            if (string.IsNullOrEmpty(name))
            {
                DebugLogger.Log($"{LogTag} Cursor ({_lastCursorX},{_lastCursorY}): empty cell");
                return;
            }

            string announcement = name;

            // Add growth stage
            string stage = GetGrowthStageForName(name);
            if (!string.IsNullOrEmpty(stage))
                announcement += ", " + stage;

            // Add probability
            string probability = GetProbability(genelogy);
            if (!string.IsNullOrEmpty(probability))
                announcement += ", " + probability;

            // Add locked status
            if (IsCurrentLocked(genelogy))
                announcement += ", Locked";

            // Add grid position context
            int maxX = genelogy.m_CursorX_Max;
            int maxY = genelogy.m_CursorY_Max;
            if (maxX > 0 || maxY > 0)
                announcement += $", {_lastCursorX + 1} of {maxX + 1}";

            // Add description after index
            string description = GetInfoPanelText("m_Description");
            if (!string.IsNullOrEmpty(description))
                announcement += ", " + description;

            if (queued)
                ScreenReader.SayQueued(announcement);
            else
                ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Cursor ({_lastCursorX},{_lastCursorY}): {announcement}");
        }

        public void AnnounceStatus()
        {
            if (!IsOpen()) return;

            var genelogy = GetGenelogy();
            if (genelogy == null)
            {
                ScreenReader.Say("Evolution Dojo");
                return;
            }

            string announcement = "Evolution Dojo";

            // Partner
            string partnerName = GetCurrentPartnerName();
            if (!string.IsNullOrEmpty(partnerName))
                announcement += ", " + partnerName;

            // Current digimon name
            string name = GetInfoPanelName();
            if (!string.IsNullOrEmpty(name))
                announcement += ", " + name;

            // Nature and property
            string nature = GetInfoPanelText("m_Nature");
            if (!string.IsNullOrEmpty(nature))
                announcement += ", Nature: " + nature;

            string property = GetInfoPanelText("m_Property");
            if (!string.IsNullOrEmpty(property))
                announcement += ", Attribute: " + property;

            // Description
            string description = GetInfoPanelText("m_Description");
            if (!string.IsNullOrEmpty(description))
                announcement += ", " + description;

            // Growth stage
            string stage = GetGrowthStageForName(name);
            if (!string.IsNullOrEmpty(stage))
                announcement += ", Stage: " + stage;

            // Probability
            string probability = GetProbability(genelogy);
            if (!string.IsNullOrEmpty(probability))
                announcement += ", " + probability;

            // Locked
            if (IsCurrentLocked(genelogy))
                announcement += ", Locked";

            // Evolution conditions
            string conditions = GetConditionsText();
            if (!string.IsNullOrEmpty(conditions))
                announcement += ", Conditions: " + conditions;

            // Evolution target
            string evoName = GetInfoPanelEvoName();
            if (!string.IsNullOrEmpty(evoName) && evoName != name)
                announcement += ", Evolves to: " + evoName;

            // Generation
            string generation = GetGenerationText();
            if (!string.IsNullOrEmpty(generation))
                announcement += ", Generation " + generation;

            // Grid position
            announcement += $", Position {_lastCursorX + 1} of {genelogy.m_CursorX_Max + 1}";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Status: {announcement}");
        }

        // --- Helpers ---

        private uGenelogyUI GetGenelogy()
        {
            try
            {
                return _panel?.m_Genelogy;
            }
            catch { return null; }
        }

        private uGenelogyInformationUI GetInfoPanel()
        {
            try
            {
                return GetGenelogy()?.m_GenelogyInfo;
            }
            catch { return null; }
        }

        private string GetInfoPanelName()
        {
            try
            {
                var info = GetInfoPanel();
                if (info == null) return null;
                var nameText = info.m_DigimonName;
                if (nameText == null) return null;
                string text = nameText.text;
                if (string.IsNullOrEmpty(text)) return null;
                return TextUtilities.StripRichTextTags(text);
            }
            catch { return null; }
        }

        private string GetInfoPanelEvoName()
        {
            try
            {
                var info = GetInfoPanel();
                if (info == null) return null;
                var nameText = info.m_DigimonName_Evo;
                if (nameText == null) return null;
                string text = nameText.text;
                if (string.IsNullOrEmpty(text)) return null;
                return TextUtilities.StripRichTextTags(text);
            }
            catch { return null; }
        }

        private string GetInfoPanelText(string fieldName)
        {
            try
            {
                var info = GetInfoPanel();
                if (info == null) return null;

                UnityEngine.UI.Text textComp = fieldName switch
                {
                    "m_Nature" => info.m_Nature,
                    "m_Property" => info.m_Property,
                    "m_Description" => info.m_Description,
                    "m_Nature_Evo" => info.m_Nature_Evo,
                    "m_Property_Evo" => info.m_Property_Evo,
                    _ => null
                };

                if (textComp == null) return null;
                string text = textComp.text;
                if (string.IsNullOrEmpty(text)) return null;
                return TextUtilities.StripRichTextTags(text);
            }
            catch { return null; }
        }

        private string GetProbability(uGenelogyUI genelogy)
        {
            try
            {
                // The game shows one of these GameObjects depending on probability
                if (genelogy.m_Fix != null && genelogy.m_Fix.activeSelf)
                    return "Guaranteed";
                if (genelogy.m_High != null && genelogy.m_High.activeSelf)
                    return "High chance";
                if (genelogy.m_Mid != null && genelogy.m_Mid.activeSelf)
                    return "Medium chance";
                if (genelogy.m_Low != null && genelogy.m_Low.activeSelf)
                    return "Low chance";

                // Check the Possibility parent - if not active, no probability shown
                if (genelogy.m_Possibility != null && !genelogy.m_Possibility.activeSelf)
                    return null;

                return null;
            }
            catch { return null; }
        }

        private bool IsCurrentLocked(uGenelogyUI genelogy)
        {
            try
            {
                if (genelogy.m_Rock != null && genelogy.m_Rock.activeSelf)
                    return true;
                return false;
            }
            catch { return false; }
        }

        private string GetGrowthStageForName(string digimonName)
        {
            if (string.IsNullOrEmpty(digimonName)) return null;

            try
            {
                var paramManager = AppMainScript.parameterManager;
                if (paramManager == null) return null;
                var digimonData = paramManager.digimonData;
                if (digimonData == null) return null;

                int count = digimonData.GetRecordMax();
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var param = digimonData.GetParams(i);
                        if (param == null) continue;
                        string name = param.GetDefaultName();
                        if (name == digimonName)
                        {
                            int growth = param.m_growth;
                            if (growth >= 0 && growth < GrowthNames.Length)
                                return GrowthNames[growth];
                            return null;
                        }
                    }
                    catch { }
                }
                return null;
            }
            catch { return null; }
        }

        private string GetConditionsText()
        {
            try
            {
                var info = GetInfoPanel();
                if (info == null) return null;

                var titles = info.m_ConditionsTitleText;
                var values = info.m_ConditionsValueText;
                if (titles == null || values == null) return null;

                var parts = new System.Collections.Generic.List<string>();
                int count = System.Math.Min(titles.Length, values.Length);

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        if (titles[i] == null || values[i] == null) continue;
                        string title = titles[i].text;
                        string value = values[i].text;
                        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(value)) continue;

                        title = TextUtilities.StripRichTextTags(title);
                        value = TextUtilities.StripRichTextTags(value);
                        parts.Add($"{title} {value}");
                    }
                    catch { }
                }

                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
            catch { return null; }
        }

        private string GetGenerationText()
        {
            try
            {
                var genelogy = GetGenelogy();
                if (genelogy == null) return null;
                var text = genelogy.m_GenerationText;
                if (text == null) return null;
                string val = text.text;
                if (string.IsNullOrEmpty(val)) return null;
                return TextUtilities.StripRichTextTags(val);
            }
            catch { return null; }
        }

        private string GetCurrentPartnerName()
        {
            try
            {
                if (_panel == null) return null;
                int partner = _panel.m_SelectPartner;

                // Dojo panel uses m_SelectPartner directly (not inverted like uPartnerPanel)
                var partnerCtrl = MainGameManager.GetPartnerCtrl(partner);
                if (partnerCtrl != null)
                {
                    var commonData = partnerCtrl.gameData?.m_commonData;
                    if (commonData != null)
                    {
                        string name = commonData.m_name;
                        if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                            return name;
                    }
                }

                return PartnerUtilities.GetPartnerLabel(partner);
            }
            catch
            {
                return _panel != null ? PartnerUtilities.GetPartnerLabel(_panel.m_SelectPartner) : "Partner";
            }
        }
    }
}
