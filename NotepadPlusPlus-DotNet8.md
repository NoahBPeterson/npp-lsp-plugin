# How to Build a Notepad++ Plugin with .NET 8 NativeAOT

This guide walks through creating a Notepad++ plugin using C# and .NET 8 NativeAOT, producing a single native DLL with no runtime dependency.

## Prerequisites

- .NET 8 SDK (or later)
- Visual Studio Build Tools with **"Desktop development with C++"** workload (required for NativeAOT linking)
- Notepad++ (64-bit or 32-bit -- you must target the matching architecture)

## How It Works

Notepad++ plugins are native DLLs that export a specific set of C functions. Traditionally, C# plugins used .NET Framework + `RGiesecke.DllExport` (UnmanagedExports) to expose managed methods as native exports.

With .NET 8 NativeAOT, we use:
- `[UnmanagedCallersOnly(EntryPoint = "...")]` to mark methods as native exports
- `PublishAot=true` + `NativeLib=Shared` to compile to a native shared library (.dll)
- The result is a standalone native DLL -- no .NET runtime needed at all

## Step 1: Create the Project

```bash
mkdir MyNppPlugin
cd MyNppPlugin
dotnet new classlib -n MyNppPlugin --framework net8.0
```

Edit `MyNppPlugin.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <OutputType>Library</OutputType>
    <PublishAot>true</PublishAot>
    <NativeLib>Shared</NativeLib>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <AssemblyName>MyNppPlugin</AssemblyName>
  </PropertyGroup>

</Project>
```

Key properties:
| Property | Purpose |
|----------|---------|
| `TargetFramework` | `net8.0-windows` enables Windows-specific APIs (P/Invoke, etc.) |
| `PublishAot` | Enables ahead-of-time native compilation |
| `NativeLib=Shared` | Produces a `.dll` (shared library) instead of an `.exe` |
| `AllowUnsafeBlocks` | Required for pointer parameters in `[UnmanagedCallersOnly]` methods |

## Step 2: Define the Notepad++ Interop Types

