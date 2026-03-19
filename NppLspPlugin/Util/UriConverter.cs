using System;

namespace NppLspPlugin.Util
{
    internal static class UriConverter
    {
        public static string PathToUri(string windowsPath)
        {
            if (string.IsNullOrEmpty(windowsPath)) return "";

            // Normalize backslashes to forward slashes
            var path = windowsPath.Replace('\\', '/');

            // Ensure leading slash for drive letter paths (C:/foo -> /C:/foo)
            if (path.Length >= 2 && path[1] == ':')
                path = "/" + path;

            // URI-encode special characters (but not / and :)
            var encoded = Uri.EscapeDataString(path)
                .Replace("%2F", "/")
                .Replace("%3A", ":");

            return "file://" + encoded;
        }

        public static string UriToPath(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return "";

            // Strip file:// prefix
            var path = uri;
            if (path.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(8);
            else if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(7);

            // Decode URI encoding
            path = Uri.UnescapeDataString(path);

            // Convert forward slashes to backslashes for Windows
            path = path.Replace('/', '\\');

            // Remove leading backslash before drive letter (e.g., \C:\foo -> C:\foo)
            if (path.Length >= 3 && path[0] == '\\' && path[2] == ':')
                path = path.Substring(1);

            return path;
        }
    }
}
