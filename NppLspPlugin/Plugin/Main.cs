using System;
using System.IO;
using NppLspPlugin.Features;
using NppLspPlugin.Lsp;
using NppLspPlugin.Server;
using NppLspPlugin.Util;

namespace NppLspPlugin.Plugin
{
    internal static class Main
    {
        internal const string PluginName = "LSP Client";

        private static ServerManager? _serverManager;
        private static LspClient? _lspClient;
        private static DocumentSync? _documentSync;
        private static Completion? _completion;
        private static Diagnostics? _diagnostics;
        private static Features.Hover? _hover;
        private static GotoDefinition? _gotoDefinition;
        private static Features.SignatureHelp? _signatureHelp;
        private static bool _isReady;

        internal static void CommandMenuInit()
        {
            PluginBase.SetCommand(0, "Open LSP Configuration...", OpenConfig);
            PluginBase.SetCommand(1, "Go to Definition", GoToDefinitionCommand);
            PluginBase.SetCommand(2, "Trigger Completion", TriggerCompletionCommand);
            PluginBase.SetCommand(3, "Restart LSP Server", RestartServer);
            PluginBase.SetCommand(4, "About", ShowAbout);
        }

        internal static void OnNppReady()
        {
            _isReady = true;
            Logger.Init();
            Logger.Log("NppLspPlugin ready");

            // Set mouse dwell time for hover support
            var sci = PluginBase.GetCurrentScintilla();
            Sci.SendMessage(sci, (uint)SciMsg.SCI_SETMOUSEDWELLTIME, 500, 0);

            // Try to start server for current document
            TryStartServerForCurrentFile();
        }

        internal static unsafe void OnNotification(SCNotification* notification)
        {
            if (!_isReady) return;

            uint code = notification->nmhdr.code;

            switch (code)
            {
                case (uint)NppMsg.NPPN_BUFFERACTIVATED:
                    OnBufferActivated();
                    break;

                case (uint)NppMsg.NPPN_FILEOPENED:
                    _documentSync?.OnFileOpened();
                    break;

                case (uint)NppMsg.NPPN_FILEBEFORECLOSE:
                    _documentSync?.OnFileBeforeClose();
                    break;

                case (uint)NppMsg.NPPN_FILESAVED:
                    _documentSync?.OnFileSaved();
                    break;

                case SciNotification.SCN_MODIFIED:
                    if (_documentSync != null)
                    {
                        int modType = notification->modificationType;
                        if ((modType & ScModification.SC_MOD_INSERTTEXT) != 0 ||
                            (modType & ScModification.SC_MOD_DELETETEXT) != 0)
                        {
                            _documentSync.OnTextModified();
                        }
                    }
                    break;

                case SciNotification.SCN_CHARADDED:
                    int ch = notification->ch;
                    _completion?.OnCharAdded(ch);
                    _signatureHelp?.OnCharAdded(ch);
                    break;

                case SciNotification.SCN_DWELLSTART:
                    if (notification->position != IntPtr.Zero)
                    {
                        _hover?.OnDwellStart((int)notification->position);
                    }
                    break;

                case SciNotification.SCN_DWELLEND:
                    _hover?.OnDwellEnd();
                    break;
            }
        }

        private static void OnBufferActivated()
        {
            TryStartServerForCurrentFile();
            _documentSync?.OnBufferActivated();
            _diagnostics?.ReapplyForCurrentFile();
        }

        private static void TryStartServerForCurrentFile()
        {
            var configDir = PluginBase.GetPluginConfigDir();

            // Auto-create default config if it doesn't exist
            var configPath = Path.Combine(configDir, "NppLspPlugin.json");
            ServerConfig.EnsureDefaultConfig(configPath);

            var config = ServerConfig.Load(configDir);
            if (config == null)
            {
                Logger.Log("No config file found or failed to parse");
                return;
            }

            var filePath = PluginBase.GetCurrentFilePath();
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Log("No current file path");
                return;
            }

            var serverDef = config.FindServerForFile(filePath);
            if (serverDef == null)
            {
                var ext = Path.GetExtension(filePath);
                Logger.Log($"No server configured for extension '{ext}' (file: {filePath})");
                return;
            }

            var languageId = serverDef.LanguageId;

            // If we already have a running server for this language, skip
            if (_serverManager != null && _serverManager.IsRunning && _serverManager.LanguageId == languageId)
                return;

            // Stop any existing server
            _serverManager?.Stop();

            var rootUri = WorkspaceDetector.DetectRoot(filePath);

            _serverManager = new ServerManager(languageId);
            _lspClient = new LspClient(_serverManager);

            _documentSync = new DocumentSync(_lspClient, languageId);
            _completion = new Completion(_lspClient);
            _diagnostics = new Diagnostics(_lspClient);
            _hover = new Features.Hover(_lspClient);
            _gotoDefinition = new GotoDefinition(_lspClient);
            _signatureHelp = new Features.SignatureHelp(_lspClient);

            // Subscribe to server notifications
            _lspClient.OnDiagnosticsReceived += _diagnostics.OnDiagnosticsReceived;

            Logger.Log($"Starting LSP server: command='{serverDef.Command}', args=[{string.Join(", ", serverDef.Args)}], languageId='{languageId}', rootUri='{rootUri}'");

            try
            {
                _serverManager.Start(serverDef.Command, serverDef.Args, rootUri);
                _lspClient.InitializeAsync(rootUri).ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        Logger.Log($"LSP initialize failed: {t.Exception?.Message}");
                        return;
                    }
                    _documentSync?.OnBufferActivated();
                    Logger.Log("LSP server initialized successfully");
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed to start LSP server: {ex.Message}");
            }
        }

        internal static void PluginCleanUp()
        {
            _serverManager?.Stop();
            Logger.Log("NppLspPlugin shutdown");
        }

        // Menu commands

        private static void OpenConfig()
        {
            var configDir = PluginBase.GetPluginConfigDir();
            var configPath = Path.Combine(configDir, "NppLspPlugin.json");
            ServerConfig.EnsureDefaultConfig(configPath);
            Npp.SendMessage(PluginBase.nppData._nppHandle,
                (uint)NppMsg.NPPM_DOOPEN, 0, configPath);
        }

        private static void GoToDefinitionCommand()
        {
            _gotoDefinition?.Execute();
        }

        private static void TriggerCompletionCommand()
        {
            _completion?.TriggerManual();
        }

        private static void RestartServer()
        {
            _serverManager?.Stop();
            _serverManager = null;
            _lspClient = null;
            TryStartServerForCurrentFile();
        }

        private static void ShowAbout()
        {
            Npp.MessageBox(PluginBase.nppData._nppHandle,
                "NppLspPlugin - LSP Client for Notepad++\n\n" +
                "Provides IDE features via the Language Server Protocol:\n" +
                "- Autocomplete\n" +
                "- Diagnostics (errors/warnings)\n" +
                "- Go to Definition\n" +
                "- Hover information\n" +
                "- Signature Help\n\n" +
                "Built with .NET 8 NativeAOT",
                PluginName, Npp.MB_OK);
        }
    }
}
