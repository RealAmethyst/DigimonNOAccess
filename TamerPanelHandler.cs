using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the Tamer menu, including:
    /// - Status view with tamer stats
    /// - SkillCheck (view learned skills)
    /// - SkillGet (buy new skills with TP)
    /// </summary>
    public class TamerPanelHandler : IAccessibilityHandler
    {
        public int Priority => 70;

        private uDigiviceTamerPanel _panel;
        private bool _wasActive = false;
        private uDigiviceTamerPanel.State _lastState = uDigiviceTamerPanel.State.None;

        // Status panel tracking
        private int _lastStatusCommand = -1;

        // SkillGet tracking
        private int _lastSkillGetTab = -1;
        private int _lastSkillGetCategory = -1;
        private string _lastSkillGetName = "";
        private bool _wasOnSkillScreen = false;

        // SkillCheck tracking
        private int _lastSkillCheckSelectNo = -1;

        public bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uDigiviceTamerPanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_CurrentState;
                return state != uDigiviceTamerPanel.State.None && state != uDigiviceTamerPanel.State.Close;
            }
            catch
            {
                return false;
            }
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
            }

            _wasActive = isActive;
        }

        private void OnOpen()
        {
            _lastState = uDigiviceTamerPanel.State.None;
            _lastStatusCommand = -1;
            _lastSkillGetTab = -1;
            _lastSkillGetCategory = -1;
            _lastSkillGetName = "";
            _wasOnSkillScreen = false;
            _lastSkillCheckSelectNo = -1;

            if (_panel == null)
                return;

            var state = _panel.m_CurrentState;
            _lastState = state;

            string stateName = GetStateName(state);
            string announcement = $"Tamer menu, {stateName}";

            if (state == uDigiviceTamerPanel.State.Status)
            {
                announcement += ". " + GetStatusInfo();
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[TamerPanel] Opened, state={state}");
        }

        private void OnClose()
        {
            _panel = null;
            _lastState = uDigiviceTamerPanel.State.None;
            DebugLogger.Log("[TamerPanel] Closed");
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var currentState = _panel.m_CurrentState;

            if (currentState != _lastState)
            {
                HandleStateChange(currentState);
                _lastState = currentState;
            }
            else
            {
                switch (currentState)
                {
                    case uDigiviceTamerPanel.State.Status:
                        CheckStatusChange();
                        break;
                    case uDigiviceTamerPanel.State.SkillGet:
                        CheckSkillGetChange();
                        break;
                    case uDigiviceTamerPanel.State.SkillCheck:
                        CheckSkillCheckChange();
                        break;
                }
            }
        }

        private void HandleStateChange(uDigiviceTamerPanel.State newState)
        {
            string stateName = GetStateName(newState);
            string announcement = stateName;

            // Reset sub-state tracking
            _lastStatusCommand = -1;
            _lastSkillGetTab = -1;
            _lastSkillGetCategory = -1;
            _lastSkillGetName = "";
            _wasOnSkillScreen = false;
            _lastSkillCheckSelectNo = -1;

            if (newState == uDigiviceTamerPanel.State.Status)
            {
                announcement += ". " + GetStatusInfo();
            }
            else if (newState == uDigiviceTamerPanel.State.SkillGet)
            {
                announcement += ". " + GetSkillGetInfo();
            }
            else if (newState == uDigiviceTamerPanel.State.SkillCheck)
            {
                announcement += ". " + GetSkillCheckInfo();
            }

            ScreenReader.Say(announcement);
            DebugLogger.Log($"[TamerPanel] State changed to {newState}");
        }

        #region Status State

        private void CheckStatusChange()
        {
            try
            {
                var statusPanel = _panel?.m_status;
                if (statusPanel == null)
                    return;

                int currentCommand = (int)statusPanel.GetSelectIndex();

                if (currentCommand != _lastStatusCommand && _lastStatusCommand >= 0)
                {
                    string commandName = GetStatusCommandName(currentCommand);
                    ScreenReader.Say($"{commandName}, {currentCommand + 1} of 2");
                }
                _lastStatusCommand = currentCommand;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in CheckStatusChange: {ex.Message}");
            }
        }

        private string GetStatusInfo()
        {
            try
            {
                var statusPanel = _panel?.m_status;
                if (statusPanel == null)
                    return "";

                int commandIndex = (int)statusPanel.GetSelectIndex();
                _lastStatusCommand = commandIndex;
                string commandName = GetStatusCommandName(commandIndex);

                string tp = GetTextSafe(statusPanel.m_StatusTPValue);
                string level = GetTextSafe(statusPanel.m_StatusLevel);
                string skills = GetTextSafe(statusPanel.m_StatusLearnedSkillCurrentValue);
                string maxSkills = GetTextSafe(statusPanel.m_StatusLearnedSkillMaxValue);
                string bits = GetTextSafe(statusPanel.m_StatusHaveBitValue);

                string stats = "";
                if (!string.IsNullOrEmpty(level))
                    stats += $"Level {level}. ";
                if (!string.IsNullOrEmpty(tp))
                    stats += $"TP {tp}. ";
                if (!string.IsNullOrEmpty(skills) && !string.IsNullOrEmpty(maxSkills))
                    stats += $"Skills {skills} of {maxSkills}. ";
                if (!string.IsNullOrEmpty(bits))
                    stats += $"Bits {bits}. ";

                return $"{stats}{commandName}, {commandIndex + 1} of 2";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in GetStatusInfo: {ex.Message}");
                return "";
            }
        }

        private string GetStatusCommandName(int index)
        {
            return index switch
            {
                0 => "Skill Check",
                1 => "Skill Get",
                _ => AnnouncementBuilder.FallbackItem("Option", index)
            };
        }

        #endregion

        #region SkillGet State

        private void CheckSkillGetChange()
        {
            try
            {
                var skillGetPanel = _panel?.m_skillGet;
                if (skillGetPanel == null)
                    return;

                var mainWindow = skillGetPanel.m_uDigiviceTamerPanelSkill_Get_MainWindow;
                if (mainWindow == null)
                    return;

                int currentTab = mainWindow.m_SelectedTabIndex;

                var tabPanel = GetCurrentTabPanel(mainWindow, currentTab);
                if (tabPanel == null)
                    return;

                var tabState = tabPanel.m_State;
                bool onSkillScreen = (tabState == uGetSkillPanelBase.State.SELECT_SKILL);
                bool onCategoryScreen = (tabState == uGetSkillPanelBase.State.SELECT_KIND);

                string currentSkillName = GetTextSafe(skillGetPanel.m_SelectSklName);
                int currentCategory = GetCurrentCategoryIndex(mainWindow, currentTab);

                // Detect screen transitions
                if (onSkillScreen && !_wasOnSkillScreen)
                {
                    string skillInfo = GetSkillAnnouncementText(mainWindow, skillGetPanel, tabPanel, currentTab, currentCategory, currentSkillName);
                    ScreenReader.Say($"Skills. {skillInfo}");
                    _lastSkillGetName = currentSkillName;
                }
                else if (onCategoryScreen && _wasOnSkillScreen)
                {
                    string tabName = GetSkillTabName(currentTab);
                    string announcement = $"Category selection. {tabName} tab";
                    if (currentCategory >= 0)
                    {
                        string categoryName = GetCategoryName(currentTab, currentCategory);
                        int categoryCount = GetCategoryCount(currentTab);
                        announcement += $". {AnnouncementBuilder.CursorPosition(categoryName, currentCategory, categoryCount)}";
                    }
                    ScreenReader.Say(announcement);
                    _lastSkillGetCategory = currentCategory;
                }
                _wasOnSkillScreen = onSkillScreen;

                // Tab changes
                if (currentTab != _lastSkillGetTab && _lastSkillGetTab >= 0)
                {
                    string tabName = GetSkillTabName(currentTab);
                    string announcement = $"{tabName} tab, {currentTab + 1} of 4";
                    if (onCategoryScreen && currentCategory >= 0)
                    {
                        string categoryName = GetCategoryName(currentTab, currentCategory);
                        int categoryCount = GetCategoryCount(currentTab);
                        announcement += $". {AnnouncementBuilder.CursorPosition(categoryName, currentCategory, categoryCount)}";
                    }
                    ScreenReader.Say(announcement);
                    _lastSkillGetCategory = currentCategory;
                }

                // Category changes on category screen
                if (onCategoryScreen)
                {
                    if (currentCategory >= 0 && currentCategory != _lastSkillGetCategory && _lastSkillGetCategory >= 0)
                    {
                        string categoryName = GetCategoryName(currentTab, currentCategory);
                        int categoryCount = GetCategoryCount(currentTab);
                        ScreenReader.Say(AnnouncementBuilder.CursorPosition(categoryName, currentCategory, categoryCount));
                    }
                    _lastSkillGetCategory = currentCategory;
                }
                // Skill changes on skill screen
                else if (onSkillScreen)
                {
                    if (currentSkillName != _lastSkillGetName)
                    {
                        AnnounceSelectedSkillInGet(mainWindow, skillGetPanel, tabPanel, currentTab, currentCategory, currentSkillName);
                    }
                    _lastSkillGetName = currentSkillName;
                }

                _lastSkillGetTab = currentTab;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in CheckSkillGetChange: {ex.Message}");
            }
        }

        private uGetSkillPanelBase GetCurrentTabPanel(uDigiviceTamerPanelSkill_Get_MainWindow mainWindow, int tabIndex)
        {
            return tabIndex switch
            {
                0 => mainWindow.m_uDigiviceTamerSKillPanel_GetBasic,
                1 => mainWindow.m_uDigiviceTamerSKillPanel_GetTrainer,
                2 => mainWindow.m_uDigiviceTamerSKillPanel_GetSurvivor,
                3 => mainWindow.m_uDigiviceTamerSKillPanel_GetCommander,
                _ => null
            };
        }

        private string GetSkillAnnouncementText(uDigiviceTamerPanelSkill_Get_MainWindow mainWindow, uDigiviceTamerPanelSkill_Get skillGetPanel, uGetSkillPanelBase tabPanel, int tabIndex, int categoryIndex, string skillName)
        {
            string tpCost = GetTextSafe(skillGetPanel.m_SelectSklTpValue);
            string description = GetTextSafe(skillGetPanel.m_SelectSklDescription);

            bool isLearned = false;
            try
            {
                var skill = mainWindow.GetSelectedTamerSkill();
                if (skill != ParameterTamerSkill.TamerSkill.NONE)
                {
                    isLearned = ParameterTamerSkill.IsLearnedSkill(skill);
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error checking skill learned status: {ex.Message}");
            }

            string announcement = skillName;

            if (isLearned)
                announcement += ", learned";
            else if (!string.IsNullOrEmpty(tpCost))
                announcement += $", not learned, costs {tpCost} TP";
            else
                announcement += ", not learned";

            if (!string.IsNullOrEmpty(description))
                announcement += $". {description}";

            // Add skill index
            try
            {
                int skillIndex = GetSkillCursorIndex(tabPanel, tabIndex);
                var subKind = GetSubKindFromTabAndCategory(tabIndex, categoryIndex);
                var skillList = ParameterTamerSkill.GetSkillInSubKind(subKind);
                if (skillList != null && skillList.Count > 0 && skillIndex >= 0)
                {
                    announcement += $", {skillIndex + 1} of {skillList.Count}";
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error getting skill index: {ex.Message}");
            }

            return announcement;
        }

        private void AnnounceSelectedSkillInGet(uDigiviceTamerPanelSkill_Get_MainWindow mainWindow, uDigiviceTamerPanelSkill_Get skillGetPanel, uGetSkillPanelBase tabPanel, int tabIndex, int categoryIndex, string skillName)
        {
            string announcement = GetSkillAnnouncementText(mainWindow, skillGetPanel, tabPanel, tabIndex, categoryIndex, skillName);
            ScreenReader.Say(announcement);
        }

        private int GetSkillCursorIndex(uGetSkillPanelBase tabPanel, int tabIndex)
        {
            try
            {
                // Each specific panel type has m_SkillCursorIndex
                return tabIndex switch
                {
                    0 => ((uDigiviceTamerSKillPanel_GetBasic)tabPanel).m_SkillCursorIndex,
                    1 => ((uDigiviceTamerSKillPanel_GetTrainer)tabPanel).m_SkillCursorIndex,
                    2 => ((uDigiviceTamerSKillPanel_GetSurvivor)tabPanel).m_SkillCursorIndex,
                    3 => ((uDigiviceTamerSKillPanel_GetCommander)tabPanel).m_SkillCursorIndex,
                    _ => -1
                };
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in GetSkillCursorIndex: {ex.Message}");
                return -1;
            }
        }

        private ParameterTamerSkill.TamerSkillSubKind GetSubKindFromTabAndCategory(int tabIndex, int categoryIndex)
        {
            // Map tab and category indices to TamerSkillSubKind enum
            return tabIndex switch
            {
                0 => categoryIndex switch // Basic
                {
                    0 => ParameterTamerSkill.TamerSkillSubKind.TREAT,
                    1 => ParameterTamerSkill.TamerSkillSubKind.LIFE,
                    2 => ParameterTamerSkill.TamerSkillSubKind.CARRIER,
                    3 => ParameterTamerSkill.TamerSkillSubKind.REVERSE,
                    _ => ParameterTamerSkill.TamerSkillSubKind.NONE
                },
                1 => categoryIndex switch // Trainer
                {
                    0 => ParameterTamerSkill.TamerSkillSubKind.TEACHER,
                    1 => ParameterTamerSkill.TamerSkillSubKind.LIFECARE,
                    2 => ParameterTamerSkill.TamerSkillSubKind.EVOLUTE,
                    _ => ParameterTamerSkill.TamerSkillSubKind.NONE
                },
                2 => categoryIndex switch // Survivor
                {
                    0 => ParameterTamerSkill.TamerSkillSubKind.FINDER,
                    1 => ParameterTamerSkill.TamerSkillSubKind.EXTRACTOR,
                    2 => ParameterTamerSkill.TamerSkillSubKind.CAMPER,
                    3 => ParameterTamerSkill.TamerSkillSubKind.WALKER,
                    _ => ParameterTamerSkill.TamerSkillSubKind.NONE
                },
                3 => categoryIndex switch // Commander
                {
                    0 => ParameterTamerSkill.TamerSkillSubKind.ORDER,
                    1 => ParameterTamerSkill.TamerSkillSubKind.TACTICS,
                    2 => ParameterTamerSkill.TamerSkillSubKind.ITEMTHROW,
                    3 => ParameterTamerSkill.TamerSkillSubKind.DROP,
                    4 => ParameterTamerSkill.TamerSkillSubKind.LEARNING,
                    _ => ParameterTamerSkill.TamerSkillSubKind.NONE
                },
                _ => ParameterTamerSkill.TamerSkillSubKind.NONE
            };
        }

        private int GetCurrentCategoryIndex(uDigiviceTamerPanelSkill_Get_MainWindow mainWindow, int tabIndex)
        {
            try
            {
                var tabPanel = GetCurrentTabPanel(mainWindow, tabIndex);
                if (tabPanel != null)
                {
                    return tabPanel.GetNextCursorIndex();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in GetCurrentCategoryIndex: {ex.Message}");
            }
            return -1;
        }

        private string GetCategoryName(int tabIndex, int categoryIndex)
        {
            return tabIndex switch
            {
                0 => categoryIndex switch // Basic
                {
                    0 => "Treat",
                    1 => "Life",
                    2 => "Carrier",
                    3 => "Reverse",
                    _ => AnnouncementBuilder.FallbackItem("Category", categoryIndex)
                },
                1 => categoryIndex switch // Trainer
                {
                    0 => "Teacher",
                    1 => "Lifecare",
                    2 => "Evolute",
                    _ => AnnouncementBuilder.FallbackItem("Category", categoryIndex)
                },
                2 => categoryIndex switch // Survivor
                {
                    0 => "Finder",
                    1 => "Extractor",
                    2 => "Camper",
                    3 => "Walker",
                    _ => AnnouncementBuilder.FallbackItem("Category", categoryIndex)
                },
                3 => categoryIndex switch // Commander
                {
                    0 => "Order",
                    1 => "Tactics",
                    2 => "Item Throw",
                    3 => "Drop",
                    4 => "Learning",
                    _ => AnnouncementBuilder.FallbackItem("Category", categoryIndex)
                },
                _ => AnnouncementBuilder.FallbackItem("Category", categoryIndex)
            };
        }

        private int GetCategoryCount(int tabIndex)
        {
            return tabIndex switch
            {
                0 => 4, // Basic
                1 => 3, // Trainer
                2 => 4, // Survivor
                3 => 5, // Commander
                _ => 1
            };
        }

        private string GetSkillGetInfo()
        {
            try
            {
                var skillGetPanel = _panel?.m_skillGet;
                if (skillGetPanel == null)
                    return "";

                var mainWindow = skillGetPanel.m_uDigiviceTamerPanelSkill_Get_MainWindow;
                if (mainWindow == null)
                    return "";

                _lastSkillGetTab = mainWindow.m_SelectedTabIndex;
                _wasOnSkillScreen = false;

                int currentCategory = GetCurrentCategoryIndex(mainWindow, _lastSkillGetTab);
                _lastSkillGetCategory = currentCategory;

                string tabName = GetSkillTabName(_lastSkillGetTab);
                string info = $"{tabName} tab, {_lastSkillGetTab + 1} of 4";

                if (currentCategory >= 0)
                {
                    string categoryName = GetCategoryName(_lastSkillGetTab, currentCategory);
                    int categoryCount = GetCategoryCount(_lastSkillGetTab);
                    info += $". {AnnouncementBuilder.CursorPosition(categoryName, currentCategory, categoryCount)}";
                }

                return info;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in GetSkillGetInfo: {ex.Message}");
                return "";
            }
        }

        private string GetSkillTabName(int index)
        {
            return index switch
            {
                0 => "Basic",
                1 => "Trainer",
                2 => "Survivor",
                3 => "Commander",
                _ => AnnouncementBuilder.FallbackItem("Tab", index)
            };
        }

        #endregion

        #region SkillCheck State

        private void CheckSkillCheckChange()
        {
            try
            {
                var skillCheckPanel = _panel?.m_skillCheck;
                if (skillCheckPanel == null)
                    return;

                var tamerSkillPanel = skillCheckPanel.m_uTamerSkillPanel;
                if (tamerSkillPanel == null)
                    return;

                int currentSelectNo = tamerSkillPanel.m_selectNo;

                if (currentSelectNo != _lastSkillCheckSelectNo)
                {
                    // Check if this is the automatic follow-up announcement after entering menu
                    bool isInitialAnnouncement = (_lastSkillCheckSelectNo == -1);

                    // Get the selected skill from the panel
                    var skill = tamerSkillPanel.GetCurrentSelectSkill();
                    string skillName = GetTamerSkillName(skill);
                    string description = GetTextSafe(skillCheckPanel.DescriptionText);

                    // Get skill count for index
                    int total = 0;
                    var skillList = tamerSkillPanel.m_ListSklData;
                    if (skillList != null)
                        total = skillList.Count;

                    if (!string.IsNullOrEmpty(skillName))
                    {
                        string announcement = skillName;

                        // Add description if valid (filter out "None" placeholder)
                        if (!string.IsNullOrEmpty(description) && description != "None")
                            announcement += $". {description}";

                        // Add index
                        if (total > 0)
                            announcement += $", {currentSelectNo + 1} of {total}";

                        // Queue initial announcement so it doesn't interrupt menu opening speech
                        if (isInitialAnnouncement)
                            ScreenReader.SayQueued(announcement);
                        else
                            ScreenReader.Say(announcement);
                    }

                    _lastSkillCheckSelectNo = currentSelectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in CheckSkillCheckChange: {ex.Message}");
            }
        }

        private string GetSkillCheckInfo()
        {
            try
            {
                var skillCheckPanel = _panel?.m_skillCheck;
                if (skillCheckPanel == null)
                    return "";

                var tamerSkillPanel = skillCheckPanel.m_uTamerSkillPanel;

                // Set to -1 so CheckSkillCheckChange() will announce full info on next frame
                // (description isn't populated until game's Update() runs)
                _lastSkillCheckSelectNo = -1;

                string tp = GetTextSafe(skillCheckPanel.TPValue);

                // Get skill count
                int total = 0;
                if (tamerSkillPanel != null)
                {
                    var skillList = tamerSkillPanel.m_ListSklData;
                    if (skillList != null)
                        total = skillList.Count;
                }

                string info = "Learned skills";
                if (!string.IsNullOrEmpty(tp))
                    info += $", TP {tp}";
                if (total > 0)
                    info += $", {total} skills";

                return info;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in GetSkillCheckInfo: {ex.Message}");
                return "";
            }
        }

        #endregion

        #region Helpers

        private string GetTamerSkillName(ParameterTamerSkill.TamerSkill skill)
        {
            return skill switch
            {
                ParameterTamerSkill.TamerSkill.NONE => "",
                ParameterTamerSkill.TamerSkill.MOOD_UP => "Mood Up",
                ParameterTamerSkill.TamerSkill.BOUNDS_UP => "Bounds Up",
                ParameterTamerSkill.TamerSkill.CONFIDENCE_UP => "Confidence Up",
                ParameterTamerSkill.TamerSkill.MIND_MASTER => "Mind Master",
                ParameterTamerSkill.TamerSkill.LONGEVITY_EVOLUTION => "Longevity Evolution",
                ParameterTamerSkill.TamerSkill.LIFE_UP => "Life Up",
                ParameterTamerSkill.TamerSkill.LIFE_LARGE_UP => "Life Large Up",
                ParameterTamerSkill.TamerSkill.HIGH_CARRY => "High Carry",
                ParameterTamerSkill.TamerSkill.MEGA_CARRY => "Mega Carry",
                ParameterTamerSkill.TamerSkill.GIGA_CARRY => "Giga Carry",
                ParameterTamerSkill.TamerSkill.MAX_CARRY => "Max Carry",
                ParameterTamerSkill.TamerSkill.DEJITAMA_SHINE => "Dejitama Shine",
                ParameterTamerSkill.TamerSkill.PASSION_INJECTION => "Passion Injection",
                ParameterTamerSkill.TamerSkill.LOVE_INJECTION => "Love Injection",
                ParameterTamerSkill.TamerSkill.BLESSING_HEAVEN => "Blessing Heaven",
                ParameterTamerSkill.TamerSkill.POWER_TRAINER => "Power Trainer",
                ParameterTamerSkill.TamerSkill.INTELLIGENT_TRAINER => "Intelligent Trainer",
                ParameterTamerSkill.TamerSkill.TOUGHNESS_TRAINER => "Toughness Trainer",
                ParameterTamerSkill.TamerSkill.FATIGUE_CARE => "Fatigue Care",
                ParameterTamerSkill.TamerSkill.CHANCE_UP => "Chance Up",
                ParameterTamerSkill.TamerSkill.MASTER_TRAINER => "Master Trainer",
                ParameterTamerSkill.TamerSkill.EAT_CARE => "Eat Care",
                ParameterTamerSkill.TamerSkill.TOILET_CARE => "Toilet Care",
                ParameterTamerSkill.TamerSkill.SLEEP_CARE => "Sleep Care",
                ParameterTamerSkill.TamerSkill.AFFECTION => "Affection",
                ParameterTamerSkill.TamerSkill.LIFE_MASTER => "Life Master",
                ParameterTamerSkill.TamerSkill.GOOD_EVOLUTION => "Good Evolution",
                ParameterTamerSkill.TamerSkill.EVOLUTION_PROMOTION => "Evolution Promotion",
                ParameterTamerSkill.TamerSkill.EVOLUTION_VETERAN => "Evolution Veteran",
                ParameterTamerSkill.TamerSkill.GREAT_EVOLUTION => "Great Evolution",
                ParameterTamerSkill.TamerSkill.WILD_FINDER => "Wild Finder",
                ParameterTamerSkill.TamerSkill.FISHERMAN => "Fisherman",
                ParameterTamerSkill.TamerSkill.WILD_SCOUTER => "Wild Scouter",
                ParameterTamerSkill.TamerSkill.GURANDA => "Guranda",
                ParameterTamerSkill.TamerSkill.INGREDIENTS_HUNTER => "Ingredients Hunter",
                ParameterTamerSkill.TamerSkill.METAL_MAN => "Metal Man",
                ParameterTamerSkill.TamerSkill.STONE_MAN => "Stone Man",
                ParameterTamerSkill.TamerSkill.LIQUID_MAN => "Liquid Man",
                ParameterTamerSkill.TamerSkill.WOOD_MAN => "Wood Man",
                ParameterTamerSkill.TamerSkill.MATERIAL_MASTER => "Material Master",
                ParameterTamerSkill.TamerSkill.COOKING_BOY => "Cooking Boy",
                ParameterTamerSkill.TamerSkill.MR_CUISINE => "Mr. Cuisine",
                ParameterTamerSkill.TamerSkill.ULTIMATE_COOK => "Ultimate Cook",
                ParameterTamerSkill.TamerSkill.HEALTH_RUNNER => "Health Runner",
                ParameterTamerSkill.TamerSkill.MENTAL_TRAINING => "Mental Training",
                ParameterTamerSkill.TamerSkill.REFRESHING_RUNNER => "Refreshing Runner",
                ParameterTamerSkill.TamerSkill.HALE => "Hale",
                ParameterTamerSkill.TamerSkill.SECOND_ORDER => "Second Order",
                ParameterTamerSkill.TamerSkill.DEFENSE_ORDER => "Defense Order",
                ParameterTamerSkill.TamerSkill.OP_GAIN => "OP Gain",
                ParameterTamerSkill.TamerSkill.THIRD_ORDER => "Third Order",
                ParameterTamerSkill.TamerSkill.HIGH_DEFENSE => "High Defense",
                ParameterTamerSkill.TamerSkill.NEXT_ORDER => "Next Order",
                ParameterTamerSkill.TamerSkill.OP_MASTER => "OP Master",
                ParameterTamerSkill.TamerSkill.ASSESS => "Assess",
                ParameterTamerSkill.TamerSkill.ABSOLUTELY_BLIND_SPOT => "Absolutely Blind Spot",
                ParameterTamerSkill.TamerSkill.BUFFER => "Buffer",
                ParameterTamerSkill.TamerSkill.YELL => "Yell",
                ParameterTamerSkill.TamerSkill.DE_BUFFER => "De-Buffer",
                ParameterTamerSkill.TamerSkill.ABSOLUTE_CONCENTRATION => "Absolute Concentration",
                ParameterTamerSkill.TamerSkill.RANGE_EXPANSION => "Range Expansion",
                ParameterTamerSkill.TamerSkill.QUICK_PITCHING => "Quick Pitching",
                ParameterTamerSkill.TamerSkill.DIFFERENT_DIMENSION_PITCHING => "Different Dimension Pitching",
                ParameterTamerSkill.TamerSkill.SOUL_PITCHING => "Soul Pitching",
                ParameterTamerSkill.TamerSkill.ITEM_GETTER => "Item Getter",
                ParameterTamerSkill.TamerSkill.BIT_GETTER => "Bit Getter",
                ParameterTamerSkill.TamerSkill.EXPLORER => "Explorer",
                ParameterTamerSkill.TamerSkill.DROP_MASTER => "Drop Master",
                ParameterTamerSkill.TamerSkill.OBSERVATION_TRICKS => "Observation Tricks",
                ParameterTamerSkill.TamerSkill.KNOWLEDGE_EXPERIENCE => "Knowledge Experience",
                ParameterTamerSkill.TamerSkill.RESEARCH_SKILLS => "Research Skills",
                _ => skill.ToString().Replace("_", " ")
            };
        }

        private string GetStateName(uDigiviceTamerPanel.State state)
        {
            return state switch
            {
                uDigiviceTamerPanel.State.Status => "Status",
                uDigiviceTamerPanel.State.SkillCheck => "Skill Check",
                uDigiviceTamerPanel.State.SkillGet => "Skill Get",
                _ => state.ToString()
            };
        }

        private string GetTextSafe(UnityEngine.UI.Text textComponent)
        {
            try
            {
                if (textComponent != null)
                    return textComponent.text ?? "";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[TamerPanel] Error in GetTextSafe: {ex.Message}");
            }
            return "";
        }

        #endregion

        public void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel?.m_CurrentState ?? uDigiviceTamerPanel.State.None;
            string stateName = GetStateName(state);
            string announcement = $"Tamer menu, {stateName}";

            if (state == uDigiviceTamerPanel.State.Status)
                announcement += ". " + GetStatusInfo();
            else if (state == uDigiviceTamerPanel.State.SkillGet)
                announcement += ". " + GetSkillGetInfo();
            else if (state == uDigiviceTamerPanel.State.SkillCheck)
                announcement += ". " + GetSkillCheckInfo();

            ScreenReader.Say(announcement);
        }
    }
}
