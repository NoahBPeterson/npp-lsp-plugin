using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using NppLspPlugin.Util;

namespace NppLspPlugin.Server
{
    internal class ServerManager
    {
        private Process? _process;
        private Thread? _readerThread;
        private readonly object _writeLock = new();
        private volatile bool _stopping;

        public string LanguageId { get; }
        public bool IsRunning => _process != null && !_process.HasExited;
        public event Action<string>? OnMessageReceived;

        public ServerManager(string languageId)
        {
            LanguageId = languageId;
        }

        public void Start(string command, string[] args, string workingDirectory)
        {
            var resolvedCommand = ResolveCommand(command);

            var startInfo = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // .cmd/.bat files need to be run through cmd.exe
            if (resolvedCommand.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
                resolvedCommand.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = "cmd.exe";
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add(resolvedCommand);
            }
            else
            {
                startInfo.FileName = resolvedCommand;
            }

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                var localPath = UriConverter.UriToPath(workingDirectory);
                if (Directory.Exists(localPath))
                    startInfo.WorkingDirectory = localPath;
            }

            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            _stopping = false;
            _process = Process.Start(startInfo);
            if (_process == null)
                throw new InvalidOperationException($"Failed to start LSP server: {command}");

            Logger.Log($"Started LSP server '{command}' (PID {_process.Id})");

            // Start stderr reader to log errors
            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                    Logger.Log($"[Server stderr] {e.Data}");
            };
            _process.BeginErrorReadLine();

            // Start stdout reader thread
            _readerThread = new Thread(ReadStdout) { IsBackground = true, Name = "LSP-Reader" };
            _readerThread.Start();
        }

        public void Send(byte[] data)
        {
            if (_process?.HasExited != false) return;

            try
            {
                lock (_writeLock)
                {
                    var stream = _process.StandardInput.BaseStream;
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error sending to server: {ex.Message}");
            }
        }

        public void Stop()
        {
            _stopping = true;

            if (_process != null && !_process.HasExited)
            {
                try
                {
                    // Send shutdown request then exit notification
                    var shutdownData = Lsp.JsonRpc.SerializeRequest(int.MaxValue, "shutdown", null);
                    Send(shutdownData);
                    var exitData = Lsp.JsonRpc.SerializeNotification("exit", null);
                    Send(exitData);

                    // Wait briefly for graceful shutdown
                    if (!_process.WaitForExit(3000))
                    {
                        _process.Kill();
                        Logger.Log("LSP server killed (did not exit gracefully)");
                    }
                    else
                    {
                        Logger.Log("LSP server shut down gracefully");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error stopping server: {ex.Message}");
                    try { _process.Kill(); } catch { }
                }
            }

            _process?.Dispose();
            _process = null;
        }

        private void ReadStdout()
        {
            try
            {
                var stream = _process!.StandardOutput.BaseStream;

                while (!_stopping && _process != null && !_process.HasExited)
                {
                    var contentLength = ReadContentLength(stream);
                    if (contentLength <= 0) break;

                    var body = new byte[contentLength];
                    int read = 0;
                    while (read < contentLength)
                    {
                        int n = stream.Read(body, read, contentLength - read);
                        if (n <= 0) break;
                        read += n;
                    }

                    if (read == contentLength)
                    {
                        var json = Encoding.UTF8.GetString(body);
                        OnMessageReceived?.Invoke(json);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_stopping)
                    Logger.Log($"Reader thread error: {ex.Message}");
            }
        }

        private static int ReadContentLength(Stream stream)
        {
            // Read headers line by line until empty line
            var headerBuilder = new StringBuilder();
            int contentLength = -1;

            while (true)
            {
                var line = ReadLine(stream);
                if (line == null) return -1;
                if (line.Length == 0) break; // Empty line = end of headers

                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = line.Substring("Content-Length:".Length).Trim();
                    int.TryParse(value, out contentLength);
                }
            }

            return contentLength;
        }

        private static string? ReadLine(Stream stream)
        {
            var sb = new StringBuilder();

            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) return null;

                if (b == '\n')
                {
                    // Strip trailing \r if present
                    if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                        sb.Length--;
                    return sb.ToString();
                }

                sb.Append((char)b);
            }
        }

        /// <summary>
        /// Resolve a command name to a full path. Handles:
        /// - Absolute paths (returned as-is)
        /// - Bare names searched on PATH (including .exe/.cmd/.bat extensions)
        /// </summary>
        private static string ResolveCommand(string command)
        {
            // Already a full/relative path with directory separators — use as-is
            if (command.Contains(Path.DirectorySeparatorChar) ||
                command.Contains(Path.AltDirectorySeparatorChar))
            {
                return command;
            }

            // Search PATH for the command, trying common executable extensions
            var pathVar = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVar)) return command;

            // Try .exe and .cmd before bare name — npm installs create extensionless
            // Unix shell scripts alongside the .cmd wrapper; the bare file isn't executable on Windows
            var extensions = new[] { ".exe", ".cmd", ".bat", "" };
            var dirs = pathVar.Split(Path.PathSeparator);

            foreach (var dir in dirs)
            {
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(dir, command + ext);
                    if (File.Exists(candidate))
                    {
                        Logger.Log($"Resolved '{command}' -> '{candidate}'");
                        return candidate;
                    }
                }
            }

            // Not found — return as-is and let Process.Start report the error
            Logger.Log($"Could not resolve '{command}' on PATH, trying as-is");
            return command;
        }
    }
}
