using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NppLspPlugin.Server;
using NppLspPlugin.Util;

namespace NppLspPlugin.Lsp
{
    internal class LspClient
    {
        private readonly ServerManager _server;
        private int _nextId;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement?>> _pendingRequests = new();
        private ServerCapabilities? _serverCapabilities;
        private volatile bool _initialized;

        public ServerCapabilities? ServerCapabilities => _serverCapabilities;
        public bool IsInitialized => _initialized;
        public event Action<PublishDiagnosticsParams>? OnDiagnosticsReceived;

        public LspClient(ServerManager server)
        {
            _server = server;
            _server.OnMessageReceived += OnMessageReceived;
        }

        public async Task InitializeAsync(string rootUri)
        {
            var initParams = new InitializeParams
            {
                ProcessId = Environment.ProcessId,
                RootUri = rootUri,
                Capabilities = new ClientCapabilities
                {
                    TextDocument = new TextDocumentClientCapabilities
                    {
                        Synchronization = new SynchronizationCapability { DidSave = true },
                        Completion = new CompletionCapability
                        {
                            CompletionItem = new CompletionItemCapability { SnippetSupport = false }
                        },
                        Hover = new HoverCapability { ContentFormat = new[] { "plaintext" } },
                        SignatureHelp = new SignatureHelpCapability(),
                        Definition = new DefinitionCapability(),
                        PublishDiagnostics = new PublishDiagnosticsCapability()
                    }
                }
            };

            var result = await SendRequestAsync("initialize", initParams);
            if (result.HasValue)
            {
                _serverCapabilities = JsonSerializer.Deserialize(
                    result.Value.GetRawText(), LspJsonContext.Default.InitializeResult)?.Capabilities;
            }

            // Send initialized notification
            SendNotification("initialized", null);
            _initialized = true;
            Logger.Log("LSP handshake complete");
        }

        public Task<JsonElement?> SendRequestAsync(string method, object? @params)
        {
            int id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<JsonElement?>();
            _pendingRequests[id] = tcs;

            var data = JsonRpc.SerializeRequest(id, method, @params);
            _server.Send(data);

            // Timeout after 10 seconds
            _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ =>
            {
                if (_pendingRequests.TryRemove(id, out var pendingTcs))
                {
                    pendingTcs.TrySetResult(null);
                }
            });

            return tcs.Task;
        }

        public void SendNotification(string method, object? @params)
        {
            var data = JsonRpc.SerializeNotification(method, @params);
            _server.Send(data);
        }

        private void OnMessageReceived(string json)
        {
            try
            {
                var response = JsonRpc.Deserialize(json);
                if (response == null) return;

                // If it has an id, it's a response to a request
                if (response.Id.HasValue)
                {
                    if (_pendingRequests.TryRemove(response.Id.Value, out var tcs))
                    {
                        if (response.Error != null)
                        {
                            Logger.Log($"LSP error [{response.Error.Code}]: {response.Error.Message}");
                            tcs.TrySetResult(null);
                        }
                        else
                        {
                            tcs.TrySetResult(response.Result);
                        }
                    }
                }
                // If it has a method, it's a notification or server request
                else if (response.Method != null)
                {
                    HandleServerNotification(response.Method, response.Params);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing message: {ex.Message}");
            }
        }

        private void HandleServerNotification(string method, JsonElement? @params)
        {
            switch (method)
            {
                case "textDocument/publishDiagnostics":
                    if (@params.HasValue)
                    {
                        var diagnostics = JsonSerializer.Deserialize(
                            @params.Value.GetRawText(), LspJsonContext.Default.PublishDiagnosticsParams);
                        if (diagnostics != null)
                        {
                            OnDiagnosticsReceived?.Invoke(diagnostics);
                        }
                    }
                    break;

                case "window/logMessage":
                case "window/showMessage":
                    if (@params.HasValue && @params.Value.TryGetProperty("message", out var msg))
                    {
                        Logger.Log($"[Server] {msg.GetString()}");
                    }
                    break;
            }
        }
    }
}
