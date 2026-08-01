using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Handles accessibility for the main Digivice menu hub
    /// This is the central menu that connects to Partner, Tamer, Item, Map, Mail, Library, System, and Save
    /// </summary>
    public class DigiviceTopPanelHandler : HandlerBase<uDigiviceTopPanel>
    {
        protected override string LogTag => "[DigiviceTopPanel]";
        public override int Priority => 50;


        public override bool IsOpen()
        {
            if (_panel == null)
            {
                _panel = Object.FindObjectOfType<uDigiviceTopPanel>();
            }

            if (_panel == null)
                return false;

            try
            {
                var state = _panel.m_State;
                return state == uDigiviceTopPanel.State.CommandSelect;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnOpen()
        {
            _lastCursor = -1;

            if (_panel == null)
                return;

            int commandIndex = GetCurrentCommandIndex();
            _lastCursor = commandIndex;

            string commandName = GetCommandName(commandIndex);
            int total = 8; // Partner, Tamer, Item, Map, DigiMessenger, Library, System, Save
            ScreenReader.Say($"{GetPanelTitle()}, {AnnouncementBuilder.CursorPosition(commandName, commandIndex, total)}");
            DebugLogger.Log($"{LogTag} Opened, command={commandIndex} ({commandName})");
        }

        protected override void OnClose()
        {
            _lastCursor = -1;
            base.OnClose();
        }

        protected override void OnUpdate()
        {
            CheckCommandChange();
        }

        private void CheckCommandChange()
        {
            if (_panel == null)
                return;

            int currentCommand = GetCurrentCommandIndex();

            if (currentCommand != _lastCursor && _lastCursor >= 0)
            {
                string commandName = GetCommandName(currentCommand);
                int total = 8;
                ScreenReader.Say(AnnouncementBuilder.CursorPosition(commandName, currentCommand, total));
                DebugLogger.Log($"{LogTag} Command changed to {commandName}");
            }
            _lastCursor = currentCommand;
        }

        private int GetCurrentCommandIndex()
        {
            try
            {
                var command = _panel?.m_Command;
                if (command != null)
                {
                    return (int)command.GetCurrentSelectIndex();
                }
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting command index: {ex.Message}");
            }
            return 0;
        }

        private string GetCommandName(int commandIndex)
        {
            string fallback = AnnouncementBuilder.FallbackItem("Option", commandIndex);

            if (_panel == null)
            {
                DebugLogger.Log($"{LogTag} Command name: m_panel was null");
                return fallback;
            }

            try
            {
                var command = _panel.m_Command;
                if (command == null)
                {
                    DebugLogger.Log($"{LogTag} Command name: m_Command was null");
                    return fallback;
                }

                var items = command.m_items;
                if (items == null)
                {
                    DebugLogger.Log($"{LogTag} Command name: m_items was null");
                    return fallback;
                }

                if (commandIndex < 0 || commandIndex >= items.Length)
                {
                    DebugLogger.Log($"{LogTag} Command name: index {commandIndex} was outside m_items length {items.Length}");
                    return fallback;
                }

                var item = items[commandIndex];
                if (item == null)
                {
                    DebugLogger.Log($"{LogTag} Command name: m_items[{commandIndex}] was null");
                    return fallback;
                }

                var headText = item.m_headText;
                if (headText == null)
                {
                    DebugLogger.Log($"{LogTag} Command name: m_items[{commandIndex}].m_headText was null");
                    return fallback;
                }

                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(headText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"{LogTag} Command name: m_items[{commandIndex}].m_headText.text unusable: {TextUtilities.DescribeUnusable(cleaned)}");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Error getting command name: {ex.Message}");
            }

            return fallback;
        }

        private string GetPanelTitle()
        {
            const string fallback = "Digivice menu";
            try
            {
                if (_panel == null)
                {
                    DebugLogger.Log($"{LogTag} Title: m_panel was null");
                    return fallback;
                }

                var headLine = _panel.m_HeadLine;
                if (headLine == null)
                {
                    DebugLogger.Log($"{LogTag} Title: m_HeadLine was null");
                    return fallback;
                }

                var headLineText = headLine.m_HeadLineText;
                if (headLineText == null)
                {
                    DebugLogger.Log($"{LogTag} Title: m_HeadLine.m_HeadLineText was null");
                    return fallback;
                }

                string cleaned = TextUtilities.StripRichTextTags(ButtonHintCache.Filter(headLineText.text))?.Trim();
                if (string.IsNullOrWhiteSpace(cleaned) || TextUtilities.IsPlaceholderText(cleaned))
                {
                    DebugLogger.Log($"{LogTag} Title: m_HeadLine.m_HeadLineText.text unusable: {TextUtilities.DescribeUnusable(cleaned)}");
                    return fallback;
                }

                return cleaned;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"{LogTag} Title read failed: {ex.Message}");
                return fallback;
            }
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int commandIndex = GetCurrentCommandIndex();
            string commandName = GetCommandName(commandIndex);
            int total = 8;
            ScreenReader.Say($"{GetPanelTitle()}, {AnnouncementBuilder.CursorPosition(commandName, commandIndex, total)}");
        }
    }
}
