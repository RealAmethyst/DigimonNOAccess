# Currently Working On: Care Menu Accessibility

## Status: COMPLETE

CarePanelHandler.cs has been created and fully integrated. All features working:
- Care menu command selection (Items, Sleep)
- Item tab switching (Consumption, Foodstuff, Evolution, Material, Key Items)
- Proper message detection using IsFindActive()
- Audio navigation pauses when care menu is open

## Overview

The Care Menu is accessed by pressing Square (X on Xbox) in the field. It allows players to take care of their Digimon partners through feeding, praising, scolding, putting to sleep, etc.

## Class Hierarchy

```
uCarePanel (main panel)
├── uCarePanelCommand (command selection: Item, Sleep, etc.)
├── uCarePanelCaption (help text/instructions)
├── uCarePanelItem / uFieldItemPanel (item selection when using items)
├── uCarePanelHpMp (HP/MP display)
├── uCarePanelCondition (condition sliders)
└── uCarePanelKizuna (bond level display)

uEducationPanel (extends uCarePanel)
├── uEducationPanelCommand (Praise/Scold selection)
├── uEducationPanelCaption
└── (other education-specific panels)
```

## Key Classes

### uCarePanel (Main Care Panel)

**File:** `decompiled/Il2Cpp/uCarePanel.cs`

**State Enum:**
- `None` = 0 - Panel closed
- `Main` = 1 - Command selection active
- `Item` = 2 - Item list open
- `ItemUse` = 3 - Item being used
- `ItemWait` = 4 - Waiting for item action
- `ItemUseWait` = 5 - Waiting for item use
- `ItemMessage` = 6 - Item message displaying
- `PutToSleepWait` = 7 - Waiting for sleep action
- `PutToSleep` = 8 - Sleep action
- `Education` = 9 - Education/discipline mode (praise/scold)
- `Message` = 10 - Message displaying
- `Wait` = 11 - General wait state
- `Close` = 12 - Panel closing
- `Cooking` = 13 - Cooking mode
- `EducationItemUse` = 14 - Using item during education
- `ItemUseWait2` = 15 - Second item use wait
- `Max` = 16

**Result Enum:**
- `Normal` = 0
- `Toilet` = 1
- `Evolution` = 2
- `Sleep` = 3
- `Tent` = 4
- `AutoPilot` = 5

**Key Properties:**
- `m_state` (State) - Current panel state
- `m_commandPanel` (uCarePanelCommand) - Command selection panel
- `m_captionPanel` (uCarePanelCaption) - Caption/instructions panel
- `m_itemPanel` (uFieldItemPanel) - Item selection panel

**Detection:**
```csharp
var panel = Object.FindObjectOfType<uCarePanel>();
if (panel != null && panel.m_state != uCarePanel.State.None)
{
    // Panel is active
}
```

### uCarePanelCommand (Command Selection)

**File:** `decompiled/Il2Cpp/uCarePanelCommand.cs`

**Key Properties:**
- `m_selectNo` (int) - Current cursor position (0-based)
- `m_selectMax` (int) - Maximum number of commands
- `m_choiceText` (Text[]) - Array of Text components for each command
- `m_choice` (GameObject[]) - Array of choice GameObjects
- `m_command_name` (string[]) - Array of command name strings
- `m_cursor` (GameObject) - Visual cursor object
- `m_isMoveCursor` (bool) - True when cursor just moved

**Methods:**
- `ResetCursorPosition()` - Reset cursor to start
- `SetSelectCommand(int select, bool force)` - Set cursor position

**Reading Current Command:**
```csharp
var commandPanel = panel.m_commandPanel;
int cursor = commandPanel.m_selectNo;
int total = commandPanel.m_selectMax;

// Get command text from the Text array
string commandName = "";
if (commandPanel.m_choiceText != null && cursor < commandPanel.m_choiceText.Length)
{
    var textComponent = commandPanel.m_choiceText[cursor];
    if (textComponent != null)
    {
        commandName = textComponent.text;
    }
}
```

### uEducationPanelCommand (Praise/Scold Selection)

