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
            ScreenReader.Say($"Digivice menu, {AnnouncementBuilder.CursorPosition(commandName, commandIndex, total)}");
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
            return commandIndex switch
            {
                0 => "Partner",
                1 => "Tamer",
                2 => "Item",
                3 => "Map",
                4 => "Digi Messenger",
                5 => "Library",
                6 => "System",
                7 => "Save",
                _ => AnnouncementBuilder.FallbackItem("Option", commandIndex)
            };
        }

        public override void AnnounceStatus()
        {
            if (!IsOpen())
                return;

            int commandIndex = GetCurrentCommandIndex();
            string commandName = GetCommandName(commandIndex);
            int total = 8;
            ScreenReader.Say($"Digivice menu, {AnnouncementBuilder.CursorPosition(commandName, commandIndex, total)}");
        }
    }
}
