using System;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Provides true positional audio using NAudio, bypassing the game's audio system.
    /// Simulates 3D audio with stereo panning and distance-based volume.
    /// </summary>
    public class PositionalAudio : IDisposable
    {
        // Audio output
        private WaveOutEvent _waveOut;
        private PanningSampleProvider _panner;
        private VolumeSampleProvider _volumeProvider;
        private SignalGenerator _signalGenerator;

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

        // Update thread
        private Thread _updateThread;
        private bool _shouldUpdate = false;

        // Different sound types for different objects
        public enum SoundType
        {
            Beep,       // Simple beep for items
            Pulse,      // Pulsing for NPCs
            Warning     // Higher pitch for enemies
        }

        private SoundType _currentSoundType = SoundType.Beep;

        public PositionalAudio()
        {
            try
            {
                InitializeAudio(SoundType.Beep);
                DebugLogger.Log("[PositionalAudio] Initialized successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[PositionalAudio] Init error: {ex.Message}");
            }
        }

        private void InitializeAudio(SoundType soundType)
        {
            // Clean up existing
            StopInternal();

            _currentSoundType = soundType;

            // Create signal generator based on sound type
            float frequency;
            SignalGeneratorType signalType;

            switch (soundType)
            {
                case SoundType.Pulse:
                    frequency = 600f;  // Medium pitch for NPCs
                    signalType = SignalGeneratorType.Sin;
                    break;
                case SoundType.Warning:
                    frequency = 880f;  // Higher pitch for enemies
                    signalType = SignalGeneratorType.Square;
                    break;
                case SoundType.Beep:
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
            // This is the correct order - no MonoToStereo needed before panning
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

            DebugLogger.Log($"[PositionalAudio] Audio initialized: {soundType}, freq={frequency}Hz");
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

        private void StopInternal()
        {
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;
            }
            catch { }
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
            if (_signalGenerator != null)
            {
                float basePitch = _currentSoundType switch
                {
                    SoundType.Pulse => 600f,
                    SoundType.Warning => 880f,
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
}
