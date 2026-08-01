namespace DigimonNOAccess
{
    /// <summary>
    /// Remembers the button-hint bar of the screen currently being announced, so it
    /// can be spoken AFTER the selected item instead of before it.
    ///
    /// Why this exists: nearly every panel has a caption strip along the bottom set
    /// through uCaptionBase.SetCaptionNoWithButtonIcon - "OK", "Back" and so on. It
    /// is genuinely useful to hear, but it is the same on most screens and it is the
    /// least urgent thing said, so leading with it makes you sit through boilerplate
    /// before you learn what you are actually on.
    ///
    /// The captions do not share a base class - only two of them derive from
    /// uCaptionBase - so there is no single object to read. Instead, every text
    /// reader in the mod already has to decide whether what it read is usable as a
    /// name. When one of them sees a hint bar it calls <see cref="Observe"/>, which
    /// both reports "not a name, do not speak this as a title" and stores it here.
    /// The handler then appends it at the end via
    /// <see cref="AnnouncementBuilder.WithButtonHints"/>.
    ///
    /// This is single-threaded by construction: readers and announcements both run
    /// on the game thread inside a handler's Update.
    /// </summary>
    public static class ButtonHintCache
    {
        private static string _current;
        private static int _observedFrame = -1;

        // How many frames a captured hint bar stays valid. Readers run immediately
        // before the announcement is built, normally in the same frame, so this only
        // has to tolerate a handler that reads and announces a frame apart. Expiring
        // rather than requiring every handler to clear on close means a screen with
        // no caption of its own can never inherit the previous screen's hints.
        private const int ValidForFrames = 2;

        /// <summary>
        /// True when this text is the button-hint bar rather than a usable name.
        /// Remembers it as the current screen's hints when so.
        ///
        /// Called from inside the "is this usable" guard of every text reader, which
        /// is why it is a predicate with a side effect rather than two calls: the
        /// readers already ask exactly this question at exactly the right moment.
        /// </summary>
        public static bool Observe(string text)
        {
            string hints = TextUtilities.ExtractButtonHints(text);
            if (hints == null)
                return false;

            _current = hints;
            _observedFrame = UnityEngine.Time.frameCount;
            return true;
        }

        /// <summary>
        /// Gate a raw UI string on its way into a name: returns it unchanged, or null
        /// if it was the button-hint bar (which is remembered for the end of the
        /// announcement instead).
        ///
        /// This MUST wrap the raw text, before StripRichTextTags. That method
        /// converts the button-glyph control characters into readable words - the
        /// very markers that identify a hint bar - so checking afterwards can never
        /// work. Returning null lets the caller's existing empty-check do the
        /// rejecting, with no change to its control flow.
        /// </summary>
        public static string Filter(string rawText)
        {
            return Observe(rawText) ? null : rawText;
        }

        /// <summary>
        /// Takes the pending button hints, if any, and clears them.
        ///
        /// One-shot by design. A caption is only read while an announcement is being
        /// built for a screen that just opened or changed state, so the very next
        /// thing spoken IS that announcement - and it is the only one the hints
        /// belong on. Consuming them means moving the cursor afterwards does not
        /// repeat the hint bar on every item.
        ///
        /// Also frame-scoped, so hints left behind by a screen that never got around
        /// to announcing cannot leak onto an unrelated line later.
        /// </summary>
        public static string TakePending()
        {
            if (_current == null)
                return null;

            if (_observedFrame < 0 || UnityEngine.Time.frameCount - _observedFrame > ValidForFrames)
            {
                Clear();
                return null;
            }

            string hints = _current;
            Clear();
            return hints;
        }

        /// <summary>
        /// Forget the remembered hints. Call when a panel closes so a screen without
        /// its own caption never inherits the previous screen's.
        /// </summary>
        public static void Clear()
        {
            _current = null;
            _observedFrame = -1;
        }
    }
}
