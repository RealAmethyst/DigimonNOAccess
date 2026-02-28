# Mod Menu Research: In-Game Accessibility Settings

## Executive Summary

The game's options menu uses a state-machine architecture with array-indexed panels. Adding a new "Accessibility" tab requires hooking into the existing `uOptionPanel` system. Three approaches exist, each with different trade-offs. The recommended approach is a **hybrid injection** - hooking the game's menu to add a new top-level entry, then rendering our own TTS-based panel when selected.

---

## Game's Options Menu Architecture

### State Machine

`uOptionPanel` drives the entire options screen with two enums:

- **State** (overall lifecycle): NONE, MAIN_SETTING, CLOSE, SAVE, APPLICATION_QUIT, AGREE, AGREE_POST, AGREE_POST_WAIT, END
- **MainSettingState** (which tab is active): TOP, OPTION, GRAPHICS, KEYCONFIG, APPLICATION_QUIT, AGREE

### Flow

```
Open() -> State = MAIN_SETTING, MainSettingState = TOP

TOP state: Category list (4 items: Option, Graphics, Key Config, Quit)
  -> User selects -> SetMainSettingState(OPTION|GRAPHICS|KEYCONFIG|...)
  -> Activates m_uOptionPanelCommand[index]
  -> Updates m_uOptionPanelHeadLine.ChangeText(state)

Subpanel state: Individual settings
  -> Back button -> SetMainSettingState(TOP)

Close() -> Save -> End
```

### Key Classes

- `uOptionPanel` - Main controller, holds the state machine and panel array
- `uOptionTopPanelCommand` - The TOP menu (category list). Has `m_CommandInfoArray[]` (CommandInfo structs with GameObject, Text, cursor position)
- `uOptionPanelCommandBase` - Base for all subpanels. Virtual methods: Initialize, Update, LoadOptionSaveData, SaveCurrentData, SetCursor
- `uOptionPanelHeadLine` - Displays tab name. Uses `m_HeadlineList[]` indexed by MainSettingState
- `uOptionPanelItemBase` - Base for individual option items (toggle, slider, etc.)

### Critical Detail: Array Indexing

The system uses **direct enum-to-array indexing**:
- `m_uOptionPanelCommand[MainSettingState]` -> the panel for that state
- `m_HeadlineList[MainSettingState]` -> the headline text
- `m_CommandInfoArray[index]` -> the top menu item

This means adding a new category requires either:
1. Extending/replacing these arrays at runtime
2. Hijacking an existing state
3. Working outside the array system entirely

---

## Current Mod Settings System

### ModSettingsHandler (F10 menu)
- Standalone TTS-only menu (no visual UI)
- Currently 1 setting: "Read Voiced Text"
- Uses abstract `SettingItem` base with `ToggleSetting` subclass
- Input: keyboard arrows + Enter/Space, controller DPad + A/B
- Priority 5 (runs before other handlers)

### OptionsMenuHandler (game menu reader)
- Reads and announces the game's native options menu
- Tracks uOptionPanel state, cursor position, item values
- Three-tier item reading: specific type casting -> CommandInfo fallback -> generic
- Priority 40

### Current Config Storage
- `hotkeys.ini` - Keybindings only (Keyboard + Controller sections)
- No persistent storage for other settings (all hardcoded or in-memory)

---

## Three Approaches

### Approach A: Full Game Menu Injection

**Concept:** Add a new MainSettingState (ACCESSIBILITY), create a real uOptionPanelCommandBase subclass, resize the panel arrays.

**How it would work:**
1. Hook `uOptionPanel.Initialize()` / `IEInitialize()`
2. After original init, resize `m_uOptionPanelCommand[]` to add our panel
3. Hook `uOptionTopPanelCommand.ItemSetUp()` to add a new CommandInfo entry
4. Extend `m_HeadlineList[]` with "Accessibility" text
5. Hook `SetMainSettingState()` to handle our new state value
6. Create a real MonoBehaviour panel with uOptionPanelItemBase children

**Pros:**
- Looks and behaves exactly like native menus
- Game's cursor system handles navigation
- Consistent with existing UI

**Cons:**
- Very fragile: enum values are compiled as integers in Il2Cpp, array resizing is risky
- Must create real Unity GameObjects that match the game's prefab structure
- Extensive hooking (5+ methods to patch)
- If game updates, patches break
- Creating proper uOptionPanelItemBase instances requires replicating prefab hierarchy (child GameObjects, Text components, cursors, sliders, etc.)

**Risk: HIGH** - The array-indexed enum system is hostile to injection.

---

### Approach B: Hybrid Injection (RECOMMENDED)

**Concept:** Add "Accessibility" as a menu item in the TOP panel, but when selected, take over rendering with our own TTS-based system (similar to current ModSettingsHandler but triggered from the game menu).

**How it would work:**
1. Hook `uOptionTopPanelCommand` to add one more item to the category list
2. Hook `SetMainSettingState()` - when our custom state is selected, suppress the game's panel switching and activate our handler
3. Our handler uses ScreenReader for all output (no visual UI needed for a blind user)
4. Navigation uses the game's existing input (or our ModInputManager)
5. Back button returns to TOP state normally

