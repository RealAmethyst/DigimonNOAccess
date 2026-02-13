using System;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Cardinal compass direction relative to the player on the current map.
    /// </summary>
    public enum CompassQuadrant
    {
        North,
        East,
        South,
        West
    }

    /// <summary>
    /// Pooled positional audio emitter that plays one of 4 directional tones
    /// depending on the compass quadrant of its obstacle relative to the player.
    /// Uses HRTF for 3D spatialization with panning fallback.
    /// All emitters stay registered with AudioOutputMixer; inactive ones are silent.
    /// </summary>
    public class CompassWallEmitter : IDisposable
    {
        private HrtfSampleProvider _hrtfProvider;
        private PanningSampleProvider _panner;
        private VolumeSampleProvider _volumeProvider;
        private QuadrantMuxSampleProvider _muxProvider;
        private bool _useHrtf;
        private bool _addedToMixer;
        private bool _disposed;

        /// <summary>Current obstacle position in world space.</summary>
        public float ObstacleX, ObstacleY, ObstacleZ;

        /// <summary>Whether this emitter is actively representing an obstacle.</summary>
        public bool Active { get; private set; }

        /// <summary>Current compass quadrant this emitter is sounding.</summary>
        public CompassQuadrant CurrentQuadrant { get; private set; }

        private const float MinVolume = 0.005f;
        private const float MaxVolume = 0.14f; // +5dB from 0.08

        // Hysteresis: once assigned a quadrant, the angle must exceed the boundary
        // by this many degrees before switching to prevent tone flickering.
        private const float HysteresisDeg = 10f;
        private bool _hasQuadrant;

        public CompassWallEmitter(string northWavPath, string eastWavPath,
                                   string southWavPath, string westWavPath)
        {
            try
            {
                // Load or create the 4 mono sources
                ISampleProvider northSource = LoadMonoSource(northWavPath, 523f);
                ISampleProvider eastSource = LoadMonoSource(eastWavPath, 659f);
                ISampleProvider southSource = LoadMonoSource(southWavPath, 392f);
                ISampleProvider westSource = LoadMonoSource(westWavPath, 440f);

                // Quadrant mux: atomically switches which of the 4 sources is read
                _muxProvider = new QuadrantMuxSampleProvider(
                    northSource, eastSource, southSource, westSource);

                // Build spatialization chain: Mux → HRTF → Volume → Mixer
                // No DirectEffect for wall sounds - we do our own distance falloff
                // and DirectEffect's occlusion simulation causes volume jitter
                if (SteamAudioManager.IsAvailable)
                {
                    _hrtfProvider = new HrtfSampleProvider(_muxProvider);
                    _volumeProvider = new VolumeSampleProvider(_hrtfProvider) { Volume = 0f };
                    _useHrtf = true;
                }
                else
                {
                    _panner = new PanningSampleProvider(_muxProvider) { Pan = 0f };
                    _volumeProvider = new VolumeSampleProvider(_panner) { Volume = 0f };
                    _useHrtf = false;
                }

                if (_volumeProvider != null)
                {
                    AudioOutputMixer.AddInput(_volumeProvider);
                    _addedToMixer = true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[CompassWall] Init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Load a WAV file as a CachedLoopingSampleProvider with random start offset,
        /// or fall back to a generated sine tone.
        /// </summary>
        private ISampleProvider LoadMonoSource(string wavPath, float fallbackFrequency)
        {
            if (wavPath != null && File.Exists(wavPath))
            {
                try
                {
                    var (samples, format) = PositionalAudio.LoadOrGetCachedAudio(wavPath);
                    int startOffset = new Random().Next(samples.Length);
                    return new CachedLoopingSampleProvider(samples, format, startOffset);
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"[CompassWall] Failed to load {wavPath}: {ex.Message}, using tone");
                }
            }

            return new SignalGenerator(44100, 1)
            {
                Frequency = fallbackFrequency,
                Type = SignalGeneratorType.Sin,
                Gain = 0.2
            };
        }

        /// <summary>
        /// Activate this emitter at the given obstacle position with a compass quadrant.
        /// </summary>
        public void Activate(float x, float y, float z, CompassQuadrant quadrant)
        {
            ObstacleX = x;
            ObstacleY = y;
            ObstacleZ = z;
            CurrentQuadrant = quadrant;
            _hasQuadrant = true;
            _muxProvider?.SetActiveQuadrant(quadrant);
            Active = true;
        }

        /// <summary>
        /// Deactivate this emitter (silenced).
        /// </summary>
        public void Deactivate()
        {
            Active = false;
            _hasQuadrant = false;
            if (_volumeProvider != null)
                _volumeProvider.Volume = 0f;
        }

        /// <summary>
        /// Update the compass quadrant based on current player position and north angle.
        /// Uses hysteresis to prevent flickering at quadrant boundaries.
        /// </summary>
        public void UpdateQuadrant(float playerX, float playerZ, float northAngleDeg)
        {
            if (!Active) return;

            var newQuadrant = GetQuadrant(playerX, playerZ, ObstacleX, ObstacleZ,
                                          northAngleDeg, _hasQuadrant ? CurrentQuadrant : (CompassQuadrant?)null);
            if (newQuadrant != CurrentQuadrant)
            {
                CurrentQuadrant = newQuadrant;
                _muxProvider?.SetActiveQuadrant(newQuadrant);
            }
        }

        /// <summary>
        /// Update audio spatialization based on player position and camera orientation.
        /// </summary>
        public void UpdateAudio(float playerX, float playerY, float playerZ,
                                float camFwdX, float camFwdY, float camFwdZ,
                                float camUpX, float camUpY, float camUpZ,
                                float maxRange)
        {
            if (!Active || _volumeProvider == null) return;

            float dx = ObstacleX - playerX;
            float dy = ObstacleY - playerY;
            float dz = ObstacleZ - playerZ;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (distance < 1f)
            {
                if (_useHrtf && _hrtfProvider != null)
                    _hrtfProvider.SetDirection(0f, 0f, -1f);
                _volumeProvider.Volume = MaxVolume;
                return;
            }

            float ndx = dx / distance;
            float ndy = dy / distance;
            float ndz = dz / distance;

            if (_useHrtf && _hrtfProvider != null)
            {
                // Camera right = cross(camUp, camForward) in Unity left-handed
                float crX = camUpY * camFwdZ - camUpZ * camFwdY;
                float crY = camUpZ * camFwdX - camUpX * camFwdZ;
                float crZ = camUpX * camFwdY - camUpY * camFwdX;

                float localRight   = ndx * crX + ndy * crY + ndz * crZ;
                float localUp      = ndx * camUpX + ndy * camUpY + ndz * camUpZ;
                float localForward = ndx * camFwdX + ndy * camFwdY + ndz * camFwdZ;

                // Steam Audio: right-handed, -Z = forward
                _hrtfProvider.SetDirection(localRight, localUp, -localForward);
            }
            else if (_panner != null)
            {
                float crX = camUpY * camFwdZ - camUpZ * camFwdY;
                float crY = camUpZ * camFwdX - camUpX * camFwdZ;
                float crZ = camUpX * camFwdY - camUpY * camFwdX;

                float pan = ndx * crX + ndy * crY + ndz * crZ;
                _panner.Pan = Math.Max(-1f, Math.Min(1f, pan));
            }

            // Volume: squared falloff
            float normalizedDist = Math.Min(1f, distance / maxRange);
            float falloff = (1f - normalizedDist) * (1f - normalizedDist);
            float volume = MinVolume + falloff * (MaxVolume - MinVolume);
            _volumeProvider.Volume = volume;
        }

        /// <summary>
        /// Calculate the compass quadrant of an obstacle relative to the player,
        /// given the map's north angle. Supports hysteresis to prevent flickering.
        /// </summary>
        public static CompassQuadrant GetQuadrant(float playerX, float playerZ,
                                                   float obstacleX, float obstacleZ,
                                                   float northAngleDeg,
                                                   CompassQuadrant? currentQuadrant = null)
        {
            float dx = obstacleX - playerX;
            float dz = obstacleZ - playerZ;

            // World angle from +Z axis (atan2 gives angle from +X, so use atan2(dx, dz) for +Z)
            float worldAngleRad = (float)Math.Atan2(dx, dz);
            float worldAngleDeg = worldAngleRad * (180f / (float)Math.PI);

            // Subtract north angle to get compass bearing (0 = north on this map)
            float compassDeg = worldAngleDeg - northAngleDeg;

            // Normalize to [0, 360)
            compassDeg = ((compassDeg % 360f) + 360f) % 360f;

            // With hysteresis: if we have a current quadrant, require extra degrees to leave it
            if (currentQuadrant.HasValue)
            {
                float half = HysteresisDeg / 2f;
                // Check if still within hysteresis zone of current quadrant
                switch (currentQuadrant.Value)
                {
                    case CompassQuadrant.North:
                        // N covers 315-45, with hysteresis: 305-55
                        if (IsInRange(compassDeg, 315f - half, 45f + half))
                            return CompassQuadrant.North;
                        break;
                    case CompassQuadrant.East:
                        if (compassDeg >= 45f - half && compassDeg < 135f + half)
                            return CompassQuadrant.East;
                        break;
                    case CompassQuadrant.South:
                        if (compassDeg >= 135f - half && compassDeg < 225f + half)
                            return CompassQuadrant.South;
                        break;
                    case CompassQuadrant.West:
                        if (compassDeg >= 225f - half && compassDeg < 315f + half)
                            return CompassQuadrant.West;
                        break;
                }
            }

            // No current quadrant or outside hysteresis zone: assign by standard boundaries
            // N: 315-45, E: 45-135, S: 135-225, W: 225-315
            if (compassDeg >= 315f || compassDeg < 45f)
                return CompassQuadrant.North;
            if (compassDeg < 135f)
                return CompassQuadrant.East;
            if (compassDeg < 225f)
                return CompassQuadrant.South;
            return CompassQuadrant.West;
        }

        /// <summary>
        /// Check if angle is in a range that wraps around 360 (e.g. 315-45).
        /// </summary>
        private static bool IsInRange(float angle, float from, float to)
        {
            // Normalize
            from = ((from % 360f) + 360f) % 360f;
            to = ((to % 360f) + 360f) % 360f;

            if (from <= to)
                return angle >= from && angle < to;
            else
                return angle >= from || angle < to; // Wraps around 360
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Active = false;

            if (_addedToMixer && _volumeProvider != null)
            {
                AudioOutputMixer.RemoveInput(_volumeProvider);
                _addedToMixer = false;
            }

            try { _hrtfProvider?.Dispose(); } catch { }

            _hrtfProvider = null;
            _panner = null;
            _volumeProvider = null;
            _muxProvider = null;
        }

        /// <summary>
        /// Sample provider that delegates Read() to one of 4 quadrant sources.
        /// Switching between sources is atomic via Interlocked.Exchange.
        /// </summary>
        private class QuadrantMuxSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider[] _sources;
            private int _activeIndex;

            public WaveFormat WaveFormat { get; }

            public QuadrantMuxSampleProvider(ISampleProvider north, ISampleProvider east,
                                              ISampleProvider south, ISampleProvider west)
            {
                _sources = new[] { north, east, south, west };
                _activeIndex = 0;
                WaveFormat = north.WaveFormat;
            }

            public void SetActiveQuadrant(CompassQuadrant quadrant)
            {
                Interlocked.Exchange(ref _activeIndex, (int)quadrant);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int index = Interlocked.CompareExchange(ref _activeIndex, 0, 0); // atomic read
                var active = _sources[index];

                // Read from active source
                int read = active.Read(buffer, offset, count);

                // Drain other sources to keep them in sync (prevents audio jump on switch)
                float[] discard = new float[count];
                for (int i = 0; i < _sources.Length; i++)
                {
                    if (i != index)
                        _sources[i].Read(discard, 0, count);
                }

                return read;
            }
        }
    }
}
