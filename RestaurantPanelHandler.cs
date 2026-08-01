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
            string typeName = GetRestaurantCaption(type);
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
            if (_panel == null)
            {
                DebugLogger.Log($"{LogTag} Sub-panels: _panel was null");
                return;
            }

            try
            {
                _itemPanel = _panel.m_itemPanel;
                if (_itemPanel == null)
                    DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel was null");
            }
            catch (System.Exception ex)
            {
                _itemPanel = null;
                DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel read failed: {ex.Message}");
            }

            if (_itemPanel == null)
            {
                _bitPanel = null;
                _efficacyWindow = null;
                _resultDialog = null;
                return;
            }

            try
            {
                _bitPanel = _itemPanel.m_bitPanel;
                if (_bitPanel == null)
                    DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel.m_bitPanel was null");
            }
            catch (System.Exception ex)
            {
                _bitPanel = null;
                DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel.m_bitPanel read failed: {ex.Message}");
            }

            try
            {
                _efficacyWindow = _itemPanel.m_efficacyWindow;
                if (_efficacyWindow == null)
                    DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel.m_efficacyWindow was null");
            }
            catch (System.Exception ex)
            {
                _efficacyWindow = null;
                DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel.m_efficacyWindow read failed: {ex.Message}");
            }

            try
            {
                _resultDialog = _itemPanel.m_resultDialog;
                if (_resultDialog == null)
                    DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel.m_resultDialog was null");
            }
            catch (System.Exception ex)
            {
                _resultDialog = null;
                DebugLogger.Log($"{LogTag} Sub-panels: m_itemPanel.m_resultDialog read failed: {ex.Message}");
            }
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
                AddLocalizedOtherStat(parts, ParameterItemDataFood.OtherParamKind.Life, "Life", foodData.m_lifeTime);
                AddLocalizedOtherStat(parts, ParameterItemDataFood.OtherParamKind.Education, "Education", foodData.m_education);
                AddLocalizedOtherStat(parts, ParameterItemDataFood.OtherParamKind.Trust, "Trust", foodData.m_trust);
                AddLocalizedOtherStat(parts, ParameterItemDataFood.OtherParamKind.Bonds, "Bonds", foodData.m_bonds);
                AddLocalizedOtherStat(parts, ParameterItemDataFood.OtherParamKind.HpCure, "HP Cure", foodData.m_hp);
                AddLocalizedOtherStat(parts, ParameterItemDataFood.OtherParamKind.MpCure, "MP Cure", foodData.m_mp);

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

        private void AddLocalizedOtherStat(
            System.Collections.Generic.List<string> parts,
            ParameterItemDataFood.OtherParamKind kind,
            string fallbackName,
            int value)
        {
            if (value == 0)
                return;

            string name = GetOtherParamName(kind, fallbackName);
            string prefix = value > 0 ? "+" : "";
            parts.Add($"{name} {prefix}{value}");
        }

        private string GetOtherParamName(
            ParameterItemDataFood.OtherParamKind kind,
            string fallback)
        {
            try
            {
                if (_efficacyWindow == null)
                {
                    DebugLogger.Log($"{LogTag} Other parameter {kind}: m_efficacyWindow was null");
                    return fallback;
                }

                var rows = _efficacyWindow.m_foodPanelParamOtherTbl;
                if (rows == null)
                {
                    DebugLogger.Log($"{LogTag} Other parameter {kind}: m_foodPanelParamOtherTbl was null");
                    return fallback;
                }

                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    if (row == null)
                    {
                        DebugLogger.Log($"{LogTag} Other parameter {kind}: m_foodPanelParamOtherTbl[{i}] was null");
                        continue;
                    }

                    if (row.m_paramKind != kind)
                        continue;

                    var nameText = row.m_paramNameText;
                    if (nameText == null)
                    {
                        DebugLogger.Log($"{LogTag} Other parameter {kind}: m_paramNameText was null");
                        return fallback;
                    }

                    string name = (TextUtilities.StripRichTextTags(nameText.text) ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(name))
                        return name;

                    DebugLogger.Log($"{LogTag} Other parameter {kind}: m_paramNameText.text was empty");
                    return fallback;
                }

                DebugLogger.Log($"{LogTag} Other parameter {kind}: no matching m_paramKind row");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading other parameter {kind} label: {ex.Message}");
            }

            return fallback;
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
                    if (_resultDialog == null)
                    {
                        DebugLogger.Log($"{LogTag} Both label: m_resultDialog was null");
                        return "Both";
                    }

                    var bothTextComponent = _resultDialog.m_bothText;
                    if (bothTextComponent == null)
                    {
                        DebugLogger.Log($"{LogTag} Both label: m_bothText was null");
                        return "Both";
                    }

                    string bothText = (TextUtilities.StripRichTextTags(bothTextComponent.text) ?? "").Trim();
                    if (!string.IsNullOrWhiteSpace(bothText))
                        return bothText;

                    DebugLogger.Log($"{LogTag} Both label: m_bothText.text was empty");
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
            try
            {
                if (_itemPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Food name: m_itemPanel was null");
                    return AnnouncementBuilder.FallbackItem("Food", index);
                }

                var parts = _itemPanel.GetSelectItemParts(index);
                if (parts == null)
                {
                    DebugLogger.Log($"{LogTag} Food name: GetSelectItemParts({index}) returned null");
                    return AnnouncementBuilder.FallbackItem("Food", index);
                }

                var nameText = parts.m_name;
                if (nameText == null)
                {
                    DebugLogger.Log($"{LogTag} Food name: selected item part m_name was null");
                    return AnnouncementBuilder.FallbackItem("Food", index);
                }

                string name = (TextUtilities.StripRichTextTags(nameText.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;

                DebugLogger.Log($"{LogTag} Food name: selected item part m_name.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading selected item part m_name.text: {ex.Message}");
            }

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
            string typeName = GetRestaurantCaption(type);
            int cursor = GetCursorPosition();
            ScreenReader.Say($"{typeName}. {BuildItemAnnouncement(cursor)}");
        }

        private string GetRestaurantCaption(uRestaurantPanel.Type type)
        {
            string fallback = type == uRestaurantPanel.Type.CampCooking ? "Camp Cooking" : "Restaurant";

            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Caption: _panel was null");
                    return fallback;
                }

                var captionPanel = _panel.m_captionPanel;
                if (captionPanel == null)
                {
                    DebugLogger.Log($"{LogTag} Caption: m_captionPanel was null");
                    return fallback;
                }

                var text = captionPanel.m_text;
                if (text == null)
                {
                    DebugLogger.Log($"{LogTag} Caption: m_captionPanel.m_text was null");
                    return fallback;
                }

                string caption = (TextUtilities.StripRichTextTags(text.text) ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(caption))
                    return caption;

                DebugLogger.Log($"{LogTag} Caption: m_captionPanel.m_text.text was empty");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading m_captionPanel.m_text.text: {ex.Message}");
            }

            return fallback;
        }
    }
}
