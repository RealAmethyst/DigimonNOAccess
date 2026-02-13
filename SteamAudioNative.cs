using System;
using System.Runtime.InteropServices;

namespace DigimonNOAccess
{
    // ========================================================================
    // Enums - Existing
    // ========================================================================

    public enum IPLerror : int
    {
        Success = 0,
        Failure = 1,
        OutOfMemory = 2,
        Initialization = 3
    }

    public enum IPLSIMDLevel : int
    {
        SSE2 = 0,
        SSE4 = 1,
        AVX = 2,
        AVX2 = 3,
        AVX512 = 4
    }

    public enum IPLHRTFType : int
    {
        Default = 0,
        SOFA = 1
    }

    public enum IPLHRTFInterpolation : int
    {
        Nearest = 0,
        Bilinear = 1
    }

    public enum IPLHRTFNormType : int
    {
        None = 0,
        RMS = 1
    }

    public enum IPLAudioEffectState : int
    {
        TailRemaining = 0,
        TailComplete = 1
    }

    // ========================================================================
    // Enums - Environmental Audio
    // ========================================================================

    public enum IPLSceneType : int
    {
        Default = 0,
        Embree = 1,
        RadeonRays = 2,
        Custom = 3
    }

    public enum IPLReflectionEffectType : int
    {
        Convolution = 0,
        Parametric = 1,
        Hybrid = 2,
        TAN = 3
    }

    public enum IPLOcclusionType : int
    {
        Raycast = 0,
        Volumetric = 1
    }

    public enum IPLDistanceAttenuationModelType : int
    {
        Default = 0,
        InverseDistance = 1,
        Callback = 2
    }

    public enum IPLAirAbsorptionModelType : int
    {
        Default = 0,
        Exponential = 1,
        Callback = 2
    }

    public enum IPLTransmissionType : int
    {
        FrequencyIndependent = 0,
        FrequencyDependent = 1
    }

    [Flags]
    public enum IPLSimulationFlags : int
    {
        Direct = 0x1,
        Reflections = 0x2,
        Pathing = 0x4
    }

    [Flags]
    public enum IPLDirectSimulationFlags : int
    {
        DistanceAttenuation = 0x1,
        AirAbsorption = 0x2,
        Directivity = 0x4,
        Occlusion = 0x8,
        Transmission = 0x10
    }

    [Flags]
    public enum IPLDirectEffectFlags : int
    {
        ApplyDistanceAttenuation = 0x1,
        ApplyAirAbsorption = 0x2,
        ApplyDirectivity = 0x4,
        ApplyOcclusion = 0x8,
        ApplyTransmission = 0x10
    }

