using System;
using System.Text.Json.Serialization;

namespace NppLspPlugin.Lsp
{
    // Initialize

    public class InitializeParams
    {
        [JsonPropertyName("processId")]
        public int? ProcessId { get; set; }

        [JsonPropertyName("rootUri")]
        public string? RootUri { get; set; }

        [JsonPropertyName("capabilities")]
        public ClientCapabilities Capabilities { get; set; } = new();
    }

    public class ClientCapabilities
    {
        [JsonPropertyName("textDocument")]
        public TextDocumentClientCapabilities TextDocument { get; set; } = new();
    }

    public class TextDocumentClientCapabilities
    {
        [JsonPropertyName("synchronization")]
        public SynchronizationCapability Synchronization { get; set; } = new();

        [JsonPropertyName("completion")]
        public CompletionCapability Completion { get; set; } = new();

        [JsonPropertyName("hover")]
        public HoverCapability Hover { get; set; } = new();

        [JsonPropertyName("signatureHelp")]
        public SignatureHelpCapability SignatureHelp { get; set; } = new();

        [JsonPropertyName("definition")]
        public DefinitionCapability Definition { get; set; } = new();

        [JsonPropertyName("publishDiagnostics")]
        public PublishDiagnosticsCapability PublishDiagnostics { get; set; } = new();
    }

    public class SynchronizationCapability
    {
        [JsonPropertyName("didSave")]
        public bool DidSave { get; set; } = true;
    }

    public class CompletionCapability
    {
        [JsonPropertyName("completionItem")]
        public CompletionItemCapability CompletionItem { get; set; } = new();
    }

    public class CompletionItemCapability
    {
        [JsonPropertyName("snippetSupport")]
        public bool SnippetSupport { get; set; } = false;
    }

    public class HoverCapability
    {
        [JsonPropertyName("contentFormat")]
        public string[] ContentFormat { get; set; } = new[] { "plaintext" };
    }

    public class SignatureHelpCapability
    {
        [JsonPropertyName("signatureInformation")]
        public SignatureInformationCapability SignatureInformation { get; set; } = new();
    }

    public class SignatureInformationCapability
    {
        [JsonPropertyName("parameterInformation")]
        public ParameterInformationCapability ParameterInformation { get; set; } = new();
    }

    public class ParameterInformationCapability
    {
        [JsonPropertyName("labelOffsetSupport")]
        public bool LabelOffsetSupport { get; set; } = false;
    }

    public class DefinitionCapability { }

    public class PublishDiagnosticsCapability { }

    // Initialize result

    public class InitializeResult
    {
        [JsonPropertyName("capabilities")]
        public ServerCapabilities Capabilities { get; set; } = new();
    }

    public class ServerCapabilities
    {
        [JsonPropertyName("textDocumentSync")]
        public int TextDocumentSync { get; set; } = 1; // 1 = Full

        [JsonPropertyName("completionProvider")]
        public CompletionOptions? CompletionProvider { get; set; }

        [JsonPropertyName("hoverProvider")]
        public bool HoverProvider { get; set; }

        [JsonPropertyName("signatureHelpProvider")]
        public SignatureHelpOptions? SignatureHelpProvider { get; set; }

        [JsonPropertyName("definitionProvider")]
        public bool DefinitionProvider { get; set; }
    }

    public class CompletionOptions
    {
        [JsonPropertyName("triggerCharacters")]
        public string[]? TriggerCharacters { get; set; }

        [JsonPropertyName("resolveProvider")]
        public bool ResolveProvider { get; set; }
    }

    public class SignatureHelpOptions
    {
        [JsonPropertyName("triggerCharacters")]
        public string[]? TriggerCharacters { get; set; }
    }

    // didOpen

    public class DidOpenTextDocumentParams
    {
        [JsonPropertyName("textDocument")]
        public TextDocumentItem TextDocument { get; set; } = new();
    }

    // didChange

    public class DidChangeTextDocumentParams
    {
        [JsonPropertyName("textDocument")]
        public VersionedTextDocumentIdentifier TextDocument { get; set; } = new();

        [JsonPropertyName("contentChanges")]
        public TextDocumentContentChangeEvent[] ContentChanges { get; set; } = Array.Empty<TextDocumentContentChangeEvent>();
    }

    public class TextDocumentContentChangeEvent
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    // didSave

    public class DidSaveTextDocumentParams
    {
        [JsonPropertyName("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; } = new();
    }

    // didClose

    public class DidCloseTextDocumentParams
    {
        [JsonPropertyName("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; } = new();
    }

    // completion

    public class CompletionParams : TextDocumentPositionParams { }

    // hover

    public class HoverParams : TextDocumentPositionParams { }

    // definition

    public class DefinitionParams : TextDocumentPositionParams { }

    // signatureHelp

    public class SignatureHelpParams : TextDocumentPositionParams { }
}
