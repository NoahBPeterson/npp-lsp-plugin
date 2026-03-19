using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NppLspPlugin.Plugin
{
    [StructLayout(LayoutKind.Sequential)]
    public struct NppData
    {
        public IntPtr _nppHandle;
        public IntPtr _scintillaMainHandle;
        public IntPtr _scintillaSecondHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ShortcutKey
    {
        public byte _isCtrl;
        public byte _isAlt;
        public byte _isShift;
        public byte _key;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FuncItem
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string _itemName;
        public IntPtr _pFunc;
        public int _cmdID;
        public int _init2Check;
        public IntPtr _pShKey;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NMHDR
    {
        public IntPtr hwndFrom;
        public UIntPtr idFrom;
        public uint code;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SCNotification
    {
        public NMHDR nmhdr;
        public IntPtr position;
        public int ch;
        public int modifiers;
        public int modificationType;
        public IntPtr text;
        public IntPtr length;
        public IntPtr linesAdded;
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
        public int updated;
    }

    public enum NppMsg : uint
    {
        NPPMSG = 0x400 + 1000,
        NPPM_GETCURRENTSCINTILLA = NPPMSG + 4,
        NPPM_GETPLUGINSCONFIGDIR = NPPMSG + 46,
        NPPM_DOOPEN = NPPMSG + 77,
        NPPM_GETFULLCURRENTPATH = NPPMSG + 25,
        NPPM_GETCURRENTLANGTYPE = NPPMSG + 5,
        NPPM_SWITCHTOFILE = NPPMSG + 35,
        NPPM_GETCURRENTBUFFERID = NPPMSG + 60,
        NPPM_GETFULLPATHFROMBUFFERID = NPPMSG + 58,
        NPPM_ADDSCNMODIFIEDFLAGS = NPPMSG + 80,
        NPPM_GETNBOPENFILES = NPPMSG + 7,

        // Notification codes (from Notepad_plus_msgs.h)
        NPPN_FIRST = 1000,
        NPPN_READY = NPPN_FIRST + 1,            // 1001
        NPPN_TBMODIFICATION = NPPN_FIRST + 2,   // 1002
        NPPN_FILEBEFORECLOSE = NPPN_FIRST + 3,   // 1003
        NPPN_FILEOPENED = NPPN_FIRST + 4,        // 1004
        NPPN_FILECLOSED = NPPN_FIRST + 5,        // 1005
        NPPN_FILEBEFOREOPEN = NPPN_FIRST + 6,    // 1006
        NPPN_FILEBEFORESAVE = NPPN_FIRST + 7,    // 1007
        NPPN_FILESAVED = NPPN_FIRST + 8,         // 1008
        NPPN_SHUTDOWN = NPPN_FIRST + 9,          // 1009
        NPPN_BUFFERACTIVATED = NPPN_FIRST + 10,  // 1010
    }

    public enum SciMsg : uint
    {
        // Text retrieval and modification
        SCI_GETLENGTH = 2006,
        SCI_GETCURRENTPOS = 2008,
        SCI_GETSELTEXT = 2161,
        SCI_SETSEL = 2160,
        SCI_REPLACESEL = 2170,
        SCI_SETTEXT = 2181,
        SCI_GETTEXT = 2182,
        SCI_GETLINE = 2153,
        SCI_GETLINECOUNT = 2154,
        SCI_LINELENGTH = 2350,
        SCI_GETCODEPAGE = 2137,

        // Position/line conversion
        SCI_POSITIONFROMLINE = 2167,
        SCI_POSITIONRELATIVE = 2670,
        SCI_LINEFROMPOSITION = 2166,
        SCI_GETCOLUMN = 2129,
        SCI_GOTOPOS = 2025,
        SCI_GOTOLINE = 2024,
        SCI_ENSUREVISIBLEENFORCEPOLICY = 2234,
        SCI_WORDSTARTPOSITION = 2266,
        SCI_WORDENDPOSITION = 2267,

        // Autocomplete
        SCI_AUTOCSHOW = 2100,
        SCI_AUTOCCANCEL = 2101,
        SCI_AUTOCACTIVE = 2102,
        SCI_AUTOCSETSEPARATOR = 2106,
        SCI_AUTOCSETIGNORECASE = 2115,
        SCI_AUTOCSETORDER = 2660,
        SCI_AUTOCSETMAXHEIGHT = 2210,

        // Calltips
        SCI_CALLTIPSHOW = 2200,
        SCI_CALLTIPCANCEL = 2201,
        SCI_CALLTIPSETHLT = 2204,
        SCI_CALLTIPSETBACK = 2205,

        // Indicators
        SCI_INDICSETSTYLE = 2080,
        SCI_INDICSETFORE = 2082,
        SCI_INDICSETALPHA = 2523,
        SCI_SETINDICATORCURRENT = 2500,
        SCI_INDICATORFILLRANGE = 2504,
        SCI_INDICATORCLEARRANGE = 2505,

        // Annotations
        SCI_ANNOTATIONSETTEXT = 2540,
        SCI_ANNOTATIONSETSTYLE = 2542,
        SCI_ANNOTATIONSETVISIBLE = 2548,
        SCI_ANNOTATIONCLEARALL = 2550,

        // Dwell
        SCI_SETMOUSEDWELLTIME = 2264,
    }

    // Scintilla notification codes
    public static class SciNotification
    {
        public const uint SCN_CHARADDED = 2001;
        public const uint SCN_UPDATEUI = 2007;
        public const uint SCN_MODIFIED = 2008;
        public const uint SCN_DWELLSTART = 2016;
        public const uint SCN_DWELLEND = 2017;
        public const uint SCN_AUTOCCOMPLETED = 2030;
    }

    // SCN_MODIFIED flags
    public static class ScModification
    {
        public const int SC_MOD_INSERTTEXT = 0x1;
        public const int SC_MOD_DELETETEXT = 0x2;
    }

    // SCN_UPDATEUI flags
    public static class ScUpdate
    {
        public const int SC_UPDATE_CONTENT = 0x1;
        public const int SC_UPDATE_SELECTION = 0x2;
    }

    // Indicator styles
    public static class IndicatorStyle
    {
        public const int INDIC_SQUIGGLE = 1;
        public const int INDIC_DOTS = 4;
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

        [DllImport("user32", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32", EntryPoint = "MessageBoxW", CharSet = CharSet.Unicode)]
        public static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        [DllImport("user32", EntryPoint = "PostMessageW")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        public const int MAX_PATH = 260;
        public const uint MB_OK = 0x00000000;
    }

    /// <summary>
    /// Scintilla API calls use ANSI/UTF-8 strings.
    /// </summary>
    public static class Sci
    {
        [DllImport("user32", EntryPoint = "SendMessageA")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32", EntryPoint = "SendMessageA")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

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
