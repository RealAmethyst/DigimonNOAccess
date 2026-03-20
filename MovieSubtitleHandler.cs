using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Speaks movie subtitles via screen reader as they appear during cutscenes.
    /// Monitors Movie.m_subtitle.m_text.text for changes and announces new lines.
    /// </summary>
    public class MovieSubtitleHandler : IAccessibilityHandler
    {
        public int Priority => 5;

        private Movie _movie;
        private bool _wasPlaying;
        private string _lastSubtitleText = "";

        public bool IsOpen()
        {
            return IsMoviePlaying();
        }

        public void Update()
        {
            bool playing = IsMoviePlaying();

            if (!playing)
            {
                if (_wasPlaying)
                {
                    _wasPlaying = false;
                    _movie = null;
                    _lastSubtitleText = "";
                }
                return;
            }

            if (!_wasPlaying)
            {
                _wasPlaying = true;
                _lastSubtitleText = "";
                DebugLogger.Log("[MovieSubtitle] Movie started playing");
            }

            if (!ModSettings.SpeakMovieSubtitles)
                return;

            try
            {
                var subtitle = _movie.m_subtitle;
                if (subtitle == null) return;

                var textComponent = subtitle.m_text;
                if (textComponent == null) return;

                string currentText = textComponent.text ?? "";
                currentText = TextUtilities.CleanText(currentText).Trim();

                if (string.IsNullOrEmpty(currentText))
                {
                    _lastSubtitleText = "";
                    return;
                }

                if (currentText != _lastSubtitleText)
                {
                    _lastSubtitleText = currentText;
                    ScreenReader.Say(currentText);
                    DebugLogger.Log($"[MovieSubtitle] Speaking: {currentText}");
                }
            }
            catch
            {
                // Movie objects can become invalid mid-frame
            }
        }

        public void AnnounceStatus()
        {
            if (!string.IsNullOrEmpty(_lastSubtitleText))
                ScreenReader.Say(_lastSubtitleText);
        }

        private bool IsMoviePlaying()
        {
            try
            {
                if (_movie != null)
                {
                    if (_movie.m_IsPlaying)
                        return true;

                    _movie = null;
                }

                _movie = Object.FindObjectOfType<Movie>();
                return _movie != null && _movie.m_IsPlaying;
            }
            catch
            {
                _movie = null;
                return false;
            }
        }
    }
}
