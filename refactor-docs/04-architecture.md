# Architecture Analysis - DigimonNOAccess Accessibility Mod

## 1. High-Level Architecture Overview

The mod is a MelonLoader plugin (BepInEx-style) for "Digimon World: Next Order" that provides screen reader accessibility. All source files reside in the project root directory (no subdirectories for source code). The architecture follows a **flat handler pattern**: one central `Main` class instantiates and updates ~40 handler classes, each responsible for making one game panel/screen accessible.

### Module Categories

- **Entry Point**: `Main.cs`
- **Infrastructure/Services**: `ScreenReader.cs`, `ModInputManager.cs`, `InputConfig.cs`, `DebugLogger.cs`
- **Audio Systems**: `PositionalAudio.cs`, `ToneGenerator.cs`, `AudioNavigationHandler.cs`
- **Input Layer**: `SDL2Controller.cs`, `GamepadInputPatch.cs`, `TriggerInput.cs`, `SteamInputPatch.cs`
- **Harmony Patches**: `DialogTextPatch.cs`, `GamepadInputPatch.cs`, `SteamInputPatch.cs`
- **UI Handlers (Menu/Panel)**: 35+ handler classes following `[Feature]Handler` naming
- **Settings**: `ModSettingsHandler.cs`
- **Field Navigation**: `NavigationListHandler.cs`, `AudioNavigationHandler.cs`

---

## 2. Call Graph Between Major Modules

### 2.1 Initialization Chain (Main.OnInitializeMelon)

```
Main.OnInitializeMelon() (Main.cs:60)
  -> DebugLogger.Initialize() (DebugLogger.cs:16)
  -> ModInputManager.Initialize() (ModInputManager.cs:42)
     -> SDL2Controller.Initialize() (SDL2Controller.cs:142)
  -> ScreenReader.Initialize() (ScreenReader.cs:40)
  -> DialogTextPatch.Apply() (DialogTextPatch.cs:65)
  -> GamepadInputPatch.Apply() (GamepadInputPatch.cs:47)
  -> [constructs all 40+ handlers] (Main.cs:102-144)
```

### 2.2 Per-Frame Update Chain (Main.OnUpdate)

```
Main.OnUpdate() (Main.cs:150)
  -> ModInputManager.Update() (ModInputManager.cs:70)
     -> SDL2Controller.Update() (SDL2Controller.cs:217)
  -> [each handler].Update() (Main.cs:159-202)
     -> each handler internally:
        -> reads game UI objects via Il2Cpp
        -> calls ScreenReader.Say() / ScreenReader.SayQueued()
        -> calls ModInputManager.IsActionTriggered()
        -> calls DebugLogger.Log()
  -> HandleGlobalKeys() (Main.cs:208)
     -> ModInputManager.IsActionTriggered()
     -> ScreenReader.Say() / ScreenReader.RepeatLast()
```

### 2.3 Status Announcement Chain (Main.AnnounceCurrentStatus)

```
Main.AnnounceCurrentStatus() (Main.cs:243)
  -> checks ~35 handlers in priority order via IsOpen()/IsActive()
  -> calls AnnounceStatus() on the first active handler
  -> each handler calls ScreenReader.Say()
```

### 2.4 Cross-Handler Dependencies

- `Main.cs:201` - `NavigationListHandler.SetEvolutionActive(_evolutionHandler.IsActive())` -- direct coupling between Main orchestrating data flow between two handlers
- `CommonMessageMonitor.cs:93` - calls `DialogTextPatch.StripRichTextTags()` -- utility dependency
- `DialogTextPatch.cs:196-197` - calls `ScreenReader.Say()` directly from Harmony patch context
- `ModSettingsHandler.cs:29-31` - directly reads/writes `DialogTextPatch.AlwaysReadText` static property
- `AudioNavigationHandler.cs:69` - creates `PositionalAudio` instance internally
- `GamepadInputPatch.cs` - reads from `SDL2Controller` to inject into game's `PadManager`

---

## 3. God Classes and God Functions

### 3.1 Main.cs - GOD CLASS (Lines: 401)

**File**: `C:\Users\Amethyst\projects\digimon world, next order\Main.cs`

