# Reuse Proposals (Phase 2, Agent 5)

Proposals for eliminating duplicated code through shared utilities, base classes, and service abstractions. Each proposal is verified against the actual source files.

---

## Proposal 1: Handler Base Class with Lifecycle Management

### [HandlerBase<TPanel> Generic Base Class]
- Current locations:
  - RestaurantPanelHandler.cs:12-15 (fields), 17-37 (IsOpen), 40-58 (Update), 83-89 (OnClose)
  - SavePanelHandler.cs:12-15 (fields), 22-36 (IsOpen), 38-64 (Update), 82-89 (OnClose)
  - StoragePanelHandler.cs:12-16 (fields), 18-29 (IsOpen), 31-49 (Update), 67-74 (OnClose)
  - TradePanelHandler.cs:12-15 (fields), 17-29 (IsOpen), 31-50 (Update), 74-79 (OnClose)
  - TrainingBonusHandler.cs:12-15 (fields), 17-37 (IsOpen), 40-58 (Update), 80-86 (OnClose)
  - TrainingResultHandler.cs:12-14 (fields), 16-35 (IsOpen), 37-56 (Update), 71-76 (OnClose)
  - CampCommandHandler.cs:12-14 (fields), 16-26 (IsOpen), 28-46 (Update), 66-71 (OnClose)
  - CommonSelectWindowHandler.cs:13-15 (fields), 17-35 (IsOpen), 37-55 (Update), 75-80 (OnClose)
  - 17+ additional handlers follow the identical pattern
- Existing reusable code: None
- Proposed solution: Create generic base class that encapsulates the lifecycle pattern
- Proposed name: `HandlerBase<TPanel>` where `TPanel : UnityEngine.Object`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\HandlerBase.cs`
- Parameters:
  - Generic type parameter `TPanel` for the panel type
  - `string LogTag` (abstract property, e.g. "[RestaurantPanel]")
  - `bool IsOpen()` (virtual, override for custom open detection)
  - `void OnOpen()` (virtual, called when panel first detected as open)
  - `void OnClose()` (virtual, called when panel detected as closed -- base resets `_panel = null`, `_lastCursor = -1`)
  - `void OnUpdate()` (virtual, called each frame while panel is open)
  - `void AnnounceStatus()` (abstract, required by all handlers)
  - Protected fields: `TPanel _panel`, `bool _wasActive`, `int _lastCursor`
  - Protected method: `TPanel FindPanel()` -- wraps `Object.FindObjectOfType<TPanel>()`
  - The `Update()` method is final (non-virtual) and implements the lifecycle:
    ```
    bool isActive = IsOpen();
    if (isActive && !_wasActive) OnOpen();
    else if (!isActive && _wasActive) OnClose();
    else if (isActive) OnUpdate();
    _wasActive = isActive;
    ```
- Return type: N/A (base class)
- Estimated impact: 25+ handler files, eliminates ~6-10 lines of boilerplate per file (150-250 lines total)

**Notes:**
- Some handlers have variant Update logic (SavePanelHandler checks `_pendingOpenAnnouncement` inside the `isActive` branch; BattleDialogHandler has custom logic with `ResetState` instead of `OnClose`). These can override `OnUpdate()` or `Update()` as needed.
- Handlers that also track a `_lastState` enum (RestaurantPanelHandler, SavePanelHandler, TradePanelHandler, TrainingBonusHandler) can add that field in the subclass; a `HandlerWithStateBase<TPanel, TState>` variant could be considered but may be over-engineering.

---

## Proposal 2: IAccessibilityHandler Interface for Handler Registry

### [IAccessibilityHandler Interface]
- Current locations:
  - Main.cs:14-57 (40+ individual handler field declarations)
  - Main.cs:102-144 (40+ individual constructor calls)
  - Main.cs:159-202 (40+ individual Update() calls)
  - Main.cs:243-390 (35+ if/else-if branches for AnnounceCurrentStatus)
- Existing reusable code: None
- Proposed solution: Create interface that all handlers implement, allowing Main to use a list
- Proposed name: `IAccessibilityHandler`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\IAccessibilityHandler.cs`
- Parameters:
  - `void Update()` -- per-frame update
  - `bool IsOpen()` -- is this handler's panel currently active
  - `void AnnounceStatus()` -- speak current state for status query
  - `int Priority { get; }` -- determines AnnounceCurrentStatus ordering (lower = checked first)
