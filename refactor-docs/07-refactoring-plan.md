# 07 - Refactoring Plan

Synthesized from discovery documents 01-06. All changes preserve existing behavior.

## Status: COMPLETED

All 4 priority levels implemented across 14 commits. See `08-refactoring-summary.md` for full details.

- Priority 1: Safe Fixes -- COMPLETED (items 1.1-1.9)
- Priority 2: Code Reuse -- COMPLETED (items 2.1-2.4)
- Priority 3: Structural Improvements -- COMPLETED (items 3.1-3.4)
- Priority 4: Cleanup -- COMPLETED (items 4.1-4.2, 4.3 moot after SteamInputPatch deletion)

Deferred items documented in the summary.

---

## Priority 1: Safe Fixes (no behavior change)

### 1.1 Remove Dead Code in MessageWindowHandler

- **What:** Delete the commented-out `UpdateCommonMessageWindow()` and `UpdateDigimonMessagePanel()` call sites (lines 91-96), and remove all the dead methods they reference: `UpdateCommonMessageWindow` (lines 372-390), `OnCommonWindowOpen` (lines 392-399), `OnCommonWindowClose` (lines 401-407), `CheckCommonMessageChange` (lines 409-437), `UpdateDigimonMessagePanel` (lines 485-503), `OnDigimonPanelOpen` (lines 505-518), `OnDigimonPanelClose` (lines 520-524), `CheckDigimonMessageChange` (lines 526-537), `GetDigimonPanelText` (lines 539-556). Also remove the dead field `_commonMessageWindow` (line 30) and its tracking fields `_wasCommonActive` (line 30), `_lastCommonText` (line 31), `_wasDigimonPanelActive` (line 38), `_lastDigimonText` (line 39).
- **Why:** ~180 lines of unreachable code that were disabled for causing duplicate announcements. Removing them makes the 939-line file significantly more readable.
- **Files affected:** `MessageWindowHandler.cs`
- **Risk:** Low -- code is already commented out / unreachable
- **Tests:** None exist

### 1.2 Remove Dead Methods in BattleHudHandler

- **What:** Delete `AnnouncePartnerName()` (lines 194-207) and `AnnouncePartnerOrderPower()` (lines 321-339). Neither is called from anywhere in the codebase.
- **Why:** ~30 lines of unreachable code.
- **Files affected:** `BattleHudHandler.cs`
- **Risk:** Low -- grep confirms zero callers
- **Tests:** None exist

### 1.3 Remove Dead Debug Method DebugLogAllAxes in TriggerInput

- **What:** Delete `DebugLogAllAxes()` (lines 110-139) which is never called. Remove the `_initialized` field (line 23) that only controls a single log message and has no functional impact.
- **Why:** 30 lines of uncalled debug code.
- **Files affected:** `TriggerInput.cs`
- **Risk:** Low
- **Tests:** None exist

### 1.4 Remove Unused Temp Variables in SDL2Controller

- **What:** Remove unused `var temp = _lastButtonStates;` (line 248) and `var tempAxis = _lastAxisStates;` (line 252).
- **Why:** Assigned but never read.
- **Files affected:** `SDL2Controller.cs`
- **Risk:** Low
- **Tests:** None exist

### 1.5 Remove SteamInputPatch Dead Code

- **What:** `SteamInputPatch.cs` is never called from `Main.cs` or any other file. Delete the file entirely.
- **Why:** The class has `Initialize()` and `Shutdown()` methods but neither is invoked. Entire file is dead code.
- **Files affected:** `SteamInputPatch.cs` (deleted), possibly `DigimonNOAccess.csproj` if it's explicitly listed
- **Risk:** Low -- verified no callers anywhere in codebase
- **Tests:** None exist

### 1.6 Remove Redundant IsActive() Wrappers

- **What:** Remove `IsActive()` from `TrainingBonusHandler.cs` (lines 149-153) and `TrainingResultHandler.cs` (lines 172-176) which just call `IsOpen()`. Update any callers (check `Main.cs` `AnnounceCurrentStatus()`) to use `IsOpen()` instead.
- **Why:** Redundant wrapper adds confusion about which method to call.
- **Files affected:** `TrainingBonusHandler.cs`, `TrainingResultHandler.cs`, `Main.cs`
- **Risk:** Low -- simple delegation replacement
- **Tests:** None exist

