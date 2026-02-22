using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the restaurant (buy food) and camp cooking panels.
    /// Tracks two state machines: the main panel state and the item sub-panel state.
    /// Announces food names, prices, stats, genre, and partner selection.
    /// </summary>
    public class RestaurantPanelHandler : HandlerBase<uRestaurantPanel>
    {
        protected override string LogTag => "[RestaurantPanel]";
        public override int Priority => 60;

        private uRestaurantPanel.State _lastPanelState = uRestaurantPanel.State.None;
        private uRestaurantPanelItem.State _lastItemState = uRestaurantPanelItem.State.MenuListSelect;
        private int _lastDialogSelect = -2; // ResultDialog cursor (All=0, L=1, R=2)

        // Cached sub-panels (obtained from _panel on open)
        private uRestaurantPanelItem _itemPanel;
        private uRestaurantPanelBit _bitPanel;
        private uRestaurantPanelEfficacyWindow _efficacyWindow;
        private uRestaurantPanelResultDialog _resultDialog;

        /// <summary>
        /// Returns true when the restaurant panel is active.
        /// Used by DialogTextPatch to suppress duplicate SetMessage announcements -
        /// the handler reads the game's own messages as state changes instead.
        /// </summary>
        public static bool ShouldSuppressMessages()
        {
            try
            {
                var mgm = MainGameManager.m_instance;
                var panel = mgm?.restaurantUI;
                if (panel != null && panel.m_enabelPanel)
                    return true;

                // Also check camp cooking (uCampPanel.m_restaurantPanel)
                var campPanel = Object.FindObjectOfType<uCampPanel>();
                var campRestaurant = campPanel?.m_restaurantPanel;
                if (campRestaurant != null && campRestaurant.m_enabelPanel)
                    return true;
            }
            catch { }
            return false;
        }

        public override bool IsOpen()
        {
            try
            {
                // Check town restaurant first
                var mgm = MainGameManager.m_instance;
                if (mgm != null)
                {
                    var panel = mgm.restaurantUI;
                    if (panel != null && panel.m_enabelPanel)
                    {
                        _panel = panel;
                        return true;
                    }
                }

                // Check camp cooking (uCampPanel.m_restaurantPanel)
                var campPanel = Object.FindObjectOfType<uCampPanel>();
                var campRestaurant = campPanel?.m_restaurantPanel;
                if (campRestaurant != null && campRestaurant.m_enabelPanel)
                {
                    _panel = campRestaurant;
                    return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        protected override void OnOpen()
        {
            _lastCursor = -1;
            _lastPanelState = uRestaurantPanel.State.None;
            _lastItemState = uRestaurantPanelItem.State.MenuListSelect;
            _lastDialogSelect = -2;

            CacheSubPanels();

            if (_panel == null)
                return;

            var type = _panel.m_type;
            string typeName = type == uRestaurantPanel.Type.CampCooking ? "Camp Cooking" : "Restaurant";
            var state = _panel.m_state;
            int cursor = GetCursorPosition();

            string announcement = $"{typeName}. {BuildItemAnnouncement(cursor)}";
            ScreenReader.Say(announcement);
            DebugLogger.Log($"{LogTag} Opened type={type}, state={state}, cursor={cursor}");

            _lastPanelState = state;
            _lastCursor = cursor;
        }

        protected override void OnClose()
        {
            _lastPanelState = uRestaurantPanel.State.None;
            _lastItemState = uRestaurantPanelItem.State.MenuListSelect;
            _lastDialogSelect = -2;
            _itemPanel = null;
            _bitPanel = null;
            _efficacyWindow = null;
            _resultDialog = null;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            if (ModInputManager.IsActionTriggered("ShopCheckBits"))
            {
                AnnounceBits();
                return;
            }

            CheckPanelStateChange();
            CheckItemStateChange();

            // Route cursor tracking based on which sub-state we're in
            bool inPartnerSelect =
                (_itemPanel != null &&
                    (_itemPanel.m_state == uRestaurantPanelItem.State.SelectDigimon ||
                     _itemPanel.m_state == uRestaurantPanelItem.State.RestaurantSelectDigimonUpdate)) ||
                (_panel != null && _panel.m_state == uRestaurantPanel.State.CampCookingSelectDigimonUpdate);

            if (inPartnerSelect)
                CheckDialogCursorChange();
            else
                CheckMenuCursorChange();
        }

        private void CacheSubPanels()
        {
            if (_panel == null) return;

            try { _itemPanel = _panel.m_itemPanel; } catch { _itemPanel = null; }
            try { _bitPanel = _itemPanel?.m_bitPanel; } catch { _bitPanel = null; }
            try { _efficacyWindow = _itemPanel?.m_efficacyWindow; } catch { _efficacyWindow = null; }
            try { _resultDialog = _itemPanel?.m_resultDialog; } catch { _resultDialog = null; }
        }

        // ── Bits ──

        private void AnnounceBits()
        {
            if (IsCampCooking)
                return;

            try
            {
                if (_bitPanel?.m_haveMoneyText != null)
                {
                    string bits = _bitPanel.m_haveMoneyText.text;
                    if (!string.IsNullOrEmpty(bits))
                    {
                        ScreenReader.Say($"You have {TextUtilities.StripRichTextTags(bits)} bits");
                        return;
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading bits: {ex.Message}");
            }
            ScreenReader.Say("Bits unknown");
        }

        // ── Item Announcement (name + price + stats + position) ──

        private bool IsCampCooking => _panel != null && _panel.m_type == uRestaurantPanel.Type.CampCooking;

        private string BuildItemAnnouncement(int cursor)
        {
            string itemText = GetFoodName(cursor);
            int total = GetMenuItemCount();
            string stats = BuildFoodDetailString();

            string announcement = itemText;

            // Only show price for restaurant (camp cooking is free)
            if (!IsCampCooking)
            {
                string price = GetPriceText();
                if (!string.IsNullOrEmpty(price))
                    announcement += $", {price}";
            }

            announcement += $". {cursor + 1} of {total}";
            if (!string.IsNullOrEmpty(stats))
                announcement += $". {stats}";

            return announcement;
        }

        // ── Food Details ──

        private string BuildFoodDetailString()
        {
            try
            {
                // Get selected cooking data from the main panel
                var cookingData = _panel?.GetSelectParamCookingData();
                if (cookingData == null) return null;

                var foodData = cookingData.GetParamItemDataFood();
                if (foodData == null) return null;

                var parts = new System.Collections.Generic.List<string>();

                // Genre/lineage
                string genre = GetFoodGenreText();
                if (!string.IsNullOrEmpty(genre))
                    parts.Add(genre);

                // Core stats
                AddStat(parts, "Satiety", foodData.m_satiety);
                AddStat(parts, "Max HP", foodData.m_hpMax);
                AddStat(parts, "Max MP", foodData.m_mpMax);
                AddStat(parts, "Attack", foodData.m_forcefulness);
                AddStat(parts, "Defense", foodData.m_robustness);
                AddStat(parts, "Wisdom", foodData.m_cleverness);
                AddStat(parts, "Speed", foodData.m_rapidity);
                AddStat(parts, "Mood", foodData.m_mood);
                AddStat(parts, "Weight", foodData.m_bodyWeight);

                // Other/special stats
                AddStat(parts, "Life", foodData.m_lifeTime);
                AddStat(parts, "Education", foodData.m_education);
                AddStat(parts, "Trust", foodData.m_trust);
                AddStat(parts, "Bonds", foodData.m_bonds);
                AddStat(parts, "HP Cure", foodData.m_hp);
                AddStat(parts, "MP Cure", foodData.m_mp);

                if (parts.Count == 0)
                    return "No stat effects";

                return string.Join(", ", parts);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error building food details: {ex.Message}");
                return null;
            }
        }

        private void AddStat(System.Collections.Generic.List<string> parts, string name, int value)
        {
            if (value != 0)
            {
                string prefix = value > 0 ? "+" : "";
                parts.Add($"{name} {prefix}{value}");
            }
        }

        // ── State Changes ──

        private void CheckPanelStateChange()
        {
            if (_panel == null) return;

            var state = _panel.m_state;
            if (state == _lastPanelState) return;

            DebugLogger.Log($"{LogTag} Panel state: {_lastPanelState} -> {state}");

            switch (state)
            {
                case uRestaurantPanel.State.CampCookingSEWait:
                    ScreenReader.Say("Cooking");
                    break;
                case uRestaurantPanel.State.CampCookingFadeInWait:
                    // Transition, don't announce
                    break;
                case uRestaurantPanel.State.CampCookingSelectDigimonUpdate:
                    AnnouncePartnerSelection();
                    break;
                case uRestaurantPanel.State.CampItemNoneMessage:
                case uRestaurantPanel.State.CampItemNoneMessageWait:
                    ScreenReader.Say("No ingredients available");
                    break;
                case uRestaurantPanel.State.UseItemMessageWait:
                case uRestaurantPanel.State.ItemEatCheck:
                    // SetItemMessage announces the eating results, no need to duplicate
                    break;
            }

            _lastPanelState = state;
        }

        private void CheckItemStateChange()
        {
            if (_itemPanel == null) return;

            var state = _itemPanel.m_state;
            if (state == _lastItemState) return;

            DebugLogger.Log($"{LogTag} Item state: {_lastItemState} -> {state}");

            switch (state)
            {
                case uRestaurantPanelItem.State.MenuListSelect:
                    // Returning to menu list - announce current item
                    _lastDialogSelect = -2;
                    int cursor = GetCursorPosition();
                    if (cursor >= 0)
                    {
                        ScreenReader.Say($"Menu. {BuildItemAnnouncement(cursor)}");
                        _lastCursor = cursor;
                    }
                    break;

                case uRestaurantPanelItem.State.SelectDigimon:
                    // In camp cooking, SelectDigimon fires before the cooking animation -
                    // the actual partner selection UI appears later at CampCookingSelectDigimonUpdate.
                    if (!IsCampCooking)
                        AnnouncePartnerSelection();
                    break;
                case uRestaurantPanelItem.State.RestaurantSelectDigimonUpdate:
                    AnnouncePartnerSelection();
                    break;

                case uRestaurantPanelItem.State.ErrorMessage:
                    // Check if partners are satiated, otherwise generic message
                    if (_itemPanel.IsAllPartnaerDigimonoSatiety())
                        ScreenReader.Say("Partners are satiated");
                    else
                        ScreenReader.Say("Cannot order this item");
                    break;

                case uRestaurantPanelItem.State.CampCookingFadeOutWait:
                    // Transition
                    break;
            }

            _lastItemState = state;
        }

        // ── Menu Cursor ──

        private void CheckMenuCursorChange()
        {
            if (_itemPanel == null) return;

            int cursor = GetCursorPosition();
            if (cursor == _lastCursor || cursor < 0) return;

            ScreenReader.Say(BuildItemAnnouncement(cursor));
            DebugLogger.Log($"{LogTag} Menu cursor: {cursor + 1}/{GetMenuItemCount()}");
            _lastCursor = cursor;
        }

        // ── Partner Selection Dialog ──

        private void CheckDialogCursorChange()
        {
            if (_resultDialog == null) return;

            try
            {
                int selectNo = _resultDialog.m_selectNo;
                if (selectNo == _lastDialogSelect) return;

                string selectName = GetPartnerSelectName(selectNo);
                ScreenReader.Say(selectName);
                DebugLogger.Log($"{LogTag} Dialog cursor: {selectName} ({selectNo})");
                _lastDialogSelect = selectNo;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error checking dialog cursor: {ex.Message}");
            }
        }

        private void AnnouncePartnerSelection()
        {
            string announcement = "Select who eats. ";

            try
            {
                if (_resultDialog != null)
                {
                    int selectNo = _resultDialog.m_selectNo;
                    announcement += GetPartnerSelectName(selectNo);
                    _lastDialogSelect = selectNo;
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error in partner selection: {ex.Message}");
            }

            ScreenReader.Say(announcement);
        }

        private string GetPartnerSelectName(int selectNo)
        {
            // Select enum: All=0, L=1, R=2
            try
            {
                var nameTbl = _resultDialog?.m_digimonNameTbl;
                if (selectNo == (int)uRestaurantPanelResultDialog.Select.All)
                {
                    // "Both" option - try reading the bothText
                    string bothText = _resultDialog?.m_bothText?.text;
                    if (!string.IsNullOrEmpty(bothText))
                        return TextUtilities.StripRichTextTags(bothText);
                    return "Both";
                }

                // L=1 maps to partner index 0 (right/Partner00), R=2 maps to partner index 1 (left/Partner01)
                // digimonNameTbl should have names for each partner
                if (nameTbl != null && selectNo >= 1 && selectNo <= 2)
                {
                    int nameIndex = selectNo - 1;
                    if (nameIndex < nameTbl.Count)
                    {
                        string name = nameTbl[nameIndex]?.text;
                        if (!string.IsNullOrEmpty(name))
                            return TextUtilities.StripRichTextTags(name);
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting partner name: {ex.Message}");
            }

            switch (selectNo)
            {
                case 0: return "Both";
                case 1: return "Partner Left";
                case 2: return "Partner Right";
                default: return $"Option {selectNo}";
            }
        }

        // ── Data Reading ──

        private int GetCursorPosition()
        {
            try
            {
                if (_itemPanel != null)
                    return _itemPanel.m_selectNo;
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
                if (_itemPanel != null)
                    return _itemPanel.m_maxListNum;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting count: {ex.Message}");
            }
            return 1;
        }

        private string GetFoodName(int index)
        {
            // Primary: read from the UI text (what sighted players see)
            try
            {
                if (_itemPanel != null)
                {
                    var parts = _itemPanel.GetSelectItemParts(index);
                    if (parts?.m_name != null)
                    {
                        string uiText = parts.m_name.text;
                        if (!string.IsNullOrEmpty(uiText))
                        {
                            string cleaned = TextUtilities.StripRichTextTags(uiText);
                            if (!string.IsNullOrEmpty(cleaned))
                                return cleaned;
                        }
                    }
                }
            }
            catch { }

            // Fallback: ParameterCookingData.GetCookingName()
            try
            {
                var cookingData = _panel?.GetSelectParamCookingData();
                if (cookingData != null)
                {
                    string name = cookingData.GetCookingName();
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
            catch { }

            return AnnouncementBuilder.FallbackItem("Food", index);
        }

        private string GetPriceText()
        {
            try
            {
                if (_bitPanel?.m_priceText != null)
                {
                    string price = _bitPanel.m_priceText.text;
                    if (!string.IsNullOrEmpty(price))
                    {
                        string cleaned = TextUtilities.StripRichTextTags(price);
                        if (!string.IsNullOrEmpty(cleaned) && cleaned != "0")
                            return $"{cleaned} bits";
                    }
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading price: {ex.Message}");
            }
            return null;
        }

        private string GetFoodGenreText()
        {
            try
            {
                if (_efficacyWindow?.m_foodGenreText != null)
                {
                    string genre = _efficacyWindow.m_foodGenreText.text;
                    if (!string.IsNullOrEmpty(genre))
                        return TextUtilities.StripRichTextTags(genre);
                }
            }
            catch { }
            return null;
        }

        // ── Status ──

        public override void AnnounceStatus()
        {
            if (!IsOpen()) return;

            // If in partner selection, announce that
            if (_itemPanel != null && _itemPanel.m_state == uRestaurantPanelItem.State.SelectDigimon)
            {
                AnnouncePartnerSelection();
                return;
            }

            var type = _panel.m_type;
            string typeName = type == uRestaurantPanel.Type.CampCooking ? "Camp Cooking" : "Restaurant";
            int cursor = GetCursorPosition();
            ScreenReader.Say($"{typeName}. {BuildItemAnnouncement(cursor)}");
        }
    }
}
