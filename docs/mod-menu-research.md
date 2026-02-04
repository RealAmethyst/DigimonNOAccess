# Mod Menu UI Research

This document contains extensive research on how the game's options menu system works and how we could potentially create a mod settings menu integrated into the game.

## 1. Options Menu Architecture

### 1.1 Core Classes

The game's options menu is built around a hierarchical panel system:

**uOptionPanel** - Root controller for the entire options menu
- Controls overall state machine
- States: NONE, MAIN_SETTING, CLOSE, SAVE, APPLICATION_QUIT, AGREE, END
- Sub-states (MainSettingState): TOP, OPTION, GRAPHICS, KEYCONFIG, APPLICATION_QUIT, AGREE
- Contains references to multiple command panels (uOptionPanelCommand array)
- Handles opening/closing animations, state transitions, and save/load callbacks
- Manages system data saving via SystemDataSaveLoad

**Panel Hierarchy:**
```
uOptionPanel (Main Controller)
- uOptionPanelHeadLine (Title/Header)
- uOptionPanelCaption (Description/Help Text)
- uOptionPanelCommand[] (Main Options Panel array)
- uOptionTopPanelCommand (Top-level menu selector - tabs)
- uOptionGraphicsPanelCommand (Graphics settings)
- uOptionKeyConfigPanelCommand (Key configuration)
```

### 1.2 Command Panel Base Classes

**uOptionPanelCommandBase** - Abstract base for all option panels
- Inherits from uPanelBase (which inherits from MonoBehaviour)

Key fields:
- `m_Caption` - Reference to uOptionPanelCaption for displaying option descriptions
- `m_MainCursor` - Visual cursor/highlight GameObject
- `m_KeyCursorController` - Handles navigation input between items
- `OptionPanel` - Back-reference to parent uOptionPanel
- `m_State` - Panel state (NONE, MAIN, CLOSE, HYDE)
- `EnableInput` - Property controlling whether panel accepts input

Virtual methods to override:
- `Initialize()` - Called during setup
- `Update()`, `LateUpdate()` - Per-frame updates
- `enablePanel(bool enable, bool finished_disable)` - Show/activate panel
- `disablePanel()` - Hide/deactivate panel
- `UpdateKeyInfo()` - Update caption based on current selection
- `LoadOptionSaveData()` - Load saved settings
- `SaveCurrentData()` - Save current settings
- `SetCursor(int index)` - Set which item is selected

**uOptionPanelCommand** - Main options panel (Sound, Camera, etc.)
- Extends uOptionPanelCommandBase

Key fields:
- `m_CommandInfoArray` - Array of CommandInfo structs describing each menu item
- `m_optionItemRoot` - Transform parent for spawning option items
- `m_items` - List<uOptionPanelItemBase> containing all option items

**CommandInfo struct:**
- `m_CommandObject` - The actual UI GameObject for this menu entry
- `m_CommandCursorPos` - Position for cursor when item is selected
- `m_CommandName` - Text component showing option name
- `m_CommandNum` - Text component showing current value
- `m_Slider` - Optional slider UI component
- `m_ContentsCursor` - Visual cursor for this item

### 1.3 Specialized Panels

**uOptionTopPanelCommand** - Menu selector (Option, Graphics, Key Config tabs)
- Displays the main category tabs at the top
- Has `m_dustBox` Transform for hidden items

**uOptionGraphicsPanelCommand** - Graphics-specific panel
- Resolution, ScreenMode, Antialiasing, DepthOfField options

**uOptionKeyConfigPanelCommand** - Key configuration panel
- Complex state machine for key binding (MAIN, KEY_CHECK, KEY_END)
- Manages keyboard and mouse configuration separately
- Has scrollbar for long key lists

## 2. UI Item System

### 2.1 Item Base Class

**uOptionPanelItemBase** - Base for all menu items (MonoBehaviour)

Key fields:
- `m_itemTitle` - GameObject container for item
- `m_contentsCursor` - Visual cursor for this item
- `m_headCursor` - Highlight cursor GameObject
- `m_headMinScale`, `m_headMaxScale` - Cursor animation scales
- `m_IndexId` - Numeric index of this item
- `m_caption` - Reference to caption panel
- `m_command` - Back-reference to parent command panel
- `Value` - Property for getting/setting item's current value (int)

Virtual methods:
- `Setup(GameObject headCursor, float minScale, float maxScale, uOptionPanelCaption caption, int index, uOptionPanelCommandBase command)` - Initialize item
- `Select` property - Set whether item is currently selected
- `_Update()` - Returns bool (true if item changed), handles input processing
- `Load()` - Load saved value for this item
- `Save()` - Save current value
- `captionKind` property - Returns CaptionKind for caption system

### 2.2 Item Variants

**uOptionPanelItemSlider** - For slider controls
- `m_sliderObject` - Reference to UnityEngine.UI.Slider component
- `m_sliderNum` - Text display showing current value
- Overrides `_Update()` to handle slider input

