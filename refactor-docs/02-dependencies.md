# Dependency Analysis Report

Project: DigimonNOAccess
File: `C:\Users\Amethyst\projects\digimon world, next order\DigimonNOAccess.csproj`
Target Framework: net6.0
Language Version: latest
Implicit Usings: disabled

---

## 1. External Dependencies (Complete Inventory)

### 1.1 NuGet Package Dependencies

- **NAudio** v2.2.1 (declared in `DigimonNOAccess.csproj` line 27)
  - Transitive dependencies (resolved from `obj/project.assets.json`):
    - NAudio.Asio v2.2.1
    - NAudio.Core v2.2.1
    - NAudio.Midi v2.2.1
    - NAudio.Wasapi v2.2.1
    - NAudio.WinMM v2.2.1
    - Microsoft.Win32.Registry v4.7.0
    - Microsoft.NETCore.Platforms v3.1.0
    - System.Security.AccessControl v4.7.0
    - System.Security.Principal.Windows v4.7.0
  - Used in: `AudioNavigationHandler.cs`, `PositionalAudio.cs`
  - Namespaces used: `NAudio.Wave`, `NAudio.Wave.SampleProviders`

### 1.2 Local Assembly References (MelonLoader Framework)

All referenced from `C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order\MelonLoader\`

- **MelonLoader** (`net6\MelonLoader.dll`) - csproj line 33
  - Used in: `Main.cs`, `ScreenReader.cs`, `OptionsMenuHandler.cs`, `DebugLogger.cs`
  - Provides: `MelonMod`, `MelonInfo`, `MelonGame`, `MelonAssembly`, `Melon<T>.Logger`, `LoggerInstance`

- **Il2CppInterop.Runtime** (`net6\Il2CppInterop.Runtime.dll`) - csproj line 36
  - Used in: `ToneGenerator.cs` (ClassInjector.RegisterTypeInIl2Cpp, Il2CppStructArray), `DialogChoiceHandler.cs` (implicit array access)
  - Provides: Il2Cpp interop infrastructure, type injection, array wrappers

- **Il2Cppmscorlib** (`Il2CppAssemblies\Il2Cppmscorlib.dll`) - csproj line 39
  - Used implicitly: Required at runtime for Il2Cpp type system (string conversions, base types)
  - Not directly referenced via namespace in code

- **Assembly-CSharp** (`Il2CppAssemblies\Assembly-CSharp.dll`) - csproj line 42
  - Used in: Nearly all handler files via `using Il2Cpp;`
  - Provides: All game types (PadManager, EventWindowPanel, TalkMain, NameEntry, CampCommandPanel, etc.)

- **Assembly-CSharp-firstpass** (`Il2CppAssemblies\Assembly-CSharp-firstpass.dll`) - csproj line 45
  - Used implicitly: May contain types referenced by Assembly-CSharp at runtime
  - Not directly referenced by any namespace in source code

- **UnityEngine.CoreModule** (`Il2CppAssemblies\UnityEngine.CoreModule.dll`) - csproj line 48
  - Used in: Nearly all files via `using UnityEngine;`
  - Provides: GameObject, Object, MonoBehaviour, Vector3, Mathf, KeyCode, Input, Transform, etc.

- **UnityEngine.UI** (`Il2CppAssemblies\UnityEngine.UI.dll`) - csproj line 51
  - Used in: `DialogHandler.cs`, `DifficultyDialogHandler.cs`, `MessageWindowHandler.cs`, `NameEntryHandler.cs`, `TamerPanelHandler.cs`
  - Provides: `Text` component (GetComponentsInChildren<Text>, .text property)

- **UnityEngine.InputLegacyModule** (`Il2CppAssemblies\UnityEngine.InputLegacyModule.dll`) - csproj line 54
  - Used in: `Main.cs`, `ModInputManager.cs`, `ModSettingsHandler.cs`, `TriggerInput.cs`
  - Provides: `Input.GetKeyDown`, `Input.GetKey`, `Input.GetAxisRaw`

- **UnityEngine.AudioModule** (`Il2CppAssemblies\UnityEngine.AudioModule.dll`) - csproj line 57
  - Used in: `ToneGenerator.cs`
  - Provides: `AudioSource`, `AudioClip`, `OnAudioFilterRead`

- **UnityEngine.PhysicsModule** (`Il2CppAssemblies\UnityEngine.PhysicsModule.dll`) - csproj line 60
  - Used in: `AudioNavigationHandler.cs`
  - Provides: `Physics.Raycast`

- **UnityEngine.AIModule** (`Il2CppAssemblies\UnityEngine.AIModule.dll`) - csproj line 63
  - Used in: `AudioNavigationHandler.cs`, `NavigationListHandler.cs`
  - Provides: `NavMesh`, `NavMeshHit`, `NavMeshPath`, `NavMeshPathStatus`

- **0Harmony** (`net6\0Harmony.dll`) - csproj line 66
  - Used in: `Main.cs`, `DialogTextPatch.cs`, `GamepadInputPatch.cs`, `SteamInputPatch.cs`
  - Provides: `HarmonyLib.Harmony`, `HarmonyMethod`, `HarmonyPatch`

### 1.3 Native/P-Invoke Dependencies (not in .csproj)

These are loaded at runtime via `DllImport` / `System.Runtime.InteropServices`:

- **Tolk.dll** - Referenced in `ScreenReader.cs` via P/Invoke
  - Screen reader abstraction layer for NVDA/JAWS/etc.

- **SDL3.dll** - Referenced in `SDL2Controller.cs` via P/Invoke
  - Controller input (class named SDL2Controller but comments indicate SDL3 API)

---

## 2. Unused / Potentially Unused Dependencies

### 2.1 Completely Unused Assembly Reference

- **Assembly-CSharp-firstpass** (csproj line 44-46)
  - No source file imports or references any type from this assembly
  - No namespace from this assembly is used
  - Verdict: **UNUSED** -- can likely be removed unless it is needed as a transitive dependency for Assembly-CSharp types at compile time. Test removal by building without it.

### 2.2 Unused Class: SteamInputPatch

- File: `C:\Users\Amethyst\projects\digimon world, next order\SteamInputPatch.cs`
- `SteamInputPatch.Initialize()` and `SteamInputPatch.Shutdown()` are never called from `Main.cs` or any other file
- The class is entirely self-contained and unreachable
- It also has unused `using MelonLoader;` (never references any MelonLoader types)
- Verdict: **DEAD CODE** -- class is never instantiated or invoked

### 2.3 Unused `using` Directives (per file)

The following files import namespaces they never use. While this does not affect runtime behavior, it is a code cleanliness issue.

**`using MelonLoader;` imported but never used (18 files):**
- `CampCommandHandler.cs`
- `CarePanelHandler.cs`
- `CharaSelectHandler.cs`
- `ColosseumPanelHandler.cs`
- `CommonSelectWindowHandler.cs`
- `DialogHandler.cs`
- `DifficultyDialogHandler.cs`
- `DigiviceTopPanelHandler.cs`
- `FarmPanelHandler.cs`
- `FieldItemPanelHandler.cs`
- `ItemPickPanelHandler.cs`
- `MailPanelHandler.cs`
- `MapPanelHandler.cs`
- `MessageWindowHandler.cs`
- `NameEntryHandler.cs`
- `PartnerPanelHandler.cs`
- `RestaurantPanelHandler.cs`
- `SavePanelHandler.cs`
- `SteamInputPatch.cs`
- `StoragePanelHandler.cs`
- `TitleMenuHandler.cs`
- `TradePanelHandler.cs`
- `TrainingPanelHandler.cs`
- `ZonePanelHandler.cs`

**`using UnityEngine.UI;` imported but never used (1 file):**
- `CharaSelectHandler.cs`

**`using Il2CppInterop.Runtime.InteropTypes.Arrays;` possibly unnecessary (1 file):**
- `DialogChoiceHandler.cs` -- imports the namespace but accesses Il2Cpp arrays implicitly through game types. May be needed for implicit conversions at compile time.

---

## 3. Internal Module Dependencies (Cross-References)

All 54 source files reside in a single namespace: `DigimonNOAccess`. There are no sub-namespaces.

### 3.1 Dependency Graph (Internal)

**Core Infrastructure (depended upon by many):**
- `ScreenReader` -- used by 40+ handler files for speech output
- `DebugLogger` -- used by 40+ handler files for debug logging
- `ModInputManager` -- used by `Main.cs`, `BattleHudHandler.cs`, `FieldHudHandler.cs`, `NavigationListHandler.cs`, `ModSettingsHandler.cs`
- `InputConfig` -- used by `ModInputManager.cs`
- `SDL2Controller` -- used by `ModInputManager.cs`, `GamepadInputPatch.cs`, `ModSettingsHandler.cs`, `Main.cs`
- `TriggerInput` -- used by `ModInputManager.cs`
- `DialogTextPatch` -- used by `Main.cs`, `MessageWindowHandler.cs`, `CommonMessageMonitor.cs`, `ModSettingsHandler.cs`

**Orchestrator:**
- `Main` (MelonMod) -- instantiates and calls Update() on all 39 handler objects, plus core infrastructure. This is the only orchestrator.

**Handler classes (leaf nodes -- only depend on infrastructure):**
All handler classes follow a uniform pattern:
- Depend on: `ScreenReader`, `DebugLogger`, and game types from `Il2Cpp`
- Some also depend on: `ModInputManager` (BattleHudHandler, FieldHudHandler, NavigationListHandler)
- Are depended on by: Only `Main.cs`

**Special relationships:**
- `Main.cs` -> `NavigationListHandler.SetEvolutionActive()` -> reads from `EvolutionHandler.IsActive()` (data flows through Main)
- `AudioNavigationHandler` -> `PositionalAudio` (uses PositionalAudio.SoundType)
- `ToneGenerator` -> defines `ToneSource` (MonoBehaviour registered via Il2Cpp injection)
- `GamepadInputPatch` -> `SDL2Controller` (extensive usage)
- `ModInputManager` -> `SDL2Controller`, `TriggerInput`, `InputConfig`

### 3.2 Full Dependency Map

```
Main.cs
  depends on:
    - HarmonyLib (0Harmony)
    - MelonLoader
    - UnityEngine (Input)
    - ScreenReader
    - DebugLogger
    - ModInputManager
    - DialogTextPatch
    - GamepadInputPatch
    - SDL2Controller
    - EvolutionHandler (reads IsActive())
    - NavigationListHandler (calls SetEvolutionActive())
    - AudioNavigationHandler (calls Cleanup())
    - All 39 handler classes (instantiation + Update() + AnnounceStatus()/IsOpen()/IsActive())

