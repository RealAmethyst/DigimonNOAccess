using System;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// P/Invoke bindings for Prism (Platform-agnostic Reader Interface for Speech
    /// and Messages), the screen-reader abstraction this mod speaks through.
    /// Supports NVDA, JAWS, SAPI, OneCore and more, on Windows, Mac and Linux.
    ///
    /// API reference: https://github.com/ethindp/prism (include/prism.h).
    ///
    /// Value ranges: volume, rate and pitch are floats from 0.0 to 1.0, with 0.5
    /// as the default for rate and pitch. Screen-reader backends (NVDA, JAWS,
    /// VoiceOver, Speech-dispatcher) do NOT accept those - they return
    /// <see cref="PrismError.NotImplemented"/> and the user's own screen reader
    /// settings apply instead. Real TTS engines (SAPI, OneCore, Orca, AVSpeech)
    /// support all of them.
    /// </summary>
    internal static class PrismInterop
    {
        private const string DllName = "prism";

        // --- Backend IDs (PRISM_BACKEND_*) ---
        // 64-bit hashes, stable across Prism versions, safe to persist in settings.
        public const ulong BACKEND_INVALID                = 0;
        public const ulong BACKEND_SAPI                   = 0x1D6DF72422CEEE66;
        public const ulong BACKEND_AV_SPEECH              = 0x28E3429577805C24;
        public const ulong BACKEND_VOICE_OVER             = 0xCB4897961A754BCB;
        public const ulong BACKEND_SPEECH_DISPATCHER      = 0xE3D6F895D949EBFE;
        public const ulong BACKEND_NVDA                   = 0x89CC19C5C4AC1A56;
        public const ulong BACKEND_JAWS                   = 0xAC3D60E9BD84B53E;
        public const ulong BACKEND_ONE_CORE               = 0x6797D32F0D994CB4;
        public const ulong BACKEND_ORCA                   = 0x10AA1FC05A17F96C;
        public const ulong BACKEND_ANDROID_SCREEN_READER  = 0xD199C175AEEC494B;
        public const ulong BACKEND_ANDROID_TTS            = 0xBC175831BFE4E5CC;
        public const ulong BACKEND_WEB_SPEECH             = 0x3572538D44D44A8F;
        public const ulong BACKEND_UIA                    = 0x6238F019DB678F8E;
        public const ulong BACKEND_ZDSR                   = 0x3D93C56C9E7F2A2E;
        public const ulong BACKEND_ZOOM_TEXT              = 0xAE439D62DC7B1479;
        public const ulong BACKEND_BOY_PC_READER          = 0x285ABA1C16F3300F;
        public const ulong BACKEND_PC_TALKER              = 0x344B951962E3B835;
        public const ulong BACKEND_SENSE_READER           = 0xED4760890B55C2F2;
        public const ulong BACKEND_SYSTEM_ACCESS          = 0x8380F2A37B2C3EB6;
        public const ulong BACKEND_WINDOW_EYES            = 0x9120D89908785C13;
        public const ulong BACKEND_SPIEL                  = 0x478B44F14AD3D89C;

        // --- Feature flags (PrismBackendFeature), returned as a bitmask ---
        public const ulong FEATURE_IS_SUPPORTED_AT_RUNTIME = 1UL << 0;
        public const ulong FEATURE_SPEAK                   = 1UL << 2;
        public const ulong FEATURE_SPEAK_TO_MEMORY         = 1UL << 3;
        public const ulong FEATURE_BRAILLE                 = 1UL << 4;
        public const ulong FEATURE_OUTPUT                  = 1UL << 5;
        public const ulong FEATURE_IS_SPEAKING             = 1UL << 6;
        public const ulong FEATURE_STOP                    = 1UL << 7;
        public const ulong FEATURE_PAUSE                   = 1UL << 8;
        public const ulong FEATURE_RESUME                  = 1UL << 9;
        public const ulong FEATURE_SET_VOLUME              = 1UL << 10;
        public const ulong FEATURE_GET_VOLUME              = 1UL << 11;
        public const ulong FEATURE_SET_RATE                = 1UL << 12;
        public const ulong FEATURE_GET_RATE                = 1UL << 13;
        public const ulong FEATURE_SET_PITCH               = 1UL << 14;
        public const ulong FEATURE_GET_PITCH               = 1UL << 15;
        public const ulong FEATURE_REFRESH_VOICES          = 1UL << 16;
        public const ulong FEATURE_COUNT_VOICES            = 1UL << 17;
        public const ulong FEATURE_GET_VOICE_NAME          = 1UL << 18;
        public const ulong FEATURE_GET_VOICE_LANGUAGE      = 1UL << 19;
        public const ulong FEATURE_GET_VOICE               = 1UL << 20;
        public const ulong FEATURE_SET_VOICE               = 1UL << 21;

        // --- Config ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismConfig prism_config_init();

        // --- Context lifecycle ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_init(ref PrismConfig cfg);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_shutdown(IntPtr ctx);

        // --- Registry (engine enumeration / creation) ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern UIntPtr prism_registry_count(IntPtr ctx);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong prism_registry_id_at(IntPtr ctx, UIntPtr index);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_name(IntPtr ctx, ulong id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool prism_registry_exists(IntPtr ctx, ulong id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create(IntPtr ctx, ulong id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_registry_create_best(IntPtr ctx);

        // --- Backend lifecycle ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void prism_backend_free(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong prism_backend_get_features(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_initialize(IntPtr backend);

        // --- Speech ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_speak(
            IntPtr backend,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_output(
            IntPtr backend,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_braille(
            IntPtr backend,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_stop(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_is_speaking(
            IntPtr backend,
            [MarshalAs(UnmanagedType.I1)] out bool outSpeaking);

        // --- Voice parameters (float 0.0 - 1.0 normalized) ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_set_volume(IntPtr backend, float volume);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_set_rate(IntPtr backend, float rate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_set_pitch(IntPtr backend, float pitch);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_get_volume(IntPtr backend, out float outVolume);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_get_rate(IntPtr backend, out float outRate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_get_pitch(IntPtr backend, out float outPitch);

        // --- Voice enumeration / selection ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_refresh_voices(IntPtr backend);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_count_voices(IntPtr backend, out UIntPtr outCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_get_voice_name(
            IntPtr backend, UIntPtr voiceId, out IntPtr outName);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_get_voice_language(
            IntPtr backend, UIntPtr voiceId, out IntPtr outLanguage);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_set_voice(IntPtr backend, UIntPtr voiceId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern PrismError prism_backend_get_voice(IntPtr backend, out UIntPtr outVoiceId);

        // --- Error ---

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr prism_error_string(PrismError error);

        // --- Helpers ---

        public static string GetBackendName(IntPtr backend)
        {
            if (backend == IntPtr.Zero) return "none";
            var ptr = prism_backend_name(backend);
            return ptr != IntPtr.Zero ? (Marshal.PtrToStringUTF8(ptr) ?? "unknown") : "unknown";
        }

        public static string GetRegistryName(IntPtr ctx, ulong id)
        {
            var ptr = prism_registry_name(ctx, id);
            return ptr != IntPtr.Zero ? (Marshal.PtrToStringUTF8(ptr) ?? "unknown") : "unknown";
        }

        public static string GetErrorString(PrismError error)
        {
            var ptr = prism_error_string(error);
            return ptr != IntPtr.Zero ? (Marshal.PtrToStringUTF8(ptr) ?? error.ToString()) : error.ToString();
        }

        public static bool Supports(ulong features, ulong flag) => (features & flag) != 0;
    }

    /// <summary>Prism initialization config. Version must be 1.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct PrismConfig
    {
        public byte Version;
        public IntPtr Hwnd; // Deprecated, set to IntPtr.Zero
    }

    internal enum PrismError
    {
        Ok = 0,
        NotInitialized,
        InvalidParam,
        NotImplemented,
        NoVoices,
        VoiceNotFound,
        SpeakFailure,
        MemoryFailure,
        RangeOutOfBounds,
        Internal,
        NotSpeaking,
        NotPaused,
        AlreadyPaused,
        InvalidUtf8,
        InvalidOperation,
        AlreadyInitialized,
        BackendNotAvailable,
        Unknown,
        InvalidAudioFormat,
        InternalBackendLimitExceeded,
        BackendEnteredUndefinedState,
    }
}
