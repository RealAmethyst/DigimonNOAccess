# 06 - Test Coverage Analysis

## Summary

**The project has no automated test framework, no test projects, and no unit tests.**

This is a single-project MelonLoader mod (`DigimonNOAccess.csproj`) with no solution file, no test project, and no test runner configuration. There are zero test files in the entire repository (excluding decompiled game code).

---

## Search Results

### Test Projects
- **`*.Tests.csproj`** - None found
- **`*.Test.csproj`** - None found
- **`*.sln`** - None found (no solution file at all)

### Test Files
- **`*Test.cs`** - One match: `decompiled\Il2Cpp\KeyConfigTest.cs` -- this is decompiled game code (class `KeyConfigTest : MonoBehaviour` in namespace `Il2Cpp`), NOT a project test file
- **`*Tests.cs`** - None found
- **`*Spec.cs`** - None found
- **`test/` or `tests/` directories** - None found

### Test Framework References
- **NUnit** - Not referenced in any `.csproj` or `.cs` file
- **xUnit** - Not referenced in any `.csproj` or `.cs` file
- **MSTest** - Not referenced in any `.csproj` or `.cs` file
- **Microsoft.NET.Test.Sdk** - Not referenced
- **coverlet / code coverage** - Not configured
- **`.runsettings`** - Not present

### Test-Related Configuration
- No test runner settings
- No CI/CD pipeline configuration that runs tests
- No code coverage configuration

---

## Existing Validation Scripts (Not Unit Tests)

The project has two PowerShell scripts in `scripts/` that perform environment validation, but these are NOT automated unit tests:

1. **`scripts/Test-ModSetup.ps1`** - Validates the mod development environment setup (checks for MelonLoader installation, Tolk DLLs, `.csproj` configuration, decompiled code presence). This is a manual pre-build environment check, not a code test.

2. **`scripts/Get-MelonLoaderInfo.ps1`** - Extracts game/runtime info from MelonLoader logs. A diagnostic utility, not a test.

---

## Source Files With No Test Coverage (All of Them)

Every source file in the project has zero test coverage. The complete list of 54 source files:

### Infrastructure / Core Systems (9 files)
- `Main.cs` - Mod entry point, handler orchestration, lifecycle management
- `ScreenReader.cs` - Tolk-based screen reader TTS output (static, P/Invoke)
- `ModInputManager.cs` - Central input system with configurable hotkeys (898 lines, multiple classes)
- `InputConfig.cs` - INI binding string parser (static)
- `SDL2Controller.cs` - SDL3 gamepad support via P/Invoke (static)
- `TriggerInput.cs` - Unity analog trigger reading fallback (static)
- `DebugLogger.cs` - File-based debug logging (static)
- `PositionalAudio.cs` - NAudio-based 3D positional audio
- `ToneGenerator.cs` - Unity AudioSource tone generation (static + MonoBehaviour)

### Harmony Patches (3 files)
- `DialogTextPatch.cs` - Intercepts game dialog text for TTS
- `GamepadInputPatch.cs` - Injects SDL3 input into game input system
- `SteamInputPatch.cs` - Blocks Steam text input overlay (appears unused)

### UI/Menu Handlers (28 files)
- `TitleMenuHandler.cs`
- `OptionsMenuHandler.cs`
- `NameEntryHandler.cs`
- `DifficultyDialogHandler.cs`
- `CharaSelectHandler.cs`
- `DigiEggHandler.cs`
- `GenealogyHandler.cs`
- `DialogHandler.cs`
- `DialogChoiceHandler.cs`
- `MessageWindowHandler.cs`
- `CommonYesNoHandler.cs`
- `CommonSelectWindowHandler.cs`
- `CampCommandHandler.cs`
- `TradePanelHandler.cs`
- `RestaurantPanelHandler.cs`
- `TrainingPanelHandler.cs`
- `TrainingBonusHandler.cs`
- `TrainingResultHandler.cs`
- `ColosseumPanelHandler.cs`
- `FarmPanelHandler.cs`
- `SavePanelHandler.cs`
- `FieldItemPanelHandler.cs`
- `StoragePanelHandler.cs`
- `MapPanelHandler.cs`
- `PartnerPanelHandler.cs`
- `ItemPickPanelHandler.cs`
- `MailPanelHandler.cs`
- `DigiviceTopPanelHandler.cs`

### Specialized Handlers (6 files)
- `TamerPanelHandler.cs`
- `ZonePanelHandler.cs`
- `CarePanelHandler.cs`
- `EvolutionHandler.cs`
- `ModSettingsHandler.cs`
- `NavigationListHandler.cs`

