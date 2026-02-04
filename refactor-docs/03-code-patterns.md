# Code Patterns Analysis (Phase 1, Agent 3)

Analysis of all 55 .cs source files (excluding decompiled/, bin/, obj/).

---

## Category 1: Repeated Code Blocks (3+ lines appearing in 2+ locations)

### 1.1 Handler Lifecycle Pattern (Update with _wasActive tracking)

The following 6-line block appears in 25+ handler files with near-identical structure:

```csharp
bool isActive = IsOpen();
if (isActive && !_wasActive)
    OnOpen();
else if (!isActive && _wasActive)
    OnClose();
else if (isActive)
    CheckCursorChange(); // or CheckStateChange()
_wasActive = isActive;
```

Occurrences:
- `RestaurantPanelHandler.cs` lines 42-59
- `SavePanelHandler.cs` lines 39-64
- `StoragePanelHandler.cs` lines 31-49
- `TamerPanelHandler.cs` lines 51-69
- `TradePanelHandler.cs` lines 32-50
- `TrainingBonusHandler.cs` lines 40-58
- `TrainingPanelHandler.cs` lines 45-64
- `TrainingResultHandler.cs` lines 38-56
- `BattleDialogHandler.cs` lines 17-48 (variant with manual _wasActive)
- `CampCommandHandler.cs` (same pattern)
- `CarePanelHandler.cs` (same pattern)
- `CharaSelectHandler.cs` (same pattern)
- `ColosseumPanelHandler.cs` (same pattern)
- `CommonSelectWindowHandler.cs` (same pattern)
- `CommonYesNoHandler.cs` lines 55-81
- `DialogChoiceHandler.cs` lines 110-137
- `DigiEggHandler.cs` (same pattern)
- `DigiviceTopPanelHandler.cs` (same pattern)
- `FarmPanelHandler.cs` (same pattern)
- `FieldItemPanelHandler.cs` (same pattern)
- `GenealogyHandler.cs` (same pattern)
- `ItemPickPanelHandler.cs` (same pattern)
- `MailPanelHandler.cs` (same pattern)
- `MapPanelHandler.cs` (same pattern)
- `PartnerPanelHandler.cs` (same pattern)

Recommendation: Extract into a base class `HandlerBase` with virtual `OnOpen()`, `OnClose()`, and `OnUpdate()` methods.

### 1.2 FindObjectOfType IsOpen() Pattern

Nearly identical null-check-then-find pattern for detecting if a panel is open:

```csharp
if (_panel == null)
    _panel = Object.FindObjectOfType<TPanelType>();
if (_panel == null)
    return false;
```

Occurrences (all in `IsOpen()` methods):
- `RestaurantPanelHandler.cs` lines 19-25
- `SavePanelHandler.cs` lines 24-30
- `StoragePanelHandler.cs` lines 19-25
- `TamerPanelHandler.cs` lines 31-38
- `TradePanelHandler.cs` lines 19-24
- `TrainingBonusHandler.cs` lines 19-25
- `TrainingPanelHandler.cs` lines 22-28
- `TrainingResultHandler.cs` lines 19-25
- `CampCommandHandler.cs` (same pattern)
- `CarePanelHandler.cs` (same pattern)
- `CharaSelectHandler.cs` (same pattern)
- `ColosseumPanelHandler.cs` (same pattern)
- `CommonSelectWindowHandler.cs` (same pattern)
- `DigiEggHandler.cs` (same pattern)
- `DigiviceTopPanelHandler.cs` (same pattern)
- `FarmPanelHandler.cs` (same pattern)
- `FieldItemPanelHandler.cs` (same pattern)
- `GenealogyHandler.cs` (same pattern)
- `ItemPickPanelHandler.cs` (same pattern)
- `MailPanelHandler.cs` (same pattern)
- `MapPanelHandler.cs` (same pattern)
- `PartnerPanelHandler.cs` (same pattern)

### 1.3 Battle Panel Null Check

The same battle panel availability check appears across all battle handlers:

