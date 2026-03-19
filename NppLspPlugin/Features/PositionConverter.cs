using System;
using System.Runtime.InteropServices;
using System.Text;
using NppLspPlugin.Lsp;
using NppLspPlugin.Plugin;

namespace NppLspPlugin.Features
{
    internal static class PositionConverter
    {
        /// <summary>
        /// Convert a Scintilla byte offset to an LSP Position (0-based line, UTF-16 character offset).
        /// </summary>
        public static Position ScintillaToLsp(IntPtr scintilla, int byteOffset)
        {
            int line = (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_LINEFROMPOSITION, byteOffset, 0);
            int lineStart = (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_POSITIONFROMLINE, line, 0);

            // Read the line text from lineStart to byteOffset
            int byteLen = byteOffset - lineStart;
            if (byteLen <= 0)
                return new Position(line, 0);

            // Get line text
            int lineLength = (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_LINELENGTH, line, 0);
            if (lineLength <= 0)
                return new Position(line, 0);

            var buffer = new byte[lineLength];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Sci.SendMessage(scintilla, (uint)SciMsg.SCI_GETLINE, line, handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            // Count UTF-16 code units in the bytes from line start to byte offset
            int utf16Offset = CountUtf16CodeUnits(buffer, 0, byteLen);
            return new Position(line, utf16Offset);
        }

        /// <summary>
        /// Convert an LSP Position to a Scintilla byte offset.
        /// </summary>
        public static int LspToScintilla(IntPtr scintilla, Position position)
        {
            int lineStart = (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_POSITIONFROMLINE, position.Line, 0);
            if (position.Character == 0)
                return lineStart;

            // Get line text
            int lineLength = (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_LINELENGTH, position.Line, 0);
            if (lineLength <= 0)
                return lineStart;

            var buffer = new byte[lineLength];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Sci.SendMessage(scintilla, (uint)SciMsg.SCI_GETLINE, position.Line, handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            // Walk UTF-8 bytes, counting UTF-16 code units until we reach the target character
            int byteIndex = 0;
            int utf16Count = 0;

            while (byteIndex < lineLength && utf16Count < position.Character)
            {
                int seqLen = GetUtf8SequenceLength(buffer[byteIndex]);
                if (byteIndex + seqLen > lineLength) break;

                // A 4-byte UTF-8 sequence = 2 UTF-16 code units (surrogate pair)
                // Everything else = 1 UTF-16 code unit
                utf16Count += (seqLen == 4) ? 2 : 1;
                byteIndex += seqLen;
            }

            return lineStart + byteIndex;
        }

        /// <summary>
        /// Count UTF-16 code units in a span of UTF-8 bytes.
        /// </summary>
        private static int CountUtf16CodeUnits(byte[] utf8, int start, int byteCount)
        {
            int utf16Count = 0;
            int i = start;
            int end = start + byteCount;

            while (i < end)
            {
                int seqLen = GetUtf8SequenceLength(utf8[i]);
                if (i + seqLen > end) break;

                utf16Count += (seqLen == 4) ? 2 : 1;
                i += seqLen;
            }

            return utf16Count;
        }

        /// <summary>
        /// Get the expected byte length of a UTF-8 sequence from its first byte.
        /// </summary>
        private static int GetUtf8SequenceLength(byte firstByte)
        {
            if (firstByte < 0x80) return 1;      // 0xxxxxxx
            if (firstByte < 0xC0) return 1;      // Invalid continuation, treat as 1
            if (firstByte < 0xE0) return 2;      // 110xxxxx
            if (firstByte < 0xF0) return 3;      // 1110xxxx
            return 4;                              // 11110xxx
        }

        /// <summary>
        /// Get the full document text from Scintilla.
        /// </summary>
        public static string GetDocumentText(IntPtr scintilla)
        {
            int length = (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_GETLENGTH, 0, 0);
            if (length <= 0) return "";

            var buffer = new byte[length + 1];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                Sci.SendMessage(scintilla, (uint)SciMsg.SCI_GETTEXT, length + 1, handle.AddrOfPinnedObject());
            }
            finally
            {
                handle.Free();
            }

            return Encoding.UTF8.GetString(buffer, 0, length);
        }

        /// <summary>
        /// Get the current cursor byte position.
        /// </summary>
        public static int GetCurrentPos(IntPtr scintilla)
        {
            return (int)Sci.SendMessage(scintilla, (uint)SciMsg.SCI_GETCURRENTPOS, 0, 0);
        }

        /// <summary>
        /// Get the current cursor as an LSP Position.
        /// </summary>
        public static Position GetCurrentLspPosition(IntPtr scintilla)
        {
            int byteOffset = GetCurrentPos(scintilla);
            return ScintillaToLsp(scintilla, byteOffset);
        }
    }
}