- Return type: N/A (interface)
- Estimated impact: Main.cs (complete restructuring of the 401-line file), 35+ handler files (add interface declaration)

**Notes:**
- Main.cs can replace 40+ fields with `List<IAccessibilityHandler>`, replace 40+ Update calls with a loop, and replace the 147-line if/else-if chain with a sorted priority loop.
- Some handlers (BattleHudHandler, FieldHudHandler, AudioNavigationHandler) do not follow the standard pattern and may need special handling. These could implement the interface but return low priority or be kept as separate fields.
- The `Priority` property replaces the implicit ordering in the if/else-if chain.

---

## Proposal 3: TextUtilities Static Class

### [TextUtilities -- Shared Text Cleaning and Filtering]
- Current locations:
  - Rich text stripping:
    - DialogTextPatch.cs:323-328 (`StripRichTextTags` -- public static, `Regex.Replace(text, @"<[^>]+>", "")`)
    - MessageWindowHandler.cs:884-895 (`CleanText` -- private, same regex plus whitespace normalization)
    - BattleDialogHandler.cs:102-114 (`CleanText` -- private, same regex)
  - Placeholder text detection:
    - DialogTextPatch.cs:330-356 (`IsPlaceholderText` -- checks `■□`, `EV_/SYS_/MSG_`, punctuation-only)
    - CommonMessageMonitor.cs:124-150 (`ShouldSkipText` -- checks `■□`, `ランゲージ`, `EV_/SYS_/MSG_`, HTML tags)
    - MessageWindowHandler.cs:852-863 (`IsIgnoredText` -- checks pattern array including `メッセージ入力欄`, `Warning`, `©`)
  - Localization ready check:
    - TitleMenuHandler.cs:103-113 (`IsLocalizationReady`)
    - CommonMessageMonitor.cs:98-106 (`IsLocalizationReady`)
  - Game loading check:
    - DialogTextPatch.cs:122-134 (`IsGameLoading`)
    - CommonMessageMonitor.cs:108-122 (`IsGameLoading`)
- Existing reusable code: `DialogTextPatch.StripRichTextTags()` is already public static and called from CommonMessageMonitor.cs:93
- Proposed solution: Extract all text utility functions into a single static utility class
- Proposed name: `TextUtilities`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\TextUtilities.cs`
- Parameters:
  - `static string StripRichTextTags(string text)` -- removes `<...>` tags
  - `static string CleanText(string text)` -- strips rich text, normalizes whitespace, trims
  - `static bool IsPlaceholderText(string text)` -- consolidated check for `■□`, `EV_/SYS_/MSG_`, punctuation-only, `ランゲージ`
  - `static bool IsLocalizationReady()` -- `try { return Localization.isActive; } catch { return false; }`
  - `static bool IsGameLoading()` -- `MainGameManager.m_instance?._IsLoad() ?? false`
- Return type: Various (see above)
- Estimated impact: 5 files directly (DialogTextPatch, MessageWindowHandler, BattleDialogHandler, CommonMessageMonitor, TitleMenuHandler), additional callers benefit

---

## Proposal 4: GameStateService for Player State Checks

### [GameStateService -- Unified Player/Game State Querying]
- Current locations:
  - AudioNavigationHandler.cs:490-582 (`IsInBattlePhase` -- 92 lines)
  - AudioNavigationHandler.cs:587-603 (`IsGamePausedOrInEvent` -- 16 lines)
  - AudioNavigationHandler.cs:610-658 (`IsInDeathRecovery` -- 48 lines)
  - AudioNavigationHandler.cs:664-695 (`IsPlayerInNonControllableState` -- 31 lines)
  - AudioNavigationHandler.cs:697-727 (`IsPlayerInControl` -- 30 lines composite)
  - AudioNavigationHandler.cs:729-803 (`IsMenuOpen` -- 74 lines)
  - NavigationListHandler.cs:173-213 (`IsPlayerInField` -- 40 lines)
  - FieldHudHandler.cs:51-83 (`IsPlayerInFieldControl` -- 32 lines)
  - MessageWindowHandler.cs:745-761 (`IsBattleResultActive` -- 16 lines)
  - MessageWindowHandler.cs:767-781 (`IsTrainingPanelActive` -- 14 lines)
- Existing reusable code: None (each class reimplements overlapping checks independently)
- Proposed solution: Create shared static service class consolidating all game-state detection
- Proposed name: `GameStateService`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\GameStateService.cs`
- Parameters:
  - `static bool IsInBattle()` -- checks uBattlePanel, MainGameComponent step, partner states
  - `static bool IsInBattleResultPhase()` -- checks uBattlePanel.m_result.m_enabled
  - `static bool IsGamePaused()` -- checks MainGameManager.m_isPause
  - `static bool IsInEvent()` -- checks player ActionState_Event
  - `static bool IsInDeathRecovery()` -- checks MainGameField.STEP.RestartLose/RestartEscape
  - `static bool IsPlayerControllable()` -- composite check excluding battle, event, death, pause
  - `static bool IsPlayerInField()` -- MainGameComponent.m_CurStep == Field AND player controllable
  - `static bool IsMenuOpen()` -- checks DigivicePanel, camp, training, other UI panels
  - `static bool IsTrainingActive()` -- checks uTrainingPanelCommand state
  - Internal: caches `PlayerCtrl` reference, refreshes each frame via `Update()` called from Main
