# Pathfinding Navigation System - Research Document

## Feature Overview

An event/point-of-interest navigation system that scans the current area map and builds a categorized list of all reachable objects (NPCs, items, transitions, enemies). Players cycle through categories and individual events via keyboard/controller, then use audio-guided pathfinding to navigate to the selected target using walkable paths.

## Controls (Reference from pkreborn-access)

- **O / I**: Cycle forward/backward through event categories (NPCs, Items, Transitions, Enemies)
- **J / K / L**: Navigate to previous / announce current / navigate to next event in the active category list
- **P**: Start audio-guided navigation to the selected event (sound pans left/right based on walkable path direction)

Categories with no entries are automatically skipped when cycling.

## Architecture Design

### Core Class: `NavigationListHandler`

Maintains a categorized list of points of interest, refreshes on map changes, and provides audio-guided pathfinding to selected targets.

### Categories

1. **NPCs** - All active NpcCtrl instances in the area
2. **Items** - All active ItemPickPointBase instances (removed when picked up)
3. **Transitions** - All MapTriggerScript instances with `enterID == MapChange`
4. **Enemies** - All active EnemyCtrl instances in the area

---

## Game API Research Findings

### 1. Current Map/Area Detection

**Class:** `MainGameManager` (singleton via `m_instance`)

```csharp
// Get current map and area IDs
var mgm = MainGameManager.m_instance;
int currentMapNo = mgm.mapNo;    // Public getter
int currentAreaNo = mgm.areaNo;  // Public getter
```

**Map change detection:** Monitor `mapNo` changes between frames, or hook `MainGameManager.RequestMapChange(int mapNo, int areaNo, string spawnPoint)`.

### 2. NPC Names

**Class:** `NpcCtrl` (extends `UnitCtrlBase`)

Two approaches to get NPC names:

**Approach A - Direct string ID:**
```csharp
NpcCtrl npc = ...;
string npcId = npc.m_npcId;  // String identifier (e.g., "Agumon", "Jijimon")
```

**Approach B - Parameter chain (more reliable for localized names):**
```csharp
NpcCtrl npc = ...;
uint paramId = npc.unitParamId;  // NpcEnemyParam ID

// Look up NpcEnemyData to get Digimon param ID
var npcEnemyData = ParameterNpcEnemyData.GetParam(paramId);
uint digiParamId = npcEnemyData.m_DigiParamId;

// Look up Digimon name
var digimonData = ParameterDigimonData.GetParam(digiParamId);
string name = digimonData.GetDefaultName();  // Localized name
```

**Fallback:** If parameter chain fails, use `npc.m_npcId` string directly, or the GameObject name.

**Source files:**
- `decompiled/Il2Cpp/NpcCtrl.cs` - NPC controller with `m_npcId` and `unitParamId`
- `decompiled/Il2Cpp/ParameterPlacementNpc.cs` - NPC placement data with `m_NpcEnemyParamId`, `m_Name` (uint, localization hash), `m_MdlName` (model name)
- `decompiled/Il2Cpp/ParameterNpcEnemyData.cs` - NPC/Enemy data with `m_DigiParamId` -> Digimon species
- `decompiled/Il2Cpp/ParameterDigimonData.cs` - Digimon data with `GetDefaultName()` for localized name

### 3. Item Names

**Class:** `ItemPickPointBase` (extends `MonoBehaviour`)

```csharp
ItemPickPointBase item = ...;
uint itemId = item.itemId;       // Item ID
bool enabled = item.enableItemPickPoint;  // Whether item is still available
bool isMaterial = item.isMaterial;        // Material vs consumable

// Look up item name
var itemData = ParameterItemData.GetParam(itemId);
string name = itemData.GetName();  // Localized item name
```

**Item removal detection:** Check `enableItemPickPoint` - becomes false when picked up. Also check `gameObject.activeInHierarchy`.