```csharp
var battlePanel = uBattlePanel.m_instance;
if (battlePanel == null || !battlePanel.m_enabled)
{
    ResetState(); // or return
    return;
}
```

Occurrences:
- `BattleDialogHandler.cs` lines 20-25
- `BattleHudHandler.cs` lines 31-37
- `BattleResultHandler.cs` lines 39-45 (variant using m_result)
- `BattleOrderRingHandler.cs` (same pattern)
- `BattleItemHandler.cs` (same pattern)
- `BattleTacticsHandler.cs` (same pattern)
- `MessageWindowHandler.cs` lines 749-753 (in IsBattleResultActive)
- `FieldHudHandler.cs` lines 56-59 (in IsPlayerInFieldControl)

### 1.4 Rich Text Stripping (Regex.Replace for HTML-like tags)

The same regex for removing Unity rich text tags appears in three different files:

```csharp
Regex.Replace(text, @"<[^>]+>", "")
```

Occurrences:
- `DialogTextPatch.cs` line 327 (StripRichTextTags method)
- `MessageWindowHandler.cs` line 890 (CleanText method)
- `BattleDialogHandler.cs` line 108 (CleanText method)

### 1.5 Duplicate OnClose() Reset Pattern

Every handler has an OnClose() that nulls the panel reference and resets cursor/state tracking:

```csharp
private void OnClose()
{
    _panel = null;
    _lastCursor = -1;
    _lastState = SomeType.None;
    DebugLogger.Log("[HandlerName] Panel closed");
}
```

Occurrences:
- `RestaurantPanelHandler.cs` lines 83-89
- `SavePanelHandler.cs` lines 82-89
- `StoragePanelHandler.cs` lines 67-74
- `TamerPanelHandler.cs` lines 99-104
- `TradePanelHandler.cs` lines 74-79
- `TrainingBonusHandler.cs` lines 80-86
- `TrainingPanelHandler.cs` lines 121-129
- `TrainingResultHandler.cs` lines 71-76
- 15+ additional handlers follow this same pattern

### 1.6 Cursor Change Announcement Pattern

The cursor-change-detection-with-announcement pattern repeats across many handlers:

```csharp
int cursor = GetCursorPosition();
if (cursor != _lastCursor && cursor >= 0)
{
    string itemText = GetMenuItemText(cursor);
    int total = GetMenuItemCount();
    string announcement = $"{itemText}, {cursor + 1} of {total}";
    ScreenReader.Say(announcement);
    _lastCursor = cursor;
}
```

Occurrences:
- `RestaurantPanelHandler.cs` lines 107-125
- `TradePanelHandler.cs` lines 98-116
- `TitleMenuHandler.cs` lines 177-199
- `CampCommandHandler.cs` (same pattern)
- `CommonSelectWindowHandler.cs` (same pattern)
- `DigiviceTopPanelHandler.cs` (same pattern)
- `MapPanelHandler.cs` (same pattern)

### 1.7 Partner Not Available Fallback

The same "Partner N not available" string with the same logic appears many times:

```csharp
ScreenReader.Say($"Partner {partnerIndex + 1} not available");
```

Occurrences:
- `BattleHudHandler.cs` lines 199, 217, 304, 327, 346
- `FieldHudHandler.cs` lines 137, 151, 169, 193, 226

### 1.8 Partner Label Selection

The partner label ternary expression repeats identically:

```csharp
string partnerLabel = partnerIndex == 0 ? "Partner 1" : "Partner 2";
```

Occurrences:
- `BattleHudHandler.cs` lines 205, 294, 317, 337, 350
- `TrainingPanelHandler.cs` line 302

### 1.9 Placeholder/Skip Text Detection Logic

Two separate implementations of placeholder text detection with overlapping logic:

`DialogTextPatch.IsPlaceholderText()` at line 330:
```csharp
if (text.Contains("...") || text.Contains("..."))
    return true;
if (text.StartsWith("EV_") || text.StartsWith("SYS_") || text.StartsWith("MSG_"))
    return true;
```

