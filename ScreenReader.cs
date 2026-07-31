using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    /// <summary>
    /// Screen reader output for the whole mod.
    ///
    /// Speech goes through Prism, a screen-reader abstraction that talks to whatever
    /// the player actually runs - NVDA, JAWS, SAPI, OneCore on Windows, Orca or
    /// speech-dispatcher on Linux, VoiceOver on Mac. Nothing here is tied to one
    /// screen reader or one operating system.
    ///
    /// prism.dll ships next to the mod assembly and is loaded from there explicitly,
    /// so the game folder does not have to be on the native search path. If it is
    /// missing or no backend is available the mod stays fully functional and simply
    /// says nothing - every method below is a safe no-op in that state.
    /// </summary>
    public static class ScreenReader
    {
        private const string PrismFileName = "prism.dll";

        private static IntPtr _context = IntPtr.Zero;
        private static IntPtr _backend = IntPtr.Zero;
        private static ulong _backendFeatures;
        private static string _backendName = "";

        // Prism backends are not documented as thread-safe. Speech is normally raised
        // from the game thread, but Harmony patches and native hooks can fire from
        // wherever the game calls them, so every backend touch is serialized.
        private static readonly object _speechLock = new object();

        private static bool _initialized = false;
        private static string _lastMessage = "";
        private static ulong _backendId;

        // Two engines have to be excluded by hand because they misreport themselves.
        // Everything else is judged at runtime by IsBackendLive.
        //
        // UIA does not speak at all - it only forwards accessibility events to
        // whatever assistive technology is listening - so it "works" exactly when
        // Narrator is already running, and then the player already has speech.
        //
        // OneCore claims FEATURE_IS_SUPPORTED_AT_RUNTIME and advertises speech, but
        // stays completely silent unless OneCore voices have been installed through
        // Windows Settings. Confirmed on Amethyst's machine 2026-07-31: it passed the
        // availability probe, appeared in the picker, and said nothing when selected.
        // There is no flag that distinguishes it from a working engine, so it is
        // excluded by name.
        //
        // Either can still be forced by setting SpeechEngineId in settings.json -
        // Initialize honours a saved preference before it consults this list.
        private static readonly ulong[] AlwaysHiddenBackends =
        {
            PrismInterop.BACKEND_UIA,
            PrismInterop.BACKEND_ONE_CORE,
        };

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private static readonly uint OurProcessId = (uint)Environment.ProcessId;
        private static bool _wasFocused = true;

        /// <summary>True when the game window is the one the player is looking at.</summary>
        private static bool IsGameFocused()
        {
            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;
                GetWindowThreadProcessId(hwnd, out uint pid);
                return pid == OurProcessId;
            }
            catch
            {
                // If we cannot tell, assume focused rather than going silent.
                return true;
            }
        }

        /// <summary>
        /// Called once per frame from Main. Stops speech mid-sentence when the player
        /// alt-tabs away, so a long announcement does not keep talking over whatever
        /// they switched to. Speak() separately refuses new speech while unfocused.
        /// </summary>
        public static void UpdateFocusState()
        {
            if (!_initialized || !ModSettings.SpeakOnlyWhenFocused)
            {
                _wasFocused = true;
                return;
            }

            bool focused = IsGameFocused();
            if (_wasFocused && !focused)
                Silence();
            _wasFocused = focused;
        }

        /// <summary>
        /// Load Prism and pick a speech backend.
        /// </summary>
        /// <param name="modFolderPath">
        /// Folder holding prism.dll. Defaults to the folder this assembly was loaded from.
        /// </param>
        /// <returns>True when speech is available.</returns>
        public static bool Initialize(string modFolderPath = null)
        {
            if (_initialized)
                return true;

            try
            {
                if (!LoadPrismLibrary(modFolderPath))
                    return false;

                var config = PrismInterop.prism_config_init();
                _context = PrismInterop.prism_init(ref config);
                if (_context == IntPtr.Zero)
                {
                    DebugLogger.Error("[ScreenReader] prism_init failed - no speech available");
                    return false;
                }

                LogAvailableBackends();

                // A saved engine preference wins, but only if it actually initializes.
                ulong preferred = ModSettings.SpeechEngineId;
                if (preferred != 0 && PrismInterop.prism_registry_exists(_context, preferred)
                    && TryCreateBackend(preferred))
                {
                    _initialized = true;
                    DebugLogger.Log($"[ScreenReader] Speaking through '{_backendName}' (saved preference)");
                    ApplyVoiceAndParams();
                    return true;
                }

                // Walk the registry ourselves rather than calling
                // prism_registry_create_best. That helper considers every registered
                // backend, including UIA and OneCore - both of which initialize
                // successfully and advertise speech while producing no audible output
                // unless the player has an AT consumer or OneCore voices installed.
                // Picking one of those would leave the mod completely silent with no
                // indication why, and there is no Tolk fallback any more.
                if (!SelectFirstUsableBackend())
                {
                    DebugLogger.Warning("[ScreenReader] Prism found no usable speech backend");
                    ShutdownContext();
                    return false;
                }

                _initialized = true;
                DebugLogger.Log($"[ScreenReader] Speaking through '{_backendName}' (features 0x{_backendFeatures:X})");
                ApplyVoiceAndParams();
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ScreenReader] Failed to initialize Prism: {ex.Message}");
                _initialized = false;
                return false;
            }
        }

        /// <summary>
        /// Load prism.dll from the mod folder. Falls back to the default native search
        /// path so a copy in the game root also works.
        /// </summary>
        private static bool LoadPrismLibrary(string modFolderPath)
        {
            string folder = modFolderPath;
            if (string.IsNullOrEmpty(folder))
                folder = Path.GetDirectoryName(typeof(ScreenReader).Assembly.Location);

            if (!string.IsNullOrEmpty(folder))
            {
                string prismPath = Path.Combine(folder, PrismFileName);
                if (File.Exists(prismPath))
                {
                    if (NativeLibrary.TryLoad(prismPath, out _))
                    {
                        DebugLogger.Log($"[ScreenReader] Loaded {prismPath}");
                        return true;
                    }
                    DebugLogger.Error($"[ScreenReader] Found but could not load {prismPath} - is it the right architecture (x64)?");
                    return false;
                }
                DebugLogger.Warning($"[ScreenReader] {PrismFileName} not found at {prismPath}, trying the default search path");
            }

            // Last resort: let the OS resolve it (e.g. a copy sitting in the game root).
            if (NativeLibrary.TryLoad(PrismFileName, out _))
            {
                DebugLogger.Log($"[ScreenReader] Loaded {PrismFileName} from the default search path");
                return true;
            }

            DebugLogger.Error($"[ScreenReader] {PrismFileName} could not be loaded - the mod will run silently");
            return false;
        }

        private static void LogAvailableBackends()
        {
            try
            {
                var count = (ulong)PrismInterop.prism_registry_count(_context);
                var names = new List<string>();
                for (ulong i = 0; i < count; i++)
                {
                    ulong id = PrismInterop.prism_registry_id_at(_context, (UIntPtr)i);
                    names.Add(PrismInterop.GetRegistryName(_context, id));
                }
                DebugLogger.Log($"[ScreenReader] Prism backends ({count}): {string.Join(", ", names)}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ScreenReader] Could not enumerate backends: {ex.Message}");
            }
        }

        /// <summary>
        /// Shut down speech and release Prism.
        /// </summary>
        public static void Shutdown()
        {
            lock (_speechLock)
            {
                if (!_initialized && _context == IntPtr.Zero)
                    return;

                _initialized = false;

                try
                {
                    if (_backend != IntPtr.Zero)
                    {
                        PrismInterop.prism_backend_stop(_backend);
                        PrismInterop.prism_backend_free(_backend);
                        _backend = IntPtr.Zero;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[ScreenReader] Error freeing backend: {ex.Message}");
                }

                ShutdownContext();
                DebugLogger.Log("[ScreenReader] Shut down");
            }
        }

        private static void ShutdownContext()
        {
            try
            {
                if (_context != IntPtr.Zero)
                {
                    PrismInterop.prism_shutdown(_context);
                    _context = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ScreenReader] Error shutting down Prism context: {ex.Message}");
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

            // Drop speech while the player is in another window, so a period spent
            // alt-tabbed does not replay at them when they come back.
            if (ModSettings.SpeakOnlyWhenFocused && !IsGameFocused())
                return;

            lock (_speechLock)
            {
                if (!_initialized || _backend == IntPtr.Zero)
                    return;

                try
                {
                    PrismError err;
                    if (PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_OUTPUT))
                        err = PrismInterop.prism_backend_output(_backend, text, interrupt);
                    else
                        err = PrismInterop.prism_backend_speak(_backend, text, interrupt);

                    if (err != PrismError.Ok)
                        DebugLogger.Warning($"[ScreenReader] Speech failed: {PrismInterop.GetErrorString(err)}");
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[ScreenReader] Speech output failed: {ex.Message}");
                }
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

            lock (_speechLock)
            {
                if (!_initialized || _backend == IntPtr.Zero)
                    return;

                try
                {
                    PrismInterop.prism_backend_stop(_backend);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[ScreenReader] Error in Silence: {ex.Message}");
                }
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

        /// <summary>
        /// Name of the speech backend in use, for diagnostics. Empty when unavailable.
        /// </summary>
        public static string BackendName => _backendName;

        /// <summary>Registry ID of the backend in use. 0 when unavailable.</summary>
        public static ulong BackendId => _backendId;

        public static bool SupportsVoice  => PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_SET_VOICE);
        public static bool SupportsRate   => PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_SET_RATE);
        public static bool SupportsVolume => PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_SET_VOLUME);

        // === Engine selection ===

        /// <summary>
        /// Speech engines the player can actually pick: only those Prism reports as
        /// usable on this machine right now. Offering one that cannot speak is worse
        /// than not offering it, because selecting it goes silent with no explanation.
        ///
        /// If nothing reports itself available the full list is returned instead of an
        /// empty one - an unfiltered picker still beats no picker at all.
        /// </summary>
        public static List<(ulong Id, string Name)> EnumerateBackends()
        {
            var list = new List<(ulong, string)>();
            if (_context == IntPtr.Zero) return list;

            try
            {
                foreach (ulong id in RegistryIds())
                {
                    if (Array.IndexOf(AlwaysHiddenBackends, id) >= 0) continue;
                    if (!IsBackendLive(id)) continue;
                    list.Add((id, PrismInterop.GetRegistryName(_context, id)));
                }

                if (list.Count == 0)
                {
                    DebugLogger.Log("[ScreenReader] No engine reported itself available; listing all of them");
                    foreach (ulong id in RegistryIds())
                    {
                        if (Array.IndexOf(AlwaysHiddenBackends, id) >= 0) continue;
                        list.Add((id, PrismInterop.GetRegistryName(_context, id)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"[ScreenReader] EnumerateBackends failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Switch speech engine. The replacement is created and initialized BEFORE the
        /// old one is released, so a backend the player does not actually have
        /// installed leaves them on the working engine instead of going silent.
        /// </summary>
        public static bool SwitchBackend(ulong newId)
        {
            if (_context == IntPtr.Zero || newId == 0) return false;
            if (newId == _backendId) return true;

            if (!PrismInterop.prism_registry_exists(_context, newId))
            {
                DebugLogger.Warning($"[ScreenReader] Engine 0x{newId:X16} is not in the registry");
                return false;
            }

            IntPtr candidate = PrismInterop.prism_registry_create(_context, newId);
            if (candidate == IntPtr.Zero)
            {
                DebugLogger.Warning($"[ScreenReader] Could not create engine 0x{newId:X16}");
                return false;
            }

            string candidateName = PrismInterop.GetBackendName(candidate);
            var err = PrismInterop.prism_backend_initialize(candidate);
            if (err != PrismError.Ok && err != PrismError.AlreadyInitialized)
            {
                DebugLogger.Warning(
                    $"[ScreenReader] Engine '{candidateName}' failed to initialize: {PrismInterop.GetErrorString(err)} - staying on '{_backendName}'");
                PrismInterop.prism_backend_free(candidate);
                return false;
            }

            ulong candidateFeatures = PrismInterop.prism_backend_get_features(candidate);
            if (!PrismInterop.Supports(candidateFeatures, PrismInterop.FEATURE_OUTPUT) &&
                !PrismInterop.Supports(candidateFeatures, PrismInterop.FEATURE_SPEAK))
            {
                DebugLogger.Warning($"[ScreenReader] Engine '{candidateName}' cannot speak - staying on '{_backendName}'");
                PrismInterop.prism_backend_free(candidate);
                return false;
            }

            IntPtr old;
            lock (_speechLock)
            {
                old = _backend;
                _backend = candidate;
                _backendId = newId;
                _backendName = candidateName;
                _backendFeatures = candidateFeatures;
                _initialized = true;
            }

            if (old != IntPtr.Zero)
            {
                try { PrismInterop.prism_backend_stop(old); } catch { }
                PrismInterop.prism_backend_free(old);
            }

            ApplyVoiceAndParams();
            DebugLogger.Log($"[ScreenReader] Switched engine to '{candidateName}'");
            return true;
        }

        /// <summary>
        /// Try each registered backend in order and keep the first that actually
        /// initializes and can speak, skipping the ones known to be silent in
        /// practice. Only if every visible backend fails do we fall back to the
        /// hidden ones - a possibly-silent engine still beats no speech at all.
        /// </summary>
        private static bool SelectFirstUsableBackend()
        {
            int live = 0;

            try
            {
                // First pass: only engines Prism reports as actually usable right now.
                foreach (ulong id in RegistryIds())
                {
                    if (Array.IndexOf(AlwaysHiddenBackends, id) >= 0) continue;
                    if (!IsBackendLive(id)) continue;

                    live++;
                    if (TryCreateBackend(id))
                        return true;
                }

                // Second pass: if nothing reported itself available, the runtime check
                // may not be populated on this build. Try everything rather than stay
                // mute.
                foreach (ulong id in RegistryIds())
                {
                    if (Array.IndexOf(AlwaysHiddenBackends, id) >= 0) continue;

                    if (TryCreateBackend(id))
                    {
                        DebugLogger.Warning(
                            $"[ScreenReader] No engine reported itself available; fell back to '{_backendName}'. "
                            + "If you hear nothing, pick another engine in the Speech settings.");
                        return true;
                    }
                }

                DebugLogger.Error($"[ScreenReader] No backend could speak. {live} engine(s) reported available.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ScreenReader] Backend selection failed: {ex.Message}");
            }

            return false;
        }

        /// <summary>Every backend ID in the registry, in registry order.</summary>
        private static List<ulong> RegistryIds()
        {
            var ids = new List<ulong>();
            try
            {
                var count = (ulong)PrismInterop.prism_registry_count(_context);
                for (ulong i = 0; i < count; i++)
                    ids.Add(PrismInterop.prism_registry_id_at(_context, (UIntPtr)i));
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"[ScreenReader] Could not enumerate the registry: {ex.Message}");
            }
            return ids;
        }

        /// <summary>
        /// Whether an engine is actually usable on this machine right now - NVDA
        /// running, SAPI voices present, and so on. Prism answers this with
        /// FEATURE_IS_SUPPORTED_AT_RUNTIME, so we create the backend just far enough
        /// to read its feature mask and free it again without initializing it.
        ///
        /// The already-active backend short-circuits to true: it is demonstrably
        /// working, and tearing it down to ask would be pointless.
        /// </summary>
        private static bool IsBackendLive(ulong id)
        {
            if (id != 0 && id == _backendId && _backend != IntPtr.Zero)
                return true;

            IntPtr probe = IntPtr.Zero;
            try
            {
                probe = PrismInterop.prism_registry_create(_context, id);
                if (probe == IntPtr.Zero)
                    return false;

                ulong features = PrismInterop.prism_backend_get_features(probe);

                bool supported = PrismInterop.Supports(features, PrismInterop.FEATURE_IS_SUPPORTED_AT_RUNTIME);
                bool canSpeak = PrismInterop.Supports(features, PrismInterop.FEATURE_OUTPUT)
                                || PrismInterop.Supports(features, PrismInterop.FEATURE_SPEAK);

                return supported && canSpeak;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[ScreenReader] Availability probe failed for 0x{id:X16}: {ex.Message}");
                return false;
            }
            finally
            {
                if (probe != IntPtr.Zero)
                {
                    try { PrismInterop.prism_backend_free(probe); } catch { }
                }
            }
        }

        private static bool TryCreateBackend(ulong id)
        {
            IntPtr backend = PrismInterop.prism_registry_create(_context, id);
            if (backend == IntPtr.Zero) return false;

            string name = PrismInterop.GetBackendName(backend);
            var err = PrismInterop.prism_backend_initialize(backend);
            if (err != PrismError.Ok && err != PrismError.AlreadyInitialized)
            {
                DebugLogger.Warning($"[ScreenReader] Saved engine '{name}' failed to initialize: {PrismInterop.GetErrorString(err)}");
                PrismInterop.prism_backend_free(backend);
                return false;
            }

            ulong features = PrismInterop.prism_backend_get_features(backend);
            if (!PrismInterop.Supports(features, PrismInterop.FEATURE_OUTPUT) &&
                !PrismInterop.Supports(features, PrismInterop.FEATURE_SPEAK))
            {
                PrismInterop.prism_backend_free(backend);
                return false;
            }

            _backend = backend;
            _backendId = id;
            _backendName = name;
            _backendFeatures = features;
            return true;
        }

        /// <summary>
        /// Prism has no backend-to-ID getter on a live instance, so the boot-time
        /// "best available" path has to match the name back against the registry.
        /// Runs once.
        /// </summary>
        private static ulong ResolveBackendIdByName(string name)
        {
            try
            {
                var count = (ulong)PrismInterop.prism_registry_count(_context);
                for (ulong i = 0; i < count; i++)
                {
                    ulong id = PrismInterop.prism_registry_id_at(_context, (UIntPtr)i);
                    if (PrismInterop.GetRegistryName(_context, id) == name)
                        return id;
                }
            }
            catch { }
            return 0;
        }

        // === Voices and voice parameters ===

        /// <summary>
        /// Voice names on the current engine, in index order. Empty when the engine
        /// does not do voice selection - screen readers use their own settings.
        /// </summary>
        public static List<string> EnumerateVoices()
        {
            var list = new List<string>();
            if (!_initialized || _backend == IntPtr.Zero) return list;
            if (!PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_COUNT_VOICES)) return list;

            lock (_speechLock)
            {
                try
                {
                    if (PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_REFRESH_VOICES))
                        PrismInterop.prism_backend_refresh_voices(_backend);

                    if (PrismInterop.prism_backend_count_voices(_backend, out UIntPtr count) != PrismError.Ok)
                        return list;

                    for (ulong i = 0; i < (ulong)count; i++)
                    {
                        if (PrismInterop.prism_backend_get_voice_name(_backend, (UIntPtr)i, out IntPtr namePtr) == PrismError.Ok
                            && namePtr != IntPtr.Zero)
                        {
                            list.Add(Marshal.PtrToStringUTF8(namePtr) ?? $"Voice {i}");
                        }
                        else
                        {
                            list.Add($"Voice {i}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[ScreenReader] EnumerateVoices failed: {ex.Message}");
                }
            }
            return list;
        }

        /// <summary>
        /// The voice the engine is actually speaking with right now, or null if it
        /// does not expose one. Used when the player has no saved preference, so the
        /// menu names the voice they can actually hear instead of guessing the first
        /// in the list.
        /// </summary>
        public static string CurrentVoiceName
        {
            get
            {
                if (!_initialized || _backend == IntPtr.Zero) return null;
                if (!PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_GET_VOICE)) return null;
                if (!PrismInterop.Supports(_backendFeatures, PrismInterop.FEATURE_GET_VOICE_NAME)) return null;

                lock (_speechLock)
                {
                    try
                    {
                        if (PrismInterop.prism_backend_get_voice(_backend, out UIntPtr index) != PrismError.Ok)
                            return null;

                        if (PrismInterop.prism_backend_get_voice_name(_backend, index, out IntPtr namePtr) != PrismError.Ok
                            || namePtr == IntPtr.Zero)
                            return null;

                        return Marshal.PtrToStringUTF8(namePtr);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[ScreenReader] Could not read the current voice: {ex.Message}");
                        return null;
                    }
                }
            }
        }

        /// <summary>
        /// Select a voice by name. Names are used rather than indices because indices
        /// shift when the voice list is refreshed.
        /// </summary>
        public static bool SetVoiceByName(string voiceName)
        {
            if (!_initialized || string.IsNullOrEmpty(voiceName) || !SupportsVoice) return false;

            var voices = EnumerateVoices();
            int index = voices.IndexOf(voiceName);
            if (index < 0) return false;

            lock (_speechLock)
            {
                var err = PrismInterop.prism_backend_set_voice(_backend, (UIntPtr)(ulong)index);
                if (err != PrismError.Ok)
                {
                    DebugLogger.Warning($"[ScreenReader] Could not select voice '{voiceName}': {PrismInterop.GetErrorString(err)}");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Push the saved voice, rate and volume onto the current engine. Anything the
        /// engine does not support is skipped - screen readers deliberately ignore
        /// these and use the player's own screen reader settings instead.
        /// </summary>
        public static void ApplyVoiceAndParams()
        {
            if (!_initialized || _backend == IntPtr.Zero) return;

            if (SupportsVoice && !string.IsNullOrEmpty(ModSettings.SpeechVoice))
                SetVoiceByName(ModSettings.SpeechVoice);

            lock (_speechLock)
            {
                try
                {
                    if (SupportsRate)
                        PrismInterop.prism_backend_set_rate(_backend, Math.Clamp(ModSettings.SpeechRatePercent, 1, 100) / 100f);

                    if (SupportsVolume)
                        PrismInterop.prism_backend_set_volume(_backend, Math.Clamp(ModSettings.SpeechVolumePercent, 1, 100) / 100f);
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[ScreenReader] Could not apply voice parameters: {ex.Message}");
                }
            }
        }
    }
}
