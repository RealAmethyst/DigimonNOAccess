using System;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace DigimonNOAccess
{
    /// <summary>
    /// NAudio ISampleProvider that applies Steam Audio DirectEffect processing.
    /// Takes mono input, outputs mono with occlusion, distance attenuation, and air absorption applied.
    /// Sits between the audio source and HrtfSampleProvider in the chain.
    /// </summary>
    public class DirectEffectSampleProvider : ISampleProvider, IDisposable
    {
        private readonly ISampleProvider _source;
        private readonly int _frameSize;
        private IntPtr _directEffect;
        private IPLAudioBuffer _inBuffer;
        private IPLAudioBuffer _outBuffer;
        private bool _buffersAllocated;
        private bool _disposed;

        // Mono staging buffers (one frame each)
        private readonly float[] _monoInFrame;
        private readonly float[] _monoOutFrame;

        // Pre-pinned handles (same pattern as HrtfSampleProvider)
        private GCHandle _monoInHandle;
        private GCHandle _monoOutHandle;
        private IntPtr _monoInPtr;
        private IntPtr _monoOutPtr;

        // Overflow buffer for frame-boundary alignment
        private readonly float[] _overflow;
        private int _overflowOffset;
        private int _overflowCount;

        // Direct effect params (updated from simulation, read on audio thread)
        private IPLDirectEffectParams _params;
        private readonly object _paramsLock = new object();

        public WaveFormat WaveFormat { get; }

        /// <summary>
        /// True if Steam Audio DirectEffect is active. False = pass-through mode.
        /// </summary>
        public bool IsActive => _buffersAllocated && _directEffect != IntPtr.Zero;

        public DirectEffectSampleProvider(ISampleProvider monoSource)
        {
            _source = monoSource ?? throw new ArgumentNullException(nameof(monoSource));
            _frameSize = SteamAudioManager.FrameSize;

            // Output is mono (same as input) - HRTF converts to stereo later
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SteamAudioManager.SampleRate, 1);

            _monoInFrame = new float[_frameSize];
            _monoOutFrame = new float[_frameSize];
            _overflow = new float[_frameSize];

            // Pin staging buffers for lifetime
            _monoInHandle = GCHandle.Alloc(_monoInFrame, GCHandleType.Pinned);
            _monoInPtr = _monoInHandle.AddrOfPinnedObject();
            _monoOutHandle = GCHandle.Alloc(_monoOutFrame, GCHandleType.Pinned);
            _monoOutPtr = _monoOutHandle.AddrOfPinnedObject();

            // Default params: fully transparent (no effect)
            _params = new IPLDirectEffectParams
            {
                flags = IPLDirectEffectFlags.ApplyDistanceAttenuation
                      | IPLDirectEffectFlags.ApplyAirAbsorption
                      | IPLDirectEffectFlags.ApplyOcclusion,
                transmissionType = IPLTransmissionType.FrequencyIndependent,
                distanceAttenuation = 1.0f,
                airAbsorptionLow = 1.0f,
                airAbsorptionMid = 1.0f,
                airAbsorptionHigh = 1.0f,
                directivity = 1.0f,
                occlusion = 1.0f,
                transmissionLow = 1.0f,
                transmissionMid = 1.0f,
                transmissionHigh = 1.0f
            };

            InitializeSteamAudio();
        }

        private void InitializeSteamAudio()
        {
            if (!SteamAudioManager.IsAvailable) return;

            try
            {
                var context = SteamAudioManager.Context;
                var audioSettings = SteamAudioManager.AudioSettings;
                var effectSettings = new IPLDirectEffectSettings { numChannels = 1 };

                var err = SteamAudioNative.iplDirectEffectCreate(
                    context, ref audioSettings, ref effectSettings, out _directEffect);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[DirectEffect] Create failed: {err}");
                    return;
                }

                _inBuffer = new IPLAudioBuffer();
                err = SteamAudioNative.iplAudioBufferAllocate(context, 1, _frameSize, ref _inBuffer);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[DirectEffect] Input buffer alloc failed: {err}");
                    SteamAudioNative.iplDirectEffectRelease(ref _directEffect);
                    return;
                }

                _outBuffer = new IPLAudioBuffer();
                err = SteamAudioNative.iplAudioBufferAllocate(context, 1, _frameSize, ref _outBuffer);
                if (err != IPLerror.Success)
                {
                    DebugLogger.Error($"[DirectEffect] Output buffer alloc failed: {err}");
                    SteamAudioNative.iplAudioBufferFree(context, ref _inBuffer);
                    SteamAudioNative.iplDirectEffectRelease(ref _directEffect);
                    return;
                }

                _buffersAllocated = true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[DirectEffect] Init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update effect parameters from simulation results.
        /// Called from the game/position update thread.
        /// </summary>
        public void UpdateParams(IPLDirectEffectParams newParams)
        {
            lock (_paramsLock)
            {
                _params = newParams;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // If DirectEffect not available, pass through source directly
            if (!_buffersAllocated || _directEffect == IntPtr.Zero)
            {
                return _source.Read(buffer, offset, count);
            }

            int samplesWritten = 0;

            // Drain overflow from previous frame
            if (_overflowCount > 0)
            {
                int toCopy = Math.Min(_overflowCount, count);
                Buffer.BlockCopy(_overflow, _overflowOffset * sizeof(float),
                    buffer, offset * sizeof(float), toCopy * sizeof(float));
                samplesWritten += toCopy;
                _overflowOffset += toCopy;
                _overflowCount -= toCopy;
            }

            // Process full frames until we have enough output
            while (samplesWritten < count)
            {
                int monoRead = _source.Read(_monoInFrame, 0, _frameSize);
                if (monoRead == 0) break;

                // Zero-pad if partial frame
                if (monoRead < _frameSize)
                {
                    for (int i = monoRead; i < _frameSize; i++)
                        _monoInFrame[i] = 0f;
                }

                // Process through DirectEffect
                ProcessDirectFrame();

                // Copy processed mono to output
                int canCopy = Math.Min(_frameSize, count - samplesWritten);
                Buffer.BlockCopy(_monoOutFrame, 0,
                    buffer, (offset + samplesWritten) * sizeof(float), canCopy * sizeof(float));
                samplesWritten += canCopy;

                // Store excess in overflow
                if (canCopy < _frameSize)
                {
                    _overflowCount = _frameSize - canCopy;
                    _overflowOffset = 0;
                    Buffer.BlockCopy(_monoOutFrame, canCopy * sizeof(float),
                        _overflow, 0, _overflowCount * sizeof(float));
                }
            }

            // Fill remaining with silence
            if (samplesWritten < count)
            {
                for (int i = samplesWritten; i < count; i++)
                    buffer[offset + i] = 0f;
            }

            return count;
        }

        private bool _nanWarned;

        private void ProcessDirectFrame()
        {
            var context = SteamAudioManager.Context;

            // Deinterleave mono input into Steam Audio buffer
            SteamAudioNative.iplAudioBufferDeinterleave(context, _monoInPtr, ref _inBuffer);

            // Read current params under lock
            IPLDirectEffectParams currentParams;
            lock (_paramsLock)
            {
                currentParams = _params;
            }

            // Apply direct effect (occlusion, attenuation, air absorption)
            SteamAudioNative.iplDirectEffectApply(
                _directEffect, ref currentParams, ref _inBuffer, ref _outBuffer);

            // Interleave output back to managed array
            SteamAudioNative.iplAudioBufferInterleave(context, ref _outBuffer, _monoOutPtr);

            // Sanitize output - if DirectEffect produced NaN/Inf, use input instead
            bool hasNaN = false;
            for (int i = 0; i < _frameSize; i++)
            {
                if (float.IsNaN(_monoOutFrame[i]) || float.IsInfinity(_monoOutFrame[i]))
                {
                    hasNaN = true;
                    break;
                }
            }
            if (hasNaN)
            {
                Buffer.BlockCopy(_monoInFrame, 0, _monoOutFrame, 0, _frameSize * sizeof(float));
                if (!_nanWarned)
                {
                    _nanWarned = true;
                    DebugLogger.Warning("[DirectEffect] NaN in output, falling back to unprocessed audio");
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            var context = SteamAudioManager.Context;

            if (_buffersAllocated && context != IntPtr.Zero)
            {
                SteamAudioNative.iplAudioBufferFree(context, ref _outBuffer);
                SteamAudioNative.iplAudioBufferFree(context, ref _inBuffer);
                _buffersAllocated = false;
            }

            if (_directEffect != IntPtr.Zero)
                SteamAudioNative.iplDirectEffectRelease(ref _directEffect);

            if (_monoInHandle.IsAllocated) _monoInHandle.Free();
            if (_monoOutHandle.IsAllocated) _monoOutHandle.Free();
        }
    }
}
