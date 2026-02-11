using System;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace DigimonNOAccess
{
    /// <summary>
    /// Generates short audio tones for battle events using NAudio.
    /// Each tone type uses a separate WaveOutEvent to avoid conflicts.
    /// Tones are fire-and-forget: call Play*() and the tone stops automatically.
    /// </summary>
    public static class BattleAudioCues
    {
        private static bool _initialized = false;

        // Reusable output devices (one per tone type to avoid conflicts)
        private static WaveOutEvent _spWarningOut;
        private static WaveOutEvent _cheerTickOut;
        private static WaveOutEvent _opMilestoneOut;
        private static WaveOutEvent _targetSwitchOut;

        /// <summary>
        /// Initialize audio output devices. Call once at startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                _spWarningOut = CreateOutput();
                _cheerTickOut = CreateOutput();
                _opMilestoneOut = CreateOutput();
                _targetSwitchOut = CreateOutput();
                _initialized = true;
                DebugLogger.Log("[BattleAudioCues] Initialized");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[BattleAudioCues] Init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clean up audio resources.
        /// </summary>
        public static void Shutdown()
        {
            _initialized = false;
            DisposeOutput(ref _spWarningOut);
            DisposeOutput(ref _cheerTickOut);
            DisposeOutput(ref _opMilestoneOut);
            DisposeOutput(ref _targetSwitchOut);
        }

        /// <summary>
        /// Rising two-tone alert when enemy charges special attack.
        /// ~200ms total: 400Hz for 100ms then 800Hz for 100ms.
        /// </summary>
        public static void PlaySPWarning()
        {
            if (!_initialized) return;
            PlayTone(_spWarningOut, new[]
            {
                (400f, 0.10f, 0.5f),
                (800f, 0.10f, 0.6f)
            });
        }

        /// <summary>
        /// Quiet subtle tick on enemy damage hit (cheer unavailable).
        /// Gives fight rhythm feedback.
        /// </summary>
        public static void PlayCheerTickQuiet()
        {
            if (!_initialized) return;
            PlayTone(_cheerTickOut, new[] { (800f, 0.03f, 0.15f) });
        }

        /// <summary>
        /// Louder distinct beep on enemy damage hit (cheer available).
        /// Signals "press X NOW for max OP".
        /// </summary>
        public static void PlayCheerTickLoud()
        {
            if (!_initialized) return;
            PlayTone(_cheerTickOut, new[] { (1200f, 0.06f, 0.45f) });
        }

        /// <summary>
        /// Brief ascending chime when partner reaches 150 OP.
        /// C5 -> E5 -> G5 (~300ms total).
        /// </summary>
        public static void PlayOPMilestone()
        {
            if (!_initialized) return;
            PlayTone(_opMilestoneOut, new[]
            {
                (523.25f, 0.10f, 0.4f),  // C5
                (659.25f, 0.10f, 0.4f),  // E5
                (783.99f, 0.10f, 0.5f)   // G5
            });
        }

        /// <summary>
        /// Low tone when enemy switches target partner.
        /// </summary>
        public static void PlayTargetSwitch()
        {
            if (!_initialized) return;
            PlayTone(_targetSwitchOut, new[] { (300f, 0.10f, 0.35f) });
        }

        /// <summary>
        /// Play a sequence of tones on the given output device.
        /// Each tuple is (frequency, durationSeconds, volume).
        /// Runs on a background thread to avoid blocking the game.
        /// </summary>
        private static void PlayTone(WaveOutEvent output, (float freq, float dur, float vol)[] tones)
        {
            if (output == null) return;

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    foreach (var (freq, dur, vol) in tones)
                    {
                        var generator = new SignalGenerator(44100, 1)
                        {
                            Frequency = freq,
                            Type = SignalGeneratorType.Sin,
                            Gain = 0.3
                        };

                        var volumeProvider = new VolumeSampleProvider(generator)
                        {
                            Volume = vol
                        };

                        // Wrap in a timed provider that stops after duration
                        var timed = new OffsetSampleProvider(volumeProvider)
                        {
                            Take = TimeSpan.FromSeconds(dur)
                        };

                        lock (output)
                        {
                            try
                            {
                                output.Stop();
                                output.Init(timed);
                                output.Play();
                            }
                            catch { }
                        }

                        // Wait for tone duration
                        Thread.Sleep((int)(dur * 1000));
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[BattleAudioCues] PlayTone error: {ex.Message}");
                }
            });
        }

        private static WaveOutEvent CreateOutput()
        {
            return new WaveOutEvent { DesiredLatency = 50 };
        }

        private static void DisposeOutput(ref WaveOutEvent output)
        {
            try
            {
                output?.Stop();
                output?.Dispose();
            }
            catch { }
            output = null;
        }
    }
}
