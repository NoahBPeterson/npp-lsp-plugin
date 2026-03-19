using System;
using System.Linq;
using System.Text.Json;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class Completion
    {
        private readonly LspClient _client;

        // Default trigger characters; overridden by server capabilities
        private static readonly char[] DefaultTriggerChars = new[] { '.' };

        public Completion(LspClient client)
        {
            _client = client;
        }

        public void OnCharAdded(int ch)
        {
            char c = (char)ch;

            var triggerChars = _client.ServerCapabilities?.CompletionProvider?.TriggerCharacters;
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
                RequestCompletion();
            }
        }

        public void TriggerManual()
        {
            RequestCompletion();
        }

        private void RequestCompletion()
        {
            var sci = PluginBase.GetCurrentScintilla();
            var position = PositionConverter.GetCurrentLspPosition(sci);
            var filePath = PluginBase.GetCurrentFilePath();
            var uri = UriConverter.PathToUri(filePath);

            var @params = new CompletionParams
            {
                TextDocument = new TextDocumentIdentifier { Uri = uri },
                Position = position
            };

            _client.SendRequestAsync("textDocument/completion", @params).ContinueWith(t =>
            {
                if (t.Result == null || !t.Result.HasValue) return;

                try
                {
                    var element = t.Result.Value;
                    CompletionItem[]? items = null;

                    // Response can be CompletionItem[] or CompletionList
                    if (element.ValueKind == JsonValueKind.Array)
                    {
                        items = JsonSerializer.Deserialize(
                            element.GetRawText(), LspJsonContext.Default.CompletionItemArray);
                    }
                    else if (element.ValueKind == JsonValueKind.Object)
                    {
                        var list = JsonSerializer.Deserialize(
                            element.GetRawText(), LspJsonContext.Default.CompletionList);
                        items = list?.Items;
                    }

                    if (items == null || items.Length == 0) return;

                    ShowCompletions(sci, items);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Completion error: {ex.Message}");
                }
            });
        }

        private static void ShowCompletions(IntPtr sci, CompletionItem[] items)
        {
            // Use newline separator since labels may contain spaces
            Sci.SendMessage(sci, (uint)SciMsg.SCI_AUTOCSETSEPARATOR, '\n', 0);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_AUTOCSETIGNORECASE, 1, 0);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_AUTOCSETMAXHEIGHT, 15, 0);

            var labels = string.Join("\n", items.Select(i => i.InsertText ?? i.Label));

            // Calculate prefix length (chars typed since trigger)
            int curPos = (int)Sci.SendMessage(sci, (uint)SciMsg.SCI_GETCURRENTPOS, 0, 0);
            int wordStart = (int)Sci.SendMessage(sci, (uint)SciMsg.SCI_WORDSTARTPOSITION, curPos, 1);
            int prefixLen = curPos - wordStart;

            Sci.SendMessage(sci, (uint)SciMsg.SCI_AUTOCSHOW, prefixLen, labels);
        }
    }
}