**uOptionPanelItemToggle** - For on/off toggles
- Overrides `_Update()` to toggle boolean state
- `OnToggle(bool toggle)` - Virtual callback for toggle changed

**uOptionPanelItemVoid** - Special item for navigation/section headers
- `m_settingState` - Which menu section this item navigates to
- Used for menu navigation, not actual values

**Specialized Volume/Camera Items:**
- `uOptionPanelItemBgmVolume` - BGM volume slider (0-100)
- `uOptionPanelItemSeVolume` - SE volume slider (0-100)
- `uOptionPanelItemVoiceVolume` - Voice volume slider (0-100)
- `uOptionPanelItemCameraH` - Horizontal camera sensitivity
- `uOptionPanelItemCameraV` - Vertical camera sensitivity
- `uOptionPanelItemSensitivity` - General sensitivity slider
- `uOptionPanelItemVoiceLanguage` - Language selector dropdown

## 3. State Management and Persistence

### 3.1 Option Data Classes

**OptionData** - Container for all user settings

Enums defined:
- `VolumeKind`: BGM, VOICE, SE, CAMERA_SENSITIVITY, MAX
- `CameraMoveKind`: CAMERA_UD, CAMERA_LR, MAX
- `OptionDataKind`: BGM, VOICE, SE, CAMERA_UPDOWN, CAMERA_LEFTRIGHT, CAMERA_SENSITIVITY, MAX
- `GraphicOptionDataKind`: Resolution, ScreenMode, Antialiasing, DepthOfField, MAX
- `KeyConfigDataKind`: A through MAX (20+ key mappings)

Key fields:
- `m_BgmVolume`, `m_VoiceVolume`, `m_SeVolume` - Float values (0-1 range)
- `m_CameraUpDown`, `m_CameraLeftRight`, `m_CameraSensitivity` - Camera settings
- `m_IsSavedFlag` - Boolean indicating if settings have been saved
- `m_Resolution`, `m_ScreenMode`, `m_Antialiasing`, `m_DepthOfField` - Graphics
- `m_Key[]`, `m_Mouse[]` - Key configuration arrays

**OptionDataAccess** - Helper class for accessing OptionData

Methods:
- `ReadSaveData(BinaryReader)` - Load from binary save file
- `WriteSaveData(BinaryWriter)` - Save to binary file
- `GetVolume(VolumeKind)` - Get volume by type
- `IsCameraMoveReverse(CameraMoveKind)` - Get camera inversion
- `SetSavedFlag(bool)` - Mark as saved/unsaved
- `ResetValue()` - Reset to defaults
- `GetKeyConfig(KeyConfigDataKind)` - Get key binding
- `SetKeyConfig(KeyConfigDataKind, KeyCode)` - Set key binding

### 3.2 Save/Load System

**SystemDataSaveLoad** - Manages option data persistence

States: Idle, ErrorMsg, Load, RetryLoad, Save, RetrySave, Delete

Key methods:
- `SaveStartSystemData(Action<bool> callback)` - Start async save with callback
- `LoadStartSystemData(Action<bool> callback)` - Start async load with callback
- `Update()` - Process save/load state machine (must be called each frame)
- `IsBusy()` - Returns true if operation in progress

**Save Workflow:**
1. User changes options in uOptionPanelCommand
2. User presses confirm/save button
3. uOptionPanel.SaveCurrentData() calls each panel's SaveCurrentData()
4. Panel's SaveCurrentData() updates the OptionData fields
5. uOptionPanel calls m_SystemDataSaveLoader.SaveStartSystemData(callback)
6. SystemDataSaveLoad handles async file I/O
7. Callback is invoked when complete

## 4. Input and Navigation

### 4.1 Cursor Control

**KeyCursorController** - Manages menu navigation
- Updates `m_MainCursor` position based on item selection
- Applies smooth animation/scaling to cursor
- Listens for up/down/left/right input to move between items

### 4.2 Caption System

When an item is selected:
1. `SetCursor(int index)` is called on the command panel
2. Panel finds corresponding item in `m_items` list
3. Panel calls `item.Select = true`
4. Item's Select setter calls caption system
5. `m_caption.ChangeCaption(item.captionKind)` displays appropriate description

**CaptionKind enum:**
- NORMAL, REVERSE, CUSTOM_SOUND, VOICE, VOID, GRAPHIC, YES_NO, KEY_CONFIG, MAX

## 5. Potential Implementation Approaches

### Approach 1: Hook into Existing Menu (Recommended)

Add a new tab to the existing options menu using Harmony patches.

**Steps:**
1. Patch `uOptionTopPanelCommand` to add a new "Mod Settings" tab
2. Add a new `MainSettingState` enum value (e.g., MOD_SETTINGS)
3. Create a custom panel class extending `uOptionPanelCommandBase`
4. Patch `uOptionPanel` to instantiate and manage the new panel
5. Handle state transitions to activate the mod panel

**Pros:**
- Fully integrated with game's menu system
- Same visual style and navigation
- Settings saved with game options (if extended properly)

