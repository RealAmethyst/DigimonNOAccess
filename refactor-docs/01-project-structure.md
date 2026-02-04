# 01 - Project Structure Analysis

## Project Overview

- **Project Name:** DigimonNOAccess
- **Type:** MelonLoader mod (Unity game accessibility mod)
- **Target Game:** Digimon World Next Order (Unity 2019.4.11f1, Il2Cpp)
- **Purpose:** Makes the game accessible for blind players via screen reader (Tolk/NVDA/JAWS), positional audio, and controller support
- **Namespace:** `DigimonNOAccess`

---

## Directory Tree

```
C:\Users\Amethyst\projects\digimon world, next order\
|
|-- DigimonNOAccess.csproj          (MSBuild project file)
|-- Main.cs                          (Mod entry point - MelonMod subclass)
|
|-- [Handler files - 37 .cs files]   (Feature-specific accessibility handlers)
|-- [Infrastructure files - 9 .cs]   (Core systems: input, audio, screen reader, patches)
|
|-- docs/                            (English documentation)
|   |-- ACCESSIBILITY_MODDING_GUIDE.md
|   |-- game-api.md
|   |-- localization-guide.md
|   |-- menu-accessibility-checklist.md
|   |-- mod-menu-research.md
|   |-- pathfinding-navigation-research.md
|   |-- setup-guide.md
|   |-- technical-reference.md
|
|-- docs-de/                         (German documentation)
|   |-- localization-guide.md
|   |-- menu-accessibility-checklist.md
|   |-- setup-guide.md
|   |-- technical-reference.md
|
|-- scripts/                         (PowerShell helper scripts)
|   |-- Get-MelonLoaderInfo.ps1
|   |-- Test-ModSetup.ps1
|
|-- sounds/                          (Audio assets for positional audio)
|   |-- door.wav
|   |-- item.wav
|   |-- potential enemie digimon.wav
|   |-- potential npc.wav
|   |-- transission.wav
|   |-- wall down.wav
|   |-- wall left.wav
|   |-- wall right.wav
|   |-- wall up.wav
|
|-- templates/                       (Starter templates for new projects)
|   |-- csproj.template
|   |-- DebugLogger.cs.template
|   |-- game-api.md.template
|   |-- Handler.cs.template
|   |-- Loc.cs.template
|   |-- Main.cs.template
|   |-- ScreenReader.cs.template
|
|-- decompiled/                      (Decompiled game source - EXCLUDED from build)
|   |-- Il2Cpp/                      (Hundreds of .cs files from Assembly-CSharp)
|   |-- Assembly-CSharp.csproj
|
|-- bin/                             (Build output)
|-- obj/                             (Build intermediates)
|
|-- CLAUDE.md                        (Claude Code project instructions - English)
|-- CLAUDE.de.md                     (Claude Code project instructions - German)
|-- README.md
|-- REFACTOR_PROMPT.md
|-- battle-system-checklist.md
|-- currently working on.md
|-- project_status.md
|-- DigimonNOAccess_debug.log        (Runtime debug log)
|-- nul                              (Likely accidental file)
```

---

## Build System

