# Team log

One line per working session. Newest at the bottom. Who implemented, who
reviewed, what changed.

- 2026-07-31 — Claude implemented: CLAUDE.md rewritten to match the global
  standards, AGENTS.md and this log added; speech backend migrated from Tolk to
  Prism (`PrismInterop.cs`, `ScreenReader.cs`, `prism.dll`); pathfinding beacon
  rebuilt onto the shared HRTF pipeline with camera-relative direction and the
  real `pathfinding_tracker.wav`; camera orientation consolidated into
  `CameraOrientation.cs`. Codex ran three read-only decompiled-code audit passes
  (UI/menus, battle, field/care) feeding `accessibility-audit.md`. Cross-review:
  Codex reviewed Claude's diff.

  Follow-up in the same session, after Amethyst answered `QUESTIONS.md`:
  dead `AudioNavigationHandler.Suspended` removed; template leftovers deleted
  (`CLAUDE.de.md`, `docs-de/`, `docs/setup-guide.md`, `templates/`); pathfinder
  volume slider with flat volume; partner condition on the F3/F4 hotkey;
  Text to Speech settings (engine, voice, rate, volume, speak-only-when-focused);
  battle buffs (`BattleBuffReader.cs`) with a toggle; localization fixes in
  `OptionsMenuHandler`, `AgreeWindowHandler`, `TrainingPanelHandler`.

  Two real defects found and fixed: auto-walk kept injecting stick input after
  leaving the field (found by Codex's field/care pass, mechanism confirmed by
  Claude), and hunger was paired with the wrong scale (`MIN_/MAX_MEAL_SIZE`
  rather than the actual 0..100 clamp) — caught by Ghidra within minutes of it
  coming online.

  Ghidra set up on `GameAssembly.dll` at `C:\Users\Amethyst\ghidra_dwno` with all
  84,160 IL2CPP method names applied. See `ghidra_project/README.md`.
