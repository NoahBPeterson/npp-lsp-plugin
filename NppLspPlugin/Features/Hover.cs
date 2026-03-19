using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class Hover
    {
        private readonly LspClient _client;

        public Hover(LspClient client)
        {
            _client = client;
        }

        public void OnDwellStart(int bytePosition)
        {
            if (_client.ServerCapabilities?.HoverProvider != true) return;

            var sci = PluginBase.GetCurrentScintilla();
            var position = PositionConverter.ScintillaToLsp(sci, bytePosition);
            var filePath = PluginBase.GetCurrentFilePath();
            var uri = UriConverter.PathToUri(filePath);

            var @params = new HoverParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri },
                Position = position
            };

            _client.SendRequestAsync("textDocument/hover", @params).ContinueWith(t =>
            {
                if (t.Result == null || !t.Result.HasValue) return;

                try
                {
                    var element = t.Result.Value;
                    if (element.ValueKind == JsonValueKind.Null) return;

                    string? text = null;

                    // The hover result can have "contents" as MarkupContent, string, or MarkedString
                    if (element.TryGetProperty("contents", out var contents))
                    {
                        if (contents.ValueKind == JsonValueKind.String)
                        {
                            text = contents.GetString();
                        }
                        else if (contents.ValueKind == JsonValueKind.Object)
                        {
                            if (contents.TryGetProperty("value", out var val))
                            {
                                text = val.GetString();
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        // Strip markdown formatting for plain text calltip
                        text = StripMarkdown(text);

                        // Truncate — calltips can't scroll
                        if (text.Length > 250)
                            text = text.Substring(0, 250) + "...";

                        Sci.SendMessage(sci, (uint)SciMsg.SCI_CALLTIPSHOW, bytePosition, text);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"Hover error: {ex.Message}");
                }
            });
        }

        public void OnDwellEnd()
        {
            var sci = PluginBase.GetCurrentScintilla();
            Sci.SendMessage(sci, (uint)SciMsg.SCI_CALLTIPCANCEL, 0, 0);
        }

        private static string StripMarkdown(string text)
        {
            // Remove code fences
            text = Regex.Replace(text, @"```\w*\n?", "");
            // Remove inline code backticks
            text = text.Replace("`", "");
            // Remove bold/italic markers
            text = Regex.Replace(text, @"\*+", "");
            // Trim excessive whitespace
            text = text.Trim();
            return text;
        }
    }
}
