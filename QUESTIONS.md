# Open questions

Date: 2026-07-31

These are the decisions I need from you before the matching work can start. They
are ordered roughly by how much they unblock. Answer inline underneath each
question — just type under it, no format needed.

Anything marked **verified** I read myself in the game's code or ours. Anything
marked **inference** is a reasonable guess I could not prove from the code we
have, and would need a real game launch to confirm.

## 1. Partner condition — the big one

**Verified:** the game stores everything about your Digimon's state —
`m_satiety` (hunger), `m_fatigue`, `m_mood`, `m_bonds`, `m_lifetime`,
`m_toiletTime`, `m_isReqToilet`, `m_isReqSleep` — with maximum and minimum
constants next to them, so we can speak a level or a percentage rather than a
raw number.

**Verified:** the mod never tells you any of it. Those fields are read only by
the code that *disables* hunger and fatigue, and by the restaurant screen. The
Care panel announces which menu item you're on and which partner is selected,
and nothing about their condition.

So: you can switch hunger off, but you can't find out whether your Digimon is
hungry. In a raising game I think that's the biggest hole in the mod.

**How would you like to hear it?** Some options, and they combine:

- A status hotkey that reads the current partner's full condition on demand.
- Automatic announcements when something crosses a threshold — "Agumon is
  getting hungry", "Agumon is exhausted".
- Spoken as part of opening the Care panel, since that's where a sighted player
  looks anyway.
- Both partners at once, or one at a time with a switch key?

**And in what words?** The game shows these as gauges, not numbers. I can give
you a percentage ("hunger 40 percent"), a coarse level ("getting hungry"), or
the raw number out of its maximum. Coarse levels read fastest but I'd be
inventing the wording, since the game has no text for these.

Your answer:we already have hots for reading out partner info, just put it there and have it speak as %


## 2. The floating sign icons — do you want them, and what do we call them?

**Verified:** the game pops icons over characters' heads. There are seven kinds:
`Partner`, `BattleIn`, `BattleOut`, `Vigilance`, `OkNg`, `Name`, `Event`. We can
always tell exactly which one fired and on which character.

**Verified:** none of them has any text. They are pure art. So if we speak them,
the words are ours, not the game's.

The interesting one is **`Vigilance`** — that's an enemy noticing you. Right now
you have no way to know an enemy has spotted you until the fight starts.

- Do you want these announced at all, or as a non-speech sound cue?
- `Vigilance` in particular — speech, a sound, or nothing?
- If speech, are you happy with me choosing the wording, or do you want to pick
  it? I'd suggest something like "Agumon noticed you" for Vigilance.

Your answer:no, enemies spots you and the music change, so that actually works for us right now.


## 3. Battle buffs and stat changes

**Verified:** the game exposes three ready-made checks —
`IsParameterUp`, `IsHighTensionUp`, `IsItemParameterUp` — over the four stats
(Forcefulness, Robustness, Cleverness, Rapidity), and it stores both how big
each buff is and how much time it has left. It also records where the buff came
from: a normal attack, high tension, or an item.

**Verified:** the mod uses none of this. Active buffs are completely invisible.

- On demand via a hotkey, announced automatically when one lands, or both?
- Do you want the remaining duration spoken, or just that the buff is active?
- The four stat names have no localized string I could find on the buff path, so
  I'd be picking words. The mod already says "Strength", "Stamina", "Wisdom",
  "Speed" for these in the training menu — should I reuse those, or use the
  game's own Forcefulness/Robustness/Cleverness/Rapidity?

Your answer:you can use those words, and yes I think having it on the partner hot in battle and automatic when something changes is a good idea, also look at the accessibility menu file for all of these to make it settings, at least this one should have it I think.


## 4. "What should I do next" — a negative result you should know about

**Verified:** this game has **no quest log and no objective text**. Scenario
progress is stored as bit flags, counters and a chapter number
(`Chapter01`..`Chapter04`, `ChapterEx`) with no descriptions attached. Even the
chapter has no readable name, just an internal code.

That means I cannot build you an objective reader out of the game's own words —
those words do not exist. This is why the mod finds quest targets by scanning
event triggers in the world instead.

- Is that acceptable as-is, or do you want me to invent something — for example
  a hand-written list of chapter objectives that we maintain ourselves?
- A hand-written list would be accurate but is a maintenance burden and only
  covers the main story, in English unless we translate it.

Your answer:that is fine, we don't need it here.


## 5. Speech engine, voice and rate — now that we're on Prism

The switch to Prism means we could expose things Tolk never could: pick the
speech engine (NVDA, JAWS, SAPI, Narrator...), pick a voice, and set rate and
volume — for engines that support it. Screen readers ignore rate and volume and
use your own settings, but SAPI voices honour them.

The Time Stranger mod has exactly this in its accessibility menu.

