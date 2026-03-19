# Plan: Notepad++ LSP Client Plugin (.NET 8 NativeAOT)

A Notepad++ plugin that acts as a generic Language Server Protocol client, bringing IDE-like features (autocomplete, diagnostics, go-to-definition, hover, signature help) to any language that has an LSP server.

## Prior Art

- **NppLspClient** (Rust, alpha) by Ekopalypse -- the only actively maintained LSP client for Notepad++. Supports completions, signature help, diagnostics. Written in Rust.
- **NppLSP** (C++, experimental/inactive) by dail8859 -- early proof of concept.
- **Notepad++ itself** has no built-in LSP support (GitHub issue #4440).

A .NET 8 NativeAOT implementation would be the first C#-based LSP client for Notepad++. The NativeAOT approach produces a single native DLL identical in deployment to a C++ plugin.

---

## Architecture Overview

```
+------------------+       Win32 Messages        +------------------+
|   Notepad++      | <-------------------------> |   NppLspPlugin   |
|   + Scintilla    |  (beNotified, SendMessage)   |   (NativeAOT)    |
+------------------+                              +--------+---------+
                                                           |
                                                  stdin/stdout JSON-RPC
                                                           |
                                                  +--------v---------+
                                                  |  Language Server  |
                                                  |  (clangd, pylsp,  |
                                                  |   tsserver, etc.) |
                                                  +------------------+
```

**Three threads:**
1. **Main thread** -- Notepad++ callbacks (`beNotified`, menu commands). All Scintilla/Npp `SendMessage` calls must happen here.
2. **Server stdout reader thread** -- reads JSON-RPC responses/notifications from the language server, queues them for the main thread.
3. **Server stdin writer thread** -- sends JSON-RPC requests to the language server from a queue.

---

## Phase 1: Foundation (Plugin Skeleton + Server Lifecycle)

### 1.1 Project Setup
- New project: `NppLspPlugin/NppLspPlugin.csproj`
- Same structure as `PoorMansTSqlFormatterNppPlugin.Net8` (NativeAOT, `[UnmanagedCallersOnly]` exports)
- Add `nuget.config` (clear sources, nuget.org only)
- No external NuGet dependencies -- use `System.Text.Json` (built into .NET 8) with source generators for AOT-safe JSON serialization

### 1.2 Expanded Notepad++ Interop
Extend the interop definitions beyond the minimal set used by the formatter plugin:

**Additional NppMsg constants needed:**
- `NPPM_GETFULLCURRENTPATH` -- get current file path
- `NPPM_GETCURRENTLANGTYPE` -- get language type
- `NPPM_SWITCHTOFILE` -- switch to an open file
- `NPPM_GETCURRENTBUFFERID` -- get buffer ID
- `NPPM_GETFULLPATHFROMBUFFERID` -- file path from buffer ID
- `NPPM_ADDSCNMODIFIEDFLAGS` -- opt into SCN_MODIFIED flags (required for Npp v8.7.7+)
- `NPPM_GETNBOPENFILES` -- count open files

**Additional SciMsg constants needed:**
- `SCI_AUTOCSHOW`, `SCI_AUTOCCANCEL`, `SCI_AUTOCACTIVE`, `SCI_AUTOCSETSEPARATOR`, `SCI_AUTOCSETIGNORECASE`, `SCI_AUTOCSETORDER`, `SCI_AUTOCSETMAXHEIGHT`
- `SCI_CALLTIPSHOW`, `SCI_CALLTIPCANCEL`, `SCI_CALLTIPSETHLT`, `SCI_CALLTIPSETBACK`
- `SCI_INDICSETSTYLE`, `SCI_INDICSETFORE`, `SCI_INDICSETALPHA`, `SCI_SETINDICATORCURRENT`, `SCI_INDICATORFILLRANGE`, `SCI_INDICATORCLEARRANGE`
- `SCI_POSITIONFROMLINE`, `SCI_POSITIONRELATIVE`, `SCI_LINEFROMPOSITION`, `SCI_GETCOLUMN`
- `SCI_GOTOPOS`, `SCI_GOTOLINE`, `SCI_ENSUREVISIBLEENFORCEPOLICY`
- `SCI_SETMOUSEDWELLTIME`
- `SCI_ANNOTATIONSETTEXT`, `SCI_ANNOTATIONSETSTYLE`, `SCI_ANNOTATIONSETVISIBLE`, `SCI_ANNOTATIONCLEARALL`
- `SCI_GETLENGTH`, `SCI_GETLINE`, `SCI_GETLINECOUNT`, `SCI_LINELENGTH`
- `SCI_GETCODEPAGE` -- to determine encoding

**Expanded SCNotification struct:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct SCNotification
{
    public NMHDR nmhdr;
    public IntPtr position;        // SCN_MODIFIED: byte position of change
    public int ch;                 // SCN_CHARADDED: the character
    public int modifiers;
    public int modificationType;   // SCN_MODIFIED: SC_MOD_INSERTTEXT, SC_MOD_DELETETEXT, etc.
    public IntPtr text;            // SCN_MODIFIED: pointer to inserted/deleted text (UTF-8)
    public IntPtr length;          // SCN_MODIFIED: length of text
    public IntPtr linesAdded;      // SCN_MODIFIED: lines added (negative = deleted)
    public int message;
    public IntPtr wParam;
    public IntPtr lParam;
    public IntPtr line;
    public int foldLevelNow;
    public int foldLevelPrev;
    public int margin;
    public int listType;
    public int x;
    public int y;
    public int token;
    public IntPtr annotationLinesAdded;
    public int updated;            // SCN_UPDATEUI: SC_UPDATE_CONTENT, SC_UPDATE_SELECTION
}
```

**Additional notification codes to handle in `beNotified`:**
| Notification | Purpose |
|---|---|
| `NPPN_READY` (1001) | Call `NPPM_ADDSCNMODIFIEDFLAGS`, initialize LSP server |
| `NPPN_BUFFERACTIVATED` (1009) | User switched tabs -- send `didOpen` if new, re-apply diagnostics |
| `NPPN_FILEOPENED` (1005) | File opened -- send `textDocument/didOpen` |
| `NPPN_FILEBEFORECLOSE` (1017) | File closing -- send `textDocument/didClose` |
| `NPPN_FILESAVED` (1014) | File saved -- send `textDocument/didSave` |
| `NPPN_SHUTDOWN` (1004) | Send `shutdown` + `exit` to server, kill process |
| `SCN_MODIFIED` (2008) | Text changed -- send `textDocument/didChange` |
| `SCN_CHARADDED` (2001) | Character typed -- trigger completions on `.`, `(`, etc. |
| `SCN_UPDATEUI` (2007) | Cursor moved -- update current position context |
| `SCN_DWELLSTART` (2016) | Mouse hover -- trigger `textDocument/hover` |
| `SCN_DWELLEND` (2017) | Mouse moved away -- cancel hover calltip |

### 1.3 Language Server Process Manager
```
ServerManager
├── SpawnServer(string command, string[] args, string workingDir)
├── StopServer()
├── SendRequest(string method, object params) -> Task<JsonElement>
├── SendNotification(string method, object params)
├── OnNotificationReceived event  (for server->client notifications)
├── OnRequestReceived event       (for server->client requests)
```

- Spawn server as a child process with redirected stdin/stdout
- **Stdout reader thread**: reads `Content-Length: N\r\n\r\n` framing, parses JSON, dispatches to a thread-safe queue
- **Stdin writer thread**: dequeues outgoing messages, serializes to JSON, writes with `Content-Length` framing
- **Main thread processing**: In `beNotified` or via a Windows timer message, drain the incoming queue and apply results to Scintilla

### 1.4 JSON-RPC Layer
```
JsonRpc
├── SerializeRequest(id, method, params) -> byte[]
├── SerializeNotification(method, params) -> byte[]
├── Deserialize(byte[]) -> (id?, method?, params/result/error)
```

- Use `System.Text.Json` with **source generators** (`[JsonSerializable]`) for NativeAOT compatibility
- Define C# types for every LSP message used, annotated for source-generated serialization
- Handle request IDs (monotonically incrementing int), pending request tracking, timeout

### 1.5 Configuration
A JSON config file in the Notepad++ plugin config directory:
```json
{
  "servers": [
    {
      "language": "python",
      "command": "pylsp",
      "args": [],
      "rootUri": null,
      "initializationOptions": {}
    },
    {
      "language": "c",
      "command": "clangd",
      "args": ["--background-index"],
      "rootUri": null,
      "initializationOptions": {}
    }
  ]
}
```

- Map Notepad++ `LangType` enum to LSP `languageId` strings
- Auto-detect `rootUri` from file path (walk up looking for `.git`, `package.json`, etc.) or use config override
- Menu command: "Open LSP Configuration..." opens the JSON file in Notepad++

---

## Phase 2: Document Synchronization

The most critical piece. The language server must have an accurate copy of every open document.

### 2.1 Document State Tracker
```
DocumentState
├── Uri (file:///... format)
├── LanguageId
├── Version (int, incremented on each change)
├── IsOpen (whether server has been told about this doc)
```

Track per-buffer state using Notepad++ buffer IDs. Convert between:
- Windows file paths ↔ `file:///` URIs
- LSP positions (0-based line, UTF-16 character offset) ↔ Scintilla byte offsets

### 2.2 Position Conversion (Critical)

LSP uses `{line: 0-based, character: UTF-16 code units}`.
Scintilla uses byte offsets into a UTF-8 buffer.

Conversion requires:
1. `SCI_POSITIONFROMLINE(line)` → byte offset of line start
2. Read the line text, count UTF-16 code units to find the byte offset of the character
3. Reverse: `SCI_LINEFROMPOSITION(byteOffset)` → line, then count back

This is the #1 source of bugs in LSP clients. Must handle:
- ASCII (1 byte = 1 UTF-16 code unit)
- BMP characters (2-3 UTF-8 bytes = 1 UTF-16 code unit)
- Supplementary characters (4 UTF-8 bytes = 2 UTF-16 code units / a surrogate pair)

### 2.3 Document Sync Protocol
1. **`textDocument/didOpen`** -- sent on `NPPN_BUFFERACTIVATED` (first activation) or `NPPN_FILEOPENED`. Includes full document text.
2. **`textDocument/didChange`** -- sent on `SCN_MODIFIED` with `SC_MOD_INSERTTEXT` or `SC_MOD_DELETETEXT`. Two modes:
   - **Full sync** (simpler): send entire document text each time
   - **Incremental sync** (efficient): send only the changed range + new text. Requires converting `SCN_MODIFIED` position/length to LSP range.
3. **`textDocument/didSave`** -- sent on `NPPN_FILESAVED`
4. **`textDocument/didClose`** -- sent on `NPPN_FILEBEFORECLOSE`

Start with **full sync** (kind=1). Upgrade to incremental later for performance.

---

## Phase 3: Core LSP Features

### 3.1 Autocompletion
**Trigger:** `SCN_CHARADDED` when the typed character is in the server's trigger characters (reported in `initialize` response), or on explicit menu command.

**Flow:**
1. Get cursor position, convert to LSP position
2. Send `textDocument/completion` request
3. On response, extract completion labels
4. Call `SCI_AUTOCSETSEPARATOR` (use `\n` as separator since labels may contain spaces)
5. Call `SCI_AUTOCSHOW(prefixLength, joinedLabels)`
6. Scintilla handles filtering as user types
7. On `SCN_AUTOCCOMPLETED`, if the selected item has `textEdit` or `additionalTextEdits`, apply them

**Details:**
- Set `SCI_AUTOCSETIGNORECASE(1)` for case-insensitive matching
- Set `SCI_AUTOCSETORDER(1)` to let Scintilla sort alphabetically, or `0` if pre-sorted
- Handle `completionItem/resolve` for items that need more detail

### 3.2 Diagnostics (Errors, Warnings)
**Trigger:** Server sends `textDocument/publishDiagnostics` notification (unsolicited).

**Flow:**
1. Receive diagnostics array with `{range, severity, message, source}`
2. For each diagnostic, convert LSP range to Scintilla byte range
3. Use indicators 9-11 (safe plugin range):
   - Indicator 9: errors (red squiggle) -- `INDIC_SQUIGGLE`, color `0x0000FF` (BGR)
   - Indicator 10: warnings (yellow squiggle) -- `INDIC_SQUIGGLE`, color `0x00BFFF`
   - Indicator 11: info/hints (blue dots) -- `INDIC_DOTS`, color `0xFF8000`
4. Clear previous indicators for this file, then fill new ranges
5. Optionally show annotations below lines: `SCI_ANNOTATIONSETTEXT(line, message)`

**Important:** Re-apply diagnostics on `NPPN_BUFFERACTIVATED` since indicators are per-Scintilla-document.

### 3.3 Hover Information
**Trigger:** `SCN_DWELLSTART` (mouse hovers for N ms). Set dwell time with `SCI_SETMOUSEDWELLTIME(500)` (500ms).

**Flow:**
1. Get hover position from `SCNotification.position`
2. Convert to LSP position
3. Send `textDocument/hover` request
4. On response, extract markdown/plaintext content
5. Strip markdown formatting (Scintilla calltips are plain text)
6. Call `SCI_CALLTIPSHOW(position, text)`
7. On `SCN_DWELLEND`, call `SCI_CALLTIPCANCEL`

### 3.4 Go to Definition
**Trigger:** Menu command or keyboard shortcut (e.g., user assigns Ctrl+Click or F12 via Notepad++ shortcut mapper).

**Flow:**
1. Get word under cursor using `SCI_WORDSTARTPOSITION` / `SCI_WORDENDPOSITION`
2. Convert cursor position to LSP position
3. Send `textDocument/definition` request
4. On response, get target `{uri, range}`
5. Convert URI to file path
6. If different file: `NPPM_DOOPEN` to open it, then `NPPM_SWITCHTOFILE`
7. Convert target LSP position to Scintilla byte offset
8. `SCI_GOTOPOS(pos)` + `SCI_ENSUREVISIBLEENFORCEPOLICY(line)`

### 3.5 Signature Help
**Trigger:** `SCN_CHARADDED` when `(` or `,` is typed (or server-specified trigger characters).

**Flow:**
1. Send `textDocument/signatureHelp` request
2. On response, format the signature label
3. `SCI_CALLTIPSHOW(pos, label)`
4. `SCI_CALLTIPSETHLT(start, end)` to highlight the active parameter
5. Update on each `,` typed; cancel on `)` or cursor movement away

---

## Phase 4: Extended Features

### 4.1 Find References
- Send `textDocument/references`
- Display results in a **dockable panel** (register via `NPPM_DMMREGASDCKDLG`)
- Each result is clickable: `NPPM_DOOPEN` + `SCI_GOTOPOS`

### 4.2 Document Symbols / Outline
- Send `textDocument/documentSymbol`
- Display in a dockable tree panel
- Clickable navigation to each symbol

### 4.3 Code Formatting
- Send `textDocument/formatting` (whole document) or `textDocument/rangeFormatting` (selection)
- Apply the returned `TextEdit[]` to the document
- Must apply edits in reverse order (bottom-to-top) to preserve byte offsets

### 4.4 Rename Symbol
- Send `textDocument/rename`
- Apply the returned `WorkspaceEdit` (may span multiple files)
- For each file: open it, apply edits in reverse order

### 4.5 Code Actions
- Send `textDocument/codeAction` at cursor position
- Display available actions in a menu or popup
- Apply the selected action's `WorkspaceEdit`

---

## Phase 5: Polish

### 5.1 Multi-server Support
- Run different LSP servers for different languages simultaneously
- Route requests based on current document's `LangType`
- Separate process + state per server

### 5.2 Status Bar / Feedback
- Show server status (starting, ready, error) in a dockable panel or via `NPPM_SETSTATUSBAR`
- Show "loading..." indicator during pending requests
- Log server communication to a debug panel

### 5.3 Error Recovery
- Auto-restart crashed servers (with backoff)
- Handle server initialization failures gracefully
- Timeout stale requests (5-10 second default)

### 5.4 Workspace Folders
- Send `workspace/didChangeWorkspaceFolders` when user opens files from different projects
- Auto-detect workspace root per-file

---

## File Structure

```
NppLspPlugin/
├── NppLspPlugin.csproj
├── nuget.config
│
├── Plugin/                         # Notepad++ plugin infrastructure
│   ├── NativeExports.cs            # 6 required exports
│   ├── NppInterop.cs               # Structs, enums, P/Invoke
│   ├── PluginBase.cs               # FuncItems, delegates, helpers
│   └── Main.cs                     # Menu commands, beNotified dispatch
│
├── Lsp/                            # LSP protocol layer
│   ├── JsonRpc.cs                  # JSON-RPC framing + serialization
│   ├── LspTypes.cs                 # All LSP message types (Position, Range, etc.)
│   ├── LspMessages.cs              # Request/response/notification types
│   ├── LspJsonContext.cs           # System.Text.Json source generator context
│   └── LspClient.cs               # High-level: send requests, handle responses
│
├── Server/                         # Server process management
│   ├── ServerManager.cs            # Spawn, stop, restart servers
│   ├── ServerConfig.cs             # Configuration file model
│   └── LanguageMapping.cs          # Npp LangType -> LSP languageId
│
├── Features/                       # Feature implementations
│   ├── DocumentSync.cs             # didOpen/didChange/didClose
│   ├── Completion.cs               # Autocomplete
│   ├── Diagnostics.cs              # Error indicators + annotations
│   ├── Hover.cs                    # Calltip on mouse hover
│   ├── GotoDefinition.cs           # Navigate to definition
│   ├── SignatureHelp.cs            # Parameter hints
│   └── PositionConverter.cs        # LSP position <-> Scintilla byte offset
│
└── Util/
    ├── UriConverter.cs             # file:/// <-> Windows path
    ├── MainThreadQueue.cs          # Queue actions for main thread execution
    └── Logger.cs                   # Debug logging
```

---

## Implementation Order

| Step | What | Why First |
|------|------|-----------|
| 1 | Plugin skeleton + config + server spawn/shutdown | Nothing works without a running server |
| 2 | JSON-RPC layer + `initialize`/`initialized` handshake | Must handshake before any features work |
| 3 | Document sync (`didOpen`/`didChange`/`didClose`) | Server needs document content for everything |
| 4 | Position converter (LSP ↔ Scintilla) | Every feature depends on this |
| 5 | Diagnostics (`publishDiagnostics` → indicators) | Most visible payoff, server pushes these automatically |
| 6 | Autocompletion | Highest user-value interactive feature |
| 7 | Hover | Low-hanging fruit once position conversion works |
| 8 | Go to definition | High value, straightforward |
| 9 | Signature help | Builds on calltip infrastructure from hover |
| 10 | Find references, symbols, formatting, rename | Extended features, each fairly independent |

---

## NativeAOT Constraints

| Constraint | Solution |
|---|---|
| No reflection-based JSON serialization | Use `System.Text.Json` source generators (`[JsonSerializable]`) |
| No `Assembly.Load` | All code compiled into single DLL (no plugin-within-a-plugin) |
| No WinForms | Dockable panels via Notepad++ `NPPM_DMMREGASDCKDLG` + Win32 P/Invoke for custom UI, or just use calltips/indicators/annotations |
| No `System.Configuration` | Simple JSON config file with `System.Text.Json` |
| Thread safety | Queue server responses for main-thread processing via `PostMessage` to the Npp HWND with a custom `WM_USER+x` message |

---

## Testing Strategy

1. **Unit-testable core**: JSON-RPC parsing, position conversion, URI conversion can all be tested outside of Notepad++
2. **Manual integration testing**: Test with well-known LSP servers:
   - **Python**: `pylsp` or `pyright`
   - **C/C++**: `clangd`
   - **TypeScript**: `typescript-language-server`
   - **Rust**: `rust-analyzer`
   - **Go**: `gopls`
3. **Start simple**: Get it working with one server (e.g., `pylsp`) end-to-end before generalizing
