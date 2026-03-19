using System;
using System.Collections.Concurrent;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;
using NppLspPlugin.Util;

namespace NppLspPlugin.Features
{
    internal class Diagnostics
    {
        private readonly LspClient _client;

        // Store diagnostics per URI for re-application on buffer switch
        private readonly ConcurrentDictionary<string, PublishDiagnosticsParams> _diagnosticsCache = new();

        // Indicator numbers (safe plugin range)
        private const int IndicatorError = 9;
        private const int IndicatorWarning = 10;
        private const int IndicatorInfo = 11;

        // BGR colors
        private const int ColorError = 0x0000FF;     // Red
        private const int ColorWarning = 0x00BFFF;   // Orange/Yellow
        private const int ColorInfo = 0xFF8000;       // Blue

        public Diagnostics(LspClient client)
        {
            _client = client;
        }

        public void OnDiagnosticsReceived(PublishDiagnosticsParams diagnostics)
        {
            _diagnosticsCache[diagnostics.Uri] = diagnostics;

            // Apply if this is the current file
            var currentPath = PluginBase.GetCurrentFilePath();
            var currentUri = UriConverter.PathToUri(currentPath);
            if (string.Equals(diagnostics.Uri, currentUri, StringComparison.OrdinalIgnoreCase))
            {
                ApplyDiagnostics(diagnostics);
            }
        }

        public void ReapplyForCurrentFile()
        {
            var currentPath = PluginBase.GetCurrentFilePath();
            var currentUri = UriConverter.PathToUri(currentPath);

            if (_diagnosticsCache.TryGetValue(currentUri, out var diagnostics))
            {
                ApplyDiagnostics(diagnostics);
            }
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
                if (length <= 0) length = 1; // Minimum 1 char indicator

                int indicator = diag.Severity switch
                {
                    DiagnosticSeverity.Error => IndicatorError,
                    DiagnosticSeverity.Warning => IndicatorWarning,
                    _ => IndicatorInfo
                };

                // Apply indicator
                Sci.SendMessage(sci, (uint)SciMsg.SCI_SETINDICATORCURRENT, indicator, 0);
                Sci.SendMessage(sci, (uint)SciMsg.SCI_INDICATORFILLRANGE, startPos, length);

                // Add annotation on the line
                int line = (int)Sci.SendMessage(sci, (uint)SciMsg.SCI_LINEFROMPOSITION, startPos, 0);
                var prefix = diag.Severity switch
                {
                    DiagnosticSeverity.Error => "Error: ",
                    DiagnosticSeverity.Warning => "Warning: ",
                    _ => "Info: "
                };
                Sci.SendMessage(sci, (uint)SciMsg.SCI_ANNOTATIONSETTEXT, line, prefix + diag.Message);
            }

            // Show annotations
            Sci.SendMessage(sci, (uint)SciMsg.SCI_ANNOTATIONSETVISIBLE, 2, 0); // ANNOTATION_BOXED

            Logger.Log($"Applied {diagnostics.Diagnostics.Length} diagnostics for {diagnostics.Uri}");
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
