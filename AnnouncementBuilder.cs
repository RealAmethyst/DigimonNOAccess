namespace DigimonNOAccess
{
    public static class AnnouncementBuilder
    {
        // "Item name, 3 of 5"
        public static string CursorPosition(string itemText, int cursor, int total)
        {
            return $"{itemText}, {cursor + 1} of {total}";
        }

        // "Menu Name. Item name, 3 of 5"
        public static string MenuOpen(string menuName, string itemText, int cursor, int total)
        {
            return $"{menuName}. {itemText}, {cursor + 1} of {total}";
        }

        // "Menu Name. State. Item name, 3 of 5"
        public static string MenuOpenWithState(string menuName, string stateText, string itemText, int cursor, int total)
        {
            return $"{menuName}. {stateText}. {itemText}, {cursor + 1} of {total}";
        }

        // "Item 3" or "Slot 3" etc.
        public static string FallbackItem(string prefix, int index)
        {
            return $"{prefix} {index + 1}";
        }

        /// <summary>
        /// Appends the game's button-hint bar to an announcement.
        ///
        /// The hints go last, deliberately. They are the same on nearly every screen
        /// and they are the least urgent thing said, so putting them ahead of the
        /// selected item makes you wait through boilerplate before hearing what you
        /// are actually on. Speaking them last means you can act the moment the item
        /// is read and let the rest run on.
        ///
        /// Returns the announcement unchanged when there are no hints, so callers can
        /// pass a possibly-null value without checking.
        /// </summary>
        public static string WithButtonHints(string announcement, string buttonHints)
        {
            if (string.IsNullOrWhiteSpace(buttonHints))
                return announcement;

            if (string.IsNullOrWhiteSpace(announcement))
                return buttonHints;

            return $"{announcement}. {buttonHints}";
        }
    }
}
