using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle result panel accessibility.
    ///
    /// Battle Result Flow (from PDF documentation):
    ///
    /// Screen 1 (Preview): All three panels shown simultaneously
    ///   - Left Partner: Name + stats (HP, MP, STR, STA, WIS, SPD) with +X gains
    ///   - Right Partner: Name + stats with +X gains
    ///   - Center: Tamer EXP, Bits, Items (rewards)
    ///   - m_isRise = true (showing gain indicators)
    ///
    /// Screen 2 (Applied/Acknowledgement): Same layout but +X removed
    ///   - Only final stat values shown
    ///   - No new information
    ///   - m_isRise = false (gains applied)
    ///
    /// Accessibility behavior:
    ///   - Screen 1: Announce all info (both partners + rewards)
    ///   - Screen 2: "Results applied. Press Continue to return to field."
    /// </summary>
    public class BattleResultHandler
    {
        private uBattlePanelResult _cachedResultPanel;
        private bool _wasEnabled = false;
        private bool _announcedScreen1 = false;
        private bool _announcedScreen2 = false;
        private bool _wasShowingRise = false;

        public void Update()
        {
            uBattlePanelResult resultPanel = null;
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null)
                {
                    resultPanel = battlePanel.m_result;
                }
            }
            catch { }

            if (resultPanel == null)
            {
                ResetState();
                return;
            }

            bool isEnabled = false;
            try
            {
                isEnabled = resultPanel.m_enabled;
            }
            catch { }

            if (!isEnabled)
            {
                if (_wasEnabled)
                {
                    ResetState();
                }
                return;
            }

            _cachedResultPanel = resultPanel;

            // Panel just became enabled
            if (!_wasEnabled)
            {
                _wasEnabled = true;
                _announcedScreen1 = false;
                _announcedScreen2 = false;
                _wasShowingRise = false;
                DebugLogger.Log("[BattleResultHandler] Battle result panel opened");
            }

            // Check if we're showing rise values (Screen 1) or not (Screen 2)
            bool isShowingRise = CheckIsShowingRise();

            // Screen 1: First time seeing rise values - announce everything
            if (isShowingRise && !_announcedScreen1)
            {
                _wasShowingRise = true;
                AnnounceScreen1();
                _announcedScreen1 = true;
                DebugLogger.Log("[BattleResultHandler] Screen 1 announced (Preview with +X gains)");
            }
            // Screen 2: Rise values disappeared - announce confirmation
            else if (!isShowingRise && _wasShowingRise && !_announcedScreen2)
            {
                AnnounceScreen2();
                _announcedScreen2 = true;
                DebugLogger.Log("[BattleResultHandler] Screen 2 announced (Applied/Acknowledgement)");
            }
        }

        private void ResetState()
        {
            _cachedResultPanel = null;
            _wasEnabled = false;
            _announcedScreen1 = false;
            _announcedScreen2 = false;
            _wasShowingRise = false;
        }

        /// <summary>
        /// Check if any digimon panel is showing rise values (+X gains).
        /// Returns true for Screen 1, false for Screen 2.
        /// </summary>
        private bool CheckIsShowingRise()
        {
            if (_cachedResultPanel == null)
                return false;

            try
            {
                var digimonPanels = _cachedResultPanel.m_resultPanelDigimons;
                if (digimonPanels == null || digimonPanels.Length == 0)
                    return false;

                // Check if any panel has m_isRise = true
                for (int i = 0; i < digimonPanels.Length && i < 2; i++)
                {
                    var panel = digimonPanels[i];
                    if (panel == null)
                        continue;

                    try
                    {
                        if (panel.m_isRise)
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Announce Screen 1: All battle results (both partners + rewards).
        /// </summary>
        private void AnnounceScreen1()
        {
            if (_cachedResultPanel == null)
                return;

            var parts = new System.Collections.Generic.List<string>();

            // Announce both partner stats
            try
            {
                var digimonPanels = _cachedResultPanel.m_resultPanelDigimons;
                if (digimonPanels != null)
                {
                    for (int i = 0; i < digimonPanels.Length && i < 2; i++)
                    {
                        var panel = digimonPanels[i];
                        if (panel == null)
                            continue;

                        string partnerStats = GetPartnerStatsText(panel, i);
                        if (!string.IsNullOrEmpty(partnerStats))
                        {
                            parts.Add(partnerStats);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleResultHandler] Error reading partner stats: {ex.Message}");
            }

            // Announce rewards from center panel
            try
            {
                var getPanel = _cachedResultPanel.m_getPanel;
                if (getPanel != null)
                {
                    string rewards = GetRewardsText(getPanel);
                    if (!string.IsNullOrEmpty(rewards))
                    {
                        parts.Add(rewards);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleResultHandler] Error reading rewards: {ex.Message}");
            }

            // Announce everything
            if (parts.Count > 0)
            {
                string fullAnnouncement = string.Join(". ", parts);
                ScreenReader.Say(fullAnnouncement);
                DebugLogger.Log($"[BattleResultHandler] Screen 1: {fullAnnouncement}");
            }
        }

        /// <summary>
        /// Get stats text for a single partner.
        /// </summary>
        private string GetPartnerStatsText(uResultPanelDigimonBase panel, int index)
        {
            try
            {
                // Get partner name
                string partnerName = "";
                try
                {
                    partnerName = panel.m_partnerName?.text ?? "";
                }
                catch { }

                if (string.IsNullOrWhiteSpace(partnerName))
                {
                    partnerName = $"Partner {index + 1}";
                }

                // Get rise values
                var statParts = new System.Collections.Generic.List<string>();
                var riseValues = panel.m_riseValues;

                if (riseValues != null && riseValues.Length > 0)
                {
                    // Order: HP, MP, STR, STA, WIS, SPD (indices 0-5)
                    string[] statNames = { "HP", "MP", "STR", "STA", "WIS", "SPD" };

                    for (int j = 0; j < riseValues.Length && j < statNames.Length; j++)
                    {
                        int value = riseValues[j];
                        if (value > 0)
                        {
                            statParts.Add($"{statNames[j]} +{value}");
                        }
                    }
                }

                if (statParts.Count > 0)
                {
                    return $"{partnerName}: {string.Join(", ", statParts)}";
                }
                else
                {
                    // Try reading from text fields as fallback
                    return GetPartnerStatsFromText(panel, partnerName);
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Fallback: read stats from text fields.
        /// </summary>
        private string GetPartnerStatsFromText(uResultPanelDigimonBase panel, string partnerName)
        {
            try
            {
                var statParts = new System.Collections.Generic.List<string>();
                var riseTexts = panel.m_riseTexts;

                if (riseTexts != null)
                {
                    string[] statNames = { "HP", "MP", "STR", "STA", "WIS", "SPD" };

                    for (int j = 0; j < riseTexts.Length && j < statNames.Length; j++)
                    {
                        var textField = riseTexts[j];
                        if (textField != null)
                        {
                            string value = textField.text;
                            if (!string.IsNullOrWhiteSpace(value) && value != "0" && value != "+0")
                            {
                                if (!value.StartsWith("+") && !value.StartsWith("-"))
                                {
                                    value = "+" + value;
                                }
                                statParts.Add($"{statNames[j]} {value}");
                            }
                        }
                    }
                }

                if (statParts.Count > 0)
                {
                    return $"{partnerName}: {string.Join(", ", statParts)}";
                }
            }
            catch { }

            return "";
        }

        /// <summary>
        /// Get rewards text from center panel.
        /// </summary>
        private string GetRewardsText(uResultPanelGet getPanel)
        {
            try
            {
                var rewardParts = new System.Collections.Generic.List<string>();

                // Tamer EXP (TP)
                string tp = getPanel.m_tpText?.text;
                if (!string.IsNullOrWhiteSpace(tp) && tp != "0")
                {
                    rewardParts.Add($"EXP {tp}");
                }

                // Bits
                string bit = getPanel.m_bitText?.text;
                if (!string.IsNullOrWhiteSpace(bit) && bit != "0")
                {
                    rewardParts.Add($"{bit} Bits");
                }

                // Items
                var itemTexts = getPanel.m_itemText;
                var itemNums = getPanel.m_itemNumText;
                if (itemTexts != null)
                {
                    for (int i = 0; i < itemTexts.Length && i < 5; i++)
                    {
                        string itemName = itemTexts[i]?.text;
                        if (!string.IsNullOrWhiteSpace(itemName))
                        {
                            string itemNum = itemNums != null && i < itemNums.Length ? itemNums[i]?.text : "";
                            if (!string.IsNullOrWhiteSpace(itemNum) && itemNum != "1")
                            {
                                rewardParts.Add($"{itemName} x{itemNum}");
                            }
                            else
                            {
                                rewardParts.Add(itemName);
                            }
                        }
                    }
                }

                if (rewardParts.Count > 0)
                {
                    return "Rewards: " + string.Join(", ", rewardParts);
                }
            }
            catch { }

            return "";
        }

        /// <summary>
        /// Announce Screen 2: Confirmation that results are applied.
        /// </summary>
        private void AnnounceScreen2()
        {
            ScreenReader.Say("Results applied. Press Continue to return to field.");
        }

        public bool IsActive()
        {
            return _wasEnabled && _cachedResultPanel != null;
        }

        public void AnnounceStatus()
        {
            if (_cachedResultPanel == null)
            {
                ScreenReader.Say("Battle result");
                return;
            }

            bool isShowingRise = CheckIsShowingRise();
            if (isShowingRise)
            {
                ScreenReader.Say("Battle results. Press Continue to apply.");
            }
            else
            {
                ScreenReader.Say("Results applied. Press Continue to return to field.");
            }
        }
    }
}
