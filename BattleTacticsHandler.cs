using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles the Battle Tactics menu (Square button menu) accessibility.
    /// This menu appears when pressing Square during battle and contains
    /// options like MP usage policy and escape.
    /// </summary>
    public class BattleTacticsHandler
    {
        private uBattlePanelTactics _cachedTacticsPanel;
        private int _lastCmdNo = -1;
        private uBattlePanelTactics.InternalMode _lastMode = uBattlePanelTactics.InternalMode.None;
        private bool _wasActive = false;

        public void Update()
        {
            // Check if battle is active first
            var battlePanel = uBattlePanel.m_instance;
            if (battlePanel == null || !battlePanel.m_enabled)
            {
                ResetState();
                return;
            }

            // Get the tactics panel from the battle panel
            uBattlePanelTactics tacticsPanel = null;
            try
            {
                tacticsPanel = battlePanel.m_tactics;
            }
            catch { }

            if (tacticsPanel == null || !tacticsPanel.gameObject.activeInHierarchy)
            {
                if (_wasActive)
                {
                    ResetState();
                }
                return;
            }

            // Check if in an active mode
            var mode = tacticsPanel.m_mode;
            if (mode == uBattlePanelTactics.InternalMode.None)
            {
                if (_wasActive)
                {
                    ResetState();
                }
                return;
            }

            _cachedTacticsPanel = tacticsPanel;

            // Panel just opened
            if (!_wasActive)
            {
                _wasActive = true;
                _lastCmdNo = tacticsPanel.m_currentCmdNo;
                _lastMode = mode;
                DebugLogger.Log("[BattleTacticsHandler] Tactics panel opened");
                AnnounceCurrentSelection(true);
                return;
            }

            // Check for mode change (e.g., Default to Target selection)
            if (mode != _lastMode)
            {
                _lastMode = mode;
                if (mode == uBattlePanelTactics.InternalMode.Target)
                {
                    ScreenReader.Say("Select target");
                }
            }

            // Check for cursor movement
            int currentCmdNo = tacticsPanel.m_currentCmdNo;
            if (currentCmdNo != _lastCmdNo)
            {
                _lastCmdNo = currentCmdNo;
                AnnounceCurrentSelection(false);
            }
        }

        private void ResetState()
        {
            _cachedTacticsPanel = null;
            _lastCmdNo = -1;
            _lastMode = uBattlePanelTactics.InternalMode.None;
            _wasActive = false;
        }

        private void AnnounceCurrentSelection(bool includeTitle)
        {
            if (_cachedTacticsPanel == null)
                return;

            string announcement = "";

            // If first time, include the panel title
            if (includeTitle)
            {
                string title = GetTitleText();
                DebugLogger.Log($"[BattleTacticsHandler] Title text: '{title}'");
                if (!string.IsNullOrWhiteSpace(title))
                {
                    announcement = title + ": ";
                }
                else
                {
                    announcement = "Tactics: ";
                }
            }

            // Get the current command description
            string cmdDescription = GetCurrentCommandDescription();
            DebugLogger.Log($"[BattleTacticsHandler] Command description: '{cmdDescription}'");
            announcement += cmdDescription;

            DebugLogger.Log($"[BattleTacticsHandler] Announcing: '{announcement}'");
            ScreenReader.Say(announcement);
        }

        private string GetTitleText()
        {
            try
            {
                return _cachedTacticsPanel?.m_titleLangText?.text ?? "";
            }
            catch
            {
                return "";
            }
        }

        private string GetCurrentCommandDescription()
        {
            if (_cachedTacticsPanel == null)
                return "Unknown";

            try
            {
                int cmdNo = _cachedTacticsPanel.m_currentCmdNo;
                DebugLogger.Log($"[BattleTacticsHandler] Current command index: {cmdNo}");

                // Try to get from command frames first
                var commandFrames = _cachedTacticsPanel.m_commandFrames;
                DebugLogger.Log($"[BattleTacticsHandler] Command frames count: {commandFrames?.Length ?? 0}");

                if (commandFrames != null && cmdNo >= 0 && cmdNo < commandFrames.Length)
                {
                    var frame = commandFrames[cmdNo];
                    if (frame != null)
                    {
                        // Try headline text (usually has the tab name)
                        string headline = frame.m_headlineLangText?.text;
                        DebugLogger.Log($"[BattleTacticsHandler] Frame headline: '{headline}'");
                        if (!string.IsNullOrWhiteSpace(headline) && !headline.Contains("Open commands"))
                        {
                            return headline;
                        }

                        // Try description text
                        string desc = frame.m_description?.text;
                        DebugLogger.Log($"[BattleTacticsHandler] Frame description: '{desc}'");
                        if (!string.IsNullOrWhiteSpace(desc) && !desc.Contains("Open commands"))
                        {
                            return desc;
                        }
                    }
                }

                // Use known command names based on index
                // The battle tactics menu has these tabs in order
                string[] knownCommands = { "Escape", "MP Usage", "Target" };
                if (cmdNo >= 0 && cmdNo < knownCommands.Length)
                {
                    return knownCommands[cmdNo];
                }

                return AnnouncementBuilder.FallbackItem("Tab", cmdNo);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[BattleTacticsHandler] Error getting command: {ex.Message}");
                return "Unknown";
            }
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

            AnnounceCurrentSelection(true);
        }
    }
}