- Return type: Various booleans (see above)
- Estimated impact: 4 files directly (AudioNavigationHandler, NavigationListHandler, FieldHudHandler, MessageWindowHandler), reduces ~350 lines of duplicated state-checking logic

**Notes:**
- AudioNavigationHandler has the most comprehensive checks (313 lines). The service should be a superset of all three implementations.
- NavigationListHandler and FieldHudHandler check overlapping but not identical states. The service methods should be composable so callers pick the exact checks they need.
- Consider caching results per-frame to avoid redundant FindObjectOfType calls.

---

## Proposal 5: PartnerUtilities for Partner Label and Availability

### [PartnerUtilities -- Shared Partner Label/Availability Functions]
- Current locations:
  - Partner label:
    - BattleHudHandler.cs:205 (`partnerIndex == 0 ? "Partner 1" : "Partner 2"`)
    - BattleHudHandler.cs:294 (same)
    - BattleHudHandler.cs:317 (same)
    - BattleHudHandler.cs:337 (same)
    - BattleHudHandler.cs:350 (same)
    - TrainingPanelHandler.cs:302 (same)
    - FieldHudHandler.cs:142 (same, with `partnerLabel` local)
  - Partner not available:
    - BattleHudHandler.cs:199, 217, 304, 327, 346 (`$"Partner {partnerIndex + 1} not available"`)
    - FieldHudHandler.cs:137, 151, 169, 193, 226 (same)
  - Stat names array:
    - BattleResultHandler.cs:236 (`{ "HP", "MP", "STR", "STA", "WIS", "SPD" }`)
    - BattleResultHandler.cs:276 (same)
    - TrainingResultHandler.cs:145 (same + "Fatigue")
- Existing reusable code: None
- Proposed solution: Extract into shared static utility class
- Proposed name: `PartnerUtilities`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\PartnerUtilities.cs`
- Parameters:
  - `static string GetPartnerLabel(int partnerIndex)` -- returns "Partner 1" or "Partner 2"
  - `static string GetPartnerNotAvailableMessage(int partnerIndex)` -- returns "Partner {N} not available"
  - `static readonly string[] StatNames = { "HP", "MP", "STR", "STA", "WIS", "SPD" }`
  - `static readonly string[] StatNamesWithFatigue = { "HP", "MP", "STR", "STA", "WIS", "SPD", "Fatigue" }`
- Return type: string / string[]
- Estimated impact: 4 files (BattleHudHandler, FieldHudHandler, BattleResultHandler, TrainingResultHandler), eliminates ~20 duplicate expressions

---

## Proposal 6: StatusEffectNames for Field Status Mapping

### [StatusEffectNames -- Consolidated Status Effect String Mapping]
- Current locations:
  - FieldHudHandler.cs:176-183 (in `AnnouncePartnerStatusEffects`)
    ```csharp
    statusEffect switch { None => "Healthy", Injury => "Injured", SeriousInjury => "Seriously Injured", Disease => "Sick", _ => "Unknown status" }
    ```
  - FieldHudHandler.cs:205-211 (in `AnnouncePartnerMood` -- near-duplicate, slightly different)
    ```csharp
    statusEffect switch { Injury => "Injured", SeriousInjury => "Seriously injured", Disease => "Sick", _ => "Has condition" }
    ```
  - FieldHudHandler.cs:235-242 (in `AnnouncePartnerFullStatus` -- near-duplicate, different default)
    ```csharp
    statusEffect switch { None => "Healthy", Injury => "Injured", SeriousInjury => "Seriously Injured", Disease => "Sick", _ => "" }
    ```
- Existing reusable code: None
- Proposed solution: Extract into shared method with optional "none" text parameter
- Proposed name: `PartnerUtilities.GetStatusEffectText` (added to Proposal 5's class)
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\PartnerUtilities.cs`
- Parameters:
  - `static string GetStatusEffectText(PartnerCtrl.FieldStatusEffect effect, string noneText = "Healthy", string unknownText = "Unknown status")`
