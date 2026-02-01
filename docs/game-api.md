# Digimon World Next Order - Game API Reference

## Overview
- **Game:** Digimon World Next Order
- **Engine:** Unity 2019.4.11f1 (Il2Cpp)
- **Main Namespace:** Il2Cpp

## Title Menu (uTitlePanel)

**Class:** `uTitlePanel` (extends `uPanelBase2`)

**Key Properties:**
- `cursorPosition` (int) - Current selection (0-3)
- `m_Text` (Text[]) - Array of menu item Text components

**SelectItem Enum:**
- 0 = Start (New Game)
- 1 = Continue (Load Game)
- 2 = Option (System Settings)
- 3 = Quit

**Methods:**
- `enablePanel(bool enable, bool finished_disable)` - Show/hide panel

**Detection:** Check if `uTitlePanel` instance exists and gameObject is active

## Options Panel (uOptionPanel)

**Class:** `uOptionPanel` (extends `MonoBehaviour`)

**Key Properties:**
- `m_State` (State enum) - Panel state
- `m_MainSettingState` (MainSettingState enum) - Current settings section
- `m_IsTitle` (bool) - True when opened from title screen
- `m_uOptionPanelHeadLine` - Headline component
- `m_uOptionPanelCommand` (uOptionPanelCommandBase[]) - Sub-panels array

**State Enum:**
- NONE, MAIN_SETTING, CLOSE, SAVE, APPLICATION_QUIT, AGREE, AGREE_POST, AGREE_POST_WAIT, END

**MainSettingState Enum:**
- TOP = 0 (System menu)
- OPTION = 1 (System Settings)
- GRAPHICS = 2
- KEYCONFIG = 3
- APPLICATION_QUIT = 4
- AGREE = 5

**Methods:**
- `Open(string lang_code, Action finish)` - Open options panel
- `Close(Action finish)` - Close options panel
- `enablePanel(bool isEnable)` - Enable/disable panel

## Options Headline (uOptionPanelHeadLine)

**Class:** `uOptionPanelHeadLine` (extends `uPanelBase`)

**Key Properties:**
- `m_HeadLine` (Text) - The displayed headline text

## Options Command Base (uOptionPanelCommandBase)

**Class:** `uOptionPanelCommandBase` (extends `uPanelBase`)

**Key Properties:**
- `m_KeyCursorController` (KeyCursorController) - Cursor controller
- `m_State` (PanelState enum)

**PanelState Enum:**
- NONE, MAIN, CLOSE, HYDE

## Cursor Controller (KeyCursorController)

**Class:** `KeyCursorController`

**Key Properties:**
- `m_DataIndex` (int) - Current cursor position (0-based)
- `m_DataMax` (int) - Total number of items
- `m_IsMove` (bool) - True when cursor just moved

**Methods:**
- `GetDataIndex()` - Get current index
- `IsMove()` - Check if cursor moved this frame

## Base Classes

### uPanelBase2 (extends UiDispBase)
- `enablePanel(bool enable, bool finished_disable)` - Show/hide
- `Update()` - Frame update

### uPanelBase
- `enablePanel(bool enable, bool finished_disable)` - Show/hide
- `disablePanel()` - Hide panel

## Game Key Bindings

From Key Config menu:
- A Button: Space (Confirm)
- B Button: Backspace (Cancel/Back)
- X Button: C
- Y Button: V
- L Button: Q
- R Button: E
- Arrow keys: Navigation

### Safe Keys for Mod
Keys NOT used by game, safe for mod functions:
- F1-F12 (function keys)
- Ctrl+key combinations
- Alt+key combinations

### Mod Key Bindings (Currently Used)
- **F1** - Repeat last announcement
- **F2** - Announce current menu/status
- **F3** - Partner 1 full status (field only)
- **F4** - Partner 2 full status (field only)

### Controller Combos for Partner Status (Field Only)
Hold RB (R1) + face button for Partner 1 info:
- **A/Cross** - HP and MP
- **B/Circle** - Status effects (Injury, Disease, etc.)
- **X/Square** - Mood/condition
- **Y/Triangle** - Name and basic info

Hold LB (L1) + face button for Partner 2 info (same layout)

### Always-On Audio Systems (No Keys Required)
- **Positional Audio Tracking** - Automatically tracks nearest object with 3D audio when player is in control
- **Wall Detection** - Automatically plays directional sounds when walls are nearby
- Both systems pause during battles, cutscenes, events, and menus

## Options Top Panel Command (uOptionTopPanelCommand)

**Class:** `uOptionTopPanelCommand` (extends `uOptionPanelCommandBase`)

**Key Properties:**
- `m_CommandInfoArray` (CommandInfo[]) - Array of visible UI slots
- `m_items` (List<uOptionPanelItemBase>) - All data items (use for total count)
- `m_KeyCursorController` (from base) - Cursor position

**CommandInfo Structure:**
- `m_CommandName` (Text) - Setting name display
- `m_CommandNum` (Text) - Setting value display
- `m_Slider` (Slider) - For slider-type settings

## Options Panel Command (uOptionPanelCommand)

**Class:** `uOptionPanelCommand` (extends `uOptionPanelCommandBase`)

Same structure as uOptionTopPanelCommand with:
- `m_CommandInfoArray` (CommandInfo[])
- `m_items` (List<uOptionPanelItemBase>)

## Options Graphics Panel Command (uOptionGraphicsPanelCommand)

**Class:** `uOptionGraphicsPanelCommand` (extends `uOptionPanelCommandBase`)

**Key Properties:**
- `m_items` (List<uOptionPanelItemBase>) - All items (NO m_CommandInfoArray)

**Note:** Does NOT have m_CommandInfoArray, read directly from m_items.

## Options Key Config Panel Command (uOptionKeyConfigPanelCommand)

**Class:** `uOptionKeyConfigPanelCommand` (extends `uOptionPanelCommandBase`)

**Key Properties:**
- `m_items` (List<uOptionKeyconfigPanelItem>) - 6 visible slot items
- `m_itemTypeList` (List<string>) - All 20 action names
- `m_keyConfigList` (short[]) - All 20 key bindings as KeyCode values
- `m_scrollItemPos` (int) - Current scroll position (0-14)
- `m_scrollMaxValue` (int) - Maximum scroll value (14)

