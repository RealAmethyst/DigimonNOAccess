using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the colosseum panel (battle arena selection).
    /// </summary>
    public class ColosseumPanelHandler : HandlerBase<uColosseumPanelCommand>
    {
        protected override string LogTag => "[ColosseumPanel]";
        public override int Priority => 60;

        private uColosseumPanelCommand.State _lastState = uColosseumPanelCommand.State.None;

        public override bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uColosseumPanelCommand>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_state;
                return _panel.gameObject != null &&
                       _panel.gameObject.activeInHierarchy &&
                       state != uColosseumPanelCommand.State.None &&
                       state != uColosseumPanelCommand.State.Close;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnOpen()
        {
            _lastCursor = -1;
            _lastState = uColosseumPanelCommand.State.None;

            if (_panel == null)
                return;

            var state = _panel.m_state;
            _lastState = state;

            if (state == uColosseumPanelCommand.State.Wait)
            {
                DebugLogger.Log($"{LogTag} Panel opened, state=Wait (silent, waiting for Main)");
            }
            else
            {
                string stateText = GetStateText(state);
                int cursor = GetCursorPosition();
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = AnnouncementBuilder.MenuOpenWithState("Colosseum", stateText, itemText, cursor, total);
                ScreenReader.Say(announcement);

                DebugLogger.Log($"{LogTag} Panel opened, state={state}, cursor={cursor}");
                _lastCursor = cursor;
            }
        }

        protected override void OnClose()
        {
            _lastState = uColosseumPanelCommand.State.None;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckStateChange();
            CheckCursorChange();
        }

        private void CheckStateChange()
        {
            if (_panel == null)
                return;

            var state = _panel.m_state;
            if (state != _lastState)
            {
                _lastState = state;

                if (state == uColosseumPanelCommand.State.Main)
                {
                    int cursor = GetCursorPosition();
                    string itemText = GetMenuItemText(cursor);
                    int total = GetMenuItemCount();
                    string stateText = GetStateText(state);
                    string announcement = AnnouncementBuilder.MenuOpenWithState("Colosseum", stateText, itemText, cursor, total);
                    ScreenReader.Say(announcement);
                    DebugLogger.Log($"{LogTag} State changed to Main, cursor={cursor}");
                    _lastCursor = cursor;
                }
                else
                {
                    DebugLogger.Log($"{LogTag} State changed to {state}");
                }
            }
        }

        private void CheckCursorChange()
        {
            if (_panel == null)
                return;

            if (_panel.m_state != uColosseumPanelCommand.State.Main)
                return;

            int cursor = GetCursorPosition();

            if (cursor != _lastCursor && cursor >= 0)
            {
                string itemText = GetMenuItemText(cursor);
                int total = GetMenuItemCount();

                string announcement = AnnouncementBuilder.CursorPosition(itemText, cursor, total);
                ScreenReader.Say(announcement);

                DebugLogger.Log($"{LogTag} Cursor changed: {itemText}");
                _lastCursor = cursor;
            }
        }

        private int GetCursorPosition()
        {
            try
            {
                var scrollView = _panel?.m_colosseumScrollView;
                if (scrollView != null)
                    return scrollView.m_selectNo;
            }
            catch { }
            return 0;
        }

        private uint GetSelectedColosseumId(int index)
        {
            try
            {
                var scrollView = _panel?.m_colosseumScrollView;
                if (scrollView == null)
                    return uint.MaxValue;

                var selectItem = scrollView.GetSelectItemData();
                if (selectItem != null)
                    return selectItem.m_itemID;

                var itemList = scrollView.m_itemList;
                if (itemList != null && index < itemList.Count)
                {
                    var item = itemList[index];
                    if (item != null)
                        return item.m_itemID;
                }
            }
            catch { }

            try
            {
                var colosseumPanel = _panel?.m_colosseumPanel;
                if (colosseumPanel != null)
                {
                    var datas = colosseumPanel.GetParameterColosseumDatas();
                    if (datas != null && index < datas.Length)
                        return datas[index].m_id;
                }
            }
            catch { }

            return uint.MaxValue;
        }

        private string GetMenuItemText(int index)
        {
            try
            {
                var colosseumPanel = _panel?.m_colosseumPanel;
                if (colosseumPanel == null)
                    return FallbackMenuItemText(index);

                uint id = GetSelectedColosseumId(index);
                if (id == uint.MaxValue)
                    return FallbackMenuItemText(index);

                var paramData = colosseumPanel.GetParameterColosseumData(id);
                if (paramData == null)
                {
                    paramData = ParameterColosseumData.GetParam(id);
                    if (paramData == null)
                        return FallbackMenuItemText(index);
                }

                var parts = new System.Collections.Generic.List<string>();

                string name = paramData.GetName();
                if (!string.IsNullOrEmpty(name))
                    parts.Add(TextUtilities.StripRichTextTags(name));

                string rank = paramData.GetRank();
                if (!string.IsNullOrEmpty(rank))
                    parts.Add($"Rank {rank}");

                string desc = paramData.GetDescription();
                if (!string.IsNullOrEmpty(desc))
                    parts.Add(TextUtilities.StripRichTextTags(desc));

                string ruleText = null;
                try
                {
                    var ruleData = colosseumPanel.GetParameterColosseumRuleData(paramData.m_rule_id);
                    if (ruleData != null)
                        ruleText = ruleData.GetName();
                }
                catch { }
                if (string.IsNullOrEmpty(ruleText))
                {
                    var descUI = _panel.m_colosseumDescription;
                    if (descUI?.m_ruleValue != null)
                        ruleText = descUI.m_ruleValue.text;
                }
                if (!string.IsNullOrEmpty(ruleText))
                    parts.Add($"Rule: {TextUtilities.StripRichTextTags(ruleText)}");

                var descPanel = _panel.m_colosseumDescription;
                if (descPanel != null)
                {
                    var limitValue = descPanel.m_limitValue;
                    if (limitValue != null && !string.IsNullOrEmpty(limitValue.text))
                    {
                        string limitText = TextUtilities.StripRichTextTags(limitValue.text);
                        var limitTitle = descPanel.m_limitTitle;
                        if (limitTitle != null && !string.IsNullOrEmpty(limitTitle.text))
                        {
                            string titleText = TextUtilities.StripRichTextTags(limitTitle.text).TrimEnd(':');
                            parts.Add($"{titleText}: {limitText}");
                        }
                        else
                        {
                            parts.Add($"Limit: {limitText}");
                        }
                    }
                }

                if (paramData.m_coin_num > 0)
                    parts.Add($"{paramData.m_coin_num} coins");

                try
                {
                    var saveData = colosseumPanel.GetColosseumData(id);
                    if (saveData != null)
                    {
                        if (saveData.m_is_clear_today)
                            parts.Add("Cleared today");
                        else if (saveData.m_is_clear)
                            parts.Add("Cleared");
                    }
                }
                catch { }

                if (parts.Count > 0)
                    return string.Join(", ", parts);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error reading match data: {ex.Message}");
            }

            return FallbackMenuItemText(index);
        }

        private string FallbackMenuItemText(int index)
        {
            try
            {
                var description = _panel?.m_colosseumDescription;
                if (description != null)
                {
                    var captionText = description.m_caption;
                    if (captionText != null && !string.IsNullOrEmpty(captionText.text))
                        return TextUtilities.StripRichTextTags(captionText.text);
                }
            }
            catch { }

            return AnnouncementBuilder.FallbackItem("Battle", index);
        }

        private int GetMenuItemCount()
        {
            try
            {
                var scrollView = _panel?.m_colosseumScrollView;
                if (scrollView != null)
                {
                    var itemList = scrollView.m_itemList;
                    if (itemList != null)
                        return itemList.Count;
                }
            }
            catch { }
            return 1;
        }

        private string GetStateText(uColosseumPanelCommand.State state)
        {
            switch (state)
            {
                case uColosseumPanelCommand.State.Main:
                    return "Select battle";
                case uColosseumPanelCommand.State.Confirm:
                    return "Confirm entry";
                case uColosseumPanelCommand.State.Warning:
                    return "Warning";
                case uColosseumPanelCommand.State.Wait:
                    return "Please wait";
                default:
                    return "Colosseum";
            }
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            var state = _panel.m_state;
            string stateText = GetStateText(state);
            int cursor = GetCursorPosition();
            string itemText = GetMenuItemText(cursor);
            int total = GetMenuItemCount();

            string announcement = AnnouncementBuilder.MenuOpenWithState("Colosseum", stateText, itemText, cursor, total);
            ScreenReader.Say(announcement);
        }
    }
}