- Return type: string
- Estimated impact: 1 file (FieldHudHandler), eliminates 3 nearly-identical switch expressions (~24 lines)

---

## Proposal 7: AnnouncementBuilder for Common Announcement Formats

### [AnnouncementBuilder -- Standardized Announcement String Construction]
- Current locations:
  - Cursor position announcement format `$"{itemText}, {cursor + 1} of {total}"`:
    - RestaurantPanelHandler.cs:119
    - TradePanelHandler.cs:110
    - CampCommandHandler.cs:85
    - CommonSelectWindowHandler.cs:94
    - TitleMenuHandler.cs:193
    - SavePanelHandler.cs:124
    - 7+ additional handlers
  - Menu open announcement format `$"Menu Name. {itemText}, {cursor + 1} of {total}"`:
    - RestaurantPanelHandler.cs:75
    - TradePanelHandler.cs:66
    - CampCommandHandler.cs:59
    - CommonSelectWindowHandler.cs:68
    - TitleMenuHandler.cs:159
    - 7+ additional handlers
  - Fallback item text format `$"Item {index + 1}"`:
    - RestaurantPanelHandler.cs:169
    - TradePanelHandler.cs:177
    - TrainingPanelHandler.cs:350
    - SavePanelHandler.cs:246
    - TitleMenuHandler.cs:239
- Existing reusable code: None
- Proposed solution: Create utility class for building standard announcement strings
- Proposed name: `AnnouncementBuilder`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\AnnouncementBuilder.cs`
- Parameters:
  - `static string CursorPosition(string itemText, int cursor, int total)` -- returns `$"{itemText}, {cursor + 1} of {total}"`
  - `static string MenuOpen(string menuName, string itemText, int cursor, int total)` -- returns `$"{menuName}. {itemText}, {cursor + 1} of {total}"`
  - `static string MenuOpenWithState(string menuName, string stateText, string itemText, int cursor, int total)` -- returns `$"{menuName}. {stateText}. {itemText}, {cursor + 1} of {total}"`
  - `static string FallbackItem(string prefix, int index)` -- returns `$"{prefix} {index + 1}"` (prefix = "Item", "Option", "Slot", "Training")
  - `static string PartnerStats(string name, string hpText, string mpText)` -- returns `$"{name}: HP {hpText}, MP {mpText}"`
- Return type: string
- Estimated impact: 15+ handler files, standardizes announcement format across the entire codebase

---

## Proposal 8: BattlePanelUtilities for Battle Panel Checks

### [BattlePanelUtilities -- Battle Panel Availability Helper]
- Current locations:
  - BattleDialogHandler.cs:20-25 (`var battlePanel = uBattlePanel.m_instance; if (battlePanel == null || !battlePanel.m_enabled)`)
  - BattleHudHandler.cs:31-37 (same pattern)
  - BattleResultHandler.cs:39-45 (variant via `battlePanel.m_result`)
  - BattleOrderRingHandler.cs (same pattern)
  - BattleItemHandler.cs (same pattern)
  - BattleTacticsHandler.cs (same pattern)
  - MessageWindowHandler.cs:749-753 (`IsBattleResultActive`)
  - FieldHudHandler.cs:56-59 (battle panel check in `IsPlayerInFieldControl`)
- Existing reusable code: None
- Proposed solution: Extract battle panel checks into shared utility
- Proposed name: `BattlePanelUtilities`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\BattlePanelUtilities.cs`
- Parameters:
  - `static bool IsBattleActive()` -- returns `uBattlePanel.m_instance != null && uBattlePanel.m_instance.m_enabled`
  - `static uBattlePanel GetBattlePanel()` -- returns `uBattlePanel.m_instance` or null
  - `static bool IsBattleResultShowing()` -- checks `m_instance?.m_result?.m_enabled`