### 1.7 Remove DumpPanelStructure Debug Method from OptionsMenuHandler

- **What:** Delete `DumpPanelStructure()` (lines 152-302, ~150 lines) and remove its call from `CheckStateChange()` (line 127).
- **Why:** 150 lines of debug-only panel structure dumping with no accessibility function. Pollutes production code.
- **Files affected:** `OptionsMenuHandler.cs`
- **Risk:** Low -- debug logging only, no functional impact
- **Tests:** None exist

### 1.8 Remove Unused `using` Directives

- **What:** Remove `using MelonLoader;` from 24 files where it's imported but never referenced. Remove `using UnityEngine.UI;` from `CharaSelectHandler.cs`.
- **Why:** Code cleanliness; unused imports add confusion about actual dependencies.
- **Files affected:** `CampCommandHandler.cs`, `CarePanelHandler.cs`, `CharaSelectHandler.cs`, `ColosseumPanelHandler.cs`, `CommonSelectWindowHandler.cs`, `DialogHandler.cs`, `DifficultyDialogHandler.cs`, `DigiviceTopPanelHandler.cs`, `FarmPanelHandler.cs`, `FieldItemPanelHandler.cs`, `ItemPickPanelHandler.cs`, `MailPanelHandler.cs`, `MapPanelHandler.cs`, `MessageWindowHandler.cs`, `NameEntryHandler.cs`, `PartnerPanelHandler.cs`, `RestaurantPanelHandler.cs`, `SavePanelHandler.cs`, `StoragePanelHandler.cs`, `TitleMenuHandler.cs`, `TradePanelHandler.cs`, `TrainingPanelHandler.cs`, `ZonePanelHandler.cs`, `CharaSelectHandler.cs` (also `using UnityEngine.UI;`)
- **Risk:** Low -- compiler will catch any that were actually needed
- **Tests:** None exist

### 1.9 Remove Accidental `nul` File

- **What:** Delete the `nul` file in the project root (likely created accidentally from a Windows `> nul` redirect).
- **Why:** Not a valid project file.
- **Files affected:** `nul`
- **Risk:** Low
- **Tests:** N/A

---

## Priority 2: Code Reuse (minimal behavior change)

### 2.1 Create TextUtilities Static Class

- **What:** Create `TextUtilities.cs` containing:
  - `StripRichTextTags(string text)` -- regex `<[^>]+>` removal (currently in DialogTextPatch:327, MessageWindowHandler:890, BattleDialogHandler:108)
  - `CleanText(string text)` -- strips tags + normalizes whitespace (MessageWindowHandler:884-895)
  - `IsPlaceholderText(string text)` -- consolidated check for placeholder characters and localization keys (DialogTextPatch:330-356, CommonMessageMonitor:124-150)
  - `IsLocalizationReady()` -- `try { return Localization.isActive; } catch { return false; }` (TitleMenuHandler:103-113, CommonMessageMonitor:98-106)
  - `IsGameLoading()` -- game loading state check (DialogTextPatch:122-134, CommonMessageMonitor:108-122)
- **Why:** 3 separate implementations of rich text stripping, 2 of placeholder detection, 2 of localization checks, and 2 of game loading checks. Consolidation ensures consistent behavior and single maintenance point.
- **Files affected:** New file `TextUtilities.cs`; updates to `DialogTextPatch.cs`, `MessageWindowHandler.cs`, `BattleDialogHandler.cs`, `CommonMessageMonitor.cs`, `TitleMenuHandler.cs`
- **Risk:** Low -- pure extraction of identical logic
- **Tests:** None exist

### 2.2 Create PartnerUtilities Static Class

