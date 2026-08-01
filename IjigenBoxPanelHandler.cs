using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Dimension Box (Ijigen Box) panel.
    /// An online hub with 5 tabs: Present, Prize Exchange, Battle Ranking,
    /// Digimon Registration, and Battle Start.
    /// </summary>
    public class IjigenBoxPanelHandler : HandlerBase<uIjigenBoxPanel>
    {
        protected override string LogTag => "[IjigenBox]";
        public override int Priority => 65;

        private uIjigenBoxPanel.State _lastState = uIjigenBoxPanel.State.None;
        private uIjigenBoxPanelTop2dCommand.Command _lastCommand = (uIjigenBoxPanelTop2dCommand.Command)(-1);
        private int _lastRankCursor = -1;
        private int _lastPartnerCursor = -1;
        private int _lastCageCursor = -1;
        private bool _announcedOpen;

        // Cached command panel (for tab names - always available after TopMenu_Main)
        private uIjigenBoxPanelTop2dCommand _command;

        // State grouping for routing updates
        private enum ActiveMode
        {
            TopMenu,
            Ranking,
            Registration,
            Battle,
            Result,
            Transition
        }

        public override bool IsOpen()
        {
            try
            {
                var mgm = MainGameManager.m_instance;
                if (mgm == null) return false;

                var panel = mgm.ijigenBoxUI;
                if (panel == null) return false;

                var state = panel.CurrentState;
                if (state == uIjigenBoxPanel.State.None ||
                    state == uIjigenBoxPanel.State.Close_End ||
                    state >= uIjigenBoxPanel.State.Num)
                    return false;

                _panel = panel;
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnOpen()
        {
            _lastState = uIjigenBoxPanel.State.None;
            _lastCommand = (uIjigenBoxPanelTop2dCommand.Command)(-1);
            _lastRankCursor = -1;
            _lastPartnerCursor = -1;
            _lastCageCursor = -1;
            _lastCursor = -1;
            _announcedOpen = false;
            _command = null;

            var state = _panel.CurrentState;
            _lastState = state;

            // Don't announce here - wait for TopMenu_Main after connecting
            DebugLogger.Log($"{LogTag} Opened, state={state}");
        }

        protected override void OnClose()
        {
            _lastState = uIjigenBoxPanel.State.None;
            _lastCommand = (uIjigenBoxPanelTop2dCommand.Command)(-1);
            _lastRankCursor = -1;
            _lastPartnerCursor = -1;
            _lastCageCursor = -1;
            _announcedOpen = false;
            _command = null;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (_panel == null) return;

            var state = _panel.CurrentState;
            var mode = GetActiveMode(state);

            // Check for state changes that warrant announcements
            if (state != _lastState)
            {
                OnStateChanged(_lastState, state, mode);
                _lastState = state;
            }

            // Route cursor tracking based on exact state
            switch (state)
            {
                case uIjigenBoxPanel.State.TopMenu_Main:
                    CheckTabCursorChange();
                    break;
                case uIjigenBoxPanel.State.Ranking_Main:
                    CheckRankCursorChange();
                    break;
                case uIjigenBoxPanel.State.Registration_CageSelect:
                    CheckCageCursorChange();
                    break;
                case uIjigenBoxPanel.State.Registration_DigimonSelect:
                    CheckPartnerCursorChange();
                    break;
            }
        }

        // ── Sub-Panel Access ──

        private void CacheCommandPanel()
        {
            if (_command != null) return;
            if (_panel == null)
            {
                DebugLogger.Log($"{LogTag} Command: panel is null");
                return;
            }

            try
            {
                var top2d = _panel.Top2d;
                if (top2d == null)
                {
                    DebugLogger.Log($"{LogTag} Command: Top2d is null");
                    return;
                }

                _command = top2d.Command;
                if (_command == null)
                    DebugLogger.Log($"{LogTag} Command: Top2d.Command is null");
            }
            catch (System.Exception ex)
            {
                _command = null;
                DebugLogger.Log($"{LogTag} Command read failed: {ex.Message}");
            }
        }

        private string GetDimensionBoxTitle()
        {
            const string fallback = "Dimension Box";

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: panel is null");
                    return fallback;
                }

                var top2d = _panel.Top2d;
                if (top2d == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: Top2d is null");
                    return fallback;
                }

                var headline = top2d.HeadLine;
                if (headline == null)
                {
                    DebugLogger.Log($"{LogTag} Headline: Top2d.HeadLine is null");
                    return fallback;
                }

                return GetRenderedText(headline.Text, "Top2d.HeadLine.Text") ?? fallback;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Headline read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetRenderedText(UnityEngine.UI.Text textComponent, string fieldName)
        {
            if (textComponent == null)
            {
                DebugLogger.Log($"{LogTag} {fieldName} component is null");
                return null;
            }

            string text = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(textComponent.text))?.Trim();
            if (string.IsNullOrWhiteSpace(text) || TextUtilities.IsPlaceholderText(text))
            {
                DebugLogger.Log($"{LogTag} {fieldName}.text is empty");
                return null;
            }

            return text;
        }

        // ── State Grouping ──

        private ActiveMode GetActiveMode(uIjigenBoxPanel.State state)
        {
            string name = state.ToString();

            if (name.StartsWith("TopMenu"))
                return ActiveMode.TopMenu;
            if (name.StartsWith("Ranking"))
                return ActiveMode.Ranking;
            if (name.StartsWith("Registration"))
                return ActiveMode.Registration;
            if (name.StartsWith("Battle"))
                return ActiveMode.Battle;
            if (name.StartsWith("Present") || name.StartsWith("PrizeExchange"))
                return ActiveMode.TopMenu;

            // Open_, Close_, No_Operation are transitions
            return ActiveMode.Transition;
        }

        // ── State Changes ──

        private void OnStateChanged(uIjigenBoxPanel.State oldState, uIjigenBoxPanel.State newState, ActiveMode mode)
        {
            DebugLogger.Log($"{LogTag} State: {oldState} -> {newState}");

            switch (newState)
            {
                case uIjigenBoxPanel.State.TopMenu_Main:
                    CacheCommandPanel();
                    var cmd = GetCurrentCommandCursor();
                    string tabName = GetCurrentTabName();
                    if (!string.IsNullOrEmpty(tabName))
                    {
                        int idx = (int)cmd;
                        string prefix = _announcedOpen ? "Menu" : GetDimensionBoxTitle();
                        ScreenReader.Say($"{prefix}. {tabName}. {idx + 1} of 5");
                        _lastCommand = cmd;
                        _announcedOpen = true;
                    }
                    _lastRankCursor = -1;
                    _lastPartnerCursor = -1;
                    _lastCageCursor = -1;
                    break;

                case uIjigenBoxPanel.State.Ranking_Main:
                    AnnounceRankingEntry();
                    break;

                case uIjigenBoxPanel.State.Registration_CageSelect:
                    AnnounceCageSelection("Select cage slot. ");
                    break;

                case uIjigenBoxPanel.State.Registration_DigimonSelect:
                    AnnouncePartnerSelection();
                    break;

                case uIjigenBoxPanel.State.Registration_ConfirmMessageStart:
                    ScreenReader.Say(GetYesNoMessage(newState, "Confirm registration"));
                    break;

                case uIjigenBoxPanel.State.Registration_ConfirmOverwriteMessageStart:
                    ScreenReader.Say(GetYesNoMessage(newState, "Confirm overwrite registration"));
                    break;

                case uIjigenBoxPanel.State.Registration_UnableMessageStart:
                    ScreenReader.Say(GetCommonMessage(newState, "Cannot register"));
                    break;

                case uIjigenBoxPanel.State.Battle_ConfirmStartMessageStart:
                    ScreenReader.Say(GetYesNoMessage(newState, "Confirm battle start"));
                    break;

                case uIjigenBoxPanel.State.Battle_NotRegistMessageStart:
                    ScreenReader.Say(GetCommonMessage(newState, "No Digimon registered"));
                    break;

                case uIjigenBoxPanel.State.Battle_AlreadyMessageStart:
                    ScreenReader.Say(GetCommonMessage(newState, "Already battled today"));
                    break;

                case uIjigenBoxPanel.State.Battle_ResultStart:
                    ScreenReader.Say(GetCommonMessage(newState, "Battle results"));
                    break;

                case uIjigenBoxPanel.State.Battle_ResultTotalNwpStart:
                    AnnounceResultNwp();
                    break;

                case uIjigenBoxPanel.State.Present_EmptyMessageStart:
                    ScreenReader.Say(GetCommonMessage(newState, "No presents"));
                    break;

                case uIjigenBoxPanel.State.Open_NetworkErrorMessageStart:
                case uIjigenBoxPanel.State.Battle_NetworkErrorMessageStart:
                    ScreenReader.Say(GetCommonMessage(newState, "Network error"));
                    break;
            }
        }

        private string GetYesNoMessage(uIjigenBoxPanel.State state, string fallback)
        {
            try
            {
                var manager = MainGameManager.m_instance;
                if (manager == null)
                {
                    DebugLogger.Log($"{LogTag} {state}: MainGameManager is null");
                    return fallback;
                }

                var window = manager.commonYesNoWindowUI;
                if (window == null)
                {
                    DebugLogger.Log($"{LogTag} {state}: commonYesNoWindowUI is null");
                    return fallback;
                }

                if (!window.m_isOpend)
                {
                    DebugLogger.Log($"{LogTag} {state}: commonYesNoWindowUI is not open");
                    return fallback;
                }

                return GetRenderedText(window.m_message, "commonYesNoWindowUI.m_message") ?? fallback;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} {state}: yes/no message read failed: {ex.Message}");
                return fallback;
            }
        }

        private string GetCommonMessage(uIjigenBoxPanel.State state, string fallback)
        {
            try
            {
                var manager = MainGameManager.m_instance;
                if (manager == null)
                {
                    DebugLogger.Log($"{LogTag} {state}: MainGameManager is null");
                    return fallback;
                }

                var messageManager = manager.MessageManager;
                if (messageManager == null)
                {
                    DebugLogger.Log($"{LogTag} {state}: MessageManager is null");
                    return fallback;
                }

                if (!messageManager.IsFindActive())
                {
                    DebugLogger.Log($"{LogTag} {state}: MessageManager has no active window");
                    return fallback;
                }

                uCommonMessageWindow window = null;
                var center = messageManager.GetCenter();
                if (center != null && center.m_isOpend)
                    window = center;

                if (window == null)
                {
                    var partner0 = messageManager.Get00();
                    if (partner0 != null && partner0.m_isOpend)
                        window = partner0;
                }

                if (window == null)
                {
                    var partner1 = messageManager.Get01();
                    if (partner1 != null && partner1.m_isOpend)
                        window = partner1;
                }

                if (window == null)
                {
                    var rightUp = messageManager.GetRightUp();
                    if (rightUp != null && rightUp.m_isOpend)
                        window = rightUp;
                }

                if (window == null)
                {
                    DebugLogger.Log($"{LogTag} {state}: no open uCommonMessageWindow was reachable");
                    return fallback;
                }

                return GetRenderedText(window.m_label, "active uCommonMessageWindow.m_label") ?? fallback;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} {state}: common message read failed: {ex.Message}");
                return fallback;
            }
        }

        // ── Tab Navigation ──

        private void CheckTabCursorChange()
        {
            if (_command == null) return;

            try
            {
                var cursor = _command.Cursor;
                if (cursor == _lastCommand) return;

                string tabName = GetTabNameForCommand(cursor);
                int idx = (int)cursor;
                ScreenReader.Say($"{tabName}. {idx + 1} of 5");
                DebugLogger.Log($"{LogTag} Tab: {cursor} ({tabName})");
                _lastCommand = cursor;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking tab cursor: {ex.Message}");
            }
        }

        private uIjigenBoxPanelTop2dCommand.Command GetCurrentCommandCursor()
        {
            try
            {
                if (_command != null)
                    return _command.Cursor;
            }
            catch { }
            return uIjigenBoxPanelTop2dCommand.Command.Present;
        }

        private int GetCurrentTabIndex()
        {
            return (int)GetCurrentCommandCursor();
        }

        private string GetCurrentTabName()
        {
            return GetTabNameForCommand(GetCurrentCommandCursor());
        }

        private string GetTabNameForCommand(uIjigenBoxPanelTop2dCommand.Command cmd)
        {
            try
            {
                if (_command == null)
                {
                    DebugLogger.Log($"{LogTag} Tab {cmd}: command panel is null");
                }
                else
                {
                    var commandText = _command.m_commandText;
                    if (commandText == null)
                    {
                        DebugLogger.Log($"{LogTag} Tab {cmd}: m_commandText is null");
                    }
                    else
                    {
                        int idx = (int)cmd;
                        if (idx < 0 || idx >= commandText.Length)
                        {
                            DebugLogger.Log($"{LogTag} Tab {cmd}: index {idx} is outside m_commandText");
                        }
                        else
                        {
                            string text = GetRenderedText(commandText[idx], $"m_commandText[{idx}]");
                            if (!string.IsNullOrWhiteSpace(text))
                                return text;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Tab {cmd}: command text read failed: {ex.Message}");
            }

            // Fallback names
            return cmd switch
            {
                uIjigenBoxPanelTop2dCommand.Command.Present => "Present",
                uIjigenBoxPanelTop2dCommand.Command.PrizeExchange => "Prize Exchange",
                uIjigenBoxPanelTop2dCommand.Command.BattleRanking => "Battle Ranking",
                uIjigenBoxPanelTop2dCommand.Command.DigimonRegist => "Digimon Registration",
                uIjigenBoxPanelTop2dCommand.Command.BattleStart => "Battle Start",
                _ => $"Tab {(int)cmd + 1}"
            };
        }

        // ── Ranking ──

        private void CheckRankCursorChange()
        {
            try
            {
                var rankUi = _panel?.Ranking?.Ui2dRanking;
                if (rankUi == null) return;

                int cursor = rankUi.RankCursor;
                if (cursor == _lastRankCursor) return;

                _lastRankCursor = cursor;
                AnnounceRankingEntry();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking rank cursor: {ex.Message}");
            }
        }

        private void AnnounceRankingEntry()
        {
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Ranking: panel is null");
                    return;
                }

                var ranking = _panel.Ranking;
                if (ranking == null)
                {
                    DebugLogger.Log($"{LogTag} Ranking: panel.Ranking is null");
                    return;
                }

                var rankUi = ranking.Ui2dRanking;
                if (rankUi == null)
                {
                    DebugLogger.Log($"{LogTag} Ranking: Ranking.Ui2dRanking is null");
                    return;
                }

                int cursor = rankUi.RankCursor;
                _lastRankCursor = cursor;

                var itemArray = rankUi.m_itemArray;
                if (itemArray == null)
                {
                    DebugLogger.Log($"{LogTag} Ranking: m_itemArray is null");
                    ScreenReader.Say($"Rank {cursor + 1}");
                    return;
                }

                if (cursor < 0 || cursor >= itemArray.Length)
                {
                    DebugLogger.Log($"{LogTag} Ranking: cursor {cursor} is outside m_itemArray");
                    ScreenReader.Say($"Rank {cursor + 1}");
                    return;
                }

                var itemObj = itemArray[cursor];
                if (itemObj == null)
                {
                    DebugLogger.Log($"{LogTag} Ranking: m_itemArray[{cursor}] is null");
                    ScreenReader.Say($"Rank {cursor + 1}");
                    return;
                }

                var item = itemObj.GetComponent<uIjigenBoxPanelRankingUi2dRankingScrollViewContentItem>();
                if (item == null)
                {
                    DebugLogger.Log($"{LogTag} Ranking: item {cursor} has no ranking content component");
                    ScreenReader.Say($"Rank {cursor + 1}");
                    return;
                }

                string announcement = BuildRankingAnnouncement(item);
                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Ranking cursor: {cursor}, {announcement}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error announcing ranking: {ex.Message}");
            }
        }

        private string BuildRankingAnnouncement(uIjigenBoxPanelRankingUi2dRankingScrollViewContentItem item)
        {
            var parts = new System.Collections.Generic.List<string>();

            try
            {
                string rank = GetRenderedText(item.Ranking, "Ranking");
                if (!string.IsNullOrWhiteSpace(rank))
                {
                    string rankUnit = GetRenderedText(item.RankingUnit, "RankingUnit") ?? "Rank";
                    parts.Add($"{rankUnit} {rank}");
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Ranking label read failed: {ex.Message}");
            }

            try
            {
                string name = item.TamerName?.text;
                if (!string.IsNullOrEmpty(name))
                    parts.Add(TextUtilities.StripRichTextTags(name));
            }
            catch { }

            try
            {
                string wins = GetRenderedText(item.WinCountValue, "WinCountValue");
                string losses = GetRenderedText(item.LoseCountValue, "LoseCountValue");
                string battles = GetRenderedText(item.BattleCountValue, "BattleCountValue");

                if (!string.IsNullOrWhiteSpace(wins))
                    parts.Add($"{wins} {GetRenderedText(item.WinCountText, "WinCountText") ?? "wins"}");
                if (!string.IsNullOrWhiteSpace(losses))
                    parts.Add($"{losses} {GetRenderedText(item.LoseCountText, "LoseCountText") ?? "losses"}");
                if (!string.IsNullOrWhiteSpace(battles))
                    parts.Add($"{battles} {GetRenderedText(item.BattleCountText, "BattleCountText") ?? "battles"}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Ranking count labels read failed: {ex.Message}");
            }

            if (parts.Count == 0)
                return "Empty ranking entry";

            return string.Join(", ", parts);
        }

        // ── Registration (Cage Selection) ──

        private int GetCageCursor()
        {
            // Read fresh from panel hierarchy each time (sub-panels may not be loaded at OnOpen)
            try
            {
                var reg = _panel?.Registration;
                if (reg == null) return -1;
                var ui3d = reg.Ui3d;
                if (ui3d == null) return -1;
                return ui3d.CageCursor;
            }
            catch { return -1; }
        }

        private void CheckCageCursorChange()
        {
            int cursor = GetCageCursor();
            if (cursor < 0 || cursor == _lastCageCursor) return;

            _lastCageCursor = cursor;
            AnnounceCageSelection(null);
        }

        private void AnnounceCageSelection(string prefix)
        {
            int cursor = GetCageCursor();
            if (cursor < 0) cursor = 0;
            _lastCageCursor = cursor;

            string cageInfo = BuildCageAnnouncement(cursor);
            string announcement = !string.IsNullOrEmpty(prefix)
                ? $"{prefix}{cageInfo}"
                : cageInfo;

            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Cage cursor: {cursor}, {cageInfo}");
        }

        private string BuildCageAnnouncement(int cursor)
        {
            // Try reading the cage's 2D display (name, battle stats)
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Cage: panel is null");
                    return $"Cage {cursor + 1}, Empty";
                }

                var registration = _panel.Registration;
                if (registration == null)
                {
                    DebugLogger.Log($"{LogTag} Cage: panel.Registration is null");
                    return $"Cage {cursor + 1}, Empty";
                }

                var registrationUi = registration.Ui2dRegistration;
                if (registrationUi == null)
                {
                    DebugLogger.Log($"{LogTag} Cage: Registration.Ui2dRegistration is null");
                    return $"Cage {cursor + 1}, Empty";
                }

                var cage = registrationUi.Cage;
                if (cage == null)
                {
                    DebugLogger.Log($"{LogTag} Cage: Ui2dRegistration.Cage is null");
                    return $"Cage {cursor + 1}, Empty";
                }

                string cleanName = GetRenderedText(cage.Name, "Cage.Name");
                if (!string.IsNullOrWhiteSpace(cleanName))
                {
                    var parts = new System.Collections.Generic.List<string>();
                    parts.Add($"Cage {cursor + 1}, {cleanName}");

                    string wins = GetRenderedText(cage.WinCountValue, "Cage.WinCountValue");
                    string losses = GetRenderedText(cage.LoseCountValue, "Cage.LoseCountValue");
                    string battles = GetRenderedText(cage.BattleCountValue, "Cage.BattleCountValue");

                    if (!string.IsNullOrWhiteSpace(wins))
                        parts.Add($"{wins} {GetRenderedText(cage.WinCountText, "Cage.WinCountText") ?? "wins"}");
                    if (!string.IsNullOrWhiteSpace(losses))
                        parts.Add($"{losses} {GetRenderedText(cage.LoseCountText, "Cage.LoseCountText") ?? "losses"}");
                    if (!string.IsNullOrWhiteSpace(battles))
                        parts.Add($"{battles} {GetRenderedText(cage.BattleCountText, "Cage.BattleCountText") ?? "battles"}");

                    return string.Join(", ", parts);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Cage announcement read failed: {ex.Message}");
            }

            return $"Cage {cursor + 1}, Empty";
        }

        // ── Registration (Partner Selection) ──

        private void CheckPartnerCursorChange()
        {
            try
            {
                var regUi = _panel?.Registration?.Ui2dRegistration;
                if (regUi == null) return;

                int cursor = regUi.PartnerCursor;
                if (cursor == _lastPartnerCursor) return;

                _lastPartnerCursor = cursor;
                AnnouncePartnerSelection();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking partner cursor: {ex.Message}");
            }
        }

        private void AnnouncePartnerSelection()
        {
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Partner: panel is null");
                    return;
                }

                var registration = _panel.Registration;
                if (registration == null)
                {
                    DebugLogger.Log($"{LogTag} Partner: panel.Registration is null");
                    return;
                }

                var regUi = registration.Ui2dRegistration;
                if (regUi == null)
                {
                    DebugLogger.Log($"{LogTag} Partner: Registration.Ui2dRegistration is null");
                    return;
                }

                int cursor = regUi.PartnerCursor;
                _lastPartnerCursor = cursor;

                var partners = regUi.Partner;
                if (partners == null)
                {
                    DebugLogger.Log($"{LogTag} Partner: Ui2dRegistration.Partner is null");
                    ScreenReader.Say($"Partner {cursor + 1}");
                    return;
                }

                if (cursor < 0 || cursor >= partners.Length)
                {
                    DebugLogger.Log($"{LogTag} Partner: cursor {cursor} is outside Partner");
                    ScreenReader.Say($"Partner {cursor + 1}");
                    return;
                }

                var partner = partners[cursor];
                if (partner == null)
                {
                    DebugLogger.Log($"{LogTag} Partner: Partner[{cursor}] is null");
                    ScreenReader.Say($"Partner {cursor + 1}");
                    return;
                }

                string announcement = BuildPartnerAnnouncement(partner, cursor);
                ScreenReader.Say(announcement);
                DebugLogger.Log($"{LogTag} Partner cursor: {cursor}, {announcement}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error announcing partner: {ex.Message}");
            }
        }

        private string BuildPartnerAnnouncement(uIjigenBoxPanelRegistrationUi2dRegistrationPartner partner, int index)
        {
            var parts = new System.Collections.Generic.List<string>();

            try
            {
                string name = partner.Name?.text;
                if (!string.IsNullOrEmpty(name))
                    parts.Add(TextUtilities.StripRichTextTags(name));
                else
                    parts.Add($"Partner {index + 1}");
            }
            catch { parts.Add($"Partner {index + 1}"); }

            try
            {
                string hp = partner.HpValue?.text;
                if (!string.IsNullOrEmpty(hp))
                    parts.Add($"HP {hp}");
            }
            catch { }

            try
            {
                string mp = partner.MpValue?.text;
                if (!string.IsNullOrEmpty(mp))
                    parts.Add($"MP {mp}");
            }
            catch { }

            try
            {
                string atk = GetRenderedText(partner.AttackValue, "Partner.AttackValue");
                if (!string.IsNullOrWhiteSpace(atk))
                    parts.Add($"{GetRenderedText(partner.AttackHeadLine, "Partner.AttackHeadLine") ?? "ATK"} {atk}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Partner attack label read failed: {ex.Message}");
            }

            try
            {
                string def = GetRenderedText(partner.DefenseValue, "Partner.DefenseValue");
                if (!string.IsNullOrWhiteSpace(def))
                    parts.Add($"{GetRenderedText(partner.DefenseHeadLine, "Partner.DefenseHeadLine") ?? "DEF"} {def}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Partner defense label read failed: {ex.Message}");
            }

            try
            {
                string wis = GetRenderedText(partner.WisdomValue, "Partner.WisdomValue");
                if (!string.IsNullOrWhiteSpace(wis))
                    parts.Add($"{GetRenderedText(partner.WisdomHeadLine, "Partner.WisdomHeadLine") ?? "WIS"} {wis}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Partner wisdom label read failed: {ex.Message}");
            }

            try
            {
                string spd = GetRenderedText(partner.SpeedValue, "Partner.SpeedValue");
                if (!string.IsNullOrWhiteSpace(spd))
                    parts.Add($"{GetRenderedText(partner.SpeedHeadLine, "Partner.SpeedHeadLine") ?? "SPD"} {spd}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Partner speed label read failed: {ex.Message}");
            }

            return string.Join(", ", parts);
        }

        // ── Result ──

        private void AnnounceResultNwp()
        {
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} NWP result: panel is null");
                    return;
                }

                var result = _panel.Result;
                if (result == null)
                {
                    DebugLogger.Log($"{LogTag} NWP result: Result is null");
                    return;
                }

                var resultUi = result.Ui2dResult;
                if (resultUi == null)
                {
                    DebugLogger.Log($"{LogTag} NWP result: Result.Ui2dResult is null");
                    return;
                }

                string label = GetRenderedText(resultUi.NwpTotalText, "NwpTotalText");
                string value = GetRenderedText(resultUi.NwpTotalValue, "NwpTotalValue");

                if (!string.IsNullOrWhiteSpace(value))
                {
                    string text = !string.IsNullOrWhiteSpace(label)
                        ? $"{label}: {value}"
                        : $"Total NWP: {value}";
                    ScreenReader.Say(text);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading NWP result: {ex.Message}");
            }
        }

        // ── Status ──

        public override void AnnounceStatus()
        {
            if (!IsOpen()) return;

            var state = _panel.CurrentState;
            var mode = GetActiveMode(state);

            switch (mode)
            {
                case ActiveMode.TopMenu:
                    string tabName = GetCurrentTabName();
                    int tabIdx = GetCurrentTabIndex();
                    ScreenReader.Say($"{GetDimensionBoxTitle()}. {tabName}. {tabIdx + 1} of 5");
                    break;

                case ActiveMode.Ranking:
                    AnnounceRankingEntry();
                    break;

                case ActiveMode.Registration:
                    AnnouncePartnerSelection();
                    break;

                case ActiveMode.Battle:
                    ScreenReader.Say($"{GetDimensionBoxTitle()}, Battle");
                    break;

                case ActiveMode.Result:
                    AnnounceResultNwp();
                    break;

                default:
                    ScreenReader.Say(GetDimensionBoxTitle());
                    break;
            }
        }
    }
}