- Return type: bool / uBattlePanel
- Estimated impact: 8 files (all battle handlers, MessageWindowHandler, FieldHudHandler)

---

## Proposal 9: DigimonNameResolver Service

### [DigimonNameResolver -- Shared Name Resolution from Model IDs]
- Current locations:
  - NavigationListHandler.cs:1098-1152 (`GetNpcName` -- uses `ParameterDigimonData.FindBaseIdToModelName()` then falls back to iterating `parameterManager.digimonData`)
  - EvolutionHandler.cs:166-236 (`GetBeforeEvolutionName` -- iterates `parameterManager.digimonData` matching by `m_mdlName`)
  - NavigationListHandler.cs:1183-1260 (`GetEnemyName` -- uses enemy-specific data chain lookups)
- Existing reusable code: None (both independently implement the same iteration pattern)
- Proposed solution: Extract model-to-name resolution into a shared service
- Proposed name: `DigimonNameResolver`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\DigimonNameResolver.cs`
- Parameters:
  - `static string ResolveFromModelName(string modelName)` -- tries `FindBaseIdToModelName` then fallback iteration
  - `static string ResolveFromEnemyCtrl(EnemyCtrl enemy)` -- uses enemy game data chain
  - `static string ResolveFromItemId(uint itemId)` -- wraps `ParameterItemData.GetParam(id).GetName()`
  - Internal caching: `Dictionary<string, string>` for model-to-name lookups (cleared on map change)
- Return type: string (null if not found)
- Estimated impact: 2-3 files (NavigationListHandler, EvolutionHandler), eliminates ~100 lines of duplicated name resolution with fallback chains

---

## Proposal 10: WorldScanner Service for Object Discovery

### [WorldScanner -- Shared World Object Scanning]
- Current locations:
  - NavigationListHandler.cs:810-843 (`ScanNPCs` -- iterates `NpcManager.m_NpcCtrlArray`)
  - NavigationListHandler.cs:845-878 (`ScanItems` -- iterates `ItemPickPointManager.m_itemPickPoints`)
  - NavigationListHandler.cs:880-910 (`ScanTransitions` -- `FindObjectsOfType<MapTriggerScript>()`)
  - NavigationListHandler.cs:912-941 (`ScanEnemies` -- iterates `EnemyManager.m_EnemyCtrlArray`)
  - AudioNavigationHandler.cs:212-233 (items -- same `ItemPickPointManager.m_itemPickPoints` iteration)
  - AudioNavigationHandler.cs:237-258 (transitions -- same `FindObjectsOfType<MapTriggerScript>()` with same filter)
  - AudioNavigationHandler.cs:262-281 (enemies -- same `EnemyManager.m_EnemyCtrlArray` iteration)
  - AudioNavigationHandler.cs:285-304 (NPCs -- same `NpcManager.m_NpcCtrlArray` iteration)
- Existing reusable code: None (both classes independently scan the same game managers)
- Proposed solution: Create shared scanner that produces lists of world objects, consumed by both systems
- Proposed name: `WorldScanner`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\WorldScanner.cs`
- Parameters:
  - `static List<(GameObject obj, Vector3 position)> GetActiveNPCs()` -- filters active NPCs from NpcManager
  - `static List<(GameObject obj, Vector3 position)> GetActiveItems()` -- filters active items from ItemPickPointManager
  - `static List<(GameObject obj, Vector3 position)> GetActiveEnemies()` -- filters active enemies from EnemyManager
  - `static List<(GameObject obj, Vector3 position)> GetActiveTransitions()` -- filters MapChange triggers
  - `static void CacheManagers()` -- called once per frame to cache manager references
- Return type: Lists of tuples (see above)
- Estimated impact: 2 files (NavigationListHandler, AudioNavigationHandler), eliminates ~130 lines of duplicated scanning code

**Notes:**
- NavigationListHandler needs the full NpcCtrl/EnemyCtrl objects for name resolution, not just GameObjects. The scanner should return typed results or the raw objects.
- AudioNavigationHandler only needs position + GameObject for distance calculation. Consider returning a lightweight struct.
- Both systems do the same active-check filter: `obj != null && obj.gameObject != null && obj.gameObject.activeInHierarchy`. This filter should be in the scanner.

---

## Proposal 11: Consolidate Logging to DebugLogger