- **Build System:** MSBuild (.NET SDK-style project)
- **Project File:** `C:\Users\Amethyst\projects\digimon world, next order\DigimonNOAccess.csproj`
- **No .sln file** - single project, built directly with `dotnet build DigimonNOAccess.csproj`
- **Target Framework:** `net6.0`
- **Language Version:** `latest`
- **Output Path:** `bin\` (flat, no target framework subfolder via `AppendTargetFrameworkToOutputPath=false`)

### Build Configuration Details

- `ImplicitUsings`: disabled (explicit using statements required)
- `Nullable`: disabled
- `CopyLocalLockFileAssemblies`: true (NuGet packages copied to output)
- Excludes from compilation: `decompiled\**`, `templates\**`, `sounds\**`
- Copies `sounds\**` to output directory (PreserveNewest)

### NuGet Dependencies

- **NAudio 2.2.1** - Audio library for positional audio (stereo panning, WAV playback)

### Assembly References (External, non-NuGet)

All references point to the game's MelonLoader installation at:
`C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order\`

- `MelonLoader\net6\MelonLoader.dll` - Mod loader framework
- `MelonLoader\net6\Il2CppInterop.Runtime.dll` - Il2Cpp interop layer
- `MelonLoader\net6\0Harmony.dll` - Harmony patching library
- `MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll` - Il2Cpp standard library
- `MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` - Game code (main target)
- `MelonLoader\Il2CppAssemblies\Assembly-CSharp-firstpass.dll` - Game code (firstpass)
- `MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll`
- `MelonLoader\Il2CppAssemblies\UnityEngine.UI.dll`
- `MelonLoader\Il2CppAssemblies\UnityEngine.InputLegacyModule.dll`
- `MelonLoader\Il2CppAssemblies\UnityEngine.AudioModule.dll`
- `MelonLoader\Il2CppAssemblies\UnityEngine.PhysicsModule.dll`
- `MelonLoader\Il2CppAssemblies\UnityEngine.AIModule.dll`

### Native/External DLL Dependencies (runtime, not referenced in .csproj)

- **Tolk.dll** - Screen reader communication library (P/Invoke from ScreenReader.cs)
- **nvdaControllerClient64.dll** - NVDA screen reader client
- **SDL3.dll** - SDL3 controller support (P/Invoke from SDL2Controller.cs, optional)

---

## Entry Points

### Primary Entry Point

**File:** `C:\Users\Amethyst\projects\digimon world, next order\Main.cs`
**Class:** `DigimonNOAccess.Main` (extends `MelonMod`)

Assembly-level attributes register the mod with MelonLoader:
```csharp
[assembly: MelonInfo(typeof(DigimonNOAccess.Main), "DigimonNOAccess", "1.0.0", "Accessibility Mod")]
[assembly: MelonGame("Bandai Namco Entertainment", "Digimon World Next Order")]
```

MelonLoader lifecycle methods used:
- **`OnInitializeMelon()`** - Mod startup. Initializes all systems in order:
  1. DebugLogger.Initialize()
  2. ModInputManager.Initialize() (which initializes SDL2Controller)
  3. ScreenReader.Initialize()
  4. Harmony patches (DialogTextPatch, GamepadInputPatch)
  5. Instantiates all 37 handler objects
- **`OnUpdate()`** - Called every frame. Updates ModInputManager, then all 37 handlers sequentially, then handles global hotkeys.
- **`OnApplicationQuit()`** - Cleanup: AudioNavigationHandler, SDL2Controller, ScreenReader shutdown.

### Secondary Entry Points (Harmony Patches)

These are applied during `OnInitializeMelon()` and intercept game methods:

1. **`DialogTextPatch.Apply()`** (`C:\Users\Amethyst\projects\digimon world, next order\DialogTextPatch.cs`)
   - Patches: `EventWindowPanel.TextShrink`, `TalkMain.PlayVoiceText`, `uCommonMessageWindow.SetMessage`, `uCommonMessageWindow.SetItemMessage`, `uDigimonMessagePanel.StartMessage`, `uFieldPanel.StartDigimonMessage`
   - Purpose: Intercepts dialog text for screen reader announcement

2. **`GamepadInputPatch.Apply()`** (`C:\Users\Amethyst\projects\digimon world, next order\GamepadInputPatch.cs`)
   - Patches: `Pad._Update`, `PadManager.GetInput`, `PadManager.GetTrigger`, `PadManager.GetRepeat`, `PadManager.IsTrigger`, `PadManager.IsRepeat`, `PadManager.IsInput`
   - Purpose: Injects SDL3 controller input into the game's input system

3. **`SteamInputPatch`** (`C:\Users\Amethyst\projects\digimon world, next order\SteamInputPatch.cs`)
   - Patches: `NameEntry.ShowSteamTextInput`
   - Purpose: Blocks inaccessible Steam text input overlay
   - Note: Has its own `Initialize()` method but is NOT called from Main.cs (appears unused currently)

---

## All Source Files by Category

### Infrastructure / Core Systems (9 files)

| File | Class | Purpose |
|------|-------|---------|
| `Main.cs` | `Main : MelonMod` | Entry point, orchestrates all handlers |
| `ScreenReader.cs` | `ScreenReader` (static) | Tolk-based screen reader TTS output |
| `ModInputManager.cs` | `ModInputManager` (static) + `ActionBindings`, `ControllerButton` enum, `InputBinding` | Central input system with configurable hotkeys |
| `InputConfig.cs` | `InputConfig` (static) | Parses binding strings from hotkeys.ini |
| `SDL2Controller.cs` | `SDL2Controller` (static) | SDL3 gamepad support via P/Invoke |
| `TriggerInput.cs` | `TriggerInput` (static) | Unity-based analog trigger reading fallback |
| `DebugLogger.cs` | `DebugLogger` (static) | File-based debug logging |
| `PositionalAudio.cs` | `PositionalAudio` + `LoopingWaveProvider` | NAudio-based 3D positional audio |
| `ToneGenerator.cs` | `ToneSource : MonoBehaviour` + `ToneGenerator` (static) | Unity AudioSource tone generation |

### Harmony Patches (3 files)

| File | Class | Purpose |
|------|-------|---------|
| `DialogTextPatch.cs` | `DialogTextPatch` (static) | Intercepts game dialog text for TTS |
| `GamepadInputPatch.cs` | `GamepadInputPatch` (static) | Injects SDL3 input into game input system |
| `SteamInputPatch.cs` | `SteamInputPatch` (static) | Blocks Steam text input overlay |

### UI/Menu Handler Files (28 files)

Each handler follows the same pattern: class with `Update()` method called every frame, `IsOpen()` for visibility check, and `AnnounceStatus()` for repeat-key.

| File | Class | Game System |
|------|-------|-------------|
| `TitleMenuHandler.cs` | `TitleMenuHandler` | Title/main menu |
| `OptionsMenuHandler.cs` | `OptionsMenuHandler` | System settings menu |
| `NameEntryHandler.cs` | `NameEntryHandler` | Character name input |
| `DifficultyDialogHandler.cs` | `DifficultyDialogHandler` | Difficulty selection |
| `CharaSelectHandler.cs` | `CharaSelectHandler` | Character/partner selection |
| `DigiEggHandler.cs` | `DigiEggHandler` | Digi-Egg selection |
| `GenealogyHandler.cs` | `GenealogyHandler` | Genealogy/lineage panel |
| `DialogHandler.cs` | `DialogHandler` | Story dialog windows |
| `DialogChoiceHandler.cs` | `DialogChoiceHandler` | Dialog choice selection |
| `MessageWindowHandler.cs` | `MessageWindowHandler` | General message windows |
| `CommonYesNoHandler.cs` | `CommonYesNoHandler` | Yes/No confirmation dialogs |
| `CommonSelectWindowHandler.cs` | `CommonSelectWindowHandler` | Generic selection windows |
| `CampCommandHandler.cs` | `CampCommandHandler` | Camp/rest menu |
| `TradePanelHandler.cs` | `TradePanelHandler` | Trading interface |
| `RestaurantPanelHandler.cs` | `RestaurantPanelHandler` | Restaurant menu |
| `TrainingPanelHandler.cs` | `TrainingPanelHandler` | Training selection |
| `TrainingBonusHandler.cs` | `TrainingBonusHandler` | Training bonus roulette |
| `TrainingResultHandler.cs` | `TrainingResultHandler` | Training results display |
| `ColosseumPanelHandler.cs` | `ColosseumPanelHandler` | Colosseum/arena |
| `FarmPanelHandler.cs` | `FarmPanelHandler` | Farm management |
| `SavePanelHandler.cs` | `SavePanelHandler` | Save/load game |
| `FieldItemPanelHandler.cs` | `FieldItemPanelHandler` | Item use in field |
| `StoragePanelHandler.cs` | `StoragePanelHandler` | Item storage |
| `MapPanelHandler.cs` | `MapPanelHandler` | Map/area navigation |
| `PartnerPanelHandler.cs` | `PartnerPanelHandler` | Partner Digimon details |
| `ItemPickPanelHandler.cs` | `ItemPickPanelHandler` | Item pickup selection |
| `MailPanelHandler.cs` | `MailPanelHandler` | Mail/message system |
| `DigiviceTopPanelHandler.cs` | `DigiviceTopPanelHandler` | Digivice top-level panel |

### Specialized Handler Files (6 files)

| File | Class | Purpose |
|------|-------|-------------|
| `TamerPanelHandler.cs` | `TamerPanelHandler` | Tamer stats panel |
| `ZonePanelHandler.cs` | `ZonePanelHandler` | Zone/area info panel |
| `CarePanelHandler.cs` | `CarePanelHandler` | Digimon care panel |
| `EvolutionHandler.cs` | `EvolutionHandler` | Digivolution events |
| `ModSettingsHandler.cs` | `ModSettingsHandler` | In-mod settings menu (F10) |
| `NavigationListHandler.cs` | `NavigationListHandler` | Categorized POI navigation (NPCs, items, transitions, enemies, facilities) |

### Field and Battle HUD Handlers (6 files)

| File | Class | Purpose |
|------|-------|-------------|
| `FieldHudHandler.cs` | `FieldHudHandler` | Field partner status via hotkeys |
| `BattleHudHandler.cs` | `BattleHudHandler` | Battle partner status announcements |
| `BattleOrderRingHandler.cs` | `BattleOrderRingHandler` | Battle order ring navigation |
| `BattleItemHandler.cs` | `BattleItemHandler` | Battle item selection |
| `BattleDialogHandler.cs` | `BattleDialogHandler` | In-battle dialog |
| `BattleTacticsHandler.cs` | `BattleTacticsHandler` | Battle tactics selection |
| `BattleResultHandler.cs` | `BattleResultHandler` | Battle result screen |
| `AudioNavigationHandler.cs` | `AudioNavigationHandler` | Always-on positional audio for nearby objects |
| `CommonMessageMonitor.cs` | `CommonMessageMonitor` | Polls for notification text changes |

---

## Configuration Files

### 1. DigimonNOAccess.csproj
- **Path:** `C:\Users\Amethyst\projects\digimon world, next order\DigimonNOAccess.csproj`
- **Controls:** Build configuration, target framework, assembly references, NuGet packages, compilation exclusions, output path
- **Format:** MSBuild XML (SDK-style)

### 2. hotkeys.ini (generated at runtime)
- **Generated by:** `ModInputManager.SaveDefaultConfig()` in `ModInputManager.cs` (line 647)
- **Runtime path:** Same folder as the mod DLL (game's Mods folder)
- **Controls:** All configurable keyboard and controller bindings for mod actions
- **Format:** INI with `[Keyboard]` and `[Controller]` sections
- **Reload:** F8 key at runtime
- **Contains bindings for:** RepeatLast, AnnounceStatus, ToggleVoicedText, Partner1/2Status/Effects/Mood/Info, BattlePartner1/2HP/Order, NavNextCategory/PrevCategory/PrevEvent/CurrentEvent/NextEvent/ToEvent

### 3. CLAUDE.md
- **Path:** `C:\Users\Amethyst\projects\digimon world, next order\CLAUDE.md`
- **Controls:** Claude Code AI assistant behavior, coding conventions, project rules
- **Format:** Markdown

### 4. CLAUDE.de.md
- **Path:** `C:\Users\Amethyst\projects\digimon world, next order\CLAUDE.de.md`
- **Controls:** Same as CLAUDE.md but in German

### 5. .gitignore (if present)
- Not detected via glob; git status shows only `REFACTOR_PROMPT.md` as untracked, suggesting most build artifacts are gitignored

---

## Fixed (Non-Configurable) Keys

Defined directly in `Main.cs` (not via hotkeys.ini):
- **F8** - Reload hotkey config (line 230)
- **F9** - Toggle input debug mode (line 235)
- **F10** - Toggle mod settings menu (defined in `ModSettingsHandler.cs`, line 41)

---

## Initialization Flow

```
MelonLoader loads DigimonNOAccess.dll
  |
  v