**File:** `decompiled/Il2Cpp/uEducationPanelCommand.cs`

Extends `uCarePanelCommand` for education/discipline mode.

**State Enum:**
- `None` = 0
- `Main` = 1 - Selection active
- `Wait` = 2

**Key Properties:**
- `state` (State) - Panel state
- `result` (EducationAction.ActionType) - Selected action result
- Inherits `m_selectNo`, `m_choiceText`, etc. from uCarePanelCommand

### EducationAction.ActionType

**File:** `decompiled/Il2Cpp/EducationAction.cs`

```csharp
public enum ActionType
{
    None,
    Scold,
    Praise
}
```

### uCarePanelCaption (Help Text)

**File:** `decompiled/Il2Cpp/uCarePanelCaption.cs`

**CaptionIndex Enum:**
- `None` = -1
- `Commoand` = 0 (typo in game code, means "Command")
- `NormalItem` = 1
- `MaterialItem` = 2
- `KeyItem` = 3
- `DigiviceStart` = 3
- `NormalItemDigivice` = 4
- `MaterialItemDigivice` = 5
- `KeyItemDigivice` = 6
- `EducationStart` = 20
- `EducationSelect` = 21
- `Fishing` = 30
- `Camp` = 40
- `DumpItemMessage` = 99

**Key Properties:**
- `m_text` (Text) - The caption text component
- `m_last_caption_index` (CaptionIndex) - Current caption mode

**Reading Caption:**
```csharp
var caption = panel.m_captionPanel;
if (caption != null && caption.m_text != null)
{
    string helpText = caption.m_text.text;
}
```

### uFieldItemPanel (Item Selection)

**File:** `decompiled/Il2Cpp/uFieldItemPanel.cs`

Extends `uCarePanelItem` which extends `uItemBase`.

**Type Enum (Item Categories):**
- `Care` = 0 - Care items
- `Camp` = 1 - Camp items
- `Digivice` = 2 - Digivice items
- `Event` = 3 - Event items
- `Battle` = 4 - Battle items
- `Oyatsu` = 5 - Snacks

**Key Properties (inherited from uItemBase):**
- `m_selectNo` (int) - Current cursor position
- `m_itemList` (List<ItemData>) - List of items
- `m_tab` (int) - Current tab index
- `m_type` (Type) - Current item type/category

**Methods:**
- `GetSelectItemParam()` - Returns `ParameterItemData` for current item
- `GetSelectItemData()` - Returns `ItemData` for current item

### uEducationPanel

**File:** `decompiled/Il2Cpp/uEducationPanel.cs`

Extends `uCarePanel` for education/discipline mode. Used when Digimon needs to be praised or scolded (e.g., after refusing to move, after training).

## Flow States

### Normal Care Menu Flow

1. Player presses Square - opens Care panel
2. `uCarePanel.m_state` = `Main`
3. `uCarePanelCommand` is active for command selection
4. Commands typically include: Item, Sleep (and contextual options)
5. If "Item" selected: `m_state` changes to `Item`, `uFieldItemPanel` becomes active
6. After item use: `m_state` cycles through ItemUse, ItemWait, etc.

### Education/Discipline Flow

1. Digimon does something requiring discipline (refuses to move, etc.)
2. `uEducationPanel` activates (extends uCarePanel)
3. `m_state` = `Education`
4. `uEducationPanelCommand` shows Praise/Scold options
5. Player selects action, `result` property contains ActionType

## Accessibility Implementation Plan

### Handler: CarePanelHandler

**What to track:**
1. Panel state changes (m_state)
2. Command cursor changes (m_commandPanel.m_selectNo)
3. Education mode (when uEducationPanelCommand is active)

**What to announce:**

**On Open (m_state becomes Main):**
- "Care menu. [Command name], [position] of [total]"

**On Command Cursor Change:**
- "[Command name], [position] of [total]"

**On Education Mode Start:**
- "Discipline. [Option], [position] of [total]"

**On State Changes:**
- Item state: Handled by existing FieldItemPanelHandler
- Sleep: "Putting partner to sleep"
- Message states: Handled by existing MessageWindowHandler