### [Unified Logging -- Route All Logging Through DebugLogger]
- Current locations using non-standard logging:
  - OptionsMenuHandler.cs:98, 109, 145, 322, 340, 470, 499, 663, 705 (`Melon<Main>.Logger.Msg()` and `.Warning()`)
  - ScreenReader.cs:54, 58, 65, 107 (`Melon<Main>.Logger.Msg()`, `.Warning()`, `.Error()`)
  - DebugLogger.cs:39 (`Melon<Main>.Logger.Warning()` -- only in initialization failure)
- Existing reusable code: `DebugLogger.cs` (70 lines) -- already used by all other files
- Proposed solution: Add `Warning()` and `Error()` methods to DebugLogger, then replace all `Melon<Main>.Logger` calls
- Proposed name: `DebugLogger` (extend existing)
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\DebugLogger.cs`
- Parameters:
  - Add `static void Warning(string message)` -- logs with `[WARN]` prefix
  - Add `static void Error(string message)` -- logs with `[ERROR]` prefix
  - Both write to the same log file as `Log()`
- Return type: void
- Estimated impact: 3 files (OptionsMenuHandler, ScreenReader, DebugLogger itself), standardizes ~15 logging calls

**Notes:**
- ScreenReader.cs is a special case: its initialization logging (line 54, "Screen reader detected") happens before DebugLogger might be ready. ScreenReader.Initialize() is called after DebugLogger.Initialize() in Main.cs:68-76, so this should be safe.
- Consider also routing to `MelonLoader.Melon<Main>.Logger` as a secondary output within DebugLogger for console visibility.

---

## Proposal 12: Constants Class for Magic Numbers and Strings

### [Constants -- Centralized Magic Numbers and User-Facing Strings]
- Current locations:
  - Timing:
    - ZonePanelHandler.cs:18 (`CheckInterval = 0.1f`)
    - MessageWindowHandler.cs:34 (`COMMON_TEXT_DELAY = 0.05f`)
    - MessageWindowHandler.cs:53 (`MIN_ANNOUNCEMENT_INTERVAL = 0.1f`)
    - SavePanelHandler.cs:18 (`OpenAnnouncementDelay = 0.05f`)
    - DialogTextPatch.cs:168, 182, 241, 268 (`500` milliseconds)
  - Poll intervals:
    - CommonYesNoHandler.cs:16 (`POLL_INTERVAL = 3`)
    - DialogChoiceHandler.cs:24 (`POLL_INTERVAL = 3`)
  - Hardcoded counts:
    - TitleMenuHandler.cs:247 (`return 4` title menu items)
    - TamerPanelHandler.cs:180 (`2` status commands)
    - TamerPanelHandler.cs:288, 533 (`4` skill tabs)
    - TrainingPanelHandler.cs:436 (`return 7` training count)
    - SavePanelHandler.cs:189 (`return 3` slot count)
    - TradePanelHandler.cs:187 (`return 2` Buy/Sell)
    - BattleResultHandler.cs:126, 163 (`i < 2` partner count)
  - User-facing strings:
    - Main.cs:384 ("In battle. Hold RB plus D-pad...")
    - Main.cs:388 ("No menu active")
    - BattleResultHandler.cs:367, 390 ("Results applied. Press Continue to return to field.")
    - BattleResultHandler.cs:386 ("Battle results. Press Continue to apply.")
    - ModSettingsHandler.cs:66-67, 77, 108, 122 (settings panel strings)
    - StoragePanelHandler.cs:219 ("Unknown Item")
    - FieldHudHandler.cs:141 ("Unknown")
- Existing reusable code: None
- Proposed solution: Create constants class organized by category
- Proposed name: `AccessibilityConstants`
- Proposed location: `C:\Users\Amethyst\projects\digimon world, next order\AccessibilityConstants.cs`
- Parameters:
  - Nested static class `Timing` with `DuplicateMessageWindowMs = 500`, `CommonTextDelay = 0.05f`, `MinAnnouncementInterval = 0.1f`, etc.
  - Nested static class `Counts` with `MaxPartners = 2`, `TitleMenuItems = 4`, `SaveSlots = 3`, etc.
  - Nested static class `Strings` with `PartnerNotAvailable`, `NoMenuActive`, `BattleResultApplied`, `UnknownItem`, etc.
  - Nested static class `PollIntervals` with `FrameSkip = 3`
- Return type: N/A (static constants)
- Estimated impact: 20+ files reference at least one magic number or hardcoded string

**Notes:**
- Not all magic numbers need extraction. SDL constants (e.g., `SDL_INIT_GAMEPAD = 0x00002000`) are fine as-is since they're fixed protocol values. Audio constants like `SampleRate = 44100` are also domain-standard.
- Focus on numbers that affect behavior (timing, thresholds, counts) and strings that users see.

---

## Proposal 13: Remove Dead Code

### [Dead Code Removal -- Non-functional Code Cleanup]
- Current locations:
  - MessageWindowHandler.cs:372-456 (`UpdateCommonMessageWindow`, `OnCommonWindowOpen`, `OnCommonWindowClose`, `CheckCommonMessageChange` -- commented out at lines 91-96, all dead)
  - MessageWindowHandler.cs:485-556 (`UpdateDigimonMessagePanel`, `OnDigimonPanelOpen`, `OnDigimonPanelClose`, `CheckDigimonMessageChange`, `GetDigimonPanelText` -- commented out at lines 94-96, all dead)
  - BattleHudHandler.cs:194-207 (`AnnouncePartnerName` -- never called from anywhere)
  - BattleHudHandler.cs:321-339 (`AnnouncePartnerOrderPower` -- never called from anywhere)
  - OptionsMenuHandler.cs:152-302 (`DumpPanelStructure` -- 150 lines of debug-only code)
  - TrainingBonusHandler.cs:149-153 (`IsActive()` -- redundant wrapper that just calls `IsOpen()`)
  - TrainingResultHandler.cs:172-176 (`IsActive()` -- redundant wrapper that just calls `IsOpen()`)
  - TriggerInput.cs:110-139 (`DebugLogAllAxes` -- never called)
  - TriggerInput.cs:23 (`_initialized` field -- only controls one log message)
  - MessageWindowHandler.cs:29-30 (`_commonMessageWindow` field -- assigned but never meaningfully used since `UpdateCommonMessageWindow` is dead)
  - SDL2Controller.cs:248, 252 (unused temp variables)
- Existing reusable code: N/A
- Proposed solution: Delete dead code; for debug utilities like `DumpPanelStructure`, move behind `#if DEBUG` or delete entirely
- Proposed name: N/A (deletion, not creation)
- Proposed location: Multiple files (see above)
- Parameters: N/A
- Estimated impact: 6 files, removes ~400 lines of dead code

