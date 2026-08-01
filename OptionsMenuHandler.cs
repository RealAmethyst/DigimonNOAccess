using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for options/settings menus.
    /// </summary>
    public class OptionsMenuHandler : IAccessibilityHandler
    {
        public int Priority => 40;

        private uOptionPanel _optionPanel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uOptionPanel.MainSettingState _lastState = uOptionPanel.MainSettingState.TOP;
        private uOptionPanel.State _lastPanelState = uOptionPanel.State.NONE;
        private string _lastValue = "";

        // Agree window tracking
        private uAgreeWindow _agreeWindow;
        private bool _agreeWasOpen;
        private uAgreeWindow.CursorIndex _lastAgreeCursor;

        /// <summary>
        /// Check if panel exists and is active.
        /// </summary>
        private bool IsPanelActive()
        {
            _optionPanel = Object.FindObjectOfType<uOptionPanel>();

            return _optionPanel != null &&
                   _optionPanel.gameObject != null &&
                   _optionPanel.gameObject.activeInHierarchy;
        }

        /// <summary>
        /// Check if options panel is currently open and in main settings mode.
        /// Yields to AccessibilityMenuHandler when it's active.
        /// </summary>
        public bool IsOpen()
        {
            if (AccessibilityMenuHandler.Instance != null && AccessibilityMenuHandler.Instance.IsOpen())
                return false;

            return IsPanelActive() && _optionPanel.m_State == uOptionPanel.State.MAIN_SETTING;
        }

        /// <summary>
        /// Called every frame to track menu state.
        /// </summary>
        public void Update()
        {
            bool isPanelActive = IsPanelActive();
            bool isInMainSetting = isPanelActive && _optionPanel.m_State == uOptionPanel.State.MAIN_SETTING;

            uOptionPanel.State currentPanelState = isPanelActive ? _optionPanel.m_State : uOptionPanel.State.NONE;

            // Detect when panel transitions to MAIN_SETTING
            if (isInMainSetting && _lastPanelState != uOptionPanel.State.MAIN_SETTING)
            {
                OnOpen();
            }
            else if (!isInMainSetting && _wasActive)
            {
                OnClose();
            }
            else if (isInMainSetting)
            {
                CheckStateChange();
                CheckCursorChange();
                CheckValueChange();
            }

            _lastPanelState = currentPanelState;
            _wasActive = isInMainSetting;

            // Track agree window independently of main settings
            UpdateAgreeWindow();
        }

        private void OnOpen()
        {
            _lastCursor = -1;
            _lastState = uOptionPanel.MainSettingState.TOP;
            _lastValue = "";

            if (_optionPanel == null)
                return;

            var state = _optionPanel.m_MainSettingState;
            string menuName = GetMenuName(state);

            var itemInfo = GetCurrentItemInfo();
            string announcement;

            if (itemInfo != null)
            {
                _lastValue = itemInfo.Value;
                announcement = $"{menuName}. {itemInfo.Name}";
                if (!string.IsNullOrEmpty(itemInfo.Value))
                    announcement += $", {itemInfo.Value}";
                announcement += $", {itemInfo.Index} of {itemInfo.Total}";
            }
            else
            {
                announcement = menuName;
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[OptionsMenu] Opened: {menuName}");

            _lastState = state;
            _lastCursor = itemInfo?.Index ?? -1;
        }

        private void OnClose()
        {
            _optionPanel = null;
            _lastCursor = -1;
            _lastValue = "";
            DebugLogger.Log("[OptionsMenu] Closed");
        }

        private void CheckStateChange()
        {
            if (_optionPanel == null)
                return;

            var state = _optionPanel.m_MainSettingState;

            if (state != _lastState)
            {
                _lastCursor = -1;
                _lastValue = "";
                string menuName = GetMenuName(state);

                var itemInfo = GetCurrentItemInfo();
                string announcement;

                if (itemInfo != null)
                {
                    _lastValue = itemInfo.Value;
                    announcement = $"{menuName}. {itemInfo.Name}";
                    if (!string.IsNullOrEmpty(itemInfo.Value))
                        announcement += $", {itemInfo.Value}";
                    announcement += $", {itemInfo.Index} of {itemInfo.Total}";
                }
                else
                {
                    announcement = menuName;
                }

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[OptionsMenu] State changed to: {menuName}");

                _lastState = state;
                _lastCursor = itemInfo?.Index ?? -1;
            }
        }

        private void CheckCursorChange()
        {
            if (_optionPanel == null)
                return;

            var itemInfo = GetCurrentItemInfo();
            if (itemInfo == null)
                return;

            if (itemInfo.Index != _lastCursor)
            {
                _lastValue = itemInfo.Value;
                string announcement = itemInfo.Name;
                if (!string.IsNullOrEmpty(itemInfo.Value))
                    announcement += $", {itemInfo.Value}";
                announcement += $", {itemInfo.Index} of {itemInfo.Total}";

                ScreenReader.Say(announcement);
                DebugLogger.Log($"[OptionsMenu] Cursor: {itemInfo.Name} = {itemInfo.Value}");
                _lastCursor = itemInfo.Index;
            }
        }

        private void CheckValueChange()
        {
            if (_optionPanel == null)
                return;

            var itemInfo = GetCurrentItemInfo();
            if (itemInfo == null)
                return;

            // Only announce value change if cursor hasn't moved
            if (itemInfo.Index == _lastCursor && !string.IsNullOrEmpty(itemInfo.Value) && itemInfo.Value != _lastValue)
            {
                ScreenReader.Say(itemInfo.Value);
                DebugLogger.Log($"[OptionsMenu] Value changed: {itemInfo.Value}");
                _lastValue = itemInfo.Value;
            }
        }

        /// <summary>
        /// The name of the options screen we are on.
        ///
        /// Read from the game's own caption first, so it is correct in whatever
        /// language the player is running. Each command panel carries a
        /// uOptionPanelCaption whose m_Caption is the rendered header - the exact
        /// text a sighted player sees at the top of the screen.
        ///
        /// The English names below are only a fallback for when the caption has not
        /// been populated yet (it fills in a frame after the panel opens). Falling
        /// back is deliberate: saying the wrong language is better than saying
        /// nothing at all about which screen you are on.
        /// </summary>
        private string GetMenuName(uOptionPanel.MainSettingState state)
        {
            string caption = GetPanelCaption(state);
            if (!string.IsNullOrWhiteSpace(caption))
                return caption;

            switch (state)
            {
                case uOptionPanel.MainSettingState.TOP:
                    return "System Menu";
                case uOptionPanel.MainSettingState.OPTION:
                    return "System Settings";
                case uOptionPanel.MainSettingState.GRAPHICS:
                    return "Graphics Settings";
                case uOptionPanel.MainSettingState.KEYCONFIG:
                    return "Key Config";
                case uOptionPanel.MainSettingState.APPLICATION_QUIT:
                    return "Quit Game";
                case uOptionPanel.MainSettingState.AGREE:
                    return "Agreement";
                default:
                    return "Options";
            }
        }

        /// <summary>
        /// The rendered caption for a settings state, or null if it is not reachable
        /// yet. Every hop logs its own reason rather than failing silently, so a
        /// broken chain after a game update shows up in the log instead of quietly
        /// reverting everyone to English.
        /// </summary>
        private string GetPanelCaption(uOptionPanel.MainSettingState state)
        {
            try
            {
                if (_optionPanel == null)
                {
                    DebugLogger.Log("[OptionsMenu] Caption: option panel was null");
                    return null;
                }

                var commandPanels = _optionPanel.m_uOptionPanelCommand;
                if (commandPanels == null)
                {
                    DebugLogger.Log("[OptionsMenu] Caption: no command panel array");
                    return null;
                }

                int index = (int)state;
                if (index < 0 || index >= commandPanels.Length)
                {
                    DebugLogger.Log($"[OptionsMenu] Caption: state {state} index {index} was outside the command panel array");
                    return null;
                }

                var panel = commandPanels[index];
                if (panel == null)
                {
                    DebugLogger.Log($"[OptionsMenu] Caption: state {state} command panel was null");
                    return null;
                }

                var captionPanel = panel.m_Caption;
                if (captionPanel == null)
                {
                    DebugLogger.Log($"[OptionsMenu] Caption: state {state} has no caption panel");
                    return null;
                }

                var text = captionPanel.m_Caption;
                if (text == null)
                {
                    DebugLogger.Log($"[OptionsMenu] Caption: state {state} caption panel has no Text");
                    return null;
                }

                string caption = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(text.text))?.Trim();
                if (string.IsNullOrWhiteSpace(caption) || TextUtilities.IsPlaceholderText(caption))
                {
                    DebugLogger.Log($"[OptionsMenu] Caption: state {state} m_Caption.m_Caption.text unusable: {TextUtilities.DescribeUnusable(caption)}");
                    return null;
                }

                return caption;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OptionsMenu] Caption read failed for {state}: {ex.Message}");
                return null;
            }
        }

        private ItemInfo GetCurrentItemInfo()
        {
            if (_optionPanel == null)
                return null;

            try
            {
                var state = _optionPanel.m_MainSettingState;
                var commandPanels = _optionPanel.m_uOptionPanelCommand;

                if (commandPanels == null)
                    return null;

                int panelIndex = (int)state;
                if (panelIndex < 0 || panelIndex >= commandPanels.Length)
                    return null;

                var commandPanel = commandPanels[panelIndex];
                if (commandPanel == null)
                    return null;

                var cursorController = commandPanel.m_KeyCursorController;
                if (cursorController == null)
                    return null;

                int dataIndex = cursorController.m_DataIndex;
                int totalItems = cursorController.m_DataMax;

                string itemName = "";
                string itemValue = "";

                // Handle different panel types
                if (state == uOptionPanel.MainSettingState.TOP)
                {
                    var topPanel = commandPanel.TryCast<uOptionTopPanelCommand>();
                    if (topPanel != null)
                    {
                        var info = GetTopPanelItem(topPanel, dataIndex);
                        itemName = info.name;
                        itemValue = info.value;
                        // Use m_DataMax for total (includes our injected Accessibility item)
                        // m_items.Count only has game items, m_DataMax has game + ours
                        totalItems = cursorController.m_DataMax;
                    }
                }
                else if (state == uOptionPanel.MainSettingState.OPTION)
                {
                    var settingsPanel = commandPanel.TryCast<uOptionPanelCommand>();
                    if (settingsPanel != null)
                    {
                        var info = GetSettingsPanelItem(settingsPanel, dataIndex);
                        itemName = info.name;
                        itemValue = info.value;
                        if (settingsPanel.m_items != null)
                            totalItems = settingsPanel.m_items.Count;
                    }
                }
                else if (state == uOptionPanel.MainSettingState.GRAPHICS)
                {
                    var graphicsPanel = commandPanel.TryCast<uOptionGraphicsPanelCommand>();
                    if (graphicsPanel != null)
                    {
                        var info = GetGraphicsPanelItem(graphicsPanel, dataIndex);
                        itemName = info.name;
                        itemValue = info.value;
                        if (graphicsPanel.m_items != null)
                            totalItems = graphicsPanel.m_items.Count;
                    }
                }
                else if (state == uOptionPanel.MainSettingState.KEYCONFIG)
                {
                    var keyConfigPanel = commandPanel.TryCast<uOptionKeyConfigPanelCommand>();
                    if (keyConfigPanel != null)
                    {
                        var info = GetKeyConfigPanelItem(keyConfigPanel, dataIndex);
                        itemName = info.name;
                        itemValue = info.value;

                        // Total count from m_itemTypeList (NOT m_DataMax which is only 6!)
                        if (keyConfigPanel.m_itemTypeList != null)
                            totalItems = keyConfigPanel.m_itemTypeList.Count;

                        // The actual item position = scrollPos + dataIndex
                        // dataIndex is 0-5 (cursor position within visible area)
                        // scrollPos indicates how many items we've scrolled past
                        int actualIndex = keyConfigPanel.m_scrollItemPos + dataIndex;
                        dataIndex = actualIndex; // Update for position reporting

                        DebugLogger.Log($"[KeyConfig] actualIndex={actualIndex}, totalItems={totalItems}");
                    }
                }

                if (string.IsNullOrEmpty(itemName))
                    itemName = AnnouncementBuilder.FallbackItem("Item", dataIndex);

                return new ItemInfo
                {
                    Name = itemName,
                    Value = itemValue,
                    Index = dataIndex + 1,
                    Total = totalItems
                };
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"[OptionsMenu] Error getting item info: {ex.Message}");
                return null;
            }
        }

        private (string name, string value) GetTopPanelItem(uOptionTopPanelCommand panel, int dataIndex)
        {
            string name = "";
            string value = "";

            // Handle our injected Accessibility item
            if (dataIndex == OptionPanelPatch.AccessibilityItemIndex && OptionPanelPatch.AccessibilityItemIndex >= 0)
                return ("Accessibility", "");

            try
            {
                if (panel == null)
                {
                    DebugLogger.Log("[OptionsMenu] Top command: top panel was null");
                    return (name, value);
                }

                string fallbackName = "";
                if (panel.m_items != null && dataIndex >= 0 && dataIndex < panel.m_items.Count)
                {
                    var item = panel.m_items[dataIndex];
                    if (item != null)
                    {
                        var voidItem = item.TryCast<uOptionPanelItemVoid>();
                        if (voidItem != null)
                            fallbackName = GetSettingStateName(voidItem.m_settingState);
                    }
                }

                var commandInfoArray = panel.m_CommandInfoArray;
                if (commandInfoArray == null)
                {
                    DebugLogger.Log("[OptionsMenu] Top command: m_CommandInfoArray was null");
                    return (fallbackName, value);
                }

                if (dataIndex < 0 || dataIndex >= commandInfoArray.Length)
                {
                    DebugLogger.Log($"[OptionsMenu] Top command: logical index {dataIndex} was outside m_CommandInfoArray");
                    return (fallbackName, value);
                }

                var info = commandInfoArray[dataIndex];
                if (info == null)
                {
                    DebugLogger.Log($"[OptionsMenu] Top command: m_CommandInfoArray[{dataIndex}] was null");
                    return (fallbackName, value);
                }

                var commandName = info.m_CommandName;
                if (commandName == null)
                {
                    DebugLogger.Log($"[OptionsMenu] Top command: m_CommandInfoArray[{dataIndex}].m_CommandName was null");
                }
                else
                {
                    name = TextUtilities.StripRichTextTags(commandName.text)?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        DebugLogger.Log($"[OptionsMenu] Top command: m_CommandInfoArray[{dataIndex}].m_CommandName.text was empty");
                        name = fallbackName;
                    }
                }

                var commandNum = info.m_CommandNum;
                if (commandNum != null)
                    value = TextUtilities.StripRichTextTags(commandNum.text)?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(name))
                    name = fallbackName;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"[OptionsMenu] Error reading top panel: {ex.Message}");
            }

            return (name, value);
        }

        private string GetSettingStateName(uOptionPanel.MainSettingState state)
        {
            switch (state)
            {
                case uOptionPanel.MainSettingState.OPTION:
                    return "System Settings";
                case uOptionPanel.MainSettingState.GRAPHICS:
                    return "Graphics Settings";
                case uOptionPanel.MainSettingState.KEYCONFIG:
                    return "Key Config";
                case uOptionPanel.MainSettingState.APPLICATION_QUIT:
                    return "Quit Game";
                case uOptionPanel.MainSettingState.AGREE:
                    return "Agreement";
                default:
                    return "";
            }
        }

        private (string name, string value) GetSettingsPanelItem(uOptionPanelCommand panel, int dataIndex)
        {
            string name = "";
            string value = "";

            try
            {
                if (panel == null)
                {
                    DebugLogger.Log("[Settings] Localized row: settings panel was null");
                    return (name, value);
                }

                // Settings panel has different item types, each with their own text fields.
                // We must check the actual item type first to read from the correct field.
                //
                // Item types:
                // - uOptionPanelItemVoiceLanguage: name="Voice Language", value from m_languageType.text
                // - uOptionPanelItemBgmVolume: name="Music Volume", value from slider
                // - uOptionPanelItemVoiceVolume: name="Voice Volume", value from slider
                // - uOptionPanelItemSeVolume: name="SFX Volume", value from slider
                // - uOptionPanelItemCameraV: name="Camera Up/Down", value from toggle
                // - uOptionPanelItemCameraH: name="Camera L/R", value from toggle
                // - uOptionPanelItemSensitivity: name="Cam. Sens.", value from slider
                // - uOptionPanelItemVoid: typically "Back" button

                var commandInfoArray = panel.m_CommandInfoArray;
                int itemCount = panel.m_items?.Count ?? 0;
                int visibleSlotCount = commandInfoArray?.Length ?? 0;

                DebugLogger.Log($"[Settings] Reading dataIndex={dataIndex}, itemCount={itemCount}, visibleSlots={visibleSlotCount}");

                // Keep the handler's established logical-index-to-visible-slot
                // mapping for this recycled row array.
                if (commandInfoArray == null)
                {
                    DebugLogger.Log("[Settings] Localized row: m_CommandInfoArray was null");
                }
                else if (dataIndex < 0)
                {
                    DebugLogger.Log($"[Settings] Localized row: logical index {dataIndex} was invalid");
                }
                else
                {
                    int scrollOffset = System.Math.Max(0, dataIndex - (visibleSlotCount - 1));
                    int visibleSlot = dataIndex - scrollOffset;

                    if (visibleSlot < 0 || visibleSlot >= visibleSlotCount)
                    {
                        DebugLogger.Log($"[Settings] Localized row: logical index {dataIndex} mapped outside m_CommandInfoArray (slot {visibleSlot})");
                    }
                    else
                    {
                        var renderedInfo = commandInfoArray[visibleSlot];
                        if (renderedInfo == null)
                        {
                            DebugLogger.Log($"[Settings] Localized row: m_CommandInfoArray[{visibleSlot}] was null for logical index {dataIndex}");
                        }
                        else
                        {
                            var renderedName = renderedInfo.m_CommandName;
                            if (renderedName == null)
                            {
                                DebugLogger.Log($"[Settings] Localized row: m_CommandInfoArray[{visibleSlot}].m_CommandName was null");
                            }
                            else
                            {
                                name = TextUtilities.StripRichTextTags(renderedName.text)?.Trim() ?? "";
                                if (string.IsNullOrWhiteSpace(name))
                                    DebugLogger.Log($"[Settings] Localized row: m_CommandInfoArray[{visibleSlot}].m_CommandName.text was empty");
                            }

                            var renderedValue = renderedInfo.m_CommandNum;
                            if (renderedValue == null)
                            {
                                DebugLogger.Log($"[Settings] Localized row: m_CommandInfoArray[{visibleSlot}].m_CommandNum was null");
                            }
                            else
                            {
                                value = TextUtilities.StripRichTextTags(renderedValue.text)?.Trim() ?? "";
                                if (string.IsNullOrWhiteSpace(value))
                                    DebugLogger.Log($"[Settings] Localized row: m_CommandInfoArray[{visibleSlot}].m_CommandNum.text was empty");
                            }
                        }
                    }
                }

                // First, check if we can read from the actual item in m_items
                // This is more reliable for special item types like VoiceLanguage
                if (panel.m_items != null && dataIndex >= 0 && dataIndex < itemCount)
                {
                    var item = panel.m_items[dataIndex];
                    if (item != null)
                    {
                        // Try specific item types that have their own text fields
                        var voiceLangItem = item.TryCast<uOptionPanelItemVoiceLanguage>();
                        if (voiceLangItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Voice Language";
                            if (string.IsNullOrWhiteSpace(value) && voiceLangItem.m_languageType != null)
                            {
                                value = TextUtilities.StripRichTextTags(voiceLangItem.m_languageType.text)?.Trim() ?? "";
                            }
                            DebugLogger.Log($"[Settings] VoiceLanguage item: value=\"{value}\"");
                            return (name, value);
                        }

                        var bgmItem = item.TryCast<uOptionPanelItemBgmVolume>();
                        if (bgmItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Music Volume";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (string.IsNullOrWhiteSpace(value) && sliderItem?.m_sliderNum != null)
                                value = TextUtilities.StripRichTextTags(sliderItem.m_sliderNum.text)?.Trim() ?? "";
                            DebugLogger.Log($"[Settings] BgmVolume item: value=\"{value}\"");
                            return (name, value);
                        }

                        var voiceVolItem = item.TryCast<uOptionPanelItemVoiceVolume>();
                        if (voiceVolItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Voice Volume";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (string.IsNullOrWhiteSpace(value) && sliderItem?.m_sliderNum != null)
                                value = TextUtilities.StripRichTextTags(sliderItem.m_sliderNum.text)?.Trim() ?? "";
                            DebugLogger.Log($"[Settings] VoiceVolume item: value=\"{value}\"");
                            return (name, value);
                        }

                        var seVolItem = item.TryCast<uOptionPanelItemSeVolume>();
                        if (seVolItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "SFX Volume";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (string.IsNullOrWhiteSpace(value) && sliderItem?.m_sliderNum != null)
                                value = TextUtilities.StripRichTextTags(sliderItem.m_sliderNum.text)?.Trim() ?? "";
                            DebugLogger.Log($"[Settings] SeVolume item: value=\"{value}\"");
                            return (name, value);
                        }

                        var cameraVItem = item.TryCast<uOptionPanelItemCameraV>();
                        if (cameraVItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Camera Up/Down";
                            // Value comes from toggle state: Normal or Reverse
                            if (string.IsNullOrWhiteSpace(value))
                                value = item.Value == 0 ? "Normal" : "Reverse";
                            DebugLogger.Log($"[Settings] CameraV item: value=\"{value}\"");
                            return (name, value);
                        }

                        var cameraHItem = item.TryCast<uOptionPanelItemCameraH>();
                        if (cameraHItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Camera L/R";
                            if (string.IsNullOrWhiteSpace(value))
                                value = item.Value == 0 ? "Normal" : "Reverse";
                            DebugLogger.Log($"[Settings] CameraH item: value=\"{value}\"");
                            return (name, value);
                        }

                        var sensItem = item.TryCast<uOptionPanelItemSensitivity>();
                        if (sensItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Camera Sensitivity";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (string.IsNullOrWhiteSpace(value) && sliderItem?.m_sliderNum != null)
                                value = TextUtilities.StripRichTextTags(sliderItem.m_sliderNum.text)?.Trim() ?? "";
                            DebugLogger.Log($"[Settings] Sensitivity item: value=\"{value}\"");
                            return (name, value);
                        }

                        var voidItem = item.TryCast<uOptionPanelItemVoid>();
                        if (voidItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                name = "Back";
                            DebugLogger.Log($"[Settings] Void (Back) item");
                            return (name, value);
                        }
                    }
                }

                DebugLogger.Log($"[Settings] RESULT: name=\"{name}\", value=\"{value}\"");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Settings] ERROR: {ex.Message}");
                DebugLogger.Warning($"[OptionsMenu] Error reading settings panel: {ex.Message}");
            }

            return (name, value);
        }

        private (string name, string value) GetGraphicsPanelItem(uOptionGraphicsPanelCommand panel, int dataIndex)
        {
            string name = "";
            string value = "";

            try
            {
                // Graphics panel has NO m_CommandInfoArray - read directly from m_items
                if (panel.m_items != null && dataIndex >= 0 && dataIndex < panel.m_items.Count)
                {
                    var item = panel.m_items[dataIndex];
                    if (item != null)
                    {
                        // Try to cast to graphics item
                        var graphicItem = item.TryCast<uOptionGraphicPanelItem>();
                        if (graphicItem != null)
                        {
                            // Get name from setting type
                            var settingType = graphicItem.m_graphicsSettingType;
                            name = GetGraphicsSettingName(settingType);

                            // Get value from m_Text (displays "ON"/"OFF"/resolution/etc)
                            if (graphicItem.m_Text != null && !string.IsNullOrEmpty(graphicItem.m_Text.text))
                                value = graphicItem.m_Text.text;
                        }
                        else
                        {
                            // Fallback for non-graphic items (like "Back" button)
                            if (item.m_caption?.m_Caption != null && !string.IsNullOrEmpty(item.m_caption.m_Caption.text))
                                name = item.m_caption.m_Caption.text;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"[OptionsMenu] Error reading graphics panel: {ex.Message}");
            }

            return (name, value);
        }

        private string GetGraphicsSettingName(uOptionGraphicPanelItem.GraphicsSettingType type)
        {
            switch (type)
            {
                case uOptionGraphicPanelItem.GraphicsSettingType.Resolution:
                    return "Resolution";
                case uOptionGraphicPanelItem.GraphicsSettingType.ScreenMode:
                    return "Screen Mode";
                case uOptionGraphicPanelItem.GraphicsSettingType.Antialiasing:
                    return "Antialiasing";
                case uOptionGraphicPanelItem.GraphicsSettingType.DepthOfField:
                    return "Depth of Field";
                default:
                    return $"Setting {(int)type}";
            }
        }

        private (string name, string value) GetKeyConfigPanelItem(uOptionKeyConfigPanelCommand panel, int dataIndex)
        {
            string name = "";
            string value = "";

            try
            {
                int scrollPos = panel.m_scrollItemPos;
                int visibleCount = panel.m_items?.Count ?? 0;

                // KEY INSIGHT: For Key Config, the cursor dataIndex is 0-5 (within visible slots)
                // The actual item index = scrollPos + dataIndex
                // So if scrollPos=2 and dataIndex=3, we're looking at item 5
                int actualItemIndex = scrollPos + dataIndex;

                DebugLogger.Log($"[KeyConfig] dataIndex={dataIndex}, scrollPos={scrollPos}, actualItemIndex={actualItemIndex}");

                // Read from the visible slot at dataIndex (which shows actualItemIndex)
                if (panel.m_items != null && dataIndex >= 0 && dataIndex < visibleCount)
                {
                    try
                    {
                        var visibleItem = panel.m_items[dataIndex];
                        DebugLogger.Log($"[KeyConfig] visibleItem[{dataIndex}] null? {visibleItem == null}");

                        if (visibleItem != null)
                        {
                            // Get action name from m_HeadText
                            if (visibleItem.m_HeadText != null)
                            {
                                name = visibleItem.m_HeadText.text ?? "";
                                DebugLogger.Log($"[KeyConfig] m_HeadText.text = \"{name}\"");
                            }

                            // Get key binding from the item directly
                            KeyCode keyCode = visibleItem.m_keyCode;
                            DebugLogger.Log($"[KeyConfig] m_keyCode = {keyCode}");
                            if (keyCode != KeyCode.None)
                            {
                                value = KeyCodeToString(keyCode);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DebugLogger.Log($"[KeyConfig] Error reading visible item: {ex.Message}");
                    }
                }

                // Fallback: try m_itemTypeList using actualItemIndex
                if (string.IsNullOrEmpty(name) && panel.m_itemTypeList != null)
                {
                    try
                    {
                        int count = panel.m_itemTypeList.Count;
                        DebugLogger.Log($"[KeyConfig] Trying itemTypeList fallback, Count={count}");

                        if (actualItemIndex >= 0 && actualItemIndex < count)
                        {
                            name = panel.m_itemTypeList._items[actualItemIndex];
                            DebugLogger.Log($"[KeyConfig] itemTypeList[{actualItemIndex}] = \"{name}\"");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        DebugLogger.Log($"[KeyConfig] Error reading itemTypeList: {ex.Message}");
                    }
                }

                // Fallback: try m_keyConfigList using actualItemIndex
                if (string.IsNullOrEmpty(value) && panel.m_keyConfigList != null && actualItemIndex >= 0 && actualItemIndex < panel.m_keyConfigList.Length)
                {
                    try
                    {
                        short keyCodeValue = panel.m_keyConfigList[actualItemIndex];
                        KeyCode keyCode = (KeyCode)keyCodeValue;
                        DebugLogger.Log($"[KeyConfig] keyConfigList[{actualItemIndex}] = {keyCode}");
                        if (keyCode != KeyCode.None)
                            value = KeyCodeToString(keyCode);
                    }
                    catch (System.Exception ex)
                    {
                        DebugLogger.Log($"[KeyConfig] Error reading keyConfigList: {ex.Message}");
                    }
                }

                DebugLogger.Log($"[KeyConfig] RESULT: name=\"{name}\", value=\"{value}\"");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[KeyConfig] ERROR: {ex.Message}");
            }

            return (name, value);
        }

        private string KeyCodeToString(KeyCode code)
        {
            // Convert common key codes to readable names
            switch (code)
            {
                case KeyCode.None: return "";
                case KeyCode.Space: return "Space";
                case KeyCode.Backspace: return "Backspace";
                case KeyCode.Return: return "Enter";
                case KeyCode.Escape: return "Escape";
                case KeyCode.Tab: return "Tab";
                case KeyCode.LeftShift: return "Left Shift";
                case KeyCode.RightShift: return "Right Shift";
                case KeyCode.LeftControl: return "Left Ctrl";
                case KeyCode.RightControl: return "Right Ctrl";
                case KeyCode.LeftAlt: return "Left Alt";
                case KeyCode.RightAlt: return "Right Alt";
                case KeyCode.UpArrow: return "Up";
                case KeyCode.DownArrow: return "Down";
                case KeyCode.LeftArrow: return "Left";
                case KeyCode.RightArrow: return "Right";
                case KeyCode.Mouse0: return "Left Click";
                case KeyCode.Mouse1: return "Right Click";
                case KeyCode.Mouse2: return "Middle Click";
                default: return code.ToString();
            }
        }

        /// <summary>
        /// Announce current menu state.
        /// </summary>
        public void AnnounceStatus()
        {
            // Check agree window first
            if (_agreeWasOpen && _agreeWindow != null)
            {
                string header = GetAgreeHeaderText();
                string yesText = GetAgreeYesText();
                string noText = GetAgreeNoText();
                var cursor = _agreeWindow.m_cursorIndex;
                string currentOption = cursor == uAgreeWindow.CursorIndex.Yes ? yesText : noText;
                ScreenReader.Say($"{header}. {yesText} or {noText}. Currently on: {currentOption}");
                return;
            }

            if (!IsOpen())
                return;

            var state = _optionPanel.m_MainSettingState;
            string menuName = GetMenuName(state);
            var itemInfo = GetCurrentItemInfo();

            string announcement;
            if (itemInfo != null)
            {
                announcement = $"{menuName}. {itemInfo.Name}";
                if (!string.IsNullOrEmpty(itemInfo.Value))
                    announcement += $", {itemInfo.Value}";
                announcement += $", {itemInfo.Index} of {itemInfo.Total}";
            }
            else
            {
                announcement = menuName;
            }

            ScreenReader.Say(announcement);
        }

        private void UpdateAgreeWindow()
        {
            try
            {
                _agreeWindow = Object.FindObjectOfType<uAgreeWindow>();
                bool isOpen = _agreeWindow != null && _agreeWindow.IsOpen;

                if (isOpen && !_agreeWasOpen)
                    OnAgreeOpen();
                else if (!isOpen && _agreeWasOpen)
                    OnAgreeClose();
                else if (isOpen)
                    CheckAgreeCursorChange();

                _agreeWasOpen = isOpen;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OptionsMenu] Error tracking agree window: {ex.Message}");
            }
        }

        private void OnAgreeOpen()
        {
            _lastAgreeCursor = uAgreeWindow.CursorIndex.Yes;

            string header = GetAgreeHeaderText();
            string yesText = GetAgreeYesText();
            string noText = GetAgreeNoText();
            var cursor = _agreeWindow.m_cursorIndex;

            string currentOption = cursor == uAgreeWindow.CursorIndex.Yes ? yesText : noText;
            string announcement = !string.IsNullOrEmpty(header)
                ? $"{header}. {yesText} or {noText}. Currently on: {currentOption}"
                : $"Agreement. {yesText} or {noText}. Currently on: {currentOption}";

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[OptionsMenu] Agree window opened: {header}");
            _lastAgreeCursor = cursor;
        }

        private void OnAgreeClose()
        {
            _agreeWindow = null;
            DebugLogger.Log("[OptionsMenu] Agree window closed");
        }

        private void CheckAgreeCursorChange()
        {
            if (_agreeWindow == null)
                return;

            var cursor = _agreeWindow.m_cursorIndex;
            if (cursor != _lastAgreeCursor)
            {
                string text = cursor == uAgreeWindow.CursorIndex.Yes
                    ? GetAgreeYesText()
                    : GetAgreeNoText();

                ScreenReader.Say(text);
                DebugLogger.Log($"[OptionsMenu] Agree cursor: {text}");
                _lastAgreeCursor = cursor;
            }
        }

        private string GetAgreeHeaderText()
        {
            try
            {
                if (_agreeWindow == null)
                {
                    DebugLogger.Log("[OptionsMenu] Agree header: agreement window was null");
                    return "Agreement";
                }

                var header = _agreeWindow.m_Header;
                if (header == null)
                {
                    DebugLogger.Log("[OptionsMenu] Agree header: m_Header was null");
                }
                else
                {
                    var headerText = header.m_headerText;
                    if (headerText == null)
                    {
                        DebugLogger.Log("[OptionsMenu] Agree header: m_Header.m_headerText was null");
                    }
                    else
                    {
                        string text = TextUtilities.StripRichTextTags(headerText.text)?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            return text;

                        DebugLogger.Log("[OptionsMenu] Agree header: m_Header.m_headerText.text was empty");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OptionsMenu] Error reading agree header: {ex.Message}");
            }

            // Fallback based on window type
            try
            {
                var windowType = _agreeWindow.m_currentWindowType;
                switch (windowType)
                {
                    case uAgreeWindow.AgreeWindowType.Eula:
                        return "End User License Agreement";
                    case uAgreeWindow.AgreeWindowType.PP:
                        return "Privacy Policy";
                    case uAgreeWindow.AgreeWindowType.KPI:
                        return "Usage Data Agreement";
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OptionsMenu] Agree header fallback type read failed: {ex.Message}");
            }

            return "Agreement";
        }

        private string GetAgreeYesText()
        {
            try
            {
                if (_agreeWindow == null)
                {
                    DebugLogger.Log("[OptionsMenu] Agree Yes label: agreement window was null");
                    return "Yes";
                }

                var yesText = _agreeWindow.m_yes;
                if (yesText == null)
                {
                    DebugLogger.Log("[OptionsMenu] Agree Yes label: m_yes was null");
                    return "Yes";
                }

                string text = TextUtilities.StripRichTextTags(yesText.text)?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                DebugLogger.Log("[OptionsMenu] Agree Yes label: m_yes.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OptionsMenu] Agree Yes label read failed: {ex.Message}");
            }
            return "Yes";
        }

        private string GetAgreeNoText()
        {
            try
            {
                if (_agreeWindow == null)
                {
                    DebugLogger.Log("[OptionsMenu] Agree No label: agreement window was null");
                    return "No";
                }

                var noText = _agreeWindow.m_no;
                if (noText == null)
                {
                    DebugLogger.Log("[OptionsMenu] Agree No label: m_no was null");
                    return "No";
                }

                string text = TextUtilities.StripRichTextTags(noText.text)?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                DebugLogger.Log("[OptionsMenu] Agree No label: m_no.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[OptionsMenu] Agree No label read failed: {ex.Message}");
            }
            return "No";
        }

        private class ItemInfo
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public int Index { get; set; }
            public int Total { get; set; }
        }
    }
}
