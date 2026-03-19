using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using NppLspPlugin.Lsp;
using NppLspPlugin.Util;

namespace NppLspPlugin.Server
{
    public class ServerConfigFile
    {
        [JsonPropertyName("servers")]
        public ServerDefinition[] Servers { get; set; } = Array.Empty<ServerDefinition>();

        /// <summary>
        /// Find a server definition matching a file path (by extension) or Npp language name.
        /// </summary>
        public ServerDefinition? FindServerForFile(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return null;

            // Normalize: ".py" -> "py"
            ext = ext.TrimStart('.');

            foreach (var s in Servers)
            {
                if (s.FileExtensions == null) continue;
                foreach (var e in s.FileExtensions)
                {
                    if (string.Equals(e.TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase))
                        return s;
                }
            }

            return null;
        }
    }

    public class ServerDefinition
    {
        [JsonPropertyName("languageId")]
        public string LanguageId { get; set; } = "";

        [JsonPropertyName("fileExtensions")]
        public string[]? FileExtensions { get; set; }

        [JsonPropertyName("command")]
        public string Command { get; set; } = "";

        [JsonPropertyName("args")]
        public string[] Args { get; set; } = Array.Empty<string>();

        [JsonPropertyName("rootUri")]
        public string? RootUri { get; set; }
    }

    internal static class ServerConfig
    {
        private const string ConfigFileName = "NppLspPlugin.json";

        public static ServerConfigFile? Load(string configDir)
        {
            var path = Path.Combine(configDir, ConfigFileName);
            if (!File.Exists(path)) return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize(json, LspJsonContext.Default.ServerConfigFile);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading config: {ex.Message}");
                return null;
            }
        }

        public static void EnsureDefaultConfig(string path)
        {
            if (File.Exists(path)) return;

            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var defaultConfig = new ServerConfigFile
            {
                Servers = new[]
                {
                    new ServerDefinition
                    {
                        LanguageId = "python",
                        FileExtensions = new[] { "py", "pyi" },
                        Command = "pylsp",
                        Args = Array.Empty<string>()
                    },
                    new ServerDefinition
                    {
                        LanguageId = "c",
                        FileExtensions = new[] { "c", "h" },
                        Command = "clangd",
                        Args = new[] { "--background-index" }
                    },
                    new ServerDefinition
                    {
                        LanguageId = "cpp",
                        FileExtensions = new[] { "cpp", "cxx", "cc", "hpp", "hxx", "hh" },
                        Command = "clangd",
                        Args = new[] { "--background-index" }
                    },
                    new ServerDefinition
                    {
                        LanguageId = "javascript",
                        FileExtensions = new[] { "js", "mjs", "cjs", "jsx" },
                        Command = "typescript-language-server",
                        Args = new[] { "--stdio" }
                    },
                    new ServerDefinition
                    {
                        LanguageId = "typescript",
                        FileExtensions = new[] { "ts", "tsx", "mts", "cts" },
                        Command = "typescript-language-server",
                        Args = new[] { "--stdio" }
                    },
                    new ServerDefinition
                    {
                        LanguageId = "rust",
                        FileExtensions = new[] { "rs" },
                        Command = "rust-analyzer",
                        Args = Array.Empty<string>()
                    },
                    new ServerDefinition
                    {
                        LanguageId = "go",
                        FileExtensions = new[] { "go" },
                        Command = "gopls",
                        Args = new[] { "serve" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(defaultConfig, LspJsonContext.Default.ServerConfigFile);
            File.WriteAllText(path, json);
        }
    }
}
