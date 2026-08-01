using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the common selection window.
    /// Used by Birdramon transport, shops, museum, treasure hunting, and many other NPC menus.
    /// Announces item names with costs (Bits/Points/Coins) when applicable.
    /// </summary>
    public class CommonSelectWindowHandler : HandlerBase<uCommonSelectWindowPanel>
    {
        protected override string LogTag => "[CommonSelectWindow]";
        public override int Priority => 35;

        private ParameterCommonSelectWindowMode.OutMode _currentOutMode;
        private ParameterCommonSelectWindowMode.WindowType _windowType;

        public override bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uCommonSelectWindowPanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                return _panel.isEnabelPanel();
            }
            catch
            {
                return _panel.gameObject != null && _panel.gameObject.activeInHierarchy;
            }
        }

        protected override void OnOpen()
        {
            _lastCursor = -1;
            _currentOutMode = ParameterCommonSelectWindowMode.OutMode.None;
            _windowType = ParameterCommonSelectWindowMode.WindowType.None;

            if (_panel == null)
                return;

            try { _currentOutMode = _panel.m_outMode; } catch { }
            try { _windowType = _panel.m_windowType; } catch { }

            int cursor = GetCursorPosition();
            string itemText = GetItemAnnouncement(cursor);
            int total = GetMenuItemCount();
            string title = GetWindowTitle();

            string announcement = AnnouncementBuilder.MenuOpen(title, itemText, cursor, total);

            string desc = GetItemDescription(cursor);
            if (!string.IsNullOrEmpty(desc))
                announcement += $". {desc}";

            // On open, also announce player's current balance if this menu has costs
            string balance = GetPlayerBalance();
            if (!string.IsNullOrEmpty(balance))
                announcement += $". {balance}";

            ScreenReader.Say(announcement);

            DebugLogger.Log($"{LogTag} Menu opened: type={_windowType}, outMode={_currentOutMode}, cursor={cursor}, total={total}");
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (ModInputManager.IsActionTriggered("ShopCheckBits"))
            {
                string balance = GetPlayerBalance();
                ScreenReader.Say(!string.IsNullOrEmpty(balance) ? balance : "Bits unknown");
                return;
            }

            CheckCursorChange();
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetItemAnnouncement(cursor);
                int total = GetMenuItemCount();

                string announcement = AnnouncementBuilder.CursorPosition(itemText, cursor, total);

                string desc = GetItemDescription(cursor);
                if (!string.IsNullOrEmpty(desc))
                    announcement += $". {desc}";

                ScreenReader.Say(announcement);

                DebugLogger.Log($"{LogTag} Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        /// <summary>
        /// Builds the full announcement for an item: name + cost + description if applicable.
        /// </summary>
        private string GetItemAnnouncement(int index)
        {
            string name = GetMenuItemText(index);
            string cost = GetItemCost(index);

            string announcement = name;
            if (!string.IsNullOrEmpty(cost))
                announcement += $", {cost}";

            return announcement;
        }

        /// <summary>
        /// Gets the best available name for a menu item.
        /// Reads directly from the UI's uItemParts.m_name text (what sighted players see).
        /// Falls back to ParameterCommonSelectWindow.GetLanguageString() if UI text unavailable.
        /// </summary>
        private string GetMenuItemText(int index)
        {
            string fallback = AnnouncementBuilder.FallbackItem("Option", index);

            if (_panel == null)
            {
                DebugLogger.Log($"{LogTag} Item name: m_panel was null");
                return fallback;
            }

            // Primary: read the actual rendered UI text from uItemParts
            try
            {
                var itemPanel = _panel.m_itemPanel;
                if (itemPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: m_itemPanel was null");
                }
                else
                {
                    var parts = itemPanel.GetSelectItemParts(index);
                    if (parts == null)
                    {
                        DebugLogger.Log($"{LogTag} Item name: GetSelectItemParts({index}) returned null");
                    }
                    else
                    {
                        var nameText = parts.m_name;
                        if (nameText == null)
                        {
                            DebugLogger.Log($"{LogTag} Item name: selected uItemParts.m_name was null");
                        }
                        else
                        {
                            string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(nameText.text))?.Trim();
                            if (!string.IsNullOrWhiteSpace(cleaned))
                                return cleaned;

                            DebugLogger.Log($"{LogTag} Item name: selected uItemParts.m_name.text was empty");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading UI item text: {ex.Message}");
            }

            // Fallback: param language string
            try
            {
                var paramList = _panel.m_paramCommonSelectWindowList;
                if (paramList == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: m_paramCommonSelectWindowList was null");
                    return fallback;
                }

                if (index < 0 || index >= paramList.Count)
                {
                    DebugLogger.Log($"{LogTag} Item name: index {index} was outside m_paramCommonSelectWindowList count {paramList.Count}");
                    return fallback;
                }

                var param = paramList[index];
                if (param == null)
                {
                    DebugLogger.Log($"{LogTag} Item name: m_paramCommonSelectWindowList[{index}] was null");
                    return fallback;
                }

                string text = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(param.GetLanguageString()))?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;

                DebugLogger.Log($"{LogTag} Item name: ParameterCommonSelectWindow.GetLanguageString() was empty for index {index}");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading param text: {ex.Message}");
            }

            return fallback;
        }

        /// <summary>
        /// Gets the cost string for an item, e.g. "100 Bits".
        /// Returns null if this menu doesn't show costs.
        /// </summary>
        private string GetItemCost(int index)
        {
            if (_currentOutMode == ParameterCommonSelectWindowMode.OutMode.None)
                return null;

            try
            {
                var paramList = _panel?.m_paramCommonSelectWindowList;
                if (paramList != null && index >= 0 && index < paramList.Count)
                {
                    var param = paramList[index];
                    if (param != null)
                    {
                        int cost = param.m_value;
                        if (cost > 0)
                        {
                            string currency = GetCurrencyName();
                            return $"{cost} {currency}";
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading cost: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets the item description via ParameterItemData looked up from uItemParts.itemID.
        /// </summary>
        private string GetItemDescription(int index)
        {
            try
            {
                var paramList = _panel?.m_paramCommonSelectWindowList;
                if (paramList == null || index < 0 || index >= paramList.Count)
                    return null;

                var param = paramList[index];
                if (param == null) return null;

                // scriptCommandParam1 contains the item string ID (e.g. "item_other_003")
                string itemStringId = param.m_scriptCommandParam1;
                if (string.IsNullOrEmpty(itemStringId))
                    return null;

                uint itemHash = Language.makeHash(itemStringId);
                var paramItemData = ParameterItemData.GetParam(itemHash);
                if (paramItemData == null) return null;

                string desc = paramItemData.GetDescription();
                if (!string.IsNullOrEmpty(desc))
                    return TextUtilities.CleanText(desc);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Gets the player's current balance from the mode panel's value text.
        /// </summary>
        private string GetPlayerBalance()
        {
            if (_currentOutMode == ParameterCommonSelectWindowMode.OutMode.None)
                return null;

            try
            {
                var modePanel = GetCurrentModePanel("balance");
                if (modePanel == null)
                    return null;

                var valueText = modePanel.m_valueText;
                if (valueText == null)
                {
                    DebugLogger.Log($"{LogTag} Balance: m_valueText was null");
                    return null;
                }

                string text = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(valueText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(text) || TextUtilities.IsPlaceholderText(text))
                {
                    DebugLogger.Log($"{LogTag} Balance: m_valueText.text unusable: {TextUtilities.DescribeUnusable(text)}");
                    return null;
                }

                string currency = GetCurrencyName();
                return $"Your {currency}: {text}";
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading balance: {ex.Message}");
            }

            return null;
        }

        private string GetCurrencyName()
        {
            string fallback = _currentOutMode switch
            {
                ParameterCommonSelectWindowMode.OutMode.Bit => "Bits",
                ParameterCommonSelectWindowMode.OutMode.DailyQuestPoint => "Daily Quest Points",
                ParameterCommonSelectWindowMode.OutMode.Coin => "Coins",
                _ => "currency"
            };

            var modePanel = GetCurrentModePanel("currency title");
            if (modePanel == null)
                return fallback;

            try
            {
                var titleText = modePanel.m_titleText;
                if (titleText == null)
                {
                    DebugLogger.Log($"{LogTag} Currency title: m_titleText was null");
                    return fallback;
                }

                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(titleText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"{LogTag} Currency title: m_titleText.text unusable: {TextUtilities.DescribeUnusable(cleaned)}");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Currency title read failed: {ex.Message}");
                return fallback;
            }
        }

        private uCommonSelectWindowPanelMode GetCurrentModePanel(string purpose)
        {
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} {purpose}: m_panel was null");
                    return null;
                }

                var modeTable = _panel.m_uCommonSelectWindowModeTbl;
                if (modeTable == null)
                {
                    DebugLogger.Log($"{LogTag} {purpose}: m_uCommonSelectWindowModeTbl was null");
                    return null;
                }

                int modeIndex = (int)_currentOutMode;
                if (modeIndex < 0 || modeIndex >= modeTable.Length)
                {
                    DebugLogger.Log($"{LogTag} {purpose}: mode index {modeIndex} was outside m_uCommonSelectWindowModeTbl length {modeTable.Length}");
                    return null;
                }

                var modePanel = modeTable[modeIndex];
                if (modePanel == null)
                {
                    DebugLogger.Log($"{LogTag} {purpose}: m_uCommonSelectWindowModeTbl[{modeIndex}] was null");
                    return null;
                }

                return modePanel;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} {purpose} chain read failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the window title. Uses descriptive name from window type since
        /// the caption panel text often contains button labels rather than titles.
        /// </summary>
        private string GetWindowTitle()
        {
            return GetWindowTypeName();
        }

        private string GetWindowTypeName()
        {
            return _windowType switch
            {
                ParameterCommonSelectWindowMode.WindowType.Transmission => "Transport",
                ParameterCommonSelectWindowMode.WindowType.TreasureHunting => "Treasure Hunting",
                ParameterCommonSelectWindowMode.WindowType.Museum => "Museum",
                ParameterCommonSelectWindowMode.WindowType.MovieTheater => "Movie Theater",
                ParameterCommonSelectWindowMode.WindowType.TamerInfo => "Tamer Info",
                ParameterCommonSelectWindowMode.WindowType.AdventureInfo => "Adventure Info",
                ParameterCommonSelectWindowMode.WindowType.ExDungeonEntrance => "Ex Dungeon",
                ParameterCommonSelectWindowMode.WindowType.ExDungeonSupport => "Ex Dungeon Support",
                ParameterCommonSelectWindowMode.WindowType.LaboratorySkillLearn => "Skill Learn",
                ParameterCommonSelectWindowMode.WindowType.TrainingMachineGradeUp => "Training Machine Upgrade",
                ParameterCommonSelectWindowMode.WindowType.TrainingTutorial => "Training Tutorial",
                ParameterCommonSelectWindowMode.WindowType.EntertainmentZonePrizeChange => "Prize Exchange",
                ParameterCommonSelectWindowMode.WindowType.TreasureFoodShop01 => "Food Shop",
                ParameterCommonSelectWindowMode.WindowType.TreasureFoodShop02 => "Food Shop",
                ParameterCommonSelectWindowMode.WindowType.TreasureMaterial => "Material Exchange",
                _ => "Selection Menu"
            };
        }

        private int GetCursorPosition()
        {
            try
            {
                var itemPanel = _panel?.m_itemPanel;
                if (itemPanel != null)
                {
                    return itemPanel.m_selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting cursor: {ex.Message}");
            }
            return 0;
        }

        private int GetMenuItemCount()
        {
            try
            {
                var paramList = _panel?.m_paramCommonSelectWindowList;
                if (paramList != null)
                {
                    return paramList.Count;
                }
            }
            catch { }
            return 1;
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int cursor = GetCursorPosition();
            string itemText = GetItemAnnouncement(cursor);
            int total = GetMenuItemCount();
            string title = GetWindowTitle();

            string announcement = AnnouncementBuilder.MenuOpen(title, itemText, cursor, total);

            string desc = GetItemDescription(cursor);
            if (!string.IsNullOrEmpty(desc))
                announcement += $". {desc}";

            string balance = GetPlayerBalance();
            if (!string.IsNullOrEmpty(balance))
                announcement += $". {balance}";

            ScreenReader.Say(announcement);
        }
    }
}