Notepad++ communicates with plugins via Win32 messages and a set of C structs. Create `NppInterop.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MyNppPlugin
{
    // Passed to setInfo() -- contains the three window handles you need
    [StructLayout(LayoutKind.Sequential)]
    public struct NppData
    {
        public IntPtr _nppHandle;
        public IntPtr _scintillaMainHandle;
        public IntPtr _scintillaSecondHandle;
    }

    // Keyboard shortcut definition (4 bytes)
    [StructLayout(LayoutKind.Sequential)]
    public struct ShortcutKey
    {
        public byte _isCtrl;
        public byte _isAlt;
        public byte _isShift;
        public byte _key;
    }

    // Menu item structure -- Notepad++ reads an array of these from getFuncsArray()
    // CRITICAL: Must use CharSet.Unicode and SizeConst=64 for the name field
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FuncItem
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string _itemName;       // Menu item display text (max 63 chars + null)
        public IntPtr _pFunc;          // Function pointer to the callback
        public int _cmdID;             // Filled in by Notepad++ after getFuncsArray()
        public int _init2Check;        // Initial check state (0 = unchecked)
        public IntPtr _pShKey;         // Pointer to ShortcutKey, or IntPtr.Zero
    }

    // Notification header -- used in beNotified()
    [StructLayout(LayoutKind.Sequential)]
    public struct SCNotification
    {
        public NMHDR nmhdr;
        // Additional fields exist but we only need the header
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMHDR
    {
        public IntPtr hwndFrom;
        public UIntPtr idFrom;
        public uint code;
    }

    // Notepad++ message constants (only include what you need)
    public enum NppMsg : uint
    {
        NPPMSG                    = 0x400 + 1000,
        NPPM_GETCURRENTSCINTILLA  = NPPMSG + 4,
        NPPM_GETPLUGINSCONFIGDIR = NPPMSG + 46,
        NPPM_DOOPEN              = NPPMSG + 77,

        // Notification codes
        NPPN_FIRST          = 1000,
        NPPN_READY          = NPPN_FIRST + 1,
        NPPN_TBMODIFICATION = NPPN_FIRST + 2,
        NPPN_SHUTDOWN       = NPPN_FIRST + 4,
    }

    // Scintilla message constants
    public enum SciMsg : uint
    {
        SCI_GETSELTEXT   = 2161,
        SCI_REPLACESEL   = 2170,
        SCI_SETTEXT      = 2181,
        SCI_GETTEXT      = 2182,
        SCI_GETCURRENTPOS = 2008,
        SCI_SETSEL       = 2160,
        SCI_GETLENGTH    = 2006,
    }

    /// <summary>
    /// Notepad++ API calls use Unicode (UTF-16) strings.
    /// </summary>
    public static class Npp
    {
        [DllImport("user32", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, out int lParam);

        [DllImport("user32", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

        [DllImport("user32", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, string lParam);

        [DllImport("user32", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        public const int MAX_PATH = 260;
        public const uint MB_OK = 0x00000000;
        public const uint MB_OKCANCEL = 0x00000001;
        public const int IDOK = 1;
    }

    /// <summary>
    /// Scintilla API calls use ANSI/UTF-8 strings.
    /// IMPORTANT: Scintilla uses ANSI marshaling, NOT Unicode.
    /// Use SendMessageA and MarshalAs(UnmanagedType.LPStr).
    /// </summary>
    public static class Sci
    {
        [DllImport("user32", EntryPoint = "SendMessageA")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32", EntryPoint = "SendMessageA")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, IntPtr lParam);

        [DllImport("user32", EntryPoint = "SendMessageA")]
        public static extern IntPtr SendMessage(
            IntPtr hWnd, uint Msg, int wParam,
            [MarshalAs(UnmanagedType.LPStr)] StringBuilder lParam);

        [DllImport("user32", EntryPoint = "SendMessageA")]
        public static extern IntPtr SendMessage(
            IntPtr hWnd, uint Msg, int wParam,
            [MarshalAs(UnmanagedType.LPStr)] string lParam);
    }
}
```

### Why Two SendMessage Classes?

This is a critical detail. Notepad++ and Scintilla use different string encodings:

- **Notepad++ messages** (`NPPM_*`) use **Unicode/UTF-16** strings (`SendMessageW`, `LPWStr`)
- **Scintilla messages** (`SCI_*`) use **ANSI/UTF-8** strings (`SendMessageA`, `LPStr`)

If you mix these up, you'll get garbled text or crashes. Using separate `Npp` and `Sci` static classes makes it impossible to accidentally use the wrong one.

## Step 3: Plugin Base Infrastructure

Create `PluginBase.cs` to manage menu items and native memory:

