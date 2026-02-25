using Il2Cpp;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides categorized event/point-of-interest navigation for the current area.
    /// Scans for NPCs, items, transitions, and enemies, then lets the player
    /// cycle through categories and events via hotkeys with screen reader announcements.
    /// </summary>
    public class NavigationListHandler : IAccessibilityHandler
    {
        public int Priority => 998;

        /// <summary>
        /// Background handler - never owns the status announce.
        /// </summary>
        public bool IsOpen() => false;

        /// <summary>
        /// Background handler - never announces status.
        /// </summary>
        public void AnnounceStatus() { }

        // Event categories
        private enum EventCategory
        {
            NPCs,
            Items,
            Materials,
            Quest,
            Transitions,
            Enemies,
            Facilities
        }

        // Represents a single navigable point of interest
        private class NavigationEvent
        {
            public string Name;
            public Vector3 Position;
            public GameObject Target;
            public EventCategory Category;
            public float DistanceToPlayer;
            /// <summary>Completion flag set ID for quest items (from form 5 SSetFlagSetData). Checked via saved scenario flags.</summary>
            public uint FlagSetId;
        }

        // Category order for cycling (only non-empty categories are used)
        private static readonly EventCategory[] AllCategories = {
            EventCategory.NPCs,
            EventCategory.Items,
            EventCategory.Materials,
            EventCategory.Quest,
            EventCategory.Transitions,
            EventCategory.Enemies,
            EventCategory.Facilities
        };

        // Event lists per category
        private Dictionary<EventCategory, List<NavigationEvent>> _events = new Dictionary<EventCategory, List<NavigationEvent>>();

        // Active (non-empty) categories for cycling
        private List<EventCategory> _activeCategories = new List<EventCategory>();

        // Current navigation state
        private int _currentCategoryIndex = -1;
        private int _currentEventIndex = 0;
        private EventCategory _preferredCategory = EventCategory.NPCs; // Preserved across map changes

        // Map change detection
        private int _lastMapNo = -1;
        private int _lastAreaNo = -1;

        // Scan cooldown (don't scan every frame)
        private float _lastScanTime = 0f;
        private const float ScanInterval = 1.0f;

        // Cached references
        private PlayerCtrl _playerCtrl;
        private NpcManager _npcManager;
        private EnemyManager _enemyManager;
        private float _lastSearchTime = 0f;

        // Track if list has been built for current area
        private bool _listBuilt = false;

        // Track if async data has finished loading (items, NPCs, enemies load via coroutines)
        private bool _itemsLoadComplete = false;
        private bool _npcsLoadComplete = false;
        private bool _enemiesLoadComplete = false;

        // Continuous rescan after map change (objects load asynchronously)
        private float _mapChangeTime = 0f;
        private float _nextRescanTime = 0f;
        private const float InitialScanDelay = 0.5f;
        private const float RescanInterval = 0.5f;
        private const float RescanDuration = 5.0f; // Keep rescanning for 5 seconds after map change
        private bool _isRescanning = false;

        // Track known GameObjects to avoid duplicate entries
        private HashSet<GameObject> _knownTargets = new HashSet<GameObject>();

        // Track NPC GameObjects that are actually facilities (to exclude from NPC list)
        private HashSet<GameObject> _facilityNpcObjects = new HashSet<GameObject>();

        // Cache for resolved quest item data: cmdBlock -> (item name, item ID, flag set ID)
        private static Dictionary<string, (string name, uint itemId, uint flagSetId)> _questItemCache = new Dictionary<string, (string, uint, uint)>();


        // Track field state to rescan after evolution/events
        private bool _wasInField = false;

        // Pathfinding beacon
        private PathfindingBeacon _pathfindingBeacon;
        private Vector3 _pathfindingDestination;
        private Vector3 _pathfindingRawDestination; // Actual target position before NavMesh sampling
        private GameObject _pathfindingTarget;
        private float _lastPathRecalcTime;
        private bool _isPathfinding;
        private EventCategory _pathfindingCategory;
        private const float PathRecalcInterval = 0.5f;

        // Auto-walk state
        private bool _autoWalkEnabled = false;
        private bool _isAutoWalking = false;
        private Vector3[] _autoWalkCorners;
        private int _autoWalkCornerIndex;
        private const float CornerReachDistance = 1.5f;
        private const float FinalApproachDistance = 0.1f;

        // Stuck detection: if player hasn't moved while auto-walking, they've arrived
        private Vector3 _autoWalkCheckPosition;
        private float _autoWalkCheckTime;
        private const float StuckCheckInterval = 1.0f;
        private const float StuckMovementThreshold = 0.3f;
        private const float StuckArrivalDistance = 15f; // Within this range, stuck = arrived

        // Obstacle avoidance: when stuck mid-path, find walkable detour to get around enemies/obstacles
        private bool _isAvoidingObstacle;
        private Vector3 _avoidanceDetourTarget;
        private float _avoidanceStartTime;
        private int _avoidanceAttempt; // Increments to alternate left/right
        private const float AvoidanceTimeout = 10f; // Max seconds trying to reach detour point

        // Virtual stick output for auto-walk (read by GamepadInputPatch)
        public static bool AutoWalkActive { get; private set; }
        public static float AutoWalkStickX { get; private set; }
        public static float AutoWalkStickY { get; private set; }
        public static float AutoWalkCameraStickX { get; private set; }

        // Pitfall avoidance: EventTriggerScripts with PIT_ command blocks
        private List<Vector3> _pitfallPositions = new List<Vector3>();
        private const float PitfallAvoidanceRadius = 10f; // How far to route around pitfalls

        // Evolution state (set by Main before Update)
        private bool _evolutionActive = false;

        public void SetEvolutionActive(bool active)
        {
            _evolutionActive = active;
        }

        public void Update()
        {
            // Find managers periodically
            float currentTime = Time.time;
            if (_playerCtrl == null || currentTime - _lastSearchTime > 2f)
            {
                _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                _npcManager = UnityEngine.Object.FindObjectOfType<NpcManager>();
                _enemyManager = UnityEngine.Object.FindObjectOfType<EnemyManager>();
                _lastSearchTime = currentTime;
            }

            // Only work when player is in the field and in control
            bool inField = IsPlayerInField();
            if (!inField)
            {
                _wasInField = false;

                // Pause pathfinding beacon audio when leaving field (auto-walk pauses naturally since Update stops)
                if (_isPathfinding && _pathfindingBeacon != null && _pathfindingBeacon.IsActive)
                    _pathfindingBeacon.Stop();

                return;
            }

            // Refresh lists when returning to field from any absence (battle, NPC talk,
            // item pickup, facility, etc.) rather than polling every second while walking.
            if (!_wasInField && _listBuilt)
            {
                // Any absence (battle, NPC talk, item pickup, etc.) - do additive rescan
                // to catch newly activated quest triggers after accepting quests
                _isRescanning = true;
                _mapChangeTime = currentTime;
                _nextRescanTime = currentTime + InitialScanDelay;
                DebugLogger.Log("[NavList] Returned to field, rescanning (preserving lists)");
            }
            _wasInField = true;

            // Check for map changes
            CheckMapChange();

            // During rescan period after map change, keep scanning for newly loaded objects
            if (_isRescanning && currentTime >= _nextRescanTime)
            {
                RescanForNewObjects();
                _nextRescanTime = currentTime + RescanInterval;

                if (currentTime - _mapChangeTime > RescanDuration)
                {
                    _isRescanning = false;
                    DebugLogger.Log($"[NavList] Rescan period ended. Final counts: " +
                        $"{_events[EventCategory.NPCs].Count} NPCs, " +
                        $"{_events[EventCategory.Items].Count} items, " +
                        $"{_events[EventCategory.Materials].Count} materials, " +
                        $"{_events[EventCategory.Quest].Count} quest, " +
                        $"{_events[EventCategory.Transitions].Count} transitions, " +
                        $"{_events[EventCategory.Enemies].Count} enemies, " +
                        $"{_events[EventCategory.Facilities].Count} facilities");

                }
            }

            // Handle input
            HandleInput();

            // Update pathfinding beacon
            if (_isPathfinding)
                UpdatePathfinding();
        }

        /// <summary>
        /// Check if the player is in the field (not in battle, not in menus, not in events).
        /// Uses the player's actionState as the game's own movement check.
        /// </summary>
        private bool IsPlayerInField()
        {
            // Evolution plays on the field but disables items/transitions
            if (_evolutionActive)
                return false;

            if (_playerCtrl == null) return false;
            if (_playerCtrl.actionState != UnitCtrlBase.ActionState.ActionState_Idle)
                return false;
            if (GameStateService.IsInBattlePhase())
                return false;
            if (GameStateService.IsGamePaused())
                return false;

            return true;
        }

        /// <summary>
        /// Detect when the player enters a new map/area and rebuild lists.
        /// After map change, continuously rescans for 5 seconds since objects
        /// load asynchronously via coroutines and appear at different times.
        /// </summary>
        private void CheckMapChange()
        {
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm == null) return;

                int mapNo = mgm.mapNo;
                int areaNo = mgm.areaNo;

                if (mapNo != _lastMapNo || areaNo != _lastAreaNo)
                {
                    _lastMapNo = mapNo;
                    _lastAreaNo = areaNo;

                    // Stop pathfinding on map change (target won't exist on new map)
                    if (_isPathfinding)
                        StopPathfinding(null);

                    // Reset for new map
                    _listBuilt = false;
                    _mapChangeTime = Time.time;
                    _nextRescanTime = Time.time + InitialScanDelay;
                    _isRescanning = true;

                    // Save preferred category before reset
                    var currentCat = GetCurrentCategory();
                    if (currentCat.HasValue)
                        _preferredCategory = currentCat.Value;

                    _currentEventIndex = 0;
                    _events.Clear();
                    _events[EventCategory.NPCs] = new List<NavigationEvent>();
                    _events[EventCategory.Items] = new List<NavigationEvent>();
                    _events[EventCategory.Materials] = new List<NavigationEvent>();
                    _events[EventCategory.Quest] = new List<NavigationEvent>();
                    _events[EventCategory.Transitions] = new List<NavigationEvent>();
                    _events[EventCategory.Enemies] = new List<NavigationEvent>();
                    _events[EventCategory.Facilities] = new List<NavigationEvent>();
                    _activeCategories.Clear();
                    _knownTargets.Clear();
                    _facilityNpcObjects.Clear();
                    _questItemCache.Clear();
                    _itemsLoadComplete = false;
                    _npcsLoadComplete = false;
                    _enemiesLoadComplete = false;

                    // Clear pitfall data from previous map
                    _pitfallPositions.Clear();

                    DebugLogger.Log($"[NavList] Map changed to {mapNo}/{areaNo}, will rescan for {RescanDuration}s");
                }

                // Initial build after delay
                if (!_listBuilt && Time.time >= _nextRescanTime)
                {
                    BuildLists();
                    _listBuilt = true;
                    _lastScanTime = Time.time;
                    _nextRescanTime = Time.time + RescanInterval;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] CheckMapChange error: {ex.Message}");
            }
        }

        /// <summary>
        /// During the rescan period, look for newly spawned objects and add them to existing lists.
        /// This catches NPCs, enemies, items, and transitions that load asynchronously.
        /// </summary>
        private void RescanForNewObjects()
        {
            if (_playerCtrl == null) return;

            Vector3 playerPos = _playerCtrl.transform.position;
            int newCount = 0;

            // Scan facilities BEFORE NPCs so facility NPCs are excluded from NPC list
            try
            {
                var rescanMgm = MainGameManager.m_instance;

                // Strategy 1: EventTriggerManager dictionary + CSV (field areas)
                var rescanEventTriggerMgr = rescanMgm?.m_EventTriggerMgr;
                if (rescanEventTriggerMgr != null)
                {
                    var triggerDict = rescanEventTriggerMgr.m_TriggerDictionary;
                    var placementData = rescanEventTriggerMgr.m_CsvbPlacementData;
                    if (triggerDict != null && placementData != null)
                    {
                        var enumerator = triggerDict.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var trigger = enumerator.Current.Value;
                            if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                                continue;
                            if (_knownTargets.Contains(trigger.gameObject))
                                continue;

                            try
                            {
                                uint placementId = enumerator.Current.Key;
                                var npcData = HashIdSearchClass<ParameterPlacementNpc>.GetParam(placementData, placementId);
                                if (npcData == null) continue;

                                var facilityType = (MainGameManager.Facility)npcData.m_Facility;
                                if (facilityType == MainGameManager.Facility.None) continue;

                                string name = ResolveFacilityName(facilityType, npcData);
                                float dist = Vector3.Distance(playerPos, trigger.transform.position);
                                _events[EventCategory.Facilities].Add(new NavigationEvent
                                {
                                    Name = name, Position = trigger.transform.position,
                                    Target = trigger.gameObject, Category = EventCategory.Facilities,
                                    DistanceToPlayer = dist
                                });
                                _knownTargets.Add(trigger.gameObject);
                                newCount++;
                            }
                            catch { }
                        }
                    }
                }

                // Strategy 2: NpcManager placement data (town facility NPCs)
                if (_npcManager != null)
                {
                    try
                    {
                        var placementList = _npcManager.m_placementNpcList;
                        if (placementList != null)
                        {
                            int placementCount = placementList.Count;
                            for (int i = 0; i < placementCount; i++)
                            {
                                try
                                {
                                    var placement = placementList[i];
                                    if (placement == null) continue;

                                    var facilityType = (MainGameManager.Facility)placement.m_Facility;
                                    if (facilityType == MainGameManager.Facility.None) continue;

                                    uint placementId = placement.id;
                                    NpcCtrl npcCtrl = null;
                                    try { npcCtrl = _npcManager._GetNpcCtrlFromPlacementId(placementId); }
                                    catch { }

                                    if (npcCtrl == null || npcCtrl.gameObject == null || !npcCtrl.gameObject.activeInHierarchy)
                                        continue;
                                    if (_knownTargets.Contains(npcCtrl.gameObject))
                                        continue;

                                    string name = ResolveFacilityName(facilityType, placement);
                                    float dist = Vector3.Distance(playerPos, npcCtrl.transform.position);
                                    _events[EventCategory.Facilities].Add(new NavigationEvent
                                    {
                                        Name = name, Position = npcCtrl.transform.position,
                                        Target = npcCtrl.gameObject, Category = EventCategory.Facilities,
                                        DistanceToPlayer = dist
                                    });
                                    _knownTargets.Add(npcCtrl.gameObject);
                                    _facilityNpcObjects.Add(npcCtrl.gameObject);
                                    newCount++;
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                // Strategy 3: MapTriggerScript scan for fishing spots and toilets
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
                foreach (var trigger in mapTriggers)
                {
                    if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                        continue;
                    if (_knownTargets.Contains(trigger.gameObject))
                        continue;

                    string name = null;
                    if (trigger.enterID == MapTriggerManager.EVENT.Fishing)
                        name = "Fishing Spot";
                    else if (trigger.enterID == MapTriggerManager.EVENT.Toilet)
                        name = "Toilet";

                    if (name == null) continue;

                    float dist = Vector3.Distance(playerPos, trigger.transform.position);
                    _events[EventCategory.Facilities].Add(new NavigationEvent
                    {
                        Name = name, Position = trigger.transform.position,
                        Target = trigger.gameObject, Category = EventCategory.Facilities,
                        DistanceToPlayer = dist
                    });
                    _knownTargets.Add(trigger.gameObject);
                    newCount++;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan Facilities error: {ex.Message}");
            }

            // Scan for new NPCs (excluding facility NPCs) - only if NPC data has finished loading
            if (!_npcsLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndNpcPlacementData())
                    {
                        _npcsLoadComplete = true;
                        DebugLogger.Log("[NavList] NPC placement data load complete, scanning NPCs");
                    }
                }
                catch { }
            }
            try
            {
                if (_npcsLoadComplete && _npcManager != null && _npcManager.m_NpcCtrlArray != null)
                {
                    foreach (var npc in _npcManager.m_NpcCtrlArray)
                    {
                        if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy)
                            continue;
                        if (_knownTargets.Contains(npc.gameObject))
                            continue;
                        if (_facilityNpcObjects.Contains(npc.gameObject))
                            continue;

                        string name = GetNpcName(npc);
                        float dist = Vector3.Distance(playerPos, npc.transform.position);
                        _events[EventCategory.NPCs].Add(new NavigationEvent
                        {
                            Name = name, Position = npc.transform.position,
                            Target = npc.gameObject, Category = EventCategory.NPCs,
                            DistanceToPlayer = dist
                        });
                        _knownTargets.Add(npc.gameObject);
                        newCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan NPCs error: {ex.Message}");
            }

            // Scan for new items and materials (only if item data has finished loading)
            if (!_itemsLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndItemPickPointData())
                    {
                        _itemsLoadComplete = true;
                        DebugLogger.Log("[NavList] Item pick point data load complete, scanning items");
                    }
                }
                catch { }
            }
            try
            {
                var itemManager = ItemPickPointManager.m_instance;
                if (_itemsLoadComplete && itemManager != null && itemManager.m_itemPickPoints != null)
                {
                    foreach (var point in itemManager.m_itemPickPoints)
                    {
                        if (point == null || point.gameObject == null)
                            continue;
                        if (!point.enableItemPickPoint)
                            continue;
                        if (_knownTargets.Contains(point.gameObject))
                            continue;

                        var category = GetItemCategory(point);
                        string name = GetItemName(point);
                        float dist = Vector3.Distance(playerPos, point.transform.position);
                        _events[category].Add(new NavigationEvent
                        {
                            Name = name, Position = point.transform.position,
                            Target = point.gameObject, Category = category,
                            DistanceToPlayer = dist
                        });
                        _knownTargets.Add(point.gameObject);
                        newCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan Items error: {ex.Message}");
            }

            // Rescan transitions using map icon data (same as initial scan)
            int transitionsBefore = _events[EventCategory.Transitions].Count;
            ScanTransitions(playerPos);
            newCount += _events[EventCategory.Transitions].Count - transitionsBefore;

            // Scan for new enemies - only if enemy data has finished loading
            if (!_enemiesLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndEnemyPlacementData())
                    {
                        _enemiesLoadComplete = true;
                        DebugLogger.Log("[NavList] Enemy placement data load complete, scanning enemies");
                    }
                }
                catch { }
            }
            try
            {
                if (_enemiesLoadComplete && _enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
                {
                    foreach (var enemy in _enemyManager.m_EnemyCtrlArray)
                    {
                        if (!GameStateService.IsEnemyAlive(enemy))
                            continue;
                        if (_knownTargets.Contains(enemy.gameObject))
                            continue;

                        string name = GetEnemyName(enemy);
                        float dist = Vector3.Distance(playerPos, enemy.transform.position);
                        _events[EventCategory.Enemies].Add(new NavigationEvent
                        {
                            Name = name, Position = enemy.transform.position,
                            Target = enemy.gameObject, Category = EventCategory.Enemies,
                            DistanceToPlayer = dist
                        });
                        _knownTargets.Add(enemy.gameObject);
                        newCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan Enemies error: {ex.Message}");
            }

            // Scan for new quest triggers (EventTriggerScripts load asynchronously)
            try
            {
                var eventTriggers = Resources.FindObjectsOfTypeAll<EventTriggerScript>();
                var placementLookup = BuildPlacementLookup();
                if (_debugQuestScan)
                    DebugLogger.Log($"[QuestRescan] Starting rescan: {eventTriggers.Count} triggers, {placementLookup.Count} placements");
                foreach (var et in eventTriggers)
                {
                    if (et == null || et.gameObject == null)
                        continue;

                    string goName = "?";
                    try { goName = et.gameObject.name; } catch { }

                    if (!et.gameObject.activeInHierarchy)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP inactive: {goName} paramId={et.m_EventParamId}");
                        continue;
                    }

                    bool enabled = false;
                    try { enabled = et.enabled; } catch { }
                    if (!enabled)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP disabled: {goName} paramId={et.m_EventParamId}");
                        continue;
                    }
                    if (_knownTargets.Contains(et.gameObject))
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP known: {goName}");
                        continue;
                    }
                    if (et.gameObject.GetComponent<NpcCtrl>() != null)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP NPC: {goName}");
                        continue;
                    }
                    if (et.gameObject.GetComponent<EnemyCtrl>() != null)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP enemy: {goName}");
                        continue;
                    }

                    string cmdBlock = null;
                    ParameterPlacementNpc placement = null;
                    if (placementLookup.TryGetValue(et.m_EventParamId, out placement))
                        cmdBlock = placement.m_CmdBlock;
                    if (string.IsNullOrEmpty(cmdBlock))
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP no cmdBlock: {goName} paramId={et.m_EventParamId}");
                        continue;
                    }
                    if (placement != null && (MainGameManager.Facility)placement.m_Facility != MainGameManager.Facility.None)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP facility: {goName} cmd={cmdBlock}");
                        continue;
                    }
                    if (cmdBlock.StartsWith("VENDOR_") || cmdBlock.StartsWith("EVENT."))
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP vendor/event: {goName} cmd={cmdBlock}");
                        continue;
                    }
                    try
                    {
                        var eid = et.enterID;
                        if (eid == MapTriggerManager.EVENT.MapChange ||
                            eid == MapTriggerManager.EVENT.TownCamera ||
                            eid == MapTriggerManager.EVENT.Fishing ||
                            eid == MapTriggerManager.EVENT.Toilet)
                        {
                            if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP enterID: {goName} cmd={cmdBlock} eid={eid}");
                            continue;
                        }
                    }
                    catch { }

                    if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] Passed filters: {goName} cmd={cmdBlock} paramId={et.m_EventParamId} enabled={enabled}");

                    // For non-PICK_ triggers, use et.enabled as quest acceptance gate.
                    // The game toggles EventTriggerScript.enabled via ActiveTalkEventTrigger
                    // when quests are accepted/completed. This works for both icon and no-icon triggers.
                    // PICK_ triggers already check enabled above, so this only applies to ST_SUB_ etc.
                    if (!cmdBlock.StartsWith("PICK_") && !enabled)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP disabled non-PICK: {goName} cmd={cmdBlock}");
                        continue;
                    }

                    if (cmdBlock.StartsWith("PICK_"))
                    {
                        var questInfo = ResolveQuestItem(cmdBlock);
                        string name = questInfo.name ?? "Quest Pickup";
                        uint flagSetId = questInfo.flagSetId;

                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] PICK_ trigger: {goName} cmd={cmdBlock} name={name} flagSetId=0x{flagSetId:X}");

                        if (flagSetId != 0 && IsQuestFlagSet(flagSetId))
                        {
                            if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] SKIP completed: {goName} flag=0x{flagSetId:X}");
                            continue;
                        }
                        // No acceptance check - show all uncompleted PICK_ items.
                        // The completion flag check above already filters finished quests.
                        float dist = Vector3.Distance(playerPos, et.transform.position);
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] ADDED PICK_: {goName} cmd={cmdBlock} name={name} dist={dist:F1}");
                        _events[EventCategory.Quest].Add(new NavigationEvent
                        {
                            Name = name, Position = et.transform.position,
                            Target = et.gameObject, Category = EventCategory.Quest,
                            DistanceToPlayer = dist, FlagSetId = flagSetId
                        });
                    }
                    else
                    {
                        uint completionFlag = GetCompletionFlagForCmdBlock(cmdBlock);
                        if (completionFlag != 0 && IsQuestFlagSet(completionFlag))
                            continue;
                        float dist = Vector3.Distance(playerPos, et.transform.position);
                        if (_debugQuestScan) DebugLogger.Log($"[QuestRescan] ADDED non-PICK: {goName} cmd={cmdBlock} flag=0x{completionFlag:X} dist={dist:F1}");
                        _events[EventCategory.Quest].Add(new NavigationEvent
                        {
                            Name = "Quest Event", Position = et.transform.position,
                            Target = et.gameObject, Category = EventCategory.Quest,
                            DistanceToPlayer = dist, FlagSetId = completionFlag
                        });
                    }
                    _knownTargets.Add(et.gameObject);
                    newCount++;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan Quest error: {ex.Message}");
            }

            // Rescan for pitfalls if none found yet (placement data may not have been ready during BuildLists)
            if (_pitfallPositions.Count == 0)
                ScanPitfalls();

            // Remove destroyed objects, picked-up items, and defeated enemies during rescan.
            // Don't remove inactive non-item/non-enemy objects - the game deactivates distant objects for performance.
            foreach (var cat in AllCategories)
            {
                if (_events.ContainsKey(cat))
                {
                    _events[cat].RemoveAll(e =>
                    {
                        // Destroyed object
                        if (e.Target == null)
                            return true;

                        // Picked-up items/materials: enableItemPickPoint becomes false after pickup.
                        if (e.Category == EventCategory.Items || e.Category == EventCategory.Materials || e.Category == EventCategory.Quest)
                        {
                            var pickPoint = e.Target.GetComponent<ItemPickPointBase>();
                            if (pickPoint != null && !pickPoint.enableItemPickPoint)
                            {
                                _knownTargets.Remove(e.Target);
                                return true;
                            }
                        }

                        // Completed quests: completion flag is now set
                        if (e.Category == EventCategory.Quest && e.FlagSetId != 0 && IsQuestFlagSet(e.FlagSetId))
                        {
                            _knownTargets.Remove(e.Target);
                            return true;
                        }

                        // Defeated/inactive enemies: check actionState for dead/dying
                        if (e.Category == EventCategory.Enemies && !e.Target.activeInHierarchy)
                        {
                            _knownTargets.Remove(e.Target);
                            return true;
                        }

                        return false;
                    });
                }
            }

            // Preserve selected item across sort
            var selectedEvent = GetSelectedEvent();

            // Always update categories after cleanup (items may have been removed)
            foreach (var kvp in _events)
                kvp.Value.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
            UpdateActiveCategories();
            RestoreSelectionAfterSort(selectedEvent);

            if (newCount > 0)
            {
                DebugLogger.Log($"[NavList] Rescan found {newCount} new objects. Totals: " +
                    $"{_events[EventCategory.NPCs].Count} NPCs, " +
                    $"{_events[EventCategory.Items].Count} items, " +
                    $"{_events[EventCategory.Materials].Count} materials, " +
                    $"{_events[EventCategory.Transitions].Count} transitions, " +
                    $"{_events[EventCategory.Enemies].Count} enemies, " +
                    $"{_events[EventCategory.Facilities].Count} facilities");
            }
        }

        /// <summary>
        /// Build the full event lists by scanning the current area.
        /// </summary>
        private void BuildLists()
        {
            _events.Clear();
            _events[EventCategory.NPCs] = new List<NavigationEvent>();
            _events[EventCategory.Items] = new List<NavigationEvent>();
            _events[EventCategory.Materials] = new List<NavigationEvent>();
            _events[EventCategory.Quest] = new List<NavigationEvent>();
            _events[EventCategory.Transitions] = new List<NavigationEvent>();
            _events[EventCategory.Enemies] = new List<NavigationEvent>();
            _events[EventCategory.Facilities] = new List<NavigationEvent>();
            _knownTargets.Clear();
            _facilityNpcObjects.Clear();

            Vector3 playerPos = _playerCtrl != null ? _playerCtrl.transform.position : Vector3.zero;

            // Scan facilities BEFORE NPCs so facility NPCs are excluded from the NPC list
            ScanFacilities(playerPos);

            // NPCs load asynchronously via coroutines - only scan once the game confirms loading is done
            if (!_npcsLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndNpcPlacementData())
                        _npcsLoadComplete = true;
                }
                catch { }
            }
            if (_npcsLoadComplete)
                ScanNPCs(playerPos);
            else
                DebugLogger.Log("[NavList] NPC placement data not yet loaded, skipping NPC scan");

            // Items load asynchronously via coroutines - only scan once the game confirms loading is done
            if (!_itemsLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndItemPickPointData())
                        _itemsLoadComplete = true;
                }
                catch { }
            }
            if (_itemsLoadComplete)
                ScanItems(playerPos);
            else
                DebugLogger.Log("[NavList] Item pick point data not yet loaded, skipping item scan");

            ScanTransitions(playerPos);

            // Quest items use the Item MonoBehaviour and spawn based on scenario flags
            ScanQuestItems(playerPos);

            // Enemies load asynchronously via coroutines - only scan once the game confirms loading is done
            if (!_enemiesLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndEnemyPlacementData())
                        _enemiesLoadComplete = true;
                }
                catch { }
            }
            if (_enemiesLoadComplete)
                ScanEnemies(playerPos);
            else
                DebugLogger.Log("[NavList] Enemy placement data not yet loaded, skipping enemy scan");

            // Scan for pitfall triggers (PIT_ command blocks) and place NavMesh obstacles
            ScanPitfalls();

            // Sort each category by distance
            foreach (var kvp in _events)
            {
                kvp.Value.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
            }

            // Track all found objects
            foreach (var kvp in _events)
            {
                foreach (var e in kvp.Value)
                {
                    if (e.Target != null)
                        _knownTargets.Add(e.Target);
                }
            }

            UpdateActiveCategories();

            DebugLogger.Log($"[NavList] Built lists: {_events[EventCategory.NPCs].Count} NPCs, " +
                $"{_events[EventCategory.Items].Count} items, " +
                $"{_events[EventCategory.Materials].Count} materials, " +
                $"{_events[EventCategory.Quest].Count} quest, " +
                $"{_events[EventCategory.Transitions].Count} transitions, " +
                $"{_events[EventCategory.Enemies].Count} enemies, " +
                $"{_events[EventCategory.Facilities].Count} facilities");
        }

        /// <summary>
        /// Refresh lists to catch items picked up, enemies defeated, etc.
        /// </summary>
        private void RefreshLists()
        {
            if (_playerCtrl == null) return;

            Vector3 playerPos = _playerCtrl.transform.position;

            // Refresh items and materials - remove picked up, add newly loaded.
            // Items load via coroutines so some may appear after the initial scan.
            Func<NavigationEvent, bool> removePickedUp = e =>
            {
                if (e.Target == null)
                    return true;
                try
                {
                    var pickPoint = e.Target.GetComponent<ItemPickPointBase>();
                    if (pickPoint != null && !pickPoint.enableItemPickPoint)
                        return true;
                }
                catch { }
                return false;
            };

            var items = _events.ContainsKey(EventCategory.Items) ? _events[EventCategory.Items] : null;
            var materials = _events.ContainsKey(EventCategory.Materials) ? _events[EventCategory.Materials] : null;
            var keyItems = _events.ContainsKey(EventCategory.Quest) ? _events[EventCategory.Quest] : null;
            items?.RemoveAll(e => removePickedUp(e));
            materials?.RemoveAll(e => removePickedUp(e));

            // Quest items (EventTrigger pickups) - remove if destroyed, inactive, or consumed
            // Uses scenario completion flags from form 5 (SSetFlagSetData) which persist in save data
            keyItems?.RemoveAll(e =>
            {
                if (e.Target == null || !e.Target.activeInHierarchy)
                    return true;
                if (e.FlagSetId != 0 && IsQuestFlagSet(e.FlagSetId))
                    return true;
                try
                {
                    var pickPoint = e.Target.GetComponent<ItemPickPointBase>();
                    if (pickPoint != null && !pickPoint.enableItemPickPoint)
                        return true;
                }
                catch { }
                return false;
            });

            // Check for new items/materials/quest that may have loaded since the last scan
            if (!_itemsLoadComplete)
            {
                try
                {
                    var mgm = MainGameManager.m_instance;
                    if (mgm != null && mgm._IsLoadEndItemPickPointData())
                    {
                        _itemsLoadComplete = true;
                        DebugLogger.Log("[NavList] Item pick point data load complete during refresh");
                    }
                }
                catch { }
            }
            var itemManager = ItemPickPointManager.m_instance;
            if (_itemsLoadComplete && itemManager != null && itemManager.m_itemPickPoints != null)
            {
                var existingTargets = new HashSet<GameObject>();
                if (items != null)
                    foreach (var e in items)
                        if (e.Target != null) existingTargets.Add(e.Target);
                if (materials != null)
                    foreach (var e in materials)
                        if (e.Target != null) existingTargets.Add(e.Target);
                if (keyItems != null)
                    foreach (var e in keyItems)
                        if (e.Target != null) existingTargets.Add(e.Target);

                foreach (var point in itemManager.m_itemPickPoints)
                {
                    if (point == null || point.gameObject == null)
                        continue;
                    if (!point.enableItemPickPoint)
                        continue;
                    if (existingTargets.Contains(point.gameObject))
                        continue;

                    var category = GetItemCategory(point);
                    var targetList = category == EventCategory.Materials ? materials
                                  : category == EventCategory.Quest ? keyItems
                                  : items;
                    if (targetList == null) continue;

                    string name = GetItemName(point);
                    float dist = Vector3.Distance(playerPos, point.transform.position);
                    targetList.Add(new NavigationEvent
                    {
                        Name = name,
                        Position = point.transform.position,
                        Target = point.gameObject,
                        Category = category,
                        DistanceToPlayer = dist
                    });
                }
            }

            // Check for new quest pickup triggers that may have been activated
            // Only if minimap shows quest icons for current area
            if (keyItems != null)
            {
                try
                {
                    var existingQuestTargets = new HashSet<GameObject>();
                    foreach (var e in keyItems)
                        if (e.Target != null) existingQuestTargets.Add(e.Target);

                    var placementLookup = BuildPlacementLookup();
                    var eventTriggers = Resources.FindObjectsOfTypeAll<EventTriggerScript>();
                    foreach (var et in eventTriggers)
                    {
                        if (et == null || et.gameObject == null || !et.gameObject.activeInHierarchy)
                            continue;
                        if (et.gameObject.GetComponent<NpcCtrl>() != null)
                            continue;
                        if (et.gameObject.GetComponent<EnemyCtrl>() != null)
                            continue;
                        if (existingQuestTargets.Contains(et.gameObject))
                            continue;

                        string cmdBlock = null;
                        ParameterPlacementNpc placement = null;
                        if (placementLookup.TryGetValue(et.m_EventParamId, out placement))
                            cmdBlock = placement.m_CmdBlock;
                        if (string.IsNullOrEmpty(cmdBlock) || !cmdBlock.StartsWith("PICK_"))
                            continue;

                        var questInfo = ResolveQuestItem(cmdBlock);
                        string name = questInfo.name ?? "Quest Pickup";
                        uint itemId = questInfo.itemId;
                        uint flagSetId = questInfo.flagSetId;

                        // Skip items already consumed (checked via saved scenario flags)
                        if (flagSetId != 0 && IsQuestFlagSet(flagSetId))
                            continue;

                        float dist = Vector3.Distance(playerPos, et.transform.position);
                        keyItems.Add(new NavigationEvent
                        {
                            Name = name,
                            Position = et.transform.position,
                            Target = et.gameObject,
                            Category = EventCategory.Quest,
                            DistanceToPlayer = dist,
                            FlagSetId = flagSetId
                        });
                    }
                }
                catch { }
            }

            // Update distances (only for active objects, keep cached position for inactive)
            Action<List<NavigationEvent>> updateDistances = list =>
            {
                if (list == null) return;
                foreach (var e in list)
                {
                    if (e.Target != null && e.Target.activeInHierarchy)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            };
            updateDistances(items);
            updateDistances(materials);
            updateDistances(keyItems);

            // Refresh enemies - use IsEnemyAlive for immediate defeat detection (same as audio nav)
            var enemies = _events.ContainsKey(EventCategory.Enemies) ? _events[EventCategory.Enemies] : null;
            if (enemies != null)
            {
                enemies.RemoveAll(e =>
                {
                    bool dead = false;
                    if (e.Target == null)
                    {
                        dead = true;
                    }
                    else
                    {
                        try
                        {
                            var enemyCtrl = e.Target.GetComponent<EnemyCtrl>();
                            dead = enemyCtrl == null || !GameStateService.IsEnemyAlive(enemyCtrl);
                        }
                        catch { dead = true; }
                    }
                    if (dead)
                    {
                        _knownTargets.Remove(e.Target);
                        if (_isPathfinding && _pathfindingTarget == e.Target)
                            StopPathfinding("Target defeated");
                        return true;
                    }
                    return false;
                });

                // Check for new enemies that may have spawned (only if enemy data loaded)
                if (!_enemiesLoadComplete)
                {
                    try
                    {
                        var mgm = MainGameManager.m_instance;
                        if (mgm != null && mgm._IsLoadEndEnemyPlacementData())
                        {
                            _enemiesLoadComplete = true;
                            DebugLogger.Log("[NavList] Enemy placement data load complete during refresh");
                        }
                    }
                    catch { }
                }
                if (_enemiesLoadComplete && _enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
                {
                    var existingTargets = new HashSet<GameObject>();
                    foreach (var e in enemies)
                        if (e.Target != null) existingTargets.Add(e.Target);

                    foreach (var enemy in _enemyManager.m_EnemyCtrlArray)
                    {
                        if (!GameStateService.IsEnemyAlive(enemy))
                            continue;
                        if (existingTargets.Contains(enemy.gameObject))
                            continue;

                        string name = GetEnemyName(enemy);
                        float dist = Vector3.Distance(playerPos, enemy.transform.position);
                        enemies.Add(new NavigationEvent
                        {
                            Name = name,
                            Position = enemy.transform.position,
                            Target = enemy.gameObject,
                            Category = EventCategory.Enemies,
                            DistanceToPlayer = dist
                        });
                    }
                }

                // Update distances for remaining active enemies
                foreach (var e in enemies)
                {
                    e.Position = e.Target.transform.position;
                    e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                }
            }

            // Refresh NPCs - remove if destroyed or deactivated (recruited NPCs get deactivated).
            var npcs = _events.ContainsKey(EventCategory.NPCs) ? _events[EventCategory.NPCs] : null;
            if (npcs != null)
            {
                npcs.RemoveAll(e => e.Target == null || !e.Target.activeInHierarchy);
                foreach (var e in npcs)
                {
                    if (e.Target != null && e.Target.activeInHierarchy)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Refresh transitions - only remove if destroyed (null).
            var transitions = _events.ContainsKey(EventCategory.Transitions) ? _events[EventCategory.Transitions] : null;
            if (transitions != null)
            {
                transitions.RemoveAll(e => e.Target == null);
                foreach (var e in transitions)
                {
                    if (e.Target != null && e.Target.activeInHierarchy)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Refresh facilities - only remove if destroyed (null).
            var facilities = _events.ContainsKey(EventCategory.Facilities) ? _events[EventCategory.Facilities] : null;
            if (facilities != null)
            {
                facilities.RemoveAll(e => e.Target == null);
                foreach (var e in facilities)
                {
                    if (e.Target != null && e.Target.activeInHierarchy)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Preserve selected item across sort
            var selectedEvent = GetSelectedEvent();

            // Re-sort all categories by distance (closest first)
            foreach (var kvp in _events)
                kvp.Value.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));

            UpdateActiveCategories();

            // Restore selection to same item after sort, or clamp if removed
            RestoreSelectionAfterSort(selectedEvent);
            ClampIndices();
        }

        /// <summary>
        /// Rebuild the active (non-empty) categories list.
        /// </summary>
        private void UpdateActiveCategories()
        {
            var previousCategory = GetCurrentCategory();
            _activeCategories.Clear();

            foreach (var cat in AllCategories)
            {
                if (_events.ContainsKey(cat) && _events[cat].Count > 0)
                {
                    _activeCategories.Add(cat);
                }
            }

            // Try to preserve current category selection
            if (previousCategory.HasValue && _activeCategories.Contains(previousCategory.Value))
            {
                _currentCategoryIndex = _activeCategories.IndexOf(previousCategory.Value);
            }
            else if (_activeCategories.Contains(_preferredCategory))
            {
                // Restore preferred category (preserved across map changes)
                _currentCategoryIndex = _activeCategories.IndexOf(_preferredCategory);
                _currentEventIndex = 0;
            }
            else if (_activeCategories.Count > 0)
            {
                // Auto-select first available category
                _currentCategoryIndex = 0;
                _currentEventIndex = 0;
            }
            else
            {
                _currentCategoryIndex = -1;
            }
        }

        private void ClampIndices()
        {
            if (_activeCategories.Count == 0)
            {
                _currentCategoryIndex = -1;
                _currentEventIndex = 0;
                return;
            }

            if (_currentCategoryIndex >= _activeCategories.Count)
                _currentCategoryIndex = _activeCategories.Count - 1;
            if (_currentCategoryIndex < 0)
                _currentCategoryIndex = 0;

            var currentList = GetCurrentEventList();
            if (currentList != null && _currentEventIndex >= currentList.Count)
            {
                _currentEventIndex = Math.Max(0, currentList.Count - 1);
            }
        }

        #region Scanning

        private void ScanNPCs(Vector3 playerPos)
        {
            try
            {
                if (_npcManager == null || _npcManager.m_NpcCtrlArray == null)
                    return;

                foreach (var npc in _npcManager.m_NpcCtrlArray)
                {
                    if (npc == null || npc.gameObject == null || !npc.gameObject.activeInHierarchy)
                        continue;

                    // Skip NPCs that are facilities (already in Facilities category)
                    if (_facilityNpcObjects.Contains(npc.gameObject))
                        continue;

                    string name = GetNpcName(npc);
                    float dist = Vector3.Distance(playerPos, npc.transform.position);

                    _events[EventCategory.NPCs].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = npc.transform.position,
                        Target = npc.gameObject,
                        Category = EventCategory.NPCs,
                        DistanceToPlayer = dist
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanNPCs error: {ex.Message}");
            }
        }

        private void ScanItems(Vector3 playerPos)
        {
            try
            {
                var itemManager = ItemPickPointManager.m_instance;
                if (itemManager == null || itemManager.m_itemPickPoints == null)
                    return;

                foreach (var point in itemManager.m_itemPickPoints)
                {
                    if (point == null || point.gameObject == null)
                        continue;

                    if (!point.enableItemPickPoint)
                        continue;

                    var category = GetItemCategory(point);
                    string name = GetItemName(point);
                    float dist = Vector3.Distance(playerPos, point.transform.position);

                    _events[category].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = point.transform.position,
                        Target = point.gameObject,
                        Category = category,
                        DistanceToPlayer = dist
                    });
                }

                DebugLogger.Log($"[NavList] ScanItems complete: {_events[EventCategory.Items].Count} items, {_events[EventCategory.Materials].Count} materials, {_events[EventCategory.Quest].Count} quest found");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanItems error: {ex.Message}");
            }
        }

        // Debug flag for verbose quest scanning logs - set to true for debugging
        private static bool _debugQuestScan = false;

        // Debug flag for verbose transition scanning logs - set to true for debugging
        private static bool _debugTransitionScan = false;


        private void ScanQuestItems(Vector3 playerPos)
        {
            try
            {
                // Debug: dump all ItemPickPoint objects on the map
                if (_debugQuestScan)
                {
                    try
                    {
                        var allPickPoints = Resources.FindObjectsOfTypeAll<ItemPickPointBase>();
                        DebugLogger.Log($"[QuestScan] DEBUG: {allPickPoints.Count} ItemPickPointBase objects in scene");
                        foreach (var pp in allPickPoints)
                        {
                            if (pp == null || pp.gameObject == null) continue;
                            string ppName = "?";
                            try { ppName = pp.gameObject.name; } catch { }
                            bool ppActive = false;
                            try { ppActive = pp.gameObject.activeInHierarchy; } catch { }
                            bool ppEnabled = false;
                            try { ppEnabled = pp.enableItemPickPoint; } catch { }
                            string itemName = "?";
                            try { itemName = GetItemName(pp); } catch { }
                            var cat = EventCategory.Items;
                            try { cat = GetItemCategory(pp); } catch { }
                            DebugLogger.Log($"[QuestScan] DEBUG ItemPickPoint: '{ppName}' active={ppActive} enabled={ppEnabled} item='{itemName}' category={cat}");
                        }
                    }
                    catch (Exception dex) { DebugLogger.Log($"[QuestScan] DEBUG ItemPickPoint error: {dex.Message}"); }
                }

                var eventTriggers = Resources.FindObjectsOfTypeAll<EventTriggerScript>();
                var placementLookup = BuildPlacementLookup();

                if (_debugQuestScan)
                    DebugLogger.Log($"[QuestScan] Starting scan: {eventTriggers.Count} total EventTriggerScripts, {placementLookup.Count} placements");

                foreach (var et in eventTriggers)
                {
                    if (et == null || et.gameObject == null || !et.gameObject.activeInHierarchy)
                        continue;

                    // Log every active trigger we encounter
                    string goName = "?";
                    try { goName = et.gameObject.name; } catch { }

                    // Skip disabled triggers - ActiveTalkEventTrigger disables the component
                    // when the quest isn't active, so this filters pre-start and post-complete triggers
                    bool enabled = false;
                    try { enabled = et.enabled; } catch { }
                    if (!enabled)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP disabled: {goName} paramId={et.m_EventParamId}");
                        continue;
                    }

                    // Skip NPC and enemy triggers - only want standalone triggers
                    if (et.gameObject.GetComponent<NpcCtrl>() != null)
                        continue;
                    if (et.gameObject.GetComponent<EnemyCtrl>() != null)
                        continue;

                    string cmdBlock = null;
                    ParameterPlacementNpc placement = null;
                    if (placementLookup.TryGetValue(et.m_EventParamId, out placement))
                        cmdBlock = placement.m_CmdBlock;

                    if (string.IsNullOrEmpty(cmdBlock))
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP no cmdBlock: {goName} paramId={et.m_EventParamId}");
                        continue;
                    }

                    // Skip facility triggers (already in Facilities category)
                    if (placement != null && (MainGameManager.Facility)placement.m_Facility != MainGameManager.Facility.None)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP facility: {goName} cmd={cmdBlock}");
                        continue;
                    }

                    // Skip known non-quest command block prefixes
                    if (cmdBlock.StartsWith("VENDOR_") || cmdBlock.StartsWith("EVENT."))
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP prefix: {goName} cmd={cmdBlock}");
                        continue;
                    }

                    // Skip non-quest enterID types (transitions, cameras, fishing, toilet)
                    bool skipEnterID = false;
                    try
                    {
                        var eid = et.enterID;
                        if (eid == MapTriggerManager.EVENT.MapChange ||
                            eid == MapTriggerManager.EVENT.TownCamera ||
                            eid == MapTriggerManager.EVENT.Fishing ||
                            eid == MapTriggerManager.EVENT.Toilet)
                            skipEnterID = true;
                    }
                    catch { }
                    if (skipEnterID)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP enterID: {goName} cmd={cmdBlock}");
                        continue;
                    }

                    // For non-PICK_ triggers, use et.enabled as quest acceptance gate.
                    // The game toggles EventTriggerScript.enabled via ActiveTalkEventTrigger
                    // when quests are accepted/completed. This works for both icon and no-icon triggers.
                    // PICK_ triggers already check enabled above, so this only applies to ST_SUB_ etc.
                    if (!cmdBlock.StartsWith("PICK_") && !enabled)
                    {
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP disabled non-PICK: {goName} cmd={cmdBlock}");
                        continue;
                    }

                    if (cmdBlock.StartsWith("PICK_"))
                    {
                        // PICK_ triggers: resolve item name and completion flag from CSVB
                        var questInfo = ResolveQuestItem(cmdBlock);
                        string name = questInfo.name ?? "Quest Pickup";
                        uint flagSetId = questInfo.flagSetId;

                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] PICK_ trigger: {goName} cmd={cmdBlock} name={name} flagSetId={flagSetId}");

                        if (flagSetId != 0 && IsQuestFlagSet(flagSetId))
                        {
                            if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP completed: {goName} cmd={cmdBlock} flag={flagSetId}");
                            continue;
                        }

                        // No acceptance check - show all uncompleted PICK_ items.
                        // The completion flag check above already filters finished quests.

                        float dist = Vector3.Distance(playerPos, et.transform.position);
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] ADDED PICK_: {goName} cmd={cmdBlock} name={name} dist={dist:F1}");
                        _events[EventCategory.Quest].Add(new NavigationEvent
                        {
                            Name = name,
                            Position = et.transform.position,
                            Target = et.gameObject,
                            Category = EventCategory.Quest,
                            DistanceToPlayer = dist,
                            FlagSetId = flagSetId
                        });
                    }
                    else
                    {
                        // Non-PICK_ quest event triggers (location-based quest objectives)
                        // Check completion via Form 5 flags in the command block's CSVB data
                        uint completionFlag = GetCompletionFlagForCmdBlock(cmdBlock);
                        if (completionFlag != 0 && IsQuestFlagSet(completionFlag))
                        {
                            if (_debugQuestScan) DebugLogger.Log($"[QuestScan] SKIP completed non-PICK: {goName} cmd={cmdBlock} flag={completionFlag}");
                            continue;
                        }

                        float dist = Vector3.Distance(playerPos, et.transform.position);
                        if (_debugQuestScan) DebugLogger.Log($"[QuestScan] ADDED non-PICK: {goName} cmd={cmdBlock} flag={completionFlag} dist={dist:F1}");
                        _events[EventCategory.Quest].Add(new NavigationEvent
                        {
                            Name = "Quest Event",
                            Position = et.transform.position,
                            Target = et.gameObject,
                            Category = EventCategory.Quest,
                            DistanceToPlayer = dist,
                            FlagSetId = completionFlag
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanQuestItems error: {ex.Message}");
            }
        }

        /// <summary>
        /// Extracts a completion flag from any command block's CSVB data by looking for Form 5 (SSetFlagSetData).
        /// Form 5 records set scenario progress flags when the event triggers. If the flag is already set,
        /// the quest event has been completed.
        /// </summary>
        private uint GetCompletionFlagForCmdBlock(string cmdBlock)
        {
            if (string.IsNullOrEmpty(cmdBlock))
                return 0;

            // Check cache (shared with PICK_ quest items)
            if (_questItemCache.TryGetValue(cmdBlock, out var cached))
                return cached.flagSetId;

            try
            {
                var ss = MainGameManager.m_instance?.m_SS;
                if (ss == null) return 0;

                var scriptBase = ss.Cast<CScenarioScriptBase>();
                if (scriptBase == null) return 0;

                var csvbInfoArr = scriptBase.m_CsvbInfo;
                if (csvbInfoArr == null) return 0;

                for (int i = 0; i < csvbInfoArr.Length; i++)
                {
                    var info = csvbInfoArr[i];
                    if (info?.m_BlockNames == null || info.m_Csvb == null) continue;
                    if (!info.m_BlockNames.ContainsKey(cmdBlock)) continue;

                    int blockIdx = info.m_BlockNames[cmdBlock];
                    var csvb = info.m_Csvb;
                    int numRecords = csvb.GetNumRecord(blockIdx);
                    var binary = csvb.m_CsvbBinary;
                    if (binary == null || numRecords == 0) continue;

                    for (int r = 0; r < numRecords; r++)
                    {
                        var accessInfo = new CCsvbVForm.SCsvbVRecAccessInfo();
                        if (!csvb.GetVRecAccessInfo(r, blockIdx, ref accessInfo)) continue;

                        uint dataPos = csvb.GetVRecDataPos(accessInfo);
                        int maxBytes = (int)((uint)binary.Length - dataPos);

                        // Form 5 = SSetFlagSetData: m_FlagSetId (offset 0), m_Val (offset 4)
                        if (accessInfo.m_formIdx == 5 && maxBytes >= 8)
                        {
                            uint flagId = ReadUInt32(binary, dataPos);
                            uint val = ReadUInt32(binary, dataPos + 4);
                            if (flagId != 0 && val == 1)
                            {
                                _questItemCache[cmdBlock] = (null, 0, flagId);
                                return flagId;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] GetCompletionFlag error for '{cmdBlock}': {ex.Message}");
            }

            // Cache miss - no flag found
            _questItemCache[cmdBlock] = (null, 0, 0);
            return 0;
        }

        private Dictionary<uint, ParameterPlacementNpc> BuildPlacementLookup()
        {
            var lookup = new Dictionary<uint, ParameterPlacementNpc>();
            try
            {
                var etMgr = MainGameManager.m_instance?.eventTriggerMgr;
                if (etMgr == null) return lookup;

                var placementData = etMgr.m_CsvbPlacementData;
                if (placementData == null) return lookup;

                var allPlacements = placementData.GetParams();
                foreach (var p in allPlacements)
                {
                    if (p != null)
                        lookup[p.id] = p;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] BuildPlacementLookup error: {ex.Message}");
            }
            return lookup;
        }

        /// <summary>
        /// Resolves quest item name and item ID from a PICK_ command block.
        /// Returns cached result if available, otherwise parses the scenario script CSVB data.
        /// </summary>
        private (string name, uint itemId, uint flagSetId) ResolveQuestItem(string cmdBlock)
        {
            if (string.IsNullOrEmpty(cmdBlock))
                return (null, 0, 0);

            if (_questItemCache.TryGetValue(cmdBlock, out var cached))
                return cached;

            var result = ParseQuestItemFromScenarioData(cmdBlock);
            _questItemCache[cmdBlock] = result;
            if (result.name != null)
                DebugLogger.Log($"[NavList] Resolved '{cmdBlock}' -> {result.name}, completionFlag=0x{result.flagSetId:X8}");
            return result;
        }

        /// <summary>
        /// Checks if a quest item's flag set has been triggered in saved scenario progress data.
        /// This is the game's persistent completion flag - survives save/reload.
        /// </summary>
        private static bool IsQuestFlagSet(uint flagSetId)
        {
            try
            {
                var progressData = StorageData.m_ScenarioProgressData;
                if (progressData == null) return false;
                return progressData.IsOnFlagSetAnd(flagSetId, true);
            }
            catch { return false; }
        }

        /// <summary>
        /// Reads the scenario script's CSVB data to find item IDs in PICK_ command blocks.
        /// Form 89 records are SStartPickItemCountData: m_ItemId (offset 0), m_Quota (offset 4), m_FlagSetId (offset 8).
        /// Falls back to scanning all records for valid item IDs if no form 89 record is found.
        /// </summary>
        private (string name, uint itemId, uint flagSetId) ParseQuestItemFromScenarioData(string cmdBlock)
        {
            try
            {
                var ss = MainGameManager.m_instance?.m_SS;
                if (ss == null) return (null, 0, 0);

                var scriptBase = ss.Cast<CScenarioScriptBase>();
                if (scriptBase == null) return (null, 0, 0);

                var csvbInfoArr = scriptBase.m_CsvbInfo;
                if (csvbInfoArr == null) return (null, 0, 0);

                for (int i = 0; i < csvbInfoArr.Length; i++)
                {
                    var info = csvbInfoArr[i];
                    if (info?.m_BlockNames == null || info.m_Csvb == null) continue;
                    if (!info.m_BlockNames.ContainsKey(cmdBlock)) continue;

                    int blockIdx = info.m_BlockNames[cmdBlock];
                    var csvb = info.m_Csvb;
                    int numRecords = csvb.GetNumRecord(blockIdx);
                    var binary = csvb.m_CsvbBinary;
                    if (binary == null || numRecords == 0) continue;

                    // Scan all records to find:
                    // - Form 89 (SStartPickItemCountData): item ID at offset 0
                    // - Form 5 (SSetFlagSetData): completion flag set ID at offset 0
                    //   This is the REAL scenario flag that persists in save data.
                    //   (Form 89's m_FlagSetId is only used by CQuestItemCounter, not by the flag system.)
                    uint foundItemId = 0;
                    uint completionFlagSetId = 0;
                    string foundName = null;
                    for (int r = 0; r < numRecords; r++)
                    {
                        var accessInfo = new CCsvbVForm.SCsvbVRecAccessInfo();
                        if (!csvb.GetVRecAccessInfo(r, blockIdx, ref accessInfo)) continue;

                        uint dataPos = csvb.GetVRecDataPos(accessInfo);
                        int maxBytes = (int)((uint)binary.Length - dataPos);

                        if (accessInfo.m_formIdx == 89 && maxBytes >= 12)
                        {
                            foundItemId = ReadUInt32(binary, dataPos);
                            try
                            {
                                var itemData = ParameterItemData.GetParam(foundItemId);
                                if (itemData != null)
                                    foundName = itemData.GetName();
                            }
                            catch { }
                        }
                        else if (accessInfo.m_formIdx == 5 && maxBytes >= 8)
                        {
                            // SSetFlagSetData: m_FlagSetId (offset 0), m_Val (offset 4)
                            uint flagId = ReadUInt32(binary, dataPos);
                            uint val = ReadUInt32(binary, dataPos + 4);
                            if (flagId != 0 && val == 1 && completionFlagSetId == 0)
                                completionFlagSetId = flagId;
                        }
                    }
                    if (foundName != null)
                        return (foundName, foundItemId, completionFlagSetId);

                    // Fallback: scan all records for any valid item ID
                    for (int r = 0; r < numRecords; r++)
                    {
                        var accessInfo = new CCsvbVForm.SCsvbVRecAccessInfo();
                        if (!csvb.GetVRecAccessInfo(r, blockIdx, ref accessInfo)) continue;

                        uint dataPos = csvb.GetVRecDataPos(accessInfo);
                        int scanBytes = System.Math.Min(32, (int)((uint)binary.Length - dataPos));

                        for (int off = 0; off + 4 <= scanBytes; off += 4)
                        {
                            uint candidateId = ReadUInt32(binary, dataPos + (uint)off);
                            if (candidateId == 0 || candidateId == 0xFFFFFFFF || candidateId < 0x1000)
                                continue;
                            try
                            {
                                var itemData = ParameterItemData.GetParam(candidateId);
                                if (itemData != null)
                                {
                                    string name = itemData.GetName();
                                    if (!string.IsNullOrEmpty(name))
                                        return (name, candidateId, 0);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScenarioData error: {ex.Message}");
            }
            return (null, 0, 0);
        }

        private static uint ReadUInt32(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> data, uint offset)
        {
            return (uint)(data[(int)offset]
                | (data[(int)offset + 1] << 8)
                | (data[(int)offset + 2] << 16)
                | (data[(int)offset + 3] << 24));
        }

        private void ScanTransitions(Vector3 playerPos)
        {
            try
            {
                var mapEnum = (AppInfo.MAP)_lastMapNo;

                // Use the game's own MapIconDataController for filtered icon list
                // This handles all flag checks (scenario flags, area arrival, etc.) identically to the minimap
                Il2CppSystem.Collections.Generic.List<ParameterDigiviceMapIconData> filteredIcons = null;
                try
                {
                    var flagMgr = DigiviceMapFlagManager.Ref;
                    if (flagMgr != null)
                    {
                        var iconCtrl = flagMgr.m_MapIconDataController;
                        if (iconCtrl != null)
                        {
                            iconCtrl.UpdateEnableIconList(mapEnum, (uint)_lastAreaNo);
                            filteredIcons = iconCtrl.GetMapIconDataList();
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_debugTransitionScan)
                        DebugLogger.Log($"[TransitionScan] MapIconDataController failed: {ex.Message}");
                }

                // Fallback: if MapIconDataController unavailable, use raw icon data
                Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ParameterDigiviceMapIconData> rawIcons = null;
                if (filteredIcons == null || filteredIcons.Count == 0)
                {
                    rawIcons = ParameterDigiviceMapIconData.GetParams(mapEnum);
                }

                bool hasAnyIcons = (filteredIcons != null && filteredIcons.Count > 0) || (rawIcons != null && rawIcons.Length > 0);

                if (!hasAnyIcons)
                {
                    if (_lastMapNo == (int)AppInfo.MAP.TOWN)
                    {
                        DebugLogger.Log($"[NavList] ScanTransitions: no icon data for map {_lastMapNo}, using town fallback");
                        ScanTransitionsFallback(playerPos);
                    }
                    else
                    {
                        DebugLogger.Log($"[NavList] ScanTransitions: no icon data for map {_lastMapNo}, using field fallback");
                        ScanTransitionsFieldFallback(playerPos);
                    }
                    return;
                }

                // Collect valid destinations from icon data (MAP_BORDER + AREA_BORDER)
                var validAreaDests = new System.Collections.Generic.HashSet<int>();
                var validMapDests = new System.Collections.Generic.HashSet<int>();

                // Process filtered icons from MapIconDataController (already flag-checked by the game)
                if (filteredIcons != null && filteredIcons.Count > 0)
                {
                    foreach (var icon in filteredIcons)
                    {
                        if (icon == null) continue;
                        var kind = (ParameterDigiviceMapIconData.MarkKind)icon.m_iconKind;
                        if (kind != ParameterDigiviceMapIconData.MarkKind.MAP_BORDER &&
                            kind != ParameterDigiviceMapIconData.MarkKind.AREA_BORDER)
                            continue;

                        if (icon.m_belongAreaID != _lastAreaNo)
                            continue;

                        if (_debugTransitionScan)
                            DebugLogger.Log($"[TransitionScan] filtered icon {kind} -> map {icon.m_adjacentMapID} area {icon.m_adjacentAreaID}");

                        if (kind == ParameterDigiviceMapIconData.MarkKind.AREA_BORDER)
                            validAreaDests.Add(icon.m_adjacentAreaID);
                        else
                            validMapDests.Add(icon.m_adjacentMapID);
                    }
                }
                else
                {
                    // Fallback: manual flag check on raw icons
                    foreach (var icon in rawIcons)
                    {
                        if (icon == null) continue;
                        var kind = (ParameterDigiviceMapIconData.MarkKind)icon.m_iconKind;
                        if (kind != ParameterDigiviceMapIconData.MarkKind.MAP_BORDER &&
                            kind != ParameterDigiviceMapIconData.MarkKind.AREA_BORDER)
                            continue;

                        if (icon.m_belongAreaID != _lastAreaNo)
                            continue;

                        if (icon.m_startScenarioFlag != 0)
                        {
                            try
                            {
                                if (!StorageData.m_ScenarioProgressData.IsOnFlagSetAnd(icon.m_startScenarioFlag, true))
                                    continue;
                            }
                            catch { }
                        }

                        if (kind == ParameterDigiviceMapIconData.MarkKind.AREA_BORDER)
                            validAreaDests.Add(icon.m_adjacentAreaID);
                        else
                            validMapDests.Add(icon.m_adjacentMapID);
                    }
                }

                // Supplement with ParameterDigiviceAreaData adjacency (catches tunnels etc.)
                try
                {
                    var areaData = ParameterDigiviceAreaData.GetParam(mapEnum, _lastAreaNo);
                    if (areaData != null)
                    {
                        int[] adjacentAreas = { areaData.m_up, areaData.m_down, areaData.m_left, areaData.m_right };
                        foreach (int adjArea in adjacentAreas)
                        {
                            if (adjArea > 0 && adjArea != _lastAreaNo && !validAreaDests.Contains(adjArea))
                            {
                                validAreaDests.Add(adjArea);
                                if (_debugTransitionScan)
                                    DebugLogger.Log($"[TransitionScan] AreaData adjacency added area {adjArea}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_debugTransitionScan)
                        DebugLogger.Log($"[TransitionScan] AreaData lookup failed: {ex.Message}");
                }

                if (_debugTransitionScan)
                    DebugLogger.Log($"[TransitionScan] Valid destinations: areas=[{string.Join(",", validAreaDests)}] maps=[{string.Join(",", validMapDests)}]");

                if (validAreaDests.Count == 0 && validMapDests.Count == 0)
                {
                    if (_lastMapNo != (int)AppInfo.MAP.TOWN)
                    {
                        DebugLogger.Log($"[NavList] ScanTransitions: no valid icon transitions for map {_lastMapNo} area {_lastAreaNo}, using field fallback");
                        ScanTransitionsFieldFallback(playerPos);
                    }
                    else
                    {
                        DebugLogger.Log($"[NavList] ScanTransitions: no valid transitions for map {_lastMapNo} area {_lastAreaNo}");
                    }
                    return;
                }

                // Match AreaChangeInfo objects to valid destinations
                var usedMapDests = new System.Collections.Generic.HashSet<int>();
                var usedAreaDests = new System.Collections.Generic.HashSet<int>();
                var areaChangeInfos = UnityEngine.Object.FindObjectsOfType<AreaChangeInfo>();

                foreach (var aci in areaChangeInfos)
                {
                    if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                        continue;

                    var dest = aci.m_Destination;
                    if (dest == null) continue;

                    if (_knownTargets.Contains(aci.gameObject))
                        continue;

                    // Skip transitions back to current area
                    if (dest.m_MapNo == _lastMapNo && dest.m_AreaNo == _lastAreaNo)
                        continue;

                    bool valid = false;
                    if (dest.m_MapNo == _lastMapNo)
                    {
                        if (validAreaDests.Contains(dest.m_AreaNo) && !usedAreaDests.Contains(dest.m_AreaNo))
                        {
                            valid = true;
                            usedAreaDests.Add(dest.m_AreaNo);
                        }
                    }
                    else
                    {
                        if (validMapDests.Contains(dest.m_MapNo) && !usedMapDests.Contains(dest.m_MapNo))
                        {
                            valid = true;
                            usedMapDests.Add(dest.m_MapNo);
                        }
                    }

                    if (!valid) continue;

                    string name = GetTransitionNameFromAreaChange(aci);
                    float dist = Vector3.Distance(playerPos, aci.transform.position);

                    _events[EventCategory.Transitions].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = aci.transform.position,
                        Target = aci.gameObject,
                        Category = EventCategory.Transitions,
                        DistanceToPlayer = dist
                    });
                    _knownTargets.Add(aci.gameObject);
                    DebugLogger.Log($"[NavList] ScanTransitions: added '{name}' -> map {dest.m_MapNo} area {dest.m_AreaNo}");
                }

                DebugLogger.Log($"[NavList] ScanTransitions: {_events[EventCategory.Transitions].Count} transitions (map {_lastMapNo} area {_lastAreaNo})");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanTransitions error: {ex.Message}");
            }
        }

        /// <summary>
        /// Town transition scanning using the fast travel system (ParameterTownJumpData).
        /// Each unlocked jump destination is matched to the nearest AreaChangeInfo for walk-to position.
        /// </summary>
        private void ScanTransitionsFallback(Vector3 playerPos)
        {
            try
            {
                var paramMgr = AppMainScript.parameterManager;
                if (paramMgr == null)
                {
                    DebugLogger.Log("[NavList] ScanTransitions town: ParameterManager not available");
                    return;
                }

                var townJumpCsvb = paramMgr.townJumpData;
                if (townJumpCsvb == null)
                {
                    DebugLogger.Log("[NavList] ScanTransitions town: townJumpData is null");
                    return;
                }

                var allJumps = townJumpCsvb.GetParams();
                if (allJumps == null || allJumps.Length == 0)
                {
                    DebugLogger.Log("[NavList] ScanTransitions town: no town jump entries");
                    return;
                }

                // Build destination lookup by iterating all destination entries
                var destCsvb = paramMgr.townJumpDestinationData;
                var destLookup = new System.Collections.Generic.Dictionary<uint, ParameterTownJumpDestinationData>();
                if (destCsvb != null)
                {
                    try
                    {
                        var allDests = destCsvb.GetParams();
                        if (allDests != null)
                        {
                            foreach (var d in allDests)
                            {
                                if (d != null)
                                    destLookup[d.m_id] = d;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[NavList] ScanTransitions town: failed to build dest lookup: {ex.Message}");
                    }
                }

                // Build lookup from active ACIs in scene (town loads only current area's ACIs)
                var allAcis = UnityEngine.Object.FindObjectsOfType<AreaChangeInfo>();
                var aciByDest = new System.Collections.Generic.Dictionary<(int mapNo, int areaNo), System.Collections.Generic.List<AreaChangeInfo>>();
                foreach (var aci in allAcis)
                {
                    if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                        continue;
                    var dest = aci.m_Destination;
                    if (dest == null) continue;
                    var key = (dest.m_MapNo, dest.m_AreaNo);
                    if (!aciByDest.ContainsKey(key))
                        aciByDest[key] = new System.Collections.Generic.List<AreaChangeInfo>();
                    aciByDest[key].Add(aci);
                }

                int added = 0;
                var addedAreas = new System.Collections.Generic.HashSet<(int, int)>();

                foreach (var jump in allJumps)
                {
                    if (jump == null) continue;

                    try
                    {
                        if (!jump.IsOnFlagSet())
                            continue;
                    }
                    catch { continue; }

                    // Resolve destination: try base destination first, fall back to grade destination
                    // The game switches ACIs between destinations as town progresses,
                    // so we match whichever destination has an actual ACI in the scene
                    ParameterTownJumpDestinationData destData = null;
                    if (destLookup.TryGetValue(jump.m_destination_id, out var baseData))
                        destData = baseData;

                    ParameterTownJumpDestinationData gradeDestData = null;
                    uint gradeDestId = jump.m_grade_destination_id;
                    if (gradeDestId != 0 && destLookup.TryGetValue(gradeDestId, out var gData))
                        gradeDestData = gData;

                    if (destData == null && gradeDestData == null)
                        continue;

                    // Try to find an ACI for the base destination
                    var baseKey = destData != null ? ((int)destData.m_mapNo, (int)destData.m_areaNo) : (-1, -1);
                    var gradeKey = gradeDestData != null ? ((int)gradeDestData.m_mapNo, (int)gradeDestData.m_areaNo) : (-1, -1);

                    AreaChangeInfo bestAci = null;
                    float bestDist = float.MaxValue;
                    (int, int) matchedKey = baseKey;

                    // First try grade destination (the upgraded one takes priority if it exists in scene)
                    if (gradeDestData != null && aciByDest.ContainsKey(gradeKey))
                    {
                        foreach (var aci in aciByDest[gradeKey])
                        {
                            if (_knownTargets.Contains(aci.gameObject)) continue;
                            float d = Vector3.Distance(playerPos, aci.transform.position);
                            if (d < bestDist) { bestDist = d; bestAci = aci; }
                        }
                        if (bestAci != null) matchedKey = gradeKey;
                    }

                    // If no grade ACI found, try base destination
                    if (bestAci == null && destData != null && aciByDest.ContainsKey(baseKey))
                    {
                        foreach (var aci in aciByDest[baseKey])
                        {
                            if (_knownTargets.Contains(aci.gameObject)) continue;
                            float d = Vector3.Distance(playerPos, aci.transform.position);
                            if (d < bestDist) { bestDist = d; bestAci = aci; }
                        }
                        if (bestAci != null) matchedKey = baseKey;
                    }

                    if (bestAci == null)
                        continue;

                    int destMap = matchedKey.Item1;
                    int destArea = matchedKey.Item2;

                    // Skip self-transitions
                    if (destMap == _lastMapNo && destArea == _lastAreaNo)
                        continue;

                    // Deduplicate by destination area
                    var areaKey = (destMap, destArea);
                    if (addedAreas.Contains(areaKey))
                        continue;

                    // Get proper area name from the matched ACI's actual destination
                    string name = null;
                    try
                    {
                        name = ParameterAreaName.GetAreaName((AppInfo.MAP)destMap, (uint)destArea);
                    }
                    catch { }
                    if (string.IsNullOrEmpty(name) || name.Contains("not found"))
                    {
                        try { name = jump.GetName(); } catch { }
                    }
                    if (string.IsNullOrEmpty(name) || name.Contains("not found"))
                        name = $"Map {destMap} Area {destArea}";

                    float dist = Vector3.Distance(playerPos, bestAci.transform.position);
                    _events[EventCategory.Transitions].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = bestAci.transform.position,
                        Target = bestAci.gameObject,
                        Category = EventCategory.Transitions,
                        DistanceToPlayer = dist
                    });
                    _knownTargets.Add(bestAci.gameObject);
                    addedAreas.Add(areaKey);
                    added++;
                }

                DebugLogger.Log($"[NavList] ScanTransitions town: {added} transitions (map {_lastMapNo} area {_lastAreaNo})");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanTransitions town error: {ex.Message}");
            }
        }

        /// <summary>
        /// Field fallback for dimensions/dungeons and other areas without minimap icon data.
        /// Scans all active AreaChangeInfo objects and adds non-self transitions.
        /// </summary>
        private void ScanTransitionsFieldFallback(Vector3 playerPos)
        {
            try
            {
                var areaChangeInfos = UnityEngine.Object.FindObjectsOfType<AreaChangeInfo>();
                var addedAreas = new System.Collections.Generic.HashSet<(int, int)>();
                int added = 0;

                foreach (var aci in areaChangeInfos)
                {
                    if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                        continue;

                    var dest = aci.m_Destination;
                    if (dest == null) continue;

                    if (_knownTargets.Contains(aci.gameObject))
                        continue;

                    // Skip transitions back to current area
                    if (dest.m_MapNo == _lastMapNo && dest.m_AreaNo == _lastAreaNo)
                        continue;

                    // Deduplicate by destination
                    var areaKey = (dest.m_MapNo, dest.m_AreaNo);
                    if (addedAreas.Contains(areaKey))
                        continue;

                    string name = GetTransitionNameFromAreaChange(aci);
                    float dist = Vector3.Distance(playerPos, aci.transform.position);

                    _events[EventCategory.Transitions].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = aci.transform.position,
                        Target = aci.gameObject,
                        Category = EventCategory.Transitions,
                        DistanceToPlayer = dist
                    });
                    _knownTargets.Add(aci.gameObject);
                    addedAreas.Add(areaKey);
                    added++;
                }

                DebugLogger.Log($"[NavList] ScanTransitions field fallback: {added} transitions (map {_lastMapNo} area {_lastAreaNo})");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanTransitions field fallback error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get transition name from AreaChangeInfo destination data.
        /// </summary>
        private string GetTransitionNameFromAreaChange(AreaChangeInfo aci)
        {
            try
            {
                var dest = aci.m_Destination;
                if (dest != null)
                {
                    try
                    {
                        string areaName = ParameterAreaName.GetAreaName((AppInfo.MAP)dest.m_MapNo, (uint)dest.m_AreaNo);
                        if (!string.IsNullOrEmpty(areaName) && !areaName.Contains("not found"))
                            return areaName;
                    }
                    catch { }

                    try
                    {
                        string mapName = ParameterMapName.GetMapName((AppInfo.MAP)dest.m_MapNo);
                        if (!string.IsNullOrEmpty(mapName) && !mapName.Contains("not found"))
                            return mapName;
                    }
                    catch { }

                    return $"Area {dest.m_MapNo}-{dest.m_AreaNo}";
                }
            }
            catch { }

            try
            {
                string objName = aci.gameObject.name;
                if (!string.IsNullOrEmpty(objName))
                    return $"Transition ({objName})";
            }
            catch { }

            return "Unknown Transition";
        }

        private void ScanEnemies(Vector3 playerPos)
        {
            try
            {
                if (_enemyManager == null || _enemyManager.m_EnemyCtrlArray == null)
                    return;

                foreach (var enemy in _enemyManager.m_EnemyCtrlArray)
                {
                    if (!GameStateService.IsEnemyAlive(enemy))
                        continue;

                    string name = GetEnemyName(enemy);
                    float dist = Vector3.Distance(playerPos, enemy.transform.position);

                    _events[EventCategory.Enemies].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = enemy.transform.position,
                        Target = enemy.gameObject,
                        Category = EventCategory.Enemies,
                        DistanceToPlayer = dist
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanEnemies error: {ex.Message}");
            }
        }

        private void ScanFacilities(Vector3 playerPos)
        {
            var mgm = MainGameManager.m_instance;
            if (mgm == null) return;

            var facilityObjects = new HashSet<GameObject>();

            // Strategy 1: EventTriggerManager dictionary + CSV data (field areas like Training Hall)
            try
            {
                var eventTriggerMgr = mgm.m_EventTriggerMgr;
                if (eventTriggerMgr != null)
                {
                    var triggerDict = eventTriggerMgr.m_TriggerDictionary;
                    var placementData = eventTriggerMgr.m_CsvbPlacementData;

                    if (triggerDict != null && placementData != null)
                    {
                        var enumerator = triggerDict.GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            var trigger = enumerator.Current.Value;
                            if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                                continue;

                            try
                            {
                                uint placementId = enumerator.Current.Key;
                                var npcData = HashIdSearchClass<ParameterPlacementNpc>.GetParam(placementData, placementId);
                                if (npcData == null) continue;

                                var facilityType = (MainGameManager.Facility)npcData.m_Facility;
                                if (facilityType == MainGameManager.Facility.None) continue;

                                string name = ResolveFacilityName(facilityType, npcData);
                                float dist = Vector3.Distance(playerPos, trigger.transform.position);
                                _events[EventCategory.Facilities].Add(new NavigationEvent
                                {
                                    Name = name, Position = trigger.transform.position,
                                    Target = trigger.gameObject, Category = EventCategory.Facilities,
                                    DistanceToPlayer = dist
                                });
                                facilityObjects.Add(trigger.gameObject);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] FacilityScan dict error: {ex.Message}");
            }

            // Strategy 2: NpcManager placement data (town areas where facilities are NPC objects)
            // In town, facilities like Item Storage are NPC placements with m_Facility != None
            try
            {
                if (_npcManager != null)
                {
                    var placementList = _npcManager.m_placementNpcList;
                    if (placementList != null)
                    {
                        int placementCount = placementList.Count;
                        for (int i = 0; i < placementCount; i++)
                        {
                            try
                            {
                                var placement = placementList[i];
                                if (placement == null) continue;

                                var facilityType = (MainGameManager.Facility)placement.m_Facility;
                                if (facilityType == MainGameManager.Facility.None) continue;

                                uint placementId = placement.id;

                                // Get the NpcCtrl for this facility placement
                                NpcCtrl npcCtrl = null;
                                try { npcCtrl = _npcManager._GetNpcCtrlFromPlacementId(placementId); }
                                catch { }

                                GameObject targetObj = null;
                                Vector3 pos;

                                if (npcCtrl != null && npcCtrl.gameObject != null && npcCtrl.gameObject.activeInHierarchy)
                                {
                                    targetObj = npcCtrl.gameObject;
                                    pos = npcCtrl.transform.position;
                                }
                                else
                                {
                                    // Use position from placement data as fallback
                                    pos = new Vector3(placement.m_Px, placement.m_Py, placement.m_Pz);
                                }

                                if (targetObj != null && facilityObjects.Contains(targetObj))
                                    continue;

                                string name = ResolveFacilityName(facilityType, placement);
                                float dist = Vector3.Distance(playerPos, pos);
                                _events[EventCategory.Facilities].Add(new NavigationEvent
                                {
                                    Name = name, Position = pos,
                                    Target = targetObj, Category = EventCategory.Facilities,
                                    DistanceToPlayer = dist
                                });

                                if (targetObj != null)
                                {
                                    facilityObjects.Add(targetObj);
                                    _facilityNpcObjects.Add(targetObj);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] FacilityScan NpcMgr error: {ex.Message}");
            }

            // Strategy 3: MapTriggerScript scan for fishing spots and toilets (works on all maps)
            try
            {
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
                foreach (var trigger in mapTriggers)
                {
                    if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                        continue;
                    if (facilityObjects.Contains(trigger.gameObject))
                        continue;

                    string name = null;
                    if (trigger.enterID == MapTriggerManager.EVENT.Fishing)
                        name = "Fishing Spot";
                    else if (trigger.enterID == MapTriggerManager.EVENT.Toilet)
                        name = "Toilet";

                    if (name == null) continue;

                    float dist = Vector3.Distance(playerPos, trigger.transform.position);
                    _events[EventCategory.Facilities].Add(new NavigationEvent
                    {
                        Name = name, Position = trigger.transform.position,
                        Target = trigger.gameObject, Category = EventCategory.Facilities,
                        DistanceToPlayer = dist
                    });
                    facilityObjects.Add(trigger.gameObject);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] FacilityScan MapTrigger error: {ex.Message}");
            }
        }

        #endregion

        #region Name Resolution

        private string GetNpcName(NpcCtrl npc)
        {
            string npcId = null;
            try { npcId = npc.m_npcId; } catch { }

            if (!string.IsNullOrEmpty(npcId))
            {
                // Use FindBaseIdToModelName to map model name -> Digimon ID directly
                try
                {
                    uint digimonId = ParameterDigimonData.FindBaseIdToModelName(npcId);
                    if (digimonId != 0)
                    {
                        var paramData = ParameterDigimonData.GetParam(digimonId);
                        if (paramData != null)
                        {
                            string name = paramData.GetDefaultName();
                            if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                return name;
                        }
                    }
                }
                catch { }

                // Fallback: iterate parameterManager.digimonData matching by m_mdlName
                try
                {
                    var digimonData = AppMainScript.parameterManager?.digimonData;
                    if (digimonData != null)
                    {
                        int count = digimonData.GetRecordMax();
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                var paramData = digimonData.GetParams(i);
                                if (paramData != null && paramData.m_mdlName == npcId)
                                {
                                    string name = paramData.GetDefaultName();
                                    if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                        return name;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // Fallback: use NpcCtrl.unitParamId to look up the Digimon name directly.
                // Some town NPCs (e.g. "C007" = Palmon) use IDs that don't match any model name
                // in ParameterDigimonData, but their unitParamId maps to the correct Digimon entry.
                try
                {
                    uint unitParamId = npc.unitParamId;
                    if (unitParamId != 0)
                    {
                        var paramData = ParameterDigimonData.GetParam(unitParamId);
                        if (paramData != null)
                        {
                            string name = paramData.GetDefaultName();
                            if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                return name;
                        }
                    }
                }
                catch { }

                // Fallback: try _GetNpcObjectName which may return the display name
                try
                {
                    if (npc._GetNpcObjectName(out string objName) && !string.IsNullOrEmpty(objName))
                    {
                        // Try to resolve the object name as a model name
                        uint digimonId2 = ParameterDigimonData.FindBaseIdToModelName(objName);
                        if (digimonId2 != 0)
                        {
                            var paramData = ParameterDigimonData.GetParam(digimonId2);
                            if (paramData != null)
                            {
                                string name = paramData.GetDefaultName();
                                if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                    return name;
                            }
                        }
                    }
                }
                catch { }

                // Fallback: try gameObject.name
                try
                {
                    string goName = npc.gameObject.name;
                    if (!string.IsNullOrEmpty(goName) && goName != npcId)
                        return goName;
                }
                catch { }

                return npcId;
            }

            try { return npc.gameObject.name ?? "Unknown NPC"; } catch { }
            return "Unknown NPC";
        }

        private EventCategory GetItemCategory(ItemPickPointBase point)
        {
            if (point.isMaterial)
                return EventCategory.Materials;

            try
            {
                uint itemId = point.itemId;
                if (itemId != 0)
                {
                    var itemData = ParameterItemData.GetParam(itemId);
                    if (itemData != null && itemData.IsKindKeyItem())
                        return EventCategory.Quest;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] GetItemCategory error: {ex.Message}");
            }

            return EventCategory.Items;
        }

        private string GetItemName(ItemPickPointBase point)
        {
            try
            {
                uint itemId = point.itemId;
                if (itemId != 0)
                {
                    var itemData = ParameterItemData.GetParam(itemId);
                    if (itemData != null)
                    {
                        string name = itemData.GetName();
                        if (!string.IsNullOrEmpty(name))
                            return name;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] GetItemName error: {ex.Message}");
            }

            return point.isMaterial ? "Unknown Material" : "Unknown Item";
        }


        private string GetEnemyName(EnemyCtrl enemy)
        {
            int level = 0;
            try
            {
                var placementData = enemy.m_placementData;
                if (placementData != null)
                    level = placementData.m_level;
            }
            catch { }

            // Primary: EnemyCtrl.gameData.m_commonData.m_name (actual localized name)
            try
            {
                var commonData = enemy.gameData?.m_commonData;
                if (commonData != null)
                {
                    string name = commonData.m_name;
                    if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                        return level > 0 ? $"{name} Lv.{level}" : name;
                }
            }
            catch { }

            // Fallback: placement chain -> ParameterDigimonData (EvolutionHandler pattern)
            try
            {
                var placementData = enemy.m_placementData;
                if (placementData != null)
                {
                    uint paramId = placementData.m_paramId;
                    if (paramId != 0)
                    {
                        var mgm = MainGameManager.m_instance;
                        Csvb<ParameterNpcEnemyData> npcEnemyCsvb = null;
                        try { npcEnemyCsvb = mgm?.m_EnemyMgr?.m_NpcEnemyData; } catch { }
                        if (npcEnemyCsvb == null)
                            try { npcEnemyCsvb = mgm?.m_NpcEnemyData; } catch { }

                        if (npcEnemyCsvb != null)
                        {
                            var npcEnemyData = HashIdSearchClass<ParameterNpcEnemyData>.GetParam(npcEnemyCsvb, paramId);
                            if (npcEnemyData != null && npcEnemyData.m_DigiParamId != 0)
                            {
                                var staticData = ParameterDigimonData.GetParam(npcEnemyData.m_DigiParamId);
                                if (staticData != null)
                                {
                                    string mdlName = staticData.m_mdlName;
                                    var digiCsvb = AppMainScript.parameterManager?.digimonData;
                                    if (digiCsvb != null)
                                    {
                                        int count = digiCsvb.GetRecordMax();
                                        for (int i = 0; i < count; i++)
                                        {
                                            try
                                            {
                                                var paramData = digiCsvb.GetParams(i);
                                                if (paramData != null && paramData.m_mdlName == mdlName)
                                                {
                                                    string name = paramData.GetDefaultName();
                                                    if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                                        return level > 0 ? $"{name} Lv.{level}" : name;
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try { return enemy.gameObject.name ?? "Unknown Enemy"; } catch { }
            return "Unknown Enemy";
        }

        private string GetFacilityName(MainGameManager.Facility facilityType)
        {
            // Derive name from game's own enum, split CamelCase into words
            string raw = facilityType.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Resolve a facility name trying multiple game data paths.
        /// For non-CommonSelect, uses the clear enum name.
        /// For CommonSelect, tries NpcEnemyParam, model name, then falls back.
        /// </summary>
        private string ResolveFacilityName(MainGameManager.Facility facilityType, ParameterPlacementNpc placementData)
        {
            // For non-CommonSelect facilities, the enum name is already clear
            if (facilityType != MainGameManager.Facility.CommonSelect)
                return GetFacilityName(facilityType);

            // For CommonSelect: try every path to get a meaningful name
            string cmdBlock = "";
            try { cmdBlock = placementData.m_CmdBlock ?? ""; } catch { }

            // Path 1: NpcEnemyParamId → ParameterNpcEnemyData → ParameterDigimonData → name
            try
            {
                uint npcEnemyId = placementData.m_NpcEnemyParamId;
                if (npcEnemyId != 0)
                {
                    var mgm = MainGameManager.m_instance;
                    Csvb<ParameterNpcEnemyData> npcEnemyCsvb = null;
                    try { npcEnemyCsvb = mgm?.m_EnemyMgr?.m_NpcEnemyData; } catch { }
                    if (npcEnemyCsvb == null)
                        try { npcEnemyCsvb = mgm?.m_NpcEnemyData; } catch { }

                    if (npcEnemyCsvb != null)
                    {
                        var npcEnemyData = HashIdSearchClass<ParameterNpcEnemyData>.GetParam(npcEnemyCsvb, npcEnemyId);
                        if (npcEnemyData != null && npcEnemyData.m_DigiParamId != 0)
                        {
                            var digiData = ParameterDigimonData.GetParam(npcEnemyData.m_DigiParamId);
                            if (digiData != null)
                            {
                                string name = digiData.GetDefaultName();
                                if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                    return name;
                            }
                        }
                    }
                }
            }
            catch { }

            // Path 2: Model name → ParameterDigimonData → name
            try
            {
                string mdlName = placementData.m_MdlName;
                if (!string.IsNullOrEmpty(mdlName))
                {
                    uint digimonId = ParameterDigimonData.FindBaseIdToModelName(mdlName);
                    if (digimonId != 0)
                    {
                        var paramData = ParameterDigimonData.GetParam(digimonId);
                        if (paramData != null)
                        {
                            string name = paramData.GetDefaultName();
                            if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                return name;
                        }
                    }

                    // Model name didn't resolve to Digimon - try iterating digimonData
                    var digiCsvb = AppMainScript.parameterManager?.digimonData;
                    if (digiCsvb != null)
                    {
                        int count = digiCsvb.GetRecordMax();
                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                var pd = digiCsvb.GetParams(i);
                                if (pd != null && pd.m_mdlName == mdlName)
                                {
                                    string name = pd.GetDefaultName();
                                    if (!string.IsNullOrEmpty(name) && !name.Contains("ランゲージ"))
                                        return name;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // Path 3: Parse cmdBlock prefix (e.g., "VENDOR_TALK00" → "Vendor")
            if (!string.IsNullOrEmpty(cmdBlock))
            {
                int talkIdx = cmdBlock.IndexOf("_TALK");
                if (talkIdx > 0)
                {
                    string prefix = cmdBlock.Substring(0, talkIdx);
                    // Format: uppercase first letter, lowercase rest
                    if (prefix.Length > 0)
                    {
                        string formatted = char.ToUpper(prefix[0]) + prefix.Substring(1).ToLower();
                        return formatted;
                    }
                }
            }

            return GetFacilityName(facilityType);
        }


        #endregion

        #region Input Handling

        private void HandleInput()
        {
            if (!_listBuilt) return;

            // Next category (O key default)
            if (ModInputManager.IsActionTriggered("NavNextCategory"))
            {
                CycleCategory(1);
            }

            // Previous category (I key default)
            if (ModInputManager.IsActionTriggered("NavPrevCategory"))
            {
                CycleCategory(-1);
            }

            // Previous event (J key default)
            if (ModInputManager.IsActionTriggered("NavPrevEvent"))
            {
                CycleEvent(-1);
            }

            // Current event (K key default)
            if (ModInputManager.IsActionTriggered("NavCurrentEvent"))
            {
                AnnounceCurrentEvent();
            }

            // Next event (L key default)
            if (ModInputManager.IsActionTriggered("NavNextEvent"))
            {
                CycleEvent(1);
            }

            // Navigate to event (P key default)
            if (ModInputManager.IsActionTriggered("NavToEvent"))
            {
                AnnouncePathToEvent();
            }

            // Toggle auto-walk (Shift+P default)
            if (ModInputManager.IsActionTriggered("ToggleAutoWalk"))
            {
                _autoWalkEnabled = !_autoWalkEnabled;
                ScreenReader.Say(_autoWalkEnabled ? "Auto walk enabled" : "Auto walk disabled");
                DebugLogger.Log($"[NavList] Auto-walk toggled: {_autoWalkEnabled}");
            }
        }

        private void CycleCategory(int direction)
        {
            if (_activeCategories.Count == 0)
            {
                ScreenReader.Say("No points of interest in this area");
                return;
            }

            if (_currentCategoryIndex < 0)
            {
                _currentCategoryIndex = direction > 0 ? 0 : _activeCategories.Count - 1;
            }
            else
            {
                _currentCategoryIndex += direction;
                if (_currentCategoryIndex >= _activeCategories.Count)
                    _currentCategoryIndex = 0;
                else if (_currentCategoryIndex < 0)
                    _currentCategoryIndex = _activeCategories.Count - 1;
            }

            _currentEventIndex = 0;

            var category = _activeCategories[_currentCategoryIndex];
            _preferredCategory = category; // Remember for map changes
            string categoryName = GetCategoryDisplayName(category);
            int count = _events[category].Count;
            int catPos = _currentCategoryIndex + 1;
            int catTotal = _activeCategories.Count;

            // Announce category then first item details in one speech
            var firstEvent = _events[category][0];
            int dist = 0;
            string cardinal = "";
            if (_playerCtrl != null && firstEvent.Target != null)
            {
                var targetPos = firstEvent.Target.transform.position;
                firstEvent.DistanceToPlayer = Vector3.Distance(
                    _playerCtrl.transform.position, targetPos);
                dist = Mathf.RoundToInt(firstEvent.DistanceToPlayer);
                Vector3 diff = targetPos - _playerCtrl.transform.position;
                cardinal = GetCardinalDirection(diff) + ", ";
            }

            ScreenReader.Say($"{categoryName}, {count} {(count == 1 ? "entry" : "entries")}, {catPos} of {catTotal}. {firstEvent.Name}, {cardinal}{dist} meters, 1 of {count}");
        }

        private void CycleEvent(int direction)
        {
            var currentList = GetCurrentEventList();
            if (currentList == null || currentList.Count == 0)
            {
                if (_activeCategories.Count == 0)
                    ScreenReader.Say("No points of interest in this area");
                else
                    ScreenReader.Say("No category selected. Press O or I to select a category");
                return;
            }

            _currentEventIndex += direction;
            if (_currentEventIndex >= currentList.Count)
                _currentEventIndex = 0;
            else if (_currentEventIndex < 0)
                _currentEventIndex = currentList.Count - 1;

            AnnounceEvent(currentList[_currentEventIndex]);
        }

        private void AnnounceCurrentEvent()
        {
            var currentList = GetCurrentEventList();
            if (currentList == null || currentList.Count == 0)
            {
                if (_activeCategories.Count == 0)
                    ScreenReader.Say("No points of interest in this area");
                else if (_currentCategoryIndex < 0)
                    ScreenReader.Say("No category selected. Press O or I to select a category");
                else
                    ScreenReader.Say("Category is empty");
                return;
            }

            if (_currentEventIndex >= 0 && _currentEventIndex < currentList.Count)
            {
                AnnounceEvent(currentList[_currentEventIndex]);
            }
        }

        private void AnnounceEvent(NavigationEvent navEvent)
        {
            if (navEvent == null) return;

            // Update distance and position
            Vector3 targetPos = navEvent.Position;
            if (_playerCtrl != null && navEvent.Target != null)
            {
                targetPos = navEvent.Target.transform.position;
                navEvent.DistanceToPlayer = Vector3.Distance(
                    _playerCtrl.transform.position, targetPos);
            }

            int dist = Mathf.RoundToInt(navEvent.DistanceToPlayer);
            var currentList = GetCurrentEventList();
            int index = _currentEventIndex + 1;
            int total = currentList != null ? currentList.Count : 0;

            // Use cardinal direction (fixed camera: +Z = north, +X = east)
            string direction = "";
            if (_playerCtrl != null)
            {
                Vector3 diff = targetPos - _playerCtrl.transform.position;
                direction = GetCardinalDirection(diff) + ", ";
            }

            ScreenReader.Say($"{navEvent.Name}, {direction}{dist} meters, {index} of {total}");
        }

        #region Pitfall Detection and Avoidance

        /// <summary>
        /// Scan for pitfall triggers - EventTriggerScripts with PIT_ command blocks.
        /// Stores their positions so pathfinding can route around them.
        /// </summary>
        private void ScanPitfalls()
        {
            try
            {
                var eventTriggers = Resources.FindObjectsOfTypeAll<EventTriggerScript>();
                var placementLookup = BuildPlacementLookup();

                if (eventTriggers == null || placementLookup.Count == 0) return;

                foreach (var et in eventTriggers)
                {
                    if (et == null || et.gameObject == null || !et.gameObject.activeInHierarchy)
                        continue;

                    bool enabled = false;
                    try { enabled = et.enabled; } catch { }
                    if (!enabled) continue;

                    uint paramId = 0;
                    try { paramId = et.m_EventParamId; } catch { continue; }

                    string cmdBlock = null;
                    ParameterPlacementNpc placement = null;
                    if (placementLookup.TryGetValue(paramId, out placement))
                    {
                        try { cmdBlock = placement.m_CmdBlock; } catch { }
                    }

                    if (string.IsNullOrEmpty(cmdBlock) || !cmdBlock.StartsWith("PIT"))
                        continue;

                    var pos = et.transform.position;
                    _pitfallPositions.Add(pos);
                    DebugLogger.Log($"[Pitfall] Found pitfall at ({pos.x:F1}, {pos.y:F1}, {pos.z:F1}) [{cmdBlock}]");
                }

                if (_pitfallPositions.Count > 0)
                    DebugLogger.Log($"[Pitfall] Total: {_pitfallPositions.Count} pitfalls on map {_lastMapNo}/{_lastAreaNo}");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[Pitfall] ScanPitfalls error: {ex.Message}");
            }
        }

        /// <summary>
        /// Adjusts a NavMesh path to route around known pitfall positions.
        /// For each path segment that passes near a pitfall, inserts a detour waypoint.
        /// </summary>
        private Vector3[] AdjustPathAroundPitfalls(Vector3[] corners)
        {
            if (_pitfallPositions.Count == 0 || corners == null || corners.Length < 2)
                return corners;

            var adjusted = new List<Vector3>();
            adjusted.Add(corners[0]);

            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 segStart = corners[i];
                Vector3 segEnd = corners[i + 1];

                // Check each pitfall against this segment (2D, ignoring Y)
                foreach (var pit in _pitfallPositions)
                {
                    float dist = DistancePointToSegment2D(pit, segStart, segEnd);
                    if (dist < PitfallAvoidanceRadius)
                    {
                        // Path passes too close to pitfall - insert a detour waypoint
                        Vector3 detour = CalculateDetourPoint(segStart, segEnd, pit);

                        // Verify the detour point is on the NavMesh
                        NavMeshHit hit;
                        if (NavMesh.SamplePosition(detour, out hit, 5f, NavMesh.AllAreas))
                        {
                            adjusted.Add(hit.position);
                            DebugLogger.Log($"[Pitfall] Detour around ({pit.x:F1}, {pit.z:F1}): waypoint at ({hit.position.x:F1}, {hit.position.z:F1})");
                        }
                    }
                }

                adjusted.Add(segEnd);
            }

            return adjusted.ToArray();
        }

        /// <summary>
        /// Calculate the shortest distance from a point to a line segment in 2D (XZ plane).
        /// </summary>
        private float DistancePointToSegment2D(Vector3 point, Vector3 segA, Vector3 segB)
        {
            float dx = segB.x - segA.x;
            float dz = segB.z - segA.z;
            float lenSq = dx * dx + dz * dz;

            if (lenSq < 0.001f)
                return Mathf.Sqrt((point.x - segA.x) * (point.x - segA.x) + (point.z - segA.z) * (point.z - segA.z));

            float t = ((point.x - segA.x) * dx + (point.z - segA.z) * dz) / lenSq;
            t = Mathf.Clamp01(t);

            float closestX = segA.x + t * dx;
            float closestZ = segA.z + t * dz;

            float distX = point.x - closestX;
            float distZ = point.z - closestZ;
            return Mathf.Sqrt(distX * distX + distZ * distZ);
        }

        /// <summary>
        /// Calculate a detour waypoint that routes around a pitfall.
        /// Goes perpendicular to the segment direction, choosing the side
        /// that keeps the path shorter overall.
        /// </summary>
        private Vector3 CalculateDetourPoint(Vector3 segStart, Vector3 segEnd, Vector3 pitfall)
        {
            // Segment direction (2D)
            float segDx = segEnd.x - segStart.x;
            float segDz = segEnd.z - segStart.z;
            float segLen = Mathf.Sqrt(segDx * segDx + segDz * segDz);

            if (segLen < 0.001f)
            {
                // Degenerate segment - just offset from pitfall
                return new Vector3(pitfall.x + PitfallAvoidanceRadius, pitfall.y, pitfall.z);
            }

            // Normalized segment direction
            float ndx = segDx / segLen;
            float ndz = segDz / segLen;

            // Two perpendicular directions
            float perpX1 = -ndz;
            float perpZ1 = ndx;

            // Detour point candidates: offset from pitfall center by avoidance radius
            Vector3 detour1 = new Vector3(
                pitfall.x + perpX1 * (PitfallAvoidanceRadius + 2f),
                pitfall.y,
                pitfall.z + perpZ1 * (PitfallAvoidanceRadius + 2f));
            Vector3 detour2 = new Vector3(
                pitfall.x - perpX1 * (PitfallAvoidanceRadius + 2f),
                pitfall.y,
                pitfall.z - perpZ1 * (PitfallAvoidanceRadius + 2f));

            // Pick the one that gives a shorter total path
            float totalDist1 = Vector3.Distance(segStart, detour1) + Vector3.Distance(detour1, segEnd);
            float totalDist2 = Vector3.Distance(segStart, detour2) + Vector3.Distance(detour2, segEnd);

            return totalDist1 <= totalDist2 ? detour1 : detour2;
        }

        #endregion

        private void AnnouncePathToEvent()
        {
            // Toggle off if already pathfinding
            if (_isPathfinding)
            {
                StopPathfinding("Pathfinding stopped");
                return;
            }

            var currentList = GetCurrentEventList();
            if (currentList == null || currentList.Count == 0 || _currentEventIndex < 0)
            {
                ScreenReader.Say("No event selected");
                return;
            }

            var navEvent = currentList[_currentEventIndex];
            if (navEvent.Target == null)
            {
                ScreenReader.Say("Target no longer exists");
                return;
            }

            if (_playerCtrl == null) return;

            Vector3 playerPos = _playerCtrl.transform.position;
            Vector3 targetPos = navEvent.Target != null && navEvent.Target.activeInHierarchy
                ? navEvent.Target.transform.position
                : navEvent.Position;

            try
            {
                NavMeshHit targetHit;
                Vector3 navTargetPos = targetPos;
                if (NavMesh.SamplePosition(targetPos, out targetHit, 20f, NavMesh.AllAreas))
                {
                    navTargetPos = targetHit.position;
                }

                NavMeshPath path = new NavMeshPath();
                bool found = NavMesh.CalculatePath(playerPos, navTargetPos, NavMesh.AllAreas, path);

                if (!found || path.corners.Length < 2)
                {
                    ScreenReader.Say($"Cannot find path to {navEvent.Name}");
                    return;
                }

                // Extract path corners and adjust around pitfalls
                var corners = AdjustPathAroundPitfalls(path.corners);
                float[] cx = new float[corners.Length];
                float[] cy = new float[corners.Length];
                float[] cz = new float[corners.Length];
                for (int i = 0; i < corners.Length; i++)
                {
                    cx[i] = corners[i].x;
                    cy[i] = corners[i].y;
                    cz[i] = corners[i].z;
                }

                // Start beacon
                if (_pathfindingBeacon == null)
                    _pathfindingBeacon = new PathfindingBeacon();

                _pathfindingBeacon.UpdatePlayerPosition(
                    playerPos.x, playerPos.y, playerPos.z,
                    _playerCtrl.transform.forward.x, _playerCtrl.transform.forward.z);
                _pathfindingBeacon.Start(navTargetPos.x, navTargetPos.y, navTargetPos.z, cx, cy, cz);

                _pathfindingDestination = navTargetPos;
                _pathfindingRawDestination = targetPos; // Actual position before NavMesh sampling
                _pathfindingTarget = navEvent.Target;
                _pathfindingCategory = navEvent.Category;
                _lastPathRecalcTime = Time.time;
                _isPathfinding = true;

                // Start auto-walk if enabled, using the same NavMesh path corners
                _isAutoWalking = false;
                if (_autoWalkEnabled)
                {
                    StartAutoWalk(corners);
                }

                // Suspend other navigation audio
                AudioNavigationHandler.Suspended = true;

                string modeText = _isAutoWalking ? "Auto walking" : "Pathfinding";
                ScreenReader.Say($"{modeText} to {navEvent.Name}");
                DebugLogger.Log($"[NavList] {modeText} started to {navEvent.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] AnnouncePathToEvent error: {ex.Message}");
                ScreenReader.Say($"Pathfinding error");
            }
        }

        /// <summary>
        /// Per-frame update for active pathfinding: updates beacon position,
        /// recalculates path periodically, checks arrival and target validity.
        /// If the beacon was paused (e.g. battle), resumes it automatically.
        /// </summary>
        private void UpdatePathfinding()
        {
            if (_playerCtrl == null || _pathfindingBeacon == null)
            {
                // For transitions, losing playerCtrl during map load means we arrived
                string msg = _pathfindingCategory == EventCategory.Transitions
                    ? "Destination reached" : null;
                StopPathfinding(msg);
                return;
            }

            try
            {
                // Check if target was destroyed (null = truly gone).
                // Don't check activeInHierarchy for most categories: the game deactivates
                // distant objects for performance, but we can still pathfind to their cached position.
                // For transitions, target destroyed = map unloaded = we arrived.
                // Exception: enemies are deactivated when defeated, so stop pathfinding to them.
                if (_pathfindingTarget == null)
                {
                    string msg = _pathfindingCategory == EventCategory.Transitions
                        ? "Destination reached" : "Target lost";
                    StopPathfinding(msg);
                    return;
                }

                if (_pathfindingCategory == EventCategory.Enemies && !_pathfindingTarget.activeInHierarchy)
                {
                    StopPathfinding("Target defeated");
                    return;
                }

                // If beacon was paused (not active), resume it
                if (!_pathfindingBeacon.IsActive)
                {
                    ResumePathfinding();
                    return;
                }

                Vector3 playerPos = _playerCtrl.transform.position;
                Vector3 playerForward = _playerCtrl.transform.forward;

                // Update beacon with current player position every frame
                _pathfindingBeacon.UpdatePlayerPosition(
                    playerPos.x, playerPos.y, playerPos.z,
                    playerForward.x, playerForward.z);

                // Distance-based arrival for NPCs, Enemies, Facilities, and Quest triggers.
                // Transitions need to walk into the zone trigger (map change stops pathfinding).
                // Items need close proximity to trigger the pickup prompt.
                // Both rely on stuck detection below instead.
                if (_pathfindingCategory != EventCategory.Transitions
                    && _pathfindingCategory != EventCategory.Items)
                {
                    float distToTarget = Vector3.Distance(playerPos, _pathfindingRawDestination);
                    bool atPathEnd = _isAutoWalking && _autoWalkCorners != null && _autoWalkCorners.Length > 0
                        && Vector3.Distance(playerPos, _autoWalkCorners[_autoWalkCorners.Length - 1]) < FinalApproachDistance;
                    if (atPathEnd || distToTarget < FinalApproachDistance)
                    {
                        StopPathfinding("Destination reached");
                        return;
                    }
                }

                // Stuck detection: player is auto-walking but not moving (hit a collider/wall).
                // Primary arrival method for transitions and facilities.
                if (_isAutoWalking)
                {
                    float timeSinceCheck = Time.time - _autoWalkCheckTime;
                    if (timeSinceCheck >= StuckCheckInterval)
                    {
                        float movedDist = Vector3.Distance(playerPos, _autoWalkCheckPosition);
                        if (movedDist < StuckMovementThreshold)
                        {
                            float distToTarget = Vector3.Distance(playerPos, _pathfindingRawDestination);
                            DebugLogger.Log($"[NavList] Stuck detected, dist to target: {distToTarget:F1}");

                            // Near destination: we've arrived (trigger zone / close enough)
                            if (distToTarget < StuckArrivalDistance)
                            {
                                StopPathfinding("Destination reached");
                                return;
                            }

                            // Far from destination: blocked mid-path by enemy/obstacle.
                            // Start walking sideways to get around it.
                            StartObstacleAvoidance(playerPos);
                            return;
                        }
                        // Player is moving - clear avoidance state
                        _isAvoidingObstacle = false;
                        _autoWalkCheckPosition = playerPos;
                        _autoWalkCheckTime = Time.time;
                    }
                }

                // Move player along path if auto-walking
                if (_isAutoWalking)
                {
                    UpdateAutoWalk();
                }

                // Recalculate path periodically
                float currentTime = Time.time;
                if (currentTime - _lastPathRecalcTime >= PathRecalcInterval)
                {
                    _lastPathRecalcTime = currentTime;
                    RecalculatePathfindingPath(playerPos);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] UpdatePathfinding error: {ex.Message}");
                StopPathfinding(null);
            }
        }

        /// <summary>
        /// Resume pathfinding after it was paused (e.g. returning from battle or menu).
        /// Recalculates the path and restarts the beacon.
        /// </summary>
        private void ResumePathfinding()
        {
            try
            {
                if (_playerCtrl == null || _pathfindingTarget == null)
                {
                    StopPathfinding(null);
                    return;
                }

                Vector3 playerPos = _playerCtrl.transform.position;

                // Update destination from live target position if active
                if (_pathfindingTarget.activeInHierarchy)
                {
                    Vector3 livePos = _pathfindingTarget.transform.position;
                    _pathfindingRawDestination = livePos;
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(livePos, out hit, 20f, NavMesh.AllAreas))
                        _pathfindingDestination = hit.position;
                }
                // Otherwise keep using cached _pathfindingDestination

                // Calculate fresh path
                NavMeshPath path = new NavMeshPath();
                bool found = NavMesh.CalculatePath(playerPos, _pathfindingDestination, NavMesh.AllAreas, path);

                if (!found || path.corners.Length < 2)
                {
                    StopPathfinding("Path lost");
                    return;
                }

                var corners = AdjustPathAroundPitfalls(path.corners);
                float[] cx = new float[corners.Length];
                float[] cy = new float[corners.Length];
                float[] cz = new float[corners.Length];
                for (int i = 0; i < corners.Length; i++)
                {
                    cx[i] = corners[i].x;
                    cy[i] = corners[i].y;
                    cz[i] = corners[i].z;
                }

                // Restart beacon
                _pathfindingBeacon.UpdatePlayerPosition(
                    playerPos.x, playerPos.y, playerPos.z,
                    _playerCtrl.transform.forward.x, _playerCtrl.transform.forward.z);
                _pathfindingBeacon.Start(
                    _pathfindingDestination.x, _pathfindingDestination.y, _pathfindingDestination.z,
                    cx, cy, cz);

                // Resume auto-walk if it was active
                if (_isAutoWalking)
                {
                    UpdateAutoWalkPath(corners);
                    _isAvoidingObstacle = false;
                    _avoidanceAttempt = 0;
                    DebugLogger.Log("[NavList] Auto-walk resumed");
                }
                else
                {
                    // Reset stuck detection for non-auto-walk resume too
                    _autoWalkCheckPosition = playerPos;
                    _autoWalkCheckTime = Time.time;
                }

                // Ensure other audio stays suspended
                AudioNavigationHandler.Suspended = true;
                _lastPathRecalcTime = Time.time;

                DebugLogger.Log("[NavList] Pathfinding resumed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ResumePathfinding error: {ex.Message}");
                StopPathfinding(null);
            }
        }

        /// <summary>
        /// Recalculate the NavMesh path and update the beacon with new corners.
        /// </summary>
        private void RecalculatePathfindingPath(Vector3 playerPos)
        {
            try
            {
                // Update destination from live target position if available
                if (_pathfindingTarget != null && _pathfindingTarget.activeInHierarchy)
                {
                    Vector3 livePos = _pathfindingTarget.transform.position;
                    _pathfindingRawDestination = livePos;
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(livePos, out hit, 20f, NavMesh.AllAreas))
                        _pathfindingDestination = hit.position;
                }

                NavMeshPath path = new NavMeshPath();
                bool found = NavMesh.CalculatePath(playerPos, _pathfindingDestination, NavMesh.AllAreas, path);

                if (found && path.corners.Length >= 2)
                {
                    var corners = AdjustPathAroundPitfalls(path.corners);

                    float[] cx = new float[corners.Length];
                    float[] cy = new float[corners.Length];
                    float[] cz = new float[corners.Length];
                    for (int i = 0; i < corners.Length; i++)
                    {
                        cx[i] = corners[i].x;
                        cy[i] = corners[i].y;
                        cz[i] = corners[i].z;
                    }
                    _pathfindingBeacon.UpdatePath(cx, cy, cz);

                    // Update auto-walk path corners
                    if (_isAutoWalking)
                    {
                        UpdateAutoWalkPath(corners);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] RecalculatePathfindingPath error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop pathfinding, clean up beacon, and resume normal audio navigation.
        /// </summary>
        private void StopPathfinding(string announcement)
        {
            var category = _pathfindingCategory;
            var rawDest = _pathfindingRawDestination;

            _isPathfinding = false;
            _pathfindingTarget = null;

            StopAutoWalk();

            if (_pathfindingBeacon != null)
            {
                _pathfindingBeacon.Stop();
            }

            AudioNavigationHandler.Suspended = false;

            // Turn player to face the target so the correct one gets interacted with
            if ((category == EventCategory.NPCs || category == EventCategory.Facilities) && _playerCtrl != null)
            {
                try
                {
                    Vector3 playerPos = _playerCtrl.transform.position;
                    Vector3 lookDir = rawDest - playerPos;
                    lookDir.y = 0;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        _playerCtrl.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[NavList] Face target error: {ex.Message}");
                }
            }

            if (!string.IsNullOrEmpty(announcement))
            {
                ScreenReader.Say(announcement);
            }

            DebugLogger.Log($"[NavList] Pathfinding stopped: {announcement ?? "silent"}");
        }

        /// <summary>
        /// Start auto-walking the player to the given NavMesh destination
        /// using the game's built-in NavMeshAgent pathfinding.
        /// </summary>
        private void StartAutoWalk(Vector3[] corners)
        {
            if (_playerCtrl == null || corners == null || corners.Length < 2)
                return;

            _autoWalkCorners = corners;
            // Start from corner 1 (corner 0 is player's current position)
            _autoWalkCornerIndex = 1;
            _isAutoWalking = true;

            // Initialize stuck detection and avoidance
            _autoWalkCheckPosition = _playerCtrl.transform.position;
            _autoWalkCheckTime = Time.time;
            _isAvoidingObstacle = false;
            _avoidanceAttempt = 0;

            DebugLogger.Log($"[NavList] Auto-walk started, {corners.Length} corners");
        }

        /// <summary>
        /// Stop auto-walking the player.
        /// </summary>
        private void StopAutoWalk()
        {
            if (!_isAutoWalking) return;

            _isAutoWalking = false;
            _autoWalkCorners = null;
            _isAvoidingObstacle = false;
            AutoWalkActive = false;
            AutoWalkStickX = 0;
            AutoWalkStickY = 0;
            AutoWalkCameraStickX = 0;
            DebugLogger.Log("[NavList] Auto-walk stopped");
        }

        /// <summary>
        /// When stuck mid-path (blocked by enemy/obstacle), find a walkable detour point
        /// on the NavMesh to the side or behind the player, verify a path exists from there
        /// to the destination, and walk to it. Alternates left/right on each attempt.
        /// </summary>
        private void StartObstacleAvoidance(Vector3 playerPos)
        {
            // Direction we were heading toward
            Vector3 targetDir;
            if (_autoWalkCorners != null && _autoWalkCornerIndex < _autoWalkCorners.Length)
                targetDir = _autoWalkCorners[_autoWalkCornerIndex] - playerPos;
            else
                targetDir = _pathfindingRawDestination - playerPos;
            targetDir.y = 0;
            if (targetDir.sqrMagnitude < 0.001f)
            {
                // Can't determine heading, just reset timer and retry
                _autoWalkCheckPosition = playerPos;
                _autoWalkCheckTime = Time.time;
                return;
            }
            targetDir.Normalize();

            // Perpendicular direction (right of heading)
            Vector3 rightDir = new Vector3(targetDir.z, 0, -targetDir.x);

            _avoidanceAttempt++;

            // Build candidate offsets: alternate starting left/right, include backward options
            // Try multiple distances to find a walkable point the player can reach
            Vector3[] directions;
            if (_avoidanceAttempt % 2 == 1)
                directions = new[] { rightDir, -rightDir, (-targetDir + rightDir).normalized, (-targetDir - rightDir).normalized, -targetDir };
            else
                directions = new[] { -rightDir, rightDir, (-targetDir - rightDir).normalized, (-targetDir + rightDir).normalized, -targetDir };

            float[] distances = { 5f, 8f, 12f };

            NavMeshHit hit;
            foreach (var dir in directions)
            {
                foreach (var dist in distances)
                {
                    Vector3 candidate = playerPos + dir * dist;
                    if (NavMesh.SamplePosition(candidate, out hit, 3f, NavMesh.AllAreas))
                    {
                        // Verify a path exists from the detour point to our destination
                        NavMeshPath testPath = new NavMeshPath();
                        if (NavMesh.CalculatePath(hit.position, _pathfindingDestination, NavMesh.AllAreas, testPath)
                            && testPath.corners.Length >= 2)
                        {
                            _avoidanceDetourTarget = hit.position;
                            _isAvoidingObstacle = true;
                            _avoidanceStartTime = Time.time;
                            _autoWalkCheckPosition = playerPos;
                            _autoWalkCheckTime = Time.time;
                            DebugLogger.Log($"[NavList] Avoiding obstacle, detour {Vector3.Distance(playerPos, hit.position):F1}m away, attempt {_avoidanceAttempt}");
                            return;
                        }
                    }
                }
            }

            // No walkable detour found - just reset stuck timer and keep trying
            // (enemy might move on its own)
            _autoWalkCheckPosition = playerPos;
            _autoWalkCheckTime = Time.time;
            DebugLogger.Log($"[NavList] No walkable detour found, retrying, attempt {_avoidanceAttempt}");
        }

        /// <summary>
        /// Calculate virtual stick input to move the player along the NavMesh path.
        /// The actual movement is handled by the game's normal player movement system
        /// via GamepadInputPatch injecting our stick values.
        /// </summary>
        private void UpdateAutoWalk()
        {
            if (!_isAutoWalking || _playerCtrl == null || _autoWalkCorners == null)
            {
                AutoWalkActive = false;
                return;
            }

            try
            {
                Vector3 currentPos = _playerCtrl.transform.position;
                Vector3 target;

                // Obstacle avoidance: walk to detour point instead of normal path
                if (_isAvoidingObstacle)
                {
                    float distToDetour = Vector3.Distance(currentPos, _avoidanceDetourTarget);
                    if (distToDetour < CornerReachDistance || Time.time - _avoidanceStartTime > AvoidanceTimeout)
                    {
                        // Reached detour point or timed out - resume normal pathfinding
                        _isAvoidingObstacle = false;
                        _autoWalkCheckPosition = currentPos;
                        _autoWalkCheckTime = Time.time;
                        RecalculatePathfindingPath(currentPos);
                        DebugLogger.Log("[NavList] Avoidance complete, resuming path");
                        return;
                    }
                    target = _avoidanceDetourTarget;
                }
                else if (_autoWalkCornerIndex >= _autoWalkCorners.Length)
                {
                    // All corners reached - walk toward actual target position
                    // to enter interaction trigger volume
                    target = _pathfindingRawDestination;
                }
                else
                {
                    target = _autoWalkCorners[_autoWalkCornerIndex];

                    // Skip past all corners that are within reach (not just one per frame)
                    float distToCorner = Vector3.Distance(currentPos, target);
                    while (distToCorner < CornerReachDistance)
                    {
                        _autoWalkCornerIndex++;
                        if (_autoWalkCornerIndex >= _autoWalkCorners.Length)
                        {
                            // All corners reached - walk toward actual target
                            target = _pathfindingRawDestination;
                            break;
                        }
                        target = _autoWalkCorners[_autoWalkCornerIndex];
                        distToCorner = Vector3.Distance(currentPos, target);
                    }
                }

                // Calculate direction to next corner (horizontal only)
                Vector3 direction = target - currentPos;
                direction.y = 0;

                if (direction.sqrMagnitude > 0.001f)
                {
                    direction.Normalize();

                    // Convert world direction to camera-relative stick values.
                    // The game applies camera transform to stick input internally,
                    // so we must project our world direction onto camera axes.
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 camForward = cam.transform.forward;
                        Vector3 camRight = cam.transform.right;
                        camForward.y = 0;
                        camRight.y = 0;
                        camForward.Normalize();
                        camRight.Normalize();

                        AutoWalkStickX = Vector3.Dot(direction, camRight);
                        // Negative because SDL convention: stick up (forward) = negative Y
                        AutoWalkStickY = -Vector3.Dot(direction, camForward);

                        // Rotate camera to follow walking direction via right stick.
                        // Negated cross product: positive when direction is to the right of camera.
                        float cross = -(camForward.x * direction.z - camForward.z * direction.x);
                        // Scale: full stick when camera is 90+ degrees off, gentle when nearly aligned
                        AutoWalkCameraStickX = Mathf.Clamp(cross * 2f, -1f, 1f);
                        // Dead zone: don't fight small angle differences
                        if (Mathf.Abs(AutoWalkCameraStickX) < 0.05f)
                            AutoWalkCameraStickX = 0;
                    }
                    else
                    {
                        // Fallback: assume camera looks at -Z
                        AutoWalkStickX = direction.x;
                        AutoWalkStickY = direction.z;
                        AutoWalkCameraStickX = 0;
                    }
                    AutoWalkActive = true;
                }
                else
                {
                    AutoWalkActive = false;
                    AutoWalkCameraStickX = 0;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] UpdateAutoWalk error: {ex.Message}");
                AutoWalkActive = false;
                StopAutoWalk();
            }
        }

        /// <summary>
        /// Update the auto-walk path corners when the path is recalculated.
        /// Finds the nearest upcoming corner to continue from.
        /// </summary>
        private void UpdateAutoWalkPath(Vector3[] newCorners)
        {
            if (!_isAutoWalking || newCorners == null || newCorners.Length < 2 || _playerCtrl == null)
                return;

            Vector3 playerPos = _playerCtrl.transform.position;

            // Find which path segment the player is closest to, then target the END of that segment.
            // This prevents picking a corner behind the player (which causes backward walking/oscillation).
            // We project the player position onto each segment [i, i+1] and pick the closest one.
            int bestNextIndex = 1;
            float bestSegDist = float.MaxValue;

            for (int i = 0; i < newCorners.Length - 1; i++)
            {
                Vector3 segStart = newCorners[i];
                Vector3 segEnd = newCorners[i + 1];
                Vector3 seg = segEnd - segStart;
                float segLenSq = seg.sqrMagnitude;

                float distToSeg;
                if (segLenSq < 0.001f)
                {
                    distToSeg = Vector3.Distance(playerPos, segStart);
                }
                else
                {
                    float t = Mathf.Clamp01(Vector3.Dot(playerPos - segStart, seg) / segLenSq);
                    Vector3 closestPoint = segStart + seg * t;
                    distToSeg = Vector3.Distance(playerPos, closestPoint);
                }

                if (distToSeg < bestSegDist)
                {
                    bestSegDist = distToSeg;
                    bestNextIndex = i + 1; // Target the end of this segment (the next corner ahead)
                }
            }

            // Skip past corners within reach distance
            while (bestNextIndex < newCorners.Length - 1
                && Vector3.Distance(playerPos, newCorners[bestNextIndex]) < CornerReachDistance)
            {
                bestNextIndex++;
            }

            _autoWalkCorners = newCorners;
            _autoWalkCornerIndex = bestNextIndex;

            DebugLogger.Log($"[NavList] Auto-walk path updated: {newCorners.Length} corners, starting at {bestNextIndex}, nearest dist={Vector3.Distance(playerPos, newCorners[bestNextIndex]):F1}");
        }

        /// <summary>
        /// Get compass direction from a world-space offset vector.
        /// Fixed camera looks toward -Z, so -Z = north on screen, +X = east.
        /// </summary>
        private string GetCardinalDirection(Vector3 offset)
        {
            float angle = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            if (angle >= 337.5f || angle < 22.5f)
                return "north";
            else if (angle >= 22.5f && angle < 67.5f)
                return "northeast";
            else if (angle >= 67.5f && angle < 112.5f)
                return "east";
            else if (angle >= 112.5f && angle < 157.5f)
                return "southeast";
            else if (angle >= 157.5f && angle < 202.5f)
                return "south";
            else if (angle >= 202.5f && angle < 247.5f)
                return "southwest";
            else if (angle >= 247.5f && angle < 292.5f)
                return "west";
            else
                return "northwest";
        }

        #endregion

        #region Helpers

        private EventCategory? GetCurrentCategory()
        {
            if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _activeCategories.Count)
                return _activeCategories[_currentCategoryIndex];
            return null;
        }

        private List<NavigationEvent> GetCurrentEventList()
        {
            var category = GetCurrentCategory();
            if (category.HasValue && _events.ContainsKey(category.Value))
                return _events[category.Value];
            return null;
        }

        /// <summary>
        /// Get the currently selected NavigationEvent, or null if none.
        /// </summary>
        private NavigationEvent GetSelectedEvent()
        {
            var list = GetCurrentEventList();
            if (list != null && _currentEventIndex >= 0 && _currentEventIndex < list.Count)
                return list[_currentEventIndex];
            return null;
        }

        /// <summary>
        /// After sorting, restore _currentEventIndex to point to the same item by Target identity.
        /// Falls back to clamping if the item was removed.
        /// </summary>
        private void RestoreSelectionAfterSort(NavigationEvent previouslySelected)
        {
            if (previouslySelected == null) return;

            var list = GetCurrentEventList();
            if (list == null) return;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Target == previouslySelected.Target)
                {
                    _currentEventIndex = i;
                    return;
                }
            }
            // Item was removed from list; clamp index
            if (_currentEventIndex >= list.Count)
                _currentEventIndex = Math.Max(0, list.Count - 1);
        }

        private string GetCategoryDisplayName(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.NPCs: return "NPCs";
                case EventCategory.Items: return "Items";
                case EventCategory.Materials: return "Materials";
                case EventCategory.Quest: return "Quest";
                case EventCategory.Transitions: return "Transitions";
                case EventCategory.Enemies: return "Enemies";
                case EventCategory.Facilities: return "Facilities";
                default: return category.ToString();
            }
        }

        #endregion
    }
}
