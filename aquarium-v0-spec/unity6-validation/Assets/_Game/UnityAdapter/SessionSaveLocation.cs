using System;
using System.IO;
using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Default play still uses Application.persistentDataPath. Tests must set OverrideDirectory
    // first and then call IsolatedFile. -projectPath does not isolate the player save directory.
    public static class SessionSaveLocation
    {
        public const string FileName = "aquarium-session-v1.json";
        public static string OverrideDirectory;

        public static string Resolve()
        {
            if (string.IsNullOrEmpty(OverrideDirectory)) return Path.Combine(Application.persistentDataPath, FileName);
            return IsolatedFile(OverrideDirectory);
        }

        public static string IsolatedFile(string root)
        {
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Injected save root is required.");
            string fullRoot = Path.GetFullPath(root);
            RejectPlayerSaveRoot(fullRoot);
            if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException("Injected save root does not exist.");
            string prefix = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string file = Path.GetFullPath(Path.Combine(fullRoot, FileName));
            if (!file.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("Save path escaped the injected root.");
            return file;
        }

        public static void RejectPlayerSaveRoot(string fullRoot)
        {
            string normalized = fullRoot.Replace('\\', '/');
            if (normalized.IndexOf("Application Support/Aquarium Journal", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("AppData/LocalLow", StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf("/Library/Application Support/", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Refusing a save root inside the default player data directory.");
        }
    }
}
