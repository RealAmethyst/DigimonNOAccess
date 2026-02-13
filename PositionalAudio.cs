using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides true positional audio using NAudio, bypassing the game's audio system.
    /// Uses Steam Audio HRTF for 3D binaural spatialization when available,
    /// falls back to stereo panning when phonon.dll is not present.
    /// Supports loading custom WAV files for different object types.
    /// Audio output is routed through the shared AudioOutputMixer (single WaveOutEvent).
    /// </summary>
    public class PositionalAudio : IDisposable
    {
        // Audio chain (no WaveOutEvent - uses shared AudioOutputMixer)
        private PanningSampleProvider _panner;
        private HrtfSampleProvider _hrtfProvider;
        private VolumeSampleProvider _volumeProvider;
        private SignalGenerator _signalGenerator;

        // Whether this instance is using HRTF (true) or panning fallback (false)
        private bool _useHrtf;

        // Whether our _volumeProvider is currently registered with the mixer
        private bool _addedToMixer;

        // Position tracking (full 3D camera orientation for HRTF)
        private float _targetX, _targetY, _targetZ;
        private float _playerX, _playerY, _playerZ;
        private float _camFwdX, _camFwdY, _camFwdZ;
        private float _camUpX, _camUpY, _camUpZ;
        private readonly object _positionLock = new object();

        // Configuration
        private float _maxDistance = 50f;
        private float _minVolume = 0.05f;
        private float _maxVolume = 0.8f;
        private bool _isPlaying = false;
        private bool _disposed = false;
        private bool _useWavFile = false;

        // Update thread
        private Thread _updateThread;
        private bool _shouldUpdate = false;

        // Sound files directory
        private static string _soundsPath;

        // Different sound types for different objects
        public enum SoundType
        {
            Item,       // Items (item.wav)
            NPC,        // NPCs (potential npc.wav)
            Enemy,      // Enemy Digimon (potential enemie digimon.wav)
            Transition, // Area transitions (transission.wav)
            Facility    // Facilities, fishing, toilets (facility.wav)
        }

        private SoundType _currentSoundType = SoundType.Item;

        // Static audio cache: read each WAV file once, reuse across all instances
        private static readonly Dictionary<string, (float[] samples, WaveFormat format)> _audioCache
            = new Dictionary<string, (float[], WaveFormat)>();
        private static readonly object _cacheLock = new object();
        private static readonly Random _cacheRandom = new Random();

        public PositionalAudio()
        {
            try
            {
                // Find sounds folder relative to mod DLL location (only resolves once)
                if (_soundsPath == null)
                {
                    string modPath = Path.GetDirectoryName(typeof(PositionalAudio).Assembly.Location);
                    _soundsPath = Path.Combine(Path.GetDirectoryName(modPath), "sounds");
                    if (!Directory.Exists(_soundsPath))
                        _soundsPath = Path.Combine(modPath, "sounds");
                    DebugLogger.Log($"[PositionalAudio] Sounds path: {_soundsPath}");
                }

                InitializeAudio(SoundType.Item);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[PositionalAudio] Init error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Get the WAV file path for a sound type
        /// </summary>
        private string GetSoundFilePath(SoundType soundType)
        {
            string filename = soundType switch
            {
                SoundType.Item => "item.wav",
                SoundType.NPC => "potential npc.wav",
                SoundType.Enemy => "potential enemie digimon.wav",
                SoundType.Transition => "transission.wav",
                SoundType.Facility => "facility.wav",
                _ => "item.wav"
            };
            return Path.Combine(_soundsPath, filename);
        }

        private void InitializeAudio(SoundType soundType)
        {
            // Clean up existing (removes from mixer if needed)
            StopInternal();

            _currentSoundType = soundType;
            _useWavFile = false;
            _useHrtf = false;

            // Try to load WAV file first
            string wavPath = GetSoundFilePath(soundType);
            if (File.Exists(wavPath))
            {
                try
                {
                    InitializeFromWavFile(wavPath);
                    _useWavFile = true;
                    return;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[PositionalAudio] Failed to load WAV: {ex.Message}, falling back to generated tone");
                }
            }
            else
            {
                // Facility type: no WAV = no audio (no tone fallback)
                if (soundType == SoundType.Facility)
                {
                    DebugLogger.Log($"[PositionalAudio] WAV file not found: {wavPath}, facility audio disabled");
                    return;
                }
                DebugLogger.Log($"[PositionalAudio] WAV file not found: {wavPath}, using generated tone");
            }

            // Fallback to generated tone (not used for Facility type)
            InitializeFromGenerator(soundType);
        }

        private void InitializeFromWavFile(string wavPath)
        {
            // Use cached audio data - only reads from disk the first time per file
            var (samples, format) = LoadOrGetCachedAudio(wavPath);

            // Random start offset prevents comb filtering when many instances play the same WAV
            int startOffset;
            lock (_cacheLock) { startOffset = _cacheRandom.Next(samples.Length); }
            var monoSource = new CachedLoopingSampleProvider(samples, format, startOffset);

            BuildAudioChain(monoSource);
        }

        internal static (float[] samples, WaveFormat format) LoadOrGetCachedAudio(string wavPath)
        {
            lock (_cacheLock)
            {
                if (_audioCache.TryGetValue(wavPath, out var cached))
                    return cached;
            }

            // First load: read WAV file, decode to mono float samples
            using (var reader = new AudioFileReader(wavPath))
            {
                ISampleProvider source;
                if (reader.WaveFormat.Channels == 2)
                    source = new StereoToMonoSampleProvider(reader);
                else
                    source = reader;

                var allSamples = new List<float>();
                float[] buf = new float[4096];
                int read;
                while ((read = source.Read(buf, 0, buf.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                        allSamples.Add(buf[i]);
                }

                var samples = allSamples.ToArray();
                var format = WaveFormat.CreateIeeeFloatWaveFormat(reader.WaveFormat.SampleRate, 1);

                lock (_cacheLock)
                {
                    _audioCache[wavPath] = (samples, format);
                }

                DebugLogger.Log($"[PositionalAudio] Cached {wavPath}: {samples.Length} samples ({samples.Length / (float)format.SampleRate:F1}s)");
                return (samples, format);
            }
        }

        private void InitializeFromGenerator(SoundType soundType)
        {
            // Create signal generator based on sound type
            float frequency;
            SignalGeneratorType signalType;

            switch (soundType)
            {
                case SoundType.NPC:
                    frequency = 600f;
                    signalType = SignalGeneratorType.Sin;
                    break;
                case SoundType.Enemy:
                    frequency = 880f;
                    signalType = SignalGeneratorType.Square;
                    break;
                case SoundType.Transition:
                    frequency = 523f;
                    signalType = SignalGeneratorType.Triangle;
                    break;
                case SoundType.Item:
                default:
                    frequency = 440f;
                    signalType = SignalGeneratorType.Sin;
                    break;
            }

            // Create mono signal generator
            _signalGenerator = new SignalGenerator(44100, 1)
            {
                Frequency = frequency,
                Type = signalType,
                Gain = 0.3
            };

            BuildAudioChain(_signalGenerator);

            DebugLogger.Log($"[PositionalAudio] Audio initialized with generator: {soundType}, freq={frequency}Hz");
        }

        /// <summary>
        /// Build the audio processing chain from a mono source.
        /// Uses HRTF when available, falls back to stereo panning.
        /// Chain ends at VolumeSampleProvider which is registered with AudioOutputMixer.
        /// </summary>
        private void BuildAudioChain(ISampleProvider monoSource)
        {
            ISampleProvider stereoSource;

            if (SteamAudioManager.IsAvailable)
            {
                // HRTF: spatialize mono → stereo
                _hrtfProvider = new HrtfSampleProvider(monoSource);
                stereoSource = _hrtfProvider;
                _useHrtf = true;
                _panner = null;
            }
            else
            {
                // Fallback: stereo panning (no front/back distinction)
                _panner = new PanningSampleProvider(monoSource)
                {
                    Pan = 0f
                };
                stereoSource = _panner;
                _hrtfProvider = null;
                _useHrtf = false;
            }

            _volumeProvider = new VolumeSampleProvider(stereoSource)
            {
                Volume = 0f // Start silent, UpdateAudioParameters will set real volume
            };
        }

        /// <summary>
        /// Start playing positional audio for a target
        /// </summary>
        public void StartTracking(SoundType soundType, float maxDistance = 50f)
        {
            try
            {
                if (_currentSoundType != soundType || _volumeProvider == null)
                {
                    InitializeAudio(soundType);
                }

                _maxDistance = maxDistance;
                _isPlaying = true;
                _shouldUpdate = true;

                // Register with shared mixer if not already
                if (!_addedToMixer && _volumeProvider != null)
                {
                    AudioOutputMixer.AddInput(_volumeProvider);
                    _addedToMixer = true;
                }

                // Start position update thread
                if (_updateThread == null || !_updateThread.IsAlive)
                {
                    _updateThread = new Thread(UpdateLoop)
                    {
                        IsBackground = true,
                        Name = "PositionalAudio_Update"
                    };
                    _updateThread.Start();
                }

                // Reduced logging - only significant events
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[PositionalAudio] StartTracking error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop playing
        /// </summary>
        public void Stop()
        {
            _shouldUpdate = false;
            _isPlaying = false;
            StopInternal();
            DebugLogger.Log("[PositionalAudio] Stopped");
        }

        /// <summary>
        /// Change the sound type while playing (for continuous tracking mode)
        /// </summary>
        public void ChangeSoundType(SoundType soundType, float maxDistance = 50f)
        {
            if (_currentSoundType == soundType)
            {
                // Same sound type, just update max distance
                _maxDistance = maxDistance;
                return;
            }

            // Different sound type - reinitialize audio
            DebugLogger.Log($"[PositionalAudio] Changing sound from {_currentSoundType} to {soundType}");

            bool wasPlaying = _isPlaying;
            _maxDistance = maxDistance;

            // Reinitialize with new sound type (StopInternal removes from mixer)
            InitializeAudio(soundType);

            // Resume if we were playing
            if (wasPlaying)
            {
                _isPlaying = true;
                _shouldUpdate = true;

                // Re-register with mixer
                if (!_addedToMixer && _volumeProvider != null)
                {
                    AudioOutputMixer.AddInput(_volumeProvider);
                    _addedToMixer = true;
                }

                // Restart update thread if needed
                if (_updateThread == null || !_updateThread.IsAlive)
                {
                    _updateThread = new Thread(UpdateLoop)
                    {
                        IsBackground = true,
                        Name = "PositionalAudio_Update"
                    };
                    _updateThread.Start();
                }
            }
        }

        private void StopInternal()
        {
            // Remove from mixer first (before disposing the chain)
            if (_addedToMixer && _volumeProvider != null)
            {
                AudioOutputMixer.RemoveInput(_volumeProvider);
                _addedToMixer = false;
            }

            try
            {
                _hrtfProvider?.Dispose();
                _hrtfProvider = null;
            }
            catch { }

            _signalGenerator = null;
            _panner = null;
            _volumeProvider = null;
        }

        /// <summary>
        /// Update the target position (call this from game thread)
        /// </summary>
        public void UpdateTargetPosition(float x, float y, float z)
        {
            lock (_positionLock)
            {
                _targetX = x;
                _targetY = y;
                _targetZ = z;
            }
        }

        /// <summary>
        /// Update the player position and camera orientation (call this from game thread).
        /// Uses full 3D camera vectors for proper HRTF spatialization matching the game's AudioListener.
        /// </summary>
        public void UpdatePlayerPosition(float x, float y, float z,
            float camFwdX, float camFwdY, float camFwdZ,
            float camUpX, float camUpY, float camUpZ)
        {
            lock (_positionLock)
            {
                _playerX = x;
                _playerY = y;
                _playerZ = z;
                _camFwdX = camFwdX;
                _camFwdY = camFwdY;
                _camFwdZ = camFwdZ;
                _camUpX = camUpX;
                _camUpY = camUpY;
                _camUpZ = camUpZ;
            }
        }

        /// <summary>
        /// Background thread that continuously updates audio parameters
        /// </summary>
        private void UpdateLoop()
        {
            while (_shouldUpdate && !_disposed)
            {
                try
                {
                    UpdateAudioParameters();
                    Thread.Sleep(16); // ~60fps update rate
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[PositionalAudio] Update error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        private void UpdateAudioParameters()
        {
            if (_volumeProvider == null)
                return;

            float targetX, targetY, targetZ;
            float playerX, playerY, playerZ;
            float cfX, cfY, cfZ, cuX, cuY, cuZ;

            lock (_positionLock)
            {
                targetX = _targetX;
                targetY = _targetY;
                targetZ = _targetZ;
                playerX = _playerX;
                playerY = _playerY;
                playerZ = _playerZ;
                cfX = _camFwdX; cfY = _camFwdY; cfZ = _camFwdZ;
                cuX = _camUpX;  cuY = _camUpY;  cuZ = _camUpZ;
            }

            // Calculate 3D direction and distance to target
            float dx = targetX - playerX;
            float dy = targetY - playerY;
            float dz = targetZ - playerZ;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Normalize direction
            if (distance > 0.01f)
            {
                dx /= distance;
                dy /= distance;
                dz /= distance;
            }

            if (_useHrtf && _hrtfProvider != null)
            {
                // Full 3D HRTF: transform world direction into camera's local space.
                // This matches how the game's own AudioListener on the camera works.
                // Camera right = cross(camUp, camForward) in Unity's left-handed system
                float crX = cuY * cfZ - cuZ * cfY;
                float crY = cuZ * cfX - cuX * cfZ;
                float crZ = cuX * cfY - cuY * cfX;

                // Project direction onto camera axes
                float localRight   = dx * crX + dy * crY + dz * crZ;
                float localUp      = dx * cuX + dy * cuY + dz * cuZ;
                float localForward = dx * cfX + dy * cfY + dz * cfZ;

                // Steam Audio: right-handed, -Z = forward
                _hrtfProvider.SetDirection(localRight, localUp, -localForward);
            }
            else if (_panner != null)
            {
                // Panning fallback: project onto camera right axis for L/R
                float crX = cuY * cfZ - cuZ * cfY;
                float crY = cuZ * cfX - cuX * cfZ;
                float crZ = cuX * cfY - cuY * cfX;

                float pan = dx * crX + dy * crY + dz * crZ;
                _panner.Pan = Math.Max(-1f, Math.Min(1f, pan));
            }

            // Volume based on distance - squared falloff for realistic attenuation
            // At 25% range: ~56% vol, at 50%: ~25% vol, at 75%: ~6% vol
            float normalizedDist = Math.Min(1f, distance / _maxDistance);
            if (distance < 3f)
            {
                _volumeProvider.Volume = _maxVolume;
            }
            else
            {
                float falloff = (1f - normalizedDist) * (1f - normalizedDist); // squared
                float volume = _minVolume + falloff * (_maxVolume - _minVolume);
                _volumeProvider.Volume = volume;
            }

            // Pitch modulation for generated tones (distance cue)
            if (_signalGenerator != null && !_useWavFile)
            {
                float basePitch = _currentSoundType switch
                {
                    SoundType.NPC => 600f,
                    SoundType.Enemy => 880f,
                    SoundType.Transition => 523f,
                    _ => 440f
                };

                float pitchMod = 1f + (1f - normalizedDist) * 0.2f;
                _signalGenerator.Frequency = basePitch * pitchMod;
            }
        }

        /// <summary>
        /// Check if currently playing
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// Set the max volume for this source. Used to make non-nearest sources quieter.
        /// </summary>
        public void SetMaxVolume(float maxVol)
        {
            _maxVolume = maxVol;
        }

        /// <summary>
        /// Get current distance to target
        /// </summary>
        public float GetCurrentDistance()
        {
            lock (_positionLock)
            {
                float dx = _targetX - _playerX;
                float dy = _targetY - _playerY;
                float dz = _targetZ - _playerZ;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shouldUpdate = false;
            _isPlaying = false;

            try
            {
                _updateThread?.Join(500);
            }
            catch { }

            StopInternal();
        }
    }

    /// <summary>
    /// Wraps a WaveStream to loop continuously
    /// </summary>
    public class LoopingWaveProvider : IWaveProvider
    {
        private readonly WaveStream _sourceStream;

        public LoopingWaveProvider(WaveStream sourceStream)
        {
            _sourceStream = sourceStream;
        }

        public WaveFormat WaveFormat => _sourceStream.WaveFormat;

        public int Read(byte[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                int bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    // End of stream, loop back to beginning
                    _sourceStream.Position = 0;
                    bytesRead = _sourceStream.Read(buffer, offset + totalBytesRead, count - totalBytesRead);
                    if (bytesRead == 0)
                        break; // Empty stream
                }
                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }

    /// <summary>
    /// Loops over a shared float[] sample buffer without owning or copying it.
    /// Each instance has its own read position, enabling many simultaneous players
    /// of the same sound with zero disk I/O after the first load.
    /// </summary>
    public class CachedLoopingSampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private int _position;

        public WaveFormat WaveFormat { get; }

        public CachedLoopingSampleProvider(float[] samples, WaveFormat format, int startOffset = 0)
        {
            _samples = samples;
            WaveFormat = format;
            _position = samples.Length > 0 ? startOffset % samples.Length : 0;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_samples.Length == 0) return 0;

            for (int i = 0; i < count; i++)
            {
                buffer[offset + i] = _samples[_position];
                _position++;
                if (_position >= _samples.Length)
                    _position = 0;
            }
            return count;
        }
    }
}
