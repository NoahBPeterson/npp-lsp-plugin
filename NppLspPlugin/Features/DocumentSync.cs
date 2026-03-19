using System;
using System.Collections.Concurrent;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class DocumentSync
    {
        private readonly LspClient _client;
        private readonly string _languageId;
        private readonly ConcurrentDictionary<IntPtr, DocumentState> _documents = new();

        public DocumentSync(LspClient client, string languageId)
        {
            _client = client;
            _languageId = languageId;
        }

        public void OnBufferActivated()
        {
            var bufferId = PluginBase.GetCurrentBufferId();
            var filePath = PluginBase.GetCurrentFilePath();
            if (string.IsNullOrEmpty(filePath)) return;

            if (!_documents.ContainsKey(bufferId))
            {
                SendDidOpen(bufferId, filePath);
            }
        }

        public void OnFileOpened()
        {
            var bufferId = PluginBase.GetCurrentBufferId();
            var filePath = PluginBase.GetCurrentFilePath();
            if (string.IsNullOrEmpty(filePath)) return;

            if (!_documents.ContainsKey(bufferId))
            {
                SendDidOpen(bufferId, filePath);
            }
        }

        public void OnFileBeforeClose()
        {
            var bufferId = PluginBase.GetCurrentBufferId();
            if (_documents.TryRemove(bufferId, out var state))
            {
                _client.SendNotification("textDocument/didClose",
                    new DidCloseTextDocumentParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = state.Uri }
                    });
                Logger.Log($"didClose: {state.Uri}");
            }
        }

        public void OnFileSaved()
        {
            var bufferId = PluginBase.GetCurrentBufferId();
            if (_documents.TryGetValue(bufferId, out var state))
            {
                _client.SendNotification("textDocument/didSave",
                    new DidSaveTextDocumentParams
                    {
                        TextDocument = new TextDocumentIdentifier { Uri = state.Uri }
                    });
            }
        }

        public void OnTextModified()
        {
            var bufferId = PluginBase.GetCurrentBufferId();
            if (!_documents.TryGetValue(bufferId, out var state)) return;

            state.Version++;

            // Full sync: send entire document text
            var sci = PluginBase.GetCurrentScintilla();
            var text = PositionConverter.GetDocumentText(sci);

            _client.SendNotification("textDocument/didChange",
                new DidChangeTextDocumentParams
                {
                    TextDocument = new VersionedTextDocumentIdentifier
                    {
                        Uri = state.Uri,
                        Version = state.Version
                    },
                    ContentChanges = new[]
                    {
                        new TextDocumentContentChangeEvent { Text = text }
                    }
                });
        }

        public string? GetCurrentUri()
        {
            var bufferId = PluginBase.GetCurrentBufferId();
            return _documents.TryGetValue(bufferId, out var state) ? state.Uri : null;
        }

        private void SendDidOpen(IntPtr bufferId, string filePath)
        {
            var uri = UriConverter.PathToUri(filePath);
            var languageId = _languageId;
            var sci = PluginBase.GetCurrentScintilla();
            var text = PositionConverter.GetDocumentText(sci);

            var state = new DocumentState
            {
                Uri = uri,
                LanguageId = languageId,
                Version = 1
            };

            _documents[bufferId] = state;

            _client.SendNotification("textDocument/didOpen",
                new DidOpenTextDocumentParams
                {
                    TextDocument = new TextDocumentItem
                    {
                        Uri = uri,
                        LanguageId = languageId,
                        Version = 1,
                        Text = text
                    }
                });

            Logger.Log($"didOpen: {uri} ({languageId})");
        }
    }

    internal class DocumentState
    {
        public string Uri { get; set; } = "";
        public string LanguageId { get; set; } = "";
        public int Version { get; set; }
    }
}
