using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the training panel (gym training selection).
    /// Tracks two independent cursors (one per partner) and announces training details.
    /// </summary>
    public class TrainingPanelHandler
    {
        private uTrainingPanelCommand _panel;
        private bool _wasActive = false;
        private int _lastCursorRight = -1; // Partner 1 (Right) cursor position
        private int _lastCursorLeft = -1;  // Partner 2 (Left) cursor position
        private uTrainingPanelCommand.State _lastState = uTrainingPanelCommand.State.None;
        private bool _lastBonusTabShowing = false; // Track info tab: false=Stats, true=Bonus

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uTrainingPanelCommand>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_state;
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy &&
                       state != uTrainingPanelCommand.State.None &&
                       state != uTrainingPanelCommand.State.Close;
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
            else if (isActive)
            {
                CheckStateChange();
                CheckCursorChanges();
                CheckTabChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursorRight = -1;
            _lastCursorLeft = -1;
            _lastState = uTrainingPanelCommand.State.None;
            _hasAnnouncedOpen = false;
            _lastBonusTabShowing = IsBonusTabShowing(); // Initialize to current state

            if (_panel == null)
                return;

            var state = _panel.m_state;
            DebugLogger.Log($"[TrainingPanel] Panel opened, state={state}");

            // If panel opens in Main state, announce immediately
            // Otherwise wait for state to become Main (handled in CheckStateChange)
            if (state == uTrainingPanelCommand.State.Main)
            {
                AnnounceFullStatus();
                _hasAnnouncedOpen = true;
            }

            _lastState = state;
        }

        private bool _hasAnnouncedOpen = false;

        private void AnnounceFullStatus()
        {
            if (_panel == null)
                return;

            // Get partner names
            string partner1Name = GetPartnerName(0);
            string partner2Name = GetPartnerName(1);

            // Get both cursor positions
            int cursorRight = GetCursorPosition(0); // Partner 1
            int cursorLeft = GetCursorPosition(1);  // Partner 2
            int totalTrainings = GetTrainingCount();

            string training1 = GetTrainingText(cursorRight);
            string training2 = GetTrainingText(cursorLeft);

            int index1 = cursorRight + 1;
            int index2 = cursorLeft + 1;

            string announcement = $"Training. {partner1Name} on {training1}, {index1} of {totalTrainings}. {partner2Name} on {training2}, {index2} of {totalTrainings}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[TrainingPanel] Announced: cursor1={cursorRight}, cursor2={cursorLeft}");
            _lastCursorRight = cursorRight;
            _lastCursorLeft = cursorLeft;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursorRight = -1;
            _lastCursorLeft = -1;
            _lastState = uTrainingPanelCommand.State.None;
            _lastBonusTabShowing = false;
            DebugLogger.Log("[TrainingPanel] Panel closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var state = _panel.m_state;
            if (state != _lastState)
            {
                DebugLogger.Log($"[TrainingPanel] State changed to {state}");

                // When entering Main state for first time, announce full status
                if (state == uTrainingPanelCommand.State.Main && !_hasAnnouncedOpen)
                {
                    AnnounceFullStatus();
                    _hasAnnouncedOpen = true;
                }
                // Don't announce Dialog state - YesNo handler handles that

                _lastState = state;
            }
        }

        private void CheckCursorChanges()
        {
            if (_panel == null)
                return;

            // Only check cursor changes when in Main state (interactive)
            var state = _panel.m_state;
            if (state != uTrainingPanelCommand.State.Main)
                return;

            int cursorRight = GetCursorPosition(0); // Partner 1
            int cursorLeft = GetCursorPosition(1);  // Partner 2
            int totalTrainings = GetTrainingCount();

            bool changed = false;
            string announcement = "";

            // Check if Partner 1 cursor moved
            if (cursorRight != _lastCursorRight && cursorRight >= 0)
            {
                string partnerName = GetPartnerName(0);
                string trainingText = GetTrainingText(cursorRight);
                int displayIndex = cursorRight + 1; // 1-based for user
                announcement = $"{partnerName}: {trainingText}. {displayIndex} of {totalTrainings}";
                changed = true;
                DebugLogger.Log($"[TrainingPanel] Partner 1 cursor: {trainingText} ({displayIndex}/{totalTrainings})");
                _lastCursorRight = cursorRight;
            }

            // Check if Partner 2 cursor moved
            if (cursorLeft != _lastCursorLeft && cursorLeft >= 0)
            {
                string partnerName = GetPartnerName(1);
                string trainingText = GetTrainingText(cursorLeft);
                int displayIndex = cursorLeft + 1; // 1-based for user

                if (changed)
                {
                    // Both moved at once (unlikely but handle it)
                    announcement += $". {partnerName}: {trainingText}. {displayIndex} of {totalTrainings}";
                }
                else
                {
                    announcement = $"{partnerName}: {trainingText}. {displayIndex} of {totalTrainings}";
                    changed = true;
                }
                DebugLogger.Log($"[TrainingPanel] Partner 2 cursor: {trainingText} ({displayIndex}/{totalTrainings})");
                _lastCursorLeft = cursorLeft;
            }

            if (changed)
            {
                ScreenReader.Say(announcement);
            }
        }

        private void CheckTabChange()
        {
            if (_panel == null)
                return;

            // Only check tab changes when in Main state
            var state = _panel.m_state;
            if (state != uTrainingPanelCommand.State.Main)
                return;

            bool bonusShowing = IsBonusTabShowing();
            if (bonusShowing != _lastBonusTabShowing)
            {
                string tabName = bonusShowing ? "Bonus" : "Stats";
                ScreenReader.Say(tabName);
                DebugLogger.Log($"[TrainingPanel] Tab changed to: {tabName}");
                _lastBonusTabShowing = bonusShowing;
            }
        }

        private bool IsBonusTabShowing()
        {
            try
            {
                var infos = _panel?.m_trainingInformations;
                if (infos != null && infos.Length > 0)
                {
                    var info = infos[0]; // Check first partner's info panel
                    if (info != null)
                    {
                        var bonusInfo = info.m_bonusInformation;
                        if (bonusInfo != null)
                        {
                            return bonusInfo.isShow;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingPanel] Error checking tab: {ex.Message}");
            }
            return false;
        }

        private int GetCursorPosition(int cursorIndex)
        {
            try
            {
                var cursors = _panel?.m_trainingCursors;
                if (cursors != null && cursorIndex < cursors.Length)
                {
                    var cursor = cursors[cursorIndex];
                    if (cursor != null)
                    {
                        return (int)cursor.index;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingPanel] Error getting cursor {cursorIndex}: {ex.Message}");
            }
            return 0;
        }

        private string GetPartnerName(int partnerIndex)
        {
            try
            {
                // Get from TrainingInformation panel's name text field
                var infos = _panel?.m_trainingInformations;
                if (infos != null && partnerIndex < infos.Length)
                {
                    var info = infos[partnerIndex];
                    if (info != null)
                    {
                        // TrainingInformation has _refTextValue_Name which shows the partner name
                        var nameText = info._refTextValue_Name;
                        if (nameText != null)
                        {
                            string name = nameText.text;
                            if (!string.IsNullOrEmpty(name))
                                return name;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingPanel] Error getting partner name: {ex.Message}");
            }

            return partnerIndex == 0 ? "Partner 1" : "Partner 2";
        }

        private string GetTrainingText(int index)
        {
            try
            {
                var contents = _panel?.m_trainingContents;
                if (contents != null && index >= 0 && index < contents.Length)
                {
                    var content = contents[index];
                    if (content != null)
                    {
                        int level = content.level;
                        var trainingIndex = content.index;
                        int bonusCount = content.bonusCount;

                        string trainingName = GetTrainingTypeName(trainingIndex);

                        // Build announcement with level and bonus info
                        string result = trainingName;

                        if (level > 0)
                        {
                            result += $", Level {level}";
                        }

                        if (bonusCount > 0)
                        {
                            result += $", {bonusCount} bonus{(bonusCount > 1 ? "es" : "")}";

                            // Try to read bonus types
                            string bonusTypes = GetBonusTypes(content);
                            if (!string.IsNullOrEmpty(bonusTypes))
                            {
                                result += $": {bonusTypes}";
                            }
                        }

                        return result;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingPanel] Error reading training text: {ex.Message}");
            }

            return $"Training {index + 1}";
        }

        private string GetBonusTypes(TrainingContent content)
        {
            try
            {
                var bonuses = content?.bounses;
                if (bonuses == null || bonuses.Length == 0)
                    return "";

                var bonusNames = new System.Collections.Generic.List<string>();
                foreach (var bonus in bonuses)
                {
                    string name = GetBonusTypeName(bonus);
                    if (!string.IsNullOrEmpty(name))
                        bonusNames.Add(name);
                }

                return string.Join(", ", bonusNames);
            }
            catch
            {
                return "";
            }
        }

        private string GetBonusTypeName(ParameterTrainingData.TrainingCorrectionKindIndex bonus)
        {
            switch (bonus)
            {
                case ParameterTrainingData.TrainingCorrectionKindIndex.FriendBonus:
                    return "Friend";
                case ParameterTrainingData.TrainingCorrectionKindIndex.RivalBonus:
                    return "Rival";
                case ParameterTrainingData.TrainingCorrectionKindIndex.GrowthBonus:
                    return "Growth";
                case ParameterTrainingData.TrainingCorrectionKindIndex.DayBonus:
                    return "Day";
                case ParameterTrainingData.TrainingCorrectionKindIndex.PlayerSkillBonus:
                    return "Skill";
                case ParameterTrainingData.TrainingCorrectionKindIndex.MealBonus:
                    return "Meal";
                case ParameterTrainingData.TrainingCorrectionKindIndex.TimeZoonBonus:
                    return "Time";
                case ParameterTrainingData.TrainingCorrectionKindIndex.MoodBonus:
                    return "Mood";
                default:
                    return "";
            }
        }

        private string GetTrainingTypeName(ParameterTrainingData.TrainingKindIndex index)
        {
            switch (index)
            {
                case ParameterTrainingData.TrainingKindIndex.Hp:
                    return "HP Training";
                case ParameterTrainingData.TrainingKindIndex.Mp:
                    return "MP Training";
                case ParameterTrainingData.TrainingKindIndex.Forcefulness:
                    return "Strength Training";
                case ParameterTrainingData.TrainingKindIndex.Robustness:
                    return "Stamina Training";
                case ParameterTrainingData.TrainingKindIndex.Cleverness:
                    return "Wisdom Training";
                case ParameterTrainingData.TrainingKindIndex.Rapidity:
                    return "Speed Training";
                case ParameterTrainingData.TrainingKindIndex.Rest:
                    return "Rest";
                default:
                    return $"Training {(int)index + 1}";
            }
        }

        private int GetTrainingCount()
        {
            try
            {
                var contents = _panel?.m_trainingContents;
                if (contents != null)
                {
                    return contents.Length;
                }
            }
            catch { }
            return 7; // Default: HP, MP, STR, STA, WIS, SPD, Rest
        }

        private string GetStateText(uTrainingPanelCommand.State state)
        {
            switch (state)
            {
                case uTrainingPanelCommand.State.Main:
                    return "Select training";
                case uTrainingPanelCommand.State.Dialog:
                    return "Confirm selection";
                case uTrainingPanelCommand.State.ChangeStateDigimonHistory:
                    return "Digimon history";
                case uTrainingPanelCommand.State.ChangeStateBonus:
                    return "Bonus details";
                case uTrainingPanelCommand.State.Wait:
                    return "Please wait";
                case uTrainingPanelCommand.State.Tutorial:
                    return "Tutorial";
                default:
                    return "Training";
            }
        }

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);

            // Get partner names and positions
            string partner1Name = GetPartnerName(0);
            string partner2Name = GetPartnerName(1);
            int cursorRight = GetCursorPosition(0);
            int cursorLeft = GetCursorPosition(1);
            int totalTrainings = GetTrainingCount();
            string training1 = GetTrainingText(cursorRight);
            string training2 = GetTrainingText(cursorLeft);

            int index1 = cursorRight + 1;
            int index2 = cursorLeft + 1;

            string announcement = $"Training. {stateText}. {partner1Name} on {training1}, {index1} of {totalTrainings}. {partner2Name} on {training2}, {index2} of {totalTrainings}";
            ScreenReader.Say(announcement);
        }
    }
}