`CommonMessageMonitor.ShouldSkipText()` at line 124:
```csharp
if (text.Contains("...") || text.Contains("..."))
    return true;
if (text.StartsWith("EV_") || text.StartsWith("SYS_") || text.StartsWith("MSG_"))
    return true;
```

Both check for the same placeholder characters and localization key prefixes but are defined independently.

### 1.10 Status Effect Switch Expression

The field status effect switch expression appears twice in FieldHudHandler:

```csharp
statusEffect switch
{
    PartnerCtrl.FieldStatusEffect.None => "Healthy",
    PartnerCtrl.FieldStatusEffect.Injury => "Injured",
    PartnerCtrl.FieldStatusEffect.SeriousInjury => "Seriously Injured",
    PartnerCtrl.FieldStatusEffect.Disease => "Sick",
    _ => "Unknown status"
};
```

Occurrences:
- `FieldHudHandler.cs` lines 176-183 (AnnouncePartnerStatusEffects)
- `FieldHudHandler.cs` lines 205-211 (AnnouncePartnerMood, slightly different variant)
- `FieldHudHandler.cs` lines 235-242 (AnnouncePartnerFullStatus)

### 1.11 Stat Names Array

The stat names array `{ "HP", "MP", "STR", "STA", "WIS", "SPD" }` is defined independently in multiple locations:

Occurrences:
- `BattleResultHandler.cs` line 236 (GetPartnerStatsText)
- `BattleResultHandler.cs` line 276 (GetPartnerStatsFromText)
- `TrainingResultHandler.cs` line 145 (GetPartnerResult -- includes "Fatigue" as 7th element)

### 1.12 IsLocalizationReady Check

```csharp
try { return Localization.isActive; }
catch { return false; }
```

Occurrences:
- `TitleMenuHandler.cs` lines 103-113
- `CommonMessageMonitor.cs` lines 98-106

### 1.13 IsGameLoading Check

```csharp
var mgr = MainGameManager.m_instance;
if (mgr != null) return mgr._IsLoad();
```

Occurrences:
- `DialogTextPatch.cs` lines 122-134
- `CommonMessageMonitor.cs` lines 108-122

---

## Category 2: Inconsistent Naming Conventions

### 2.1 Cursor Tracking Field Names

Different handlers use different names for the same concept (tracking the last known cursor position):

- `_lastCursor` -- RestaurantPanelHandler.cs:14, SavePanelHandler.cs:14, TradePanelHandler.cs:14, TitleMenuHandler.cs:19, OptionsMenuHandler.cs:14
- `_lastCursorIndex` -- BattleDialogHandler.cs:13
- `_lastSelectNo` -- (used in some handlers for m_selectNo)
- `_lastCursorL` / `_lastCursorR` -- StoragePanelHandler.cs:14-15
- `_lastCursorRight` / `_lastCursorLeft` -- TrainingPanelHandler.cs:15-16
- `_lastCmdNo` -- CampCommandHandler.cs
- `_lastStatusCommand` -- TamerPanelHandler.cs:19
- `_lastSkillCheckSelectNo` -- TamerPanelHandler.cs:28
- `_lastCursorPosition` -- DialogChoiceHandler.cs:22
- `_lastCommandIndex` -- DigiviceTopPanelHandler.cs

### 2.2 Open/Active Detection Method Names

Handlers inconsistently name their "is this panel showing?" method:

- `IsOpen()` -- RestaurantPanelHandler.cs:17, SavePanelHandler.cs:22, StoragePanelHandler.cs:18, TamerPanelHandler.cs:30, TradePanelHandler.cs:17, TrainingBonusHandler.cs:17, TrainingPanelHandler.cs:20, TrainingResultHandler.cs:16, CommonYesNoHandler.cs:22, and 15+ other handlers
- `IsActive()` -- BattleHudHandler.cs:384, BattleDialogHandler.cs:116, BattleResultHandler.cs:370, TrainingBonusHandler.cs:149 (has both!), TrainingResultHandler.cs:172 (has both!)
- `IsChoicesActive()` -- DialogChoiceHandler.cs:30
- `IsUsable()` -- TitleMenuHandler.cs:81 (with `IsOpen()` at line 272 as a wrapper)
- `IsPanelActive()` -- OptionsMenuHandler.cs:22 (internal, `IsOpen()` also exists)
- `IsPanelReady()` -- TitleMenuHandler.cs:28 (internal)

