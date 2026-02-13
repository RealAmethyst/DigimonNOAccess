using System;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace DigimonNOAccess
{
    /// <summary>
    /// NAudio ISampleProvider that applies Steam Audio HRTF binaural processing.
    /// Takes mono input, outputs stereo binaural audio.
    /// Replaces PanningSampleProvider for true 3D spatial audio with front/back distinction.
    /// </summary>
    public class HrtfSampleProvider : ISampleProvider, IDisposable
    {
        private readonly ISampleProvider _source;
        private readonly int _frameSize;
        private IntPtr _binauralEffect;
        private IPLAudioBuffer _inBuffer;
        private IPLAudioBuffer _outBuffer;
        private bool _buffersAllocated;
        private bool _disposed;

        // Mono input staging buffer (one frame of mono samples)
        private readonly float[] _monoFrame;

        // Stereo output staging buffer (one frame of interleaved stereo)
        private readonly float[] _stereoFrame;

        // Pre-pinned handles for the staging buffers (avoids GC pressure on audio thread)
        private GCHandle _monoHandle;
        private GCHandle _stereoHandle;
        private IntPtr _monoPtr;
        private IntPtr _stereoPtr;

        // Overflow buffer for when NAudio requests fewer samples than one HRTF frame produces
        private readonly float[] _overflow;
        private int _overflowOffset;
        private int _overflowCount;

        // Direction from listener to source in Steam Audio coordinates (right-handed)
        private float _dirX, _dirY, _dirZ;
        private readonly object _directionLock = new object();

        public WaveFormat WaveFormat { get; }

        public HrtfSampleProvider(ISampleProvider monoSource)
        {
            _source = monoSource ?? throw new ArgumentNullException(nameof(monoSource));
            _frameSize = SteamAudioManager.FrameSize;

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(SteamAudioManager.SampleRate, 2);

            _monoFrame = new float[_frameSize];
            _stereoFrame = new float[_frameSize * 2];
            _overflow = new float[_frameSize * 2];

            // Pin staging buffers once for the lifetime of this instance.
            // This avoids GCHandle.Alloc/Free on every audio frame which causes GC pressure and stuttering.
            _monoHandle = GCHandle.Alloc(_monoFrame, GCHandleType.Pinned);
            _monoPtr = _monoHandle.AddrOfPinnedObject();
            _stereoHandle = GCHandle.Alloc(_stereoFrame, GCHandleType.Pinned);
            _stereoPtr = _stereoHandle.AddrOfPinnedObject();

            // Default direction: directly in front (Steam Audio: -Z = forward)
            _dirX = 0f;
            _dirY = 0f;
            _dirZ = -1f;

            InitializeSteamAudio();
        }

        private void InitializeSteamAudio()
        {
            _binauralEffect = SteamAudioManager.CreateBinauralEffect();
            if (_binauralEffect == IntPtr.Zero)
            {
                DebugLogger.Warning("[HrtfSampleProvider] Failed to create binaural effect");
                return;
            }

            var context = SteamAudioManager.Context;

            _inBuffer = new IPLAudioBuffer();
            var err = SteamAudioNative.iplAudioBufferAllocate(context, 1, _frameSize, ref _inBuffer);
            if (err != IPLerror.Success)
            {
                DebugLogger.Error($"[HrtfSampleProvider] Failed to allocate input buffer: {err}");
                SteamAudioManager.ReleaseBinauralEffect(ref _binauralEffect);
                return;
            }

            _outBuffer = new IPLAudioBuffer();
            err = SteamAudioNative.iplAudioBufferAllocate(context, 2, _frameSize, ref _outBuffer);
            if (err != IPLerror.Success)
            {
                DebugLogger.Error($"[HrtfSampleProvider] Failed to allocate output buffer: {err}");
                SteamAudioNative.iplAudioBufferFree(context, ref _inBuffer);
                SteamAudioManager.ReleaseBinauralEffect(ref _binauralEffect);
                return;
            }

            _buffersAllocated = true;
        }

        /// <summary>
        /// Update the direction from listener to source in Steam Audio coordinates.
        /// Called from the position update thread.
        /// Steam Audio coords: X=right, Y=up, -Z=forward (right-handed).
        /// </summary>
        public void SetDirection(float x, float y, float z)
        {
            lock (_directionLock)
            {
                _dirX = x;
                _dirY = y;
                _dirZ = z;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            // If Steam Audio not set up, pass through silence
            if (!_buffersAllocated || _binauralEffect == IntPtr.Zero)
            {
                for (int i = 0; i < count; i++)
                    buffer[offset + i] = 0f;
                return count;
            }

            int samplesWritten = 0;

            // First, drain any overflow from previous frame processing
            if (_overflowCount > 0)
            {
                int toCopy = Math.Min(_overflowCount, count);
                // Use Buffer.BlockCopy - NAudio's WaveBuffer overlays byte[]/float[]
                // which causes Array.Copy to throw ArrayTypeMismatchException
                Buffer.BlockCopy(_overflow, _overflowOffset * sizeof(float),
                    buffer, offset * sizeof(float), toCopy * sizeof(float));
                samplesWritten += toCopy;
                _overflowOffset += toCopy;
                _overflowCount -= toCopy;
            }

            // Process full HRTF frames until we have enough output
            while (samplesWritten < count)
            {
                // Read one frame of mono samples from source
                int monoRead = _source.Read(_monoFrame, 0, _frameSize);
                if (monoRead == 0) break;

                // Zero-pad if we got fewer than a full frame
                if (monoRead < _frameSize)
                {
                    for (int i = monoRead; i < _frameSize; i++)
                        _monoFrame[i] = 0f;
                }

                // Process through HRTF (uses pre-pinned pointers, no GC allocation)
                ProcessHrtfFrame();

                // Copy processed stereo to output buffer
                int stereoSamples = _frameSize * 2;
                int canCopy = Math.Min(stereoSamples, count - samplesWritten);
                Buffer.BlockCopy(_stereoFrame, 0,
                    buffer, (offset + samplesWritten) * sizeof(float), canCopy * sizeof(float));
                samplesWritten += canCopy;

                // Store any excess in overflow
                if (canCopy < stereoSamples)
                {
                    _overflowCount = stereoSamples - canCopy;
                    _overflowOffset = 0;
                    Buffer.BlockCopy(_stereoFrame, canCopy * sizeof(float),
                        _overflow, 0, _overflowCount * sizeof(float));
                }
            }

            // Fill any remaining with silence
            if (samplesWritten < count)
            {
                for (int i = samplesWritten; i < count; i++)
                    buffer[offset + i] = 0f;
            }

            return count;
        }

        private void ProcessHrtfFrame()
        {
            var context = SteamAudioManager.Context;

            // Copy mono samples into Steam Audio input buffer (pre-pinned pointer)
            SteamAudioNative.iplAudioBufferDeinterleave(context, _monoPtr, ref _inBuffer);

            // Set up binaural params with current direction
            float dirX, dirY, dirZ;
            lock (_directionLock)
            {
                dirX = _dirX;
                dirY = _dirY;
                dirZ = _dirZ;
            }

            var binauralParams = new IPLBinauralEffectParams
            {
                direction = new IPLVector3 { x = dirX, y = dirY, z = dirZ },
                interpolation = IPLHRTFInterpolation.Bilinear,
                spatialBlend = 1.0f,
                hrtf = SteamAudioManager.Hrtf,
                peakDelays = IntPtr.Zero
            };

            // Apply HRTF
            SteamAudioNative.iplBinauralEffectApply(
                _binauralEffect, ref binauralParams, ref _inBuffer, ref _outBuffer);

            // Read stereo output back to managed array (pre-pinned pointer)
            SteamAudioNative.iplAudioBufferInterleave(context, ref _outBuffer, _stereoPtr);
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

            SteamAudioManager.ReleaseBinauralEffect(ref _binauralEffect);

            // Unpin staging buffers
            if (_monoHandle.IsAllocated) _monoHandle.Free();
            if (_stereoHandle.IsAllocated) _stereoHandle.Free();
        }
    }
}