**Responsibilities (too many)**:
1. MelonMod lifecycle management (lines 60-148, 392-398)
2. Holds references to ALL 40+ handler instances (lines 14-57)
3. Orchestrates initialization order (lines 60-148)
4. Calls Update() on every handler every frame (lines 150-206)
5. Global hotkey handling (lines 208-241)
6. Status announcement priority routing for ALL handlers (lines 243-390)
7. Cross-handler data flow coordination (line 201)

**AnnounceCurrentStatus() - GOD FUNCTION** (lines 243-390): A 147-line if/else-if chain checking 35+ handlers in priority order. Every new handler requires adding another branch here. This is the single biggest maintenance burden in the codebase.

### 3.2 NavigationListHandler.cs - GOD CLASS (Lines: 1737)

**File**: `C:\Users\Amethyst\projects\digimon world, next order\NavigationListHandler.cs`

**Responsibilities (too many)**:
1. World scanning for NPCs (line 810-843)
2. World scanning for items (line 845-878)
3. World scanning for transitions (line 880-910)
4. World scanning for enemies (line 912-941)
5. World scanning for facilities with 3 strategies (line 943-1092)
6. Name resolution for NPCs via multiple fallback paths (line 1098-1152)
7. Name resolution for enemies via multiple data chain lookups (line 1183-1260)
8. Name resolution for facilities with 3 fallback paths (line 1281-1380)
9. Name resolution for transitions (line 1382-1457)
10. Name resolution for items (line 1154-1181)
11. Category/event cycling UI logic (line 1504-1576)
12. NavMesh path calculation (line 1607-1674)
13. Cardinal direction computation (line 1680-1701)
14. Map change detection and rescan scheduling (line 220-273)
15. Incremental rescanning for async-loaded objects (line 279-564)
16. Periodic refresh for items/enemies (line 617-746)
17. Input handling (line 1463-1502)

This class does scanning, name resolution, navigation state, input handling, path calculation, and announcements all in one 1737-line file.

### 3.3 AudioNavigationHandler.cs - LARGE CLASS (Lines: 822)

**File**: `C:\Users\Amethyst\projects\digimon world, next order\AudioNavigationHandler.cs`

**Responsibilities**:
1. Positional audio tracking of nearest target (lines 113-203)
2. Finding nearest target across 5 object types (lines 205-331)
3. Wall detection via NavMesh (lines 335-425)
4. Playing wall sound effects via NAudio (lines 435-480)
5. Complex game state checks: IsInBattlePhase (lines 490-582), IsGamePausedOrInEvent (lines 587-603), IsInDeathRecovery (lines 610-658), IsPlayerInNonControllableState (lines 664-695), IsPlayerInControl (lines 697-727), IsMenuOpen (lines 729-803)

The "is player in control" logic (6 separate methods, lines 490-803) is duplicated in concept with `NavigationListHandler.IsPlayerInField()` (line 173-213) and `FieldHudHandler.IsPlayerInFieldControl()` (line 51-83).

### 3.4 ModInputManager.cs - LARGE CLASS (Lines: 898)

**File**: `C:\Users\Amethyst\projects\digimon world, next order\ModInputManager.cs`

Contains the `ModInputManager` static class plus 3 additional types (`ActionBindings`, `ControllerButton` enum, `InputBinding` class) all in one file. The button mapping logic (lines 305-520) has massive switch statements duplicated between SDL2 and PadManager paths.

### 3.5 OptionsMenuHandler.cs - LARGE CLASS (Lines: 888)

**File**: `C:\Users\Amethyst\projects\digimon world, next order\OptionsMenuHandler.cs`

**Responsibilities**:
1. Panel state tracking (lines 22-67)
2. Reading settings from 4 different panel types: Top, Settings, Graphics, KeyConfig (lines 366-822)
3. Type-casting and reading from 8+ specific item types (lines 505-667)
4. Debug dumping of panel structure (lines 152-302)
5. KeyCode to string conversion (lines 824-849)

The `DumpPanelStructure` method (lines 152-302) is 150 lines of debug code mixed with production logic.

### 3.6 MessageWindowHandler.cs - LARGE CLASS (Lines: 939)