**Cons:**
- Complex to implement
- Requires multiple Harmony patches
- Risk of breaking if game updates

### Approach 2: Custom Standalone Panel

Create a separate panel that appears when a hotkey is pressed.

**Steps:**
1. Inherit from `uPanelBase` directly
2. Create UI programmatically or from prefab
3. Show as overlay when hotkey pressed
4. Handle input separately from game menus

**Pros:**
- Simpler to implement
- Less coupling with game code
- Can be opened anytime (not just in options)

**Cons:**
- Separate from game's option menu
- Need to create UI from scratch
- Different visual style unless carefully matched

### Approach 3: Simple INI-Based Config (Current)

Continue using hotkeys.ini with F8 to reload.

**Pros:**
- Already implemented
- No UI code needed
- Easy to edit externally

**Cons:**
- Not accessible in-game
- Requires exiting game to change settings

## 6. Key Classes for Implementation

### For Approach 1 (Integrated Menu):

```csharp
// Classes to patch:
uOptionPanel - Add new MainSettingState, instantiate mod panel
uOptionTopPanelCommand - Add new tab item
uOptionPanelCommand - Reference for creating similar panel

// Classes to extend:
uOptionPanelCommandBase - Base for mod settings panel
uOptionPanelItemBase - Base for mod setting items
uOptionPanelItemSlider - For slider settings
uOptionPanelItemToggle - For toggle settings
```

### For Approach 2 (Standalone):

```csharp
// Classes to extend:
uPanelBase - Base for custom panel
MonoBehaviour - For custom items

// Unity UI to use:
UnityEngine.UI.Canvas
UnityEngine.UI.Text
UnityEngine.UI.Slider
UnityEngine.UI.Toggle
UnityEngine.UI.Button
```

## 7. UI Hierarchy Example

Typical panel structure in hierarchy:
```
Canvas
- OptionPanelRoot (Panel container)
  - Background Image
  - TopCommands (uOptionTopPanelCommand)
    - Command Item 0 (Button, Text)
    - Command Item 1 (Button, Text)
    - Cursor (m_MainCursor GameObject)
  - OptionCommand (uOptionPanelCommand)
    - Item Root (m_optionItemRoot)
      - OptionItem 0
      - OptionItem 1
      - ...
    - Cursor
  - Caption (uOptionPanelCaption)
    - Text Component
  - Animators (for open/close animations)
```

## 8. Important Implementation Notes

1. **Item Update Order**: Items must implement `_Update()` returning bool to indicate if value changed. This controls caption updates.

2. **Cursor Management**: Each item has its own headCursor GameObject. The parent command panel manages the main visual cursor (m_MainCursor).

3. **Animation Waiting**: uOptionPanel has `waitAnimation()` coroutine that waits for animation completion before changing states.

4. **Panel Enable/Disable Pattern**:
   - `enablePanel(bool, bool finished_disable)` - Show panel
   - `disablePanel()` - Hide panel

5. **Async Save**: SystemDataSaveLoad must be updated each frame. Callback won't fire if Update() isn't called.

6. **Sound Effects**: Each panel can have open/close sound effects via SetOpenCloseSE().

## 9. Mod Settings We Could Expose

Potential settings for the mod menu:

**Speech Settings:**
- Read voiced text (toggle) - currently F5
- Speech rate (if Tolk supports it)
- Interrupt mode (always interrupt vs queue)

**Audio Navigation:**
- Enable/disable positional audio (toggle)
- Wall detection (toggle)
- Detection ranges (sliders): Items, NPCs, Enemies, Transitions
- Volume levels (sliders): Positional audio, Wall sounds

**Controller Settings:**
- Enable SDL3 (toggle)
- Button layout preview

**Hotkeys:**
- Display current bindings (read-only initially)
- Future: rebind in-game

## 10. Recommended Next Steps

1. **Start with Approach 2** (Standalone Panel) for simplicity
2. Create basic panel with a few toggle/slider items
3. Test accessibility with screen reader
4. If successful, consider migrating to Approach 1 for full integration

The standalone approach lets us validate the concept before investing in the more complex integrated approach.

## 11. Files to Reference

Decompiled code locations:
- `decompiled/Il2Cpp/uOptionPanel.cs`
- `decompiled/Il2Cpp/uOptionPanelCommand.cs`
- `decompiled/Il2Cpp/uOptionPanelCommandBase.cs`
- `decompiled/Il2Cpp/uOptionPanelItemBase.cs`
- `decompiled/Il2Cpp/uOptionPanelItemSlider.cs`
- `decompiled/Il2Cpp/uOptionPanelItemToggle.cs`
- `decompiled/Il2Cpp/uOptionTopPanelCommand.cs`
- `decompiled/Il2Cpp/OptionData.cs`
- `decompiled/Il2Cpp/OptionDataAccess.cs`
- `decompiled/Il2Cpp/SystemDataSaveLoad.cs`
- `decompiled/Il2Cpp/uPanelBase.cs`