**Source files:**
- `decompiled/Il2Cpp/ItemPickPointBase.cs` - Item pickup with `itemId`, `enableItemPickPoint`
- `decompiled/Il2Cpp/ParameterItemData.cs` - Item data with `GetName()`, `GetDescription()`
- `decompiled/Il2Cpp/ItemPickPointManager.cs` - Singleton `m_instance` with `m_itemPickPoints` list

### 4. Enemy Names

**Class:** `EnemyCtrl` (extends `DigimonCtrl` extends `UnitCtrlBase`)

```csharp
EnemyCtrl enemy = ...;
var placementData = enemy.m_placementData;  // ParameterPlacementEnemy
uint paramId = placementData.m_paramId;     // NpcEnemyParam reference

// Look up enemy species name
var npcEnemyData = ParameterNpcEnemyData.GetParam(paramId);
uint digiParamId = npcEnemyData.m_DigiParamId;
var digimonData = ParameterDigimonData.GetParam(digiParamId);
string name = digimonData.GetDefaultName();

// Also available: level info
int level = placementData.m_level;
```

**Source files:**
- `decompiled/Il2Cpp/EnemyCtrl.cs` - Enemy controller with `m_placementData`
- `decompiled/Il2Cpp/ParameterPlacementEnemy.cs` - Enemy placement with `m_paramId`, `m_level`, stats
- `decompiled/Il2Cpp/EnemyManager.cs` - Singleton with `m_EnemyCtrlArray`

### 5. Map Transition Names

**Class:** `MapTriggerScript` (extends `MonoBehaviour`)

```csharp
MapTriggerScript trigger = ...;
// Only care about map change triggers
if (trigger.enterID != MapTriggerManager.EVENT.MapChange) continue;

// Get destination info from AreaChangeInfo component on same or parent GameObject
var areaChangeInfo = trigger.GetComponent<AreaChangeInfo>();
// OR: Check MainGameField.m_AreaChangeInfo if needed

if (areaChangeInfo != null)
{
    var dest = areaChangeInfo.m_Destination;  // CDestination
    int destMapNo = dest.m_MapNo;
    int destAreaNo = dest.m_AreaNo;

    // Get human-readable area name
    string mapName = ParameterMapName.GetMapName((AppInfo.MAP)destMapNo);
    // OR more specific:
    string areaName = ParameterAreaName.GetAreaName((AppInfo.MAP)destMapNo, (uint)destAreaNo);
}
```

**If AreaChangeInfo is not on the trigger GameObject:** The trigger's position is still valid for navigation. Use the trigger's `gameObject.name` as a fallback label, or check `MainGameField.m_AreaChangeInfo`.

**MapTriggerManager.EVENT enum:**
- `Toilet = 0`
- `MapChange = 1` (area transitions - the ones we care about)
- `TalkEvent = 2`
- `TownCamera = 3`
- `Fishing = 4`
- `Non = -1`

**Source files:**
- `decompiled/Il2Cpp/MapTriggerScript.cs` - Trigger zones with `enterID`, `unitID`, position
- `decompiled/Il2Cpp/AreaChangeInfo.cs` - Destination info (`CDestination` with `m_MapNo`, `m_AreaNo`, `m_SpawnPoint`)
- `decompiled/Il2Cpp/ParameterMapName.cs` - `GetMapName(AppInfo.MAP)` for map names
- `decompiled/Il2Cpp/ParameterAreaName.cs` - `GetAreaName(AppInfo.MAP, uint)` for area names

### 6. NavMesh Pathfinding

The game uses Unity's NavMesh system for character movement.

**Class:** `CharacterMove` (on all moving characters)

Key points:
- Game uses `NavMeshAgent` on characters via `CharacterMove.m_agent`
- `CharacterMove.SetDestinationAI(Vector3)` sets pathfinding destination
- `NavMesh.SamplePosition()` already used by our mod for wall detection
- `NavMesh.CalculatePath()` is available as standard Unity API

