# DigimonNOAccess — Codex operations notes

Read `CLAUDE.md` first; it is the master truth for this repo and its rules win on
any conflict. This file adds only what Codex needs to operate here. Dispatch
briefs ride on top of both.

## The mod

- MelonLoader mod for **Digimon World: Next Order** (Unity 2019.4.11f1, Il2Cpp,
  **64-bit**, net6). Sources are flat in the repo root, one `.csproj`, no `.sln`.
- Build: `"C:\Program Files\dotnet\dotnet.exe" build DigimonNOAccess.csproj`
  (the full path is required; plain `dotnet` is not on PATH). Output goes to
  `bin\`.
- There is **no test suite**. A clean build plus a careful read is the whole
  automated safety net; Amethyst verifies behaviour in-game.
- Never deploy into the game folder, never commit, never push. Report instead.

## Ground truth

- `decompiled\Il2Cpp\` is the game's own decompiled C#. It is the only
  acceptable source for a class, method, field or enum name. Never assert
  something you have not read there.
- `docs\game-api.md` is a convenience index and can be stale. Where it disagrees
  with `decompiled\`, the decompiled source wins and the doc is the bug.
- `decompiled\` is gitignored (copyrighted game code) — read it, never commit it.
- Method RVAs, when a native address is genuinely needed:
  `C:\Program Files (x86)\Steam\steamapps\common\Digimon World Next Order\Il2CppDumper-win-v6.7.46\script.json`.

## Ghidra

- Ghidra work for this project lives in `ghidra_project\` (helper scripts,
  gitignored outputs). Headless Ghidra is at
  `H:\projects\ghidra_11.3.2_PUBLIC\support\analyzeHeadless.bat`.
- A Ghidra project open takes an **exclusive lock**, so one project directory can
  never serve two engineers. If a dispatch needs Ghidra, the brief names the exact
  project directory that is yours; do not open one that was not named.
- Read-only dispatches cannot launch Ghidra at all (opening writes a lock).
  Work from saved listings and the decompiled C# instead, and say so.
- Any label or comment you add is provisional: list every one (address plus text)
  in your report. Nothing is banked until Claude validates it.

## Landmines — do not step on these

Each of these has already cost a dead partner Digimon or a debugging round trip.

- Never hook `SetPartnerFatigue` at `0x5957C0`. 24 bytes; the trampoline
  overwrites the adjacent `m_lifetime` and causes instant death plus an infinite
  rebirth loop.
- Never call `SetFatigue()` or `SetSatiety()` to reset a stat — they corrupt the
  life gauge. Set the field properties directly (`pd.m_fatigue = 0`).
- Never write `m_lifetime`.
- Never create a second `WaveOutEvent`. All audio goes through
  `AudioOutputMixer`; competing outputs stutter.
- NAudio's `WaveBuffer` overlays `byte[]` and `float[]`, so `Array.Copy` throws
  `ArrayTypeMismatchException` on those buffers. Use `Buffer.BlockCopy`.
- Never hardcode text the mod speaks. It comes from the game's own
  `UILabel.text`, `Localization`, `Parameter*` tables or CSVB records. The mod's
  own UI wording (menu labels, "3 of 8") is the exception.
- Never orient spatial audio on the player transform. `CameraOrientation` is the
  single source of truth for the listener, and it reads the game camera.

## Shell and sandbox realities

- Invoke bash by full path (`C:\Program Files\Git\bin\bash.exe`) — plain `bash`
  is WSL here and cannot handle these paths.
- Prefer simple single-cmdlet reads and `python3` for parsing. The
  non-interactive policy denies `cmd /c`, nested-quoted `pwsh -Command "..."`,
  and `try/catch` one-liners.
- H: is a network share. Write dispatches to it run unsandboxed under an explicit
  per-dispatch grant; treat every write as deliberate and list it in the report.

## Report format

Summary / Files changed / Commands run and results / Findings and annotations
(for RE work: every Ghidra label and comment, address plus text) / What Amethyst
should test / Open risks.

Amethyst reads everything with a screen reader: short headings, short bullets,
no tables, no ASCII art.
