using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the map panel (world/area/minimap navigation)
    /// and the town jump panel (fast travel when in the city).
    /// Both open with the same key but are different UI systems.
    /// </summary>
    public class MapPanelHandler : HandlerBase<uDigiviceMapPanel>
    {
        protected override string LogTag => "[MapPanel]";
        public override int Priority => 65;

        // Field map state
        private uDigiviceMapPanel.State _lastState = uDigiviceMapPanel.State.NONE;
        private string _lastLocationName = "";

        // Town jump state
        private uTownJumpPanel _townJumpPanel;
        private uTownJumpPanelCommand _townJumpCommand;
        private bool _townJumpActive;
        private int _lastTownJumpCursor = -1;

        public override bool IsOpen()
        {
            // Check town jump first (asset-loaded via MainGameManager)
            // Skip if construction panel is active (town jump UI briefly appears during confirmations)
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    var constructionPanel = Object.FindObjectOfType<uConstructionPanel>();
                    if (constructionPanel != null && constructionPanel.m_state != uConstructionPanel.State.None)
                    {
                        // Construction is open, don't detect town jump
                    }
                    else
                    {
                        var tjPanel = mgm.townJumpUI;
                        if (tjPanel != null && tjPanel.m_state == uTownJumpPanel.State.CommandMain)
                        {
                            _townJumpPanel = tjPanel;
                            _townJumpActive = true;
                            return true;
                        }
                    }
                }
            }
            catch { }

            _townJumpActive = false;
            _townJumpPanel = null;

            // Then check field map (scene object)
            if (_panel == null)
                _panel = Object.FindObjectOfType<uDigiviceMapPanel>();

            if (_panel == null)
                return false;

            var state = _panel.m_state;
            return state != uDigiviceMapPanel.State.NONE && state != uDigiviceMapPanel.State.CLOSE;
        }

        protected override void OnOpen()
        {
            if (_townJumpActive)
            {
                OnTownJumpOpen();
                return;
            }

            _lastState = uDigiviceMapPanel.State.NONE;
            _lastLocationName = "";

            if (_panel == null)
                return;

            var state = _panel.m_state;
            _lastState = state;

            string mapLevel = GetMapLevelName(state);
            string locationName = GetCurrentLocationName();
            _lastLocationName = locationName;

            string announcement = !string.IsNullOrEmpty(locationName)
                ? $"Map, {mapLevel}. {locationName}"
                : $"Map, {mapLevel}";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened, state={state}, location={locationName}");
        }

        protected override void OnClose()
        {
            if (_townJumpActive || _townJumpPanel != null)
                OnTownJumpClose();

            _lastState = uDigiviceMapPanel.State.NONE;
            _lastLocationName = "";
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (_townJumpActive)
            {
                UpdateTownJump();
                return;
            }

            CheckStateChange();
            CheckLocationChange();
        }

        // ── Town Jump (Fast Travel) ──

        private void OnTownJumpOpen()
        {
            _lastTownJumpCursor = -1;

            try { _townJumpCommand = _townJumpPanel?.m_townJumpPanelCommand; }
            catch { _townJumpCommand = null; }

            int cursor = GetTownJumpCursor();
            string destination = GetTownJumpDestinationName(cursor);
            int total = GetTownJumpItemCount();

            string announcement = $"{GetTownJumpCaptionText()}. {destination}. {cursor + 1} of {total}";
            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Town Jump opened, cursor={cursor}, items={total}");
            _lastTownJumpCursor = cursor;
        }

        private void OnTownJumpClose()
        {
            _townJumpPanel = null;
            _townJumpCommand = null;
            _townJumpActive = false;
            _lastTownJumpCursor = -1;
        }

        private void UpdateTownJump()
        {
            if (_townJumpCommand == null) return;

            int cursor = GetTownJumpCursor();
            if (cursor == _lastTownJumpCursor || cursor < 0) return;

            string destination = GetTownJumpDestinationName(cursor);
            int total = GetTownJumpItemCount();

            ScreenReader.Say($"{destination}. {cursor + 1} of {total}");
            DebugLogger.Log($"{LogTag} Town Jump cursor: {destination} ({cursor + 1}/{total})");
            _lastTownJumpCursor = cursor;
        }

        private int GetTownJumpCursor()
        {
            try
            {
                if (_townJumpCommand != null)
                    return _townJumpCommand.m_selectNo;
            }
            catch { }
            return 0;
        }

        private int GetTownJumpItemCount()
        {
            try
            {
                var datas = _townJumpPanel?.GetParameterTownJumpDatas();
                if (datas != null && datas.Length > 0)
                    return datas.Length;
            }
            catch { }

            try
            {
                if (_townJumpCommand != null)
                    return _townJumpCommand.m_itemMaxNum;
            }
            catch { }
            return 1;
        }

        private string GetTownJumpDestinationName(int index)
        {
            try
            {
                if (_townJumpCommand == null)
                {
                    DebugLogger.Log($"{LogTag} Destination {index}: town jump command is null");
                }
                else if (_townJumpPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Destination {index}: town jump panel is null");
                }
                else
                {
                    uint pointId = _townJumpCommand.selectPointId;
                    if (pointId == 0)
                    {
                        DebugLogger.Log($"{LogTag} Destination {index}: selectPointId is zero");
                    }
                    else
                    {
                        var jumpData = _townJumpPanel.GetParameterTownJumpData(pointId);
                        if (jumpData == null)
                        {
                            DebugLogger.Log($"{LogTag} Destination {index}: GetParameterTownJumpData({pointId}) returned null");
                        }
                        else
                        {
                            string dataName = jumpData.GetName();
                            string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(dataName))?.Trim();
                            if (!string.IsNullOrWhiteSpace(cleaned))
                                return cleaned;

                            DebugLogger.Log($"{LogTag} Destination {index}: ParameterTownJumpData.GetName() is empty");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Destination {index}: parameter name read failed: {ex.Message}");
            }

            try
            {
                if (_townJumpCommand == null)
                {
                    DebugLogger.Log($"{LogTag} Destination {index}: command unavailable for rendered row");
                }
                else
                {
                    var parts = _townJumpCommand.GetSelectItemParts(index);
                    if (parts == null)
                    {
                        DebugLogger.Log($"{LogTag} Destination {index}: selected row parts are null");
                    }
                    else
                    {
                        var nameText = parts.m_name;
                        if (nameText == null)
                        {
                            DebugLogger.Log($"{LogTag} Destination {index}: selected row m_name is null");
                        }
                        else
                        {
                            string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(nameText.text))?.Trim();
                            if (!string.IsNullOrWhiteSpace(cleaned))
                                return cleaned;

                            DebugLogger.Log($"{LogTag} Destination {index}: selected row m_name.text is empty");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Destination {index}: rendered row read failed: {ex.Message}");
            }

            DebugLogger.Log($"{LogTag} Destination {index}: localized name unavailable; using English fallback");
            return AnnouncementBuilder.FallbackItem("Destination", index);
        }

        private string GetTownJumpCaptionText()
        {
            const string fallback = "Fast Travel";

            try
            {
                if (_townJumpPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Town jump caption: panel is null");
                    return fallback;
                }

                var caption = _townJumpPanel.m_caption;
                if (caption == null)
                {
                    DebugLogger.Log($"{LogTag} Town jump caption: m_caption is null");
                    return fallback;
                }

                var text = caption.m_text;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} Town jump caption: m_caption.m_text is null");
                    return fallback;
                }

                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(text.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"{LogTag} Town jump caption: m_caption.m_text.text is empty");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Town jump caption read failed: {ex.Message}");
                return fallback;
            }
        }

        // ── Field Map ──

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var currentState = _panel.m_state;

            if (currentState != _lastState && currentState != uDigiviceMapPanel.State.CLOSE)
            {
                _lastLocationName = "";
                string mapLevel = GetMapLevelName(currentState);
                string locationName = GetCurrentLocationName();
                _lastLocationName = locationName;

                string announcement = !string.IsNullOrEmpty(locationName)
                    ? $"{mapLevel}. {locationName}"
                    : mapLevel;

                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} State changed to {mapLevel}");
                _lastState = currentState;
            }
        }

        private void CheckLocationChange()
        {
            if (_panel == null)
                return;

            string currentLocation = GetCurrentLocationName();

            if (!string.IsNullOrEmpty(currentLocation) && currentLocation != _lastLocationName)
            {
                ScreenReader.Say(currentLocation);
                DebugLogger.Log($"{LogTag} Location changed: {currentLocation}");
                _lastLocationName = currentLocation;
            }
        }

        private string GetMapLevelName(uDigiviceMapPanel.State state)
        {
            string fallback = state switch
            {
                uDigiviceMapPanel.State.WORLD => "World Map",
                uDigiviceMapPanel.State.AREA => "Area Map",
                uDigiviceMapPanel.State.MINI_AREA => "Local Map",
                _ => "Map"
            };

            if (state != uDigiviceMapPanel.State.WORLD &&
                state != uDigiviceMapPanel.State.AREA &&
                state != uDigiviceMapPanel.State.MINI_AREA)
                return fallback;

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} {state} caption: map panel is null");
                    return fallback;
                }

                var caption = _panel.m_uDigiviceMapPanelCaption;
                if (caption == null)
                {
                    DebugLogger.Log($"{LogTag} {state} caption: m_uDigiviceMapPanelCaption is null");
                    return fallback;
                }

                var text = caption.m_caption;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} {state} caption: m_caption is null");
                    return fallback;
                }

                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(text.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"{LogTag} {state} caption: m_caption.text is empty");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} {state} caption read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetCurrentLocationName()
        {
            try
            {
                var headline = _panel?.m_uDigiviceMapPanelHeadLine;
                if (headline != null)
                {
                    var currentLookingText = headline.m_CurrentLookingName;
                    if (currentLookingText != null)
                    {
                        string text = currentLookingText.text;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }

                    var mapNameText = headline.m_MapName;
                    if (mapNameText != null)
                    {
                        string text = mapNameText.text;
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting location name: {ex.Message}");
            }

            return "";
        }

        // ── Status ──

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            if (_townJumpActive)
            {
                int cursor = GetTownJumpCursor();
                string destination = GetTownJumpDestinationName(cursor);
                int total = GetTownJumpItemCount();
                ScreenReader.Say($"{GetTownJumpCaptionText()}. {destination}. {cursor + 1} of {total}");
                return;
            }

            var state = _panel.m_state;
            string mapLevel = GetMapLevelName(state);
            string locationName = GetCurrentLocationName();

            string announcement = !string.IsNullOrEmpty(locationName)
                ? $"Map, {mapLevel}. {locationName}"
                : $"Map, {mapLevel}";

            ScreenReader.Say(announcement);
        }
    }
}