**File**: `C:\Users\Amethyst\projects\digimon world, next order\MessageWindowHandler.cs`

Handles 5 different message systems (EventWindowPanel, uCommonMessageWindow, uDigimonMessagePanel, uBattlePanelDialog, uCaptionBase) all in one class.

---

## 4. Tight Coupling Between Modules

### 4.1 Main.cs is Coupled to Every Handler

**File**: `C:\Users\Amethyst\projects\digimon world, next order\Main.cs`

Main directly instantiates, stores, and calls Update() on all 40+ handlers (lines 14-57, 102-144, 159-202). It also directly calls IsOpen()/IsActive()/AnnounceStatus() on each handler (lines 243-390). Adding or removing any handler requires modifying Main in 3 places (field declaration, construction, Update call) plus the AnnounceCurrentStatus chain.

### 4.2 Handler-to-Handler Data Flow Through Main

**File**: `C:\Users\Amethyst\projects\digimon world, next order\Main.cs`, line 201

```csharp
_navigationListHandler.SetEvolutionActive(_evolutionHandler.IsActive());
```

NavigationListHandler depends on EvolutionHandler's state, but this dependency is wired explicitly in Main. If more cross-handler dependencies arise, Main becomes the broker for all inter-handler communication.

### 4.3 DialogTextPatch Static State Coupling

**File**: `C:\Users\Amethyst\projects\digimon world, next order\DialogTextPatch.cs`

Multiple classes directly access `DialogTextPatch` static members:
- `ModSettingsHandler.cs:29` reads/writes `DialogTextPatch.AlwaysReadText`
- `Main.cs:223` reads/writes `DialogTextPatch.AlwaysReadText`
- `CommonMessageMonitor.cs:93` calls `DialogTextPatch.StripRichTextTags()`
- `MessageWindowHandler.cs` subscribes to `DialogTextPatch.OnTextIntercepted` event

The static event `OnTextIntercepted` (line 30) and static properties `LatestText`/`HasNewText` are accessed from multiple places without any synchronization guarantees.

### 4.4 SDL2Controller and GamepadInputPatch Coupling

**Files**:
- `C:\Users\Amethyst\projects\digimon world, next order\SDL2Controller.cs`
- `C:\Users\Amethyst\projects\digimon world, next order\GamepadInputPatch.cs`
- `C:\Users\Amethyst\projects\digimon world, next order\ModInputManager.cs`

`GamepadInputPatch` directly reads `SDL2Controller.IsButtonHeld()` and stick values (lines 333-406). `ModInputManager` also reads SDL2Controller directly (lines 305-353) AND reads PadManager (lines 355-406). `ModSettingsHandler.cs:91-95` reads `SDL2Controller` directly bypassing `ModInputManager`. Three different places read controller state independently.

### 4.5 ScreenReader Used as Global Sink

**File**: `C:\Users\Amethyst\projects\digimon world, next order\ScreenReader.cs`

Every handler, every patch, and Main.cs all call `ScreenReader.Say()` directly. There is no announcement queue, priority system, or way to batch/defer announcements. This means:
- Harmony patches (DialogTextPatch) call ScreenReader from patch context (potentially different thread timing)
- Multiple handlers could try to announce simultaneously in the same frame

---

## 5. Missing Abstractions

### 5.1 No Common Handler Interface or Base Class

All 35+ handler classes follow an identical pattern but have NO shared interface or base class:
- `Update()` method
- `IsOpen()` or `IsActive()` method
- `AnnounceStatus()` method
- Internal pattern: track `_wasActive`, detect open/close, detect cursor/state changes

Examples of the duplicated pattern:

**DialogHandler.cs** (lines 32-53):
```csharp
public void Update()
{
    bool isActive = IsOpen();
    if (isActive && !_wasActive) { OnOpen(); }
    else if (!isActive && _wasActive) { OnClose(); }
    else if (isActive) { CheckCursorChange(); }
    _wasActive = isActive;
}
```

