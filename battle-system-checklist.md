# Battle System Accessibility Checklist

## Overview

Making battles accessible for blind players. The battle system uses real-time combat where you can issue orders to your two partner Digimon via the "Order Ring" menu.

---

## Key Classes Identified

### uBattlePanel (Main Battle UI)
- `m_instance` - static singleton
- `m_enabled` - whether battle UI is active
- `m_digimon` - array of `uBattlePanelDigimon` (partner status panels)
- `m_itemBox` - `uBattlePanelItemBox` (item menu)
- `m_tactics` - `uBattlePanelTactics`
- `m_description` - `uBattleDescription` (button hints)

### uBattlePanelDigimon (Partner Status - extends uDigimonPanelBase)
- `m_hpText` (Text) - HP display
- `m_mpText` (Text) - MP display
- `m_now_hp` (int) - Current HP value
- `m_now_mp` (int) - Current MP value
- `m_partner` (PartnerCtrl) - Partner reference
- `m_orderLabel` (Text) - Current order label

### uBattlePanelCommand (Order Ring)
- `m_selectIndex` (int) - Current command cursor position
- `m_selectDigimon` (int) - Which partner (0 or 1)
- `m_selectMode` (SelectMode enum) - None=-1, Default=0, LRCommand=1
- `m_command_tbl` (PartnerCommand[]) - Available commands array
- `m_cursor` (GameObject) - Visual cursor

### uBattlePanelItemBox (Battle Items - extends uItemBase)
- `m_selectNo` (int) - Item cursor position (from uItemBase)
- `m_isSelectTarget` (bool) - Whether selecting target for item
- `m_target` (ORDER_UNIT) - Target selection
- `m_MainHeadLineText` (Text) - Headline
- `m_CaptionText` (Text) - Caption
- `GetSelectItemParam()` - Get current item data

### PartnerCommand Enum (Battle Orders)
- CrossfireAttack, ScatteredAttack - Tactical attacks
- Free, FreeAll - Let Digimon fight freely
- Attack1-4 - Specific move slots
- SpAttack, SpAttackAll - Special attacks
- Guard, GuardAll - Defensive stance
- Approach, Leave - Movement commands
- Escape - Run from battle
- Cheer - Cheer partner (builds Order Power)
- Exe - ExE combination attack (when available)

### uBattlePanelTactics (Square Button Menu)
- `m_mode` (InternalMode enum) - None=-1, Default=0, Target=1
- `m_currentCmdNo` (int) - Current tab index (0-2)
- `m_commandFrames` (uBattleTacticsCommandBase[]) - Tab panels
- `m_titleLangText` (Text) - "Tactics Menu" title
- `m_captionLangText` (Text) - Button hints (not tab names)

### uBattlePanelResult (Victory Screen)
- `m_enabled` (bool) - Whether result panel is active
- `m_captionLangText` (Text) - Button hints
- `m_getPanel` (uResultPanelGet) - Item rewards panel
- `m_skillPanel` (uResultPanelSkill) - Skills learned panel

### uResultPanelGet (Item Rewards)
- `m_playerName` (Text) - Partner name
- `m_itemText` (Text[]) - Item names array
- `m_itemNumText` (Text[]) - Item quantities array
- `m_tpText` (Text) - TP gained
- `m_bitText` (Text) - Bit currency gained

---

## Implementation Tasks

### 1. BattleHudHandler - Partner HP/MP Status
**Priority:** HIGH
**Pattern:** D-pad only (no shoulder buttons needed in battle)

**Controls:**
- D-Up = Partner 1 HP and MP
- D-Down = Partner 2 HP and MP
- D-Left = Partner 1 current order
- D-Right = Partner 2 current order
- F3 = Partner 1 full status (keyboard)
- F4 = Partner 2 full status (keyboard)

**Known Issue:** D-pad input may be consumed by game during battle (camera/targeting). Keyboard fallback (F3/F4) should work.

**Detection:**
```csharp
var battlePanel = uBattlePanel.m_instance;
if (battlePanel != null && battlePanel.m_enabled)
{
    var panels = battlePanel.m_digimon;
    var partner0 = panels[0]; // Partner 1
    var partner1 = panels[1]; // Partner 2

    string hp = partner0.m_hpText?.text;
    string mp = partner0.m_mpText?.text;
}
```

**Status:** [x] Implemented - BattleHudHandler.cs

---

### 2. BattleOrderRingHandler - Command Selection
**Priority:** HIGH
**Description:** Announce selected commands in the Order Ring

**Detection:**
```csharp
var cmdPanel = FindObjectOfType<uBattlePanelCommand>();
if (cmdPanel != null && cmdPanel.gameObject.activeInHierarchy)
{
    int selectIndex = cmdPanel.m_selectIndex;
    int partnerIndex = cmdPanel.m_selectDigimon;
    var commands = cmdPanel.m_command_tbl;

    if (commands != null && selectIndex >= 0 && selectIndex < commands.Length)
    {
        var cmd = commands[selectIndex];
        string cmdName = GetCommandName(cmd);
    }
}
```

