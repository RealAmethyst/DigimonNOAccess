using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the training bonus roulette panel.
    /// Announces only the final result when the roulette stops spinning.
    /// </summary>
    public class TrainingBonusHandler
    {
        private uTrainingPanelBonus _panel;
        private bool _wasActive = false;
        private uTrainingPanelBonus.State _lastState = uTrainingPanelBonus.State.None;
        private bool _resultAnnounced = false;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uTrainingPanelBonus>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_state;
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy &&
                       state != uTrainingPanelBonus.State.None;
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
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = uTrainingPanelBonus.State.None;
            _resultAnnounced = false;

            if (_panel == null)
                return;

            var state = _panel.m_state;

            // Only announce "Roulette" when it first opens - the result will be announced when it stops
            if (state == uTrainingPanelBonus.State.Main)
            {
                ScreenReader.Say("Bonus roulette spinning");
                DebugLogger.Log("[TrainingBonus] Roulette panel opened, spinning");
            }

            _lastState = state;
        }

        private void OnClose()
        {
            _panel = null;
            _lastState = uTrainingPanelBonus.State.None;
            _resultAnnounced = false;
            DebugLogger.Log("[TrainingBonus] Roulette panel closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var state = _panel.m_state;

            // Detect when roulette stops and enters result state
            if (state == uTrainingPanelBonus.State.RouletteResult && _lastState != uTrainingPanelBonus.State.RouletteResult)
            {
                // Roulette just stopped - announce the result
                if (!_resultAnnounced)
                {
                    AnnounceRouletteResult();
                    _resultAnnounced = true;
                }
            }
            else if (state == uTrainingPanelBonus.State.Main && _lastState != uTrainingPanelBonus.State.Main)
            {
                // Started spinning again (if that happens)
                _resultAnnounced = false;
                DebugLogger.Log("[TrainingBonus] Roulette spinning");
            }

            _lastState = state;
        }

        private void AnnounceRouletteResult()
        {
            try
            {
                var icons = _panel?.m_rouletteIcons;
                int resultIndex = _panel?.m_rouletteIconIndex ?? -1;

                if (icons == null || resultIndex < 0 || resultIndex >= icons.Length)
                {
                    DebugLogger.Log($"[TrainingBonus] Could not read result: icons={icons != null}, index={resultIndex}");
                    return;
                }

                var resultIcon = icons[resultIndex];
                if (resultIcon == null)
                {
                    DebugLogger.Log("[TrainingBonus] Result icon is null");
                    return;
                }

                var iconKind = resultIcon.iconIndex;
                string resultText = iconKind == uTrainingPanelBonus.RouletteIcon.RouletteIconKindIndex.Win
                    ? "Bonus!"
                    : "No bonus";

                ScreenReader.Say(resultText);
                DebugLogger.Log($"[TrainingBonus] Result: {resultText} (index={resultIndex}, kind={iconKind})");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TrainingBonus] Error reading result: {ex.Message}");
            }
        }
    }
}
