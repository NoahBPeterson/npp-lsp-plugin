using System;
using System.Text.Json;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class SignatureHelp
    {
        private readonly LspClient _client;

        private static readonly char[] DefaultTriggerChars = new[] { '(', ',' };

        public SignatureHelp(LspClient client)
        {
            _client = client;
        }

        public void OnCharAdded(int ch)
        {
            char c = (char)ch;

            // Cancel on closing paren
            if (c == ')')
            {
                var sci = PluginBase.GetCurrentScintilla();
                Sci.SendMessage(sci, (uint)SciMsg.SCI_CALLTIPCANCEL, 0, 0);
                return;
            }

            var triggerChars = _client.ServerCapabilities?.SignatureHelpProvider?.TriggerCharacters;
            bool shouldTrigger = false;

            if (triggerChars != null)
            {
                foreach (var tc in triggerChars)
                {
                    if (tc.Length > 0 && tc[0] == c)
                    {
                        shouldTrigger = true;
                        break;
                    }
                }
            }
            else
            {
                shouldTrigger = Array.IndexOf(DefaultTriggerChars, c) >= 0;
            }

            if (shouldTrigger)
            {
                RequestSignatureHelp();
            }
        }

        private void RequestSignatureHelp()
        {
            if (_client.ServerCapabilities?.SignatureHelpProvider == null) return;

            var sci = PluginBase.GetCurrentScintilla();
            var position = PositionConverter.GetCurrentLspPosition(sci);
            var filePath = PluginBase.GetCurrentFilePath();
            var uri = UriConverter.PathToUri(filePath);

            var @params = new SignatureHelpParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri },
                Position = position
            };

            _client.SendRequestAsync("textDocument/signatureHelp", @params).ContinueWith(t =>
            {
                if (t.Result == null || !t.Result.HasValue) return;

                try
                {
                    var element = t.Result.Value;
                    if (element.ValueKind == JsonValueKind.Null) return;

                    var sigHelp = JsonSerializer.Deserialize(
                        element.GetRawText(), LspJsonContext.Default.SignatureHelp);

                    if (sigHelp?.Signatures == null || sigHelp.Signatures.Length == 0) return;

                    int activeIndex = Math.Min(sigHelp.ActiveSignature, sigHelp.Signatures.Length - 1);
                    var sig = sigHelp.Signatures[activeIndex];

                    int curPos = PositionConverter.GetCurrentPos(sci);
                    Sci.SendMessage(sci, (uint)SciMsg.SCI_CALLTIPSHOW, curPos, sig.Label);

                    // Highlight active parameter
                    if (sig.Parameters != null && sigHelp.ActiveParameter < sig.Parameters.Length)
                    {
                        var param = sig.Parameters[sigHelp.ActiveParameter];
                        int start = sig.Label.IndexOf(param.Label, StringComparison.Ordinal);
                        if (start >= 0)
                        {
                            int end = start + param.Label.Length;
                            Sci.SendMessage(sci, (uint)SciMsg.SCI_CALLTIPSETHLT, start, end);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log($"SignatureHelp error: {ex.Message}");
                }
            });
        }
    }
}