**For our pathfinding:**
```csharp
// Calculate walkable path from player to target
NavMeshPath path = new NavMeshPath();
bool found = NavMesh.CalculatePath(playerPos, targetPos, NavMesh.AllAreas, path);

if (found && path.status == NavMeshPathStatus.PathComplete)
{
    // path.corners contains the waypoints
    // Use first few corners to determine direction for audio guidance
    Vector3 nextWaypoint = path.corners[1];  // Next point to walk toward

    // Calculate direction relative to player facing
    Vector3 direction = (nextWaypoint - playerPos).normalized;
    float angle = Vector3.SignedAngle(playerForward, direction, Vector3.up);

    // Convert angle to stereo pan (-1 = full left, +1 = full right)
    float pan = Mathf.Clamp(angle / 90f, -1f, 1f);
}
```

**Path validity:** `NavMeshPathStatus` values:
- `PathComplete` - Full path found
- `PathPartial` - Path found but doesn't reach destination (blocked)
- `PathInvalid` - No path possible

**Source files:**
- `decompiled/Il2Cpp/CharacterMove.cs` - Character movement with `NavMeshAgent`
- Unity API: `NavMesh.CalculatePath()`, `NavMesh.SamplePosition()`, `NavMeshPath`

### 7. Object Lifecycle (Map Loading)

All managers are singletons that persist between maps but clear/reload their data:

**On map change:**
1. `MainGameManager.RequestMapChange(mapNo, areaNo, spawnPoint)` triggers load
2. Managers call their cleanup: `ItemPickPointManager.UnloadItemPickPoint()`, `NpcManager.MapEnd()`, `EnemyManager.MapEnd()`
3. New data loaded from CSV parameters
4. Objects instantiated: items via `LoadItemPickPoint()`, NPCs via `CreateNpc()`, enemies via `_CreateEnemy()`
5. `MapStart()` called when fully ready

**Detection approach for list refresh:**
- Monitor `MainGameManager.mapNo` and `MainGameManager.areaNo` for changes
- When changed, wait briefly for objects to spawn, then rescan
- For items: also monitor `enableItemPickPoint` to detect pickups mid-session

---

## Implementation Plan

### Phase 1: Event List System
- Create `NavigationListHandler` class
- Scan current area for all NPCs, items, transitions, enemies
- Build categorized list with proper names
- Implement category cycling (O/I keys)
- Implement event cycling within category (J/K/L keys)
- Announce current selection via screen reader
- Auto-refresh on map change
- Auto-remove items when picked up

### Phase 2: Audio-Guided Pathfinding
- Use `NavMesh.CalculatePath()` to find walkable route to selected target
- Continuously update path as player moves
- Use positional audio (stereo panning) to indicate direction of next waypoint
- Volume/pitch changes for distance feedback
- Sound stops when target reached (within interaction range)
- Handle partial/invalid paths gracefully

### Phase 3: Integration
- Add hotkeys to `hotkeys.ini` (keyboard + controller sections)
- Use `IsPlayerInControl()` checks from AudioNavigationHandler
- Only activate when audio navigation is active (same conditions)
- Integrate with existing ModInputManager action system

---

## Key Integration Points with Existing Code

### AudioNavigationHandler (existing)
- Already has `IsPlayerInControl()` with all necessary state checks
- Already tracks NPCs, items, transitions, enemies with `FindNearestTarget()`
- Already uses `PositionalAudio` for stereo panning
- Already uses `NavMesh.SamplePosition()` for wall detection
- The new system should share the same activity conditions

### EvolutionHandler (existing)
- Shows the pattern for looking up Digimon names via `parameterManager.digimonData`
- Uses `GetDefaultName()` for localized names
- Pattern: iterate `digimonData.GetRecordMax()` or use `GetParam(id)` for direct lookup

### PositionalAudio (existing)
- NAudio-based stereo panning system
- Can be reused for path guidance audio
- Supports continuous tracking with `UpdatePlayerPosition()` / `UpdateTargetPosition()`

### ModInputManager (existing)
- Central input handler with keyboard + controller bindings
- Add new actions: `CycleFilterForward`, `CycleFilterBackward`, `PrevEvent`, `CurrentEvent`, `NextEvent`, `NavigateToEvent`
