# Changelog (since v1.0)

## New Features

- New speech engine. The mod now talks to your screen reader through Prism instead of Tolk. It supports more screen readers (NVDA, JAWS, System Access, ZDSR, Narrator and others), works with SAPI voices if you have no screen reader running, and it is what will let the mod work on Linux and Mac later. You no longer need Tolk.dll or nvdaControllerClient64.dll in your game folder; prism.dll ships with the mod and goes in the Mods folder with everything else.
- Fishing minigame support: lure selection with name reading, bite detection with 1-frame delay to filter misses, catch result announcements
- Movie subtitle speech: screen reader reads subtitles during movie cutscenes (with toggle in Speech settings)

- Partner condition on the partner keys. F3 and F4 (or your controller bindings) now also tell you how your Digimon is actually doing: tiredness, mood, discipline and curse as percentages of their bars, plus "hungry", "needs toilet" and "needs sleep" when those are showing. Weight and age stay exact numbers, as the game shows them.
- Battle stat boosts are announced. When a partner's Strength, Stamina, Wisdom or Speed goes up or wears off, you hear it, and the partner keys in battle now include any boosts currently running. There is a toggle for this in Gameplay settings if you would rather not hear it.
- Text to speech settings. In the Speech category you can now pick which speech engine the mod talks through, and on engines that support it (SAPI voices and similar) choose the voice, rate and volume. Screen readers ignore those and keep using your own settings, so those options only appear when they actually do something. There is also a "Speaking Through" line showing what is in use.
- Speak only when focused, on by default. The mod goes quiet while you are in another window, and stops talking mid-sentence when you switch away, instead of carrying on at you.
- Pathfinder beep volume slider in the Audio settings.

- The startup logos are announced. During the boot sequence you now hear "Bandai Namco", "Unity" and "CRIWARE" as each one appears, instead of an unexplained silent wait.
- Battle status conditions are announced. When a Digimon becomes poisoned, slowed, paralysed, confused, crystallised or angry you hear it, and you hear when it wears off.

## Bug Fixes

- The mod will no longer pick a speech engine that cannot actually be heard. Some engines report success but stay silent unless you have extra Windows components set up, and the automatic pick could land on one of those. It now skips them, and only falls back to one as a last resort with a warning telling you to choose another engine in Speech settings.
- Changing the speech engine now updates the Speech menu straight away, so voice, rate and volume appear or disappear to match what the new engine supports, and "Speaking Through" shows the engine you actually switched to.
- The voice list now starts on the voice your engine is really using, rather than always showing the first one in the list.
- Auto walk no longer keeps steering after you leave the field. If a battle, menu, event or facility started while auto walk was running, it kept pushing the stick in the background the whole time. It now stops when you leave the field and picks the route back up when you return.
- The pathfinding beep is now properly 3D. It goes through the same HRTF spatial audio as every other navigation sound, so you can hear whether it is in front of you, behind you, or above or below you, instead of only left and right.
- The pathfinding beep now follows the camera. Turning the camera moves the beep around you exactly like it moves item and NPC sounds. Before, it was pointed relative to the direction your character was facing, so it did not react to the camera at all.
- The pathfinding beep uses the proper tracker sound now. It was silently falling back to a plain generated tone because the sound file was missing.
- The beep points more steadily. It aims at a point ahead of you along the route, measured from where you actually are, so on a long straight stretch it no longer drifts behind you between route updates.

- Audio navigation no longer plays during field loading, area warps, or evolution sequences
- Digivice menu items now read from game UI text instead of hardcoded English (localization support)
- Field Guide header reads from game's own headline text instead of hardcoded "Library"
- Character select reads gender and character name from game UI, with proper header announcement
- Fix button icon mappings for PlayStation and Xbox controller layouts
- Colosseum: read match details via data API, fix premature and stale announcements
- Card Gallery: new handler with name/number/rarity and collection progress
- Evolution Dojo: read future evolution targets with nature/attribute/conditions
- Town Jump: fix item count and destination name
- Storage: fix side-switch announcements, section-aware headers
- Construction: fix dialog return, back-out, and duplicate announcement issues
