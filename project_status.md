# DigimonNOAccess - Project Status

## Game Information
- **Game:** Digimon World Next Order
- **Developer:** Bandai Namco Entertainment
- **Engine:** Unity 2019.4.11f1 (Il2Cpp)
- **Architecture:** 64-bit
- **Runtime:** net6

## Paths
- **Game Directory:** C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order
- **Project Directory:** C:\Users\Amethyst\projects\digimon world, next order

## Setup Status
- [x] MelonLoader installed
- [x] Tolk DLLs in place (64-bit)
- [x] .NET SDK installed
- [x] Decompiler available (ilspycmd)
- [x] Code decompiled to decompiled/

## User Info
- **Experience Level:** Beginner
- **Screen Reader Approach:** OnUpdate (frame-based checking)
- **Localization:** Reads directly from game (no separate mod translations)

## MelonLoader Attribute
```csharp
[assembly: MelonGame("Bandai Namco Entertainment", "Digimon World Next Order")]
```

## Target Framework
```xml
<TargetFramework>net6.0</TargetFramework>
```

## Current Phase
New game flow complete through Digi-Egg selection with confirmation dialogs. Dialog choices now track properly using TalkMain.m_cursor. Voice detection filters voiced dialog from TTS. **3D positional audio navigation fully working** using NAudio (bypasses game's CRI audio system). **Always-on audio system** - no toggle keys required, all navigation sounds are automatic when player is in control.

### Battle System Accessibility (IN PROGRESS)
Handlers implemented:
- **BattleHudHandler** - Partner HP/MP status via D-pad (D-Up/Down for HP, D-Left/Right for orders). **Known issue:** D-pad input may be consumed by game. Use F3/F4 keyboard fallback.
- **BattleOrderRingHandler** - Order Ring command selection announcements (WORKING)
- **BattleItemHandler** - Battle item menu with target selection (WORKING)
- **BattleDialogHandler** - Battle Yes/No dialogs (escape confirmation, etc.) (WORKING)
- **BattleTacticsHandler** - Square button menu tabs (Escape, MP Usage, Target)
- **BattleResultHandler** - Victory screen announcements and reward logging

Controls: D-pad only for battle status (no shoulder buttons). F3/F4 keyboard fallback for partner status.

### NPC Menu Accessibility (COMPLETE)
All major NPC dialog menus now have accessibility handlers:
- **CampCommandHandler** - Camp menu commands (Rest, Train, etc.)
- **CommonSelectWindowHandler** - Generic selection windows (items, locations)
- **TradePanelHandler** - Shop buy/sell menus
- **RestaurantPanelHandler** - Restaurant and camp cooking menus
- **TrainingPanelHandler** - Gym training selection (HP, MP, STR, STA, WIS, SPD)
- **ColosseumPanelHandler** - Battle arena selection
- **FarmPanelHandler** - Farm goods management
- **SavePanelHandler** - Save/load game menu

### Field Gameplay Menus (COMPLETE)
All major field menus now have accessibility handlers:
- **FieldItemPanelHandler** - Item use menu with category tabs
- **StoragePanelHandler** - Storage/inventory transfer between panels
- **MapPanelHandler** - World/area map navigation
- **PartnerPanelHandler** - Partner status with multi-tab support (Status, Attack, Tactics, History)
- **ItemPickPanelHandler** - Field item/material pickup
- **MailPanelHandler** - Digital messenger/mail reading
- **DigiviceTopPanelHandler** - Digivice main menu
- **ZonePanelHandler** - Zone/area selection
- **FieldHudHandler** - Partner status via controller combos (RB/LB + face buttons)
- **CarePanelHandler** - Care menu (Square button) with command selection, item tabs (Consumption/Foodstuff/etc.), and proper state detection

## Known Issues

### Steam Big Picture Text Input
- **Problem:** When Steam Big Picture mode is enabled, the game uses Steam's text input overlay for name entry, which is inaccessible
- **Solution:** Disable Steam Big Picture mode before playing
- **Technical Note:** The game checks `uNameInput.m_canShowSteamTextInput` - we attempted to disable this in code but Steam triggers the overlay before our code runs

## Implemented Features

### Title Menu (COMPLETE)
- **Class:** `uTitlePanel`
- **Detection:** `FindObjectOfType<uTitlePanel>()`, check `gameObject.activeInHierarchy`
- **Cursor:** `cursorPosition` (0-3)
- **Items:** Start, Continue, Option, Quit
- **Status:** Fully accessible

### Options Menu System (COMPLETE)
- **Class:** `uOptionPanel`
- **State tracking:** `m_State` (MAIN_SETTING = active), `m_MainSettingState` (which submenu)
- **Sub-panels:** `m_uOptionPanelCommand[]` array indexed by MainSettingState

#### System Menu (TOP) - COMPLETE
- **Class:** `uOptionTopPanelCommand`
- **Items:** System Settings, Graphics, Key Config (+ Back/Quit when in-game)
- **Reading:** `m_CommandInfoArray[dataIndex]` directly (no scrolling)

#### System Settings (OPTION) - COMPLETE
- **Class:** `uOptionPanelCommand`
- **Items:** 7 total (Music Volume, Voice Volume, SFX Volume, Voice Language, Camera Up/Down, Camera L/R, Camera Sensitivity)
- **Visible slots:** 6 (`m_CommandInfoArray.Length`)
- **Key insight:** Different item types have their own text fields!
  - `uOptionPanelItemVoiceLanguage` - name hardcoded, value from `m_languageType.text`
  - `uOptionPanelItemBgmVolume`, `uOptionPanelItemVoiceVolume`, `uOptionPanelItemSeVolume`, `uOptionPanelItemSensitivity` - extend Slider, value from `m_sliderNum.text`
  - `uOptionPanelItemCameraV`, `uOptionPanelItemCameraH` - toggles, value from `item.Value` (0=Normal, 1=Reverse)
  - `uOptionPanelItemVoid` - Back button
- **Reading:** Must use `TryCast<T>()` to check item type, then read from type-specific fields
- **Scrolling:** No explicit scroll field; calculated as `scrollOffset = max(0, dataIndex - (visibleSlots - 1))`

#### Graphics Settings (GRAPHICS) - COMPLETE
- **Class:** `uOptionGraphicsPanelCommand`
- **Items:** Resolution, Screen Mode, Antialiasing, Depth of Field
- **Key difference:** NO `m_CommandInfoArray`, read directly from `m_items`
- **Item class:** `uOptionGraphicPanelItem` with `m_graphicsSettingType` enum and `m_Text` for value

#### Key Config (KEYCONFIG) - COMPLETE
- **Class:** `uOptionKeyConfigPanelCommand`
- **Items:** 20 key bindings
- **Visible slots:** 6 (`m_items.Count`)
- **Scrolling:** Has `m_scrollItemPos` field! Actual item = `scrollPos + dataIndex`
- **Item data:** `m_itemTypeList` (action names), `m_keyConfigList` (KeyCode values)
- **Reading visible slots:** `m_items[dataIndex].m_HeadText.text` for name, `m_items[dataIndex].m_keyCode` for binding

### Difficulty Selection (COMPLETE)
- **Class:** `uDifficultyDialog`
- **Detection:** `FindObjectOfType<uDifficultyDialog>()`, check `m_State == State.Main`
- **Cursor:** `CursorPosition` (int)
- **Items:** `m_difficlutItems.m_difficultText[]` (Text array)
- **State Enum:** None, Main, End
- **Status:** Fully accessible

### Character Selection (COMPLETE)
- **Class:** `uCharaSelectPanel`
- **Detection:** `FindObjectOfType<uCharaSelectPanel>()`, check `gameObject.activeInHierarchy`
- **Selection:** `Gender` (int, 0=Male, 1=Female)
- **Caption:** Uses hardcoded English (game's caption is Japanese even in English version)
- **Status:** Fully accessible

### Digi-Egg Selection (COMPLETE)
- **Class:** `uRebirthPanel`
- **Detection:** `FindObjectOfType<uRebirthPanel>()`, use `IsOpened()` method
- **Selection:** `selectEgg` (int, 0-based index), `eggMax` (total count)
- **Text Fields (LABELS not data):**
  - `m_headLineText` - Shows "L Partner" or "R Partner" label
  - `m_natureText` - Shows "Nature" label
  - `m_attrText` - Shows "Attr" label
  - `m_jijimonText` - Shows "Jijimon" label
  - `m_text` - Actual Jijimon comment about current egg
- **Digivolution Details:** Press confirm to open `uGenealogy` panel which shows actual Digimon info
- **Confirmation:** Uses `uCommonYesNoWindow` for egg selection confirmation
- **Status:** Fully accessible

### Digivolution Details Panel (COMPLETE)
- **Class:** `uGenealogy`
- **Detection:** `FindObjectOfType<uGenealogy>()`, check `gameObject.activeInHierarchy`
- **Text Fields:**
  - `m_name_text` - Digimon name
  - `m_nature_text` - Nature type
  - `m_attr_text` - Attribute type
  - `m_detail_text` - Description/lore
- **State Enum:** Main, ScrollAfter, ScrollBefore
- **Navigation:** Arrow keys to browse evolution tree
- **Status:** Fully accessible

### Dialog Choices (COMPLETE)
- **Class:** `TalkMain` + `EventWindowPanel`
- **Detection:** Check `TalkMain.m_maxChoiceNum > 0`
- **Cursor:** `TalkMain.m_cursor` (0, 1, or 2 for up to 3 choices)
- **Choice Text:** `EventWindowPanel.m_choicesText[]` array
- **Key insight:** m_cursor is the definitive cursor position, NOT visual cursor Y position
- **Status:** Fully accessible

### Common Yes/No Dialog (COMPLETE)
- **Class:** `uCommonYesNoWindow`
- **Detection:** `FindObjectOfType<uCommonYesNoWindow>()`, check `gameObject.activeInHierarchy`
- **Cursor:** `m_cursorIndex` (CursorIndex.Yes = 0, CursorIndex.No = 1)
- **Text Fields:**
  - `m_message` - The confirmation question/prompt
  - `m_yes` - Yes button text
  - `m_no` - No button text
- **Usage:** Appears when selecting eggs, confirming actions
- **Status:** Fully accessible

### Name Entry Screen (COMPLETE)
- **Class:** `NameEntry` (controller), `uNameInput` (UI panel)
- **Detection:** `FindObjectOfType<NameEntry>()`, check `m_state != NONE`
- **Type:** `eType.Player` or `eType.Digitama`
- **State Enum:** NONE, INIT, REQUEST, INPUT, INPUT_END, STEAM_TEXTINPUT, STEAM_TEXTINPUT_END
- **UI Elements:** `m_uNameInput.m_label` (prompt), `m_uNameInput.m_InputField` (input field)
- **Focus tracking:** `isInputFieldSelect` indicates if input field has focus
- **Status:** Fully accessible (requires Steam Big Picture to be disabled)

### Dialog Windows (COMPLETE)
- **Class:** `uDialogBase`
- **Detection:** `FindObjectOfType<uDialogBase>()`, check `activeInHierarchy`
- **Cursor:** `m_cursorIndex` (Yes=0, No=1)
- **Text:** Read from child Text components
- **Status:** Fully accessible

### Message Windows (ENHANCED - IMMEDIATE TEXT WITH VOICE DETECTION)
- **Classes:** `EventWindowPanel` (story), `uCommonMessageWindow` (field), `uDigimonMessagePanel` (partner), `uBattlePanelDialog` (battle), `uCaptionBase` (hints)
- **Detection:** `FindObjectsOfType<T>()`, check `activeInHierarchy`
- **Text Fields:**
  - `EventWindowPanel.m_normalText` - Main story dialog text
  - `EventWindowPanel.m_nameText` - Speaker name
  - `uCommonMessageWindow.m_label` - Field message text
  - `uDigimonMessagePanel.m_text` - Partner status messages
  - `uCaptionBase.m_text` - Field hints/instructions
- **Harmony Patch Integration:**
  - Patches `EventWindowPanel.TextShrink(string text)` - intercepts actual localized text
  - Patches `TalkMain.PlayVoiceText` - detects voiced dialog to skip TTS
  - Text is captured the instant it's set, before typewriter animation starts
  - `DialogTextPatch.OnTextIntercepted` event fires immediately with full text
- **Features:**
  - **Immediate text announcement** - no waiting for typewriter animation
  - **Voice detection** - skips TTS for voiced dialog (500ms timing window)
  - Speaker name tracking from `EventWindowPanel.m_nameText`
  - Filters placeholder/system text (Japanese placeholders, unresolved keys)
  - Filters punctuation-only text ("!", "?!", etc.)
  - Rich text tag removal for clean reading
- **Status:** Enhanced with Harmony patches for instant text reading and voice filtering

## Technical Discoveries

### Harmony Patches for Immediate Text
- Use `HarmonyLib.Harmony` to patch game methods and intercept data
- `EventWindowPanel.TextShrink(string text)` receives actual localized text ready for display
- `TalkMain.DispTextMain(name, text)` receives localization KEYS, not actual text (don't use for TTS)
- `TalkMain.PlayVoiceText` is called when dialog has voice audio - use for voice detection
- Prefix patches capture parameters before the original method runs
- Event-based notification allows handlers to react immediately

### Voice Detection Pattern
- `TalkMain.PlayVoiceText` is called before `EventWindowPanel.TextShrink` for voiced dialog
- Track timestamp of last voice call, then check timing in TextShrink
- 500ms window works well - if voice triggered within 500ms of text, skip TTS

### Dialog Choice Cursor Tracking
- `TalkMain.m_cursor` is the definitive cursor position (0, 1, or 2)
- `TalkMain.m_maxChoiceNum` indicates how many choices are active (0 = no choices)
- `EventWindowPanel.m_choicesText[]` contains the Text components for each choice
- **DO NOT** calculate cursor from visual position - the visual cursor (`UiDispBase`) Y position jumps unpredictably
- Poll TalkMain.m_cursor directly for accurate tracking

### Common Confirmation Dialogs
- `uCommonYesNoWindow` is used throughout the game for Yes/No confirmations
- Has `m_message`, `m_yes`, `m_no` Text fields and `m_cursorIndex` enum
- Opens via `Open(string message, Action<bool> callback)` method
- Called by `uRebirthPanel.SetDialog()` when selecting an egg

### Il2Cpp Type Detection
- `item.GetType().Name` returns base wrapper type, not actual game type
- Must use `TryCast<SpecificType>()` to detect and access derived types
- Check most specific types first, then fall back to base types

### Scrolling Lists Pattern
Two approaches found in game:
1. **No scroll field** (Settings): Calculate offset from `dataIndex` and `visibleSlotCount`
2. **Explicit scroll field** (Key Config): Use `m_scrollItemPos` directly

### UI Slot vs Data Item Mismatch
- `m_CommandInfoArray` contains fixed UI templates
- Special item types (VoiceLanguage) have their own Text components that override slot text
- Always prefer reading from actual item's type-specific fields over CommandInfoArray

## Debug Logging
- **File:** `Mods\DigimonNOAccess_debug.log`
- **Class:** `DebugLogger` with `Log()`, `LogSection()` methods
- Clears on each game start

## Files
- `Main.cs` - Entry point, handler initialization, Harmony setup
- `ScreenReader.cs` - Tolk wrapper for TTS
- `DebugLogger.cs` - File-based debug logging
- `TitleMenuHandler.cs` - Title screen menu accessibility
- `OptionsMenuHandler.cs` - All options submenus accessibility
- `DifficultyDialogHandler.cs` - Difficulty selection accessibility
- `CharaSelectHandler.cs` - Character/gender selection accessibility
- `NameEntryHandler.cs` - Name entry screen accessibility
- `DigiEggHandler.cs` - Digi-Egg selection accessibility (rebirth/new game)
- `GenealogyHandler.cs` - Digivolution Details panel accessibility
- `DialogHandler.cs` - Yes/No dialog accessibility
- `DialogChoiceHandler.cs` - Dialog choices (multiple options in conversation)
- `CommonYesNoHandler.cs` - Common Yes/No confirmation dialog accessibility
- `MessageWindowHandler.cs` - Message window/story text accessibility (uses DialogTextPatch for immediate text, IsFindActive() for proper visibility detection)
- `DialogTextPatch.cs` - Harmony patches for EventWindowPanel.TextShrink and TalkMain.PlayVoiceText
- `SteamInputPatch.cs` - Harmony patch attempt (not used - Steam Big Picture must be disabled manually)
- `AudioNavigationHandler.cs` - Always-on audio navigation (auto-tracks nearest object with positional audio)
- `PositionalAudio.cs` - NAudio-based 3D positional audio system (stereo panning, volume, WAV file loading)
- `WallDetectionHandler.cs` - Always-on wall detection via NavMesh (directional wall sounds)
- `ToneGenerator.cs` - Legacy programmatic audio tone generation (not currently used)
- `CampCommandHandler.cs` - Camp menu accessibility
- `CommonSelectWindowHandler.cs` - Generic selection window accessibility
- `TradePanelHandler.cs` - Shop/trade menu accessibility
- `RestaurantPanelHandler.cs` - Restaurant/cooking menu accessibility
- `TrainingPanelHandler.cs` - Training gym menu accessibility
- `ColosseumPanelHandler.cs` - Colosseum battle selection accessibility
- `FarmPanelHandler.cs` - Farm management menu accessibility
- `SavePanelHandler.cs` - Save/load menu accessibility
- `FieldItemPanelHandler.cs` - Field item use menu accessibility
- `StoragePanelHandler.cs` - Storage/inventory transfer accessibility
- `MapPanelHandler.cs` - Map navigation accessibility
- `PartnerPanelHandler.cs` - Partner status menu accessibility
- `ItemPickPanelHandler.cs` - Item pickup accessibility
- `MailPanelHandler.cs` - Mail/messenger accessibility
- `DigiviceTopPanelHandler.cs` - Digivice main menu accessibility
- `ZonePanelHandler.cs` - Zone selection accessibility
- `FieldHudHandler.cs` - Partner status via controller combos
- `CarePanelHandler.cs` - Care menu accessibility (command selection + education/discipline mode)
- `BattleHudHandler.cs` - Battle partner HP/MP status via D-pad (F3/F4 keyboard fallback)
- `BattleOrderRingHandler.cs` - Order Ring command selection
- `BattleItemHandler.cs` - Battle item menu accessibility
- `BattleDialogHandler.cs` - Battle Yes/No dialog accessibility
- `BattleTacticsHandler.cs` - Square button tactics menu (Escape, MP Usage, Target tabs)
- `BattleResultHandler.cs` - Victory screen and rewards announcement
- `docs/game-api.md` - Documented game API reference
- `battle-system-checklist.md` - Battle system implementation checklist

### Audio Navigation System (ALWAYS-ON)
- **Mode:** Always active when player is in control (no toggle keys)
- **Auto-stops during:** Battles, cutscenes, events, death states
- **Menu pause:** Automatically pauses when Care menu, Digivice, or Save menu is open
- **Classes:** `AudioNavigationHandler`, `PositionalAudio`, `WallDetectionHandler`
- **Detection Sources:**
  - `ItemPickPointManager.m_instance.m_itemPickPoints` - Collectible items (range 100m)
  - `MapTriggerScript` with `enterID == MapChange` - Area transitions (range 80m)
  - `NpcManager.m_NpcCtrlArray` - NPCs (range 120m)
  - `EnemyManager.m_EnemyCtrlArray` - Enemies/wild Digimon (range 150m)
  - `PartnerCtrl` - Partner Digimon (range 200m, fallback)
- **3D Positional Audio (NAudio with Custom Sound Files):**
  - Uses NAudio library to bypass game's CRI audio system (which has 2D-baked sounds)
  - Loads custom WAV files from sounds/ folder for each object type
  - Falls back to generated tones if WAV files not found
  - Continuous audio that updates in real-time as player/target move
  - Stereo panning based on target direction relative to player facing
  - Volume scales with distance (louder when closer)
  - Auto-switches to nearest target every 0.5 seconds
  - Auto-stops when target reached (within 3 meters)
- **Sound Files (sounds/ folder):**
  - `item.wav` - Items
  - `potential npc.wav` - NPCs
  - `potential enemie digimon.wav` - Enemy Digimon
  - `transission.wav` - Area transitions
  - `wall up.wav`, `wall down.wav`, `wall left.wav`, `wall right.wav` - Wall detection
- **Wall Detection (NavMesh-based):**
  - Uses NavMesh.SamplePosition to detect impassable areas (same data game uses for pathfinding)
  - Checks 4 directions (ahead, behind, left, right) relative to player facing
  - Plays directional sounds with stereo panning (left/right walls pan to respective speakers)
  - Volume levels: ahead 0.4, behind 0.5, left/right 0.3 (less harsh)
  - Falls back to raycasting if NavMesh unavailable
- **Player Control Detection:**
  - Checks `uBattlePanel.m_instance.m_enabled` for battle state
  - Checks `PlayerCtrl.actionState` for Event, Battle, Dead, LiquidCrystallization states
  - Checks `MainGameManager.careUI.m_state` for Care menu state
  - Checks `uDigivicePanel.m_instance.m_enabled` for Digivice menu
  - Checks `uSavePanelCommand.m_State` for Save/Load menu
  - Only runs when player is in normal controllable state with no menus open
- **Technical:**
  - NAudio `AudioFileReader` loads WAV files
  - `LoopingWaveProvider` enables continuous playback
  - `PanningSampleProvider` handles stereo positioning
  - `VolumeSampleProvider` controls volume levels
  - Background thread updates audio parameters at 60fps
  - Cross product calculation determines left/right panning
  - Dot product determines front/back positioning

### Audio System Technical Notes
- **Game Audio (CRI Atom):** The game uses CRI Atom middleware. Common SE sounds are baked as 2D at authoring time and cannot be made 3D via code. EV3D and Cry cue sheets are 3D but have limited/unknown cue names.
- **Solution:** NAudio provides Windows-level audio playback, completely independent of the game's audio system. This allows true positional audio simulation via stereo panning.
- **Dependencies:** NAudio NuGet package (NAudio.dll, NAudio.Core.dll, NAudio.WinMM.dll, NAudio.Wasapi.dll deployed to Mods folder)

## Next Steps
1. **Fix D-pad in Battle** - Investigate why D-pad input isn't detected during battle (game may consume it)
2. **Battle Result Improvements** - Read actual item/TP/Bit text from uResultPanelGet
3. **Tactics Tab Names** - Find proper text source for tab names instead of hardcoded strings
4. **Partner Interaction** - Handle talking to partner Digimon outside battle (uPartnerTacticsPanel?)
5. **Battle State Announcements** - Add battle start/victory/defeat announcements
6. **Cutscene Subtitles** - MovieSubtitle class for cutscene accessibility