### Field and Battle HUD Handlers (8 files)
- `FieldHudHandler.cs`
- `BattleHudHandler.cs`
- `BattleOrderRingHandler.cs`
- `BattleItemHandler.cs`
- `BattleDialogHandler.cs`
- `BattleTacticsHandler.cs`
- `BattleResultHandler.cs`
- `AudioNavigationHandler.cs`
- `CommonMessageMonitor.cs`

---

## Broken, Skipped, or Obsolete Tests

Not applicable -- no tests exist.

---

## Testing Challenges for This Project

This project presents significant challenges for automated testing:

1. **Heavy runtime dependencies**: Almost every class depends on Unity engine types (`GameObject`, `Transform`, `MonoBehaviour`), Il2Cpp interop types, and MelonLoader APIs. These cannot be instantiated outside the game process.

2. **Static-heavy architecture**: Most infrastructure classes (`ScreenReader`, `ModInputManager`, `SDL2Controller`, `DebugLogger`, `InputConfig`, `TriggerInput`, `ToneGenerator`) are entirely static with no interfaces, making mocking impossible without refactoring.

3. **P/Invoke dependencies**: `ScreenReader.cs` (Tolk.dll), `SDL2Controller.cs` (SDL3.dll) use native DLL calls that require the actual DLLs at runtime.

4. **No abstraction layer**: Handlers directly call static methods on infrastructure classes and directly access game objects via Il2Cpp interop. There is no dependency injection, no interfaces, and no service abstraction.

5. **Game-state dependent logic**: Handler `Update()` methods check live game panel visibility and read UI element text, which requires a running game instance.

---

## Modules Most Suitable for Unit Testing (Priority Order)

Despite the challenges, certain modules contain logic that could be extracted and tested if the architecture were refactored:

### High Priority (Testable with moderate refactoring)

1. **`InputConfig.cs`** - INI binding string parser
   - Contains pure string parsing logic (`ParseBinding`, `ParseControllerBinding`)
   - Could be tested for correct parsing of various binding string formats
   - Minimal external dependencies

2. **`ModInputManager.cs`** - Specifically the binding registration and config load/save logic
   - `RegisterAction`, `RegisterDefaultBindings`, `LoadConfig`, `SaveDefaultConfig` contain logic that could be isolated
   - The `ActionBindings` and `InputBinding` classes could be tested independently
   - The input polling (`IsActionPressed`, `IsActionJustPressed`) depends on Unity/SDL but the binding data model does not

3. **`PositionalAudio.cs`** - Audio panning calculations
   - The stereo panning math (converting 3D positions to left/right audio balance) is pure calculation
   - Distance-based volume falloff is testable math
   - The `LoopingWaveProvider` audio stream logic could be tested if NAudio dependency is provided

### Medium Priority (Testable with interface extraction)

4. **`DebugLogger.cs`** - File logging
   - Log formatting and filtering logic could be tested
   - File I/O could be abstracted behind an interface

5. **Handler announcement text formatting** - Many handlers build announcement strings from game data
   - The string formatting/composition logic could be extracted into pure functions
   - Example: stat formatting in `FieldHudHandler`, `BattleHudHandler`, `TamerPanelHandler`

6. **`Main.cs`** - Handler priority/dispatch logic
   - `AnnounceCurrentStatus()` contains priority-ordered handler selection that could be tested if handlers implemented a common interface

### Low Priority (Requires major refactoring or integration testing)

7. **Harmony patches** (`DialogTextPatch.cs`, `GamepadInputPatch.cs`) - These are interception hooks that can only be meaningfully tested in integration with the game
8. **All handler `Update()` / `IsOpen()` methods** - Deeply coupled to live game UI state
9. **`SDL2Controller.cs`** / `TriggerInput.cs` - Hardware-dependent input

---

## Recommendations

1. **No tests exist today** -- any refactoring effort should prioritize introducing tests alongside architectural changes, not as a separate effort afterward.

2. **Start with `InputConfig.cs`** as a proof-of-concept test target. It has the least external dependencies and the most testable pure logic (string parsing).

3. **Extract interfaces for infrastructure** (`IScreenReader`, `IInputManager`, `ILogger`, `IAudioSystem`) to enable mocking and testing of handler logic without the game running.

4. **Consider an integration test harness** using MelonLoader's test capabilities or a custom in-game test runner for handlers that cannot be tested outside the game process.

5. **A test project would require**: A new `DigimonNOAccess.Tests.csproj` targeting `net6.0`, a test framework (xUnit or NUnit), a mocking library (Moq or NSubstitute), and interfaces extracted from the static infrastructure classes.