TrainingBonusHandler has both `IsOpen()` at line 17 and `IsActive()` at line 149 that just calls `IsOpen()`. Same for TrainingResultHandler with `IsOpen()` at line 16 and `IsActive()` at line 172.

### 2.3 Active State Tracking Field Names

- `_wasActive` -- Used by most handlers (RestaurantPanelHandler.cs:13, SavePanelHandler.cs:13, etc.)
- `_wasEnabled` -- BattleResultHandler.cs:29
- `_wasEventPanelActive` -- MessageWindowHandler.cs:21
- `_wasCommonActive` -- MessageWindowHandler.cs:30
- `_wasDigimonPanelActive` -- MessageWindowHandler.cs:38
- `_wasBattleDialogActive` -- MessageWindowHandler.cs:43
- `_wasCaptionActive` -- MessageWindowHandler.cs:48
- `_wasUsable` -- TitleMenuHandler.cs:18
- `_wasFishingPromptActive` -- FieldHudHandler.cs:27
- `_wasChoicesActive` -- DialogChoiceHandler.cs:17

### 2.4 Logging System Usage

Most files use `DebugLogger.Log()`, but some use `Melon<Main>.Logger`:

- `OptionsMenuHandler.cs` uses `Melon<Main>.Logger.Msg()` throughout (lines 98, 109, 145, 322, 340, etc.) and `Melon<Main>.Logger.Warning()` (lines 470, 499, 663, 705)
- `ScreenReader.cs` uses `Melon<Main>.Logger.Msg()` (line 54), `Melon<Main>.Logger.Warning()` (lines 58, 107), and `Melon<Main>.Logger.Error()` (line 65)
- All other files consistently use `DebugLogger.Log()`

### 2.5 State Enum Field Names

- `_lastState` -- RestaurantPanelHandler.cs:15, SavePanelHandler.cs:15, TamerPanelHandler.cs:16, TradePanelHandler.cs:15, TrainingPanelHandler.cs:17
- `_lastPanelState` -- OptionsMenuHandler.cs:16 (different from `_lastState` at line 15, both used)
- `_lastBonusTabShowing` -- TrainingPanelHandler.cs:18 (bool tracking)

### 2.6 Class Name vs Actual Library

- `SDL2Controller` class name in `SDL2Controller.cs` line 20 (comment says "Keep class name for compatibility") but actually wraps SDL3
- `ModInputManager.IsUsingSDL2` property name references SDL2 but checks for SDL3 availability

---

## Category 3: Inconsistent Error Handling Patterns

### 3.1 Silent Catch vs Logged Catch

Some catch blocks log errors while others silently swallow exceptions:

Silent catches (empty `catch { }`):
- `RestaurantPanelHandler.cs` line 182 (GetMenuItemCount)
- `SavePanelHandler.cs` line 188 (GetSlotCount)
- `StoragePanelHandler.cs` line 196 (GetItemCount)
- `TamerPanelHandler.cs` lines 184, 322, 351, 376, 401, 458, 544, 618, 762
- `TradePanelHandler.cs` line 196 (GetMenuItemCount)
- `TrainingPanelHandler.cs` lines 371, 435
- `BattleResultHandler.cs` lines 45, 58, 139, 222, 301, 357
- `BattleHudHandler.cs` lines 64, 77, 88, 100, 315, 335, 372
- `DialogTextPatch.cs` lines 116, 132, 147, 163, 199, 227, 254, 281, 317
- `CommonMessageMonitor.cs` lines 47, 95, 104, 119
- `MessageWindowHandler.cs` lines 137, 242, 301, 366, 452, 479, 553, 579, 666, 698, 840
- `FieldHudHandler.cs` lines 60, 78, 88, 100, 263, 307, 324
- `ScreenReader.cs` lines 80, 134
- `SDL2Controller.cs` line 490

