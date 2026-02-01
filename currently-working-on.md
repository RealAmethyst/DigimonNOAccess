# Currently Working On - Field Gameplay Menus

## Overview

This document tracks field gameplay menus that need accessibility handlers. These menus are accessed during normal gameplay and are essential for blind players.

---

## High Priority Menus

### 1. uSavePanel - Save/Load Game Menu

**File:** `decompiled/Il2Cpp/uSavePanel.cs`

**Purpose:** Save and load game progress. Critical for gameplay.

**State Enum:**
- NONE
- MAIN_SETTING
- CLOSE
- SAVE_CHECK
- SAVE
- POST_WAIT
- SYSTEM_SAVE
- SAVE_END
- LOAD_CHECK
- LOAD
- END

**Key Properties:**
- Cursor-based slot selection
- Multiple state transitions for save/load flows
- Callback handling for save completion
- Input key checking with cursor index tracking

**Implementation Notes:**
- Need to announce current save slot
- Announce slot details (date, playtime, chapter)
- Announce confirmation dialogs
- Track state changes for feedback

---

### 2. uFieldItemPanel - Field Item Use Menu

**File:** `decompiled/Il2Cpp/uFieldItemPanel.cs`

**Purpose:** Use items during field exploration. Accessed from main field menu.

**Type Enum (Item Categories):**
- Care = 0
- Camp = 1
- Digivice = 2
- Event = 3
- Battle = 4
- Oyatsu = 5 (Snacks)
- Max = 6

**Result Enum:**
- ItemSelect
- ItemMenuToCommandMenu
- ItemMessage
- ItemUse

**Key Properties:**
- TypeData struct with position/scroll tracking for each category
- Multiple item category tabs
- Context-dependent item lists
- Item usage result announcements

**Implementation Notes:**
- Announce current category tab
- Announce item name and description
- Track scroll position per category
- Announce item use results

---

### 3. uStoragePanel - Storage/Inventory Management

**File:** `decompiled/Il2Cpp/uStoragePanel.cs`

**Purpose:** Transfer items between inventory and storage boxes.

**Key Properties:**
- m_itemPanelL (left panel - source)
- m_itemPanelR (right panel - destination)
- m_captionPanel (header/title)
- m_infoPanel (item details)
- m_openType (storage type state)

**Key Methods:**
- ChangeItemList - Switch between lists
- ArrowAction - Navigate between panels
- Window switching with validation

**Implementation Notes:**
- Announce which panel is active (left/right)
- Announce current item in each panel
- Announce transfer actions
- Track storage type context

---

### 4. uDigiviceMapPanel - Map Menu

**File:** `decompiled/Il2Cpp/uDigiviceMapPanel.cs`

**Purpose:** View world map, area maps, and navigate between locations.

**State Enum:**
- NONE
- WORLD
- AREA
- MINI_AREA
- CLOSE
- MAX

**RapidChangeKind Enum:**
- NORMAL
- EXTRA_DUNGEON

**Key Properties:**
- MapPanelBitFlag with state change tracking
- Multi-level map navigation
- Dialog state management

**Implementation Notes:**
- Announce current map level (World/Area/Mini)
- Announce selected location name
- Announce available destinations
- Track map type (Normal vs Extra Dungeon)

---

## Medium Priority Menus

### 5. uPartnerPanel - Partner Status Menu

**File:** `decompiled/Il2Cpp/uPartnerPanel.cs`

**Purpose:** View detailed Digimon partner information.

**State Enum (Tabs):**
- None
- Top
- Status
- Attack
- Tactics
- History

**Key Properties:**
- Multi-tab navigation
- InitializeTask coroutine for async loading
- State-based UI management

**Implementation Notes:**
- Announce current tab
- Announce tab-specific content:
  - Status: HP, MP, stats, hunger, fatigue
  - Attack: Known moves/skills
  - Tactics: Battle AI settings
  - History: Evolution history, achievements

---

### 6. uItemPickPanel - Item Pickup Menu

**File:** `decompiled/Il2Cpp/uItemPickPanel.cs`

**Purpose:** Pick up items and materials found in the field.

**State Enum:**
- None
- CommandMain
- ItemPick
- MaterialPick
- Result
- Close
- Wait
- Max

**Result Enum:**
- None
- End

**Key Properties:**
- Material vs item type distinction
- Callback-based state changes
- Count/quantity management

**Implementation Notes:**
- Announce item/material being picked up
- Announce quantity available
- Announce pickup confirmation
- Distinguish between items and materials

---

### 7. uMailPanel - Digital Messenger/Mail Menu

**File:** `decompiled/Il2Cpp/uMailPanel.cs`

**Purpose:** Read messages and quest information.

**State Enum:**
- SELECT
- NEXT
- RETURN

**Key Properties:**
- MailPanelBitFlag with 9 different state flags
- m_ListMailData (List of mail items)
- m_SelectTab (current tab index)
- m_SelectTabInfoTbl (tab information array)

**Key Methods:**
- UpdateMailList
- SetSelectTab

**Implementation Notes:**
- Announce current mail folder/tab
- Announce mail sender and subject
- Announce mail content when selected
- Track read/unread status

---

## Lower Priority Menus

### 8. uFieldPanel - Field HUD Status Display

**File:** `decompiled/Il2Cpp/uFieldPanel.cs`

**Purpose:** Display partner status conditions during exploration.

**SignIconIndex Enum (17 status types):**
- BowelMovement
- Hunger
- Sleepiness
- Fatigue
- Injury
- Disease
- SeriousInjury
- Confusion
- LovingKindness
- Joy
- Sorrow
- Anger
- Observation
- Suspicion
- Surprise
- BattleIn
- BattleOut

**Implementation Notes:**
- Announce status changes when they occur
- Periodic status summary on key press
- Priority announcement for critical states (Injury, Disease)

---

## Common Implementation Patterns

All these menus follow consistent patterns that match existing handlers:

**State Tracking:**
- State enums for menu phases
- m_state property to check current state
- State.None or State.NONE for inactive

**Cursor/Navigation:**
- KeyCursorController or m_selectNo for position
- m_DataIndex / m_DataMax for bounds
- IsMove() to detect cursor changes

**Item Data:**
- m_itemList or similar for item arrays
- GetSelectItemParam() for current item details
- ParameterItemData.GetName() for localized names

**Panel Lifecycle:**
- FindObjectOfType<T>() to get instance
- gameObject.activeInHierarchy for visibility
- enablePanel() / disablePanel() methods

---

## Implementation Status

- [x] uSavePanel - Complete (SavePanelHandler.cs)
- [x] uFieldItemPanel - Complete (FieldItemPanelHandler.cs)
- [x] uStoragePanel - Complete (StoragePanelHandler.cs)
- [x] uDigiviceMapPanel - Complete (MapPanelHandler.cs)
- [x] uPartnerPanel - Complete (PartnerPanelHandler.cs)
- [x] uItemPickPanel - Complete (ItemPickPanelHandler.cs)
- [x] uMailPanel - Complete (MailPanelHandler.cs)
- [x] uDigiviceTopPanel - Complete (DigiviceTopPanelHandler.cs)
- [x] Zone Selection - Complete (ZonePanelHandler.cs)
- [x] uFieldPanel - Complete (FieldHudHandler.cs) - Controller combo system for partner status

---

## Next Steps

1. Read decompiled source for first target menu
2. Identify exact properties and methods needed
3. Create handler following existing patterns
4. Test with screen reader
5. Document in game-api.md when complete
