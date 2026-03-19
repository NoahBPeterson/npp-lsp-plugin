namespace NppLspPlugin.Server
{
    internal static class LanguageMapping
    {
        // Notepad++ LangType enum values -> LSP languageId strings
        // See: https://github.com/notepad-plus-plus/notepad-plus-plus/blob/master/PowerEditor/src/MISC/PluginsManager/Notepad_plus_msgs.h
        public static string? GetLanguageId(int langType)
        {
            return langType switch
            {
                1 => "php",
                2 => "c",
                3 => "cpp",
                4 => "cs",       // C#
                5 => "objective-c",
                6 => "java",
                7 => "rc",
                8 => "html",
                9 => "xml",
                10 => "makefile",
                11 => "pascal",
                12 => "bat",
                14 => "css",
                15 => "lua",
                16 => "perl",
                17 => "python",
                19 => "asm",
                20 => "diff",
                21 => "properties",
                22 => "postscript",
                23 => "ruby",
                24 => "smalltalk",
                25 => "vb",
                26 => "vhdl",
                28 => "caml",
                29 => "ada",
                30 => "verilog",
                31 => "matlab",
                32 => "haskell",
                33 => "inno",
                35 => "cmake",
                36 => "yaml",
                38 => "erlang",
                40 => "asm",       // ASM duplicate
                41 => "d",
                42 => "r",
                43 => "jsp",
                44 => "coffeescript",
                45 => "json",
                46 => "javascript",
                47 => "fortran",
                49 => "go",
                50 => "verilog",
                51 => "rust",
                56 => "typescript",
                // Extended languages
                _ => null
            };
        }
    }
}