Logged catches (with `DebugLogger.Log()`):
- `RestaurantPanelHandler.cs` lines 138-141, 164-167
- `SavePanelHandler.cs` lines 172-175, 204-207, 241-244, 306-309
- `StoragePanelHandler.cs` lines 131-134, 168-170, 214-217
- `TradePanelHandler.cs` lines 128-131, 173-175
- `TrainingPanelHandler.cs` lines 247-249, 268-271, 297-299, 345-348
- `BattleHudHandler.cs` lines 186-189, 236-239, 267-270, 289-291

### 3.2 Inconsistent Exception Type in Catch Blocks

Some files use `System.Exception` and others use bare `Exception`:

`System.Exception ex`:
- `RestaurantPanelHandler.cs` lines 138, 164
- `SavePanelHandler.cs` lines 172, 204, 241, 306
- `StoragePanelHandler.cs` lines 131, 168, 214
- `TradePanelHandler.cs` lines 128, 173
- `TrainingPanelHandler.cs` lines 247, 268, 297, 345
- `TrainingResultHandler.cs` lines 117, 165
- `BattleHudHandler.cs` lines 186, 236, 267
- `DialogChoiceHandler.cs` lines 227, 243
- `CommonYesNoHandler.cs` line 165
- `SteamInputPatch.cs` lines 36, 63
- `ToneGenerator.cs` line 154

Bare `Exception ex` (requires `using System;`):
- `ScreenReader.cs` lines 63, 105
- `SDL2Controller.cs` lines 196, 267, 446, 477

### 3.3 Melon Logger vs DebugLogger in Error Paths

Errors in `OptionsMenuHandler.cs` are logged to both systems inconsistently:
- `OptionsMenuHandler.cs` line 470: `Melon<Main>.Logger.Warning()`
- `OptionsMenuHandler.cs` line 663: both `DebugLogger.Log()` and `Melon<Main>.Logger.Warning()` on successive lines
- Most other handlers only use `DebugLogger.Log()`

`ScreenReader.cs` only uses `Melon<Main>.Logger` (lines 54, 58, 65, 107) and never uses `DebugLogger`.

### 3.4 Inconsistent IsOpen() Exception Handling

Some `IsOpen()` methods have try-catch and some do not:

With try-catch:
- `RestaurantPanelHandler.cs` lines 27-37
- `TamerPanelHandler.cs` lines 40-48
- `TrainingBonusHandler.cs` lines 27-37
- `TrainingPanelHandler.cs` lines 30-41
- `TrainingResultHandler.cs` lines 27-35
- `ZonePanelHandler.cs` lines 27-42

Without try-catch:
- `StoragePanelHandler.cs` lines 18-29
- `SavePanelHandler.cs` lines 22-36
- `TradePanelHandler.cs` lines 17-29

---

## Category 4: Dead Code

### 4.1 Commented-Out Methods in MessageWindowHandler

- `MessageWindowHandler.cs` lines 91-96: Two method calls are commented out with explanatory comments:
  ```csharp
  // UpdateCommonMessageWindow() was causing duplicate/stale announcements
  // UpdateCommonMessageWindow();
  // UpdateDigimonMessagePanel() was causing duplicate announcements
  // UpdateDigimonMessagePanel();
  ```
  The methods `UpdateCommonMessageWindow()` (lines 372-390), `OnCommonWindowOpen()` (lines 392-399), `OnCommonWindowClose()` (lines 401-407), `CheckCommonMessageChange()` (lines 409-437), and `UpdateDigimonMessagePanel()` (lines 485-503), `OnDigimonPanelOpen()` (lines 505-518), `OnDigimonPanelClose()` (lines 520-524), `CheckDigimonMessageChange()` (lines 526-537), `GetDigimonPanelText()` (lines 539-556) are all dead code that is never called.

### 4.2 Unused AnnouncePartnerName and AnnouncePartnerOrderPower in BattleHudHandler