**CampCommandHandler.cs** (lines 29-46): Identical pattern.
**CommonSelectWindowHandler.cs** (lines 37-55): Identical pattern.
**TrainingPanelHandler.cs** (lines 45-63): Identical pattern.
**ColosseumPanelHandler.cs**, **FarmPanelHandler.cs**, **SavePanelHandler.cs**, etc.: All identical.

An `IAccessibilityHandler` interface with `Update()`, `IsOpen()`, `AnnounceStatus()`, and `Priority` would allow Main to use a list/registry instead of 40+ individual fields. A `BaseHandler<TPanel>` could provide the common open/close/cursor tracking logic.

### 5.2 No "Player State" Abstraction

Three different classes independently check whether the player is in a controllable field state:

- **AudioNavigationHandler.cs** lines 490-803: `IsInBattlePhase()`, `IsGamePausedOrInEvent()`, `IsInDeathRecovery()`, `IsPlayerInNonControllableState()`, `IsPlayerInControl()`, `IsMenuOpen()` (313 lines total)
- **NavigationListHandler.cs** lines 173-213: `IsPlayerInField()` (40 lines)
- **FieldHudHandler.cs** lines 51-83: `IsPlayerInFieldControl()` (32 lines)

Each has slightly different checks, leading to potential inconsistencies. A shared `GameStateService` or `PlayerStateTracker` would eliminate this duplication.

### 5.3 No Name Resolution Service

Name resolution logic for Digimon/NPC/Enemy/Facility is duplicated across:

- **NavigationListHandler.cs** lines 1098-1380: `GetNpcName()`, `GetEnemyName()`, `GetItemName()`, `ResolveFacilityName()` (282 lines)
- **EvolutionHandler.cs** lines 166-236: `GetBeforeEvolutionName()` which iterates `parameterManager.digimonData` matching by `m_mdlName` -- same pattern as NavigationListHandler.GetNpcName()

Both use the same `ParameterDigimonData.FindBaseIdToModelName()` -> `GetDefaultName()` pattern, and both have the same fallback through `AppMainScript.parameterManager.digimonData` iteration.

### 5.4 No Announcement Builder/Formatter

Every handler builds announcement strings inline with `$"..."` interpolation. Common patterns repeated everywhere:
- `$"{name}, {index + 1} of {total}"` (position format)
- `$"Menu Name. {item}, {position}"` (menu announcement)
- `$"Partner {index + 1}: HP {hp}, MP {mp}"` (stat format)

A shared `AnnouncementBuilder` could standardize these formats.

### 5.5 No Controller Abstraction Layer

Controller input is checked through 3 different paths:
- `ModInputManager.IsActionTriggered()` - configurable actions (used by most handlers)
- `SDL2Controller.IsButtonHeld()` - direct SDL3 access (used by ModSettingsHandler.cs:91-95)
- `Input.GetKeyDown()` - direct Unity input (used by Main.cs:229-236 for F8/F9)

There should be a single input facade so no class needs to know about SDL2Controller directly.

### 5.6 No Game Object Scanner Abstraction

Both `NavigationListHandler` (line 810-941) and `AudioNavigationHandler` (lines 205-331) independently scan for NPCs, items, enemies, and transitions using the same game APIs (`NpcManager.m_NpcCtrlArray`, `ItemPickPointManager.m_instance.m_itemPickPoints`, `EnemyManager.m_EnemyCtrlArray`, `FindObjectsOfType<MapTriggerScript>()`). A shared `WorldScanner` service could eliminate this duplication.

---

## 6. Separation of Concerns Issues

### 6.1 Handlers Mix Data Extraction, Logic, and Presentation

Every handler class combines:
1. **Data extraction** (reading game UI objects via Il2Cpp reflection)
2. **State tracking logic** (open/close detection, cursor tracking)
3. **Presentation/Announcement** (building strings and calling ScreenReader.Say)

For example, `FieldHudHandler.cs`:
- Lines 132-144: Extracts partner name from `panel.m_digimon_name?.text`
- Lines 146-162: Extracts HP/MP data
- Lines 221-251: Combines data, builds string, calls ScreenReader.Say

These three concerns should ideally be separated so that data extraction can be tested independently from announcement formatting.

### 6.2 Debug Logging Mixed with Production Code

