using System;
using System.Collections.Concurrent;
using System.IO;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class Diagnostics
    {
        private readonly LspClient _client;
        private readonly string[] _supportedExtensions;

        // Store diagnostics per file path (normalized) for re-application on buffer switch
        private readonly ConcurrentDictionary<string, PublishDiagnosticsParams> _diagnosticsCache = new();

        // Indicator numbers (safe plugin range)
        private const int IndicatorError = 9;
        private const int IndicatorWarning = 10;
        private const int IndicatorInfo = 11;

        // BGR colors
        private const int ColorError = 0x0000FF;     // Red
        private const int ColorWarning = 0x00BFFF;   // Orange/Yellow
        private const int ColorInfo = 0xFF8000;       // Blue

        public Diagnostics(LspClient client, string[] supportedExtensions)
        {
            _client = client;
            _supportedExtensions = supportedExtensions;
        }

        public void OnDiagnosticsReceived(PublishDiagnosticsParams diagnostics)
        {
            // Only process diagnostics for files we support
            var path = UriConverter.UriToPath(diagnostics.Uri);
            if (!IsSupportedFile(path))
            {
                Logger.Log($"Ignoring diagnostics for unsupported file: {diagnostics.Uri}");
                return;
            }

            // Cache by normalized path (case-insensitive key)
            var normalizedPath = path.ToUpperInvariant();
            _diagnosticsCache[normalizedPath] = diagnostics;

            // Apply if this is the current file
            var currentPath = PluginBase.GetCurrentFilePath();
            if (string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                ApplyDiagnostics(diagnostics);
            }
        }

        public void ReapplyForCurrentFile()
        {
            var currentPath = PluginBase.GetCurrentFilePath();
            if (string.IsNullOrEmpty(currentPath)) return;

            var normalizedPath = currentPath.ToUpperInvariant();
            if (_diagnosticsCache.TryGetValue(normalizedPath, out var diagnostics))
            {
                ApplyDiagnostics(diagnostics);
            }
            else
            {
                // No cached diagnostics — clear any stale indicators
                ClearAll();
            }
        }

        private bool IsSupportedFile(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            if (string.IsNullOrEmpty(ext)) return false;
            ext = ext.TrimStart('.');

            foreach (var e in _supportedExtensions)
            {
                if (string.Equals(e.TrimStart('.'), ext, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private void ClearAll()
        {
            var sci = PluginBase.GetCurrentScintilla();
            int docLength = (int)Sci.SendMessage(sci, (uint)SciMsg.SCI_GETLENGTH, 0, 0);
            ClearIndicator(sci, IndicatorError, docLength);
            ClearIndicator(sci, IndicatorWarning, docLength);
            ClearIndicator(sci, IndicatorInfo, docLength);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_ANNOTATIONCLEARALL, 0, 0);
        }

        private void ApplyDiagnostics(PublishDiagnosticsParams diagnostics)
        {
            var sci = PluginBase.GetCurrentScintilla();

            // Setup indicators
            SetupIndicator(sci, IndicatorError, IndicatorStyle.INDIC_SQUIGGLE, ColorError);
            SetupIndicator(sci, IndicatorWarning, IndicatorStyle.INDIC_SQUIGGLE, ColorWarning);
            SetupIndicator(sci, IndicatorInfo, IndicatorStyle.INDIC_DOTS, ColorInfo);

            // Clear all existing indicators
            int docLength = (int)Sci.SendMessage(sci, (uint)SciMsg.SCI_GETLENGTH, 0, 0);
            ClearIndicator(sci, IndicatorError, docLength);
            ClearIndicator(sci, IndicatorWarning, docLength);
            ClearIndicator(sci, IndicatorInfo, docLength);

            // Clear annotations
            Sci.SendMessage(sci, (uint)SciMsg.SCI_ANNOTATIONCLEARALL, 0, 0);

            foreach (var diag in diagnostics.Diagnostics)
            {
                int startPos = PositionConverter.LspToScintilla(sci, diag.Range.Start);
                int endPos = PositionConverter.LspToScintilla(sci, diag.Range.End);
                int length = endPos - startPos;
                if (length <= 0) length = 1;

                int indicator = diag.Severity switch
                {
                    DiagnosticSeverity.Error => IndicatorError,
                    DiagnosticSeverity.Warning => IndicatorWarning,
                    _ => IndicatorInfo
                };

                Sci.SendMessage(sci, (uint)SciMsg.SCI_SETINDICATORCURRENT, indicator, 0);
                Sci.SendMessage(sci, (uint)SciMsg.SCI_INDICATORFILLRANGE, startPos, length);

                int line = (int)Sci.SendMessage(sci, (uint)SciMsg.SCI_LINEFROMPOSITION, startPos, 0);
                var prefix = diag.Severity switch
                {
                    DiagnosticSeverity.Error => "Error: ",
                    DiagnosticSeverity.Warning => "Warning: ",
                    _ => "Info: "
                };
                Sci.SendMessage(sci, (uint)SciMsg.SCI_ANNOTATIONSETTEXT, line, prefix + diag.Message);
            }

            if (diagnostics.Diagnostics.Length > 0)
                Sci.SendMessage(sci, (uint)SciMsg.SCI_ANNOTATIONSETVISIBLE, 2, 0);

            var summary = diagnostics.Diagnostics.Length > 0
                ? string.Join("; ", Array.ConvertAll(diagnostics.Diagnostics,
                    d => $"L{d.Range.Start.Line + 1}: {d.Message}"))
                : "(none)";
            Logger.Log($"Applied {diagnostics.Diagnostics.Length} diagnostics for {diagnostics.Uri}: {summary}");
        }

        private static void SetupIndicator(IntPtr sci, int indicator, int style, int color)
        {
            Sci.SendMessage(sci, (uint)SciMsg.SCI_INDICSETSTYLE, indicator, style);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_INDICSETFORE, indicator, color);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_INDICSETALPHA, indicator, 100);
        }

        private static void ClearIndicator(IntPtr sci, int indicator, int docLength)
        {
            Sci.SendMessage(sci, (uint)SciMsg.SCI_SETINDICATORCURRENT, indicator, 0);
            Sci.SendMessage(sci, (uint)SciMsg.SCI_INDICATORCLEARRANGE, 0, docLength);
        }
    }
}
