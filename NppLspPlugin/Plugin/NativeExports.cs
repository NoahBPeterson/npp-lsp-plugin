using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NppLspPlugin.Plugin
{
    public static class NativeExports
    {
        [UnmanagedCallersOnly(EntryPoint = "isUnicode",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static int IsUnicode()
        {
            return 1;
        }

        [UnmanagedCallersOnly(EntryPoint = "setInfo",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static void SetInfo(NppData notepadPlusData)
        {
            PluginBase.nppData = notepadPlusData;
            Main.CommandMenuInit();
        }

        [UnmanagedCallersOnly(EntryPoint = "getFuncsArray",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe IntPtr GetFuncsArray(int* nbF)
        {
            IntPtr result = PluginBase.GetNativeFuncItemsPointer(out int count);
            *nbF = count;
            return result;
        }

        [UnmanagedCallersOnly(EntryPoint = "messageProc",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static uint MessageProc(uint message, IntPtr wParam, IntPtr lParam)
        {
            return 1;
        }

        private static IntPtr _ptrPluginName = IntPtr.Zero;

        [UnmanagedCallersOnly(EntryPoint = "getName",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static IntPtr GetName()
        {
            if (_ptrPluginName == IntPtr.Zero)
                _ptrPluginName = Marshal.StringToHGlobalUni(Main.PluginName);
            return _ptrPluginName;
        }

        [UnmanagedCallersOnly(EntryPoint = "beNotified",
            CallConvs = new[] { typeof(CallConvCdecl) })]
        public static unsafe void BeNotified(SCNotification* notifyCode)
        {
            uint code = notifyCode->nmhdr.code;

            if (code == (uint)NppMsg.NPPN_TBMODIFICATION)
            {
                PluginBase.RefreshFuncItemCmdIDs();
            }
            else if (code == (uint)NppMsg.NPPN_READY)
            {
                Main.OnNppReady();
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
            else
            {
                Main.OnNotification(notifyCode);
            }
        }
    }
}