**Notes:**
- MessageWindowHandler dead code is the largest single block (~180 lines across the two commented-out subsystems). Removing it makes the 939-line file significantly more readable.
- `IsActive()` wrappers on TrainingBonusHandler and TrainingResultHandler should be removed, and callers should be updated to use `IsOpen()` directly. Check Main.cs `AnnounceCurrentStatus()` for any `IsActive()` calls on these handlers.

---

## Proposal 14: Standardize Exception Handling

### [Exception Handling Consistency]
- Current locations:
  - Silent catches (`catch { }`) -- 58+ occurrences across the codebase (see 03-code-patterns.md Category 3.1)
  - Logged catches -- 25+ occurrences
  - `System.Exception ex` -- 30+ occurrences (RestaurantPanelHandler, SavePanelHandler, etc.)
  - Bare `Exception ex` -- 6 occurrences (ScreenReader.cs, SDL2Controller.cs)
- Existing reusable code: `DebugLogger.Log()` is available everywhere
- Proposed solution: Establish consistent pattern using `System.Exception` and categorize which catch blocks should log vs. stay silent
- Proposed name: N/A (pattern standardization)
- Proposed location: All handler files
- Parameters:
  - Rule: `IsOpen()` and `FindPanel()` methods: silent catch is acceptable (panel may not exist yet)
  - Rule: `GetCursorPosition()`, `GetMenuItemText()`, `GetMenuItemCount()` methods: log with `DebugLogger.Log` since these indicate unexpected state
  - Rule: All catch blocks should use `System.Exception ex` consistently (not bare `Exception`)
  - Rule: No catch block should be completely empty in data-extraction methods
- Estimated impact: 20+ files, ~80 catch blocks to review

---

## Proposal 15: Standardize Naming Conventions

