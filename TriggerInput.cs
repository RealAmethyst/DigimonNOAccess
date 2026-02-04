using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Custom input helper for reading L2/R2 triggers directly from Unity.
    /// The game's PadManager doesn't expose triggers, so we read them ourselves.
    /// </summary>
    public static class TriggerInput
    {
        // Trigger threshold - how far the trigger must be pressed to count as "held"
        private const float TriggerThreshold = 0.3f;

        // Unity axis indices for triggers (may vary by controller/platform)
        // Xbox controller on Windows: LT = axis 9, RT = axis 10
        // These are 0-indexed in GetAxisRaw, so we try multiple options
        private static readonly string[] LeftTriggerAxes = { "Axis 9", "Axis 3", "JoystickAxis9", "JoystickAxis3" };
        private static readonly string[] RightTriggerAxes = { "Axis 10", "Axis 3", "JoystickAxis10", "JoystickAxis3" };

        // Cache which axis works (determined on first successful read)
        private static string _workingLeftAxis = null;
        private static string _workingRightAxis = null;

        /// <summary>
        /// Check if Left Trigger (L2/LT) is held down.
        /// </summary>
        public static bool IsLeftTriggerHeld()
        {
            return GetTriggerValue(true) > TriggerThreshold;
        }

        /// <summary>
        /// Check if Right Trigger (R2/RT) is held down.
        /// </summary>
        public static bool IsRightTriggerHeld()
        {
            return GetTriggerValue(false) > TriggerThreshold;
        }

        /// <summary>
        /// Get the raw trigger value (0 to 1).
        /// </summary>
        private static float GetTriggerValue(bool isLeft)
        {
            // Try cached axis first
            string cachedAxis = isLeft ? _workingLeftAxis : _workingRightAxis;
            if (cachedAxis != null)
            {
                try
                {
                    return Mathf.Abs(Input.GetAxisRaw(cachedAxis));
                }
                catch
                {
                    // Axis no longer valid, reset cache
                    if (isLeft) _workingLeftAxis = null;
                    else _workingRightAxis = null;
                }
            }

            // Try to find a working axis
            string[] axesToTry = isLeft ? LeftTriggerAxes : RightTriggerAxes;
            foreach (var axisName in axesToTry)
            {
                try
                {
                    float value = Mathf.Abs(Input.GetAxisRaw(axisName));
                    // If we got a non-zero value, cache this axis
                    if (value > 0.01f)
                    {
                        if (isLeft) _workingLeftAxis = axisName;
                        else _workingRightAxis = axisName;
                    }
                    return value;
                }
                catch
                {
                    // This axis doesn't exist, try next
                }
            }

            // Fallback: try raw joystick axis numbers
            try
            {
                // Xbox triggers on Windows are often axis 9 (LT) and 10 (RT)
                // But Unity's Input.GetAxisRaw with numbers requires proper Input Manager setup
                // Try joystick button approach as last resort
                int buttonIndex = isLeft ? 4 : 5; // Common indices for triggers as buttons
                if (Input.GetKey((KeyCode)(KeyCode.JoystickButton0 + buttonIndex)))
                {
                    return 1.0f;
                }
            }
            catch { }

            return 0f;
        }
    }
}
