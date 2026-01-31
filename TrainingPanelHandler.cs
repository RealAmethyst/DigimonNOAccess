using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the training panel (gym training selection).
    /// </summary>
    public class TrainingPanelHandler
    {
        private uTrainingPanelCommand _panel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uTrainingPanelCommand.State _lastState = uTrainingPanelCommand.State.None;

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
                CheckCursorChange();
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastState = uTrainingPanelCommand.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetTrainingText(cursor);
            int total = GetTrainingCount();

            string announcement = $"Training. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);

            DebugLogger.Log($"[TrainingPanel] Panel opened, state={state}, cursor={cursor}");
            _lastState = state;
            _lastCursor = cursor;
        }

        private void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _lastState = uTrainingPanelCommand.State.None;
            DebugLogger.Log("[TrainingPanel] Panel closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var state = _panel.m_state;
            if (state != _lastState)
            {
                string stateText = GetStateText(state);
                ScreenReader.Say(stateText);
                DebugLogger.Log($"[TrainingPanel] State changed to {state}");
                _lastState = state;
                _lastCursor = -1; // Reset cursor on state change
            }
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetTrainingText(cursor);
                int total = GetTrainingCount();

                string announcement = $"{itemText}, {cursor + 1} of {total}";
                ScreenReader.Say(announcement);

                DebugLogger.Log($"[TrainingPanel] Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                var cursors = _panel?.m_trainingCursors;
                if (cursors != null && cursors.Length > 0)
                {
                    // Get the first cursor's index (convert TrainingKindIndex to int)
                    var cursor = cursors[0];
                    if (cursor != null)
                    {
                        return (int)cursor.index;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingPanel] Error getting cursor: {ex.Message}");
            }
            return 0;
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
                        // Get training level info
                        int level = content.level;
                        var trainingIndex = content.index;

                        // Map training type to readable name
                        string trainingName = GetTrainingTypeName(trainingIndex);

                        if (level > 0)
                        {
                            return $"{trainingName}, Level {level}";
                        }
                        return trainingName;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingPanel] Error reading text: {ex.Message}");
            }

            return $"Training {index + 1}";
        }

        private string GetTrainingTypeName(ParameterTrainingData.TrainingKindIndex index)
        {
            // Map the training kind to readable names
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
            return 6; // Default: HP, MP, STR, STA, WIS, SPD
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
                    return "Bonus";
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
            int cursor = GetCursorPosition();
            string itemText = GetTrainingText(cursor);
            int total = GetTrainingCount();

            string announcement = $"Training. {stateText}. {itemText}, {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
        }
    }
}
