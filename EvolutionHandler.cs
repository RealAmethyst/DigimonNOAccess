using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the digivolution sequence.
    /// Announces when digivolution starts and the resulting Digimon name.
    /// </summary>
    public class EvolutionHandler
    {
        private EvolutionBase _evolution;
        private bool _wasActive = false;
        private EvolutionBase.State _lastState = EvolutionBase.State.Loading;
        private bool _announcedStart = false;
        private bool _announcedResult = false;
        private string _originalPartnerName = null; // Cached at start of evolution

        public bool IsActive()
        {
            if (_evolution == null)
            {
                // Try to find any active evolution (EvolutionMain or MiracleMain)
                _evolution = Object.FindObjectOfType<EvolutionBase>();
            }

            if (_evolution == null)
                return false;

            try
            {
                return _evolution.gameObject != null &&
                       _evolution.gameObject.activeInHierarchy;
            }
            catch
            {
                return false;
            }
        }

        public void Update()
        {
            bool isActive = IsActive();

            if (isActive && !_wasActive)
            {
                OnStart();
            }
            else if (!isActive && _wasActive)
            {
                OnEnd();
            }
            else if (isActive)
            {
                CheckStateChange();
            }

            _wasActive = isActive;
        }

        private void OnStart()
        {
            _lastState = EvolutionBase.State.Loading;
            _announcedStart = false;
            _announcedResult = false;

            DebugLogger.Log("[Evolution] Digivolution sequence started");

            // Get the original partner name from m_BeforeModelName
            _originalPartnerName = GetBeforeEvolutionName();

            // Announce that digivolution is happening with partner identification
            if (!string.IsNullOrEmpty(_originalPartnerName))
            {
                ScreenReader.Say($"{_originalPartnerName} is digivolving");
                DebugLogger.Log($"[Evolution] {_originalPartnerName} is digivolving");
            }
            else
            {
                ScreenReader.Say("Digivolving");
            }
            _announcedStart = true;
        }

        private void OnEnd()
        {
            _evolution = null;
            _lastState = EvolutionBase.State.Loading;
            _announcedStart = false;
            _announcedResult = false;
            _originalPartnerName = null;
            DebugLogger.Log("[Evolution] Digivolution sequence ended");
        }

        private void CheckStateChange()
        {
            if (_evolution == null)
                return;

            try
            {
                var state = _evolution.m_state;

                if (state != _lastState)
                {
                    DebugLogger.Log($"[Evolution] State changed to {state}");

                    // Announce the result when entering Finish state
                    if (state == EvolutionBase.State.Finish && !_announcedResult)
                    {
                        AnnounceResult();
                        _announcedResult = true;
                    }

                    _lastState = state;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Evolution] Error checking state: {ex.Message}");
            }
        }

        private void AnnounceResult()
        {
            if (_evolution == null)
                return;

            try
            {
                // m_DigimonName contains the NEW Digimon name (what it evolved INTO)
                string newDigimonName = _evolution.m_DigimonName;

                if (!string.IsNullOrEmpty(newDigimonName))
                {
                    string announcement;
                    // Use the cached original name (what it was BEFORE evolution)
                    if (!string.IsNullOrEmpty(_originalPartnerName))
                    {
                        announcement = $"{_originalPartnerName} digivolved to {newDigimonName}";
                    }
                    else
                    {
                        announcement = $"Digivolved to {newDigimonName}";
                    }
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"[Evolution] Announced: {announcement}");
                }
                else
                {
                    ScreenReader.Say("Digivolution complete");
                    DebugLogger.Log("[Evolution] Announced: Digivolution complete (no name found)");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Evolution] Error announcing result: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the name of the partner that is digivolving BEFORE evolution.
        /// Uses m_BeforeModelName which stores the original model name,
        /// then searches parameter data for matching model to get localized name.
        /// </summary>
        private string GetBeforeEvolutionName()
        {
            if (_evolution == null)
                return null;

            try
            {
                // m_BeforeModelName contains the model names BEFORE evolution
                var beforeModelNames = _evolution.m_BeforeModelName;
                if (beforeModelNames == null || beforeModelNames.Length == 0)
                {
                    DebugLogger.Log("[Evolution] m_BeforeModelName is null or empty");
                    return null;
                }

                // Get the first partner's before model name
                string modelName = beforeModelNames[0];
                if (string.IsNullOrEmpty(modelName))
                {
                    DebugLogger.Log("[Evolution] Before model name is empty");
                    return null;
                }

                DebugLogger.Log($"[Evolution] Before model name: {modelName}");

                // Search parameter data for matching model name
                var paramManager = AppMainScript.parameterManager;
                if (paramManager == null)
                {
                    DebugLogger.Log("[Evolution] parameterManager is null");
                    return null;
                }

                var digimonData = paramManager.digimonData;
                if (digimonData == null)
                {
                    DebugLogger.Log("[Evolution] digimonData is null");
                    return null;
                }

                // Iterate through all digimon to find matching model name
                int count = digimonData.GetRecordMax();
                DebugLogger.Log($"[Evolution] Searching {count} digimon records for model {modelName}");

                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        var paramData = digimonData.GetParams(i);
                        if (paramData != null && paramData.m_mdlName == modelName)
                        {
                            string name = paramData.GetDefaultName();
                            if (!string.IsNullOrEmpty(name))
                            {
                                DebugLogger.Log($"[Evolution] Found match at index {i}: {name}");
                                return name;
                            }
                        }
                    }
                    catch { }
                }

                DebugLogger.Log($"[Evolution] No matching digimon found for model {modelName}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Evolution] Error getting before evolution name: {ex.Message}");
            }

            return null;
        }

        public void AnnounceStatus()
        {
            if (!IsActive())
                return;

            try
            {
                var state = _evolution.m_state;
                string digimonName = _evolution.m_DigimonName;

                string announcement = "Digivolution in progress";
                // Use cached original name
                if (!string.IsNullOrEmpty(_originalPartnerName))
                {
                    announcement = $"{_originalPartnerName} digivolution in progress";
                }
                if (!string.IsNullOrEmpty(digimonName))
                {
                    announcement += $", becoming {digimonName}";
                }

                ScreenReader.Say(announcement);
            }
            catch
            {
                ScreenReader.Say("Digivolution in progress");
            }
        }
    }
}
