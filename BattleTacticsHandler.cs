using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles the Battle Tactics menu (Square button menu) accessibility.
    /// This menu appears when pressing Square during battle and contains:
    /// - Tab 0: Escape
    /// - Tab 1: MP Usage
    /// - Tab 2: Target
    ///
    /// Tracks both tab changes (m_currentCmdNo) and within-tab option
    /// changes (m_selectCursorX on each command frame).
    /// </summary>
    public class BattleTacticsHandler : IAccessibilityHandler
    {
        public int Priority => 86;
        public bool IsOpen() => IsActive();

        private uBattlePanelTactics _cachedTacticsPanel;
        private int _lastCmdNo = -1;
        private uBattlePanelTactics.InternalMode _lastMode = uBattlePanelTactics.InternalMode.None;
        private bool _wasActive = false;

        // Track within-tab cursor position and description text
        private int _lastCursorX = -1;
        private int _lastCursorY = -1;
        private string _lastDescription = "";

        // Track target changes within Target mode
        private string _lastTargetName = "";

        private static readonly string[] TabNames = { "Escape", "MP Usage", "Target" };

        public void Update()
        {
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                ResetState();
                return;
            }

            uBattlePanelTactics tacticsPanel = null;
            try
            {
                tacticsPanel = battlePanel.m_tactics;
            }
            catch { }

            if (tacticsPanel == null || !tacticsPanel.gameObject.activeInHierarchy)
            {
                if (_wasActive)
                    ResetState();
                return;
            }

            var mode = tacticsPanel.m_mode;
            if (mode == uBattlePanelTactics.InternalMode.None)
            {
                if (_wasActive)
                    ResetState();
                return;
            }

            _cachedTacticsPanel = tacticsPanel;

            // Panel just opened
            if (!_wasActive)
            {
                _wasActive = true;
                _lastCmdNo = tacticsPanel.m_currentCmdNo;
                _lastMode = mode;
                SnapshotCursorState(tacticsPanel.m_currentCmdNo);
                DebugLogger.Log("[BattleTacticsHandler] Tactics panel opened");
                AnnounceTab(tacticsPanel.m_currentCmdNo, true);
                return;
            }

            // Mode changed (e.g., Default -> Target selection)
            if (mode != _lastMode)
            {
                _lastMode = mode;
                if (mode == uBattlePanelTactics.InternalMode.Target)
                {
                    AnnounceTargetSelection();
                }
            }

            // Tab changed (cursor moved between tabs)
            int currentCmdNo = tacticsPanel.m_currentCmdNo;
            if (currentCmdNo != _lastCmdNo)
            {
                _lastCmdNo = currentCmdNo;
                SnapshotCursorState(currentCmdNo);
                AnnounceTab(currentCmdNo, false);
                return;
            }

            // In Target mode, check if the selected target changed
            if (mode == uBattlePanelTactics.InternalMode.Target)
            {
                CheckTargetChanged();
                return;
            }

            // Check within-tab option changes (cursor or description text)
            CheckWithinTabChange(currentCmdNo);
        }

        /// <summary>
        /// Snapshot the current cursor state for the active tab.
        /// </summary>
        private void SnapshotCursorState(int tabIndex)
        {
            _lastCursorX = -1;
            _lastCursorY = -1;
            _lastDescription = "";

            try
            {
                var frames = _cachedTacticsPanel?.m_commandFrames;
                if (frames != null && tabIndex >= 0 && tabIndex < frames.Length)
                {
                    var frame = frames[tabIndex];
                    if (frame != null)
                    {
                        _lastCursorX = frame.m_selectCursorX;
                        _lastCursorY = frame.m_selectCursorY;
                        _lastDescription = frame.m_description?.text ?? "";
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Check if the cursor or description changed within the current tab.
        /// </summary>
        private void CheckWithinTabChange(int tabIndex)
        {
            try
            {
                var frames = _cachedTacticsPanel?.m_commandFrames;
                if (frames == null || tabIndex < 0 || tabIndex >= frames.Length)
                    return;

                var frame = frames[tabIndex];
                if (frame == null) return;

                int cursorX = frame.m_selectCursorX;
                int cursorY = frame.m_selectCursorY;
                string desc = frame.m_description?.text ?? "";

                bool cursorChanged = cursorX != _lastCursorX || cursorY != _lastCursorY;
                bool descChanged = desc != _lastDescription && !string.IsNullOrWhiteSpace(desc);

                if (cursorChanged || descChanged)
                {
                    _lastCursorX = cursorX;
                    _lastCursorY = cursorY;
                    _lastDescription = desc;

                    string cleanDesc = TextUtilities.StripRichTextTags(desc);
                    if (!string.IsNullOrWhiteSpace(cleanDesc))
                    {
                        DebugLogger.Log($"[BattleTacticsHandler] Tab {tabIndex} option changed: cursor({cursorX},{cursorY}), desc: {cleanDesc}");
                        ScreenReader.Say(cleanDesc);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Announce the current tab with its content.
        /// </summary>
        private void AnnounceTab(int tabIndex, bool includeTitle)
        {
            if (_cachedTacticsPanel == null) return;

            string announcement = "";
            if (includeTitle)
                announcement = "Tactics: ";

            string tabName = GetTabName(tabIndex);
            string tabContent = GetTabContent(tabIndex);

            announcement += tabName;
            if (!string.IsNullOrEmpty(tabContent))
                announcement += ", " + tabContent;

            DebugLogger.Log($"[BattleTacticsHandler] Tab {tabIndex}: {announcement}");
            ScreenReader.Say(announcement);
        }

        /// <summary>
        /// Get the name of a tab by index.
        /// Uses the parent panel's title text, then falls back to known names.
        /// </summary>
        private string GetTabName(int index)
        {
            string fallback = index >= 0 && index < TabNames.Length
                ? TabNames[index]
                : AnnouncementBuilder.FallbackItem("Tab", index);

            try
            {
                if (_cachedTacticsPanel == null)
                {
                    DebugLogger.Log("[BattleTacticsHandler] Tab title: cached tactics panel was null");
                    return fallback;
                }

                var titleText = _cachedTacticsPanel.m_titleLangText;
                if (titleText == null)
                {
                    DebugLogger.Log("[BattleTacticsHandler] Tab title: m_titleLangText was null");
                    return fallback;
                }

                string title = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(titleText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(title) || TextUtilities.IsPlaceholderText(title))
                {
                    DebugLogger.Log($"[BattleTacticsHandler] Tab title: m_titleLangText.text unusable: {TextUtilities.DescribeUnusable(title)}");
                    return fallback;
                }

                return title;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleTacticsHandler] Tab title read failed: {ex.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// Get contextual content for a tab from its description text.
        /// </summary>
        private string GetTabContent(int index)
        {
            try
            {
                var frames = _cachedTacticsPanel?.m_commandFrames;
                if (frames != null && index >= 0 && index < frames.Length)
                {
                    var frame = frames[index];
                    if (frame != null)
                    {
                        string desc = frame.m_description?.text;
                        if (!string.IsNullOrWhiteSpace(desc))
                            return TextUtilities.StripRichTextTags(desc);
                    }
                }
            }
            catch { }

            // Fallback for Target tab
            if (index == 2)
                return GetTargetTabSummary();

            return "";
        }

        /// <summary>
        /// Get a summary of the current target for the Target tab.
        /// </summary>
        private string GetTargetTabSummary()
        {
            try
            {
                var target = _cachedTacticsPanel?.GetTarget();
                if (target != null)
                {
                    return "Current target: " + GetEnemyDescription(target);
                }
            }
            catch { }

            return GetCaptionText("Select enemy target");
        }

        /// <summary>
        /// Announce when entering Target selection mode.
        /// </summary>
        private void AnnounceTargetSelection()
        {
            _lastTargetName = "";
            string announcement = GetCaptionText("Select target");

            try
            {
                var target = _cachedTacticsPanel?.GetTarget();
                if (target != null)
                {
                    string desc = GetEnemyDescription(target);
                    announcement += ": " + desc;
                    _lastTargetName = desc;
                }
            }
            catch { }

            ScreenReader.Say(announcement);
        }

        private string GetCaptionText(string fallback)
        {
            try
            {
                if (_cachedTacticsPanel == null)
                {
                    DebugLogger.Log("[BattleTacticsHandler] Target caption: cached tactics panel was null");
                    return fallback;
                }

                var captionText = _cachedTacticsPanel.m_captionLangText;
                if (captionText == null)
                {
                    DebugLogger.Log("[BattleTacticsHandler] Target caption: m_captionLangText was null");
                    return fallback;
                }

                string caption = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(captionText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(caption) || TextUtilities.IsPlaceholderText(caption))
                {
                    DebugLogger.Log($"[BattleTacticsHandler] Target caption: m_captionLangText.text unusable: {TextUtilities.DescribeUnusable(caption)}");
                    return fallback;
                }

                return caption;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleTacticsHandler] Target caption read failed: {ex.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// Check if the selected target changed while in Target mode.
        /// </summary>
        private void CheckTargetChanged()
        {
            try
            {
                var target = _cachedTacticsPanel?.GetTarget();
                if (target == null) return;

                string desc = GetEnemyDescription(target);
                if (desc != _lastTargetName)
                {
                    _lastTargetName = desc;
                    ScreenReader.Say(desc);
                }
            }
            catch { }
        }

        /// <summary>
        /// Build a description string for an enemy DigimonCtrl.
        /// </summary>
        private string GetEnemyDescription(DigimonCtrl enemy)
        {
            string name = "Enemy";
            string level = "";
            int hpPercent = 0;

            // Get name (use commonData.m_name, then ParameterDigimonData fallback)
            try
            {
                var gameData = enemy.gameData;
                if (gameData != null)
                {
                    string commonName = gameData.m_commonData?.m_name;
                    if (!string.IsNullOrEmpty(commonName) && !commonName.Contains("ランゲージ"))
                    {
                        name = TextUtilities.StripRichTextTags(commonName);
                    }
                    else
                    {
                        int digimonId = gameData.m_no;
                        if (digimonId > 0)
                        {
                            var paramData = ParameterDigimonData.GetParam((uint)digimonId);
                            string paramName = paramData?.GetDefaultName();
                            if (!string.IsNullOrEmpty(paramName) && !paramName.Contains("ランゲージ"))
                                name = TextUtilities.StripRichTextTags(paramName);
                        }
                    }
                }
            }
            catch { }

            // Get level and HP from the enemy's HP bar
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null)
                {
                    var bars = battlePanel.m_enemy_hps;
                    if (bars != null)
                    {
                        for (int i = 0; i < bars.Length; i++)
                        {
                            var bar = bars[i];
                            if (bar == null || bar.m_unit == null) continue;

                            if (bar.m_unit.Pointer == enemy.Pointer)
                            {
                                level = bar.m_levelText?.text ?? "";
                                hpPercent = (int)(bar.m_now_hp_rate * 100);
                                break;
                            }
                        }
                    }
                }
            }
            catch { }

            string targeting = BattleMonitorHandler.GetEnemyTargetPartner(enemy);

            string result = name;
            if (!string.IsNullOrEmpty(level))
                result += $" level {level}";
            result += $", {hpPercent}% HP";
            if (!string.IsNullOrEmpty(targeting))
                result += $", targeting {targeting}";

            return result;
        }

        private void ResetState()
        {
            _cachedTacticsPanel = null;
            _lastCmdNo = -1;
            _lastMode = uBattlePanelTactics.InternalMode.None;
            _wasActive = false;
            _lastTargetName = "";
            _lastCursorX = -1;
            _lastCursorY = -1;
            _lastDescription = "";
        }

        public bool IsActive()
        {
            return _wasActive && _cachedTacticsPanel != null;
        }

        public void AnnounceStatus()
        {
            if (_cachedTacticsPanel == null)
            {
                ScreenReader.Say("Tactics menu");
                return;
            }

            AnnounceTab(_cachedTacticsPanel.m_currentCmdNo, true);
        }
    }
}
