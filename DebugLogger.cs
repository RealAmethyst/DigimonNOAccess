using System;
using System.IO;

namespace DigimonNOAccess
{
    /// <summary>
    /// Simple file logger for debugging.
    /// Logs to Mods folder for easy access.
    /// </summary>
    public static class DebugLogger
    {
        private static string _logPath;
        private static object _lock = new object();
        private static bool _initialized = false;

        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // Log to the Mods folder where our DLL is
                string modsFolder = Path.GetDirectoryName(typeof(DebugLogger).Assembly.Location);
                _logPath = Path.Combine(modsFolder, "DigimonNOAccess_debug.log");

                // Clear old log on startup
                if (File.Exists(_logPath))
                {
                    File.Delete(_logPath);
                }

                Log("=== DigimonNOAccess Debug Log Started ===");
                Log($"Time: {DateTime.Now}");
                _initialized = true;
            }
            catch (Exception ex)
            {
                // Can't use DebugLogger.Warning here since initialization failed
                System.Console.WriteLine($"[DigimonNOAccess] Failed to initialize debug logger: {ex.Message}");
            }
        }

        public static void Log(string message)
        {
            if (!_initialized || string.IsNullOrEmpty(_logPath))
                return;

            try
            {
                lock (_lock)
                {
                    using (var writer = new StreamWriter(_logPath, true))
                    {
                        writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                    }
                }
            }
            catch
            {
                // Silently fail - don't want logging to break the mod
            }
        }

        public static void Warning(string message)
        {
            Log($"[WARN] {message}");
        }

        public static void Error(string message)
        {
            Log($"[ERROR] {message}");
        }

        public static void LogSection(string title)
        {
            Log("");
            Log($"=== {title} ===");
        }
    }
}
