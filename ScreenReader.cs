using System;
using System.Runtime.InteropServices;
namespace DigimonNOAccess
{
    /// <summary>
    /// Screen reader communication via Tolk library.
    /// Tolk.dll and nvdaControllerClient64.dll must be in the game directory.
    /// </summary>
    public static class ScreenReader
    {
        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_IsLoaded();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_HasSpeech();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Output([MarshalAs(UnmanagedType.LPWStr)] string text, bool interrupt);

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_Silence();

        [DllImport("Tolk.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        private static bool _initialized = false;
        private static string _lastMessage = "";

        /// <summary>
        /// Initialize the screen reader library.
        /// </summary>
        public static bool Initialize()
        {
            if (_initialized)
                return true;

            try
            {
                Tolk_Load();
                _initialized = Tolk_IsLoaded() && Tolk_HasSpeech();

                if (_initialized)
                {
                    var readerPtr = Tolk_DetectScreenReader();
                    var readerName = readerPtr != IntPtr.Zero ? Marshal.PtrToStringUni(readerPtr) : "Unknown";
                    DebugLogger.Log($"Screen reader detected: {readerName}");
                }
                else
                {
                    DebugLogger.Warning("No screen reader detected or Tolk failed to load");
                }

                return _initialized;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Error($"Failed to initialize Tolk: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Shut down the screen reader library.
        /// </summary>
        public static void Shutdown()
        {
            if (_initialized)
            {
                try
                {
                    Tolk_Unload();
                }
                catch (System.Exception ex)
                {
                    DebugLogger.Log($"[ScreenReader] Error in Shutdown: {ex.Message}");
                }
                _initialized = false;
            }
        }

        /// <summary>
        /// Speak text through the screen reader.
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="interrupt">If true, interrupts current speech</param>
        public static void Say(string text, bool interrupt = true)
        {
            if (string.IsNullOrEmpty(text))
                return;

            _lastMessage = text;

            if (!_initialized)
                return;

            try
            {
                Tolk_Output(text, interrupt);
            }
            catch (System.Exception ex)
            {
                DebugLogger.Warning($"Tolk output failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Speak text without interrupting current speech.
        /// The text will be queued and spoken after current speech finishes.
        /// Use this for non-critical messages like field Digimon chatter.
        /// </summary>
        /// <param name="text">Text to speak</param>
        public static void SayQueued(string text)
        {
            Say(text, interrupt: false);
        }

        /// <summary>
        /// Stop current speech.
        /// </summary>
        public static void Silence()
        {
            if (!_initialized)
                return;

            try
            {
                Tolk_Silence();
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ScreenReader] Error in Silence: {ex.Message}");
            }
        }

        /// <summary>
        /// Repeat the last spoken message.
        /// </summary>
        public static void RepeatLast()
        {
            if (!string.IsNullOrEmpty(_lastMessage))
            {
                Say(_lastMessage, true);
            }
        }

        /// <summary>
        /// Check if screen reader is available.
        /// </summary>
        public static bool IsAvailable => _initialized;
    }
}