- **What:** Create `PartnerUtilities.cs` containing:
  - `GetPartnerLabel(int partnerIndex)` -- returns "Partner 1" or "Partner 2"
  - `GetPartnerNotAvailableMessage(int partnerIndex)` -- returns "Partner N not available"
  - `GetStatusEffectText(FieldStatusEffect effect, string noneText = "Healthy", string unknownText = "Unknown status")` -- consolidated status effect switch
  - `static readonly string[] StatNames = { "HP", "MP", "STR", "STA", "WIS", "SPD" }`
  - `static readonly string[] StatNamesWithFatigue` (adds "Fatigue")
- **Why:** Partner label ternary appears 7 times across 3 files. Status effect switch appears 3 times in FieldHudHandler. Stat names array appears 3 times in 2 files.
- **Files affected:** New file `PartnerUtilities.cs`; updates to `BattleHudHandler.cs`, `FieldHudHandler.cs`, `TrainingPanelHandler.cs`, `BattleResultHandler.cs`, `TrainingResultHandler.cs`
- **Risk:** Low -- pure extraction of duplicated values and expressions
- **Tests:** None exist

### 2.3 Extend DebugLogger with Warning/Error Methods and Unify Logging

- **What:** Add `Warning(string message)` and `Error(string message)` to `DebugLogger.cs`. Replace all `Melon<Main>.Logger.Msg/Warning/Error()` calls in `OptionsMenuHandler.cs` and `ScreenReader.cs` with `DebugLogger.Log/Warning/Error()`.
- **Why:** Two files use `Melon<Main>.Logger` while all others use `DebugLogger`. Standardizing to one system ensures all log output goes to the same file and follows the same format.
- **Files affected:** `DebugLogger.cs`, `OptionsMenuHandler.cs`, `ScreenReader.cs`
- **Risk:** Low -- logging output changes location but not functionality
- **Tests:** None exist

### 2.4 Create AnnouncementBuilder Utility

- **What:** Create `AnnouncementBuilder.cs` with:
  - `CursorPosition(string itemText, int cursor, int total)` -- `$"{itemText}, {cursor + 1} of {total}"`
  - `MenuOpen(string menuName, string itemText, int cursor, int total)` -- `$"{menuName}. {itemText}, {cursor + 1} of {total}"`
  - `FallbackItem(string prefix, int index)` -- `$"{prefix} {index + 1}"`
- **Why:** The cursor position format appears in 15+ handlers. Centralizing ensures consistent format and makes future localization easier.
- **Files affected:** New file `AnnouncementBuilder.cs`; updates to `RestaurantPanelHandler.cs`, `TradePanelHandler.cs`, `CampCommandHandler.cs`, `CommonSelectWindowHandler.cs`, `TitleMenuHandler.cs`, `SavePanelHandler.cs`, `MapPanelHandler.cs`, `DigiviceTopPanelHandler.cs`, and ~7 more handlers
- **Risk:** Low -- pure string formatting extraction
- **Tests:** None exist

---

## Priority 3: Structural Improvements (architecture changes)

### 3.1 Create IAccessibilityHandler Interface

- **What:** Create `IAccessibilityHandler.cs` with:
  - `void Update()`
  - `bool IsOpen()`
  - `void AnnounceStatus()`
  - `int Priority { get; }` -- lower value = checked first for status announcements
- **Why:** All 35+ handlers follow the same contract but have no shared type. This interface enables the handler registry pattern and eliminates the god-function in Main.cs.
- **Files affected:** New file `IAccessibilityHandler.cs`; all handler files (add `: IAccessibilityHandler`); `Main.cs` (use `List<IAccessibilityHandler>`)
- **Risk:** Medium -- all handlers must correctly implement the interface; Main.cs update loop and status routing change significantly
- **Tests:** None exist

### 3.2 Create HandlerBase<TPanel> Generic Base Class

- **What:** Create `HandlerBase.cs` with:
  - Generic `TPanel` parameter for the panel type
  - `_panel`, `_wasActive`, `_lastCursor` protected fields
  - Final `Update()` implementing the lifecycle pattern (IsOpen -> OnOpen/OnClose/OnUpdate)
  - Virtual `IsOpen()` with standard FindObjectOfType + null check + active check
  - Virtual `OnOpen()`, `OnClose()`, `OnUpdate()` for subclass overrides
  - Abstract `AnnounceStatus()`
  - Abstract `string LogTag { get; }` or pass via constructor
  - Implements `IAccessibilityHandler`
