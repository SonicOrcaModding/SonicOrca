using System;
using System.IO;
using System.Reflection;

namespace SonicOrca
{

    public static class GamePaths
    {
        private static string _contentRootOverride;

        public static string ContentRootDirectory
        {
            get => string.IsNullOrEmpty(_contentRootOverride) ? GetDefaultContentRoot() : _contentRootOverride;
            set => _contentRootOverride = value;
        }

        private static string GetDefaultContentRoot()
        {
            string loc = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(loc))
                loc = Assembly.GetExecutingAssembly().Location;
            return Path.GetDirectoryName(loc) ?? string.Empty;
        }

        /// <summary>
        /// Resolves a path relative to the game install (e.g. shaders/foo.shader), or returns an existing absolute path.
        /// </summary>
        public static string ResolveContentFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path must be non-empty.", nameof(path));
            if (File.Exists(path))
                return path;
            string root = ContentRootDirectory;
            if (string.IsNullOrEmpty(root))
                return path;
            return Path.Combine(root, path);
        }
    }
}