**Scrolling:** This panel has EXPLICIT scroll tracking unlike System Settings.
- Actual item index = `m_scrollItemPos + m_DataIndex`
- Total items = `m_itemTypeList.Count` (20), NOT `m_DataMax` (6)

**Reading current item:**
```csharp
int scrollPos = panel.m_scrollItemPos;
int actualIndex = scrollPos + cursor.m_DataIndex;

// Read from visible slot (updates automatically when scrolled)
var visibleItem = panel.m_items[cursor.m_DataIndex];
string name = visibleItem.m_HeadText.text;
KeyCode key = visibleItem.m_keyCode;

// Or read from full lists
string name = panel.m_itemTypeList._items[actualIndex];
KeyCode key = (KeyCode)panel.m_keyConfigList[actualIndex];
```

## Options Keyconfig Panel Item (uOptionKeyconfigPanelItem)

**Class:** `uOptionKeyconfigPanelItem` (extends `MonoBehaviour`)

**Key Properties:**
- `m_HeadText` (Text) - Action name display
- `m_keyCode` (KeyCode) - Current key binding

## Options Panel Item Base (uOptionPanelItemBase)

**Class:** `uOptionPanelItemBase` (extends `MonoBehaviour`)

**Key Properties:**
- `Value` (int) - Current setting value
- `m_IndexId` (int) - Item index
- `m_caption` (uOptionPanelCaption) - Caption component
- `captionKind` (CaptionKind) - Type of caption display

## System Settings Item Types

**IMPORTANT:** Each item type has its own text fields. Use `TryCast<T>()` to detect type, then read from type-specific fields.

### uOptionPanelItemSlider (base for volume/sensitivity)
- `m_sliderObject` (Slider) - The slider component
- `m_sliderNum` (Text) - Displays current value

### uOptionPanelItemBgmVolume (Music Volume)
- Extends `uOptionPanelItemSlider`
- Name: "Music Volume"
- Value: `m_sliderNum.text`

### uOptionPanelItemVoiceVolume (Voice Volume)
- Extends `uOptionPanelItemSlider`
- Name: "Voice Volume"
- Value: `m_sliderNum.text`

### uOptionPanelItemSeVolume (SFX Volume)
- Extends `uOptionPanelItemSlider`
- Name: "SFX Volume"
- Value: `m_sliderNum.text`

### uOptionPanelItemSensitivity (Camera Sensitivity)
- Extends `uOptionPanelItemSlider`
- Name: "Camera Sensitivity"
- Value: `m_sliderNum.text`

### uOptionPanelItemToggle (base for toggles)
- `OnToggle(bool toggle)` - Called when toggled

### uOptionPanelItemCameraV (Camera Up/Down)
- Extends `uOptionPanelItemToggle`
- Name: "Camera Up/Down"
- Value: `Value == 0 ? "Normal" : "Reverse"`

### uOptionPanelItemCameraH (Camera Left/Right)
- Extends `uOptionPanelItemToggle`
- Name: "Camera L/R"
- Value: `Value == 0 ? "Normal" : "Reverse"`

### uOptionPanelItemVoiceLanguage (Voice Language)
- Extends `uOptionPanelItemToggle`
- `m_languageType` (Text) - Displays current language (e.g., "Japanese", "English")
- `m_voiceLanguageKind` (CriSoundManager.VoiceLanguageKind) - Current language enum
- Name: "Voice Language"
- Value: `m_languageType.text`

### uOptionPanelItemVoid (Back button)
- Empty item, typically used for Back/navigation
- Name: "Back"

## Options Panel Caption (uOptionPanelCaption)

**Class:** `uOptionPanelCaption` (extends `uPanelBase`)

**Key Properties:**
- `m_Caption` (Text) - The actual text component

**To get caption text:** `item.m_caption.m_Caption.text`

## Finding Panels at Runtime

```csharp
// Find uTitlePanel
var titlePanel = UnityEngine.Object.FindObjectOfType<uTitlePanel>();

// Find uOptionPanel
var optionPanel = UnityEngine.Object.FindObjectOfType<uOptionPanel>();

// Check if active
bool isActive = panel != null && panel.gameObject.activeInHierarchy;

// Get current command panel from options
var commandPanels = optionPanel.m_uOptionPanelCommand;
int panelIndex = (int)optionPanel.m_MainSettingState;
var currentPanel = commandPanels[panelIndex];

// Get cursor position from command panel
var cursor = currentPanel.m_KeyCursorController;
int position = cursor.m_DataIndex;
int total = cursor.m_DataMax;
```

## Difficulty Selection (New Game)

**Class:** `uDifficultyDialog` (extends `uPanelBase2`)

**Key Properties:**
- `CursorPosition` (int) - Current selection (0-based)
- `m_State` (State enum) - Panel state
- `m_difficlutItems` (DifficultItem) - Contains difficulty text array
- `m_type` (DifficultyType) - Number of choices available

**State Enum:**
- None = 0
- Main = 1 (actively selecting)
- End = 2

**DifficultItem Structure:**
- `m_difficultText` (Text[]) - Array of difficulty option labels

**Detection:** `FindObjectOfType<uDifficultyDialog>()`, check `m_State == State.Main`

## Character Selection (New Game)

**Class:** `uCharaSelectPanel` (extends `uPanelBase2`)

**Key Properties:**
- `Gender` (int) - Current selection (0=Male, 1=Female)
- `m_captionText` (Text) - Caption/prompt text (NOTE: Japanese even in English version)

**Detection:** `FindObjectOfType<uCharaSelectPanel>()`, check `gameObject.activeInHierarchy`

## Digi-Egg Selection (uRebirthPanel)

**Class:** `uRebirthPanel` (extends `MonoBehaviour`)

Used for: Selecting Digi-Eggs during new game or rebirth.

**Key Properties:**
- `selectEgg` (int) - Currently selected egg index (0-based)
- `eggMax` (int) - Total number of available eggs
- `m_text` (Text) - Jijimon's actual comment about the current egg
- `m_simple_genealogy` (uSimpleGenealogy) - Simple evolution icons display
- `m_genealogy` (uGenealogy) - Detailed evolution panel (opened with confirm)
- `m_eggTbl` (int[]) - Table of egg IDs
- `m_state` (int) - Internal panel state

**Label Fields (NOT data - these show static labels):**
- `m_headLineText` (Text) - Shows "L Partner" or "R Partner" label
- `m_natureText` (Text) - Shows "Nature" label
- `m_attrText` (Text) - Shows "Attr" label
- `m_jijimonText` (Text) - Shows "Jijimon" label