- **Why:** 25+ handlers duplicate the identical lifecycle pattern (6-10 lines each = 150-250 lines total). The base class eliminates this boilerplate and enforces consistent behavior.
- **Files affected:** New file `HandlerBase.cs`; 25+ handler files refactored to extend base class
- **Risk:** Medium -- each handler's Update/IsOpen/OnClose must be verified to match the base class pattern or correctly override it. Some handlers (BattleDialogHandler, SavePanelHandler) have variant Update logic that needs careful override.
- **Tests:** None exist

### 3.3 Refactor Main.cs to Use Handler Registry

- **What:** Replace 40+ individual handler fields with `List<IAccessibilityHandler>`. Replace 40+ Update() calls with a foreach loop. Replace the 147-line `AnnounceCurrentStatus()` if/else-if chain with a priority-sorted loop.
- **Why:** Main.cs is a god class that must be edited in 3+ places for every handler added/removed. A registry pattern reduces it to a single `_handlers.Add(new XHandler())` call.
- **Files affected:** `Main.cs`
- **Risk:** Medium -- the priority ordering in AnnounceCurrentStatus must be preserved. Some handlers (BattleHudHandler, FieldHudHandler, AudioNavigationHandler, NavigationListHandler) have special needs (they're not standard panel handlers) that may need separate handling.
- **Tests:** None exist

### 3.4 Create GameStateService for Player State Checks

- **What:** Create `GameStateService.cs` consolidating player/game state detection:
  - `IsInBattle()`, `IsGamePaused()`, `IsInEvent()`, `IsInDeathRecovery()`, `IsPlayerControllable()`, `IsPlayerInField()`, `IsMenuOpen()`
  - Sourced from the three existing implementations: AudioNavigationHandler (313 lines), NavigationListHandler (40 lines), FieldHudHandler (32 lines)
- **Why:** Three classes independently check overlapping game states with slightly different logic, creating potential inconsistencies. A unified service ensures consistent answers.
- **Files affected:** New file `GameStateService.cs`; updates to `AudioNavigationHandler.cs`, `NavigationListHandler.cs`, `FieldHudHandler.cs`, `MessageWindowHandler.cs`
- **Risk:** Medium -- the three implementations have slight differences that must be carefully reconciled. AudioNavigationHandler has the most comprehensive checks and should be the primary source.
- **Tests:** None exist

---

## Priority 4: Cleanup

### 4.1 Standardize Exception Handling Patterns

- **What:** Establish and apply consistent rules:
  - `IsOpen()` / panel detection methods: silent `catch { }` is acceptable (panel may not exist)
  - Data extraction methods (`GetMenuItemText`, `GetCursorPosition`, etc.): log with `DebugLogger.Log()` including the exception message
  - Always use `System.Exception` (not bare `Exception`) for consistency
  - Never leave catch blocks completely empty in data-extraction methods
- **Why:** 58+ silent catches vs 25+ logged catches. Inconsistent exception handling makes debugging harder.
- **Files affected:** 20+ handler files, `SDL2Controller.cs`, `ScreenReader.cs`
- **Risk:** Low -- only changes logging, not control flow
- **Tests:** None exist

### 4.2 Standardize Naming Conventions

- **What:**
  - Cursor tracking fields: standardize to `_lastCursor` (primary), `_lastCursorLeft`/`_lastCursorRight` (dual-panel)
  - Active state fields: standardize to `_wasActive`
  - Rename `_wasEnabled` (BattleResultHandler), `_wasUsable` (TitleMenuHandler), `_wasChoicesActive` (DialogChoiceHandler) to `_wasActive`
  - Rename `_lastCursorIndex` (BattleDialogHandler), `_lastCursorPosition` (DialogChoiceHandler), `_lastCommandIndex` (DigiviceTopPanelHandler) to `_lastCursor`
  - Rename `SDL2Controller` -> `SDLController` (class is already wrapping SDL3, version-neutral name is better)
  - Rename `ModInputManager.IsUsingSDL2` -> `ModInputManager.IsUsingSDL`
- **Why:** Inconsistent naming for the same concepts makes code harder to read and navigate.
- **Files affected:** `BattleResultHandler.cs`, `TitleMenuHandler.cs`, `DialogChoiceHandler.cs`, `BattleDialogHandler.cs`, `DigiviceTopPanelHandler.cs`, `SDL2Controller.cs` (renamed to `SDLController.cs`), `ModInputManager.cs`, `GamepadInputPatch.cs`, `Main.cs`, `ModSettingsHandler.cs`
- **Risk:** Medium -- renaming SDL2Controller affects multiple files and the class name is referenced from GamepadInputPatch, ModInputManager, Main, and ModSettingsHandler. Must be done as a coordinated change.
- **Tests:** None exist

### 4.3 Fix Harmony Patch ID Inconsistency

- **What:** Standardize Harmony patch IDs. Currently `Main.cs:85` uses `"com.digimonoaccess.patches"` and `SteamInputPatch.cs:19` uses `"com.accessibility.digimonno.steaminput"` (different naming scheme). Since SteamInputPatch is being deleted in 1.5, this may be moot. Verify no other inconsistent IDs exist.
- **Why:** Harmony IDs should follow a consistent pattern for the mod.
- **Files affected:** Potentially `Main.cs` only (if SteamInputPatch is already deleted)
- **Risk:** Low
- **Tests:** None exist

---

## Implementation Order and Dependencies

```
Priority 1 (Safe Fixes):
  1.1 through 1.9 can all be done in parallel (independent file changes)

Priority 2 (Code Reuse):
  2.1 TextUtilities - no dependencies
  2.2 PartnerUtilities - no dependencies
  2.3 Unified Logging - no dependencies
  2.4 AnnouncementBuilder - no dependencies
  (All can be done in parallel)

Priority 3 (Structural):
  3.1 IAccessibilityHandler - no dependencies
  3.2 HandlerBase<TPanel> - depends on 3.1
  3.3 Main.cs Registry - depends on 3.1 and 3.2
  3.4 GameStateService - no dependencies (can parallel with 3.1/3.2)

Priority 4 (Cleanup):
  4.1 Exception handling - should follow Priority 2 (logging must be unified first)
  4.2 Naming conventions - should be done last (after structural changes to avoid merge conflicts)
  4.3 Harmony IDs - trivial, do anytime
```

---

## Items Explicitly Deferred

### Deferred: WorldScanner Service (from Proposal 10)
- **Reason:** High complexity, requires both NavigationListHandler and AudioNavigationHandler to change their scanning approach. These are the two most complex files (1737 and 822 lines). Better to tackle after the handler base class and game state service are stable.

### Deferred: DigimonNameResolver Service (from Proposal 9)
- **Reason:** Only 2 files affected, and NavigationListHandler's name resolution is tightly coupled to its scanning logic. Better to address when NavigationListHandler is broken apart in a future refactor.

### Deferred: Breaking Apart NavigationListHandler (from Architecture findings)
- **Reason:** 1737-line god class needs to be split into scanner, name resolver, path calculator, and UI handler. This is the largest single refactoring task and should be its own dedicated effort after the foundation (base class, game state service, utilities) is in place.

### Deferred: Breaking Apart AudioNavigationHandler
- **Reason:** 822 lines with complex game state checks. Will benefit from GameStateService (Priority 3.4) but full decomposition should follow NavigationListHandler refactor.

### Deferred: Constants Class (from Proposal 12)
- **Reason:** 20+ files with small scattered changes. Better to introduce constants incrementally as files are touched for other reasons, rather than a single massive PR.

### Deferred: Test Framework Introduction
- **Reason:** No test framework exists. Adding one requires architectural changes (interfaces for static classes, dependency injection) that go beyond this refactoring scope. The interface extraction in Priority 3 lays the groundwork for future testability.

---

**STOP: This plan requires review and explicit approval before proceeding to implementation.**
