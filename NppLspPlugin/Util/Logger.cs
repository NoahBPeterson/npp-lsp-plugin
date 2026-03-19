using System;
using System.IO;
using NppLspPlugin.Plugin;

namespace NppLspPlugin.Util
{
    internal static class Logger
    {
        private static string? _logPath;
        private static readonly object _lock = new();

        public static void Init()
        {
            try
            {
                var configDir = PluginBase.GetPluginConfigDir();
                _logPath = Path.Combine(configDir, "NppLspPlugin.log");
            }
            catch
            {
                // Can't log if we can't get the config dir
            }
        }

        public static void Log(string message)
        {
            if (_logPath == null) return;

            try
            {
                lock (_lock)
                {
                    File.AppendAllText(_logPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Swallow logging errors
            }
        }
    }
}
