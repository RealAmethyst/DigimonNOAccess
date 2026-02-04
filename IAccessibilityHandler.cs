namespace DigimonNOAccess
{
    /// <summary>
    /// Common interface for all accessibility handlers.
    /// Handlers make game UI panels accessible via screen reader announcements.
    /// </summary>
    public interface IAccessibilityHandler
    {
        /// <summary>
        /// Called every frame to check for state changes and make announcements.
        /// </summary>
        void Update();

        /// <summary>
        /// Returns true if this handler's associated game panel is currently visible/active.
        /// </summary>
        bool IsOpen();

        /// <summary>
        /// Speaks the current state of this handler's panel for the status repeat hotkey.
        /// </summary>
        void AnnounceStatus();

        /// <summary>
        /// Priority for status announcements. Lower values are checked first.
        /// Used by Main to determine which handler announces when multiple panels are open.
        /// </summary>
        int Priority { get; }
    }
}
