using System;
using Il2Cpp;
using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Single source of truth for the listener orientation used by all spatial audio.
    ///
    /// Everything the player hears positionally - navigation sounds, walls, the
    /// pathfinding beacon - must be oriented the same way, or turning the camera
    /// moves some sounds and not others. That orientation is the game camera, not
    /// the player: CameraManager.Ref.m_mainCameraObject is the object that carries
    /// the game's own AudioListener, so matching it keeps our audio consistent with
    /// the game's.
    ///
    /// The vectors are returned unprojected (pitch and roll included) because HRTF
    /// needs the real orientation; callers that only want a compass bearing project
    /// onto the horizontal plane themselves.
    /// </summary>
    public static class CameraOrientation
    {
        private static int _lastLoggedCameraMode = -1;

        /// <summary>
        /// Gets the camera's forward and up vectors. Falls back to Camera.main and
        /// then to world axes, so callers always receive a usable orientation.
        /// </summary>
        public static void GetVectors(out Vector3 camForward, out Vector3 camUp)
        {
            camForward = Vector3.forward;
            camUp = Vector3.up;

            try
            {
                var camMgr = CameraManager.Ref;
                if (camMgr != null && camMgr.m_mainCameraObject != null)
                {
                    var t = camMgr.m_mainCameraObject.transform;
                    camForward = t.forward;
                    camUp = t.up;

                    // Log camera mode changes for diagnostics
                    int currentMode = (int)camMgr.modeID;
                    if (currentMode != _lastLoggedCameraMode)
                    {
                        _lastLoggedCameraMode = currentMode;
                        DebugLogger.Log($"[CameraOrientation] Camera mode: {camMgr.modeID}, fwd: ({camForward.x:F2}, {camForward.y:F2}, {camForward.z:F2}), up: ({camUp.x:F2}, {camUp.y:F2}, {camUp.z:F2})");
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CameraOrientation] CameraManager read failed: {ex.Message}");
            }

            try
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    camForward = cam.transform.forward;
                    camUp = cam.transform.up;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[CameraOrientation] Camera.main fallback failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Projects a world-space direction into the camera's local axes.
        /// Right is cross(up, forward) in Unity's left-handed system.
        /// </summary>
        public static void ToCameraLocal(
            float dirX, float dirY, float dirZ,
            float camFwdX, float camFwdY, float camFwdZ,
            float camUpX, float camUpY, float camUpZ,
            out float localRight, out float localUp, out float localForward)
        {
            float crX = camUpY * camFwdZ - camUpZ * camFwdY;
            float crY = camUpZ * camFwdX - camUpX * camFwdZ;
            float crZ = camUpX * camFwdY - camUpY * camFwdX;

            localRight   = dirX * crX + dirY * crY + dirZ * crZ;
            localUp      = dirX * camUpX + dirY * camUpY + dirZ * camUpZ;
            localForward = dirX * camFwdX + dirY * camFwdY + dirZ * camFwdZ;
        }
    }
}