**Command Name Mapping:**
- CrossfireAttack → "Crossfire Attack"
- ScatteredAttack → "Scattered Attack"
- Free → "Fight Freely"
- Attack1-4 → Read actual move names from partner
- SpAttack → "Special Attack"
- Guard → "Guard"
- Escape → "Escape"
- Cheer → "Cheer"
- Exe → "ExE Attack"

**Status:** [x] Implemented - BattleOrderRingHandler.cs

---

### 3. BattleItemHandler - Item Menu
**Priority:** MEDIUM
**Description:** Announce items and target selection

**Detection:**
```csharp
var battlePanel = uBattlePanel.m_instance;
if (battlePanel?.m_itemBox != null)
{
    var itemBox = battlePanel.m_itemBox;
    if (itemBox.gameObject.activeInHierarchy)
    {
        int cursor = itemBox.m_selectNo;
        var itemParam = itemBox.GetSelectItemParam();
        string itemName = itemParam?.GetName() ?? "";

        if (itemBox.m_isSelectTarget)
        {
            var target = itemBox.m_target;
            // Announce target selection
        }
    }
}
```

**Status:** [x] Implemented - BattleItemHandler.cs

---

### 4. BattleDialogHandler - Battle Confirmations
**Priority:** MEDIUM
**Description:** Handle Yes/No dialogs in battle (escape confirmation, etc.)

**Detection:**
```csharp
var dialog = FindObjectOfType<uBattlePanelDialog>();
if (dialog != null && dialog.gameObject.activeInHierarchy)
{
    string message = dialog.m_messageText?.text ?? "";
    int cursor = dialog.m_cursorIndex; // 0=Yes, 1=No
}
```

**Status:** [x] Implemented - BattleDialogHandler.cs

---

### 5. BattleTacticsHandler - Square Button Menu
**Priority:** MEDIUM
**Description:** Announce tactics menu tabs (Escape, MP Usage, Target)

**Detection:**
```csharp
var tacticsPanel = battlePanel.m_tactics;
if (tacticsPanel != null && tacticsPanel.gameObject.activeInHierarchy)
{
    int cmdNo = tacticsPanel.m_currentCmdNo; // Tab index 0-2
    var mode = tacticsPanel.m_mode; // None, Default, Target
}
```

**Tabs:** Escape (0), MP Usage (1), Target (2)

**Status:** [x] Implemented - BattleTacticsHandler.cs
**Known Issue:** Caption text shows button hints, not tab names. Using hardcoded tab names based on index.

---

### 6. BattleResultHandler - Victory/Defeat Screen
**Priority:** MEDIUM
**Description:** Announce battle results and rewards

**Detection:**
```csharp
var resultPanel = FindObjectOfType<uBattlePanelResult>();
if (resultPanel != null && resultPanel.m_enabled)
{
    // uResultPanelGet has item rewards
    var getPanel = resultPanel.m_getPanel;
    // m_itemText[] - item names
    // m_tpText - TP gained
    // m_bitText - Bit currency gained
}
```

**Status:** [x] Implemented - BattleResultHandler.cs
**Known Issue:** Caption text shows " Continue" (button hint). Defaults to "Victory!" announcement.

---

### 7. Battle State Announcements
**Priority:** LOW
**Description:** Announce battle start, victory, defeat

**Detection Points:**
- Battle start: When `uBattlePanel.m_enabled` becomes true
- Battle end: When it becomes false
- Check `uBattlePanelResult` for win/lose state

**Status:** [ ] Not Started

---

## File Structure

```
DigimonNOAccess/
├── BattleHudHandler.cs       - Partner HP/MP via D-pad (D-pad may not work, use F3/F4)
├── BattleOrderRingHandler.cs - Order Ring command selection (WORKING)
├── BattleItemHandler.cs      - Battle item menu (WORKING)
├── BattleDialogHandler.cs    - Battle Yes/No dialogs (WORKING)
├── BattleTacticsHandler.cs   - Square button menu tabs
├── BattleResultHandler.cs    - Victory screen and rewards
└── BattleStateHandler.cs     - Battle start/end announcements (NOT STARTED)
```

---

## API Reference Updates Needed

After implementation, add to `docs/game-api.md`:
- Battle panel detection
- Order ring cursor tracking
- Battle item menu pattern
- PartnerCommand enum reference

---

## Testing Notes

1. Start a battle with wild Digimon
2. Test D-pad status checking during combat
3. Open Order Ring (hold face button?) and navigate
4. Open item menu and select items
5. Try to escape and confirm dialog works
6. Win/lose battle to test result announcements
