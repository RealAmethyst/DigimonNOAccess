using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Digivice Library (Digimon encyclopedia).
    /// Tracks two views: grid browsing (Top) and detail view with tabs
    /// (Before/After evolution, Skill).
    /// Supports evo-tree navigation: pressing OK on an evo target loads
    /// that Digimon's detail page (selectDigiID changes).
    /// </summary>
    public class LibraryHandler : IAccessibilityHandler
    {
        private const string LogTag = "[Library]";
        public int Priority => 65;

        private uDigiviceLibraryPanel _libraryPanel;
        private bool _wasActive;
        private uDigiviceLibraryPanel.State _lastMainState = uDigiviceLibraryPanel.State.None;
        private uDigiviceLibraryDetailPanel.State _lastDetailState = uDigiviceLibraryDetailPanel.State.None;
        private int _lastGridCursor = -1;
        private int _lastEvoCursor = -1;
        private int _lastSkillCursorX = -1;
        private int _lastSkillCursorY = -1;
        private uint _lastSelectDigiID;

        // Waiting for detail panel to leave LoadWait/None state
        private bool _waitingForDetailState;
        // Deferred skill read (caption updates 1 frame after cursor)
        private bool _pendingSkillRead;
        // Deferred first item (localization needs 1-2 frames)
        private int _pendingOpenFrames = -1;

        // Personality enum to string (ParameterDigimonData.PersonalityType)
        private static readonly string[] PersonalityNames =
            { "None", "Honest", "Twisted", "Lukewarm", "Fussy", "Enthusiastic", "Prudent" };

        // Attribute enum to string (ParameterDigimonCardData.AttributeIndex)
        private static readonly string[] AttributeNames =
            { "None", "Data", "Vaccine", "Virus", "Free", "Variable", "Unknown" };

        public bool IsOpen()
        {
            try
            {
                if (_libraryPanel == null)
                    return false;
                var state = _libraryPanel.GetState();
                return state != uDigiviceLibraryPanel.State.None;
            }
            catch
            {
                _libraryPanel = null;
                return false;
            }
        }

        public void Update()
        {
            if (_libraryPanel == null)
            {
                try
                {
                    _libraryPanel = uDigiviceLibraryPanel.m_instance;
                }
                catch { return; }
                if (_libraryPanel == null) return;
            }

            bool isActive = IsOpen();

            if (isActive && !_wasActive)
                OnOpen();
            else if (!isActive && _wasActive)
                OnClose();
            else if (isActive)
                OnUpdate();

            _wasActive = isActive;
        }

        public void AnnounceStatus()
        {
            if (!IsOpen()) return;

            try
            {
                var state = _libraryPanel.GetState();
                if (state == uDigiviceLibraryPanel.State.Top)
                {
                    string name = GetGridItemName();
                    int cursor = GetGridCursor();
                    int total = GetGridTotal();
                    ScreenReader.Say(AnnouncementBuilder.MenuOpen(GetHeaderTitle(), name, cursor, total));
                }
                else if (state == uDigiviceLibraryPanel.State.Detail)
                {
                    AnnounceDetailStatus();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"{LogTag} AnnounceStatus error: {ex.Message}");
            }
        }

        private void OnOpen()
        {
            _lastMainState = uDigiviceLibraryPanel.State.None;
            _lastDetailState = uDigiviceLibraryDetailPanel.State.None;
            _lastGridCursor = -1;
            _lastEvoCursor = -1;
            _lastSkillCursorX = -1;
            _lastSkillCursorY = -1;
            _lastSelectDigiID = 0;
            _waitingForDetailState = false;
            _pendingSkillRead = false;

            // 2-frame delay for name localization (game loads text async)
            _pendingOpenFrames = 0;
            DebugLogger.Log($"{LogTag} Opened");
        }

        private void OnClose()
        {
            _libraryPanel = null;
            _lastMainState = uDigiviceLibraryPanel.State.None;
            _lastDetailState = uDigiviceLibraryDetailPanel.State.None;
            _lastGridCursor = -1;
            _lastEvoCursor = -1;
            _lastSkillCursorX = -1;
            _lastSkillCursorY = -1;
            _lastSelectDigiID = 0;
            _waitingForDetailState = false;
            _pendingSkillRead = false;
            _pendingOpenFrames = -1;

            DebugLogger.Log($"{LogTag} Closed");
        }

        private void OnUpdate()
        {
            // Wait 2 frames for name localization on initial open
            if (_pendingOpenFrames >= 0)
            {
                _pendingOpenFrames++;
                if (_pendingOpenFrames >= 2)
                    AnnounceFirstItem();
                return;
            }

            // Wait for detail panel to finish loading
            if (_waitingForDetailState)
            {
                TryAnnounceDetailEntry();
                return;
            }

            // Deferred skill read (caption updates 1 frame after cursor move)
            if (_pendingSkillRead)
            {
                _pendingSkillRead = false;
                AnnounceCurrentSkill();
            }

            CheckMainStateChange();
        }

        private void AnnounceFirstItem()
        {
            _pendingOpenFrames = -1;

            try
            {
                var state = _libraryPanel.GetState();
                if (state != uDigiviceLibraryPanel.State.Top)
                {
                    ScreenReader.Say(GetHeaderTitle());
                    return;
                }

                _lastMainState = state;
                string name = GetGridItemName();
                int cursor = GetGridCursor();
                int total = GetGridTotal();
                _lastGridCursor = cursor;

                ScreenReader.Say(AnnouncementBuilder.MenuOpen(GetHeaderTitle(), name, cursor, total));
                DebugLogger.Log($"{LogTag} First item: {name}, cursor={cursor}, {cursor + 1} of {total}");
            }
            catch (System.Exception ex)
            {
                ScreenReader.Say(GetHeaderTitle());
                DebugLogger.Warning($"{LogTag} Open error: {ex.Message}");
            }
        }

        private void CheckMainStateChange()
        {
            var state = _libraryPanel.GetState();
            if (state == _lastMainState)
            {
                if (state == uDigiviceLibraryPanel.State.Top)
                    CheckGridCursor();
                else if (state == uDigiviceLibraryPanel.State.Detail)
                    CheckDetailUpdates();
                return;
            }

            var prevState = _lastMainState;
            _lastMainState = state;
            DebugLogger.Log($"{LogTag} State: {prevState} -> {state}");

            if (state == uDigiviceLibraryPanel.State.Top)
            {
                // Returning from detail to grid
                _lastDetailState = uDigiviceLibraryDetailPanel.State.None;
                _lastEvoCursor = -1;
                _lastSkillCursorX = -1;
                _lastSkillCursorY = -1;
                _lastSelectDigiID = 0;
                _pendingSkillRead = false;
                _waitingForDetailState = false;

                string name = GetGridItemName();
                int cursor = GetGridCursor();
                int total = GetGridTotal();
                _lastGridCursor = cursor;
                ScreenReader.Say(AnnouncementBuilder.CursorPosition(name, cursor, total));
            }
            else if (state == uDigiviceLibraryPanel.State.Detail)
            {
                // Entering detail view - wait for valid state
                _waitingForDetailState = true;
            }
        }

        private void CheckGridCursor()
        {
            try
            {
                int cursor = GetGridCursor();
                if (cursor == _lastGridCursor)
                    return;

                _lastGridCursor = cursor;
                string name = GetGridItemName();
                int total = GetGridTotal();
                DebugLogger.Log($"{LogTag} Grid: {name}, cursor={cursor}, {cursor + 1} of {total}");
                ScreenReader.Say(AnnouncementBuilder.CursorPosition(name, cursor, total));
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Grid cursor error: {ex.Message}");
            }
        }

        private void TryAnnounceDetailEntry()
        {
            try
            {
                var detailState = GetDetailState();

                // Keep waiting if still loading
                if (detailState == uDigiviceLibraryDetailPanel.State.LoadWait ||
                    detailState == uDigiviceLibraryDetailPanel.State.None)
                    return;

                _waitingForDetailState = false;
                _lastDetailState = detailState;
                _lastSelectDigiID = _libraryPanel.m_DetailPanel.selectDigiID;

                AnnounceDigimonInfo(detailState);

                // Set initial cursors for the current tab
                if (detailState == uDigiviceLibraryDetailPanel.State.Before ||
                    detailState == uDigiviceLibraryDetailPanel.State.After)
                {
                    _lastEvoCursor = GetEvoCursor(detailState);
                }
                else if (detailState == uDigiviceLibraryDetailPanel.State.Skill)
                {
                    CaptureSkillCursor();
                    _pendingSkillRead = true;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Detail entry error: {ex.Message}");
            }
        }

        private void CheckDetailUpdates()
        {
            try
            {
                var detailState = GetDetailState();

                // Skip LoadWait - don't update _lastDetailState
                if (detailState == uDigiviceLibraryDetailPanel.State.LoadWait)
                    return;

                // Check for tab change
                if (detailState != _lastDetailState)
                {
                    var prevDetail = _lastDetailState;
                    _lastDetailState = detailState;
                    _lastEvoCursor = -1;
                    _pendingSkillRead = false;

                    DebugLogger.Log($"{LogTag} Tab: {prevDetail} -> {detailState}");

                    if (detailState == uDigiviceLibraryDetailPanel.State.Back)
                    {
                        var info = GetDigimonInfoFromParams();
                        ScreenReader.Say($"{info.name}. {info.natureLabel}: {info.nature}. {info.attributeLabel}: {info.attribute}");
                    }
                    else
                    {
                        string tabName = GetTabName(detailState);
                        if (tabName != null)
                        {
                            string announcement = $"{tabName} tab";

                            if (detailState == uDigiviceLibraryDetailPanel.State.Before ||
                                detailState == uDigiviceLibraryDetailPanel.State.After)
                            {
                                string evoName = GetEvoDigimonName(detailState);
                                if (evoName != null)
                                {
                                    int evoCursor = GetEvoCursor(detailState);
                                    int evoTotal = GetEvoTotal(detailState);
                                    _lastEvoCursor = evoCursor;
                                    announcement += $". {AnnouncementBuilder.CursorPosition(evoName, evoCursor, evoTotal)}";
                                }
                            }
                            else if (detailState == uDigiviceLibraryDetailPanel.State.Skill)
                            {
                                CaptureSkillCursor();
                                _pendingSkillRead = true;
                            }

                            ScreenReader.Say(announcement);
                        }
                    }

                    // Update tracked Digimon ID after tab change
                    _lastSelectDigiID = _libraryPanel.m_DetailPanel.selectDigiID;
                    return;
                }

                // Same state - check if viewed Digimon changed (evo tree navigation)
                uint currentDigiID = _libraryPanel.m_DetailPanel.selectDigiID;
                if (currentDigiID != _lastSelectDigiID && _lastSelectDigiID != 0)
                {
                    _lastSelectDigiID = currentDigiID;
                    _lastEvoCursor = -1;

                    AnnounceDigimonInfo(detailState);

                    // Set initial evo cursor for the new Digimon
                    if (detailState == uDigiviceLibraryDetailPanel.State.Before ||
                        detailState == uDigiviceLibraryDetailPanel.State.After)
                    {
                        _lastEvoCursor = GetEvoCursor(detailState);
                    }
                    return;
                }

                // Same Digimon, same state - check cursor changes
                if (detailState == uDigiviceLibraryDetailPanel.State.Before ||
                    detailState == uDigiviceLibraryDetailPanel.State.After)
                {
                    CheckEvoCursor(detailState);
                }
                else if (detailState == uDigiviceLibraryDetailPanel.State.Skill)
                {
                    CheckSkillCursor();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Detail update error: {ex.Message}");
            }
        }

        /// <summary>
        /// Announces the current Digimon's info with tab context.
        /// Used for initial detail entry and evo tree navigation.
        /// </summary>
        private void AnnounceDigimonInfo(uDigiviceLibraryDetailPanel.State detailState)
        {
            var info = GetDigimonInfoFromParams();
            string announcement = $"{info.name}. {info.natureLabel}: {info.nature}. {info.attributeLabel}: {info.attribute}";

            if (detailState != uDigiviceLibraryDetailPanel.State.Back)
            {
                string tabName = GetTabName(detailState);
                if (tabName != null)
                    announcement += $". {tabName} tab";
            }

            // Include first evo entry when on Before/After tab
            if (detailState == uDigiviceLibraryDetailPanel.State.Before ||
                detailState == uDigiviceLibraryDetailPanel.State.After)
            {
                string evoName = GetEvoDigimonName(detailState);
                if (evoName != null)
                {
                    int evoCursor = GetEvoCursor(detailState);
                    int evoTotal = GetEvoTotal(detailState);
                    announcement += $". {AnnouncementBuilder.CursorPosition(evoName, evoCursor, evoTotal)}";
                }
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Detail: {info.name}, nature={info.nature}, attr={info.attribute}, state={detailState}");
        }

        private void CheckEvoCursor(uDigiviceLibraryDetailPanel.State state)
        {
            try
            {
                int cursor = GetEvoCursor(state);
                if (cursor == _lastEvoCursor)
                    return;

                _lastEvoCursor = cursor;
                string name = GetEvoDigimonName(state);
                int total = GetEvoTotal(state);
                if (name != null)
                {
                    ScreenReader.Say(AnnouncementBuilder.CursorPosition(name, cursor, total));
                    DebugLogger.Log($"{LogTag} Evo: {name}, cursor={cursor}, {cursor + 1} of {total}");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Evo cursor error: {ex.Message}");
            }
        }

        private void CaptureSkillCursor()
        {
            var skillPanel = _libraryPanel.m_DetailPanel?.m_DetailSKill;
            if (skillPanel != null)
            {
                _lastSkillCursorX = skillPanel.m_CursorX;
                _lastSkillCursorY = skillPanel.m_CursorY;
            }
        }

        private void CheckSkillCursor()
        {
            try
            {
                var skillPanel = _libraryPanel.m_DetailPanel?.m_DetailSKill;
                if (skillPanel == null) return;

                // Handle deferred read from previous frame's cursor change
                if (_pendingSkillRead)
                {
                    _pendingSkillRead = false;
                    AnnounceCurrentSkill();
                    return;
                }

                int cursorX = skillPanel.m_CursorX;
                int cursorY = skillPanel.m_CursorY;

                if (cursorX == _lastSkillCursorX && cursorY == _lastSkillCursorY)
                    return;

                _lastSkillCursorX = cursorX;
                _lastSkillCursorY = cursorY;
                // Defer the actual announcement by 1 frame so caption text updates
                _pendingSkillRead = true;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Skill cursor error: {ex.Message}");
            }
        }

        private void AnnounceCurrentSkill()
        {
            try
            {
                var skillPanel = _libraryPanel.m_DetailPanel?.m_DetailSKill;
                if (skillPanel == null) return;

                // Force the game to update the caption for the current cursor position
                // (same pattern as PartnerPanelHandler's skill select grid)
                try { skillPanel.SetSelectSkillCaption(); } catch { }

                int cursorX = skillPanel.m_CursorX;
                int cursorY = skillPanel.m_CursorY;

                // Check if this Digimon has learned this skill via attack flags
                // Grid index uses column-major order: column * 7 rows + row
                bool hasSkill = false;
                try
                {
                    uint gridIndex = (uint)(cursorX * 7 + cursorY);
                    var attackFlg = skillPanel.m_DigiAttackFlg;
                    if (attackFlg != null && attackFlg[gridIndex])
                        hasSkill = true;
                    if (!hasSkill)
                    {
                        var releaseFlg = skillPanel.m_ReleaseAttackFlg;
                        if (releaseFlg != null && releaseFlg[gridIndex])
                            hasSkill = true;
                    }
                }
                catch { }

                // Get skill name from caption
                var caption = skillPanel.m_SelectSkillCaption;
                if (caption == null) return;

                uint skillCode = caption.GetSkillCode();
                string name = null;

                if (skillCode != 0)
                {
                    try
                    {
                        var attackData = ParameterAttackData.GetParam(skillCode);
                        if (attackData != null)
                            name = attackData.GetName();
                    }
                    catch { }

                    if (string.IsNullOrEmpty(name))
                    {
                        name = caption.m_SkillName?.text;
                        if (!string.IsNullOrEmpty(name))
                            name = TextUtilities.StripRichTextTags(name);
                    }
                }

                if (hasSkill && !string.IsNullOrEmpty(name))
                {
                    ScreenReader.Say(name);
                    DebugLogger.Log($"{LogTag} Skill: {name} (code={skillCode}) at ({cursorX}, {cursorY})");
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    ScreenReader.Say($"{name}, not learned");
                    DebugLogger.Log($"{LogTag} Skill: {name}, not learned (code={skillCode}) at ({cursorX}, {cursorY})");
                }
                else
                {
                    ScreenReader.Say("Empty");
                    DebugLogger.Log($"{LogTag} Skill: Empty at ({cursorX}, {cursorY})");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Skill announce error: {ex.Message}");
            }
        }

        private void AnnounceDetailStatus()
        {
            var detailState = GetDetailState();
            if (detailState == uDigiviceLibraryDetailPanel.State.LoadWait)
                detailState = uDigiviceLibraryDetailPanel.State.Back;

            if (detailState == uDigiviceLibraryDetailPanel.State.Back)
            {
                var info = GetDigimonInfoFromParams();
                ScreenReader.Say($"{info.name}. {info.natureLabel}: {info.nature}. {info.attributeLabel}: {info.attribute}");
            }
            else if (detailState == uDigiviceLibraryDetailPanel.State.Before ||
                     detailState == uDigiviceLibraryDetailPanel.State.After)
            {
                string tabName = GetTabName(detailState);
                string evoName = GetEvoDigimonName(detailState);
                int cursor = GetEvoCursor(detailState);
                int total = GetEvoTotal(detailState);
                if (evoName != null)
                    ScreenReader.Say($"{tabName} tab. {AnnouncementBuilder.CursorPosition(evoName, cursor, total)}");
                else
                    ScreenReader.Say($"{tabName} tab");
            }
            else if (detailState == uDigiviceLibraryDetailPanel.State.Skill)
            {
                string skillName = GetCurrentSkillName();
                ScreenReader.Say($"Skill tab. {skillName ?? ""}");
            }
        }

        // --- Data accessors ---

        /// <summary>
        /// Returns the actual 0-based index in the full Digimon list,
        /// accounting for scroll offset. m_DataCursor is only the visual
        /// position within the visible grid (e.g., 0-27 for a 4x7 grid).
        /// When the grid scrolls, m_DispStartY increases but m_DataCursor
        /// stays at the edge, so we combine them for the real index.
        /// </summary>
        private int GetGridCursor()
        {
            var mainWindow = _libraryPanel.m_TopPanel.m_MainWindow;
            int columns = uDigiviceLibraryTopMainWindow.MAX_DISP_ICON_NUM_X;
            if (columns <= 0) columns = 7;
            return mainWindow.m_DispStartY * columns + mainWindow.m_DataCursor;
        }

        private int GetGridTotal()
        {
            return _libraryPanel.m_TopPanel.m_MainWindow.m_DigiList.Count;
        }

        private string GetGridItemName()
        {
            const string fallback = "Unknown";

            try
            {
                if (_libraryPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Grid name: library panel was null");
                    return fallback;
                }

                var topPanel = _libraryPanel.m_TopPanel;
                if (topPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Grid name: m_TopPanel was null");
                    return fallback;
                }

                var mainWindow = topPanel.m_MainWindow;
                if (mainWindow == null)
                {
                    DebugLogger.Log($"{LogTag} Grid name: m_TopPanel.m_MainWindow was null");
                    return fallback;
                }

                var nameText = mainWindow.m_NameText;
                if (nameText == null)
                {
                    DebugLogger.Log($"{LogTag} Grid name: m_TopPanel.m_MainWindow.m_NameText was null");
                    return fallback;
                }

                string name = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(nameText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(name) || TextUtilities.IsPlaceholderText(name))
                {
                    DebugLogger.Log($"{LogTag} Grid name: m_TopPanel.m_MainWindow.m_NameText.text unusable: {TextUtilities.DescribeUnusable(name)}");
                    return fallback;
                }

                // Preserve the game's rendered "???" for undiscovered Digimon.
                return name;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Grid name read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetHeaderTitle()
        {
            try
            {
                if (_libraryPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Header: library panel was null");
                    return "Field Guide";
                }

                var headLine = _libraryPanel.m_HeadLine;
                if (headLine == null)
                {
                    DebugLogger.Log($"{LogTag} Header: m_HeadLine was null");
                    return "Field Guide";
                }

                var headerText = headLine.m_Text;
                if (headerText == null)
                {
                    DebugLogger.Log($"{LogTag} Header: m_HeadLine.m_Text was null");
                    return "Field Guide";
                }

                string text = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(headerText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(text) || TextUtilities.IsPlaceholderText(text))
                {
                    DebugLogger.Log($"{LogTag} Header: m_HeadLine.m_Text.text unusable: {TextUtilities.DescribeUnusable(text)}");
                    return "Field Guide";
                }

                return text;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Header read failed: {ex.Message}");
            }
            return "Field Guide";
        }

        private uDigiviceLibraryDetailPanel.State GetDetailState()
        {
            return _libraryPanel.m_DetailPanel.GetState();
        }

        /// <summary>
        /// Gets Digimon info directly from parameter data using selectDigiID.
        /// This works regardless of which tab the detail panel is on.
        /// </summary>
        private (string name, string natureLabel, string nature, string attributeLabel, string attribute) GetDigimonInfoFromParams()
        {
            ParameterDigimonData param = null;
            try
            {
                if (_libraryPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Detail parameter fallback: library panel was null");
                }
                else
                {
                    var detailPanel = _libraryPanel.m_DetailPanel;
                    if (detailPanel == null)
                    {
                        DebugLogger.Log($"{LogTag} Detail parameter fallback: m_DetailPanel was null");
                    }
                    else
                    {
                        param = ParameterDigimonData.GetParam(detailPanel.selectDigiID);
                        if (param == null)
                            DebugLogger.Log($"{LogTag} Detail parameter fallback: ParameterDigimonData.GetParam returned null");
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Detail parameter fallback read failed: {ex.Message}");
            }

            string name = "Unknown";
            string natureFallback = "";
            string attributeFallback = "";

            if (param != null)
            {
                name = param.GetDefaultName() ?? "Unknown";

                int personality = param.m_personality;
                natureFallback = (personality >= 0 && personality < PersonalityNames.Length)
                    ? PersonalityNames[personality]
                    : "Unknown";

                int attr = param.m_attr;
                attributeFallback = (attr >= 0 && attr < AttributeNames.Length)
                    ? AttributeNames[attr]
                    : "Unknown";
            }

            var detailInfo = GetRenderedDetailInfo();
            string natureLabel = GetDetailInfoText(
                detailInfo, true, uDigiviceLibraryDetailInfo.TextIndex.Nature, "Nature")
                .TrimEnd(':', '：')
                .Trim();
            string nature = GetDetailInfoText(
                detailInfo, false, uDigiviceLibraryDetailInfo.TextIndex.Nature, natureFallback);
            string attributeLabel = GetDetailInfoText(
                detailInfo, true, uDigiviceLibraryDetailInfo.TextIndex.Property, "Attribute")
                .TrimEnd(':', '：')
                .Trim();
            string attribute = GetDetailInfoText(
                detailInfo, false, uDigiviceLibraryDetailInfo.TextIndex.Property, attributeFallback);

            return (name, natureLabel, nature, attributeLabel, attribute);
        }

        private uDigiviceLibraryDetailInfo GetRenderedDetailInfo()
        {
            try
            {
                if (_libraryPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Detail info: library panel was null");
                    return null;
                }

                var detailPanel = _libraryPanel.m_DetailPanel;
                if (detailPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Detail info: m_DetailPanel was null");
                    return null;
                }

                var detailInfo = detailPanel.m_Info;
                if (detailInfo == null)
                    DebugLogger.Log($"{LogTag} Detail info: m_DetailPanel.m_Info was null");
                return detailInfo;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Detail info panel read failed: {ex.Message}");
                return null;
            }
        }

        private string GetDetailInfoText(
            uDigiviceLibraryDetailInfo detailInfo,
            bool title,
            uDigiviceLibraryDetailInfo.TextIndex index,
            string fallback)
        {
            string arrayName = title ? "m_TitleText" : "m_ValueText";

            try
            {
                if (detailInfo == null)
                {
                    DebugLogger.Log($"{LogTag} Detail info {index}: m_Info was null for {arrayName}");
                    return fallback;
                }

                var textArray = title ? detailInfo.m_TitleText : detailInfo.m_ValueText;
                if (textArray == null)
                {
                    DebugLogger.Log($"{LogTag} Detail info {index}: {arrayName} was null");
                    return fallback;
                }

                int textIndex = (int)index;
                if (textIndex < 0 || textIndex >= textArray.Length)
                {
                    DebugLogger.Log($"{LogTag} Detail info {index}: index {textIndex} was outside {arrayName}");
                    return fallback;
                }

                var textComponent = textArray[textIndex];
                if (textComponent == null)
                {
                    DebugLogger.Log($"{LogTag} Detail info {index}: {arrayName}[{textIndex}] was null");
                    return fallback;
                }

                string rendered = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(textComponent.text))?.Trim();
                if (string.IsNullOrWhiteSpace(rendered) || TextUtilities.IsPlaceholderText(rendered))
                {
                    DebugLogger.Log($"{LogTag} Detail info {index}: {arrayName}[{textIndex}].text unusable: {TextUtilities.DescribeUnusable(rendered)}");
                    return fallback;
                }

                return rendered;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Detail info {index} {arrayName} read failed: {ex.Message}");
                return fallback;
            }
        }

        private int GetEvoCursor(uDigiviceLibraryDetailPanel.State state)
        {
            // m_Cursor is 0-based
            if (state == uDigiviceLibraryDetailPanel.State.Before)
                return _libraryPanel.m_DetailPanel.m_DetailBefore.m_Cursor;
            else
                return _libraryPanel.m_DetailPanel.m_DetailAfter.m_Cursor;
        }

        private int GetEvoTotal(uDigiviceLibraryDetailPanel.State state)
        {
            if (state == uDigiviceLibraryDetailPanel.State.Before)
                return _libraryPanel.m_DetailPanel.m_DetailBefore.m_CntEvo;
            else
                return _libraryPanel.m_DetailPanel.m_DetailAfter.m_CntEvo;
        }

        private string GetEvoDigimonName(uDigiviceLibraryDetailPanel.State state)
        {
            try
            {
                var panel = state == uDigiviceLibraryDetailPanel.State.Before
                    ? _libraryPanel.m_DetailPanel.m_DetailBefore
                    : (uDigiviceLibraryDetailBefore)_libraryPanel.m_DetailPanel.m_DetailAfter;

                var evoList = panel.m_EvoDigimons;
                int cursor = panel.m_Cursor;

                // m_Cursor is 0-based, use directly as list index
                if (evoList == null || evoList.Count == 0 || cursor < 0 || cursor >= evoList.Count)
                {
                    DebugLogger.Warning($"{LogTag} Evo name: cursor={cursor}, list={evoList?.Count ?? -1}");
                    return null;
                }

                uint digiId = evoList[cursor];
                var param = ParameterDigimonData.GetParam(digiId);
                return param?.GetDefaultName();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Evo name error: {ex.Message}");
                return null;
            }
        }

        private string GetCurrentSkillName()
        {
            try
            {
                var skillPanel = _libraryPanel.m_DetailPanel?.m_DetailSKill;
                if (skillPanel == null) return null;

                // Force caption update
                try { skillPanel.SetSelectSkillCaption(); } catch { }

                var caption = skillPanel.m_SelectSkillCaption;
                if (caption == null) return null;

                // Check attack flags for learned status
                bool hasSkill = false;
                try
                {
                    uint gridIndex = (uint)(skillPanel.m_CursorX * 7 + skillPanel.m_CursorY);
                    var attackFlg = skillPanel.m_DigiAttackFlg;
                    if (attackFlg != null && attackFlg[gridIndex])
                        hasSkill = true;
                    if (!hasSkill)
                    {
                        var releaseFlg = skillPanel.m_ReleaseAttackFlg;
                        if (releaseFlg != null && releaseFlg[gridIndex])
                            hasSkill = true;
                    }
                }
                catch { }

                uint code = caption.GetSkillCode();
                if (code == 0) return "Empty";

                string name = null;
                try
                {
                    var attackData = ParameterAttackData.GetParam(code);
                    name = attackData?.GetName();
                }
                catch { }

                if (string.IsNullOrEmpty(name))
                {
                    name = caption.m_SkillName?.text;
                    if (!string.IsNullOrEmpty(name))
                        name = TextUtilities.StripRichTextTags(name);
                }

                if (string.IsNullOrEmpty(name)) return "Empty";
                return hasSkill ? name : $"{name}, not learned";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"{LogTag} Skill name error: {ex.Message}");
            }
            return null;
        }

        private string GetTabName(uDigiviceLibraryDetailPanel.State state)
        {
            switch (state)
            {
                case uDigiviceLibraryDetailPanel.State.Before: return "Before";
                case uDigiviceLibraryDetailPanel.State.After: return GetRenderedTabName(false, "After");
                case uDigiviceLibraryDetailPanel.State.Skill: return GetRenderedTabName(true, "Skill");
                case uDigiviceLibraryDetailPanel.State.Back: return "Info";
                default: return null;
            }
        }

        private string GetRenderedTabName(bool skill, string fallback)
        {
            try
            {
                if (_libraryPanel == null)
                {
                    DebugLogger.Log($"{LogTag} {fallback} tab: library panel was null");
                    return fallback;
                }

                var detailPanel = _libraryPanel.m_DetailPanel;
                if (detailPanel == null)
                {
                    DebugLogger.Log($"{LogTag} {fallback} tab: m_DetailPanel was null");
                    return fallback;
                }

                var tabText = skill ? detailPanel.m_skillLangText : detailPanel.m_afterLangText;
                string fieldName = skill ? "m_skillLangText" : "m_afterLangText";
                if (tabText == null)
                {
                    DebugLogger.Log($"{LogTag} {fallback} tab: {fieldName} was null");
                    return fallback;
                }

                string rendered = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(tabText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(rendered) || TextUtilities.IsPlaceholderText(rendered))
                {
                    DebugLogger.Log($"{LogTag} {fallback} tab: {fieldName}.text unusable: {TextUtilities.DescribeUnusable(rendered)}");
                    return fallback;
                }

                return rendered;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} {fallback} tab label read failed: {ex.Message}");
                return fallback;
            }
        }
    }
}
