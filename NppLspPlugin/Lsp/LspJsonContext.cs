using System.Text.Json;
using System.Text.Json.Serialization;
using NppLspPlugin.Server;

namespace NppLspPlugin.Lsp
{
    [JsonSerializable(typeof(JsonRpcRequest))]
    [JsonSerializable(typeof(JsonRpcNotification))]
    [JsonSerializable(typeof(JsonRpcResponse))]
    [JsonSerializable(typeof(InitializeParams))]
    [JsonSerializable(typeof(InitializeResult))]
    [JsonSerializable(typeof(ClientCapabilities))]
    [JsonSerializable(typeof(TextDocumentClientCapabilities))]
    [JsonSerializable(typeof(ServerCapabilities))]
    [JsonSerializable(typeof(CompletionOptions))]
    [JsonSerializable(typeof(SignatureHelpOptions))]
    [JsonSerializable(typeof(DidOpenTextDocumentParams))]
    [JsonSerializable(typeof(DidChangeTextDocumentParams))]
    [JsonSerializable(typeof(DidCloseTextDocumentParams))]
    [JsonSerializable(typeof(DidSaveTextDocumentParams))]
    [JsonSerializable(typeof(CompletionParams))]
    [JsonSerializable(typeof(HoverParams))]
    [JsonSerializable(typeof(DefinitionParams))]
    [JsonSerializable(typeof(SignatureHelpParams))]
    [JsonSerializable(typeof(TextDocumentItem))]
    [JsonSerializable(typeof(TextDocumentIdentifier))]
    [JsonSerializable(typeof(VersionedTextDocumentIdentifier))]
    [JsonSerializable(typeof(TextDocumentPositionParams))]
    [JsonSerializable(typeof(TextDocumentContentChangeEvent))]
    [JsonSerializable(typeof(Position))]
    [JsonSerializable(typeof(Range))]
    [JsonSerializable(typeof(Location))]
    [JsonSerializable(typeof(Location[]))]
    [JsonSerializable(typeof(TextEdit))]
    [JsonSerializable(typeof(CompletionItem))]
    [JsonSerializable(typeof(CompletionItem[]))]
    [JsonSerializable(typeof(CompletionList))]
    [JsonSerializable(typeof(Diagnostic))]
    [JsonSerializable(typeof(Diagnostic[]))]
    [JsonSerializable(typeof(PublishDiagnosticsParams))]
    [JsonSerializable(typeof(Hover))]
    [JsonSerializable(typeof(MarkupContent))]
    [JsonSerializable(typeof(SignatureHelp))]
    [JsonSerializable(typeof(SignatureInformation))]
    [JsonSerializable(typeof(ParameterInformation))]
    [JsonSerializable(typeof(ServerConfigFile))]
    [JsonSerializable(typeof(ServerDefinition))]
    [JsonSerializable(typeof(ServerDefinition[]))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    internal partial class LspJsonContext : JsonSerializerContext
    {
    }
}