### Handler: EducationPanelHandler (or combined with CarePanelHandler)

**Detection:**
```csharp
var eduPanel = Object.FindObjectOfType<uEducationPanel>();
if (eduPanel != null && eduPanel.m_state == uCarePanel.State.Education)
{
    var eduCommand = Object.FindObjectOfType<uEducationPanelCommand>();
    if (eduCommand != null && eduCommand.state == uEducationPanelCommand.State.Main)
    {
        // Education selection is active
    }
}
```

## Existing Handlers That May Overlap

1. **FieldItemPanelHandler** - Already handles uFieldItemPanel
   - May need to check if care panel is parent context
   - The item selection WITHIN care mode should work via existing handler

2. **MessageWindowHandler** - Handles message displays
   - Care messages should work automatically

3. **CommonYesNoHandler** - Handles yes/no dialogs
   - Sleep confirmation, etc.

## Implementation Priority

1. **CarePanelHandler** - Main care command menu
   - Detect when care panel opens (m_state != None)
   - Track command selection via m_commandPanel
   - Announce command names from m_choiceText array

2. **Education mode support** - Within CarePanelHandler or separate
   - Detect uEducationPanelCommand
   - Announce Praise/Scold options

3. **Integration check** - Verify existing handlers work within care context
   - Item selection (FieldItemPanelHandler)
   - Messages (MessageWindowHandler)
   - Confirmations (CommonYesNoHandler)

## Code Pattern Reference

Based on existing handlers (FieldItemPanelHandler):

```csharp
public class CarePanelHandler
{
    private uCarePanel _panel;
    private bool _wasActive = false;
    private int _lastCursor = -1;
    private uCarePanel.State _lastState = uCarePanel.State.None;

    public bool IsOpen()
    {
        if (_panel == null)
        {
            _panel = Object.FindObjectOfType<uCarePanel>();
        }
        return _panel != null && _panel.m_state != uCarePanel.State.None;
    }

    public void Update()
    {
        bool isActive = IsOpen();

        if (isActive && !_wasActive)
        {
            OnOpen();
        }
        else if (!isActive && _wasActive)
        {
            OnClose();
        }
        else if (isActive)
        {
            CheckStateChange();
            CheckCursorChange();
        }

        _wasActive = isActive;
    }

    private void CheckCursorChange()
    {
        // Only track when in Main state (command selection)
        if (_panel.m_state != uCarePanel.State.Main)
            return;

        var commandPanel = _panel.m_commandPanel;
        if (commandPanel == null)
            return;

        int cursor = commandPanel.m_selectNo;
        if (cursor != _lastCursor)
        {
            string name = GetCommandName(cursor);
            int total = commandPanel.m_selectMax;
            ScreenReader.Say($"{name}, {cursor + 1} of {total}");
            _lastCursor = cursor;
        }
    }

    private string GetCommandName(int index)
    {
        var commandPanel = _panel.m_commandPanel;
        if (commandPanel?.m_choiceText != null &&
            index < commandPanel.m_choiceText.Length)
        {
            var text = commandPanel.m_choiceText[index];
            if (text != null)
                return text.text;
        }
        return "Unknown";
    }
}
```

## Testing Checklist

- [x] Care menu opens announcement ("Care menu. [Command], X of Y")
- [x] Command navigation (up/down) announces command names
- [x] Item selection transitions to FieldItemPanelHandler correctly
- [x] Item tab switching announces tab names (Consumption, Foodstuff, etc.)
- [x] Sleep option announces "not sleepy" message when appropriate
- [x] Messages display correctly (via MessageWindowHandler with IsFindActive)
- [x] Menu closes properly (no lingering announcements)
- [x] F2 status repeat works for care menu
- [x] Audio navigation pauses when care menu is open

## Notes

- The uCarePanel uses uFieldItemPanel for item selection, which should already be accessible via FieldItemPanelHandler
- Education mode appears when Digimon refuses commands or after certain events
- The m_choiceText array contains the actual displayed Text components
- State machine is key - different states show different UI elements