**OptionsMenuHandler.cs** lines 152-302: The `DumpPanelStructure()` method is 150 lines of debug instrumentation called from production `CheckStateChange()` (line 126). This debug code should be behind a flag or in a separate debug utility.

**BattleHudHandler.cs** lines 209-297: `AnnouncePartnerHpMp()` has extensive `DebugLogger.Log()` calls documenting which data source was used, mixed into the main logic flow.

### 6.3 Configuration Embedded in Code

**ModInputManager.cs** lines 147-237: Default key bindings are hardcoded in `RegisterDefaultBindings()`. The default config file content is a 95-line string literal (lines 649-745).

**AudioNavigationHandler.cs** lines 20-25: Detection ranges (`ItemRange = 100f`, `NpcRange = 120f`, etc.) are hardcoded constants with no way to configure them.

**NavigationListHandler.cs** lines 76-78: Timing constants (`InitialScanDelay`, `RescanInterval`, `RescanDuration`) are hardcoded.

### 6.4 Multiple Types in Single File

**ModInputManager.cs**: Contains `ModInputManager` (static class), `ActionBindings` (class), `ControllerButton` (enum), and `InputBinding` (class) -- 4 types in one 898-line file.

**ToneGenerator.cs**: Contains `ToneSource` (MonoBehaviour) and `ToneGenerator` (static factory) in one file.

**PositionalAudio.cs**: Contains `PositionalAudio` (IDisposable class) and `LoopingWaveProvider` (IWaveProvider) in one file.

### 6.5 Harmony Patches Directly Call ScreenReader

**DialogTextPatch.cs** lines 196, 251, 278: Harmony prefix/postfix methods call `ScreenReader.Say()` and `ScreenReader.SayQueued()` directly from patch context. This mixes the interception concern (capturing game data) with the presentation concern (announcing to user). The patch should only capture/store data; a handler should decide when and how to announce it.

---

## 7. Summary of Key Findings

### Critical Architecture Issues (High Impact)

1. **Main.cs is a god class** - manually wires 40+ handlers with no abstraction. Every handler addition/removal requires 3+ edits.
   - File: `Main.cs`, lines 14-57 (fields), 102-144 (construction), 159-202 (update), 243-390 (status routing)

2. **No handler interface/base class** - 35+ handlers duplicate the same Update/IsOpen/AnnounceStatus pattern with no shared contract.
   - Example files: `DialogHandler.cs:32-53`, `CampCommandHandler.cs:29-46`, `CommonSelectWindowHandler.cs:37-55`

3. **NavigationListHandler is a 1737-line god class** - scanning, name resolution, navigation, input, path calculation, and announcements in one class.
   - File: `NavigationListHandler.cs`, entire file

### Moderate Architecture Issues (Medium Impact)

4. **Triplicated player state checking** - AudioNavigationHandler (313 lines), NavigationListHandler (40 lines), FieldHudHandler (32 lines) each independently check game state.
   - Files: `AudioNavigationHandler.cs:490-803`, `NavigationListHandler.cs:173-213`, `FieldHudHandler.cs:51-83`

5. **Duplicated name resolution** - NavigationListHandler and EvolutionHandler both resolve Digimon names from model IDs.
   - Files: `NavigationListHandler.cs:1098-1152`, `EvolutionHandler.cs:166-236`

6. **Duplicated world scanning** - NavigationListHandler and AudioNavigationHandler independently scan for items, NPCs, enemies, transitions.
   - Files: `NavigationListHandler.cs:810-941`, `AudioNavigationHandler.cs:205-331`

7. **Controller input accessed inconsistently** - 3 different paths (ModInputManager, SDL2Controller direct, Unity Input direct).
   - Files: `ModSettingsHandler.cs:91-95`, `Main.cs:229-236`, most handlers via `ModInputManager`

### Minor Architecture Issues (Low Impact)

8. **Debug code mixed with production** - `OptionsMenuHandler.DumpPanelStructure()` (150 lines).
   - File: `OptionsMenuHandler.cs:152-302`

9. **Multiple types per file** - ModInputManager.cs has 4 types, ToneGenerator.cs has 2, PositionalAudio.cs has 2.
   - Files: `ModInputManager.cs`, `ToneGenerator.cs`, `PositionalAudio.cs`

