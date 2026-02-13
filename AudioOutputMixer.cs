using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Shared audio output that mixes all positional audio sources through a single WaveOutEvent.
    /// Eliminates stuttering caused by multiple WaveOutEvent instances competing for audio resources.
    /// All PositionalAudio instances register their provider chains here instead of owning WaveOutEvents.
    /// </summary>
    public static class AudioOutputMixer
    {
        private static WaveOutEvent _waveOut;
        private static MixingSampleProvider _mixer;
        private static bool _initialized;

        public static bool IsInitialized => _initialized;

        public static void Initialize()
        {
            if (_initialized) return;

            try
            {
                // 44100Hz stereo matches Steam Audio HRTF output and all our source chains
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
                {
                    ReadFully = true // Output silence when no inputs (keeps WaveOut alive)
                };

                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 200
                };
                _waveOut.PlaybackStopped += (sender, args) =>
                {
                    if (args.Exception != null)
                        DebugLogger.Error($"[AudioMixer] Playback error: {args.Exception.GetType().Name}: {args.Exception.Message}");
                };
                _waveOut.Init(_mixer);
                _waveOut.Play();

                _initialized = true;
                DebugLogger.Log("[AudioMixer] Initialized - single output for all positional audio");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[AudioMixer] Init failed: {ex.Message}");
            }
        }

        public static void AddInput(ISampleProvider source)
        {
            if (!_initialized || _mixer == null || source == null) return;
            try
            {
                _mixer.AddMixerInput(source);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[AudioMixer] AddInput error: {ex.Message}");
            }
        }

        public static void RemoveInput(ISampleProvider source)
        {
            if (!_initialized || _mixer == null || source == null) return;
            try
            {
                _mixer.RemoveMixerInput(source);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[AudioMixer] RemoveInput error: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            _initialized = false;

            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;
                _mixer = null;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[AudioMixer] Shutdown error: {ex.Message}");
            }

            DebugLogger.Log("[AudioMixer] Shut down");
        }
    }
}