ModInputManager.cs
  depends on:
    - SDL2Controller
    - TriggerInput
    - InputConfig
    - DebugLogger
    - UnityEngine (Input, KeyCode)

GamepadInputPatch.cs
  depends on:
    - HarmonyLib
    - Il2Cpp (PadManager)
    - SDL2Controller
    - DebugLogger
    - UnityEngine

AudioNavigationHandler.cs
  depends on:
    - PositionalAudio
    - NAudio.Wave / NAudio.Wave.SampleProviders
    - UnityEngine.AI (NavMesh)
    - UnityEngine (Physics, Vector3, GameObject)
    - DebugLogger
    - ScreenReader
    - Il2Cpp (game types)

NavigationListHandler.cs
  depends on:
    - UnityEngine.AI (NavMesh, NavMeshPath)
    - ModInputManager
    - ScreenReader
    - DebugLogger
    - Il2Cpp (game types)

PositionalAudio.cs
  depends on:
    - NAudio.Wave / NAudio.Wave.SampleProviders
    - System.IO, System.Threading

ToneGenerator.cs
  depends on:
    - Il2CppInterop.Runtime.Injection (ClassInjector)
    - Il2CppInterop.Runtime.InteropTypes.Arrays (Il2CppStructArray)
    - UnityEngine (MonoBehaviour, AudioSource, GameObject)

