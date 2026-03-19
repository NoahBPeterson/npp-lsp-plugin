using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace NppLspPlugin.Plugin
{
    internal delegate void NppFuncItemDelegate();

    internal static class PluginBase
    {
        internal static NppData nppData;
        internal static List<FuncItem> _funcItems = new List<FuncItem>();
        internal static IntPtr _nativeFuncItemsPtr = IntPtr.Zero;
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

        internal static string GetCurrentFilePath()
        {
            var sb = new StringBuilder(Npp.MAX_PATH);
            Npp.SendMessage(nppData._nppHandle,
                (uint)NppMsg.NPPM_GETFULLCURRENTPATH, Npp.MAX_PATH, sb);
            return sb.ToString();
        }

        internal static int GetCurrentLangType()
        {
            Npp.SendMessage(nppData._nppHandle,
                (uint)NppMsg.NPPM_GETCURRENTLANGTYPE, 0, out int langType);
            return langType;
        }

        internal static IntPtr GetCurrentBufferId()
        {
            return Npp.SendMessage(nppData._nppHandle,
                (uint)NppMsg.NPPM_GETCURRENTBUFFERID, IntPtr.Zero, IntPtr.Zero);
        }

        internal static string GetPluginConfigDir()
        {
            var sb = new StringBuilder(Npp.MAX_PATH);
            Npp.SendMessage(nppData._nppHandle,
                (uint)NppMsg.NPPM_GETPLUGINSCONFIGDIR, Npp.MAX_PATH, sb);
            return sb.ToString();
        }
    }
}