**Key Methods:**
- `IsOpened()` (bool) - Check if panel is open and active
- `GetEggNo(int no)` (int) - Get egg ID from index
- `SelectEgg()` - Handle egg selection
- `SetDialog()` - Opens the Yes/No confirmation dialog
- `DialogCallBack(bool is_yes)` - Callback from confirmation dialog
- `InitializeDiagram(int no, bool simple)` - Initialize evolution diagram

**Detection:** `FindObjectOfType<uRebirthPanel>()`, use `IsOpened()` method

**Example:**
```csharp
var panel = FindObjectOfType<uRebirthPanel>();
if (panel != null && panel.IsOpened())
{
    int currentEgg = panel.selectEgg;
    int totalEggs = panel.eggMax;
    // Get actual Jijimon comment from m_text, NOT m_jijimonText (which is a label)
    string jijimonComment = panel.m_text?.text ?? "";
}
```

## Digivolution Details (uGenealogy)

**Class:** `uGenealogy` (extends `uPanelBase`)

Used for: Displaying detailed Digimon evolution information when viewing egg details.

**Key Properties:**
- `m_name_text` (Text) - Current Digimon name
- `m_nature_text` (Text) - Nature type (e.g., "Brainy", "Fighter")
- `m_attr_text` (Text) - Attribute (e.g., "Data", "Vaccine", "Virus")
- `m_detail_text` (Text) - Digimon description/lore
- `m_selectGlowth` (int) - Currently selected growth stage
- `m_state` (State enum) - Current state

**State Enum:**
- Main = 0 (normal viewing)
- ScrollAfter = 1 (scrolling animation)
- ScrollBefore = 2 (scrolling animation)

**Detection:** `FindObjectOfType<uGenealogy>()`, check `gameObject.activeInHierarchy`

**Example:**
```csharp
var panel = FindObjectOfType<uGenealogy>();
if (panel != null && panel.gameObject.activeInHierarchy)
{
    string name = panel.m_name_text?.text ?? "";
    string nature = panel.m_nature_text?.text ?? "";
    string attr = panel.m_attr_text?.text ?? "";
    string detail = panel.m_detail_text?.text ?? "";
}
```

## Simple Genealogy (uSimpleGenealogy)

**Class:** `uSimpleGenealogy` (extends `uPanelBase`)

Used for: Showing simplified evolution icons on the egg selection screen.

**Key Properties:**
- `m_genealogy_icons` (uGenealogyIcon) - Icon container
- `m_images` (Image[]) - Evolution stage icons

**Note:** This panel shows ICONS only, no text. For actual names/info, use `uGenealogy`.

## Common Yes/No Dialog (uCommonYesNoWindow)

**Class:** `uCommonYesNoWindow` (extends `uCommonWindowBase`)

Used for: Confirmation dialogs throughout the game (egg selection, quit confirmation, etc.)

**Key Properties:**
- `m_message` (Text) - The confirmation question/prompt
- `m_yes` (Text) - Yes button text
- `m_no` (Text) - No button text
- `m_cursorIndex` (CursorIndex enum) - Current selection
- `m_yesBase` (RectTransform) - Yes button container
- `m_noBase` (RectTransform) - No button container
- `m_cursor` (RectTransform) - Visual cursor
- `DEFAULT_CURSOR` (CursorIndex) - Default cursor position when opening

**CursorIndex Enum:**
- Yes = 0
- No = 1

**Key Methods:**
- `Open(string message, Action<bool> callback)` - Open the dialog with a message
- `Close()` - Close the dialog
- `SetCursorIndex(int index)` - Programmatically set cursor position

**Detection:** `FindObjectOfType<uCommonYesNoWindow>()`, check `gameObject.activeInHierarchy`

**Example:**
```csharp
var dialog = FindObjectOfType<uCommonYesNoWindow>();
if (dialog != null && dialog.gameObject.activeInHierarchy)
{
    string message = dialog.m_message?.text ?? "";
    string yesText = dialog.m_yes?.text ?? "Yes";
    string noText = dialog.m_no?.text ?? "No";
    var cursor = dialog.m_cursorIndex; // Yes or No
}
```

## DigitamaScene (Egg Scene Controller)

**Class:** `DigitamaScene` (extends `MonoBehaviour`)

Used for: Managing the overall Digi-Egg selection scene flow.

**SceneNo Enum:**
- Non = 0
- DeathProduction = 1
- EggLoadWait = 2
- DigitamaSelect = 3 (actively selecting egg)
- BornProduction = 4
- NameEntry = 5
- StartEvolution = 6
- DigitamaDecision = 7
- LoadWait = 8

**Key Properties:**
- `m_sceneNo` (SceneNo) - Current scene state
- `m_lookEggNo` (int) - Currently viewed egg number

## Name Entry System (New Game)

**Class:** `NameEntry` (extends `MonoBehaviour`)

**Key Properties:**
- `m_state` (eState enum) - Current state
- `Type` (eType) - Player or Digitama
- `Name` (string) - The entered name
- `Title` (string) - Prompt title
- `m_uNameInput` (uNameInput) - The UI panel

**eType Enum:**
- Player = 0
- Digitama = 1

**eState Enum:**
- NONE = 0
- INIT = 1
- REQUEST = 2
- INPUT = 3 (actively typing)
- INPUT_END = 4
- STEAM_TEXTINPUT = 5
- STEAM_TEXTINPUT_END = 6

**Detection:** `FindObjectOfType<NameEntry>()`, check `m_state != NONE`

## Name Input UI (uNameInput)

**Class:** `uNameInput` (extends `uCommonWindowBase`)

**Key Properties:**
- `m_state` (State enum) - none, MAIN
- `m_label` (Text) - The prompt label text
- `m_InputField` (InputField) - The text input field
- `m_digimonName` (string) - Digimon name if applicable

## Dialog System (uDialogBase)

**Class:** `uDialogBase` (extends `uPanelBase`)

**Key Properties:**
- `m_cursorIndex` (CursorIndex enum) - Current selection
- `m_callback` (Action<bool>) - Callback when dialog closes

**CursorIndex Enum:**
- Yes = 0
- No = 1

**Methods:**
- `OpenDialog(string title, string message, Action<bool> callback)` - Open dialog

