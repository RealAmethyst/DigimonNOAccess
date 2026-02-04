using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for options/settings menus.
    /// </summary>
    public class OptionsMenuHandler
    {
        private uOptionPanel _optionPanel;
        private bool _wasActive = false;
        private int _lastCursor = -1;
        private uOptionPanel.MainSettingState _lastState = uOptionPanel.MainSettingState.TOP;
        private uOptionPanel.State _lastPanelState = uOptionPanel.State.NONE;
        private string _lastValue = "";

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
        /// </summary>
        public bool IsOpen()
        {
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
            Melon<Main>.Logger.Msg($"[OptionsMenu] Opened: {menuName}");

            _lastState = state;
            _lastCursor = itemInfo?.Index ?? -1;
        }

        private void OnClose()
        {
            _optionPanel = null;
            _lastCursor = -1;
            _lastValue = "";
            Melon<Main>.Logger.Msg("[OptionsMenu] Closed");
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
                Melon<Main>.Logger.Msg($"[OptionsMenu] State changed to: {menuName}");

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
                Melon<Main>.Logger.Msg($"[OptionsMenu] Cursor: {itemInfo.Name} = {itemInfo.Value}");
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
                Melon<Main>.Logger.Msg($"[OptionsMenu] Value changed: {itemInfo.Value}");
                _lastValue = itemInfo.Value;
            }
        }

        private string GetMenuName(uOptionPanel.MainSettingState state)
        {
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
                        if (topPanel.m_items != null)
                            totalItems = topPanel.m_items.Count;
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
                    itemName = $"Item {dataIndex + 1}";

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
                Melon<Main>.Logger.Warning($"[OptionsMenu] Error getting item info: {ex.Message}");
                return null;
            }
        }

        private (string name, string value) GetTopPanelItem(uOptionTopPanelCommand panel, int dataIndex)
        {
            string name = "";
            string value = "";

            try
            {
                // TOP panel: m_CommandInfoArray maps directly to m_items (no scrolling)
                // Read from m_CommandInfoArray for displayed text
                var commandInfoArray = panel.m_CommandInfoArray;
                if (commandInfoArray != null && dataIndex >= 0 && dataIndex < commandInfoArray.Length)
                {
                    var info = commandInfoArray[dataIndex];
                    if (info != null)
                    {
                        if (info.m_CommandName != null && !string.IsNullOrEmpty(info.m_CommandName.text))
                            name = info.m_CommandName.text;
                        if (info.m_CommandNum != null && !string.IsNullOrEmpty(info.m_CommandNum.text))
                            value = info.m_CommandNum.text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Melon<Main>.Logger.Warning($"[OptionsMenu] Error reading top panel: {ex.Message}");
            }

            return (name, value);
        }

        private (string name, string value) GetSettingsPanelItem(uOptionPanelCommand panel, int dataIndex)
        {
            string name = "";
            string value = "";

            try
            {
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
                            name = "Voice Language";
                            if (voiceLangItem.m_languageType != null)
                            {
                                value = voiceLangItem.m_languageType.text ?? "";
                            }
                            DebugLogger.Log($"[Settings] VoiceLanguage item: value=\"{value}\"");
                            return (name, value);
                        }

                        var bgmItem = item.TryCast<uOptionPanelItemBgmVolume>();
                        if (bgmItem != null)
                        {
                            name = "Music Volume";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (sliderItem?.m_sliderNum != null)
                                value = sliderItem.m_sliderNum.text ?? "";
                            DebugLogger.Log($"[Settings] BgmVolume item: value=\"{value}\"");
                            return (name, value);
                        }

                        var voiceVolItem = item.TryCast<uOptionPanelItemVoiceVolume>();
                        if (voiceVolItem != null)
                        {
                            name = "Voice Volume";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (sliderItem?.m_sliderNum != null)
                                value = sliderItem.m_sliderNum.text ?? "";
                            DebugLogger.Log($"[Settings] VoiceVolume item: value=\"{value}\"");
                            return (name, value);
                        }

                        var seVolItem = item.TryCast<uOptionPanelItemSeVolume>();
                        if (seVolItem != null)
                        {
                            name = "SFX Volume";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (sliderItem?.m_sliderNum != null)
                                value = sliderItem.m_sliderNum.text ?? "";
                            DebugLogger.Log($"[Settings] SeVolume item: value=\"{value}\"");
                            return (name, value);
                        }

                        var cameraVItem = item.TryCast<uOptionPanelItemCameraV>();
                        if (cameraVItem != null)
                        {
                            name = "Camera Up/Down";
                            // Value comes from toggle state: Normal or Reverse
                            value = item.Value == 0 ? "Normal" : "Reverse";
                            DebugLogger.Log($"[Settings] CameraV item: value=\"{value}\"");
                            return (name, value);
                        }

                        var cameraHItem = item.TryCast<uOptionPanelItemCameraH>();
                        if (cameraHItem != null)
                        {
                            name = "Camera L/R";
                            value = item.Value == 0 ? "Normal" : "Reverse";
                            DebugLogger.Log($"[Settings] CameraH item: value=\"{value}\"");
                            return (name, value);
                        }

                        var sensItem = item.TryCast<uOptionPanelItemSensitivity>();
                        if (sensItem != null)
                        {
                            name = "Camera Sensitivity";
                            var sliderItem = item.TryCast<uOptionPanelItemSlider>();
                            if (sliderItem?.m_sliderNum != null)
                                value = sliderItem.m_sliderNum.text ?? "";
                            DebugLogger.Log($"[Settings] Sensitivity item: value=\"{value}\"");
                            return (name, value);
                        }

                        var voidItem = item.TryCast<uOptionPanelItemVoid>();
                        if (voidItem != null)
                        {
                            name = "Back";
                            DebugLogger.Log($"[Settings] Void (Back) item");
                            return (name, value);
                        }
                    }
                }

                // Fallback: read from m_CommandInfoArray (visible UI slots)
                // This is used if we couldn't identify the specific item type
                DebugLogger.Log($"[Settings] Using CommandInfoArray fallback");

                if (commandInfoArray != null && visibleSlotCount > 0)
                {
                    // Calculate which visible slot shows the current item
                    int scrollOffset = System.Math.Max(0, dataIndex - (visibleSlotCount - 1));
                    int visibleSlot = dataIndex - scrollOffset;

                    DebugLogger.Log($"[Settings] scrollOffset={scrollOffset}, visibleSlot={visibleSlot}");

                    if (visibleSlot >= 0 && visibleSlot < visibleSlotCount)
                    {
                        var cmdInfo = commandInfoArray[visibleSlot];
                        DebugLogger.Log($"[Settings] cmdInfo[{visibleSlot}] null? {cmdInfo == null}");

                        if (cmdInfo != null)
                        {
                            if (cmdInfo.m_CommandName != null)
                            {
                                name = cmdInfo.m_CommandName.text ?? "";
                                DebugLogger.Log($"[Settings] m_CommandName.text = \"{name}\"");
                            }

                            if (cmdInfo.m_CommandNum != null)
                            {
                                value = cmdInfo.m_CommandNum.text ?? "";
                                DebugLogger.Log($"[Settings] m_CommandNum.text = \"{value}\"");
                            }
                        }
                    }
                }

                DebugLogger.Log($"[Settings] RESULT: name=\"{name}\", value=\"{value}\"");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[Settings] ERROR: {ex.Message}");
                Melon<Main>.Logger.Warning($"[OptionsMenu] Error reading settings panel: {ex.Message}");
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
                Melon<Main>.Logger.Warning($"[OptionsMenu] Error reading graphics panel: {ex.Message}");
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

        private class ItemInfo
        {
            public string Name { get; set; }
            public string Value { get; set; }
            public int Index { get; set; }
            public int Total { get; set; }
        }
    }
}