- `BattleHudHandler.cs` lines 194-207: `AnnouncePartnerName()` is never called anywhere in the codebase
- `BattleHudHandler.cs` lines 321-339: `AnnouncePartnerOrderPower()` is never called anywhere in the codebase

### 4.3 DumpPanelStructure Debug Method in OptionsMenuHandler

- `OptionsMenuHandler.cs` lines 152-302: `DumpPanelStructure()` is a large debug-only method (150 lines) that dumps panel structures to the log. It is called from `CheckStateChange()` at line 127, but this is pure debug logging that serves no accessibility function.

### 4.4 Redundant IsActive() Wrappers

Several handlers have `IsActive()` that simply calls `IsOpen()`:
- `TrainingBonusHandler.cs` lines 149-153: `public bool IsActive() { return IsOpen(); }`
- `TrainingResultHandler.cs` lines 172-176: `public bool IsActive() { return IsOpen(); }`

### 4.5 Unused _subscribedToPatch Late-Init Pattern

- `MessageWindowHandler.cs` lines 79-84: The `_subscribedToPatch` flag and late subscription in `Update()` could be done once in the constructor instead:
  ```csharp
  if (!_subscribedToPatch)
  {
      DialogTextPatch.OnTextIntercepted += OnDialogTextIntercepted;
      _subscribedToPatch = true;
  }
  ```

### 4.6 DebugLogAllAxes in TriggerInput

- `TriggerInput.cs` lines 110-139: `DebugLogAllAxes()` is a debug method that is never called from any file in the codebase.

### 4.7 Unused _initialized Field in TriggerInput

- `TriggerInput.cs` line 23: `_initialized` is set to `true` at line 76 but is only used to control whether a single log message is printed. It does not control any functional behavior.

### 4.8 MessageWindowHandler Unused Fields

- `MessageWindowHandler.cs` line 30: `_commonMessageWindow` is assigned in `IsCommonWindowOpen()` but the actual message reading for common windows was moved to `CommonMessageMonitor` and `DialogTextPatch`. The field is still populated but `UpdateCommonMessageWindow()` is commented out, making the assignment at line 305 (and similar) pointless.

### 4.9 Unused Temp Variable in SDL2Controller

- `SDL2Controller.cs` line 248: `var temp = _lastButtonStates;` -- the `temp` variable is assigned but never used (the old reference is discarded immediately).
- `SDL2Controller.cs` line 252: `var tempAxis = _lastAxisStates;` -- same issue.

---

## Category 5: Magic Numbers and Hardcoded Strings

### 5.1 Magic Numbers

#### Timing Constants
- `ZonePanelHandler.cs` line 18: `CheckInterval = 0.1f` -- rate limiting for zone checks
- `MessageWindowHandler.cs` line 34: `COMMON_TEXT_DELAY = 0.05f` -- delay before announcing common messages
- `MessageWindowHandler.cs` line 53: `MIN_ANNOUNCEMENT_INTERVAL = 0.1f` -- minimum time between announcements
- `SavePanelHandler.cs` line 18: `OpenAnnouncementDelay = 0.05f`
- `DialogTextPatch.cs` line 168: `500` (milliseconds) -- voiced dialog detection window
- `DialogTextPatch.cs` lines 182, 241, 268: `500` (milliseconds) -- duplicate message detection window
- `DialogTextPatch.cs` line 159: `20` -- max voiced text keys before clearing
- `MessageWindowHandler.cs` line 660: `txt.Length > 5` -- minimum battle dialog text length

#### Poll Intervals
- `CommonYesNoHandler.cs` line 16: `POLL_INTERVAL = 3` -- check every 3 frames
- `DialogChoiceHandler.cs` line 24: `POLL_INTERVAL = 3` -- check every 3 frames