**Implementation detail - Adding the menu item:**
- `m_CommandInfoArray` is a serialized array on the MonoBehaviour
- We can clone an existing CommandInfo's GameObject, rename its Text to "Accessibility"
- Insert it into the array (or add after last item)
- The TOP panel's cursor system uses `m_CommandInfoArray.Length` for bounds

**Implementation detail - State hijacking:**
- We don't need a real MainSettingState enum value
- Hook `SetMainSettingState()`: if cursor index matches our item, set a mod flag and suppress the game's transition
- While our flag is set, our handler takes over Update/input
- On back/cancel, clear flag and call `SetMainSettingState(TOP)`

**Pros:**
- Menu item appears in the real game menu (feels native)
- No need to create complex Unity UI (TTS-only output works perfectly for a blind user)
- Much less fragile than full injection
- Leverages existing ModSettingsHandler patterns
- Settings categories and subcategories are easy to add

**Cons:**
- Visual menu item exists but the submenu is TTS-only (no visual controls)
- Still requires hooking the TOP panel to add the item
- Sighted helpers can't see the accessibility settings visually

**Risk: MEDIUM** - Limited hooking, graceful degradation if hooks fail.

---

### Approach C: Separate Menu with Game Menu Entry Point

**Concept:** Keep ModSettingsHandler as a standalone TTS menu, but make it accessible from the game's options menu instead of (or in addition to) F10.

**How it would work:**
1. In OptionsMenuHandler, detect when user is on TOP panel
2. Add a virtual "Accessibility" item that only exists in TTS (not visually)
3. When cursor goes past the last real item, announce "Accessibility"
4. On confirm, open ModSettingsHandler
5. ModSettingsHandler handles everything from there

**Pros:**
- Minimal code changes
- No game hooks needed
- Very robust (nothing to break)

**Cons:**
- "Ghost" menu item (announced but not visible) is confusing
- Doesn't feel like a real menu integration
- Navigation is weird (going past the end of a list)

**Risk: LOW** but **user experience is poor**.

---

## Why Approach B is Recommended

For a blind user, the visual panel contents don't matter - TTS is the interface. What matters is:
1. "Accessibility" shows up as a real menu item when navigating the options menu
2. Selecting it enters a familiar settings navigation (up/down, confirm, back)
3. Settings persist across sessions
4. Keybinds are manageable from within the game

Approach B gives us the real menu entry (native feel) without the massive complexity of creating full Unity UI panels. The TTS-only submenu is actually an advantage: we control the entire experience and aren't limited by the game's widget types.

---

## Detailed Design for Approach B

### 1. New Patch: OptionPanelPatch.cs

```
Hook: uOptionTopPanelCommand.Initialize() [postfix]
  -> Clone last CommandInfo GameObject
  -> Set text to "Accessibility"
  -> Add to m_CommandInfoArray
  -> Update cursor bounds

Hook: uOptionPanel.SetMainSettingState() [prefix]
  -> If state index == our item index:
    -> Suppress original method
    -> Set AccessibilityMenuActive = true
    -> Hide all native subpanels
    -> Return false (skip original)

Hook: uOptionPanel.CheckInputKey() [prefix]
  -> If AccessibilityMenuActive:
    -> Route input to AccessibilityMenuHandler
    -> Return false (skip original)
```

### 2. New Handler: AccessibilityMenuHandler.cs

Categories:
- General (Read Voiced Text, Announcement Verbosity, etc.)
- Audio Navigation (ranges, volumes, per-type enable/disable)
- Keybindings (show current bindings, allow rebinding)
- About (mod version, credits)

Navigation model:
- Top level: category list (Up/Down to browse, Confirm to enter)
- Inside category: setting list (Up/Down, Left/Right for values, Confirm to toggle)
- Back: return to category list or to game's TOP menu

### 3. Settings Persistence

Two options for saving:

**Option 1: MelonPreferences (if available in Il2Cpp build)**
```
File: UserData/DigimonNOAccess.cfg
Format: INI (MelonLoader managed)

[General]
ReadVoicedText = false

[AudioNavigation]
ItemRange = 80
NpcRange = 80
EnemyRange = 100
NearestVolume = 0.8
```

MelonPreferences handles auto-save, type safety, defaults, file creation.

**Option 2: Custom JSON/INI file**
```
File: Mods/DigimonNOAccess/settings.json
```

Manual read/write with System.Text.Json or simple INI parser (already have one for hotkeys.ini).

### 4. Keybind Rebinding (Future Phase)

The game's KEYCONFIG panel has a key capture system:
- State machine: MAIN -> KEY_CHECK -> KEY_END
- Listens for next key press, stores as new binding

We can replicate this for mod keybinds:
- Announce "Press new key for [ActionName]"
- Wait for keypress via ModInputManager
- Store in hotkeys.ini or settings file

---

## Settings Inventory (What Goes in the Menu)

### General
- Read Voiced Text (toggle) - currently the only ModSettings item
- Announcement verbosity (Low/Medium/High)

