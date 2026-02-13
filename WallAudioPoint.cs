using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Lightweight positional audio emitter for a single wall boundary point.
    /// Managed externally by AudioNavigationHandler's wall scanning system.
    /// Uses HRTF for 3D spatialization with panning fallback.
    /// All emitters stay registered with AudioOutputMixer; inactive ones are silent.
    /// </summary>
    public class WallAudioPoint : IDisposable
    {
        private DirectEffectSampleProvider _directEffect;
        private HrtfSampleProvider _hrtfProvider;
        private PanningSampleProvider _panner;
        private VolumeSampleProvider _volumeProvider;
        private LoopingWaveProvider _loopingWave;
        private AudioFileReader _audioFile;
        private SignalGenerator _signalGenerator;
        private bool _useHrtf;
        private bool _addedToMixer;
        private bool _disposed;
        private int _simulationSourceId = -1;

        /// <summary>Current wall position in world space.</summary>
        public float WallX, WallY, WallZ;

        /// <summary>Whether this emitter is actively representing a wall point.</summary>
        public bool Active { get; private set; }

        private const float MinVolume = 0.01f;
        private const float MaxVolume = 0.16f;

        public WallAudioPoint(string wavPath)
        {
            try
            {
                ISampleProvider monoSource;

                if (wavPath != null && File.Exists(wavPath))
                {
                    _audioFile = new AudioFileReader(wavPath);
                    _loopingWave = new LoopingWaveProvider(_audioFile);

                    if (_loopingWave.WaveFormat.Channels == 2)
                        monoSource = new StereoToMonoSampleProvider(_loopingWave.ToSampleProvider());
                    else
                        monoSource = _loopingWave.ToSampleProvider();
                }
                else
                {
                    // Fallback: low continuous tone
                    _signalGenerator = new SignalGenerator(44100, 1)
                    {
                        Frequency = 200f,
                        Type = SignalGeneratorType.Sin,
                        Gain = 0.2
                    };
                    monoSource = _signalGenerator;
                }

                // Build spatialization chain
                if (SteamAudioManager.IsAvailable)
                {
                    ISampleProvider processedMono = monoSource;

                    // Register simulation source for occlusion
                    if (SteamAudioEnvironment.IsInitialized)
                        _simulationSourceId = SteamAudioEnvironment.RegisterSource();

                    // DirectEffect: occlusion/attenuation on mono
                    _directEffect = new DirectEffectSampleProvider(processedMono);
                    processedMono = _directEffect;

                    _hrtfProvider = new HrtfSampleProvider(processedMono);
                    _volumeProvider = new VolumeSampleProvider(_hrtfProvider) { Volume = 0f };
                    _useHrtf = true;
                }
                else
                {
                    _panner = new PanningSampleProvider(monoSource) { Pan = 0f };
                    _volumeProvider = new VolumeSampleProvider(_panner) { Volume = 0f };
                    _useHrtf = false;
                }

                // Register with mixer immediately (stays registered for lifetime)
                if (_volumeProvider != null)
                {
                    AudioOutputMixer.AddInput(_volumeProvider);
                    _addedToMixer = true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[WallAudio] Init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark this emitter as active at the given wall position.
        /// </summary>
        public void Activate(float x, float y, float z)
        {
            WallX = x;
            WallY = y;
            WallZ = z;
            Active = true;
        }

        /// <summary>
        /// Mark this emitter as inactive (silenced).
        /// </summary>
        public void Deactivate()
        {
            Active = false;
            if (_volumeProvider != null)
                _volumeProvider.Volume = 0f;
        }

        /// <summary>
        /// Update audio spatialization based on player position and full camera orientation.
        /// Uses the same 3D camera vectors as the game's AudioListener for correct spatialization.
        /// </summary>
        public void UpdateAudio(float playerX, float playerY, float playerZ,
                                float camFwdX, float camFwdY, float camFwdZ,
                                float camUpX, float camUpY, float camUpZ,
                                float maxRange)
        {
            if (!Active || _volumeProvider == null) return;

            float dx = WallX - playerX;
            float dy = WallY - playerY;
            float dz = WallZ - playerZ;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Very close - full volume
            if (distance < 1f)
            {
                if (_useHrtf && _hrtfProvider != null)
                    _hrtfProvider.SetDirection(0f, 0f, -1f); // directly ahead
                _volumeProvider.Volume = MaxVolume;
                return;
            }

            // Normalize direction to wall
            float ndx = dx / distance;
            float ndy = dy / distance;
            float ndz = dz / distance;

            if (_useHrtf && _hrtfProvider != null)
            {
                // Full 3D HRTF: transform world direction into camera's local space
                // Camera right = cross(camUp, camForward) in Unity's left-handed system
                float crX = camUpY * camFwdZ - camUpZ * camFwdY;
                float crY = camUpZ * camFwdX - camUpX * camFwdZ;
                float crZ = camUpX * camFwdY - camUpY * camFwdX;

                // Project direction onto camera axes
                float localRight   = ndx * crX + ndy * crY + ndz * crZ;
                float localUp      = ndx * camUpX + ndy * camUpY + ndz * camUpZ;
                float localForward = ndx * camFwdX + ndy * camFwdY + ndz * camFwdZ;

                // Steam Audio: right-handed, -Z = forward
                _hrtfProvider.SetDirection(localRight, localUp, -localForward);

                // Update simulation source and apply direct effect
                if (_simulationSourceId >= 0)
                {
                    SteamAudioEnvironment.SetSourcePosition(_simulationSourceId, WallX, WallY, WallZ);
                    if (SteamAudioEnvironment.GetDirectParams(_simulationSourceId, out var directParams))
                        _directEffect?.UpdateParams(directParams);
                }
            }
            else if (_panner != null)
            {
                // Panning fallback: project onto camera right axis
                float crX = camUpY * camFwdZ - camUpZ * camFwdY;
                float crY = camUpZ * camFwdX - camUpX * camFwdZ;
                float crZ = camUpX * camFwdY - camUpY * camFwdX;

                float pan = ndx * crX + ndy * crY + ndz * crZ;
                _panner.Pan = Math.Max(-1f, Math.Min(1f, pan));
            }

            // Volume: squared falloff matching nav sounds
            float normalizedDist = Math.Min(1f, distance / maxRange);
            float falloff = (1f - normalizedDist) * (1f - normalizedDist);
            float volume = MinVolume + falloff * (MaxVolume - MinVolume);
            _volumeProvider.Volume = volume;
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

            if (_simulationSourceId >= 0)
            {
                SteamAudioEnvironment.UnregisterSource(_simulationSourceId);
                _simulationSourceId = -1;
            }

            try { _hrtfProvider?.Dispose(); } catch { }
            try { _directEffect?.Dispose(); } catch { }
            try { _audioFile?.Dispose(); } catch { }

            _hrtfProvider = null;
            _directEffect = null;
            _panner = null;
            _volumeProvider = null;
            _loopingWave = null;
            _audioFile = null;
            _signalGenerator = null;
        }
    }
}
