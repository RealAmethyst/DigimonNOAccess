using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles battle result panel (victory/defeat) accessibility.
    /// Announces when battle ends and any rewards or skills learned.
    /// </summary>
    public class BattleResultHandler
    {
        private uBattlePanelResult _cachedResultPanel;
        private bool _wasEnabled = false;
        private string _lastCaption = "";

        public void Update()
        {
            // Find the result panel
            uBattlePanelResult resultPanel = null;
            try
            {
                resultPanel = Object.FindObjectOfType<uBattlePanelResult>();
            }
            catch { }

            if (resultPanel == null)
            {
                ResetState();
                return;
            }

            // Check if panel is enabled
            bool isEnabled = false;
            try
            {
                isEnabled = resultPanel.m_enabled || resultPanel.IsEnabled;
            }
            catch
            {
                try
                {
                    isEnabled = resultPanel.gameObject.activeInHierarchy;
                }
                catch { }
            }

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
                DebugLogger.Log("[BattleResultHandler] Battle result panel opened");
                AnnounceResult();
                return;
            }

            // Check for caption changes (e.g., skill learned notifications)
            string currentCaption = GetCaptionText();
            if (!string.IsNullOrWhiteSpace(currentCaption) && currentCaption != _lastCaption)
            {
                _lastCaption = currentCaption;
                ScreenReader.Say(currentCaption);
            }
        }

        private void ResetState()
        {
            _cachedResultPanel = null;
            _wasEnabled = false;
            _lastCaption = "";
        }

        private void AnnounceResult()
        {
            if (_cachedResultPanel == null)
                return;

            // Get the caption text which usually says "Victory!" or similar
            string caption = GetCaptionText();
            DebugLogger.Log($"[BattleResultHandler] Caption text: '{caption}'");

            // Also try to find any visible text in the panel
            string panelText = GetAnyVisibleText();
            DebugLogger.Log($"[BattleResultHandler] Panel text: '{panelText}'");

            if (!string.IsNullOrWhiteSpace(caption))
            {
                _lastCaption = caption;
                ScreenReader.Say(caption);
            }
            else if (!string.IsNullOrWhiteSpace(panelText))
            {
                _lastCaption = panelText;
                ScreenReader.Say(panelText);
            }
            else
            {
                // Fallback - announce victory since we won if this panel shows
                ScreenReader.Say("Victory!");
            }

            // Check for items or skills
            CheckRewards();
        }

        private string GetCaptionText()
        {
            try
            {
                return _cachedResultPanel?.m_captionLangText?.text ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetAnyVisibleText()
        {
            try
            {
                // Try to find Text components in the panel hierarchy
                var texts = _cachedResultPanel.GetComponentsInChildren<UnityEngine.UI.Text>();
                if (texts != null)
                {
                    foreach (var text in texts)
                    {
                        if (text != null && text.gameObject.activeInHierarchy)
                        {
                            string content = text.text;
                            if (!string.IsNullOrWhiteSpace(content) &&
                                !content.Contains("メッセージ") && // Skip placeholder text
                                content.Length > 2)
                            {
                                DebugLogger.Log($"[BattleResultHandler] Found text: '{content}'");
                                return content;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleResultHandler] Error getting text: {ex.Message}");
            }
            return "";
        }

        private void CheckRewards()
        {
            if (_cachedResultPanel == null)
                return;

            try
            {
                // Check for items panel and read rewards
                var getPanel = _cachedResultPanel.m_getPanel;
                if (getPanel != null && getPanel.gameObject.activeInHierarchy)
                {
                    DebugLogger.Log("[BattleResultHandler] Item reward panel detected");

                    // Read TP and Bit rewards
                    string tp = getPanel.m_tpText?.text;
                    string bit = getPanel.m_bitText?.text;
                    DebugLogger.Log($"[BattleResultHandler] TP: '{tp}', Bit: '{bit}'");

                    // Read item names
                    var itemTexts = getPanel.m_itemText;
                    var itemNums = getPanel.m_itemNumText;
                    if (itemTexts != null)
                    {
                        for (int i = 0; i < itemTexts.Length && i < 5; i++)
                        {
                            string itemName = itemTexts[i]?.text;
                            string itemNum = itemNums != null && i < itemNums.Length ? itemNums[i]?.text : "";
                            if (!string.IsNullOrWhiteSpace(itemName))
                            {
                                DebugLogger.Log($"[BattleResultHandler] Item {i}: '{itemName}' x{itemNum}");
                            }
                        }
                    }

                    // Read partner name
                    string partnerName = getPanel.m_playerName?.text;
                    DebugLogger.Log($"[BattleResultHandler] Partner name: '{partnerName}'");
                }

                // Check for skills panel
                var skillPanel = _cachedResultPanel.m_skillPanel;
                if (skillPanel != null && skillPanel.gameObject.activeInHierarchy)
                {
                    DebugLogger.Log("[BattleResultHandler] Skill panel detected");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleResultHandler] Error checking rewards: {ex.Message}");
            }
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

            string caption = GetCaptionText();
            if (!string.IsNullOrWhiteSpace(caption))
            {
                ScreenReader.Say($"Battle result: {caption}");
            }
            else
            {
                ScreenReader.Say("Battle result screen");
            }
        }
    }
}
