using System;
using System.Text.Json.Serialization;

namespace NppLspPlugin.Lsp
{
    public class Position
    {
        [JsonPropertyName("line")]
        public int Line { get; set; }

        [JsonPropertyName("character")]
        public int Character { get; set; }

        public Position() { }
        public Position(int line, int character) { Line = line; Character = character; }
    }

    public class Range
    {
        [JsonPropertyName("start")]
        public Position Start { get; set; } = new();

        [JsonPropertyName("end")]
        public Position End { get; set; } = new();
    }

    public class Location
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";

        [JsonPropertyName("range")]
        public Range Range { get; set; } = new();
    }

    public class TextDocumentIdentifier
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";
    }

    public class VersionedTextDocumentIdentifier : TextDocumentIdentifier
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }
    }

    public class TextDocumentItem
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";

        [JsonPropertyName("languageId")]
        public string LanguageId { get; set; } = "";

        [JsonPropertyName("version")]
        public int Version { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";
    }

    public class TextDocumentPositionParams
    {
        [JsonPropertyName("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; } = new();

        [JsonPropertyName("position")]
        public Position Position { get; set; } = new();
    }

    public class TextEdit
    {
        [JsonPropertyName("range")]
        public Range Range { get; set; } = new();

        [JsonPropertyName("newText")]
        public string NewText { get; set; } = "";
    }

    public class CompletionItem
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("kind")]
        public int Kind { get; set; }

        [JsonPropertyName("detail")]
        public string? Detail { get; set; }

        [JsonPropertyName("insertText")]
        public string? InsertText { get; set; }

        [JsonPropertyName("textEdit")]
        public TextEdit? TextEdit { get; set; }

        [JsonPropertyName("sortText")]
        public string? SortText { get; set; }

        [JsonPropertyName("filterText")]
        public string? FilterText { get; set; }
    }

    public class CompletionList
    {
        [JsonPropertyName("isIncomplete")]
        public bool IsIncomplete { get; set; }

        [JsonPropertyName("items")]
        public CompletionItem[] Items { get; set; } = Array.Empty<CompletionItem>();
    }

    public class Diagnostic
    {
        [JsonPropertyName("range")]
        public Range Range { get; set; } = new();

        [JsonPropertyName("severity")]
        public int Severity { get; set; } = 1;

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("source")]
        public string? Source { get; set; }
    }

    public class PublishDiagnosticsParams
    {
        [JsonPropertyName("uri")]
        public string Uri { get; set; } = "";

        [JsonPropertyName("diagnostics")]
        public Diagnostic[] Diagnostics { get; set; } = Array.Empty<Diagnostic>();
    }

    public class Hover
    {
        [JsonPropertyName("contents")]
        public MarkupContent? Contents { get; set; }
    }

    public class MarkupContent
    {
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "plaintext";

        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }

    public class SignatureHelp
    {
        [JsonPropertyName("signatures")]
        public SignatureInformation[] Signatures { get; set; } = Array.Empty<SignatureInformation>();

        [JsonPropertyName("activeSignature")]
        public int ActiveSignature { get; set; }

        [JsonPropertyName("activeParameter")]
        public int ActiveParameter { get; set; }
    }

    public class SignatureInformation
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("parameters")]
        public ParameterInformation[]? Parameters { get; set; }
    }

    public class ParameterInformation
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";
    }

    // Severity constants
    public static class DiagnosticSeverity
    {
        public const int Error = 1;
        public const int Warning = 2;
        public const int Information = 3;
        public const int Hint = 4;
    }
}
