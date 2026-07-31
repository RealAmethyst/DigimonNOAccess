using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Audio beacon that guides the player to a destination along a NavMesh path.
    ///
    /// The beep is placed at a guide point a short way ahead of the player along the
    /// path, so it leads rather than pointing straight at a destination behind a wall.
    /// It repeats faster the closer the destination gets.
    ///
    /// Spatialization goes through the same pipeline as every other navigation sound:
    /// Steam Audio HRTF when phonon.dll is present, stereo panning otherwise, mixed
    /// into the shared <see cref="AudioOutputMixer"/> rather than owning its own
    /// output device. Direction is measured against the CAMERA, not the player, so
    /// turning the camera moves the beep exactly like it moves the item and NPC
    /// sounds - see <see cref="CameraOrientation"/>.
    /// </summary>
    public class PathfindingBeacon : IDisposable
    {
        // NAudio chain (no WaveOutEvent - registers with the shared AudioOutputMixer)
        private IntervalBeepSampleProvider _beeper;
        private HrtfSampleProvider _hrtfProvider;
        private PanningSampleProvider _panner;
        private VolumeSampleProvider _volumeProvider;
        private bool _useHrtf;
        private bool _addedToMixer;

        // Position tracking (full 3D camera orientation for HRTF)
        private float _playerX, _playerY, _playerZ;
        private float _camFwdX, _camFwdY, _camFwdZ;
        private float _camUpX, _camUpY, _camUpZ;
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

        // Fallback tone, used only when pathfinding_tracker.wav is missing
        private const float FallbackToneSeconds = 0.08f;
        private const float FallbackToneHz = 800f;

        // State
        private bool _isActive = false;
        private volatile bool _disposed = false;

        // Update thread
        private Thread _updateThread;
        private volatile bool _shouldUpdate = false;

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
                if (_volumeProvider == null)
                {
                    DebugLogger.Error("[PathBeacon] Audio chain unavailable, beacon not started");
                    return;
                }

                _isActive = true;
                _shouldUpdate = true;

                AudioOutputMixer.AddInput(_volumeProvider);
                _addedToMixer = true;

                _updateThread = new Thread(UpdateLoop)
                {
                    IsBackground = true,
                    Name = "PathfindingBeacon_Update"
                };
                _updateThread.Start();

                DebugLogger.Log($"[PathBeacon] Started (hrtf={_useHrtf})");
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

            try
            {
                _updateThread?.Join(500);
            }
            catch { }
            _updateThread = null;

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
        /// Update the player's position and the camera orientation to spatialize against.
        /// Called each frame from the game thread.
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

        private void InitializeAudio()
        {
            StopInternal();

            float[] samples = LoadBeepSamples(out int sampleRate);
            if (samples == null || samples.Length == 0)
                return;

            if (SteamAudioManager.IsAvailable && sampleRate != SteamAudioManager.SampleRate)
            {
                DebugLogger.Warning(
                    $"[PathBeacon] Beep sample rate {sampleRate}Hz does not match the HRTF pipeline "
                    + $"({SteamAudioManager.SampleRate}Hz) - the beep will play at the wrong pitch");
            }

            _beeper = new IntervalBeepSampleProvider(samples, sampleRate)
            {
                Interval = MaxInterval
            };

            ISampleProvider stereoSource;
            if (SteamAudioManager.IsAvailable)
            {
                _hrtfProvider = new HrtfSampleProvider(_beeper);
                stereoSource = _hrtfProvider;
                _panner = null;
                _useHrtf = true;
            }
            else
            {
                _panner = new PanningSampleProvider(_beeper) { Pan = 0f };
                stereoSource = _panner;
                _hrtfProvider = null;
                _useHrtf = false;
            }

            _volumeProvider = new VolumeSampleProvider(stereoSource)
            {
                Volume = ModSettings.PathfinderVolume
            };
        }

        /// <summary>
        /// Load the tracker beep, reusing the shared audio cache so repeated
        /// pathfinding sessions never touch disk twice. Falls back to a generated
        /// click if the WAV is missing so navigation still works.
        /// </summary>
        private float[] LoadBeepSamples(out int sampleRate)
        {
            string wavPath = ResolveSoundPath("pathfinding_tracker.wav");

            if (wavPath != null && File.Exists(wavPath))
            {
                try
                {
                    var (samples, format) = PositionalAudio.LoadOrGetCachedAudio(wavPath);
                    if (samples.Length > 0)
                    {
                        sampleRate = format.SampleRate;
                        return samples;
                    }
                    DebugLogger.Warning($"[PathBeacon] {wavPath} decoded to zero samples");
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[PathBeacon] Failed to load {wavPath}: {ex.Message}");
                }
            }
            else
            {
                DebugLogger.Warning($"[PathBeacon] pathfinding_tracker.wav not found at {wavPath ?? "(unresolved sounds folder)"}, using generated click");
            }

            sampleRate = SteamAudioManager.SampleRate;
            return GenerateFallbackClick(sampleRate);
        }

        private static string ResolveSoundPath(string fileName)
        {
            try
            {
                string modPath = Path.GetDirectoryName(typeof(PathfindingBeacon).Assembly.Location);
                if (string.IsNullOrEmpty(modPath))
                    return null;

                string soundsPath = Path.Combine(Path.GetDirectoryName(modPath), "sounds");
                if (!Directory.Exists(soundsPath))
                    soundsPath = Path.Combine(modPath, "sounds");

                return Path.Combine(soundsPath, fileName);
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"[PathBeacon] Could not resolve sounds folder: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// A short sine click with fades, baked into a buffer so it feeds the same
        /// interval provider as the real WAV.
        /// </summary>
        private static float[] GenerateFallbackClick(int sampleRate)
        {
            int length = (int)(sampleRate * FallbackToneSeconds);
            var samples = new float[length];
            int fade = Math.Max(1, sampleRate / 1000); // 1ms fade in and out

            for (int i = 0; i < length; i++)
            {
                float envelope = 1f;
                if (i < fade) envelope = i / (float)fade;
                else if (i >= length - fade) envelope = (length - 1 - i) / (float)fade;

                samples[i] = 0.3f * envelope
                    * (float)Math.Sin(2.0 * Math.PI * FallbackToneHz * i / sampleRate);
            }

            return samples;
        }

        private void StopInternal()
        {
            if (_addedToMixer && _volumeProvider != null)
            {
                AudioOutputMixer.RemoveInput(_volumeProvider);
                _addedToMixer = false;
            }

            try
            {
                _hrtfProvider?.Dispose();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[PathBeacon] HRTF dispose error: {ex.Message}");
            }

            _hrtfProvider = null;
            _panner = null;
            _beeper = null;
            _volumeProvider = null;
            _useHrtf = false;
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
            // Snapshot the chain once: Stop() can null these out from the game thread
            // while this update thread is mid-pass.
            var beeper = _beeper;
            var volume = _volumeProvider;
            var hrtf = _hrtfProvider;
            var panner = _panner;
            if (beeper == null || volume == null)
                return;

            float playerX, playerY, playerZ;
            float cfX, cfY, cfZ, cuX, cuY, cuZ;
            float destX, destY, destZ;
            float[] cornersX, cornersY, cornersZ;

            lock (_positionLock)
            {
                playerX = _playerX;
                playerY = _playerY;
                playerZ = _playerZ;
                cfX = _camFwdX; cfY = _camFwdY; cfZ = _camFwdZ;
                cuX = _camUpX;  cuY = _camUpY;  cuZ = _camUpZ;
                destX = _destX;
                destY = _destY;
                destZ = _destZ;
                cornersX = _pathCornersX;
                cornersY = _pathCornersY;
                cornersZ = _pathCornersZ;
            }

            // Distance to the destination drives cadence and volume
            float ddx = destX - playerX;
            float ddy = destY - playerY;
            float ddz = destZ - playerZ;
            float distToDest = (float)Math.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);

            // The beep sits at a guide point ahead of the player along the path
            CalculateGuidePoint(playerX, playerY, playerZ, cornersX, cornersY, cornersZ,
                destX, destY, destZ, out float guideX, out float guideY, out float guideZ);

            float dx = guideX - playerX;
            float dy = guideY - playerY;
            float dz = guideZ - playerZ;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (dist > 0.01f)
            {
                dx /= dist;
                dy /= dist;
                dz /= dist;
            }
            else
            {
                // Standing on the guide point: keep it in front rather than snapping
                // to an arbitrary direction as the normalization degenerates.
                dx = cfX; dy = cfY; dz = cfZ;
            }

            CameraOrientation.ToCameraLocal(dx, dy, dz, cfX, cfY, cfZ, cuX, cuY, cuZ,
                out float localRight, out float localUp, out float localForward);

            if (_useHrtf && hrtf != null)
            {
                // Steam Audio: right-handed, -Z = forward
                hrtf.SetDirection(localRight, localUp, -localForward);
            }
            else if (panner != null)
            {
                panner.Pan = Math.Max(-1f, Math.Min(1f, localRight));
            }

            // Distance is carried entirely by cadence: the beep repeats faster the closer
            // the destination gets. Volume stays flat at whatever the player set, so the
            // loudness cue never fights the distance cue.
            float closeness = 1f - Math.Min(1f, distToDest / MaxBeaconDistance);

            beeper.Interval = MinInterval + (1f - closeness) * (MaxInterval - MinInterval);
            volume.Volume = ModSettings.PathfinderVolume;
        }

        /// <summary>
        /// Calculate a guide point ~GuideDistance units ahead of the player along the path.
        /// This point is what the beep is positioned at, so the sound "leads" the player.
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

            // Find the path segment nearest to the player, and how far along it they are
            int nearestSegment = 0;
            float nearestT = 0f;
            float bestDist = float.MaxValue;
            for (int i = 0; i < cornersX.Length - 1; i++)
            {
                float dist = PointToSegmentDistance(playerX, playerZ,
                    cornersX[i], cornersZ[i], cornersX[i + 1], cornersZ[i + 1], out float t);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearestSegment = i;
                    nearestT = t;
                }
            }

            // Walk forward from where the player actually is on that segment, not from
            // its start corner. Paths are recalculated twice a second from the player's
            // position, so starting at the corner would drag the guide point backwards
            // in between - on a long straight leg it can end up behind the player.
            float remaining = GuideDistance;
            {
                int i = nearestSegment;
                float segDx = cornersX[i + 1] - cornersX[i];
                float segDy = cornersY[i + 1] - cornersY[i];
                float segDz = cornersZ[i + 1] - cornersZ[i];
                float segLen = (float)Math.Sqrt(segDx * segDx + segDy * segDy + segDz * segDz);
                float travelled = segLen * nearestT;

                if (segLen - travelled >= remaining)
                {
                    float t = (travelled + remaining) / segLen;
                    guideX = cornersX[i] + segDx * t;
                    guideY = cornersY[i] + segDy * t;
                    guideZ = cornersZ[i] + segDz * t;
                    return;
                }

                remaining -= (segLen - travelled);
            }

            for (int i = nearestSegment + 1; i < cornersX.Length - 1; i++)
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
        /// <paramref name="t"/> is how far along the segment the closest point sits,
        /// clamped to 0..1.
        /// </summary>
        private float PointToSegmentDistance(float px, float pz, float ax, float az, float bx, float bz, out float t)
        {
            float abx = bx - ax;
            float abz = bz - az;
            float apx = px - ax;
            float apz = pz - az;

            float abLenSq = abx * abx + abz * abz;
            if (abLenSq < 0.0001f)
            {
                t = 0f;
                return (float)Math.Sqrt(apx * apx + apz * apz);
            }

            t = Math.Max(0f, Math.Min(1f, (apx * abx + apz * abz) / abLenSq));
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
            Stop();
        }
    }

    /// <summary>
    /// Repeats a short mono clip at a settable interval: the whole clip plays, then
    /// silence fills the rest of the cycle. Plays the clip in full every time rather
    /// than cutting it at a fixed beep length, so the tracker sound keeps its shape
    /// no matter how fast the cadence gets.
    ///
    /// The sample buffer is shared and never written to, so several beacons could
    /// read the same cached clip.
    /// </summary>
    public class IntervalBeepSampleProvider : ISampleProvider
    {
        private readonly float[] _samples;
        private readonly int _sampleRate;

        private int _positionInCycle;
        private int _cycleSamples;

        // Written from the position update thread, read on the audio thread.
        private volatile float _interval = 1.0f;

        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// Seconds between the start of each beep. Never shorter than the clip itself.
        /// A change takes effect at the next cycle boundary so a beep is never cut off
        /// half way through.
        /// </summary>
        public float Interval
        {
            get => _interval;
            set => _interval = value;
        }

        public IntervalBeepSampleProvider(float[] samples, int sampleRate)
        {
            _samples = samples ?? Array.Empty<float>();
            _sampleRate = sampleRate;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            _cycleSamples = ComputeCycleSamples();
        }

        private int ComputeCycleSamples()
        {
            int wanted = (int)(_sampleRate * _interval);
            // A cycle is at least one full clip, so the clip is never truncated.
            return Math.Max(wanted, Math.Max(1, _samples.Length));
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (_samples.Length == 0)
            {
                Array.Clear(buffer, offset, count);
                return count;
            }

            int written = 0;
            while (written < count)
            {
                if (_positionInCycle < _samples.Length)
                {
                    int toCopy = Math.Min(count - written, _samples.Length - _positionInCycle);
                    // NAudio's WaveBuffer overlays byte[]/float[], which makes
                    // Array.Copy throw ArrayTypeMismatchException. BlockCopy is safe.
                    Buffer.BlockCopy(_samples, _positionInCycle * sizeof(float),
                        buffer, (offset + written) * sizeof(float), toCopy * sizeof(float));
                    written += toCopy;
                    _positionInCycle += toCopy;
                }
                else
                {
                    int toWrite = Math.Min(count - written, _cycleSamples - _positionInCycle);
                    if (toWrite <= 0)
                    {
                        // Interval shrank below the current position: start the next cycle now.
                        _positionInCycle = 0;
                        _cycleSamples = ComputeCycleSamples();
                        continue;
                    }
                    Array.Clear(buffer, offset + written, toWrite);
                    written += toWrite;
                    _positionInCycle += toWrite;
                }

                if (_positionInCycle >= _cycleSamples)
                {
                    _positionInCycle = 0;
                    // Pick up any interval change only at the boundary.
                    _cycleSamples = ComputeCycleSamples();
                }
            }

            return written;
        }
    }
}
