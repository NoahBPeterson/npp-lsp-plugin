using System.IO;

namespace NppLspPlugin.Util
{
    internal static class WorkspaceDetector
    {
        private static readonly string[] RootMarkers = new[]
        {
            ".git",
            ".hg",
            ".svn",
            "package.json",
            "Cargo.toml",
            "go.mod",
            "pyproject.toml",
            "setup.py",
            "CMakeLists.txt",
            "Makefile",
            ".sln",
            ".csproj",
        };

        public static string DetectRoot(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return UriConverter.PathToUri("");

            var dir = Path.GetDirectoryName(filePath);
            while (!string.IsNullOrEmpty(dir))
            {
                foreach (var marker in RootMarkers)
                {
                    var candidate = Path.Combine(dir, marker);
                    if (File.Exists(candidate) || Directory.Exists(candidate))
                    {
                        return UriConverter.PathToUri(dir);
                    }
                }

                var parent = Path.GetDirectoryName(dir);
                if (parent == dir) break;
                dir = parent;
            }

            // Fall back to the file's directory
            var fallback = Path.GetDirectoryName(filePath) ?? "";
            return UriConverter.PathToUri(fallback);
        }
    }
}