#### Counts and Indices
- `TitleMenuHandler.cs` line 247: `return 4;` -- hardcoded title menu item count
- `DigiviceTopPanelHandler.cs`: `total = 8` -- hardcoded digivice menu item count
- `TamerPanelHandler.cs` line 180: `$"{currentCommand + 1} of 2"` -- hardcoded 2 status commands
- `TamerPanelHandler.cs` lines 288, 533: `$"{currentTab + 1} of 4"` -- hardcoded 4 skill tabs
- `TamerPanelHandler.cs` lines 506-511: `GetCategoryCount()` returns hardcoded values (4, 3, 4, 5)
- `TrainingPanelHandler.cs` line 436: `return 7;` -- fallback training count
- `SavePanelHandler.cs` line 189: `return 3;` -- fallback slot count
- `TradePanelHandler.cs` line 187: `return 2;` -- hardcoded Buy/Sell count
- `BattleResultHandler.cs` lines 126, 163: `i < 2` -- hardcoded 2 partners

#### Audio/Controller Thresholds
- `SDL2Controller.cs` line 135: `TriggerThreshold = 8000` -- 25% trigger threshold
- `SDL2Controller.cs` line 136: `StickThreshold = 16000` -- 50% stick threshold
- `TriggerInput.cs` line 12: `TriggerThreshold = 0.3f`
- `ToneGenerator.cs` line 28: `SampleRate = 44100`

#### SDL/Gamepad Constants
- `SDL2Controller.cs` line 23: `SDL_INIT_GAMEPAD = 0x00002000`
- `SDL2Controller.cs` line 24: `SDL_INIT_JOYSTICK = 0x00000200`
- `SDL2Controller.cs` line 169: `(int)button < 26` -- max button count
- `TriggerInput.cs` line 95: `buttonIndex = isLeft ? 4 : 5` -- common trigger button indices

### 5.2 Hardcoded Strings

#### User-Facing Strings (should be constants or localization)
- `BattleHudHandler.cs` line 199, 217, 304, 327, 346 and `FieldHudHandler.cs` line 137, 151, 169, 193, 226: `$"Partner {partnerIndex + 1} not available"`
- `BattleHudHandler.cs` lines 205, 294, 317, 337, 350: `partnerIndex == 0 ? "Partner 1" : "Partner 2"`
- `TrainingPanelHandler.cs` line 302: `partnerIndex == 0 ? "Partner 1" : "Partner 2"`
- `BattleResultHandler.cs` line 367: `"Results applied. Press Continue to return to field."`
- `BattleResultHandler.cs` line 386: `"Battle results. Press Continue to apply."`
- `BattleResultHandler.cs` line 390: `"Results applied. Press Continue to return to field."` (duplicate of line 367)
- `Main.cs` line 384: `"In battle. Hold RB plus D-pad for Partner 1, LB plus D-pad for Partner 2"`
- `Main.cs` line 388: `"No menu active"`
- `StoragePanelHandler.cs` line 219: `"Unknown Item"`
- `FieldHudHandler.cs` line 141: `"Unknown"`
- `ModSettingsHandler.cs` lines 66-67: `"Mod Settings. {_settings.Length} items. Use up and down to navigate, confirm to change, cancel to close."`
- `ModSettingsHandler.cs` line 77: `"Mod Settings closed"`
- `ModSettingsHandler.cs` line 108: `"Top of list"`
- `ModSettingsHandler.cs` line 122: `"Bottom of list"`

#### Fallback Text Patterns
- `TitleMenuHandler.cs` lines 233-240: Hardcoded fallback menu items (`"New Game"`, `"Load Game"`, `"System Settings"`, `"Quit Game"`)
- `RestaurantPanelHandler.cs` line 169: `$"Item {index + 1}"` fallback
- `TradePanelHandler.cs` line 177: `$"Item {index + 1}"` fallback
- `TrainingPanelHandler.cs` line 350: `$"Training {index + 1}"` fallback
- `SavePanelHandler.cs` line 246: `$"Slot {slotIndex + 1}"` fallback