### [Naming Convention Standardization]
- Current locations:
  - Cursor field names:
    - `_lastCursor` (most handlers)
    - `_lastCursorIndex` (BattleDialogHandler.cs:13)
    - `_lastCursorL`/`_lastCursorR` (StoragePanelHandler.cs:14-15)
    - `_lastCmdNo` (CampCommandHandler -- not actually present, uses `_lastCursor`)
    - `_lastStatusCommand` (TamerPanelHandler.cs:19)
    - `_lastCursorPosition` (DialogChoiceHandler.cs:22)
    - `_lastCommandIndex` (DigiviceTopPanelHandler)
  - Active state field names:
    - `_wasActive` (most handlers)
    - `_wasEnabled` (BattleResultHandler.cs:29)
    - `_wasUsable` (TitleMenuHandler.cs:18)
    - `_wasChoicesActive` (DialogChoiceHandler.cs:17)
  - Open detection method names:
    - `IsOpen()` (most handlers)
    - `IsActive()` (battle handlers)
    - `IsChoicesActive()` (DialogChoiceHandler.cs:30)
    - `IsUsable()` (TitleMenuHandler.cs:81)
  - SDL naming:
    - `SDL2Controller` class (SDL2Controller.cs:20 -- actually wraps SDL3)
    - `ModInputManager.IsUsingSDL2` (references SDL2 but checks SDL3)
- Existing reusable code: N/A
- Proposed solution: Standardize to `_lastCursor`, `_wasActive`, `IsOpen()` across all handlers. Rename SDL2Controller to SDL3Controller.
- Proposed name: N/A (renaming, not creation)
- Proposed location: All handler files + SDL2Controller.cs + ModInputManager.cs
- Parameters:
  - Primary cursor field: `_lastCursor` (or `_lastCursorLeft`/`_lastCursorRight` for dual-panel handlers)
  - Primary active tracking field: `_wasActive`
  - Primary open detection method: `IsOpen()`
  - SDL class rename: `SDL2Controller` -> `SDLController` (version-neutral)
- Estimated impact: 10+ files for field renaming, 2 files for SDL renaming

---

## Summary: Priority-Ordered Implementation Plan

### High Priority (largest impact, most duplication eliminated)

1. **Proposal 1 (HandlerBase<TPanel>)** -- 25+ files, ~250 lines of boilerplate eliminated. This is the single highest-impact extraction and should be done first.
2. **Proposal 3 (TextUtilities)** -- 5 files, consolidates 3 separate text-cleaning implementations and 2 placeholder-detection implementations.
3. **Proposal 13 (Dead Code Removal)** -- 6 files, ~400 lines removed. Zero risk of behavioral change.
4. **Proposal 4 (GameStateService)** -- 4 files, ~350 lines of state-checking consolidated. Eliminates potential inconsistencies between three independent implementations.

### Medium Priority (meaningful improvement, moderate scope)

5. **Proposal 2 (IAccessibilityHandler)** -- Depends on Proposal 1. Transforms Main.cs from god class to clean orchestrator.
6. **Proposal 7 (AnnouncementBuilder)** -- 15+ files, standardizes announcement format strings.
7. **Proposal 5 (PartnerUtilities)** -- 4 files, eliminates ~20 partner label/availability duplications.
8. **Proposal 6 (StatusEffectNames)** -- 1 file, consolidates 3 near-identical switch expressions.
9. **Proposal 11 (Unified Logging)** -- 3 files, standardizes logging system.

### Low Priority (cleanup and consistency)

10. **Proposal 8 (BattlePanelUtilities)** -- 8 files, small per-file saving but consistent pattern.
11. **Proposal 9 (DigimonNameResolver)** -- 2-3 files, extracts complex name resolution logic.
12. **Proposal 10 (WorldScanner)** -- 2 files, large potential but complex to implement correctly.
13. **Proposal 12 (Constants)** -- 20+ files, important for maintainability but lots of small changes.
14. **Proposal 14 (Exception Handling)** -- 20+ files, tedious but improves debugging.
15. **Proposal 15 (Naming Conventions)** -- 10+ files, cosmetic but improves readability.

### Dependencies

- Proposal 2 depends on Proposal 1 (handlers must implement interface via base class)
- Proposal 6 can be merged into Proposal 5 (same target file)
- Proposal 4 should be done before Proposal 10 (GameStateService informs WorldScanner design)
- Proposal 11 should be done before Proposal 14 (logging system must be stable before standardizing catch blocks)
- Proposal 15 should be done last (renaming after structural changes avoids merge conflicts)
