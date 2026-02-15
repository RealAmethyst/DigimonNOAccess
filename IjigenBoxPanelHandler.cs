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
            if (_command != null || _panel == null) return;
            try { _command = _panel.Top2d?.Command; } catch { _command = null; }
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
                        string prefix = _announcedOpen ? "Menu" : "Dimension Box";
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
                    ScreenReader.Say("Confirm registration");
                    break;

                case uIjigenBoxPanel.State.Registration_ConfirmOverwriteMessageStart:
                    ScreenReader.Say("Confirm overwrite registration");
                    break;

                case uIjigenBoxPanel.State.Registration_UnableMessageStart:
                    ScreenReader.Say("Cannot register");
                    break;

                case uIjigenBoxPanel.State.Battle_ConfirmStartMessageStart:
                    ScreenReader.Say("Confirm battle start");
                    break;

                case uIjigenBoxPanel.State.Battle_NotRegistMessageStart:
                    ScreenReader.Say("No Digimon registered");
                    break;

                case uIjigenBoxPanel.State.Battle_AlreadyMessageStart:
                    ScreenReader.Say("Already battled today");
                    break;

                case uIjigenBoxPanel.State.Battle_ResultStart:
                    ScreenReader.Say("Battle results");
                    break;

                case uIjigenBoxPanel.State.Battle_ResultTotalNwpStart:
                    AnnounceResultNwp();
                    break;

                case uIjigenBoxPanel.State.Present_EmptyMessageStart:
                    ScreenReader.Say("No presents");
                    break;

                case uIjigenBoxPanel.State.Open_NetworkErrorMessageStart:
                case uIjigenBoxPanel.State.Battle_NetworkErrorMessageStart:
                    ScreenReader.Say("Network error");
                    break;
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
            // Try reading from UI text first
            try
            {
                if (_command?.m_commandText != null)
                {
                    int idx = (int)cmd;
                    if (idx >= 0 && idx < _command.m_commandText.Length)
                    {
                        string text = _command.m_commandText[idx]?.text;
                        if (!string.IsNullOrEmpty(text))
                            return TextUtilities.StripRichTextTags(text);
                    }
                }
            }
            catch { }

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
                var rankUi = _panel?.Ranking?.Ui2dRanking;
                if (rankUi == null) return;

                int cursor = rankUi.RankCursor;
                _lastRankCursor = cursor;

                var itemArray = rankUi.m_itemArray;
                if (itemArray == null || cursor < 0 || cursor >= itemArray.Length)
                {
                    ScreenReader.Say($"Rank {cursor + 1}");
                    return;
                }

                var itemObj = itemArray[cursor];
                if (itemObj == null)
                {
                    ScreenReader.Say($"Rank {cursor + 1}");
                    return;
                }

                var item = itemObj.GetComponent<uIjigenBoxPanelRankingUi2dRankingScrollViewContentItem>();
                if (item == null)
                {
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
                string rank = item.Ranking?.text;
                if (!string.IsNullOrEmpty(rank))
                    parts.Add($"Rank {TextUtilities.StripRichTextTags(rank)}");
            }
            catch { }

            try
            {
                string name = item.TamerName?.text;
                if (!string.IsNullOrEmpty(name))
                    parts.Add(TextUtilities.StripRichTextTags(name));
            }
            catch { }

            try
            {
                string wins = item.WinCountValue?.text;
                string losses = item.LoseCountValue?.text;
                string battles = item.BattleCountValue?.text;

                if (!string.IsNullOrEmpty(wins))
                    parts.Add($"{wins} wins");
                if (!string.IsNullOrEmpty(losses))
                    parts.Add($"{losses} losses");
                if (!string.IsNullOrEmpty(battles))
                    parts.Add($"{battles} battles");
            }
            catch { }

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
                var cage = _panel?.Registration?.Ui2dRegistration?.Cage;
                if (cage != null)
                {
                    string name = cage.Name?.text;
                    if (!string.IsNullOrEmpty(name))
                    {
                        string cleanName = TextUtilities.StripRichTextTags(name);
                        var parts = new System.Collections.Generic.List<string>();
                        parts.Add($"Cage {cursor + 1}, {cleanName}");

                        string wins = cage.WinCountValue?.text;
                        string losses = cage.LoseCountValue?.text;
                        string battles = cage.BattleCountValue?.text;

                        if (!string.IsNullOrEmpty(wins))
                            parts.Add($"{wins} wins");
                        if (!string.IsNullOrEmpty(losses))
                            parts.Add($"{losses} losses");
                        if (!string.IsNullOrEmpty(battles))
                            parts.Add($"{battles} battles");

                        return string.Join(", ", parts);
                    }
                }
            }
            catch { }

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
                var regUi = _panel?.Registration?.Ui2dRegistration;
                if (regUi == null) return;

                int cursor = regUi.PartnerCursor;
                _lastPartnerCursor = cursor;

                var partners = regUi.Partner;
                if (partners == null || cursor < 0 || cursor >= partners.Length)
                {
                    ScreenReader.Say($"Partner {cursor + 1}");
                    return;
                }

                var partner = partners[cursor];
                if (partner == null)
                {
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
                string atk = partner.AttackValue?.text;
                if (!string.IsNullOrEmpty(atk))
                    parts.Add($"ATK {atk}");
            }
            catch { }

            try
            {
                string def = partner.DefenseValue?.text;
                if (!string.IsNullOrEmpty(def))
                    parts.Add($"DEF {def}");
            }
            catch { }

            try
            {
                string wis = partner.WisdomValue?.text;
                if (!string.IsNullOrEmpty(wis))
                    parts.Add($"WIS {wis}");
            }
            catch { }

            try
            {
                string spd = partner.SpeedValue?.text;
                if (!string.IsNullOrEmpty(spd))
                    parts.Add($"SPD {spd}");
            }
            catch { }

            return string.Join(", ", parts);
        }

        // ── Result ──

        private void AnnounceResultNwp()
        {
            try
            {
                var resultUi = _panel?.Result?.Ui2dResult;
                if (resultUi == null) return;

                string label = resultUi.NwpTotalText?.text;
                string value = resultUi.NwpTotalValue?.text;

                if (!string.IsNullOrEmpty(value))
                {
                    string text = !string.IsNullOrEmpty(label)
                        ? $"{TextUtilities.StripRichTextTags(label)}: {TextUtilities.StripRichTextTags(value)}"
                        : $"Total NWP: {TextUtilities.StripRichTextTags(value)}";
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
                    ScreenReader.Say($"Dimension Box. {tabName}. {tabIdx + 1} of 5");
                    break;

                case ActiveMode.Ranking:
                    AnnounceRankingEntry();
                    break;

                case ActiveMode.Registration:
                    AnnouncePartnerSelection();
                    break;

                case ActiveMode.Battle:
                    ScreenReader.Say("Dimension Box, Battle");
                    break;

                case ActiveMode.Result:
                    AnnounceResultNwp();
                    break;

                default:
                    ScreenReader.Say("Dimension Box");
                    break;
            }
        }
    }
}