#### Log Tag Strings
Every handler has its own hardcoded log tag string such as:
- `"[RestaurantPanel]"`, `"[SavePanel]"`, `"[StoragePanel]"`, `"[TamerPanel]"`, `"[TradePanel]"`, `"[TrainingBonus]"`, `"[TrainingPanel]"`, `"[TrainingResult]"`, `"[ZonePanel]"`, `"[BattleResultHandler]"`, `"[BattleHudHandler]"`, `"[DialogChoice]"`, `"[YesNo]"`, `"[CommonMessageMonitor]"`, `"[MessageWindow]"`, `"[EventPanel]"`, `"[CommonMessage]"`, `"[SDL3Controller]"`, `"[ToneGen]"`, `"[SteamInputPatch]"`, `"[TriggerInput]"`, `"[ModSettings]"`, `"[SetMessage]"`, `"[DigimonMessage]"`, `"[FieldDigimonMessage]"`, `"[KeyConfig]"`, `"[Settings]"`

These are embedded throughout and could use a `nameof()` or constant-based approach.

#### State/Enum-to-String Mappings
Multiple handlers have large switch expressions mapping enums to display strings:
- `RestaurantPanelHandler.cs` lines 186-205 (GetStateText)
- `TradePanelHandler.cs` lines 200-214 (GetStateText)
- `TrainingPanelHandler.cs` lines 377-400 (GetBonusTypeName)
- `TrainingPanelHandler.cs` lines 402-423 (GetTrainingTypeName)
- `TrainingPanelHandler.cs` lines 439-458 (GetStateText)
- `TamerPanelHandler.cs` lines 462-499 (GetCategoryName -- 4 nested switch expressions)
- `TamerPanelHandler.cs` lines 664-741 (GetTamerSkillName -- 78-line switch mapping)
- `FieldHudHandler.cs` lines 176-183, 205-211, 235-242 (status effect mapping -- 3 near-duplicates)
- `SavePanelHandler.cs` lines 151-161 (GetStateAnnouncement)
- `OptionsMenuHandler.cs` lines 345-364 (GetMenuName)
- `OptionsMenuHandler.cs` lines 711-726 (GetGraphicsSettingName)
- `OptionsMenuHandler.cs` lines 824-849 (KeyCodeToString)

These could be consolidated into a shared `DisplayNames` utility class.

### 5.3 SDL Hint Strings
- `SDL2Controller.cs` line 151: `"SDL_WINDOWS_DISABLE_THREAD_NAMING"` and `"1"`

### 5.4 Harmony Patch Identifiers
- `Main.cs` line 85: `"com.digimonoaccess.patches"`
- `SteamInputPatch.cs` line 19: `"com.accessibility.digimonno.steaminput"` (inconsistent naming with Main.cs)

---

## Summary

| Category | Count |
|---|---|
| Repeated code blocks | 13 distinct patterns |
| Inconsistent naming | 6 categories of inconsistency |
| Inconsistent error handling | 4 patterns |
| Dead code | 9 instances |
| Magic numbers / hardcoded strings | 30+ distinct magic numbers, 15+ hardcoded string patterns |

### Highest-Impact Refactoring Opportunities

1. **Handler base class** -- Extracting the lifecycle pattern (IsOpen/Update/_wasActive/OnOpen/OnClose/CheckCursorChange) into a `HandlerBase<TPanel>` generic base class would eliminate hundreds of lines of boilerplate across 25+ files.

2. **Shared text utilities** -- The rich text stripping, placeholder detection, and localization-ready check should be in a single utility class referenced by all consumers.

3. **Constants class** -- All magic numbers (timing, thresholds, counts) and user-facing strings should be centralized into a `Constants` or `Config` class.

4. **Unified logging** -- Choose one logging system (`DebugLogger.Log()`) and use it consistently everywhere, eliminating the `Melon<Main>.Logger` usage in OptionsMenuHandler and ScreenReader.

5. **Dead code removal** -- The commented-out methods in MessageWindowHandler (100+ lines), unused methods in BattleHudHandler, and debug-only DumpPanelStructure in OptionsMenuHandler should be removed.

6. **Display name utility** -- The many switch expressions mapping enums to display strings (especially the triplicated status effect mapping in FieldHudHandler) should be consolidated.