10. **Harmony patches directly announce** - DialogTextPatch calls ScreenReader.Say from patch context instead of deferring to handlers.
    - File: `DialogTextPatch.cs:196, 251, 278`

11. **No announcement formatting abstraction** - position/stat/menu announcement formats are repeated inline across all handlers.
    - Example: `$"{name}, {cursor + 1} of {total}"` appears in nearly every handler

---

## 8. Complete File Inventory with Line Counts

### Infrastructure (5 files, ~2,200 lines)
- `Main.cs` (401 lines) - Entry point, god class orchestrator
- `ScreenReader.cs` (153 lines) - Tolk DLL wrapper
- `ModInputManager.cs` (898 lines) - Input system + 3 extra types
- `InputConfig.cs` (277 lines) - Config file parser
- `DebugLogger.cs` (70 lines) - File logger

### Audio Systems (3 files, ~1,590 lines)
- `AudioNavigationHandler.cs` (822 lines) - Always-on positional audio + wall detection
- `PositionalAudio.cs` (549 lines) - NAudio-based 3D audio
- `ToneGenerator.cs` (214 lines) - Unity AudioSource tone generation

### Input/Patches (4 files, ~960 lines)
- `SDL2Controller.cs` (496 lines) - SDL3 controller wrapper
- `GamepadInputPatch.cs` (448 lines) - Harmony patch for controller injection
- `TriggerInput.cs` (141 lines) - Unity axis trigger reader
- `SteamInputPatch.cs` (68 lines) - Steam text input blocker
- `DialogTextPatch.cs` (358 lines) - Harmony text interception

### Field Navigation (1 file, 1,737 lines)
- `NavigationListHandler.cs` (1,737 lines) - Categorized POI navigation

### UI Handlers (35 files, ~9,600 lines)
- `MessageWindowHandler.cs` (939 lines)
- `OptionsMenuHandler.cs` (888 lines)
- `TamerPanelHandler.cs` (787 lines)
- `TrainingPanelHandler.cs` (484 lines)
- `NameEntryHandler.cs` (415 lines)
- `BattleResultHandler.cs` (394 lines)
- `BattleHudHandler.cs` (389 lines)
- `DigiEggHandler.cs` (339 lines)
- `FieldHudHandler.cs` (335 lines)
- `PartnerPanelHandler.cs` (346 lines)
- `SavePanelHandler.cs` (329 lines)
- `GenealogyHandler.cs` (318 lines)
- `FieldItemPanelHandler.cs` (310 lines)
- `DialogChoiceHandler.cs` (309 lines)
- `TitleMenuHandler.cs` (277 lines)
- `CommonYesNoHandler.cs` (242 lines)
- `StoragePanelHandler.cs` (239 lines)
- `TradePanelHandler.cs` (232 lines)
- `FarmPanelHandler.cs` (231 lines)
- `ColosseumPanelHandler.cs` (228 lines)
- `MailPanelHandler.cs` (227 lines)
- `RestaurantPanelHandler.cs` (222 lines)
- `CarePanelHandler.cs` (213 lines)
- `ModSettingsHandler.cs` (210 lines)
- `BattleTacticsHandler.cs` (209 lines)
- `MapPanelHandler.cs` (205 lines)
- `BattleOrderRingHandler.cs` (201 lines)
- `EvolutionHandler.cs` (267 lines)
- `DifficultyDialogHandler.cs` (187 lines)
- `TrainingResultHandler.cs` (177 lines)
- `ItemPickPanelHandler.cs` (173 lines)
- `DialogHandler.cs` (164 lines)
- `BattleItemHandler.cs` (164 lines)
- `CampCommandHandler.cs` (162 lines)
- `CommonSelectWindowHandler.cs` (172 lines)
- `CommonMessageMonitor.cs` (152 lines)
- `DigiviceTopPanelHandler.cs` (142 lines)
- `BattleDialogHandler.cs` (121 lines)
- `CharaSelectHandler.cs` (121 lines)
- `ZonePanelHandler.cs` (116 lines)
- `TrainingBonusHandler.cs` (154 lines)