**Detection:** `FindObjectOfType<uDialogBase>()`, check `activeInHierarchy`

## Message Window System (uCommonMessageWindow)

**Class:** `uCommonMessageWindow` (extends `uCommonWindowBase`)

**Key Properties:**
- `m_label` (Text) - The message text
- `m_digimonName` (string) - Speaker name if applicable
- `m_partnerNo` (PARTNER_NO) - Which partner (if any)
- `m_nextButton` (uPanelBase2) - Next/continue button
- `m_isHeadsUp` (bool) - Whether in heads-up mode

**Type Enum (Window Position):**
- Center = 0 / PartnerR = 0
- PartnerL = 1
- RightUp = 2

**Manager Class:** `CommonMessageWindowManager`
- `GetCenter()` - Get center window
- `Get00()` / `Get01()` - Get partner windows
- `GetRightUp()` - Get right-up window
- `IsFindActive()` - Check if any window active

**Detection:** `FindObjectsOfType<uCommonMessageWindow>()`, check `activeInHierarchy`

## Digimon Message Panel (uDigimonMessagePanel)

**Class:** `uDigimonMessagePanel` (extends `uPanelBaseAutoClose2`)

Used for: Partner Digimon status messages (hunger, fatigue, etc.)

**Key Properties:**
- `m_text` (Text) - The message text
- `m_windowType` (MessageWindowType enum) - Field or Battle
- `m_winRect` (RectTransform) - Window rectangle
- `m_margin` (float) - Text margin

**MessageWindowType Enum:**
- None = -1
- Field = 0
- Battle = 1

**Methods:**
- `StartMessage(string message, float time)` - Display a timed message
- `ClosePanel()` - Close the message panel

**Detection:** `FindObjectsOfType<uDigimonMessagePanel>()`, check `activeInHierarchy`

## TypewriterEffect (Text Animation)

**Class:** `TypewriterEffect` (extends `MonoBehaviour`)

Used for: Animating text display character-by-character in dialog windows.

**Static Properties:**
- `current` (TypewriterEffect) - Currently active typewriter instance (null if none)

**Key Properties:**
- `isActive` (bool) - True while typing animation is in progress, false when complete
- `mFullText` (string) - The COMPLETE text to be displayed (available even during animation!)
- `mCurrentOffset` (int) - Current character position in the typing animation
- `mActive` (bool) - Internal active flag
- `charsPerSecond` (int) - Typing speed

**Key Methods:**
- `Finish()` - Immediately completes the typing animation
- `ResetToBeginning()` - Restarts the animation from the beginning

**Detection Pattern:**
```csharp
// Get complete text even during typing animation
var typewriter = TypewriterEffect.current;
if (typewriter != null)
{
    string fullText = typewriter.mFullText;  // Complete text, not partial!
    bool isTyping = typewriter.isActive;     // True while animating

    // Wait for typing to finish before announcing
    if (!isTyping)
    {
        // Text is complete, safe to announce
    }
}
```

## Caption System (Field Hints)

**Class:** `uCaptionBase` (extends `uPanelBase`)

Used for: Field hints, tutorial prompts, and instruction text.

**Key Properties:**
- `m_text` (Text) - The caption/hint text component

**Key Methods:**
- `SetCaptionNo(string code)` - Set caption by localization code
- `SetCaptionNoWithButtonIcon(string code)` - Set caption with button icons

**Detection:** `FindObjectsOfType<uCaptionBase>()`, check `activeInHierarchy` and `m_text.text`

**Derived Classes:**
- `uCarePanelCaption`, `uEducationPanelCaption`, `uShopPanelCaption`, etc.

## Tutorial System

**Class:** `TutorialMain` (extends `TalkMain`)

**Key Methods:**
- `Initialize()` - Setup tutorial
- `InitializeCommands()` - Setup commands
- `Rewind()` - Reset tutorial
- `IsSkip()` - Check if skippable

## Battle System (Overview)

**Classes found:**
- `MainGameBattle` - Main battle controller
- `uBattlePanel` - Battle UI base
- `uBattlePanelStart` - Battle start UI
- `uBattleDescription` - Action descriptions
- `uBattleTextPop` - Floating damage/status text
- `uBattlePanelCommand` - Command selection

## Event/Story System

**MainGameEvent** - Event execution handler
- `SetEvent()` - Start an event
- `_EventBattleStart()` - Trigger battle from event

**TalkMain** - Dialog/story sequence base class
- `m_common_message_window` (uCommonMessageWindow) - Active message window
- `m_ui_root` (EventWindowPanel[]) - Array of story dialog panels
- `m_cursor` (int) - Current dialog choice cursor position (0, 1, or 2)
- `m_maxChoiceNum` (int) - Number of active choices (0 = no choices active)
- `PlayVoiceText(name, text, id, a3, a4, a5)` - Called when dialog has voice audio
- `DispText(a0, a1, a2, a3, a4, a5)` - Display text without voice
- `DispTextMain(name, text)` - Receives LOCALIZATION KEY (not actual text!)
- `TalkWindowDisp()` - Show/hide talk window
- `CommonMessageWindow()` - Show common message window

