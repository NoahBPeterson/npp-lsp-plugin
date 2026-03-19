using System;
using System.Text.Json;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class GotoDefinition
    {
        private readonly LspClient _client;

        public GotoDefinition(LspClient client)
        {
            _client = client;
        }

        public void Execute()
        {
            if (_client.ServerCapabilities?.DefinitionProvider != true)
            {
                Logger.Log("Server does not support go-to-definition");
                return;
            }

            var sci = PluginBase.GetCurrentScintilla();
            var position = PositionConverter.GetCurrentLspPosition(sci);
            var filePath = PluginBase.GetCurrentFilePath();
            var uri = UriConverter.PathToUri(filePath);

            var @params = new DefinitionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri },
                Position = position
            };

            _client.SendRequestAsync("textDocument/definition", @params).ContinueWith(t =>
            {
                if (t.Result == null || !t.Result.HasValue) return;

                try
                {
                    var element = t.Result.Value;
                    Location? location = null;

                    // Response can be Location, Location[], or LocationLink[]
                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        var locations = JsonSerializer.Deserialize(
                            element.GetRawText(), LspJsonContext.Default.LocationArray);
                        if (locations != null && locations.Length > 0)
                            location = locations[0];
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        location = JsonSerializer.Deserialize(
                            element.GetRawText(), LspJsonContext.Default.Location);
                    }

                    if (location == null) return;

                    NavigateToLocation(location);
                }
                catch (Exception ex)
                {
                    Logger.Log($"GotoDefinition error: {ex.Message}");
                }
            });
        }

        private static void NavigateToLocation(Location location)
        {
            var targetPath = UriConverter.UriToPath(location.Uri);
            var currentPath = PluginBase.GetCurrentFilePath();

            // Open the file if it's different
            if (!string.Equals(targetPath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                Npp.SendMessage(PluginBase.nppData._nppHandle,
                    (uint)NppMsg.NPPM_DOOPEN, 0, targetPath);
            }

            // Navigate to the position
            var sci = PluginBase.GetCurrentScintilla();
            int pos = PositionConverter.LspToScintilla(sci, location.Range.Start);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_GOTOPOS, pos, 0);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_ENSUREVISIBLEENFORCEPOLICY,
                location.Range.Start.Line, 0);

            Logger.Log($"Navigated to {location.Uri}:{location.Range.Start.Line}:{location.Range.Start.Character}");
        }
    }
}