```csharp
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace MyNppPlugin
{
    internal delegate void NppFuncItemDelegate();

    internal static class PluginBase
    {
        internal static NppData nppData;
        internal static List<FuncItem> _funcItems = new List<FuncItem>();
        internal static IntPtr _nativeFuncItemsPtr = IntPtr.Zero;

        // IMPORTANT: Must keep delegate references alive to prevent GC collection.
        // Without this, the garbage collector will collect the delegates and
        // Notepad++ will crash when it tries to call the function pointers.
        internal static List<NppFuncItemDelegate> _delegates = new List<NppFuncItemDelegate>();

        internal static void SetCommand(int index, string commandName,
            NppFuncItemDelegate functionPointer)
        {
            _delegates.Add(functionPointer);

            FuncItem funcItem = new FuncItem();
            funcItem._itemName = commandName;
            funcItem._pFunc = Marshal.GetFunctionPointerForDelegate(functionPointer);
            funcItem._cmdID = 0;
            funcItem._init2Check = 0;
            funcItem._pShKey = IntPtr.Zero;
            _funcItems.Add(funcItem);
        }

        internal static IntPtr GetNativeFuncItemsPointer(out int count)
        {
            count = _funcItems.Count;

            if (_nativeFuncItemsPtr != IntPtr.Zero)
                Marshal.FreeHGlobal(_nativeFuncItemsPtr);

            int size = Marshal.SizeOf<FuncItem>();
            _nativeFuncItemsPtr = Marshal.AllocHGlobal(size * count);

            for (int i = 0; i < count; i++)
            {
                IntPtr itemPtr = IntPtr.Add(_nativeFuncItemsPtr, i * size);
                Marshal.StructureToPtr(_funcItems[i], itemPtr, false);
            }

            return _nativeFuncItemsPtr;
        }

        internal static void RefreshFuncItemCmdIDs()
        {
            if (_nativeFuncItemsPtr == IntPtr.Zero) return;

            int size = Marshal.SizeOf<FuncItem>();
            for (int i = 0; i < _funcItems.Count; i++)
            {
                IntPtr itemPtr = IntPtr.Add(_nativeFuncItemsPtr, i * size);
                _funcItems[i] = Marshal.PtrToStructure<FuncItem>(itemPtr);
            }
        }

        internal static IntPtr GetCurrentScintilla()
        {
            Npp.SendMessage(nppData._nppHandle,
                (uint)NppMsg.NPPM_GETCURRENTSCINTILLA, 0, out int cur);
            return cur == 0
                ? nppData._scintillaMainHandle
                : nppData._scintillaSecondHandle;
        }
    }
}
```

### Key Detail: Preventing Delegate GC

The `_delegates` list is critical. When you call `Marshal.GetFunctionPointerForDelegate`, the returned native pointer does NOT prevent the delegate from being garbage collected. If the GC collects the delegate, Notepad++ will crash when it invokes the menu command. Storing delegates in a static list keeps them alive for the process lifetime.

### AOT Compatibility

Use `Marshal.SizeOf<FuncItem>()` (generic) instead of `Marshal.SizeOf(typeof(FuncItem))` (Type-based). The Type-based overload triggers AOT warnings because it may require runtime code generation.

## Step 4: The Required Native Exports

Notepad++ expects every plugin DLL to export exactly these 6 functions. Create `NativeExports.cs`:

```csharp
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MyNppPlugin
{
    public static class NativeExports
    {
        // ---- 1. isUnicode ----
        // Must return non-zero (BOOL TRUE) for Unicode Notepad++.
        // If this export is missing or returns 0, Notepad++ will reject
        // the plugin with "This ANSI plugin is not compatible".
        [UnmanagedCallersOnly(EntryPoint = "isUnicode",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int IsUnicode()
        {
            return 1;
        }

        // ---- 2. setInfo ----
        // Called once at startup. Notepad++ passes you the three handles
        // you'll use for all future communication.
        [UnmanagedCallersOnly(EntryPoint = "setInfo",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void SetInfo(NppData notepadPlusData)
        {
            PluginBase.nppData = notepadPlusData;
            Main.CommandMenuInit();
        }

        // ---- 3. getFuncsArray ----
        // Returns a pointer to your array of FuncItem structs.
        // Notepad++ reads this to build the Plugins menu.
        [UnmanagedCallersOnly(EntryPoint = "getFuncsArray",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe IntPtr GetFuncsArray(int* nbF)
        {
            IntPtr result = PluginBase.GetNativeFuncItemsPointer(out int count);
            *nbF = count;
            return result;
        }

        // ---- 4. messageProc ----
        // Called for Windows messages. Return 1 to indicate "handled".
        // Most plugins just return 1 and ignore messages.
        [UnmanagedCallersOnly(EntryPoint = "messageProc",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static uint MessageProc(uint message, IntPtr wParam, IntPtr lParam)
        {
            return 1;
        }

        // ---- 5. getName ----
        // Returns a pointer to a null-terminated UTF-16 string with
        // your plugin's display name. Allocate once, never free.
        private static IntPtr _ptrPluginName = IntPtr.Zero;

        [UnmanagedCallersOnly(EntryPoint = "getName",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static IntPtr GetName()
        {
            if (_ptrPluginName == IntPtr.Zero)
                _ptrPluginName = Marshal.StringToHGlobalUni(Main.PluginName);
            return _ptrPluginName;
        }

        // ---- 6. beNotified ----
        // Called for Notepad++/Scintilla notifications.
        // At minimum, handle NPPN_TBMODIFICATION and NPPN_SHUTDOWN.
        [UnmanagedCallersOnly(EntryPoint = "beNotified",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe void BeNotified(SCNotification* notifyCode)
        {
            uint code = notifyCode->nmhdr.code;

            if (code == (uint)NppMsg.NPPN_TBMODIFICATION)
            {
                // Notepad++ has assigned command IDs -- refresh our copies
                PluginBase.RefreshFuncItemCmdIDs();
            }
            else if (code == (uint)NppMsg.NPPN_SHUTDOWN)
            {
                Main.PluginCleanUp();
                if (_ptrPluginName != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_ptrPluginName);
                    _ptrPluginName = IntPtr.Zero;
                }
            }
        }
    }
}
```