### Audio Navigation
- Master audio nav volume (slider 0-100)
- Per-type enable: Items, NPCs, Enemies, Transitions, Facilities (toggles)
- Per-type range: Items, NPCs, Enemies, Transitions (slider)
- Per-type volume multiplier (slider)
- HRTF / Stereo pan mode (toggle, if Steam Audio unavailable)

### Navigation
- Auto-walk speed modifier
- Pathfinding final approach distance
- Compass direction format (Cardinal / Degrees)

### Keybindings
- List all current bindings (read-only initially)
- Phase 2: interactive rebinding

---

## Key Files Reference

### Decompiled (read-only reference)
- `decompiled/Il2Cpp/uOptionPanel.cs` - Main controller + state machine
- `decompiled/Il2Cpp/uOptionTopPanelCommand.cs` - Category list panel
- `decompiled/Il2Cpp/uOptionPanelCommandBase.cs` - Panel base class
- `decompiled/Il2Cpp/uOptionPanelHeadLine.cs` - Tab headline display
- `decompiled/Il2Cpp/uOptionPanelItemBase.cs` - Item base class
- `decompiled/Il2Cpp/uOptionPanelItemToggle.cs` - Toggle control
- `decompiled/Il2Cpp/uOptionPanelItemSlider.cs` - Slider control
- `decompiled/Il2Cpp/OptionData.cs` - Settings data structure

### Mod files (to modify/create)
- `OptionsMenuHandler.cs` - Extend to handle accessibility state
- `ModSettingsHandler.cs` - Refactor into AccessibilityMenuHandler
- `Main.cs` - Register new handler, init preferences
- NEW: `OptionPanelPatch.cs` - Harmony patches for menu injection
- NEW: `AccessibilityMenuHandler.cs` - Full settings menu handler
- NEW: `ModPreferences.cs` - Settings persistence wrapper

---

## Implementation Phases

### Phase 1: Foundation
- Create settings persistence (MelonPreferences or custom)
- Migrate "Read Voiced Text" from in-memory to persistent
- Create AccessibilityMenuHandler with category navigation (TTS-only)
- Keep F10 as entry point initially

### Phase 2: Menu Injection
- Write OptionPanelPatch hooks
- Add "Accessibility" item to TOP panel
- Wire up state hijacking so selecting it activates our handler
- Keep F10 as alternative entry point

### Phase 3: Settings Population
- Add all audio navigation settings
- Add all general settings
- Wire settings to actual mod behavior (replace hardcoded constants)

### Phase 4: Keybind Management
- Show current keybinds in menu
- Add interactive rebinding
- Persist to hotkeys.ini or migrate to settings file

---

## Sound Effects (Confirmed)

The game exposes menu sounds via `CriSoundManager` static methods. All use `PlayCommonSe(string cueName)`:

- **Cursor move:** `CriSoundManager.PlayCommonSe(CriSoundManager.SE_MoveCursor1)`
- **Confirm/OK:** `CriSoundManager.PlayCommonSe(CriSoundManager.SE_OK)`
- **Cancel/Back:** `CriSoundManager.PlayCommonSe(CriSoundManager.SE_Cancel)`
- **Window open:** `CriSoundManager.PlayCommonSe(CriSoundManager.SE_OpenWindow1)`
- **Window close:** `CriSoundManager.PlayCommonSe(CriSoundManager.SE_CloseWindow1)`
- **Error:** `CriSoundManager.PlayCommonSe(CriSoundManager.SE_Error)`

All static, no instance needed, no extra parameters. Playing these at the right moments makes TTS-only menus feel native.

---

## Settings Persistence (Confirmed)

**MelonPreferences is NOT available** in MelonLoader 0.7.1 (removed in 0.7.x).

**Solution: Newtonsoft.Json** (already bundled with MelonLoader 0.7.1 as a dependency).

```
File: {ModFolder}/settings.json

{
  "General": {
    "ReadVoicedText": false,
    "AnnouncementVerbosity": "Medium"
  },
  "AudioNavigation": {
    "ItemRange": 80,
    "NpcRange": 80,
    "EnemyRange": 100,
    "TransitionRange": 60,
    "NearestVolume": 0.8,
    "BackgroundVolume": 0.15,
    "ItemsEnabled": true,
    "NpcsEnabled": true,
    "EnemiesEnabled": true,
    "TransitionsEnabled": true,
    "FacilitiesEnabled": true
  }
}
```

- Load on startup (with defaults if file missing)
- Save on every setting change (immediate, prevents loss)
- Same folder as hotkeys.ini: `Path.GetDirectoryName(MelonAssembly.Location)`
- Newtonsoft.Json handles serialization/deserialization automatically

---

## Open Questions

1. **Il2Cpp array handling:** Il2CppReferenceArray may not support standard C# array resize. May need Il2Cpp interop methods or create a new array and reassign the field.
2. **GameObject cloning:** `Object.Instantiate()` should work for cloning CommandInfo GameObjects, but need to verify the clone gets proper parent transform and layout.
3. **Headline text injection:** `m_HeadlineList` is a string array on the MonoBehaviour - same Il2Cpp array concern.
4. **Input conflict:** While our handler is active in the options menu, we must suppress the game's input handling for that panel to prevent double-processing.