    // ========================================================================
    // Structs - Existing
    // ========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLVector3
    {
        public float x;
        public float y;
        public float z;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLContextSettings
    {
        public uint version;
        public IntPtr logCallback;
        public IntPtr allocateCallback;
        public IntPtr freeCallback;
        public IPLSIMDLevel simdLevel;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLAudioSettings
    {
        public int samplingRate;
        public int frameSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLAudioBuffer
    {
        public int numChannels;
        public int numSamples;
        public IntPtr data; // float** - pointer to array of float* (one per channel)
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLHRTFSettings
    {
        public IPLHRTFType type;
        public IntPtr sofaFileName;
        public IntPtr sofaData;
        public int sofaDataSize;
        public float volume;
        public IPLHRTFNormType normType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLBinauralEffectSettings
    {
        public IntPtr hrtf;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLBinauralEffectParams
    {
        public IPLVector3 direction;
        public IPLHRTFInterpolation interpolation;
        public float spatialBlend;
        public IntPtr hrtf;
        public IntPtr peakDelays;
    }

    // ========================================================================
    // Structs - Environmental Audio: Geometry
    // ========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLTriangle
    {
        public int index0;
        public int index1;
        public int index2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLMaterial
    {
        public float absorptionLow;
        public float absorptionMid;
        public float absorptionHigh;
        public float scattering;
        public float transmissionLow;
        public float transmissionMid;
        public float transmissionHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLCoordinateSpace3
    {
        public IPLVector3 right;
        public IPLVector3 up;
        public IPLVector3 ahead;
        public IPLVector3 origin;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLSceneSettings
    {
        public IPLSceneType type;
        public IntPtr closestHitCallback;
        public IntPtr anyHitCallback;
        public IntPtr batchedClosestHitCallback;
        public IntPtr batchedAnyHitCallback;
        public IntPtr userData;
        public IntPtr embreeDevice;
        public IntPtr radeonRaysDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLStaticMeshSettings
    {
        public int numVertices;
        public int numTriangles;
        public int numMaterials;
        public IntPtr vertices;       // IPLVector3*
        public IntPtr triangles;      // IPLTriangle*
        public IntPtr materialIndices; // int*
        public IntPtr materials;      // IPLMaterial*
    }

    // ========================================================================
    // Structs - Environmental Audio: Simulation
    // ========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLDistanceAttenuationModel
    {
        public IPLDistanceAttenuationModelType type;
        public float minDistance;
        public IntPtr callback;  // IPLDistanceAttenuationCallback
        public IntPtr userData;
        public byte dirty;       // IPLbool
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLAirAbsorptionModel
    {
        public IPLAirAbsorptionModelType type;
        public float coefficientLow;
        public float coefficientMid;
        public float coefficientHigh;
        public IntPtr callback;  // IPLAirAbsorptionCallback
        public IntPtr userData;
        public byte dirty;       // IPLbool
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLDirectivity
    {
        public float dipoleWeight;
        public float dipolePower;
        public IntPtr callback;  // IPLDirectivityCallback
        public IntPtr userData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLBakedDataIdentifier
    {
        public int identifier;
        public int type; // IPLBakedDataType
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLSimulationSettings
    {
        public IPLSimulationFlags flags;
        public IPLSceneType sceneType;
        public IPLReflectionEffectType reflectionType;
        public int maxNumOcclusionSamples;
        public int maxNumRays;
        public int numDiffuseSamples;
        public float maxDuration;
        public int maxOrder;
        public int maxNumSources;
        public int numThreads;
        public int rayBatchSize;
        public int numVisSamples;
        public int samplingRate;
        public int frameSize;
        public IntPtr openCLDevice;
        public IntPtr radeonRaysDevice;
        public IntPtr tanDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLSourceSettings
    {
        public IPLSimulationFlags flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLSimulationSharedInputs
    {
        public IPLCoordinateSpace3 listener;
        public int numRays;
        public int numBounces;
        public float duration;
        public int order;
        public float irradianceMinDistance;
        public IntPtr pathingVisCallback;
        public IntPtr pathingUserData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLSimulationInputs
    {
        public IPLSimulationFlags flags;
        public IPLDirectSimulationFlags directFlags;
        public IPLCoordinateSpace3 source;
        public IPLDistanceAttenuationModel distanceAttenuationModel;
        public IPLAirAbsorptionModel airAbsorptionModel;
        public IPLDirectivity directivity;
        public IPLOcclusionType occlusionType;
        public float occlusionRadius;
        public int numOcclusionSamples;
        public float reverbScale0;
        public float reverbScale1;
        public float reverbScale2;
        public float hybridReverbTransitionTime;
        public float hybridReverbOverlapPercent;
        public byte baked; // IPLbool
        public IPLBakedDataIdentifier bakedDataIdentifier;
        public IntPtr pathingProbes; // IPLProbeBatch
        public float visRadius;
        public float visThreshold;
        public float visRange;
        public int pathingOrder;
        public byte enableValidation;    // IPLbool
        public byte findAlternatePaths;  // IPLbool
        public int numTransmissionRays;
        public IntPtr deviationModel; // IPLDeviationModel*
    }

    // ========================================================================
    // Structs - Environmental Audio: Effect Parameters
    // ========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLDirectEffectSettings
    {
        public int numChannels;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLDirectEffectParams
    {
        public IPLDirectEffectFlags flags;
        public IPLTransmissionType transmissionType;
        public float distanceAttenuation;
        public float airAbsorptionLow;
        public float airAbsorptionMid;
        public float airAbsorptionHigh;
        public float directivity;
        public float occlusion;
        public float transmissionLow;
        public float transmissionMid;
        public float transmissionHigh;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLReflectionEffectSettings
    {
        public IPLReflectionEffectType type;
        public int irSize;
        public int numChannels;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLReflectionEffectParams
    {
        public IPLReflectionEffectType type;
        public IntPtr ir; // IPLReflectionEffectIR (opaque handle)
        public float reverbTime0;
        public float reverbTime1;
        public float reverbTime2;
        public float eq0;
        public float eq1;
        public float eq2;
        public int delay;
        public int numChannels;
        public int irSize;
        public IntPtr tanDevice;
        public int tanSlot;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLPathEffectParams
    {
        public float eqCoeffs0;
        public float eqCoeffs1;
        public float eqCoeffs2;
        public IntPtr shCoeffs; // float*
        public int order;
        public byte binaural; // IPLbool
        public IntPtr hrtf;   // IPLHRTF
        public IPLCoordinateSpace3 listener;
        public byte normalizeEQ; // IPLbool
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IPLSimulationOutputs
    {
        public IPLDirectEffectParams direct;
        public IPLReflectionEffectParams reflections;
        public IPLPathEffectParams pathing;
    }

    // ========================================================================
    // P/Invoke Functions
    // ========================================================================

    internal static class SteamAudioNative
    {
        private const string Library = "phonon";
        private const CallingConvention CC = CallingConvention.Cdecl;

        public static uint MakeVersion(int major, int minor, int patch)
        {
            return (uint)((major << 16) | (minor << 8) | patch);
        }

        // --- Context ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplContextCreate(
            ref IPLContextSettings settings,
            out IntPtr context);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplContextRelease(ref IntPtr context);

        // --- HRTF ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplHRTFCreate(
            IntPtr context,
            ref IPLAudioSettings audioSettings,
            ref IPLHRTFSettings hrtfSettings,
            out IntPtr hrtf);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplHRTFRelease(ref IntPtr hrtf);

        // --- Binaural Effect ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplBinauralEffectCreate(
            IntPtr context,
            ref IPLAudioSettings audioSettings,
            ref IPLBinauralEffectSettings effectSettings,
            out IntPtr effect);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplBinauralEffectRelease(ref IntPtr effect);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplBinauralEffectReset(IntPtr effect);

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLAudioEffectState iplBinauralEffectApply(
            IntPtr effect,
            ref IPLBinauralEffectParams parameters,
            ref IPLAudioBuffer inBuffer,
            ref IPLAudioBuffer outBuffer);

        // --- Audio Buffers ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplAudioBufferAllocate(
            IntPtr context,
            int numChannels,
            int numSamples,
            ref IPLAudioBuffer audioBuffer);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplAudioBufferFree(
            IntPtr context,
            ref IPLAudioBuffer audioBuffer);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplAudioBufferDeinterleave(
            IntPtr context,
            IntPtr interleavedBuffer,
            ref IPLAudioBuffer audioBuffer);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplAudioBufferInterleave(
            IntPtr context,
            ref IPLAudioBuffer audioBuffer,
            IntPtr interleavedBuffer);

        // --- Scene ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplSceneCreate(
            IntPtr context,
            ref IPLSceneSettings settings,
            out IntPtr scene);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSceneRelease(ref IntPtr scene);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSceneCommit(IntPtr scene);

        // --- Static Mesh ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplStaticMeshCreate(
            IntPtr scene,
            ref IPLStaticMeshSettings settings,
            out IntPtr staticMesh);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplStaticMeshRelease(ref IntPtr staticMesh);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplStaticMeshAdd(IntPtr staticMesh, IntPtr scene);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplStaticMeshRemove(IntPtr staticMesh, IntPtr scene);

        // --- Simulator ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplSimulatorCreate(
            IntPtr context,
            ref IPLSimulationSettings settings,
            out IntPtr simulator);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSimulatorRelease(ref IntPtr simulator);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSimulatorSetScene(IntPtr simulator, IntPtr scene);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSimulatorCommit(IntPtr simulator);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSimulatorSetSharedInputs(
            IntPtr simulator,
            IPLSimulationFlags flags,
            ref IPLSimulationSharedInputs sharedInputs);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSimulatorRunDirect(IntPtr simulator);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSimulatorRunReflections(IntPtr simulator);

        // --- Source ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplSourceCreate(
            IntPtr simulator,
            ref IPLSourceSettings settings,
            out IntPtr source);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSourceRelease(ref IntPtr source);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSourceAdd(IntPtr source, IntPtr simulator);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSourceRemove(IntPtr source, IntPtr simulator);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSourceSetInputs(
            IntPtr source,
            IPLSimulationFlags flags,
            ref IPLSimulationInputs inputs);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplSourceGetOutputs(
            IntPtr source,
            IPLSimulationFlags flags,
            ref IPLSimulationOutputs outputs);

        // --- Direct Effect ---

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLerror iplDirectEffectCreate(
            IntPtr context,
            ref IPLAudioSettings audioSettings,
            ref IPLDirectEffectSettings effectSettings,
            out IntPtr effect);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplDirectEffectRelease(ref IntPtr effect);

        [DllImport(Library, CallingConvention = CC)]
        public static extern void iplDirectEffectReset(IntPtr effect);

        [DllImport(Library, CallingConvention = CC)]
        public static extern IPLAudioEffectState iplDirectEffectApply(
            IntPtr effect,
            ref IPLDirectEffectParams parameters,
            ref IPLAudioBuffer inBuffer,
            ref IPLAudioBuffer outBuffer);
    }
}
