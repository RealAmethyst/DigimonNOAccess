using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides true positional audio using NAudio, bypassing the game's audio system.
    /// Simulates 3D audio with stereo panning and distance-based volume.
    /// Supports loading custom WAV files for different object types.
    /// </summary>
    public class PositionalAudio : IDisposable
    {
        // Audio output
        private WaveOutEvent _waveOut;
        private PanningSampleProvider _panner;
        private VolumeSampleProvider _volumeProvider;
        private SignalGenerator _signalGenerator;
        private LoopingWaveProvider _loopingWave;
        private AudioFileReader _audioFile;

        // Position tracking
        private float _targetX, _targetY, _targetZ;
        private float _playerX, _playerY, _playerZ;
        private float _playerForwardX, _playerForwardZ;
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
            Transition  // Area transitions (transission.wav)
        }

        private SoundType _currentSoundType = SoundType.Item;

        public PositionalAudio()
        {
            try
            {
                // Find sounds folder relative to mod DLL location
                string modPath = Path.GetDirectoryName(typeof(PositionalAudio).Assembly.Location);
                _soundsPath = Path.Combine(Path.GetDirectoryName(modPath), "sounds");

                // Fallback to project directory structure
                if (!Directory.Exists(_soundsPath))
                {
                    _soundsPath = Path.Combine(modPath, "sounds");
                }

                DebugLogger.Log($"[PositionalAudio] Sounds path: {_soundsPath}");
                DebugLogger.Log($"[PositionalAudio] Sounds folder exists: {Directory.Exists(_soundsPath)}");

                InitializeAudio(SoundType.Item);
                DebugLogger.Log("[PositionalAudio] Initialized successfully");
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
                _ => "item.wav"
            };
            return Path.Combine(_soundsPath, filename);
        }

        private void InitializeAudio(SoundType soundType)
        {
            // Clean up existing
            StopInternal();

            _currentSoundType = soundType;
            _useWavFile = false;

            // Try to load WAV file first
            string wavPath = GetSoundFilePath(soundType);
            if (File.Exists(wavPath))
            {
                try
                {
                    InitializeFromWavFile(wavPath);
                    _useWavFile = true;
                    DebugLogger.Log($"[PositionalAudio] Loaded WAV file: {wavPath}");
                    return;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[PositionalAudio] Failed to load WAV: {ex.Message}, falling back to generated tone");
                }
            }
            else
            {
                DebugLogger.Log($"[PositionalAudio] WAV file not found: {wavPath}, using generated tone");
            }

            // Fallback to generated tone
            InitializeFromGenerator(soundType);
        }

        private void InitializeFromWavFile(string wavPath)
        {
            _audioFile = new AudioFileReader(wavPath);

            // Create looping provider
            _loopingWave = new LoopingWaveProvider(_audioFile);

            // Convert to mono if stereo, for proper panning
            ISampleProvider sampleProvider;
            if (_loopingWave.WaveFormat.Channels == 2)
            {
                // Convert stereo to mono for panning to work correctly
                sampleProvider = new StereoToMonoSampleProvider(_loopingWave.ToSampleProvider());
            }
            else
            {
                sampleProvider = _loopingWave.ToSampleProvider();
            }

            // PanningSampleProvider takes MONO input and creates stereo output with panning
            _panner = new PanningSampleProvider(sampleProvider)
            {
                Pan = 0f
            };

            // Add volume control
            _volumeProvider = new VolumeSampleProvider(_panner)
            {
                Volume = 0.5f
            };

            // Create output device
            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100
            };
            _waveOut.Init(_volumeProvider);
        }

        private void InitializeFromGenerator(SoundType soundType)
        {
            // Create signal generator based on sound type
            float frequency;
            SignalGeneratorType signalType;

            switch (soundType)
            {
                case SoundType.NPC:
                    frequency = 600f;  // Medium pitch for NPCs
                    signalType = SignalGeneratorType.Sin;
                    break;
                case SoundType.Enemy:
                    frequency = 880f;  // Higher pitch for enemies
                    signalType = SignalGeneratorType.Square;
                    break;
                case SoundType.Transition:
                    frequency = 523f;  // C5 for transitions
                    signalType = SignalGeneratorType.Triangle;
                    break;
                case SoundType.Item:
                default:
                    frequency = 440f;  // A4 note for items
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

            // PanningSampleProvider takes MONO input and creates stereo output with panning
            _panner = new PanningSampleProvider(_signalGenerator)
            {
                Pan = 0f  // -1 = left, 0 = center, 1 = right
            };

            // Add volume control to the stereo output from panner
            _volumeProvider = new VolumeSampleProvider(_panner)
            {
                Volume = 0.5f
            };

            // Create output device
            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100
            };
            _waveOut.Init(_volumeProvider);

            DebugLogger.Log($"[PositionalAudio] Audio initialized with generator: {soundType}, freq={frequency}Hz");
        }

        /// <summary>
        /// Start playing positional audio for a target
        /// </summary>
        public void StartTracking(SoundType soundType, float maxDistance = 50f)
        {
            try
            {
                if (_currentSoundType != soundType || _waveOut == null)
                {
                    InitializeAudio(soundType);
                }

                _maxDistance = maxDistance;
                _isPlaying = true;
                _shouldUpdate = true;

                _waveOut?.Play();

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

                DebugLogger.Log($"[PositionalAudio] Started tracking with {soundType}, maxDist={maxDistance}");
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

            // Reinitialize with new sound type
            InitializeAudio(soundType);

            // Resume if we were playing
            if (wasPlaying)
            {
                _isPlaying = true;
                _shouldUpdate = true;
                _waveOut?.Play();

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
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;
            }
            catch { }

            try
            {
                _audioFile?.Dispose();
                _audioFile = null;
                _loopingWave = null;
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
        /// Update the player position and forward direction (call this from game thread)
        /// </summary>
        public void UpdatePlayerPosition(float x, float y, float z, float forwardX, float forwardZ)
        {
            lock (_positionLock)
            {
                _playerX = x;
                _playerY = y;
                _playerZ = z;
                _playerForwardX = forwardX;
                _playerForwardZ = forwardZ;
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
            if (_panner == null || _volumeProvider == null)
                return;

            float targetX, targetY, targetZ;
            float playerX, playerY, playerZ;
            float forwardX, forwardZ;

            lock (_positionLock)
            {
                targetX = _targetX;
                targetY = _targetY;
                targetZ = _targetZ;
                playerX = _playerX;
                playerY = _playerY;
                playerZ = _playerZ;
                forwardX = _playerForwardX;
                forwardZ = _playerForwardZ;
            }

            // Calculate direction to target (horizontal plane)
            float dx = targetX - playerX;
            float dz = targetZ - playerZ;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz);

            // Normalize direction to target
            if (distance > 0.01f)
            {
                dx /= distance;
                dz /= distance;
            }

            // Normalize player forward
            float forwardMag = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (forwardMag > 0.01f)
            {
                forwardX /= forwardMag;
                forwardZ /= forwardMag;
            }

            // Calculate pan using cross product (determines left/right)
            // Cross product Y component, negated to match Unity's coordinate system
            float cross = forwardZ * dx - forwardX * dz;

            // Pan: negative = left, positive = right
            // Clamp to -1 to 1 range
            float pan = Math.Max(-1f, Math.Min(1f, cross));

            // Calculate if target is in front or behind using dot product
            float dot = forwardX * dx + forwardZ * dz;

            // If target is behind, make the pan more extreme and reduce volume slightly
            if (dot < 0)
            {
                // Behind: push pan toward extremes
                pan = pan > 0 ? Math.Min(1f, pan + 0.3f) : Math.Max(-1f, pan - 0.3f);
            }

            // Calculate volume based on distance
            float normalizedDist = Math.Min(1f, distance / _maxDistance);
            float volume = _maxVolume - (normalizedDist * (_maxVolume - _minVolume));

            // If very close, boost volume
            if (distance < 3f)
            {
                volume = _maxVolume;
            }

            // Apply to audio
            _panner.Pan = pan;
            _volumeProvider.Volume = volume;

            // Adjust frequency based on distance for additional cue (closer = slightly higher pitch)
            // Only works with generated tones, not WAV files
            if (_signalGenerator != null && !_useWavFile)
            {
                float basePitch = _currentSoundType switch
                {
                    SoundType.NPC => 600f,
                    SoundType.Enemy => 880f,
                    SoundType.Transition => 523f,
                    _ => 440f
                };

                // Pitch rises as you get closer (up to 20% higher when very close)
                float pitchMod = 1f + (1f - normalizedDist) * 0.2f;
                _signalGenerator.Frequency = basePitch * pitchMod;
            }
        }

        /// <summary>
        /// Check if currently playing
        /// </summary>
        public bool IsPlaying => _isPlaying;

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
}
