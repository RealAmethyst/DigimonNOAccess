using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// Manages the Steam Audio context and HRTF, shared across all PositionalAudio instances.
    /// Call Initialize() once at mod startup and Shutdown() at mod exit.
    /// </summary>
    public static class SteamAudioManager
    {
        public const int SampleRate = 44100;
        public const int FrameSize = 1024;

        private static IntPtr _context = IntPtr.Zero;
        private static IntPtr _hrtf = IntPtr.Zero;
        private static bool _initialized;
        private static bool _resolverRegistered;

        /// <summary>
        /// True if Steam Audio initialized successfully and HRTF processing is available.
        /// When false, PositionalAudio falls back to stereo panning.
        /// </summary>
        public static bool IsAvailable => _initialized && _context != IntPtr.Zero && _hrtf != IntPtr.Zero;

        public static IntPtr Context => _context;
        public static IntPtr Hrtf => _hrtf;

        public static IPLAudioSettings AudioSettings => new IPLAudioSettings
        {
            samplingRate = SampleRate,
            frameSize = FrameSize
        };

        /// <summary>
        /// Initialize Steam Audio context and HRTF.
        /// Safe to call if phonon.dll is missing - sets IsAvailable = false.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                RegisterDllResolver();

                // Create context
                var contextSettings = new IPLContextSettings
                {
                    version = SteamAudioNative.MakeVersion(4, 5, 2),
                    logCallback = IntPtr.Zero,
                    allocateCallback = IntPtr.Zero,
                    freeCallback = IntPtr.Zero,
                    simdLevel = IPLSIMDLevel.AVX2
                };

                var err = SteamAudioNative.iplContextCreate(ref contextSettings, out _context);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[SteamAudio] Context creation failed: {err}");
                    return;
                }

                // Create HRTF
                var audioSettings = AudioSettings;
                var hrtfSettings = new IPLHRTFSettings
                {
                    type = IPLHRTFType.Default,
                    sofaFileName = IntPtr.Zero,
                    sofaData = IntPtr.Zero,
                    sofaDataSize = 0,
                    volume = 1.0f,
                    normType = IPLHRTFNormType.None
                };

                err = SteamAudioNative.iplHRTFCreate(_context, ref audioSettings, ref hrtfSettings, out _hrtf);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[SteamAudio] HRTF creation failed: {err}");
                    SteamAudioNative.iplContextRelease(ref _context);
                    return;
                }

                _initialized = true;
                DebugLogger.Log("[SteamAudio] Initialized successfully - HRTF binaural audio available");

                // Initialize environmental audio (scene, simulator, geometry)
                SteamAudioEnvironment.Initialize();
            }
            catch (DllNotFoundException)
            {
                DebugLogger.Warning("[SteamAudio] phonon.dll not found - falling back to stereo panning");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudio] Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a binaural effect for a single audio source.
        /// Each PositionalAudio instance should have its own effect.
        /// Returns IntPtr.Zero on failure.
        /// </summary>
        public static IntPtr CreateBinauralEffect()
        {
            if (!IsAvailable) return IntPtr.Zero;

            try
            {
                var audioSettings = AudioSettings;
                var effectSettings = new IPLBinauralEffectSettings
                {
                    hrtf = _hrtf
                };

                IPLerror err = SteamAudioNative.iplBinauralEffectCreate(
                    _context, ref audioSettings, ref effectSettings, out IntPtr effect);

                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[SteamAudio] Binaural effect creation failed: {err}");
                    return IntPtr.Zero;
                }

                return effect;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudio] CreateBinauralEffect error: {ex.Message}");
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Release a binaural effect when a PositionalAudio instance is disposed.
        /// </summary>
        public static void ReleaseBinauralEffect(ref IntPtr effect)
        {
            if (effect == IntPtr.Zero) return;

            try
            {
                SteamAudioNative.iplBinauralEffectRelease(ref effect);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudio] ReleaseBinauralEffect error: {ex.Message}");
                effect = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Shut down Steam Audio and release all shared resources.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized) return;

            // Shut down environmental audio before releasing core resources
            SteamAudioEnvironment.Shutdown();

            try
            {
                if (_hrtf != IntPtr.Zero)
                    SteamAudioNative.iplHRTFRelease(ref _hrtf);

                if (_context != IntPtr.Zero)
                    SteamAudioNative.iplContextRelease(ref _context);

                DebugLogger.Log("[SteamAudio] Shut down");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[SteamAudio] Shutdown error: {ex.Message}");
            }
            finally
            {
                _context = IntPtr.Zero;
                _hrtf = IntPtr.Zero;
                _initialized = false;
            }
        }

        private static void RegisterDllResolver()
        {
            if (_resolverRegistered) return;
            _resolverRegistered = true;

            NativeLibrary.SetDllImportResolver(typeof(SteamAudioManager).Assembly, (name, assembly, path) =>
            {
                if (name != "phonon") return IntPtr.Zero;

                // Look for phonon.dll next to the mod DLL
                string modDir = Path.GetDirectoryName(assembly.Location);
                string dllPath = Path.Combine(modDir, "phonon.dll");

                if (File.Exists(dllPath) && NativeLibrary.TryLoad(dllPath, out IntPtr handle))
                    return handle;

                // Try one directory up (Mods/ → game root)
                string parentDir = Path.GetDirectoryName(modDir);
                dllPath = Path.Combine(parentDir, "phonon.dll");

                if (File.Exists(dllPath) && NativeLibrary.TryLoad(dllPath, out handle))
                    return handle;

                return IntPtr.Zero;
            });
        }
    }
}