ScreenReader.cs
  depends on:
    - MelonLoader (Melon<Main>.Logger)
    - System.Runtime.InteropServices (DllImport for Tolk.dll)

SDL2Controller.cs
  depends on:
    - System.Runtime.InteropServices (DllImport for SDL3.dll)

DialogTextPatch.cs
  depends on:
    - HarmonyLib
    - Il2Cpp (game types)
    - System.Text.RegularExpressions
    - ScreenReader
    - DebugLogger

SteamInputPatch.cs (DEAD CODE - never called)
  depends on:
    - HarmonyLib
    - Il2Cpp (NameEntry)
    - DebugLogger
```

---

## 4. Circular Dependencies

**No circular dependencies exist.**

All dependencies flow in one direction:
- `Main` -> Handlers -> Infrastructure (ScreenReader, DebugLogger, ModInputManager)
- Infrastructure classes do not reference handler classes
- Handler classes do not reference each other

The only cross-handler data flow is mediated by `Main.cs`:
- `Main` reads `_evolutionHandler.IsActive()` and passes it to `_navigationListHandler.SetEvolutionActive()`

This is a clean, tree-structured dependency graph with no cycles.

---

## 5. Outdated or Deprecated Dependencies

### 5.1 NAudio v2.2.1
- Status: **CURRENT** -- v2.2.1 is the latest stable release as of February 2026
- No newer version available

### 5.2 MelonLoader
- Version: Not explicitly versioned in .csproj (references local DLLs)
- Latest MelonLoader: v0.7.x
- Status: Cannot determine without checking the installed DLL version
- Note: The project targets net6.0 which is compatible with MelonLoader's net6 runtime

### 5.3 0Harmony (Harmony)
- Version: Bundled with MelonLoader (not independently versioned in project)
- Harmony 2.x is current; MelonLoader ships its own patched version
- Status: Managed by MelonLoader, not independently upgradeable

### 5.4 .NET 6.0 Target Framework
- Status: **.NET 6.0 reached end of support on November 12, 2024**
- .NET 6 is an LTS release that is now out of support
- However, this is constrained by MelonLoader's runtime requirements (MelonLoader provides the net6 runtime)
- Action: Cannot upgrade independently; must follow MelonLoader's supported framework versions

### 5.5 Unity Il2Cpp Assemblies
- Version: Game-specific, tied to the game's Unity version
- Status: Not independently upgradeable; determined by the game binary

---

## 6. Summary of Actionable Findings

### High Priority
1. **SteamInputPatch.cs is dead code** -- never called from anywhere. Either integrate it (call `Initialize()`/`Shutdown()` from `Main.cs`) or remove it entirely.

### Medium Priority
2. **Assembly-CSharp-firstpass** reference may be removable -- test building without it to verify.
3. **24 files have unused `using MelonLoader;`** -- these can be cleaned up for code hygiene.

### Low Priority
4. **CharaSelectHandler.cs** has unused `using UnityEngine.UI;` -- can be removed.
5. **DialogChoiceHandler.cs** may have unnecessary `using Il2CppInterop.Runtime.InteropTypes.Arrays;` -- test removal.
6. **.NET 6.0 is end-of-life** -- constrained by MelonLoader, not independently actionable.

### No Issues
- NAudio is at the latest version (v2.2.1)
- No circular dependencies exist
- Internal dependency structure is clean and tree-shaped
- All other assembly references are actively used in code
