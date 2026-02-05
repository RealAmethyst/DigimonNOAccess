using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Audio beacon that guides the player to a destination using spatial audio.
    /// Beeps faster as the player approaches, pans to indicate direction,
    /// and calculates a guide point along the NavMesh path ahead of the player.
    /// </summary>
    public class PathfindingBeacon : IDisposable
    {
        // NAudio chain
        private WaveOutEvent _waveOut;
        private PanningSampleProvider _panner;
        private VolumeSampleProvider _volumeProvider;
        private BeepingSampleProvider _beeper;

        // Position tracking
        private float _playerX, _playerY, _playerZ;
        private float _playerForwardX, _playerForwardZ;
        private float _destX, _destY, _destZ;
        private float[] _pathCornersX;
        private float[] _pathCornersY;
        private float[] _pathCornersZ;
        private readonly object _positionLock = new object();

        // Configuration
        private const float GuideDistance = 10f;
        private const float MaxBeaconDistance = 200f;
        private const float MinInterval = 0.15f;
        private const float MaxInterval = 1.5f;
        private const float MinVolume = 0.2f;
        private const float MaxVolume = 0.6f;

        // State
        private bool _isActive = false;
        private bool _disposed = false;

        // Update thread
        private Thread _updateThread;
        private bool _shouldUpdate = false;

        public bool IsActive => _isActive;

        /// <summary>
        /// Start the beacon guiding toward a destination along a NavMesh path.
        /// </summary>
        public void Start(float destX, float destY, float destZ, float[] cornersX, float[] cornersY, float[] cornersZ)
        {
            try
            {
                Stop();

                lock (_positionLock)
                {
                    _destX = destX;
                    _destY = destY;
                    _destZ = destZ;
                    _pathCornersX = cornersX;
                    _pathCornersY = cornersY;
                    _pathCornersZ = cornersZ;
                }

                InitializeAudio();

                _isActive = true;
                _shouldUpdate = true;

                _waveOut?.Play();

                _updateThread = new Thread(UpdateLoop)
                {
                    IsBackground = true,
                    Name = "PathfindingBeacon_Update"
                };
                _updateThread.Start();

                DebugLogger.Log("[PathBeacon] Started");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[PathBeacon] Start error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop the beacon.
        /// </summary>
        public void Stop()
        {
            _shouldUpdate = false;
            _isActive = false;
            StopInternal();
        }

        /// <summary>
        /// Update the path corners when the path is recalculated.
        /// </summary>
        public void UpdatePath(float[] cornersX, float[] cornersY, float[] cornersZ)
        {
            lock (_positionLock)
            {
                _pathCornersX = cornersX;
                _pathCornersY = cornersY;
                _pathCornersZ = cornersZ;
            }
        }

        /// <summary>
        /// Update the player's current position and facing direction.
        /// Called each frame from the game thread.
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
        /// Get the current distance from the player to the destination.
        /// </summary>
        public float GetDistanceToDestination()
        {
            lock (_positionLock)
            {
                float dx = _destX - _playerX;
                float dy = _destY - _playerY;
                float dz = _destZ - _playerZ;
                return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            }
        }

        private void InitializeAudio()
        {
            string modPath = Path.GetDirectoryName(typeof(PathfindingBeacon).Assembly.Location);
            string soundsPath = Path.Combine(Path.GetDirectoryName(modPath), "sounds");
            if (!Directory.Exists(soundsPath))
                soundsPath = Path.Combine(modPath, "sounds");

            string wavPath = Path.Combine(soundsPath, "pathfinding_tracker.wav");

            ISampleProvider source;

            if (File.Exists(wavPath))
            {
                var audioFile = new AudioFileReader(wavPath);
                var looping = new LoopingWaveProvider(audioFile);

                ISampleProvider sampleProvider;
                if (looping.WaveFormat.Channels == 2)
                    sampleProvider = new StereoToMonoSampleProvider(looping.ToSampleProvider());
                else
                    sampleProvider = looping.ToSampleProvider();

                source = sampleProvider;
                DebugLogger.Log($"[PathBeacon] Loaded WAV: {wavPath}");
            }
            else
            {
                // Fallback: generate a short click tone
                var generator = new SignalGenerator(44100, 1)
                {
                    Frequency = 800f,
                    Type = SignalGeneratorType.Sin,
                    Gain = 0.3
                };
                source = generator;
                DebugLogger.Log("[PathBeacon] WAV not found, using generated tone");
            }

            _beeper = new BeepingSampleProvider(source)
            {
                BeepInterval = MaxInterval
            };

            _panner = new PanningSampleProvider(_beeper)
            {
                Pan = 0f
            };

            _volumeProvider = new VolumeSampleProvider(_panner)
            {
                Volume = MinVolume
            };

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 100
            };
            _waveOut.Init(_volumeProvider);
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

            _beeper = null;
            _panner = null;
            _volumeProvider = null;

            try
            {
                _updateThread?.Join(500);
            }
            catch { }
            _updateThread = null;
        }

        private void UpdateLoop()
        {
            while (_shouldUpdate && !_disposed)
            {
                try
                {
                    UpdateAudioParameters();
                    Thread.Sleep(16); // ~60fps
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[PathBeacon] Update error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }

        private void UpdateAudioParameters()
        {
            if (_panner == null || _volumeProvider == null || _beeper == null)
                return;

            float playerX, playerY, playerZ;
            float forwardX, forwardZ;
            float destX, destY, destZ;
            float[] cornersX, cornersY, cornersZ;

            lock (_positionLock)
            {
                playerX = _playerX;
                playerY = _playerY;
                playerZ = _playerZ;
                forwardX = _playerForwardX;
                forwardZ = _playerForwardZ;
                destX = _destX;
                destY = _destY;
                destZ = _destZ;
                cornersX = _pathCornersX;
                cornersY = _pathCornersY;
                cornersZ = _pathCornersZ;
            }

            // Calculate distance to destination
            float ddx = destX - playerX;
            float ddy = destY - playerY;
            float ddz = destZ - playerZ;
            float distToDest = (float)Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);

            // Calculate guide point along the path
            float guideX, guideY, guideZ;
            CalculateGuidePoint(playerX, playerY, playerZ, cornersX, cornersY, cornersZ,
                destX, destY, destZ, out guideX, out guideY, out guideZ);

            // Direction to guide point (horizontal plane)
            float dx = guideX - playerX;
            float dz = guideZ - playerZ;
            float dist2D = (float)Math.Sqrt(dx * dx + dz * dz);

            if (dist2D > 0.01f)
            {
                dx /= dist2D;
                dz /= dist2D;
            }

            // Normalize player forward
            float forwardMag = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
            if (forwardMag > 0.01f)
            {
                forwardX /= forwardMag;
                forwardZ /= forwardMag;
            }

            // Pan using cross product (same algorithm as PositionalAudio.cs:430)
            float cross = forwardZ * dx - forwardX * dz;
            float pan = Math.Max(-1f, Math.Min(1f, cross));

            // If guide point is behind player, push pan to extremes
            float dot = forwardX * dx + forwardZ * dz;
            if (dot < 0)
            {
                pan = pan > 0 ? Math.Min(1f, pan + 0.3f) : Math.Max(-1f, pan - 0.3f);
            }

            // Closeness factor: 0 = far (MaxBeaconDistance), 1 = arrived
            float closeness = 1f - Math.Min(1f, distToDest / MaxBeaconDistance);

            // Beep interval: far = slow, close = rapid
            float interval = MinInterval + (1f - closeness) * (MaxInterval - MinInterval);

            // Volume: far = quiet, close = louder
            float volume = MinVolume + closeness * (MaxVolume - MinVolume);

            // Apply
            _panner.Pan = pan;
            _volumeProvider.Volume = volume;
            _beeper.BeepInterval = interval;
        }

        /// <summary>
        /// Calculate a guide point ~GuideDistance units ahead of the player along the path.
        /// This point is used for panning so the sound "leads" the player.
        /// </summary>
        private void CalculateGuidePoint(
            float playerX, float playerY, float playerZ,
            float[] cornersX, float[] cornersY, float[] cornersZ,
            float destX, float destY, float destZ,
            out float guideX, out float guideY, out float guideZ)
        {
            // Default to destination
            guideX = destX;
            guideY = destY;
            guideZ = destZ;

            if (cornersX == null || cornersX.Length < 2)
                return;

            // Find the path segment nearest to the player
            int nearestSegment = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < cornersX.Length - 1; i++)
            {
                float dist = PointToSegmentDistance(playerX, playerZ,
                    cornersX[i], cornersZ[i], cornersX[i + 1], cornersZ[i + 1]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearestSegment = i;
                }
            }

            // Walk along path from nearest segment, advancing GuideDistance
            float remaining = GuideDistance;
            for (int i = nearestSegment; i < cornersX.Length - 1; i++)
            {
                float segDx = cornersX[i + 1] - cornersX[i];
                float segDy = cornersY[i + 1] - cornersY[i];
                float segDz = cornersZ[i + 1] - cornersZ[i];
                float segLen = (float)Math.Sqrt(segDx * segDx + segDy * segDy + segDz * segDz);

                if (segLen < 0.001f)
                    continue;

                if (remaining <= segLen)
                {
                    float t = remaining / segLen;
                    guideX = cornersX[i] + segDx * t;
                    guideY = cornersY[i] + segDy * t;
                    guideZ = cornersZ[i] + segDz * t;
                    return;
                }

                remaining -= segLen;
            }

            // Remaining distance exceeds path length: use destination
        }

        /// <summary>
        /// Distance from point (px, pz) to line segment (ax, az)-(bx, bz) in 2D.
        /// </summary>
        private float PointToSegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float abx = bx - ax;
            float abz = bz - az;
            float apx = px - ax;
            float apz = pz - az;

            float abLenSq = abx * abx + abz * abz;
            if (abLenSq < 0.0001f)
            {
                return (float)Math.Sqrt(apx * apx + apz * apz);
            }

            float t = Math.Max(0f, Math.Min(1f, (apx * abx + apz * abz) / abLenSq));
            float closestX = ax + t * abx;
            float closestZ = az + t * abz;
            float dx = px - closestX;
            float dz = pz - closestZ;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _shouldUpdate = false;
            _isActive = false;
            StopInternal();
        }
    }

    /// <summary>
    /// Wraps a sample provider to create a beep-silence-beep pattern.
    /// Plays the source audio for one "beep" duration, then inserts silence.
    /// The BeepInterval property controls the gap between beeps and can be
    /// updated in real-time from another thread.
    /// </summary>
    public class BeepingSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _sampleRate;
        private const float BeepDuration = 0.08f; // Duration of each beep in seconds

        private int _beepSamples;
        private int _silenceSamples;
        private int _positionInCycle;
        private float _beepInterval = 1.0f;
        private bool _intervalChanged = true;

        public WaveFormat WaveFormat => _source.WaveFormat;

        /// <summary>
        /// Time in seconds between the start of each beep.
        /// Thread-safe: can be updated from the audio update thread.
        /// </summary>
        public float BeepInterval
        {
            get => _beepInterval;
            set
            {
                if (Math.Abs(_beepInterval - value) > 0.001f)
                {
                    _beepInterval = value;
                    _intervalChanged = true;
                }
            }
        }

        public BeepingSampleProvider(ISampleProvider source)
        {
            _source = source;
            _sampleRate = source.WaveFormat.SampleRate;
            _beepSamples = (int)(_sampleRate * BeepDuration);
            RecalcSilence();
        }

        private void RecalcSilence()
        {
            float silenceDuration = Math.Max(0f, _beepInterval - BeepDuration);
            _silenceSamples = (int)(_sampleRate * silenceDuration);
            _intervalChanged = false;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_intervalChanged)
                RecalcSilence();

            int totalCycle = _beepSamples + _silenceSamples;
            if (totalCycle <= 0)
                totalCycle = _beepSamples > 0 ? _beepSamples : 1;

            int written = 0;
            while (written < count)
            {
                int posInCycle = _positionInCycle % totalCycle;

                if (posInCycle < _beepSamples)
                {
                    // In the beep portion: read from source
                    int beepRemaining = _beepSamples - posInCycle;
                    int toRead = Math.Min(count - written, beepRemaining);
                    int read = _source.Read(buffer, offset + written, toRead);
                    if (read == 0)
                    {
                        // Source exhausted, fill silence
                        Array.Clear(buffer, offset + written, count - written);
                        written = count;
                        break;
                    }
                    written += read;
                    _positionInCycle += read;
                }
                else
                {
                    // In the silence portion: output zeros
                    int silenceRemaining = totalCycle - posInCycle;
                    int toWrite = Math.Min(count - written, silenceRemaining);
                    Array.Clear(buffer, offset + written, toWrite);
                    written += toWrite;
                    _positionInCycle += toWrite;
                }

                // Wrap cycle position
                if (_positionInCycle >= totalCycle)
                {
                    _positionInCycle -= totalCycle;
                    // Re-check interval in case it changed mid-cycle
                    if (_intervalChanged)
                        RecalcSilence();
                    totalCycle = _beepSamples + _silenceSamples;
                    if (totalCycle <= 0)
                        totalCycle = _beepSamples > 0 ? _beepSamples : 1;
                }
            }

            return written;
        }
    }
}
