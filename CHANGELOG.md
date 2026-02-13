# Changelog - Since v0.3.1

## Battle System

- Real-time battle narration: HP changes, attacks, and KOs announced as they happen (BattleMonitorHandler)
- Positional audio cues during battle (BattleAudioCues)
- Damage popup values intercepted and announced (BattleDamagePopPatch)
- Reorganized battle hotkeys: enemy info on F6/F7/F11, order power on F12, last SP charge warning on RT+DPadLeft
- Partner full status on F3/F4

## Navigation

- Fix ghost transitions from other maps appearing in nav list (uses game's AreaChangeParent hierarchy)
- Defeated enemies now removed from nav list immediately after battle
- Pathfinding to a defeated enemy stops right away instead of walking to the empty spot
- All nav categories re-sorted by distance on every refresh (closest first)
- Region name announced on zone change (e.g. "Nigh Plains" before "Vast Plateau"), only when region changes
- Quest item resolution from scenario CSVB data with completion detection via save flags
- Pathfinding to key items stops when entering trigger zone instead of relying on stuck detection
- Materials split into separate navigation category
- NPC names resolved properly for town Digimon (e.g. Palmon instead of C007)
- Recruited/deactivated NPCs removed from nav list
- Late-loading items picked up during RefreshLists
- Async load gating prevents missing objects after map changes

## Menus and Panels

- Library handler: full Digivice Library accessibility with grid browsing, detail tabs, evolution tree navigation, and skill detection
- Shop handler: buy/sell browsing, quantity selection, price and bits announcements
- Education panel: praise/scold choices announced, emotion message on open, cursor tracking
- Mail handler: complete rewrite with title/sender/read status, body reading, folder position, sort type detection, attachment claims
- Evolution history: known targets now show name, nature, attribute, stat requirements (met/not met), and evolution probability
- Title screen: Agreement option name fixed, agree window with Yes/No cursor tracking
- Training info hotkeys for per-partner stats and bonus announcements

## Partner Info

- Fixed STR/STA labels (were incorrectly ATK/DEF)
- Added growth stage, age, weight, nature, attribute, active time, personality
- Added care meters: happiness, discipline, curse, tiredness

## Speech and Messages

- Button icons resolved to readable names based on input device (keyboard/PS/Xbox)
- TalkMain message patches for NPC talk script popups (item rewards, notifications)
- SetLangMessage patch for localization-key field notifications
- CommonMessageMonitor removed (caused duplicate/stale text)
- Education completion messages properly queued so both partners are heard
- Digivolution info after education now uses Say instead of being incorrectly queued
- Item messages reformatted ("ItemName x 3" becomes "3 ItemName")
- Rich text and icon tags cleaned for screen reader output
- Partner sleep recovery messages properly queued with Say/SayQueued
- Fixed stale notification re-announcements after battles/events
- Fixed NPC dialog repeat detection (reset on dialog close)
- Save system messages no longer leak into gameplay announcements
- Zone announcement deferred until player is in field control (no longer interrupts storage transfers)

## Input

- L3/R3 (stick press) buttons added for mod shortcuts
- TriggerInput merged into ModInputManager
- Controller disconnect announced via screen reader
- Bits hotkey (F10/RT+DPadDown) for shop and trade menus

## Developer Tools

- PowerShell scripts for searching GameAssembly and item IDs
