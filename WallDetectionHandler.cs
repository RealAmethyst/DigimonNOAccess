using Il2Cpp;
using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using UnityEngine;
using UnityEngine.AI;

namespace DigimonNOAccess
{
    /// <summary>
    /// Detects impassable terrain using NavMesh and plays directional sounds.
    /// Always active when player is in control in the overworld.
    /// </summary>
    public class WallDetectionHandler : IDisposable
    {
        // Detection configuration
        private const float DetectionDistance = 2f;
        private const float CheckInterval = 0.3f;
        private const float NavMeshSampleRadius = 0.5f;

        // State
        private bool _initialized = false;
        private float _lastCheckTime = 0f;
        private PlayerCtrl _playerCtrl;

        // Wall states (to avoid repeating sounds)
        private bool _wallAhead = false;
        private bool _wallBehind = false;
        private bool _wallLeft = false;
        private bool _wallRight = false;

        // Sound file paths
        private string _soundsPath;

        public void Initialize()
        {
            if (_initialized) return;

            try
            {
                string modPath = Path.GetDirectoryName(typeof(WallDetectionHandler).Assembly.Location);
                _soundsPath = Path.Combine(Path.GetDirectoryName(modPath), "sounds");

                if (!Directory.Exists(_soundsPath))
                {
                    _soundsPath = Path.Combine(modPath, "sounds");
                }

                _initialized = true;
                DebugLogger.Log("[WallDetection] Initialized");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[WallDetection] Init error: {ex.Message}");
            }
        }

        public void Update()
        {
            if (!_initialized)
            {
                Initialize();
            }

            // Only run when player is in control
            if (!IsPlayerInControl())
            {
                ResetWallStates();
                return;
            }

            // Find player if needed
            if (_playerCtrl == null)
            {
                _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                if (_playerCtrl == null) return;
            }

            // Rate limit checking
            float currentTime = Time.time;
            if (currentTime - _lastCheckTime < CheckInterval) return;
            _lastCheckTime = currentTime;

            DetectWallsNavMesh();
        }

        private bool IsPlayerInControl()
        {
            try
            {
                var battlePanel = uBattlePanel.m_instance;
                if (battlePanel != null && battlePanel.m_enabled)
                {
                    return false;
                }

                if (_playerCtrl == null)
                {
                    _playerCtrl = UnityEngine.Object.FindObjectOfType<PlayerCtrl>();
                }

                if (_playerCtrl == null)
                {
                    return false;
                }

                var actionState = _playerCtrl.actionState;
                switch (actionState)
                {
                    case UnitCtrlBase.ActionState.ActionState_Event:
                    case UnitCtrlBase.ActionState.ActionState_Battle:
                    case UnitCtrlBase.ActionState.ActionState_Dead:
                    case UnitCtrlBase.ActionState.ActionState_DeadGataway:
                    case UnitCtrlBase.ActionState.ActionState_LiquidCrystallization:
                        return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DetectWallsNavMesh()
        {
            if (_playerCtrl == null) return;

            try
            {
                Vector3 playerPos = _playerCtrl.transform.position;
                Vector3 forward = _playerCtrl.transform.forward;
                Vector3 right = _playerCtrl.transform.right;

                bool wallAhead = !IsPositionWalkable(playerPos + forward * DetectionDistance);
                bool wallBehind = !IsPositionWalkable(playerPos - forward * DetectionDistance);
                bool wallLeft = !IsPositionWalkable(playerPos - right * DetectionDistance);
                bool wallRight = !IsPositionWalkable(playerPos + right * DetectionDistance);

                if (wallAhead && !_wallAhead)
                {
                    PlayWallSound("wall up.wav", 0f, 0.4f);
                }
                if (wallBehind && !_wallBehind)
                {
                    PlayWallSound("wall down.wav", 0f, 0.5f);
                }
                if (wallLeft && !_wallLeft)
                {
                    PlayWallSound("wall left.wav", -0.8f, 0.3f);
                }
                if (wallRight && !_wallRight)
                {
                    PlayWallSound("wall right.wav", 0.8f, 0.3f);
                }

                _wallAhead = wallAhead;
                _wallBehind = wallBehind;
                _wallLeft = wallLeft;
                _wallRight = wallRight;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[WallDetection] Detection error: {ex.Message}");
            }
        }

        private bool IsPositionWalkable(Vector3 position)
        {
            try
            {
                NavMeshHit hit;
                bool foundNavMesh = NavMesh.SamplePosition(position, out hit, NavMeshSampleRadius, NavMesh.AllAreas);

                if (!foundNavMesh)
                {
                    return false;
                }

                float horizontalDistance = Vector2.Distance(
                    new Vector2(position.x, position.z),
                    new Vector2(hit.position.x, hit.position.z)
                );

                return horizontalDistance < NavMeshSampleRadius;
            }
            catch
            {
                return !Physics.Raycast(
                    _playerCtrl.transform.position + Vector3.up * 0.5f,
                    (position - _playerCtrl.transform.position).normalized,
                    DetectionDistance
                );
            }
        }

        private void ResetWallStates()
        {
            _wallAhead = false;
            _wallBehind = false;
            _wallLeft = false;
            _wallRight = false;
        }

        private void PlayWallSound(string filename, float pan, float volume = 0.5f)
        {
            try
            {
                string filePath = Path.Combine(_soundsPath, filename);
                if (!File.Exists(filePath)) return;

                var audioFile = new AudioFileReader(filePath);

                ISampleProvider sampleProvider;
                if (audioFile.WaveFormat.Channels == 2)
                {
                    sampleProvider = new StereoToMonoSampleProvider(audioFile);
                }
                else
                {
                    sampleProvider = audioFile;
                }

                var panner = new PanningSampleProvider(sampleProvider)
                {
                    Pan = pan
                };

                var volumeProvider = new VolumeSampleProvider(panner)
                {
                    Volume = volume
                };

                var waveOut = new WaveOutEvent();
                waveOut.Init(volumeProvider);

                waveOut.PlaybackStopped += (sender, args) =>
                {
                    try
                    {
                        waveOut.Dispose();
                        audioFile.Dispose();
                    }
                    catch { }
                };

                waveOut.Play();
            }
            catch { }
        }

        public void Dispose()
        {
        }
    }
}
