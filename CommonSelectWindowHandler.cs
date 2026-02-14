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
                ScreenReader.Say(announcement);

                DebugLogger.Log($"{LogTag} Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        /// <summary>
        /// Builds the full announcement for an item: name + cost if applicable.
        /// </summary>
        private string GetItemAnnouncement(int index)
        {
            string name = GetMenuItemText(index);
            string cost = GetItemCost(index);

            if (!string.IsNullOrEmpty(cost))
                return $"{name}, {cost}";

            return name;
        }

        /// <summary>
        /// Gets the best available name for a menu item.
        /// Reads directly from the UI's uItemParts.m_name text (what sighted players see).
        /// Falls back to ParameterCommonSelectWindow.GetLanguageString() if UI text unavailable.
        /// </summary>
        private string GetMenuItemText(int index)
        {
            // Primary: read the actual rendered UI text from uItemParts
            try
            {
                var itemPanel = _panel?.m_itemPanel;
                if (itemPanel != null)
                {
                    var parts = itemPanel.GetSelectItemParts(index);
                    if (parts != null)
                    {
                        var nameText = parts.m_name;
                        if (nameText != null)
                        {
                            string uiText = nameText.text;
                            if (!string.IsNullOrEmpty(uiText))
                            {
                                string cleaned = TextUtilities.StripRichTextTags(uiText);
                                if (!string.IsNullOrEmpty(cleaned))
                                    return cleaned;
                            }
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
                var paramList = _panel?.m_paramCommonSelectWindowList;
                if (paramList != null && index >= 0 && index < paramList.Count)
                {
                    var param = paramList[index];
                    if (param != null)
                    {
                        string text = param.GetLanguageString();
                        if (!string.IsNullOrEmpty(text))
                            return text;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading param text: {ex.Message}");
            }

            return AnnouncementBuilder.FallbackItem("Option", index);
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
        /// Gets the player's current balance from the mode panel's value text.
        /// </summary>
        private string GetPlayerBalance()
        {
            if (_currentOutMode == ParameterCommonSelectWindowMode.OutMode.None)
                return null;

            try
            {
                var modeTbl = _panel?.m_uCommonSelectWindowModeTbl;
                if (modeTbl != null)
                {
                    int modeIndex = (int)_currentOutMode;
                    if (modeIndex >= 0 && modeIndex < modeTbl.Length)
                    {
                        var modePanel = modeTbl[modeIndex];
                        if (modePanel != null)
                        {
                            var valueText = modePanel.m_valueText;
                            if (valueText != null)
                            {
                                string text = valueText.text;
                                if (!string.IsNullOrEmpty(text))
                                {
                                    string currency = GetCurrencyName();
                                    return $"Your {currency}: {TextUtilities.StripRichTextTags(text)}";
                                }
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading balance: {ex.Message}");
            }

            return null;
        }

        private string GetCurrencyName()
        {
            return _currentOutMode switch
            {
                ParameterCommonSelectWindowMode.OutMode.Bit => "Bits",
                ParameterCommonSelectWindowMode.OutMode.DailyQuestPoint => "Daily Quest Points",
                ParameterCommonSelectWindowMode.OutMode.Coin => "Coins",
                _ => "currency"
            };
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

            string balance = GetPlayerBalance();
            if (!string.IsNullOrEmpty(balance))
                announcement += $". {balance}";

            ScreenReader.Say(announcement);
        }
    }
}
