using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// A MonoBehaviour component that generates audio tones in real-time using OnAudioFilterRead.
    /// This avoids Il2Cpp AudioClip.Create/SetData issues.
    /// </summary>
    public class ToneSource : MonoBehaviour
    {
        // Register the type with Il2Cpp
        static ToneSource()
        {
            ClassInjector.RegisterTypeInIl2Cpp<ToneSource>();
        }

        public ToneSource(System.IntPtr ptr) : base(ptr) { }

        // Tone parameters
        public float Frequency = 440f;
        public float Volume = 0.5f;
        public ToneType Type = ToneType.Sine;
        public bool IsPlaying = false;

        // Internal state
        private double _phase = 0;
        private const int SampleRate = 44100;

        public enum ToneType
        {
            Sine,           // Simple sine wave
            Pulse4Hz,       // Sine with 4Hz pulse
            Pulse8Hz,       // Sine with 8Hz pulse
            TwoTone,        // Alternating two frequencies
            Sweep           // Frequency sweep
        }

        // Sweep parameters
        private float _sweepStartFreq = 300f;
        private float _sweepEndFreq = 600f;
        private float _sweepDuration = 1f;
        private float _sweepTime = 0f;

        public void SetSweep(float startFreq, float endFreq, float duration)
        {
            _sweepStartFreq = startFreq;
            _sweepEndFreq = endFreq;
            _sweepDuration = duration;
            _sweepTime = 0f;
        }

        private void OnAudioFilterRead(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> data, int channels)
        {
            if (!IsPlaying || Volume <= 0)
            {
                // Fill with silence
                for (int i = 0; i < data.Length; i++)
                    data[i] = 0f;
                return;
            }

            int samplesPerChannel = data.Length / channels;

            for (int i = 0; i < samplesPerChannel; i++)
            {
                float sample = GenerateSample();

                // Write to all channels
                for (int c = 0; c < channels; c++)
                {
                    data[i * channels + c] = sample;
                }
            }
        }

        private float GenerateSample()
        {
            float sample = 0f;
            float currentFreq = Frequency;

            switch (Type)
            {
                case ToneType.Sine:
                    sample = Mathf.Sin((float)_phase) * Volume;
                    break;

                case ToneType.Pulse4Hz:
                    float envelope4 = (Mathf.Sin((float)(_phase * 4f / Frequency)) + 1f) * 0.5f;
                    sample = Mathf.Sin((float)_phase) * envelope4 * Volume;
                    break;

                case ToneType.Pulse8Hz:
                    float envelope8 = (Mathf.Sin((float)(_phase * 8f / Frequency)) + 1f) * 0.5f;
                    sample = Mathf.Sin((float)_phase) * envelope8 * Volume;
                    break;

                case ToneType.TwoTone:
                    // Switch between freq and freq*1.5 every ~0.33 seconds
                    float switchPhase = (float)(_phase / (2 * Mathf.PI * 3));
                    bool useFirst = (Mathf.Floor(switchPhase) % 2) == 0;
                    currentFreq = useFirst ? Frequency : Frequency * 1.5f;
                    sample = Mathf.Sin((float)_phase) * Volume;
                    break;

                case ToneType.Sweep:
                    _sweepTime += 1f / SampleRate;
                    if (_sweepTime > _sweepDuration) _sweepTime = 0f;
                    float t = _sweepTime / _sweepDuration;
                    currentFreq = Mathf.Lerp(_sweepStartFreq, _sweepEndFreq, t);
                    sample = Mathf.Sin((float)_phase) * Volume;
                    break;
            }

            // Advance phase
            _phase += 2 * Mathf.PI * currentFreq / SampleRate;
            if (_phase > 2 * Mathf.PI * 1000) // Prevent overflow
                _phase -= 2 * Mathf.PI * 1000;

            return sample;
        }

        public void Play()
        {
            IsPlaying = true;
            _phase = 0;
            _sweepTime = 0;
        }

        public void Stop()
        {
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Factory for creating ToneSource components.
    /// </summary>
    public static class ToneGenerator
    {
        private static bool _typeRegistered = false;

        public static void EnsureRegistered()
        {
            if (_typeRegistered) return;

            try
            {
                // The static constructor of ToneSource registers the type
                var type = typeof(ToneSource);
                _typeRegistered = true;
                DebugLogger.Log("[ToneGen] ToneSource type registered with Il2Cpp");
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ToneGen] Failed to register ToneSource: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a ToneSource on the given GameObject
        /// </summary>
        public static ToneSource CreateToneSource(GameObject obj, float frequency, float volume, ToneSource.ToneType type)
        {
            EnsureRegistered();

            try
            {
                // Ensure there's an AudioSource component
                var audioSource = obj.GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = obj.AddComponent<AudioSource>();
                }

                // Configure the AudioSource for 3D
                audioSource.spatialBlend = 1f;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
                audioSource.minDistance = 1f;
                audioSource.maxDistance = 30f;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
                audioSource.volume = 1f; // Volume is controlled by ToneSource

                // Add ToneSource component
                var toneSource = obj.AddComponent<ToneSource>();
                toneSource.Frequency = frequency;
                toneSource.Volume = volume;
                toneSource.Type = type;

                // Start the AudioSource (OnAudioFilterRead needs it playing)
                audioSource.Play();

                return toneSource;
            }
            catch (System.Exception ex)
            {
                DebugLogger.Log($"[ToneGen] CreateToneSource error: {ex.Message}");
                return null;
            }
        }

        // Preset configurations
        public static class Presets
        {
            public static (float freq, float vol, ToneSource.ToneType type) Wall = (100f, 0.3f, ToneSource.ToneType.Sine);
            public static (float freq, float vol, ToneSource.ToneType type) Item = (500f, 0.4f, ToneSource.ToneType.Pulse4Hz);
            public static (float freq, float vol, ToneSource.ToneType type) Npc = (400f, 0.35f, ToneSource.ToneType.TwoTone);
            public static (float freq, float vol, ToneSource.ToneType type) Enemy = (350f, 0.45f, ToneSource.ToneType.Pulse8Hz);
            public static (float freq, float vol, ToneSource.ToneType type) Door = (300f, 0.35f, ToneSource.ToneType.Sweep);
            public static (float freq, float vol, ToneSource.ToneType type) Test = (440f, 0.5f, ToneSource.ToneType.Sine);
        }
    }
}
