using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the training result panel.
    /// Announces stat gains after training completes.
    /// </summary>
    public class TrainingResultHandler : IAccessibilityHandler
    {
        public int Priority => 52;

        public void AnnounceStatus()
        {
            if (!IsOpen()) return;
            ScreenReader.Say("Training results");
        }

        private uTrainingPanelResult _panel;
        private bool _wasActive = false;
        private bool _resultAnnounced = false;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uTrainingPanelResult>();
            }

            if (_panel == null)
                return false;

            try
            {
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy;
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
            else if (isActive && !_resultAnnounced)
            {
                // Announce results once panel is active and populated
                AnnounceResults();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _resultAnnounced = false;

            if (_panel == null)
                return;

            DebugLogger.Log("[TrainingResult] Result panel opened");

            // Small delay before announcing to let values populate
            // We'll announce on the first update after detection
        }

        private void OnClose()
        {
            _panel = null;
            _resultAnnounced = false;
            DebugLogger.Log("[TrainingResult] Result panel closed");
        }

        public void AnnounceResults()
        {
            if (_resultAnnounced || _panel == null)
                return;

            try
            {
                var digimonPanels = _panel.m_trainingResultPanelDigimons;
                if (digimonPanels == null || digimonPanels.Length == 0)
                {
                    DebugLogger.Log("[TrainingResult] No digimon panels found");
                    return;
                }

                string announcement = "Training complete. ";

                for (int i = 0; i < digimonPanels.Length; i++)
                {
                    var digiPanel = digimonPanels[i];
                    if (digiPanel == null)
                        continue;

                    string partnerResult = GetPartnerResult(digiPanel, i);
                    if (!string.IsNullOrEmpty(partnerResult))
                    {
                        announcement += partnerResult;
                        if (i < digimonPanels.Length - 1)
                            announcement += " ";
                    }
                }

                if (!string.IsNullOrEmpty(announcement) && announcement != "Training complete. ")
                {
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"[TrainingResult] {announcement}");
                }

                _resultAnnounced = true;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingResult] Error reading results: {ex.Message}");
            }
        }

        private string GetPartnerResult(uTrainingResultPanelDigimon panel, int index)
        {
            try
            {
                // Get partner name from base class field
                string name = AnnouncementBuilder.FallbackItem("Partner", index);
                try
                {
                    var nameText = panel.m_partnerName;
                    if (nameText != null && !string.IsNullOrEmpty(nameText.text))
                        name = nameText.text;
                }
                catch { }

                // Get rise values (stat gains) - this is an int array
                var riseValues = panel.m_riseValues;
                if (riseValues == null || riseValues.Length == 0)
                    return "";

                var gains = new System.Collections.Generic.List<string>();

                // RiseType enum: Hp=0, Mp=1, Forcefulness=2, Robustness=3, Cleverness=4, Rapidity=5, Fatigue=6
                var statNames = PartnerUtilities.StatNamesWithFatigue;

                for (int i = 0; i < riseValues.Length && i < statNames.Length; i++)
                {
                    int value = riseValues[i];
                    if (value != 0)
                    {
                        // Skip fatigue for now (index 6) as it's not a positive gain
                        if (i == 6)
                            continue;

                        gains.Add($"{statNames[i]} plus {value}");
                    }
                }

                if (gains.Count == 0)
                    return "";

                return $"{name}: {string.Join(", ", gains)}.";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingResult] Error reading partner {index}: {ex.Message}");
                return "";
            }
        }
    }
}