### Calling Convention

All 6 exports **must** use `CallConvCdecl`. Notepad++ declares its plugin function typedefs as `__cdecl`. On x64, this technically doesn't matter (there's only one calling convention), but specifying it explicitly is correct and necessary for x86 builds.

### The `unsafe` Keyword

`getFuncsArray` and `beNotified` use raw pointers (`int*`, `SCNotification*`). This is the cleanest way to match the C signatures with `[UnmanagedCallersOnly]`, which requires all parameter types to be [blittable](https://learn.microsoft.com/en-us/dotnet/framework/interop/blittable-and-non-blittable-types) (directly representable in native memory without marshaling).

## Step 5: Your Plugin Logic

Create `Main.cs`:

```csharp
using System;
using System.Text;

namespace MyNppPlugin
{
    internal static class Main
    {
        internal const string PluginName = "My Plugin";

        internal static void CommandMenuInit()
        {
            PluginBase.SetCommand(0, "My Command", MyCommand);
        }

        internal static void PluginCleanUp()
        {
            // Free any resources here
        }

        internal static void MyCommand()
        {
            // Example: get selected text from Scintilla and show it
            IntPtr scintilla = PluginBase.GetCurrentScintilla();

            int selLen = (int)Sci.SendMessage(
                scintilla, (uint)SciMsg.SCI_GETSELTEXT, 0, 0);

            if (selLen > 1)
            {
                StringBuilder buf = new StringBuilder(selLen);
                Sci.SendMessage(
                    scintilla, (uint)SciMsg.SCI_GETSELTEXT, 0, buf);

                Npp.MessageBox(PluginBase.nppData._nppHandle,
                    $"Selected text:\n{buf}",
                    PluginName, Npp.MB_OK);
            }
            else
            {
                Npp.MessageBox(PluginBase.nppData._nppHandle,
                    "No text selected.",
                    PluginName, Npp.MB_OK);
            }
        }
    }
}
```

## Step 6: Build

```powershell
# For 64-bit Notepad++ (most common):
dotnet publish -c Release -r win-x64

# For 32-bit Notepad++:
dotnet publish -c Release -r win-x86
```

The output is a single native DLL at:
```
bin\Release\net8.0-windows\win-x64\native\MyNppPlugin.dll
```

**IMPORTANT:** Copy from the `native\` subdirectory, NOT the parent. The parent contains a managed assembly that Notepad++ cannot load.

## Step 7: Install

```powershell
# Create plugin directory (run as Administrator)
mkdir "C:\Program Files\Notepad++\plugins\MyNppPlugin"

# Copy the native DLL
copy bin\Release\net8.0-windows\win-x64\native\MyNppPlugin.dll ^
     "C:\Program Files\Notepad++\plugins\MyNppPlugin\"
```

Restart Notepad++. Your plugin appears under **Plugins > My Plugin**.

## Common Pitfalls

### 1. "This ANSI plugin is not compatible"
- You copied the managed DLL instead of the native one. Use the file from `native\`.
- Or your `isUnicode` export is missing / returns 0.

### 2. Plugin doesn't appear in menu at all
- The DLL name must match the folder name: `plugins\MyNppPlugin\MyNppPlugin.dll`
- Check architecture: 64-bit Notepad++ needs `win-x64`, 32-bit needs `win-x86`

### 3. Notepad++ crashes when clicking a menu item
- Your delegate was garbage collected. Make sure all delegates passed to `Marshal.GetFunctionPointerForDelegate` are stored in a static list/field.

### 4. Text is garbled / wrong characters
- You used `Npp.SendMessage` (Unicode) for a Scintilla message, or `Sci.SendMessage` (ANSI) for a Notepad++ message. Keep them separate.

### 5. Build fails with linker errors
- Install the "Desktop development with C++" workload in Visual Studio / Build Tools. NativeAOT requires the MSVC linker.

### 6. NuGet restore fails with auth errors
- Your machine has a nuget.config with private feeds. Add a local `nuget.config` to your project directory:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

## NativeAOT Limitations to Be Aware Of

| Feature | Status |
|---------|--------|
| P/Invoke, `[DllImport]` | Works |
| `Marshal.*` APIs | Works |
| `[UnmanagedCallersOnly]` exports | Works |
| Basic reflection (`typeof`, `is`) | Works |
| WinForms | Not supported in .NET 8 NativeAOT (experimental in .NET 9+) |
| `Assembly.Load*` / plugin systems | Not supported |
| `System.Configuration.ApplicationSettingsBase` | Not supported |
| Unrestricted `System.Reflection.Emit` | Not supported |

For settings/configuration, use simple XML or JSON files with `System.Xml` or manual parsing. For UI dialogs, use Win32 API via P/Invoke, or open config files in Notepad++ itself.

## Useful Scintilla Messages

| Message | Description |
|---------|-------------|
| `SCI_GETSELTEXT(0, buffer)` | Get selected text into buffer. Call with `0` for lParam first to get length. |
| `SCI_REPLACESEL(0, text)` | Replace selection with text |
| `SCI_GETTEXT(length, buffer)` | Get all document text |
| `SCI_SETTEXT(0, text)` | Replace entire document |
| `SCI_GETCURRENTPOS(0, 0)` | Get cursor position (byte offset) |
| `SCI_SETSEL(anchor, caret)` | Set selection / cursor position |
| `SCI_GETLENGTH(0, 0)` | Get document length in bytes |
| `SCI_GOTOPOS(pos, 0)` | Move cursor to position |
| `SCI_GETLINE(line, buffer)` | Get text of a specific line |
| `SCI_GETLINECOUNT(0, 0)` | Get number of lines |
| `SCI_INDICSETSTYLE(indicator, style)` | Set indicator style (for underlines, etc.) |
| `SCI_INDICSETFORE(indicator, color)` | Set indicator color |
| `SCI_AUTOCSHOW(lenEntered, list)` | Show autocomplete list |
| `SCI_CALLTIPSHOW(pos, text)` | Show a calltip/tooltip |

Full Scintilla documentation: https://www.scintilla.org/ScintillaDoc.html

## Useful Notepad++ Messages

| Message | Description |
|---------|-------------|
| `NPPM_GETCURRENTSCINTILLA` | Get which Scintilla view is active (0 = main, 1 = second) |
| `NPPM_GETPLUGINSCONFIGDIR` | Get the plugin config directory path |
| `NPPM_DOOPEN(0, filepath)` | Open a file in Notepad++ |
| `NPPM_GETCURRENTLANGTYPE` | Get the language type of current document |
| `NPPM_GETFULLCURRENTPATH` | Get full path of current file |
| `NPPM_MENUCOMMAND` | Execute a Notepad++ menu command by ID |

## Reference Implementation

See `PoorMansTSqlFormatterNppPlugin.Net8` in this repository for a complete, working example that formats SQL code.
