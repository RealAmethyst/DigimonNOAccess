using UnityEngine;

namespace DigimonNOAccess
{
    /// <summary>
    /// Generic base class for accessibility handlers that follow the standard
    /// panel lifecycle: detect open/close via FindObjectOfType, track state changes,
    /// and announce via ScreenReader.
    /// </summary>
    public abstract class HandlerBase<TPanel> : IAccessibilityHandler where TPanel : Object
    {
        protected TPanel _panel;
        protected bool _wasActive;
        protected int _lastCursor = -1;

        /// <summary>Log tag prefix for debug messages, e.g. "[RestaurantPanel]"</summary>
        protected abstract string LogTag { get; }

        /// <summary>Priority for AnnounceCurrentStatus ordering. Lower = checked first.</summary>
        public abstract int Priority { get; }

        /// <summary>
        /// Per-frame update implementing the standard lifecycle.
        /// Detects open/close transitions and delegates to virtual methods.
        /// </summary>
        public void Update()
        {
            bool isActive = IsOpen();
            if (isActive && !_wasActive)
                OnOpen();
            else if (!isActive && _wasActive)
                OnClose();
            else if (isActive)
                OnUpdate();
            _wasActive = isActive;
        }

        /// <summary>
        /// Checks if this handler's panel is currently visible.
        /// Default: FindObjectOfType + null check + gameObject.activeInHierarchy.
        /// Override for custom detection logic.
        /// </summary>
        public virtual bool IsOpen()
        {
            try
            {
                if (_panel == null)
                    _panel = Object.FindObjectOfType<TPanel>();
                if (_panel == null)
                    return false;
                // Try to check if it's a Component with a gameObject
                if (_panel is Component comp)
                    return comp.gameObject != null && comp.gameObject.activeInHierarchy;
                return _panel != null;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Called when panel is first detected as open.</summary>
        protected virtual void OnOpen()
        {
            DebugLogger.Log($"{LogTag} Panel opened");
        }

        /// <summary>Called when panel is detected as closed.</summary>
        protected virtual void OnClose()
        {
            _panel = null;
            _lastCursor = -1;
            _wasActive = false;
            DebugLogger.Log($"{LogTag} Panel closed");
        }

        /// <summary>Called each frame while panel is open (after initial OnOpen).</summary>
        protected virtual void OnUpdate() { }

        /// <summary>Announces current state for status hotkey. Must be implemented by subclass.</summary>
        public abstract void AnnounceStatus();
    }
}
