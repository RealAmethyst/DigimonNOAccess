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
            KeyItems,
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
            EventCategory.KeyItems,
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
        private float _leftFieldTime = 0f;

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
                if (_wasInField)
                    _leftFieldTime = currentTime;
                _wasInField = false;

                // Pause pathfinding beacon audio when leaving field (auto-walk pauses naturally since Update stops)
                if (_isPathfinding && _pathfindingBeacon != null && _pathfindingBeacon.IsActive)
                    _pathfindingBeacon.Stop();

                return;
            }

            // Rescan after returning to field from evolution/battles (>2s away)
            // Don't wipe existing lists - just do an additive rescan that removes
            // destroyed/picked-up objects and adds any new ones. This prevents losing
            // all items when briefly leaving the field (e.g. item pickup dialog).
            if (!_wasInField && _listBuilt && (currentTime - _leftFieldTime > 2f))
            {
                // Check if we just won a battle - remove defeated enemy immediately
                // instead of waiting for the game to deactivate it via _UpdateEnemyActiveSetting
                var defeatedObj = GameStateService.GetLastDefeatedEnemyObject();
                if (defeatedObj != null && _events.ContainsKey(EventCategory.Enemies))
                {
                    _events[EventCategory.Enemies].RemoveAll(e => e.Target == defeatedObj);
                    DebugLogger.Log("[NavList] Removed defeated enemy from list (battle win)");

                    if (_isPathfinding && _pathfindingTarget == defeatedObj)
                        StopPathfinding("Target defeated");
                }

                _isRescanning = true;
                _mapChangeTime = currentTime;
                _nextRescanTime = currentTime + InitialScanDelay;
                DebugLogger.Log("[NavList] Returned to field after extended absence, rescanning (preserving lists)");
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
                        $"{_events[EventCategory.KeyItems].Count} key items, " +
                        $"{_events[EventCategory.Transitions].Count} transitions, " +
                        $"{_events[EventCategory.Enemies].Count} enemies, " +
                        $"{_events[EventCategory.Facilities].Count} facilities");
                }
            }

            // Periodically refresh to catch item pickups and enemy changes
            if (_listBuilt && !_isRescanning && currentTime - _lastScanTime >= ScanInterval)
            {
                RefreshLists();
                _lastScanTime = currentTime;
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
                    _events[EventCategory.KeyItems] = new List<NavigationEvent>();
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

                // Strategy 3: FishingTrigger
                var fishingTriggers = UnityEngine.Object.FindObjectsOfType<FishingTrigger>();
                if (fishingTriggers != null)
                {
                    foreach (var ft in fishingTriggers)
                    {
                        if (ft == null || ft.gameObject == null || !ft.gameObject.activeInHierarchy) continue;
                        if (_knownTargets.Contains(ft.gameObject)) continue;

                        float dist = Vector3.Distance(playerPos, ft.transform.position);
                        _events[EventCategory.Facilities].Add(new NavigationEvent
                        {
                            Name = "Fishing Spot", Position = ft.transform.position,
                            Target = ft.gameObject, Category = EventCategory.Facilities,
                            DistanceToPlayer = dist
                        });
                        _knownTargets.Add(ft.gameObject);
                        newCount++;
                    }
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

            // Scan for new transitions using AreaChangeParent hierarchy
            try
            {
                string sourcePrefix = GetAreaChangePrefix();
                var parents = UnityEngine.Object.FindObjectsOfType<AreaChangeParent>();
                AreaChangeParent currentParent = null;
                foreach (var p in parents)
                {
                    if (p != null && p.gameObject != null && p.gameObject.name == sourcePrefix)
                    {
                        currentParent = p;
                        break;
                    }
                }

                if (currentParent != null)
                {
                    var childAcis = currentParent.GetComponentsInChildren<AreaChangeInfo>();
                    foreach (var aci in childAcis)
                    {
                        if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                            continue;
                        if (_knownTargets.Contains(aci.gameObject))
                            continue;

                        var dest = aci.m_Destination;
                        if (dest == null) continue;

                        if (dest.m_MapNo == _lastMapNo && dest.m_AreaNo == _lastAreaNo)
                            continue;

                        string name = GetTransitionNameFromAreaChange(aci);
                        float dist = Vector3.Distance(playerPos, aci.transform.position);
                        _events[EventCategory.Transitions].Add(new NavigationEvent
                        {
                            Name = name, Position = aci.transform.position,
                            Target = aci.gameObject, Category = EventCategory.Transitions,
                            DistanceToPlayer = dist
                        });
                        _knownTargets.Add(aci.gameObject);
                        newCount++;
                    }
                }
                else
                {
                    // Fallback: scene + name prefix filtering
                    var mgm = MainGameManager.m_instance;
                    string currentScene = mgm?.mapSceneName;

                    var areaChangeInfos = UnityEngine.Object.FindObjectsOfType<AreaChangeInfo>();
                    foreach (var aci in areaChangeInfos)
                    {
                        if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                            continue;
                        if (_knownTargets.Contains(aci.gameObject))
                            continue;

                        var dest = aci.m_Destination;
                        if (dest == null) continue;

                        if (!string.IsNullOrEmpty(currentScene) && !aci.gameObject.scene.name.StartsWith(currentScene))
                            continue;

                        if (!aci.gameObject.name.StartsWith(sourcePrefix))
                            continue;

                        if (dest.m_MapNo == _lastMapNo && dest.m_AreaNo == _lastAreaNo)
                            continue;

                        string name = GetTransitionNameFromAreaChange(aci);
                        float dist = Vector3.Distance(playerPos, aci.transform.position);
                        _events[EventCategory.Transitions].Add(new NavigationEvent
                        {
                            Name = name, Position = aci.transform.position,
                            Target = aci.gameObject, Category = EventCategory.Transitions,
                            DistanceToPlayer = dist
                        });
                        _knownTargets.Add(aci.gameObject);
                        newCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan Transitions error: {ex.Message}");
            }

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
                        if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
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

            // Remove destroyed objects and picked-up items during rescan.
            // Don't remove inactive non-item objects - the game deactivates distant objects for performance.
            foreach (var cat in AllCategories)
            {
                if (_events.ContainsKey(cat))
                {
                    _events[cat].RemoveAll(e =>
                    {
                        // Destroyed object
                        if (e.Target == null)
                        {
                            // Note: can't remove null from HashSet, but that's fine
                            return true;
                        }
                        // Picked-up items/materials: enableItemPickPoint becomes false after pickup.
                        // Don't check activeInHierarchy - game deactivates distant items for performance.
                        if (e.Category == EventCategory.Items || e.Category == EventCategory.Materials || e.Category == EventCategory.KeyItems)
                        {
                            var pickPoint = e.Target.GetComponent<ItemPickPointBase>();
                            if (pickPoint != null && !pickPoint.enableItemPickPoint)
                            {
                                _knownTargets.Remove(e.Target);
                                return true;
                            }
                        }
                        return false;
                    });
                }
            }

            // Always update categories after cleanup (items may have been removed)
            foreach (var kvp in _events)
                kvp.Value.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
            UpdateActiveCategories();

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
            _events[EventCategory.KeyItems] = new List<NavigationEvent>();
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
                $"{_events[EventCategory.KeyItems].Count} key items, " +
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
            var keyItems = _events.ContainsKey(EventCategory.KeyItems) ? _events[EventCategory.KeyItems] : null;
            items?.RemoveAll(e => removePickedUp(e));
            materials?.RemoveAll(e => removePickedUp(e));

            // Key items (EventTrigger pickups) - remove if destroyed, inactive, or consumed
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

            // Check for new items/materials/key items that may have loaded since the last scan
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
                                  : category == EventCategory.KeyItems ? keyItems
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
                            Category = EventCategory.KeyItems,
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

            // Refresh enemies - remove destroyed and defeated.
            var enemies = _events.ContainsKey(EventCategory.Enemies) ? _events[EventCategory.Enemies] : null;
            if (enemies != null)
            {
                enemies.RemoveAll(e => e.Target == null);

                // Remove defeated enemies: deactivated via SetDisp(false) after defeat.
                enemies.RemoveAll(e => e.Target != null && !e.Target.activeInHierarchy);

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
                        if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
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

                // Update distances for remaining (only active objects, keep cached for inactive)
                foreach (var e in enemies)
                {
                    if (e.Target != null && e.Target.activeInHierarchy)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
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

            // Re-sort all categories by distance (closest first)
            foreach (var kvp in _events)
                kvp.Value.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));

            UpdateActiveCategories();

            // Clamp current indices if lists shrunk
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

                DebugLogger.Log($"[NavList] ScanItems complete: {_events[EventCategory.Items].Count} items, {_events[EventCategory.Materials].Count} materials, {_events[EventCategory.KeyItems].Count} key items found");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanItems error: {ex.Message}");
            }
        }

        private void ScanQuestItems(Vector3 playerPos)
        {
            try
            {
                // Quest items are standalone EventTriggerScript objects with PICK_ command blocks.
                // They are invisible trigger zones placed by the scenario system for quest pickups.
                // Must use FindObjectsOfTypeAll because standalone EventTriggers aren't found by FindObjectsOfType.
                var eventTriggers = Resources.FindObjectsOfTypeAll<EventTriggerScript>();
                var placementLookup = BuildPlacementLookup();

                foreach (var et in eventTriggers)
                {
                    if (et == null || et.gameObject == null || !et.gameObject.activeInHierarchy)
                        continue;

                    // Skip NPC and enemy triggers - only want standalone pickup triggers
                    if (et.gameObject.GetComponent<NpcCtrl>() != null)
                        continue;
                    if (et.gameObject.GetComponent<EnemyCtrl>() != null)
                        continue;

                    // Verify this is a PICK_ trigger via placement data
                    string cmdBlock = null;
                    if (placementLookup.TryGetValue(et.m_EventParamId, out var placement))
                        cmdBlock = placement.m_CmdBlock;

                    if (string.IsNullOrEmpty(cmdBlock) || !cmdBlock.StartsWith("PICK_"))
                        continue;

                    var questInfo = ResolveQuestItem(placement.m_CmdBlock);
                    string name = questInfo.name ?? "Quest Pickup";
                    uint itemId = questInfo.itemId;
                    uint flagSetId = questInfo.flagSetId;

                    // Skip items already consumed (checked via saved scenario flags)
                    if (flagSetId != 0 && IsQuestFlagSet(flagSetId))
                        continue;

                    float dist = Vector3.Distance(playerPos, et.transform.position);

                    _events[EventCategory.KeyItems].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = et.transform.position,
                        Target = et.gameObject,
                        Category = EventCategory.KeyItems,
                        DistanceToPlayer = dist,
                        FlagSetId = flagSetId
                    });
                }

                DebugLogger.Log($"[NavList] ScanQuestItems: {_events[EventCategory.KeyItems].Count} active quest items");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanQuestItems error: {ex.Message}");
            }
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
                        else if (accessInfo.m_formIdx == 5 && maxBytes >= 8 && completionFlagSetId == 0)
                        {
                            // SSetFlagSetData: m_FlagSetId (offset 0), m_Val (offset 4)
                            uint flagId = ReadUInt32(binary, dataPos);
                            uint val = ReadUInt32(binary, dataPos + 4);
                            if (flagId != 0 && val == 1)
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

        /// <summary>
        /// Build the source map prefix for AreaChangeInfo/AreaChangeParent object names.
        /// Game names transitions as AC{mapNo:02d}{areaNo:02d}_{dest}, and groups
        /// them under AreaChangeParent objects named AC{mapNo:02d}{areaNo:02d}.
        /// </summary>
        private string GetAreaChangePrefix()
        {
            return $"AC{_lastMapNo:D2}{_lastAreaNo:D2}";
        }

        private void ScanTransitions(Vector3 playerPos)
        {
            try
            {
                string sourcePrefix = GetAreaChangePrefix();

                // Find the AreaChangeParent container for the current map.
                // The game organizes transitions under per-map AreaChangeParent objects
                // named AC{mapNo:02d}{areaNo:02d} (e.g., "AC0201" for Vast Plateau).
                var parents = UnityEngine.Object.FindObjectsOfType<AreaChangeParent>();
                AreaChangeParent currentParent = null;
                foreach (var p in parents)
                {
                    if (p != null && p.gameObject != null && p.gameObject.name == sourcePrefix)
                    {
                        currentParent = p;
                        break;
                    }
                }

                if (currentParent != null)
                {
                    // Get transitions from the AreaChangeParent's children
                    var childAcis = currentParent.GetComponentsInChildren<AreaChangeInfo>();
                    foreach (var aci in childAcis)
                    {
                        if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                            continue;

                        var dest = aci.m_Destination;
                        if (dest == null) continue;

                        // Skip entrances TO the current map
                        if (dest.m_MapNo == _lastMapNo && dest.m_AreaNo == _lastAreaNo)
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
                    }
                    DebugLogger.Log($"[NavList] ScanTransitions: found parent '{sourcePrefix}' with {_events[EventCategory.Transitions].Count} transitions");
                }
                else
                {
                    // Fallback: use scene + name prefix filtering
                    DebugLogger.Log($"[NavList] ScanTransitions: no AreaChangeParent '{sourcePrefix}' found, using fallback");
                    var mgm = MainGameManager.m_instance;
                    string currentScene = mgm?.mapSceneName;

                    var areaChangeInfos = UnityEngine.Object.FindObjectsOfType<AreaChangeInfo>();
                    foreach (var aci in areaChangeInfos)
                    {
                        if (aci == null || aci.gameObject == null || !aci.gameObject.activeInHierarchy)
                            continue;

                        var dest = aci.m_Destination;
                        if (dest == null) continue;

                        if (!string.IsNullOrEmpty(currentScene) && !aci.gameObject.scene.name.StartsWith(currentScene))
                            continue;

                        if (!aci.gameObject.name.StartsWith(sourcePrefix))
                            continue;

                        if (dest.m_MapNo == _lastMapNo && dest.m_AreaNo == _lastAreaNo)
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
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanTransitions error: {ex.Message}");
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
                    if (enemy == null || enemy.gameObject == null || !enemy.gameObject.activeInHierarchy)
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

            // Strategy 3: FishingTrigger (standalone fishing spots)
            try
            {
                var fishingTriggers = UnityEngine.Object.FindObjectsOfType<FishingTrigger>();
                if (fishingTriggers != null)
                {
                    foreach (var ft in fishingTriggers)
                    {
                        if (ft == null || ft.gameObject == null || !ft.gameObject.activeInHierarchy) continue;
                        if (facilityObjects.Contains(ft.gameObject)) continue;

                        float dist = Vector3.Distance(playerPos, ft.transform.position);
                        _events[EventCategory.Facilities].Add(new NavigationEvent
                        {
                            Name = "Fishing Spot", Position = ft.transform.position,
                            Target = ft.gameObject, Category = EventCategory.Facilities,
                            DistanceToPlayer = dist
                        });
                        facilityObjects.Add(ft.gameObject);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] FacilityScan fishing error: {ex.Message}");
            }

            // Strategy 4: Static toilet (training hall map only - map 1, area 12)
            try
            {
                if (_lastMapNo == 1 && _lastAreaNo == 12)
                {
                    var toiletObj = GameObject.Find("TriggerToiletPrefab");
                    if (toiletObj != null && toiletObj.activeInHierarchy && !facilityObjects.Contains(toiletObj))
                    {
                        float dist = Vector3.Distance(playerPos, toiletObj.transform.position);
                        _events[EventCategory.Facilities].Add(new NavigationEvent
                        {
                            Name = "Toilet", Position = toiletObj.transform.position,
                            Target = toiletObj, Category = EventCategory.Facilities,
                            DistanceToPlayer = dist
                        });
                        facilityObjects.Add(toiletObj);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] FacilityScan toilet error: {ex.Message}");
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
                        return EventCategory.KeyItems;
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

            ScreenReader.Say($"{categoryName}, {count} {(count == 1 ? "entry" : "entries")}, {catPos} of {catTotal}");
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

                // Extract path corners into flat arrays for the beacon
                var corners = path.corners;
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

                // Key items: stop pathfinding when player enters the trigger zone.
                // The game's MapTriggerScript.isStay/isEnter tracks whether the player
                // is inside the trigger volume. This ensures the event will actually fire.
                if (_pathfindingCategory == EventCategory.KeyItems && _pathfindingTarget != null)
                {
                    try
                    {
                        var trigger = _pathfindingTarget.GetComponent<MapTriggerScript>();
                        if (trigger != null && (trigger.isStay || trigger.isEnter))
                        {
                            StopPathfinding("Destination reached");
                            return;
                        }
                    }
                    catch { }
                }

                // Distance-based arrival for NPCs, Enemies, and Facilities.
                // Transitions need to walk into the zone trigger (map change stops pathfinding).
                // Items need close proximity to trigger the pickup prompt.
                // Both rely on stuck detection below instead.
                if (_pathfindingCategory != EventCategory.Transitions
                    && _pathfindingCategory != EventCategory.Items
                    && _pathfindingCategory != EventCategory.KeyItems)
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

                var corners = path.corners;
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
                    var corners = path.corners;
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
                    }
                    else
                    {
                        // Fallback: assume camera looks at -Z
                        AutoWalkStickX = direction.x;
                        AutoWalkStickY = direction.z;
                    }
                    AutoWalkActive = true;
                }
                else
                {
                    AutoWalkActive = false;
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

        private string GetCategoryDisplayName(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.NPCs: return "NPCs";
                case EventCategory.Items: return "Items";
                case EventCategory.Materials: return "Materials";
                case EventCategory.KeyItems: return "Key Items";
                case EventCategory.Transitions: return "Transitions";
                case EventCategory.Enemies: return "Enemies";
                case EventCategory.Facilities: return "Facilities";
                default: return category.ToString();
            }
        }

        #endregion
    }
}