**EventWindowPanel** - Story dialog display panel
- `m_normalText` (Text) - The displayed text
- `m_nameText` (Text) - Speaker name text
- `m_choicesText` (Text[]) - Array of dialog choice Text components
- `m_cursor` (UiDispBase) - Visual cursor (DON'T use for position tracking!)
- `TextShrink(string text)` - Receives ACTUAL LOCALIZED TEXT ready for display
- `SetCursorPosition(int choice)` - Set visual cursor to a choice

## Dialog Choices System

**Tracking dialog choices when multiple options are presented:**

**Key Discovery:** Use `TalkMain.m_cursor` directly for cursor position, NOT visual position.

**Detection Pattern:**
```csharp
var talkMain = FindObjectOfType<TalkMain>();
if (talkMain != null && talkMain.m_maxChoiceNum > 0)
{
    // Choices are active
    int cursorPos = talkMain.m_cursor;  // 0, 1, or 2
    int maxChoices = talkMain.m_maxChoiceNum;  // Usually 2 or 3
}
```

**Reading choice text:**
```csharp
var panels = FindObjectsOfType<EventWindowPanel>();
foreach (var panel in panels)
{
    if (panel.m_choicesText != null && panel.gameObject.activeInHierarchy)
    {
        for (int i = 0; i < panel.m_choicesText.Length; i++)
        {
            var choiceText = panel.m_choicesText[i];
            if (choiceText != null && choiceText.gameObject.activeInHierarchy)
            {
                string text = choiceText.text;
                // This is an active choice
            }
        }
    }
}
```

**WARNING:** Do NOT calculate cursor position from visual cursor Y coordinate. The `EventWindowPanel.m_cursor` (UiDispBase) position jumps unpredictably between values like 0, 3, 5. Always use `TalkMain.m_cursor` which gives accurate 0/1/2 values.

**Harmony Patch Approach for Immediate Text (RECOMMENDED):**
```csharp
// Patch EventWindowPanel.TextShrink - this receives actual localized text!
var method = AccessTools.Method(typeof(EventWindowPanel), "TextShrink",
    new Type[] { typeof(string) });
harmony.Patch(method, prefix: new HarmonyMethod(typeof(MyPatch), "TextShrinkPrefix"));

// Prefix receives the actual text that will be displayed
private static void TextShrinkPrefix(EventWindowPanel __instance, string text)
{
    // 'text' is the actual localized text like "(Where am I?)"
    // Read speaker name from panel
    string speaker = __instance.m_nameText?.text ?? "";
    // Announce immediately!
}
```

**Voice Detection Pattern:**
```csharp
// Patch TalkMain.PlayVoiceText to detect voiced dialog
var voiceMethod = AccessTools.Method(typeof(TalkMain), "PlayVoiceText",
    new Type[] { typeof(string), typeof(string), typeof(string),
                 typeof(string), typeof(string), typeof(string) });
harmony.Patch(voiceMethod, prefix: new HarmonyMethod(typeof(MyPatch), "VoicePrefix"));

private static DateTime _lastVoiceTime = DateTime.MinValue;

private static void VoicePrefix(string name, string text, string id)
{
    _lastVoiceTime = DateTime.Now;
}

// In TextShrinkPrefix, check if voice was triggered recently
private static void TextShrinkPrefix(EventWindowPanel __instance, string text)
{
    // If voice triggered within 500ms, skip TTS (game will play voice audio)
    if ((DateTime.Now - _lastVoiceTime).TotalMilliseconds < 500)
        return;
    // Otherwise, announce via TTS
}
```

**OLD APPROACH (DON'T USE - receives keys, not text):**
```csharp
// TalkMain.DispTextMain receives LOCALIZATION KEYS like "EV_MIN_0000_000"
// NOT the actual text! Use EventWindowPanel.TextShrink instead.
```

**Localization System:**
- `Localization.isActive` - Check if localization is loaded
- `Localization.Exists(key)` - Check if a key exists
- `Localization.Get(key, warnIfMissing)` - Get localized text from key
- `Localization.Localize(key)` - Alternative method to get localized text
- Keys follow pattern: `EV_MIN_XXXX_YYYY` for story events

**SceneMain** - Cutscene handler

**MovieSubtitle** - Movie subtitle display
- `m_start_frame` / `m_end_frame` - Subtitle timing

## Positional Audio System (NAudio) - Always-On

The mod uses NAudio for 3D positional audio, bypassing the game's CRI Atom system (which has 2D-baked sounds). The system is **always active** when the player is in control - no toggle keys required.

### Why NAudio Instead of Game Audio
- **CRI Atom Limitation:** The game's common SE cue sheet sounds are authored as 2D in CRI Atom at build time. The `use3D` parameter only affects position updates, not actual 3D playback.
- **Unity AudioSource Limitation:** Il2Cpp games don't support standard Unity AudioSource manipulation from managed code.
- **NAudio Solution:** Provides Windows-level audio playback completely independent of game systems.

### PositionalAudio Class

**File:** `PositionalAudio.cs`

**Key Methods:**
- `StartTracking(SoundType, maxDistance)` - Begin tracking with specified sound type
- `Stop()` - Stop tracking audio
- `ChangeSoundType(SoundType, maxDistance)` - Switch to different sound while playing
- `UpdateTargetPosition(x, y, z)` - Update target world position
- `UpdatePlayerPosition(x, y, z, forwardX, forwardZ)` - Update player position and facing direction
- `GetCurrentDistance()` - Get current distance to target
- `IsPlaying` - Check if currently playing

**SoundType Enum:**
- `Item` - Items (loads item.wav)
- `NPC` - NPCs (loads potential npc.wav)
- `Enemy` - Enemy Digimon (loads potential enemie digimon.wav)
- `Transition` - Area transitions (loads transission.wav)

**Audio Chain:**
```
AudioFileReader/SignalGenerator (mono) → PanningSampleProvider (stereo) → VolumeSampleProvider → WaveOutEvent
```

**Position Calculation:**
```csharp
// Direction to target (normalized)
float dx = (targetX - playerX) / distance;
float dz = (targetZ - playerZ) / distance;

// Cross product for left/right panning
float pan = forwardZ * dx - forwardX * dz;  // -1 (left) to +1 (right)

// Dot product for front/back detection
float dot = forwardX * dx + forwardZ * dz;  // positive = front, negative = back
```

**Features:**
- Loads custom WAV files from sounds/ folder (falls back to generated tones)
- Stereo panning based on target angle relative to player facing
- Volume scales with distance (louder when closer)
- Pitch increases as player approaches (proximity cue, generated tones only)
- Background thread updates at 60fps for smooth audio
- Auto-switches to nearest target every 0.5 seconds
- Auto-stops when player reaches target (within 3 meters)

### AudioNavigationHandler - Always-On Mode

**File:** `AudioNavigationHandler.cs`

**Behavior:**
- Automatically initializes and starts tracking on first Update
- Continuously scans for nearest target every 0.5 seconds
- Automatically switches between targets as player moves
- Pauses during battles, cutscenes, events (ActionState_Event, ActionState_Battle, etc.)
- No toggle keys - always active when player is in control

**Object Detection Priority:**
1. Items (`ItemPickPointManager.m_itemPickPoints`) - range 100m
2. Transitions (`MapTriggerScript` with `enterID == MapChange`) - range 80m
3. Enemies (`EnemyManager.m_EnemyCtrlArray`) - range 150m
4. NPCs (`NpcManager.m_NpcCtrlArray`) - range 120m
5. Partners (`PartnerCtrl`) - range 200m (fallback only)

### WallDetectionHandler - Always-On Mode

**File:** `WallDetectionHandler.cs`

**Behavior:**
- Uses NavMesh.SamplePosition to detect impassable areas
- Checks 4 directions relative to player: ahead, behind, left, right
- Plays directional sounds with stereo panning
- Volume levels: ahead 0.4, behind 0.5, left/right 0.3
- Pauses during battles, cutscenes, events
- No toggle key - always active when player is in control

**Sound Files:**
- `wall up.wav` - Wall ahead (center pan)
- `wall down.wav` - Wall behind (center pan)
- `wall left.wav` - Wall to left (left speaker)
- `wall right.wav` - Wall to right (right speaker)

### Player Control Detection

Both systems use `IsPlayerInControl()` which checks:
- `uBattlePanel.m_instance.m_enabled == false` (not in battle)
- `PlayerCtrl.actionState` is not Event, Battle, Dead, DeadGataway, or LiquidCrystallization

## NPC Menu Panels

### Camp Command Panel (uCampPanelCommand)

**Class:** `uCampPanelCommand` (extends `uPanelBase`)

Used for: Camp menu when talking to the camp NPC.

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_KeyCursorController` (KeyCursorController) - Cursor position tracking
- `m_CampCommandContents` (CampCommandContent[]) - Array of menu items

**State Enum:**
- None, Main, Close, Wait

**CampCommandContent Structure:**
- `m_name` (Text) - Command name text

**Detection:** `FindObjectOfType<uCampPanelCommand>()`, check `m_state != None`

**Example:**
```csharp
var panel = FindObjectOfType<uCampPanelCommand>();
if (panel != null && panel.m_state == uCampPanelCommand.State.Main)
{
    int cursor = panel.m_KeyCursorController.m_DataIndex;
    int total = panel.m_KeyCursorController.m_DataMax;
    string itemName = panel.m_CampCommandContents[cursor].m_name.text;
}
```

### Common Select Window (uCommonSelectWindow)

**Class:** `uCommonSelectWindow` (extends `uCommonWindowBase`)

Used for: Generic selection menus (item selection, location selection, etc.)

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_uCommonSelectWindowPanelItem` (uCommonSelectWindowPanelItem) - Item panel component

**State Enum:**
- NONE, MAIN, CLOSE

**uCommonSelectWindowPanelItem:**
- Extends `uItemBase`
- `m_selectNo` (int) - Current cursor position (from uItemBase)
- `m_maxListNum` (int) - Total number of items
- `GetSelectItemParam()` - Returns `ParameterItemData` for current selection

**Detection:** `FindObjectOfType<uCommonSelectWindow>()`, check `m_state == State.MAIN`

**Example:**
```csharp
var panel = FindObjectOfType<uCommonSelectWindow>();
if (panel != null && panel.m_state == uCommonSelectWindow.State.MAIN)
{
    var itemPanel = panel.m_uCommonSelectWindowPanelItem;
    int cursor = itemPanel.m_selectNo;
    int total = itemPanel.m_maxListNum;
    var paramData = itemPanel.GetSelectItemParam();
    string name = paramData?.GetName() ?? "";
}
```

### Trade Panel (uTradePanelCommand)

**Class:** `uTradePanelCommand` (extends `uPanelBase`)

Used for: Shop/trade menus when buying or selling items.

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_tradePanelItem` (uTradePanelItem) - Item panel component
- `m_tradeDescription` (uTradeDescription) - Description panel

**State Enum:**
- None, Main, Close, Wait, NumInput, etc.

**uTradePanelItem:**
- Extends `uItemBase`
- `m_selectNo` (int) - Current cursor position
- `m_itemList` (List<ItemData>) - Item list (use Count for total)
- `GetSelectItemParam()` - Returns `ParameterItemData` for current selection

**uTradeDescription:**
- `m_name` (Text) - Item name display
- `m_price` (Text) - Price display
- `m_description` (Text) - Item description

**Detection:** `FindObjectOfType<uTradePanelCommand>()`, check `m_state != None`

**Example:**
```csharp
var panel = FindObjectOfType<uTradePanelCommand>();
if (panel != null && panel.m_state == uTradePanelCommand.State.Main)
{
    var itemPanel = panel.m_tradePanelItem;
    int cursor = itemPanel.m_selectNo;
    int total = itemPanel.m_itemList?.Count ?? 0;

    // Get name from description panel (preferred)
    string name = panel.m_tradeDescription?.m_name?.text ?? "";
    string price = panel.m_tradeDescription?.m_price?.text ?? "";
}
```

### Restaurant Panel (uRestaurantPanel)

**Class:** `uRestaurantPanel` (extends `MonoBehaviour`)

Used for: Restaurant/cooking menus.

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_type` (Type enum) - Restaurant or CampCooking

**State Enum:**
- None, ItemWait, UseItemMessageWait, ItemEatCheck, CampCookingSEWait, CampCookingFadeInWait, CampCookingSelectDigimonUpdate, etc.

**Type Enum:**
- Restaurant = 0
- CampCooking = 1

**uRestaurantPanelItem:**
- Extends `uItemBase`
- `m_selectNo` (int) - Current cursor position
- `m_maxListNum` (int) - Total number of items
- `GetSelectItem()` - Returns `ItemData` for current selection
- `GetSelectItemParam()` - Returns `ParameterItemData` for current selection (use this for name!)

**Detection:** `FindObjectOfType<uRestaurantPanel>()`, check `m_state != None`

**Example:**
```csharp
var panel = FindObjectOfType<uRestaurantPanel>();
if (panel != null && panel.m_state != uRestaurantPanel.State.None)
{
    var itemPanel = FindObjectOfType<uRestaurantPanelItem>();
    int cursor = itemPanel.m_selectNo;
    int total = itemPanel.m_maxListNum;

    // Use GetSelectItemParam() for item name (NOT GetSelectItem().m_name!)
    var paramData = itemPanel.GetSelectItemParam();
    string name = paramData?.GetName() ?? "";
}
```

### Training Panel (uTrainingPanelCommand)

**Class:** `uTrainingPanelCommand` (extends `MonoBehaviour`)

Used for: Gym training selection menu.

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_trainingCursors` (TrainingCursor[]) - Cursor objects
- `m_trainingContents` (TrainingContent[]) - Training slot data

**State Enum:**
- None, Main, Dialog, ChangeStateDigimonHistory, ChangeStateBonus, Close, Wait, Tutorial

**TrainingCursor (nested class):**
- `index` (TrainingKindIndex) - Current position as enum

**TrainingContent:**
- `level` (int) - Training level
- `index` (TrainingKindIndex) - Training type
- `bonusCount` (int) - Number of bonuses

**TrainingKindIndex Enum (ParameterTrainingData.TrainingKindIndex):**
- Hp = 0
- Mp = 1
- Forcefulness = 2 (Strength)
- Robustness = 3 (Stamina)
- Cleverness = 4 (Wisdom)
- Rapidity = 5 (Speed)
- Rest = 6
- Max = 7

**Detection:** `FindObjectOfType<uTrainingPanelCommand>()`, check `m_state != None && m_state != Close`

**Example:**
```csharp
var panel = FindObjectOfType<uTrainingPanelCommand>();
if (panel != null && panel.m_state == uTrainingPanelCommand.State.Main)
{
    int cursor = (int)panel.m_trainingCursors[0].index;
    int total = panel.m_trainingContents.Length;

    var content = panel.m_trainingContents[cursor];
    int level = content.level;
    var kindIndex = content.index;

    // Map to readable name
    string name = kindIndex switch
    {
        TrainingKindIndex.Hp => "HP Training",
        TrainingKindIndex.Mp => "MP Training",
        TrainingKindIndex.Forcefulness => "Strength Training",
        TrainingKindIndex.Robustness => "Stamina Training",
        TrainingKindIndex.Cleverness => "Wisdom Training",
        TrainingKindIndex.Rapidity => "Speed Training",
        TrainingKindIndex.Rest => "Rest",
        _ => $"Training {(int)kindIndex + 1}"
    };
}
```

### Colosseum Panel (uColosseumPanelCommand)

**Class:** `uColosseumPanelCommand` (extends `MonoBehaviour`)

Used for: Battle arena selection menu.

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_colosseumScrollView` (ColosseumScrollView) - Scrollable battle list
- `m_colosseumDescription` (uColosseumDescription) - Description panel

**State Enum:**
- None, Main, Confirm, Warning, Close, Wait

**ColosseumScrollView:**
- Extends `uItemBase`
- `m_selectNo` (int) - Current cursor position
- `m_itemList` (List<ItemData>) - Battle list (use Count for total)

**uColosseumDescription:**
- `m_caption` (Text) - Battle name/title
- `m_ruleValue` (Text) - Rule description

**Detection:** `FindObjectOfType<uColosseumPanelCommand>()`, check `m_state != None && m_state != Close`

**Example:**
```csharp
var panel = FindObjectOfType<uColosseumPanelCommand>();
if (panel != null && panel.m_state == uColosseumPanelCommand.State.Main)
{
    var scrollView = panel.m_colosseumScrollView;
    int cursor = scrollView.m_selectNo;
    int total = scrollView.m_itemList?.Count ?? 1;

    var desc = panel.m_colosseumDescription;
    string caption = desc?.m_caption?.text ?? "";
    string rule = desc?.m_ruleValue?.text ?? "";
}
```

### Farm Panel (uFarmPanelCommand)

**Class:** `uFarmPanelCommand` (extends `MonoBehaviour`)

Used for: Farm goods management menu.

**Key Properties:**
- `m_state` (State enum) - Panel state
- `m_farmCursor` (FarmCursor) - Cursor object
- `m_farmContents` (uFarmPanelFarmContent[]) - Farm slot UI elements

**State Enum:**
- None, Main, Item, Wait

**FarmCursor (nested class):**
- `index` (int) - Current position

**uFarmPanelFarmContent:**
- `m_name` (Text) - Item name
- `m_day` (Text) - Day information
- `m_time` (Text) - Time information
- `m_condition` (Text) - Condition status

**Detection:** `FindObjectOfType<uFarmPanelCommand>()`, check `m_state != None`

**Example:**
```csharp
var panel = FindObjectOfType<uFarmPanelCommand>();
if (panel != null && panel.m_state != uFarmPanelCommand.State.None)
{
    int cursor = panel.m_farmCursor.index;
    int total = panel.m_farmContents.Length;

    var content = panel.m_farmContents[cursor];
    string name = content.m_name?.text ?? "";
    string condition = content.m_condition?.text ?? "";
    string day = content.m_day?.text ?? "";
}
```

## Common Patterns for NPC Menus

### uItemBase Pattern
Many NPC menu panels use classes that extend `uItemBase`:

**Key Properties (inherited):**
- `m_selectNo` (int) - Current cursor position
- `m_itemList` (List<ItemData>) - List of items (use Count for total)
- `GetSelectItemData()` - Returns `ItemData` for current selection
- `GetSelectItemParam()` - Returns `ParameterItemData` for current selection

**Getting Item Names:**
```csharp
// WRONG - ItemData doesn't have m_name:
// string name = itemPanel.GetSelectItem().m_name;  // Error!

// CORRECT - Use ParameterItemData.GetName():
var paramData = itemPanel.GetSelectItemParam();
string name = paramData?.GetName() ?? "";
```

### ParameterItemData

**Class:** `ParameterItemData` (base class for item data)

**Key Methods:**
- `GetName()` - Returns localized item name string

**Derived Classes:**
- ParameterItemDataFood
- ParameterItemDataRecovery
- ParameterItemDataBattle
- ParameterItemDataMaterial
- ParameterItemDataKeyItem
- ParameterItemDataOther
- ParameterItemDataEvolution

## Save/Load Menu (uSavePanelCommand)

**Class:** `uSavePanelCommand` (extends `uPanelBase`)

Used for: Save and load game progress menu.

**State Enum:**
- NONE = 0
- MAIN = 1 (selecting slot)
- CLOSE = 2
- LOAD_CHECK = 3 (confirm load)
- LOAD = 4 (loading)
- SAVE_CHECK = 5 (confirm save)
- SAVE = 6 (saving)

**Key Properties:**
- `m_State` (State enum) - Current menu state
- `m_KeyCursorController` (KeyCursorController) - Cursor position tracking
- `m_items` (uSavePanelItemBase[]) - Array of save slot items
- `m_Caption` (uSavePanelCaption) - Caption/instruction panel

**Key Methods:**
- `GetCorsorIndex()` - Returns current cursor position (note: typo in game code)
- `ExistSavedata(int slot)` - Check if save slot has data
- `GetSaveItemDatas()` - Get array of save item data

**Detection:** `FindObjectOfType<uSavePanelCommand>()`, check `m_State == State.MAIN`

### uSavePanelItemSaveItem (Save Slot Details)

**Class:** `uSavePanelItemSaveItem` (extends `uSavePanelItemBase`)

Used for: Individual save slot display with game data.

**Key Properties:**
- `m_playerNameText` (Text) - Player name
- `m_tamarLavelText` (Text) - Tamer level value
- `m_areaText` (Text) - Current area/location name
- `m_playTimeText` (Text) - Play time display
- `m_timeStampText` (Text) - Save date/time
- `m_NoDataText` (Text) - "No Data" text for empty slots

**Example:**
```csharp
var panel = FindObjectOfType<uSavePanelCommand>();
if (panel != null && panel.m_State == uSavePanelCommand.State.MAIN)
{
    int cursor = panel.m_KeyCursorController.m_DataIndex;
    int total = panel.m_KeyCursorController.m_DataMax;

    var item = panel.m_items[cursor];
    var saveItem = item.TryCast<uSavePanelItemSaveItem>();
    if (saveItem != null)
    {
        // Check for empty slot
        if (saveItem.m_NoDataText?.gameObject.activeInHierarchy == true)
        {
            // Empty slot
        }
        else
        {
            string playerName = saveItem.m_playerNameText?.text ?? "";
            string area = saveItem.m_areaText?.text ?? "";
            string playTime = saveItem.m_playTimeText?.text ?? "";
        }
    }
}
```

### uSavePanelHeadLine (Menu Header)

**Class:** `uSavePanelHeadLine` (extends `uPanelBase`)

**Key Properties:**
- `m_HeadLine` (Text) - Header text (e.g., "Save" or "Load")

### uSavePanelCaption (Menu Caption)

**Class:** `uSavePanelCaption` (extends `uPanelBase`)

**Key Properties:**
- `m_Caption` (Text) - Caption/instruction text

**CaptionKind Enum:**
- NORMAL = 0
- YESNO = 1 (confirmation mode)

## Field Item Panel (uFieldItemPanel)

**Class:** `uFieldItemPanel` (extends `MonoBehaviour`)

Used for: Using items during field exploration.

**Type Enum (Item Categories):**
- Care = 0
- Camp = 1
- Digivice = 2
- Event = 3
- Battle = 4
- Oyatsu = 5 (Snacks)
- Max = 6

**Result Enum:**
- ItemSelect
- ItemMenuToCommandMenu
- ItemMessage
- ItemUse

**Key Properties:**
- Multiple item category tabs with TypeData struct
- Context-dependent item lists per category
- Scroll position tracking per category

**Detection:** `FindObjectOfType<uFieldItemPanel>()`, check state is active

## Storage Panel (uStoragePanel)

**Class:** `uStoragePanel` (extends `MonoBehaviour`)

Used for: Transferring items between inventory and storage boxes.

**Key Properties:**
- `m_itemPanelL` - Left panel (source)
- `m_itemPanelR` - Right panel (destination)
- `m_captionPanel` - Header/title panel
- `m_infoPanel` - Item details panel
- `m_openType` - Storage type state

**Key Methods:**
- `ChangeItemList` - Switch between lists
- `ArrowAction` - Navigate between panels

**Detection:** `FindObjectOfType<uStoragePanel>()`, check panel is active

## Map Panel (uDigiviceMapPanel)

**Class:** `uDigiviceMapPanel` (extends `MonoBehaviour`)

Used for: Viewing world map, area maps, and navigation.

**State Enum:**
- NONE
- WORLD
- AREA
- MINI_AREA
- CLOSE
- MAX

**RapidChangeKind Enum:**
- NORMAL
- EXTRA_DUNGEON

**Key Properties:**
- Multi-level map navigation (World/Area/Mini)
- MapPanelBitFlag with state tracking
- Dialog state management

**Detection:** `FindObjectOfType<uDigiviceMapPanel>()`, check state is not NONE

## Partner Panel (uPartnerPanel)

**Class:** `uPartnerPanel` (extends `MonoBehaviour`)

Used for: Viewing detailed Digimon partner information.

**State Enum (Tabs):**
- None
- Top
- Status
- Attack
- Tactics
- History

**Key Properties:**
- Multi-tab navigation
- Tab-specific content display
- InitializeTask coroutine for async loading

**Tab Content:**
- Status: HP, MP, stats, hunger, fatigue
- Attack: Known moves/skills
- Tactics: Battle AI settings
- History: Evolution history, achievements

**Detection:** `FindObjectOfType<uPartnerPanel>()`, check state is not None

## Item Pick Panel (uItemPickPanel)

**Class:** `uItemPickPanel` (extends `MonoBehaviour`)

Used for: Picking up items and materials found in the field.

**State Enum:**
- None
- CommandMain
- ItemPick
- MaterialPick
- Result
- Close
- Wait
- Max

**Result Enum:**
- None
- End

**Key Properties:**
- Material vs item type distinction
- Callback-based state changes
- Count/quantity management

**Detection:** `FindObjectOfType<uItemPickPanel>()`, check state is not None

## Mail Panel (uMailPanel)

**Class:** `uMailPanel` (extends `MonoBehaviour`)

Used for: Reading messages and quest information.

**State Enum:**
- SELECT
- NEXT
- RETURN

**Key Properties:**
- `m_ListMailData` - List of mail items
- `m_SelectTab` - Current tab index
- `m_SelectTabInfoTbl` - Tab information array
- MailPanelBitFlag with 9 state flags

**Key Methods:**
- `UpdateMailList` - Refresh mail list
- `SetSelectTab` - Change active tab

**Detection:** `FindObjectOfType<uMailPanel>()`, check panel is active

## Digivice Top Panel (uDigiviceTopPanel)

**Class:** `uDigiviceTopPanel` (extends `MonoBehaviour`)

Used for: Main Digivice menu with navigation to sub-menus.

**Key Properties:**
- Main menu cursor/selection
- Navigation to Partner, Item, Map, Mail, etc.

**Detection:** `FindObjectOfType<uDigiviceTopPanel>()`, check panel is active

## Zone Panel

Used for: Zone/area selection when traveling.

**Key Properties:**
- Zone list with cursor selection
- Zone name and description

**Detection:** Check for zone selection panel active state
