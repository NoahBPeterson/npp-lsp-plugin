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

        public ServerDefinition? FindServer(string languageId)
        {
            foreach (var s in Servers)
            {
                if (string.Equals(s.Language, languageId, StringComparison.OrdinalIgnoreCase))
                    return s;
            }
            return null;
        }
    }

    public class ServerDefinition
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = "";

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
                        Language = "python",
                        Command = "pylsp",
                        Args = Array.Empty<string>()
                    },
                    new ServerDefinition
                    {
                        Language = "c",
                        Command = "clangd",
                        Args = new[] { "--background-index" }
                    },
                    new ServerDefinition
                    {
                        Language = "cpp",
                        Command = "clangd",
                        Args = new[] { "--background-index" }
                    }
                }
            };

            var json = JsonSerializer.Serialize(defaultConfig, LspJsonContext.Default.ServerConfigFile);
            File.WriteAllText(path, json);
        }
    }
}
