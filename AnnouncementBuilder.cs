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
    }
}
