# Where we are, and what's next

Last updated: 2026-08-01, end of the Prism + localization session.

Everything below is committed, pushed and deployed to the game folder. You have
tested and confirmed all of it in game.

## How to pick this up

Three commands are all you need:

- **Build:** `"C:\Program Files\dotnet\dotnet.exe" build DigimonNOAccess.csproj`
- **Deploy:** `pwsh -File scripts\deploy.ps1` (add `-WhatIf` to preview). It copies
  only the seven shipped DLLs plus `sounds\`, and never touches your
  `settings.json` or `hotkeys.ini`.
- **Decompile something:** `ghidra_project\run_ghidra.bat decompile_by_name.py <name>`
  — takes a name substring, prints readable C in about 30 seconds. See
  `ghidra_project\README.md`.

The full map of what is still missing is `accessibility-audit.md`. It is written
so every claim says what kind of evidence stands behind it. `QUESTIONS.md` is
fully answered; the decisions are summarised in the audit.

## The one thing to remember about this codebase

`decompiled\` is **not** a decompilation. It is an Il2CppInterop proxy dump:
every class, field, method signature and enum is real, but every method body is
an empty thunk. It cannot tell you what a function *does*, when it fires, or
whether a string is display text or an asset name.

That trap bit us twice in one day: `m_satiety` looked like it paired with
`MAX_MEAL_SIZE` but actually clamps to 0..100, and `GetCorrectionName` sits
beside `GetBonusIconName` with an identical signature while only one returns
localized text. **Decompile before it becomes code.** Ghidra is set up for
exactly this and answers both kinds of question in half a minute.

## Next, in the order I'd do it

### 1. The seven confirmed battle defects — highest value

These are not gaps. They are things the mod claims to do and doesn't. Full
detail with hooks is in `accessibility-audit.md` under "Confirmed defects in
shipped code".

- **Damage numbers are never spoken.** `BattleDamagePopPatch` only plays a tick.
  Healing, poison, back attacks and OP gains are entirely silent. Hook
  `uDamagePop.Set(int, Transform, Type, UNITID, NatureRateInfo)`.
- **Order Power is summed across partners.** `GetOrderPower()` adds both values
  and returns one total, so it can tell you a command is affordable when neither
  partner can actually afford it. It is per partner in
  `uBattlePanelCommand.m_dispOrderPower[]`.
- **Enemy naming leaks internal names.** `GetEnemyName` falls through to the
  Unity GameObject name and then the literal "Enemy". Use
  `uEnemyName.m_text.text`, or fail closed.
- **The order ring throws away the `[Area]` / `[Shot]` prefix**, which is real
  tactical information a sighted player reads.
- **Learned skills are missed in results.** Postfix
  `uResultPanelSkill.SetSkill(string)`.
- **BattleMonitorHandler advertises checks it never runs** (target switching,
  ExE availability). Implement or delete the claims.
- **The damage tick is not a praise-timing cue** despite being documented as one.

### 2. Bucket D — 25 items needing Ghidra

Listed in the audit and in the raw report at
`H:\.claude\team\digimon-world-next-order\20260731_221235_108210_consult.md`.
Best two first:

- `MainGameField.m_fieldStatusEffectMessages` would settle "Injured" /
  "Seriously Injured" / "Sick" in `PartnerUtilities`.
- `uPartnerStatusPanelStatus.m_TitleTexts[]` would give proper localized labels
  for the care stats on **both** the partner panel and your F3/F4 readout — so
  "Discipline" becomes whatever the game actually calls it. Needs the array
  index mapped to each value field.

### 3. Smaller things worth a look

- **Button hints on hand-built announcements**: these now append inside
  `ScreenReader.Say`, so every screen should get them. If one is missing, say
  which.
- `NDH6SA~M` — a stray zero-byte file in the repo root. Yours; delete if junk.

## Things that can never be fixed, and why

Recorded so nobody re-opens them:

- **Day, season and time names.** The Digivice renders these as localized
  *sprite* assets (`m_SeasonIcon`, `m_WeekIcon`, `m_DayIcon`, `m_TimeZoneIcon`).
  There is no string anywhere. Our English is the only option.
- **Battle status condition names**, the startup logos, the sign icons, card
  rarity, range grades. Icon- or art-only. Our wording, approved.
- **No quest log exists.** Scenario progress is bit flags, counters and a chapter
  number with no descriptions. This is why navigation scans event triggers
  instead.
- **Bond, trust, lifespan and raw hunger are deliberately not spoken.** No
  display field exists for any of them, and the rule is parity with the sighted
  view.
- **The Field Guide saying "Unknown" is correct, not a bug.** Verified in game
  on 2026-08-01: entries you have met speak their real name, undiscovered ones
  render as `???` and cannot be selected at all. `???` is punctuation-only and
  therefore counts as placeholder text, which is why it comes out as "Unknown" —
  exactly the intended behaviour. It briefly looked like a defect because the
  log line claimed "was empty" for every rejection reason; that message now
  reports the real cause via `TextUtilities.DescribeUnusable`.
