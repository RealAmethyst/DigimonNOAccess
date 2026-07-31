# DigimonNOAccess — what's still missing

Date: 2026-07-31

This is the map of everything in Digimon World: Next Order that a blind player
can't reach yet, plus the defects we found in code we already shipped, with the
exact hook for each one so we can just build from it.

Codex swept the decompiled code in three passes — menus, battle, and the
field/care loop. I checked every load-bearing claim against the real source
myself before writing any of it down, and where I verified something personally
I say so. The raw reports are in `H:\.claude\team\digimon-world-next-order\`.

Navigate by heading. The order is roughly the order I'd build in, not the order
the code is organised in.

## Read this first — what our sources can and cannot prove

This matters more here than it did on other projects, because it changes how much
weight each claim below can carry.

**`decompiled/` is not a decompilation.** It is an Il2CppInterop proxy-assembly
dump. Every method body in all 908 files is an `il2cpp_runtime_invoke` thunk — I
checked, and no file in the tree contains a single real `if` or `for` outside the
interop boilerplate. It proves, with certainty: class names, field names and
types, method signatures, enum members, constants. It cannot show what any method
does, when it fires, whether a string is localized, or what a flag means.

**Ghidra now fills that gap, and it is set up and working.** `GameAssembly.dll`
is imported and analysed at `C:\Users\Amethyst\ghidra_dwno` with all 84,160
IL2CPP method names applied. `ghidra_project\run_ghidra.bat decompile_by_name.py
<name>` gives readable C in about 30 seconds. See `ghidra_project\README.md`.

That distinction is not academic. Within minutes of Ghidra coming online it
caught a real bug in this very audit's first implementation: `m_satiety` looks
like it pairs with `MIN_MEAL_SIZE`/`MAX_MEAL_SIZE`, and the signature dump can
never tell you otherwise, but `SetSatiety` actually clamps to 0..100. **Treat any
claim below that is not marked verified as a strong inference from a name, and
decompile it before it becomes code.**

## The short version

- Three passes complete: menus, battle, field/care.
- **Seven confirmed defects in code we already shipped.** Those are listed first
  because they are worth more than any new feature: they are things the mod
  claims to do and doesn't.
- Seven uncovered UI families, of which two block normal play.
- The single biggest gap is battle: status conditions, damage numbers, buffs and
  order-ring timing are all invisible.
- Three negative results that save us work: there is no quest log, no universal
  cursor hook, and no localized text for any of the sign icons.

## Decisions already made (2026-07-31)

All of `QUESTIONS.md` is answered. Settled policy:

- **Parity is the rule.** "If they don't show, don't speak them." Anything the
  sighted view doesn't give, we don't give. This killed bond, trust, lifespan and
  raw hunger from the partner readout even though all four are trivially
  readable.
- **Partner condition** goes on the existing F3/F4 partner hotkey, as
  percentages. Done.
- **Sign icons: not building.** The music already changes when an enemy spots
  you, so `Vigilance` is covered in practice.
- **Battle buffs:** on the partner hotkey in battle, plus automatic on change,
  with a setting. Use Strength / Stamina / Wisdom / Speed as the wording.
- **No quest objectives.** Accepted as-is; the game has no text for them.
- **Speech engine, voice, rate, volume** exposed in the Text to Speech category.
  Done. **Speak only when focused**, on by default. Done.
- **Pathfinder beep:** volume slider, no on/off. Distance is carried by cadence,
  not loudness. Done.
- **Hardcoded English** where the game has a localized label: worth fixing,
  players have complained.

## Already fixed in this pass

- Pathfinding beep now runs through HRTF, oriented on the camera, using the real
  tracker sound. It was on a separate output with plain stereo panning, pointed
  at the player's facing rather than the camera, and silently falling back to a
  generated 800 Hz tone because the WAV was missing.
- Speech moved from Tolk to Prism.
- `AudioNavigationHandler.Suspended` deleted. It was write-only — set in three
  places, read nowhere — so pathfinding never actually muted the other nav
  sounds despite the comment saying it did.
- **Auto-walk no longer keeps injecting stick input after you leave the field.**
  `NavigationListHandler.Update()` returns early when not in the field, and the
  comment claimed auto-walk "pauses naturally since Update stops". It doesn't:
  `GamepadInputPatch` reads `AutoWalkActive` from the game's own input path,
  which keeps running. A stale stick value stayed jammed on through battles,
  menus and events. Verified end to end.

## Confirmed defects in shipped code

These are not gaps. They are things the mod already claims to do, and doesn't.
I consider these the highest-value work in the document.

### Damage numbers are not spoken at all

`BattleDamagePopPatch` never calls `ScreenReader` — it only plays a quiet or loud
tick. It also handles only `Damage`, `NormalDamage`, `Critical` and `Break`,
while `uDamagePop.Type` also has `BackDamage`, `Recovery`, `Poison`, `Timer`,
`Buff` and `GetOP`. So healing, poison ticks, back attacks and OP gains are
entirely silent, and damage itself is only a sound.

- Hook: postfix `uDamagePop.Set(int damage, Transform target, uDamagePop.Type type, MainGameManager.UNITID unitId, uDamagePop.NatureRateInfo natureRateInfo)`.
  Complete arguments, population boundary, not an update method.
- Text: the populated fields `m_criticalHitLangText`, `m_backAttackLangText`,
  `m_breakLangText`, `m_timeLangText`, `m_orderPowerLangText`, and
  `m_natureTextInfo[].m_natureText` / `m_multiplyText` for effectiveness.
- Trap: throttle or aggregate multi-hit damage.

### The damage tick is not the praise-timing cue it is documented as

The tick fires per qualifying damage popup, so multi-hit attacks stack it. But
praise is not limited to damage: `DigimonCtrl.PraiseCheckType` is `Hit`, `Avoid`,
`Guard`, and the timing windows live in `AppInfo.PraiseCheckInfo` (`highTime`,
`midTime`, `lowTime`, `isCountTime`, `actionTimer`), evaluated by
`PlayerCtrl.PraiseActionCheck(PartnerCtrl)`. The tone may still be useful as
combat rhythm, but "press now for max OP" is not supported by the model.

### BattleMonitorHandler advertises checks it never runs

Its class comment claims enemy target switching and ExE/SP availability
monitoring. Its update path calls only `MonitorEnemySPAttacks`,
`MonitorOPMilestones` and `MonitorCheerAvailability`. `GetEnemyTargetPartner` is
never called, `BattleAudioCues.PlayTargetSwitch()` has no call site, and no ExE
check exists despite `uBattlePanel.IsCanUseExe()` and `CheckExeSpEmission()`
being available. Either implement or delete the claims.

### Order Power is summed across partners

`BattleMonitorHandler.GetOrderPower()` adds both `uBattlePanelDigimon.m_dispOrderPower`
values and returns one total. The UI stores them per partner in
`uBattlePanelCommand.m_dispOrderPower[]`, and attack cost is per partner via
`ParameterAttackData.m_consumptionOP`. A combined figure can tell you a command
is affordable when neither partner can actually afford it.

Related: `BattleOrderRingHandler.GetAttackCommandInfo()` reads partner zero's
attack and cost when `m_selectDigimon` means both. Partners can have different
attacks and costs, so both must be read.

### Enemy naming bypasses the player-facing UI

`BattleMonitorHandler.GetEnemyName(...)` falls back through
`DigimonCommonData.m_name`, `ParameterDigimonData.GetDefaultName()`, the Unity
GameObject name, and finally the invented string `"Enemy"`. GameObject names are
internal asset names, and parameter names can reveal an identity the UI has
chosen not to show. The player-facing source is `uEnemyName.m_text.text`
(populated at `uEnemyName.StartSign(GameObject, bool)`), with
`uEnemyHpBar.m_levelText.text` for level. Fail closed or use the rendered UI.

### Order-ring text is deliberately thrown away

`BattleOrderRingHandler.StripAttackTypePrefix()` removes the bracketed prefix —
`[Area]`, `[Shot]`, `[Front]`. `ParameterAttackData.AttackTypeIndex` covers
front, fan, rush, shot, radiation, range, whole and target. That prefix is real
tactical information a sighted player reads. Keep it.

### Learned skills are missed in battle results

`uResultPanelSkill.SetSkill(string skillText)` receives already-prepared skill
text, and `uBattlePanelResult.CheckLearnSkill()` plus its skill queue exist.
`BattleResultHandler` never touches either. A postfix on `SetSkill(string)`
announcing its argument once closes this.

Also, that handler treats "any `m_isRise` is true" as the sole signal for the
first results page. Nothing proves at least one panel is always true, so a
no-stat-gain result may be missed. Needs a live test.

## Tier 1 — blocks normal play

### Battle status conditions

Poison, slow, paralysis, confusion, liquid crystal, anger variants, poison-slow.
These change how your Digimon behave autonomously and whether they survive.
Nothing tells you about any of them.

- Best hook: postfix `uBattlePanel.enableAbnormalSign(MainGameManager.UNITID unitId, ParameterAttackData.AbnormalIndex abnomaly, bool sw)`.
  The arguments give unit, condition and visible on/off directly. Dedupe by
  (unit, condition), announce only changes.
- Backup: postfix either `DigimonCtrl.SetAbnormal(...)` overload for
  application, `DigimonCtrl.AbnormalRecovery()` for all-clear.
- **No text source exists.** `AbnormalIndex` is an enum; the icon classes expose
  only child indices, sprites and timers. Wording would be ours — the same
  decision shape as the sign icons, which you declined. Worth asking again here,
  because unlike an enemy noticing you, there is no audio cue standing in.
- Do not hook `DigimonCtrl.UpdateAbnormal()` or any icon `Update` — they recur.
- Correction worth knowing: there is **no `Sleep` member** in the battle
  `AbnormalIndex`. Sleep belongs to the care loop, not battle.

### Language-selection window

`uAgreeLanguageSelectWindow` — first-run, and it gates progression. Every
selectable row is a sprite; there is no localized language name in the class.

- Open: `Open(SystemLanguage currentLang, Action<bool, SystemLanguage> callback)`.
- Cursor: the `Select` property setter, `set_Select(int)`.
- Header text: `Text m_header`. Choices: `SystemLanguage[] m_LanguageList`.
- Trap: the setter also runs during initialization; dedupe by active instance and
  previous index.
- Speaking the `SystemLanguage` enum names would be inventing words — but this is
  the one screen where that may be justified, since a player who cannot read it
  cannot start the game.

### Input-wait prompt

`uInputWaitComment` — the field prompt that gates area transitions. Small, clean,
and it has a real getter.

- Hook: postfix `MainGameManager.EnableInputWaitCommentUI(bool _isActive)` for
  the open/close edge; `uInputWaitComment.SetDispText(uint _langId)` for changes.
- Text: `GetDispText()` or `m_Text.text`. `m_LangId` is raw — resolve with
  `Language.GetString(uint)`.
- Trap: visibility can precede the text being assigned. Read after
  `SetDispText`, or a frame later.
- Effort: small. Value: blocks play when it gates a transition.

## Tier 2 — important, not blocking

### Buff and High Tension icons in battle

Decided: build this, with a setting, wording Strength / Stamina / Wisdom / Speed.

- Data is public and needs no reflection: `DigimonGameData.IsParameterUp(type)`,
  `IsHighTensionUp(type)`, `IsItemParameterUp(type)` over `ParameterUpType`
  (Forcefulness, Robustness, Cleverness, Rapidity). Magnitude and remaining time
  are in `m_parameter_up[]`, `m_parameter_up_time[]`, `m_tensionSkills[]`,
  `m_itemUpInfos[]` and the `ParameterUpInfo` struct. **Verified: none of the
  three predicates is used anywhere in the mod today.**
- Icon hook candidate: `uDigimonHpBarBase.SetActiveBuf(int no, DigimonGameData.ParameterUpType type)`
  — but it is virtual with caller count 0, so confirm it live before relying on
  it. The predicates above are the safer route for an on-demand read.
- High Tension has a real localized description:
  `ParameterHighTensionSkill.m_description_code` via `GetParam(uint)`, resolved
  with `Language.GetString(uint)`.
- Trap: the icon slot number is not necessarily a condition ID.

### Restaurant recipe and ingredient window

A genuine subwindow inside an already-covered family. `uRestaurantPanel` owns
`m_materialWindow`, and `RestaurantPanelHandler` never references it — so you
hear the dish but not what it needs.

- Hook: postfix `uRestaurantPanelRecipeWindow.UpdateInfo(ref ItemData item_data)`.
- Text: `GetTextTbl(TextNo)` with `Title`, `ItemName`, `ItemHave`, `ItemHaveNum`
  per material row; `ParameterItemData.GetName()` is the finished accessor.
- Traps: two slots (`Item01`, `Item02`) — do not speak padded or inactive ones.
  Icon loading goes through a coroutine, so a postfix there fires at iterator
  creation. Dedupe by recipe.

### Order-ring timing and command state

The order ring is the core of the battle loop and only partly covered. Per-partner
OP and countdown state exist and can be threshold-spoken or sonified without
per-frame speech. See the raw battle report for the full field list.

### Timed modes

`uTimerPanel` with `TimerMode.Colosseum` and `ExDungeon`. `Main.AnnounceTimeInfo()`
reads `GetTimer()` only for `ExDungeon` and never announces opening or
thresholds, so the Colosseum timer is invisible.

- Hook: `uTimerPanel.OpenPanel(TimerMode mode)`; sample `GetTimer()` for
  thresholds. `Update()` runs every frame — thresholds only.

### Escape and Ijigen Box combat

Escape becomes a separate `MainGameEscape` sequence with its own distance and
velocity UI. Ijigen Box has an entirely separate battle presentation state
machine. Neither is touched by the battle handlers.

## Tier 3 — completeness

- **Tamer level-up panel** (`uTarmerLevelUpPanel`, `MainGameTarmerLevelUp`). A
  dedicated progress state with a result of `SkillSet` or `End`, but the class
  declares no text, label or cursor field at all — everything is in the prefab.
  Needs runtime inspection before anything can be built. Effort: large.
- **Toilet sequence** (`uToiletPanel`). Only `Sprite[] m_textures`. Hook
  `MainGameManager.enableToiletUI(bool)` for open/close. No text anywhere;
  `MODE` names (`Toilet`, `Portable`, `Noguso`) are internal identifiers.
- **End roll / credits** (`uEndRollPanel`). Declares no fields at all. Could be
  child text or baked images — the source cannot say. Inspect before building.
- **Care mistakes / training failure** (`m_trainingFailure`). A hidden evolution
  input with no display field. Under the parity rule, do not expose it.

## Not worth building

- **A quest objective reader.** There is nothing to read.
  `CScenarioProgressData` is bit flags, counters, and a chapter number
  (`Chapter01`..`Chapter04`, `ChapterEx`) with no descriptions attached. Even the
  chapter has no localized name. Verified myself. This is why the mod finds quest
  targets by scanning `EventTriggerScript` objects instead — correctly.
- **The sign icons.** Declined: the music already changes when an enemy notices
  you.
- **Bond, trust, lifespan, raw hunger.** No display field anywhere in
  `uPartnerStatusPanelStatus`. Parity rule — verified myself against the panel's
  actual field list.

## How text works in this game

There is **no single lookup that resolves every string**. Establishing that was
worth the pass on its own.

- **`Language` is the game's own system and the main one.**
  `Language.GetString(uint lang_code)`, `GetString(string)`,
  `GetString(string language, string lang_code)` return finished display strings —
  no second lookup. `GetStringWithButtonIcon(...)` additionally substitutes button
  glyphs. `Language.makeHash(string)` converts a textual key to the numeric form.
  All verified.
- **`UtilityScript.SetLangText(ref Text, string|uint)`** is the wrapper the game
  itself uses to write localized text into a UI field. `SetLangButtonText` and
  `SetLangIconText` are the button/icon variants. `SetText` writes a literal and
  does **no** localization. Verified.
- **NGUI `Localization` is a separate, secondary system** with its own dictionary:
  `Localization.Get(string key, bool warnIfMissing = true)`, `Localize`, `Format`.
  Its keys are **not** interchangeable with `Language` IDs.
- **Parameter tables with finished-string accessors** (verified):
  `ParameterItemData.GetName()` / `GetDescription()`,
  `ParameterAttackData.GetName()`, `ParameterAreaName.GetAreaName(MAP, uint)`,
  `ParameterMapName.GetMapName(MAP)`, `ParameterTownJumpData.GetName()`,
  `ParameterCommonSelectWindow.GetLanguageString()`.
- **Raw language IDs** needing `Language.GetString(uint)`:
  `ParameterAreaName.m_LanguageCode`, `ParameterMapName.m_LanguageCode`,
  `ParameterDigitalMessengerData.m_mailLanguage_code`,
  `ParameterPlacementNpc.m_Name` / `m_Message`,
  `ParameterCommonSelectWindow.m_langid1` / `m_langid2`.
- **Rendered text** — `Text.text`, `UILabel.text`, `TextMesh.text` — is already
  resolved, but can be empty or stale on the frame a panel opens.
- Never cache a localized string across a language change, and never speak a raw
  missing key.

### Our own hardcoded English

Roughly 65 places where the mod speaks English at a screen where the game shows
a localized label. Decided: worth fixing. The clear ones are
`OptionsMenuHandler` (36 — "System Menu", "Graphics Settings", …),
`TrainingPanelHandler` (21 — "Strength Training", "Friend", "Rival", "Growth"),
`LibraryHandler` (4 tab names), `AgreeWindowHandler` (3). The battle handlers add
more: `BattleOrderRingHandler.GetTacticalCommandName()`, `"ExE Attack"`,
`BattleDialogHandler`'s `"Yes"`/`"No"`/`"Confirm?"`, `BattleItemHandler`'s
`"Unknown Item"`/`"Both Partners"`, and `BattleResultHandler`'s full sentences.
`PartnerUtilities.GetStatusEffectText` ("Injured", "Sick") is the same problem.

Leave alone: keyboard key names in `ButtonIconResolver`, and our own narration of
a state ("Please wait", "Choose action"). Those are our words, not the game's.

## No universal cursor hook — verified

Worth stating plainly so nobody goes looking again.

`KeyCursorController` has exactly the right shape (`m_DataIndex`, `m_DataMax`,
`IsMove()`, `GetDataIndex()`, `SetDataIndex()`) but **only 7 files in the whole
game reference it**, all in already-covered families. `SimpleCursor` has one
owner. `ListBase` has one concrete subclass. `uPanelBase` is open/close plumbing
with no cursor at all.

The best shared hook is `uItemBase.MoveCursor(int select_no, float t)` with
`m_selectNo` / `m_lastSelectNo` and an `OnMoveCursor(ItemData)` callback —
verified — and fourteen list classes derive from it. It could consolidate the
existing handlers, but it closes none of the gaps above, because every uncovered
family sits outside that hierarchy.

## Recurring traps

- **The interop dump cannot prove behaviour.** Decompile before you rely on
  "this fires on cursor move".
- **Recycled scroll cells.** Read the logical index, never the object position.
- **Delayed population.** Many `Open`/`SetData` methods only store data; text
  fills in a frame later. Prefer the data object over scraping the UI.
- **Coroutines.** A postfix on an iterator method runs when the iterator is
  *created*, not when it works. Affects restaurant icon loading and several
  detail pages.
- **Padded lists.** Never use row count as the item total.
- **Caller count 0 in the wrapper does not mean unused** — it can be reached
  natively. It does mean the anchor is unproven.
- **Never resolve by RVA.** The fixed addresses in `CareMechanicsPatch` are a
  standing liability; resolve IL2CPP methods by name at runtime instead.

## What's still open

- Status-condition wording: there is no localized text for any battle abnormal
  state. Under the parity rule they are visible (icons), so they should be
  spoken — but the words would be ours. Needs your call.
- Several hooks are marked "needs a running game": the tamer level-up panel's
  focus structure, the end roll's content, whether `SetActiveBuf` actually fires,
  and whether `uInputWaitComment` populates before or after it becomes visible.
  Ghidra can now answer most of these without a play session.
- Nothing in this document has been tested in a running game.
