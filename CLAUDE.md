# DigimonNOAccess — Digimon World: Next Order accessibility mod

A screen-reader accessibility mod that makes the game playable by blind players.
Every menu, the battle system, field navigation with pathfinding and auto-walk,
spatial audio with HRTF, and an in-game accessibility settings menu.

The global `H:\.claude\CLAUDE.md` carries the standing rules for all of
Amethyst's projects — how to talk to them, no-guessing, no hacky workarounds,
the Claude/Codex teamwork protocol. This file does not repeat those. It records
what is specific to **this** project, and wins on conflict.

## Environment

- **Game directory:** `C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order`
- **Engine:** Unity 2019.4.11f1, Il2Cpp, **64-bit**
- **Loader:** MelonLoader (net6), Harmony patches plus a few native hooks
- **Build:** `"C:\Program Files\dotnet\dotnet.exe" build DigimonNOAccess.csproj`
  (the full path is required on this machine; plain `dotnet` is not on PATH)
- Single `.csproj`, no `.sln`, no test framework. Amethyst verifies in-game.

### Where the built mod goes

`bin\` is the deployable set. It is copied into `<game>\Mods\`:

- `DigimonNOAccess.dll` plus the NAudio DLLs
- `prism.dll` — speech
- `phonon.dll` — Steam Audio HRTF
- `sounds\` — the WAV files for spatial audio
- `settings.json` and `hotkeys.ini` are written at runtime next to the DLL

The sounds folder is resolved relative to the assembly: first
`<parent of mod folder>\sounds`, then `<mod folder>\sounds`. In a normal install
that resolves to `<game>\Mods\sounds`.

## Hard rules for this project

These are the ones that have actually bitten us here. Breaking them has cost
real debugging rounds and, twice, a dead partner Digimon.

### Reverse engineering is ground truth

- Search `decompiled\` for the real class, method and field names before writing
  a line. Never guess a signature. `decompiled\Il2Cpp\` is the game's own code.
- `docs\game-api.md` is a convenience index, not the source of truth. If it
  disagrees with `decompiled\`, the decompiled source wins and the doc is wrong.
- Native work (CSVB script engine, anything Harmony cannot reach) goes through
  Ghidra. Scripts live in `ghidra_project\`; Ghidra work products stay local and
  are not committed.
- Method RVAs are in `<game>\Il2CppDumper-win-v6.7.46\script.json`.

### Resolve by name, never by address

The game gets patched. Anchors must be class and method names, or another stable
identifier. The one place we use addresses is the native hooks in
`CareMechanicsPatch.cs`, and they are documented there with what they are and how
they were found — treat any address as suspect after a game update.

**Two native-hook landmines, both confirmed by killing a Digimon:**

- Never hook `SetPartnerFatigue` at `0x5957C0`. The function is 24 bytes; the
  trampoline overwrites the adjacent `m_lifetime` field, which causes instant
  death and an infinite rebirth loop.
- Never call `SetFatigue()` or `SetSatiety()` to reset a stat. They corrupt the
  life gauge. Set the field properties directly (`pd.m_fatigue = 0`).
- Never write `m_lifetime` at all. It drains naturally from fatigue.

### Never hardcode spoken text

Everything the mod says comes from the game's own text: `UILabel.text`,
`Localization`, the `Parameter*` tables, or a CSVB record. This is a
multi-language game and hardcoded English silently breaks every other language.
The only exception is pre-baked art with no text source anywhere — and that gets
called out, not papered over.

Our own UI strings (the accessibility menu, hotkey names, "3 of 8") are the
mod's own words and are fine in English.

### Trace struct-offset chains end to end

For anything that reaches native code, follow the pointer from its anchor to the
exact field the consumer dereferences, in the RE tool, in the session that uses
it. A chain that is one hop short still reads fine at every step and fails later
inside the game's own code. Never fail silently on a hop — each one logs its own
named reason.

### Thread safety

Audio runs on background threads; the game runs on the Unity main thread.

- Capture positions and game state on the game thread, hand them to the audio
  code through a lock, and never touch Unity objects from an audio thread.
- `PositionalAudio` and `PathfindingBeacon` each run a ~60fps update thread that
  only reads snapshotted floats.
- Speech is serialized inside `ScreenReader`; call it from anywhere.

## Architecture

### Handlers

One handler per screen or menu, all registered in one list in `Main.cs`, sorted
by `Priority` (lowest first). Two shapes:

- `HandlerBase<TPanel>` — the standard lifecycle (open, close, cursor update)
  for a panel-backed screen. Most handlers use this.
- `IAccessibilityHandler` directly — `Update()`, `IsOpen()`, `AnnounceStatus()`,
  `Priority`. For handlers with their own state machine.

`AnnounceCurrentStatus()` walks the list in priority order and the first handler
reporting `IsOpen()` answers. That ordering is why `Priority` matters.

Files are flat in the project root. Naming: `[Feature]Handler` for handlers,
`[Feature]Patch` for Harmony/native patch classes, `_camelCase` for private
fields, English for all logs and comments.

### Speech

- `ScreenReader` is the only way to speak. `Say`, `SayQueued`, `Silence`,
  `RepeatLast`, `IsAvailable`.
- It is backed by **Prism** (`PrismInterop.cs`, `prism.dll`), which targets
  whatever screen reader is running — NVDA, JAWS, SAPI, OneCore on Windows,
  Orca or speech-dispatcher on Linux, VoiceOver on Mac. **Tolk is gone**; do not
  reintroduce a Windows-only or NVDA-only speech path.
- Prism missing or no backend available is a supported state: everything
  no-ops and the mod still runs.
- Default announcement format is a single call: name, then optional description,
  then index of total. "Fire Blast, 120 power, 3 of 8".

### Spatial audio

All positional sound shares one output and one listener orientation.

- `AudioOutputMixer` — one `WaveOutEvent` and one `MixingSampleProvider` for
  everything. Never create another `WaveOutEvent`; competing outputs stutter.
- `HrtfSampleProvider` — Steam Audio binaural HRTF, mono in, stereo out. Falls
  back to `PanningSampleProvider` when `phonon.dll` is absent.
- `CameraOrientation` — the single source of truth for the listener. Direction
  is measured against the **camera** (`CameraManager.Ref.m_mainCameraObject`,
  which carries the game's own AudioListener), never the player transform. A
  sound oriented on the player does not move when the camera turns, which is the
  bug this class exists to prevent.
- Chain for any source: mono → HRTF (or panner) → volume → mixer.
- `PositionalAudio` for world objects, `PathfindingBeacon` for the pathfinding
  tracker beep, `WallAudioPoint` / `CompassWallEmitter` for wall audio
  (currently disabled).
- NAudio quirk: its `WaveBuffer` overlays `byte[]` and `float[]`, so
  `Array.Copy` throws `ArrayTypeMismatchException`. Use `Buffer.BlockCopy`.

### Settings and input

- `ModSettings` — static, JSON-persisted to `settings.json`. Add a property, add
  it to `ApplyFromData`, `CreateData` and `SettingsData`, or it will not persist.
- `AccessibilityMenuHandler` — the mod's own settings menu, injected into the
  game's options panel by `OptionPanelPatch`. Read the comments in that patch
  before touching `m_items`; the prefix/postfix sandwich exists because the game
  rebuilds its visual slots from that array.
- `ModInputManager` — configurable hotkeys, keyboard and controller, with
  `ActionContext` gating so the same binding can mean different things in field
  and battle. `SDLController` wraps SDL3.
- Respect the game's own controls. Never override a game key.

### Utilities

`TextUtilities` (rich-text stripping, placeholder detection), `PartnerUtilities`,
`AnnouncementBuilder`, `GameStateService` (all "where is the player" checks),
`DebugLogger` (always use this, never MelonLogger directly).

## Design principles

- **Playability, not simplification.** Make the game playable the way sighted
  players play it. Cheats and gameplay toggles are a last resort, and when they
  exist they are opt-in settings, never the default.
- Cache lookups, avoid per-frame `FindObjectOfType` where an event will do.
- Announce state changes, not state. Repeated announcements are worse than none.
- Handle rapid input; the player is often holding a direction.

## Documentation and records

- `CHANGELOG.md` — plain language, written for blind end users, not commit
  messages. Update it when behaviour a player would notice changes.
- `accessibility-audit.md` — the map of what is still missing, with the hook for
  each gap.
- `QUESTIONS.md` — open design decisions waiting on Amethyst. Answer inline.
- `TEAM-LOG.md` — one line per working session: who implemented, who reviewed.
- `docs\game-api.md` — index of verified game classes and methods.
- `docs\ACCESSIBILITY_MODDING_GUIDE.md` — code patterns.
- `AGENTS.md` — Codex's operating notes for this repo.

## Before implementing

1. Search `decompiled\` for the real names. Never guess.
2. Check whether an existing handler already owns that screen (`Main.cs` list).
3. Check `docs\game-api.md` for what is already documented — then verify it.
4. Follow the cross-review rule in the global instructions: whoever implements,
   the other engineer reviews before it is called done.
