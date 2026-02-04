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
        }

        // Category order for cycling (only non-empty categories are used)
        private static readonly EventCategory[] AllCategories = {
            EventCategory.NPCs,
            EventCategory.Items,
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

        // Track field state to rescan after evolution/events
        private bool _wasInField = false;
        private float _leftFieldTime = 0f;

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
                return;
            }

            // Rescan after returning to field from evolution/battles (>2s away)
            if (!_wasInField && _listBuilt && (currentTime - _leftFieldTime > 2f))
            {
                _isRescanning = true;
                _mapChangeTime = currentTime;
                _nextRescanTime = currentTime + InitialScanDelay;
                _knownTargets.Clear();
                _facilityNpcObjects.Clear();
                foreach (var cat in AllCategories)
                    if (_events.ContainsKey(cat))
                        _events[cat].Clear();
                _listBuilt = false;
                DebugLogger.Log("[NavList] Returned to field after extended absence, rescanning");
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
        }

        /// <summary>
        /// Check if the player is in the field (not in battle, not in menus, not in events).
        /// Delegates to GameStateService.IsPlayerInField() with additional evolution check.
        /// </summary>
        private bool IsPlayerInField()
        {
            // Evolution plays on the field but disables items/transitions
            if (_evolutionActive)
                return false;

            return GameStateService.IsPlayerInField(_playerCtrl);
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
                    _events[EventCategory.Transitions] = new List<NavigationEvent>();
                    _events[EventCategory.Enemies] = new List<NavigationEvent>();
                    _events[EventCategory.Facilities] = new List<NavigationEvent>();
                    _activeCategories.Clear();
                    _knownTargets.Clear();
                    _facilityNpcObjects.Clear();

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

            // Scan for new NPCs (excluding facility NPCs)
            try
            {
                if (_npcManager != null && _npcManager.m_NpcCtrlArray != null)
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

            // Scan for new items
            try
            {
                var itemManager = ItemPickPointManager.m_instance;
                if (itemManager != null && itemManager.m_itemPickPoints != null)
                {
                    foreach (var point in itemManager.m_itemPickPoints)
                    {
                        if (point == null || point.gameObject == null || !point.gameObject.activeInHierarchy)
                            continue;
                        if (!point.enableItemPickPoint)
                            continue;
                        if (_knownTargets.Contains(point.gameObject))
                            continue;

                        string name = GetItemName(point);
                        float dist = Vector3.Distance(playerPos, point.transform.position);
                        _events[EventCategory.Items].Add(new NavigationEvent
                        {
                            Name = name, Position = point.transform.position,
                            Target = point.gameObject, Category = EventCategory.Items,
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

            // Scan for new transitions
            try
            {
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
                foreach (var trigger in mapTriggers)
                {
                    if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                        continue;
                    if (trigger.enterID != MapTriggerManager.EVENT.MapChange)
                        continue;
                    if (_knownTargets.Contains(trigger.gameObject))
                        continue;

                    string name = GetTransitionName(trigger);
                    float dist = Vector3.Distance(playerPos, trigger.transform.position);
                    _events[EventCategory.Transitions].Add(new NavigationEvent
                    {
                        Name = name, Position = trigger.transform.position,
                        Target = trigger.gameObject, Category = EventCategory.Transitions,
                        DistanceToPlayer = dist
                    });
                    _knownTargets.Add(trigger.gameObject);
                    newCount++;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] Rescan Transitions error: {ex.Message}");
            }

            // Scan for new enemies
            try
            {
                if (_enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
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

            // Only remove truly destroyed objects during rescan (null target).
            // Don't remove inactive objects - the game deactivates distant objects for performance.
            foreach (var cat in AllCategories)
            {
                if (_events.ContainsKey(cat))
                {
                    _events[cat].RemoveAll(e =>
                    {
                        if (e.Target == null)
                        {
                            _knownTargets.Remove(e.Target);
                            return true;
                        }
                        return false;
                    });
                }
            }

            if (newCount > 0)
            {
                // Sort and update categories
                foreach (var kvp in _events)
                    kvp.Value.Sort((a, b) => a.DistanceToPlayer.CompareTo(b.DistanceToPlayer));
                UpdateActiveCategories();

                DebugLogger.Log($"[NavList] Rescan found {newCount} new objects. Totals: " +
                    $"{_events[EventCategory.NPCs].Count} NPCs, " +
                    $"{_events[EventCategory.Items].Count} items, " +
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
            _events[EventCategory.Transitions] = new List<NavigationEvent>();
            _events[EventCategory.Enemies] = new List<NavigationEvent>();
            _events[EventCategory.Facilities] = new List<NavigationEvent>();
            _knownTargets.Clear();
            _facilityNpcObjects.Clear();

            Vector3 playerPos = _playerCtrl != null ? _playerCtrl.transform.position : Vector3.zero;

            // Scan facilities BEFORE NPCs so facility NPCs are excluded from the NPC list
            ScanFacilities(playerPos);
            ScanNPCs(playerPos);
            ScanItems(playerPos);
            ScanTransitions(playerPos);
            ScanEnemies(playerPos);

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

            // Refresh items (check enableItemPickPoint and activeInHierarchy)
            var items = _events.ContainsKey(EventCategory.Items) ? _events[EventCategory.Items] : null;
            if (items != null)
            {
                items.RemoveAll(e =>
                {
                    if (e.Target == null || !e.Target.activeInHierarchy)
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

                // Update distances
                foreach (var e in items)
                {
                    if (e.Target != null)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Refresh enemies (check activeInHierarchy)
            var enemies = _events.ContainsKey(EventCategory.Enemies) ? _events[EventCategory.Enemies] : null;
            if (enemies != null)
            {
                int beforeCount = enemies.Count;
                enemies.RemoveAll(e => e.Target == null || !e.Target.activeInHierarchy);

                // Check for new enemies that may have spawned
                if (_enemyManager != null && _enemyManager.m_EnemyCtrlArray != null)
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

                // Update distances for remaining
                foreach (var e in enemies)
                {
                    if (e.Target != null)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Refresh NPCs (update distances, remove inactive)
            var npcs = _events.ContainsKey(EventCategory.NPCs) ? _events[EventCategory.NPCs] : null;
            if (npcs != null)
            {
                npcs.RemoveAll(e => e.Target == null || !e.Target.activeInHierarchy);
                foreach (var e in npcs)
                {
                    if (e.Target != null)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Refresh transitions (update distances, remove inactive)
            var transitions = _events.ContainsKey(EventCategory.Transitions) ? _events[EventCategory.Transitions] : null;
            if (transitions != null)
            {
                transitions.RemoveAll(e => e.Target == null || !e.Target.activeInHierarchy);
                foreach (var e in transitions)
                {
                    if (e.Target != null)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

            // Refresh facilities (update distances, remove inactive)
            var facilities = _events.ContainsKey(EventCategory.Facilities) ? _events[EventCategory.Facilities] : null;
            if (facilities != null)
            {
                facilities.RemoveAll(e => e.Target == null || !e.Target.activeInHierarchy);
                foreach (var e in facilities)
                {
                    if (e.Target != null)
                    {
                        e.Position = e.Target.transform.position;
                        e.DistanceToPlayer = Vector3.Distance(playerPos, e.Position);
                    }
                }
            }

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
                    if (point == null || point.gameObject == null || !point.gameObject.activeInHierarchy)
                        continue;

                    if (!point.enableItemPickPoint)
                        continue;

                    string name = GetItemName(point);
                    float dist = Vector3.Distance(playerPos, point.transform.position);

                    _events[EventCategory.Items].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = point.transform.position,
                        Target = point.gameObject,
                        Category = EventCategory.Items,
                        DistanceToPlayer = dist
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanItems error: {ex.Message}");
            }
        }

        private void ScanTransitions(Vector3 playerPos)
        {
            try
            {
                var mapTriggers = UnityEngine.Object.FindObjectsOfType<MapTriggerScript>();
                foreach (var trigger in mapTriggers)
                {
                    if (trigger == null || trigger.gameObject == null || !trigger.gameObject.activeInHierarchy)
                        continue;

                    if (trigger.enterID != MapTriggerManager.EVENT.MapChange)
                        continue;

                    string name = GetTransitionName(trigger);
                    float dist = Vector3.Distance(playerPos, trigger.transform.position);

                    _events[EventCategory.Transitions].Add(new NavigationEvent
                    {
                        Name = name,
                        Position = trigger.transform.position,
                        Target = trigger.gameObject,
                        Category = EventCategory.Transitions,
                        DistanceToPlayer = dist
                    });
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] ScanTransitions error: {ex.Message}");
            }
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

                return npcId;
            }

            try { return npc.gameObject.name ?? "Unknown NPC"; } catch { }
            return "Unknown NPC";
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
                        {
                            // Indicate if it's a material
                            if (point.isMaterial)
                                return $"{name} (Material)";
                            return name;
                        }
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

        private string GetTransitionName(MapTriggerScript trigger)
        {
            try
            {
                // Try to get AreaChangeInfo from the same GameObject
                var areaChangeInfo = trigger.GetComponent<AreaChangeInfo>();
                if (areaChangeInfo != null && areaChangeInfo.m_Destination != null)
                {
                    var dest = areaChangeInfo.m_Destination;
                    int destMapNo = dest.m_MapNo;
                    int destAreaNo = dest.m_AreaNo;

                    // Try area name first (more specific)
                    try
                    {
                        string areaName = ParameterAreaName.GetAreaName((AppInfo.MAP)destMapNo, (uint)destAreaNo);
                        if (!string.IsNullOrEmpty(areaName) && !areaName.Contains("not found"))
                            return areaName;
                    }
                    catch { }

                    // Fall back to map name
                    try
                    {
                        string mapName = ParameterMapName.GetMapName((AppInfo.MAP)destMapNo);
                        if (!string.IsNullOrEmpty(mapName) && !mapName.Contains("not found"))
                            return mapName;
                    }
                    catch { }

                    return $"Area {destMapNo}-{destAreaNo}";
                }

                // Try parent GameObject for AreaChangeInfo
                if (trigger.transform.parent != null)
                {
                    var parentAreaInfo = trigger.transform.parent.GetComponent<AreaChangeInfo>();
                    if (parentAreaInfo != null && parentAreaInfo.m_Destination != null)
                    {
                        var dest = parentAreaInfo.m_Destination;
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
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] GetTransitionName error: {ex.Message}");
            }

            // Fallback: use GameObject name which often contains area info
            try
            {
                string objName = trigger.gameObject.name;
                if (!string.IsNullOrEmpty(objName))
                    return $"Transition ({objName})";
            }
            catch { }

            return "Unknown Transition";
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

            // Navigate to event (P key default) - will be enhanced with audio pathfinding later
            if (ModInputManager.IsActionTriggered("NavToEvent"))
            {
                AnnouncePathToEvent();
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
                : navEvent.Position; // Use cached position if object is currently inactive

            // Use NavMesh to calculate walkable path
            // First, find nearest valid NavMesh point to target (target may be at map edge or off-mesh)
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

                string cardinal = GetCardinalDirection(targetPos - playerPos);

                if (found && path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete)
                {
                    float totalDist = 0f;
                    for (int i = 0; i < path.corners.Length - 1; i++)
                        totalDist += Vector3.Distance(path.corners[i], path.corners[i + 1]);

                    ScreenReader.Say($"{navEvent.Name}: {cardinal}, {Mathf.RoundToInt(totalDist)} meters walking distance");
                }
                else if (found && path.status == UnityEngine.AI.NavMeshPathStatus.PathPartial)
                {
                    float totalDist = 0f;
                    for (int i = 0; i < path.corners.Length - 1; i++)
                        totalDist += Vector3.Distance(path.corners[i], path.corners[i + 1]);

                    ScreenReader.Say($"{navEvent.Name}: {cardinal}, {Mathf.RoundToInt(totalDist)} meters, partial path");
                }
                else
                {
                    int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, targetPos));
                    ScreenReader.Say($"{navEvent.Name}: {cardinal}, {dist} meters straight line");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[NavList] AnnouncePathToEvent error: {ex.Message}");
                int dist = Mathf.RoundToInt(Vector3.Distance(playerPos, targetPos));
                ScreenReader.Say($"{navEvent.Name}: {dist} meters");
            }
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
                case EventCategory.Transitions: return "Transitions";
                case EventCategory.Enemies: return "Enemies";
                case EventCategory.Facilities: return "Facilities";
                default: return category.ToString();
            }
        }

        #endregion
    }
}
