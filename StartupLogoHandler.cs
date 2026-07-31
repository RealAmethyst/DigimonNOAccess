using System;
using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Announces the publisher and middleware logos that play while the game boots.
    ///
    /// A sighted player watches three splash screens go by before the title screen
    /// appears, and until now that was a silent stretch where you could not tell
    /// whether the game had launched, frozen, or was still loading. These are pure
    /// art with no text anywhere, so the wording is ours - it just names what is on
    /// screen.
    ///
    /// MainTitle.UpdateLogo walks m_LogoBandaiNamco, m_LogoUnity and m_LogoCri in
    /// turn, loading each one's RawImage, activating it, fading it, then moving on;
    /// after the last it starts the opening movie. Rather than hooking that private
    /// method we simply watch which of the three named GameObjects is active, which
    /// needs no offsets and no private anchors.
    /// </summary>
    public class StartupLogoHandler : IAccessibilityHandler
    {
        // Background announcer - never owns the status key.
        public int Priority => 996;
        public bool IsOpen() => false;
        public void AnnounceStatus() { }

        private MainTitle _mainTitle;
        private bool _finished;
        private float _nextSearchTime;
        private float _giveUpTime = -1f;

        // Which logos we have already spoken, in the order UpdateLogo shows them.
        private readonly bool[] _announced = new bool[3];

        // How long to keep watching before concluding the logos are done or were
        // skipped. Without this the handler would call FindObjectOfType forever on
        // a save that boots straight past them.
        private const float WatchSeconds = 90f;
        private const float SearchInterval = 0.5f;

        public void Update()
        {
            if (_finished)
                return;

            try
            {
                float now = Time.time;

                if (_giveUpTime < 0f)
                    _giveUpTime = now + WatchSeconds;

                if (now > _giveUpTime)
                {
                    _finished = true;
                    DebugLogger.Log("[StartupLogo] Stopped watching for logos");
                    return;
                }

                // FindObjectOfType is expensive, so only retry occasionally until the
                // title object exists.
                if (_mainTitle == null)
                {
                    if (now < _nextSearchTime)
                        return;
                    _nextSearchTime = now + SearchInterval;

                    _mainTitle = UnityEngine.Object.FindObjectOfType<MainTitle>();
                    if (_mainTitle == null)
                        return;
                }

                // Names taken from the textures the game actually loads, not from the
                // field names: LogoLoading pulls "logo/bannam", "logo/unity" and
                // "logo/criware". The third is CRIWARE, the audio middleware, so the
                // field name m_LogoCri understates it.
                CheckLogo(0, _mainTitle.m_LogoBandaiNamco, "Bandai Namco");
                CheckLogo(1, _mainTitle.m_LogoUnity, "Unity");
                CheckLogo(2, _mainTitle.m_LogoCri, "CRIWARE");

                // Once the last one has been shown and gone, there is nothing more to
                // watch - the opening movie and title screen have their own handlers.
                if (_announced[2] && !IsShowing(_mainTitle.m_LogoCri))
                {
                    _finished = true;
                    DebugLogger.Log("[StartupLogo] All logos announced");
                }
            }
            catch (Exception ex)
            {
                // A failure here must never block boot - give up quietly.
                DebugLogger.Log($"[StartupLogo] Error, stopping: {ex.Message}");
                _finished = true;
            }
        }

        private void CheckLogo(int index, GameObject logo, string spoken)
        {
            if (_announced[index] || !IsShowing(logo))
                return;

            _announced[index] = true;
            ScreenReader.Say(spoken);
            DebugLogger.Log($"[StartupLogo] {spoken}");
        }

        private static bool IsShowing(GameObject logo)
        {
            return logo != null && logo.activeInHierarchy;
        }
    }
}
