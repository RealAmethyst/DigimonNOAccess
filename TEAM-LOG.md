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
2026-07-31 - Codex - implemented Bucket A localization fixes A1-A96 across the 38 audited handlers and patches; A28 and A32 specification gaps were left unchanged where no verified source was reachable.

- 2026-08-01 — Codex implemented Bucket A of the localization audit (94 of 96
  items across 38 files, two correct partials); Claude reviewed and fixed three
  things it found. The new readers rejected null and empty text but not
  placeholder text, so raw `SYS_` keys and the game's Japanese
  "language not found" marker could have been spoken aloud - 61 guards extended
  with `TextUtilities.IsPlaceholderText`. A8 was reverted as unreachable dead
  code: its only caller already reads the same label first, with a placeholder
  filter the replacement lacked.

  Button hints then moved to the end of screen announcements. Two mistakes worth
  remembering: the hint check first ran on the STRIPPED text, but
  `StripRichTextTags` converts the button-glyph control characters into words -
  the very markers that identify a hint bar - so it never fired; and the
  re-append was only wired into `AnnouncementBuilder.MenuOpen`, which about half
  the handlers do not use, so those screens went silent instead. Final design:
  detect on raw text at read time via `ButtonHintCache.Filter`, append in
  `ScreenReader.Say` so every handler is covered regardless of how it builds its
  string, one-shot so cursor moves do not repeat it. Confirmed working in game.

  Also corrected: the Field Guide reading "Unknown" is NOT a bug. `???` for an
  undiscovered Digimon is punctuation-only and so hits the placeholder branch -
  the feature working as designed. Amethyst challenged the claim and was right.
  The misleading log line ("was empty" for every rejection reason) was replaced
  with `TextUtilities.DescribeUnusable`, which reports the actual cause.