Main.OnInitializeMelon()
  |-- DebugLogger.Initialize()           [file logging]
  |-- ModInputManager.Initialize()       [hotkey system]
  |   |-- SDL2Controller.Initialize()    [SDL3 gamepad, optional]
  |   |-- RegisterDefaultBindings()      [default hotkeys]
  |   |-- LoadConfig()                   [hotkeys.ini override]
  |-- ScreenReader.Initialize()          [Tolk -> NVDA/JAWS]
  |-- Harmony patches
  |   |-- DialogTextPatch.Apply()        [text interception]
  |   |-- GamepadInputPatch.Apply()      [SDL3 input injection, conditional]
  |-- Instantiate 37 handler objects
  |
  v
Main.OnUpdate() [every frame]
  |-- ModInputManager.Update()           [poll input state]
  |   |-- SDL2Controller.Update()        [SDL3 poll]
  |-- [37 handler].Update()             [each handler checks its game panel]
  |-- HandleGlobalKeys()                 [F1/F2/F5/F8/F9]
  |
  v
Main.OnApplicationQuit()
  |-- AudioNavigationHandler.Cleanup()
  |-- SDL2Controller.Shutdown()
  |-- ScreenReader.Shutdown()
```

---

## Handler Pattern

Every handler follows the same general pattern (exemplified by `TitleMenuHandler`):

1. **Field caching:** Caches references to game UI objects (e.g., `uTitlePanel`, `MainTitle`)
2. **State tracking:** Tracks previous state (`_wasOpen`, `_lastCursor`, etc.) to detect changes
3. **`Update()` method:** Called every frame; checks if panel is visible, detects state transitions
4. **`IsOpen()` method:** Returns whether this handler's panel is currently active
5. **`AnnounceStatus()` method:** Speaks current state for the repeat/status hotkey
6. **Screen reader output:** Calls `ScreenReader.Say()` with formatted text on state changes

The handlers do NOT share a common base class or interface. Each is a standalone class with conventionally-named methods.

---

## Sound Assets

All in `C:\Users\Amethyst\projects\digimon world, next order\sounds\`:

- `item.wav` - Item proximity sound
- `potential npc.wav` - NPC proximity sound
- `potential enemie digimon.wav` - Enemy proximity sound
- `transission.wav` - Area transition proximity sound
- `door.wav` - Door proximity sound
- `wall up.wav`, `wall down.wav`, `wall left.wav`, `wall right.wav` - Wall detection sounds

Used by `PositionalAudio.cs` and `AudioNavigationHandler.cs`.

---

## Scripts

### Get-MelonLoaderInfo.ps1
- **Path:** `C:\Users\Amethyst\projects\digimon world, next order\scripts\Get-MelonLoaderInfo.ps1`
- **Purpose:** Parses MelonLoader `Latest.log` to extract game name, developer, runtime type, Unity version
- **Language:** PowerShell (German comments/output)

### Test-ModSetup.ps1
- **Path:** `C:\Users\Amethyst\projects\digimon world, next order\scripts\Test-ModSetup.ps1`
- **Purpose:** Validates mod project setup (MelonLoader installation, Tolk DLLs, .csproj configuration, decompiled code)
- **Language:** PowerShell (German comments/output)

---

## Templates

All in `C:\Users\Amethyst\projects\digimon world, next order\templates\`:

- `csproj.template` - MSBuild project file template
- `Main.cs.template` - MelonMod entry point template
- `Handler.cs.template` - Accessibility handler template
- `ScreenReader.cs.template` - Screen reader communication template
- `DebugLogger.cs.template` - Debug logger template
- `Loc.cs.template` - Localization helper template
- `game-api.md.template` - Game API documentation template

These are for bootstrapping new accessibility mod projects and are excluded from compilation.

---

## Key Observations

1. **Flat file structure:** All 46 .cs source files are in the project root directory with no subdirectories for organization.
2. **No solution file:** Single project, no multi-project structure.
3. **No test framework:** No test files, no test project, no test runner configuration.
4. **No interface/base class for handlers:** All 37+ handlers are concrete classes with no shared abstraction despite following identical patterns.
5. **Sequential update loop:** All handlers are updated sequentially in `Main.OnUpdate()` with hardcoded references.
6. **Main.cs is a registration hub:** The `Main` class holds 37 handler fields, instantiates them all manually, updates them all manually, and contains the entire priority-based `AnnounceCurrentStatus()` dispatch logic.
7. **Mixed concerns in `ModInputManager.cs`:** Contains the `ModInputManager` class, `ActionBindings` class, `ControllerButton` enum, and `InputBinding` class all in one file (898 lines).
8. **SteamInputPatch appears unused:** Has `Initialize()` and `Shutdown()` methods but neither is called from `Main.cs`.
9. **Naming inconsistency in SDL2Controller:** Class is named `SDL2Controller` but actually uses SDL3 (comment says "Keep class name for compatibility").