- Do you want an engine picker in the accessibility menu?
- Voice, rate and volume sliders for SAPI-style engines?
- Or is "use whatever screen reader is running" the right behaviour and we keep
  the menu simpler?

Your answer:yes please, put it in the text to speech category.


## 6. Speak only when the game window is focused?

Time Stranger has a setting that drops speech when you alt-tab away, and stops
anything mid-sentence when you leave. This mod currently speaks regardless.

- Do you want that setting here?
- On or off by default?

Your answer:yes please. and On by default


## 7. Pathfinding is supposed to mute the other navigation sounds — it doesn't

**Verified, and this may be part of why the beep felt wrong to you.**

There's a switch in the code called `AudioNavigationHandler.Suspended`, with a
comment saying it exists to "silence other navigation sounds during
pathfinding". `NavigationListHandler` sets it to true when you start pathfinding
and false when you stop.

Nothing ever reads it. Across the whole mod there are exactly four mentions: the
declaration and the three places that set it. So it does nothing at all.

That means while you're pathfinding, every item, NPC, enemy and transition sound
in range keeps playing on top of the beep. The tracker beep is competing with
all of them instead of having the field to itself.

I have not changed this, because whether to mute the world is your call, not a
bug I should silently decide. It is a small fix either way.

- Mute all other navigation sounds while pathfinding, as the code intended?
- Duck them (quieter but still audible) rather than mute?
- Or leave it as it is — you want to keep hearing the world while walking a
  route?

Your answer:remove that switch, it was never ment to be used, and the path finder beep was wrong anyways.


## 8. A volume control for the pathfinder beep

The beep now runs through the same mixer as the navigation sounds, so it can
have its own volume slider in the Audio settings like the other sound types do.
It doesn't have one today — it's fixed, ramping from quiet when far to louder as
you arrive.

- Do you want a slider for it?
- Should it also get an on/off toggle, or is stopping pathfinding enough?

Your answer:no on off tuggle, but volume is fine, it doesn't get louder and louder, but it does get faster and faster, so yes a volume slider is nice.


## 9. Hardcoded English in our own code

**Verified:** the mod speaks hardcoded English in about 65 places where the game
itself shows a localized label. The clear ones:

- The options menu section names — "System Menu", "System Settings", "Graphics
  Settings", "Key Config" and 32 more.
- The training menu — "Strength Training", "Stamina Training", "Wisdom
  Training", "Speed Training", plus the bonus names "Friend", "Rival", "Growth".
- The Field Guide tab names — "Before", "After", "Skill", "Info".
- The agreement screens — "End User License Agreement", "Privacy Policy".

For an English player nothing is broken. For anyone playing in German, French,
Japanese and so on, the mod suddenly speaks English at them in those places.

Another ~64 cases I'd leave alone: keyboard key names, and our own narration of
a state like "Please wait" or "Choose action". Those are our words, not the
game's labels, and that's allowed.

- Is fixing the localization ones worth doing? It's real work — each one needs
  the right game text source found and verified.
- Do you play in English only, or does this matter to you personally as well as
  to other users?

Your answer:fixing those localization stuff is important, as there are players that have reported this as annoying.


## 10. Leftover template files

The project was started from an "Accessibility Mod Template" and some of it was
never filled in or cleaned up. These are stale and now contradict the rewritten
`CLAUDE.md`:

- `CLAUDE.de.md` — a German copy of the old template, still containing
  unfilled placeholders like "[FILL IN AT SETUP]".
- `docs-de/` — German copies of the same template docs.
- `docs/setup-guide.md` — the "interview the user to set up a new project"
  script. This project is long past setup.
- `templates/` — generic code templates including a Tolk-based
  `ScreenReader.cs.template`, which now teaches the wrong thing.

- Delete them? Keep them? I'd suggest deleting all four, since git keeps the
  history if you ever want them back.

Your answer:just delete them.


## 11. Ghidra — do you want behavioural certainty?

**Verified:** the `decompiled/` folder is not actually a decompilation. It gives
us every class, field, method signature and enum in the game, which is enough to
know *what to hook and what data is there* — but every method body is an empty
stub. It cannot tell us what any function *does*.

So questions like "does this fire when the cursor moves, or when the screen
loads" can't be answered from it. Right now those get answered by trying it in
game, which costs you a test round each time.

Ghidra on `GameAssembly.dll` would answer them properly, but the setup here is
broken — `ghidra_project/run_ghidra.bat` points at folders that no longer exist,
and there's no analysed database.

- Worth me setting that up? It's a one-off cost and it would make every future
  finding provable instead of "probably".
- It's also the kind of self-contained job Codex is good at, so I could hand it
  over rather than doing it myself.

Your answer: yes definitely, this would mean we can ghidra and find things properly, definitely do this, than update claude.md when that is all done so we continue using it as well.
