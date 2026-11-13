using System;
using System.IO;
using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Validation mode never falls back to the player directory. Normal play does.
    public static class SessionSaveLocation
    {
        public const string FileName = "aquarium-session-v1.json";
        public const string DemoFileName = "aquarium-behaviour-demo-v2.json";
        public static bool ValidationMode;
        public static string OverrideDirectory;

        public static string Resolve()
        {
            return ValidationMode ? RequiredIsolated(FileName) : ProductionOrOverride(FileName);
        }

        public static string ResolveDemo()
        {
            return ValidationMode ? RequiredIsolated(DemoFileName) : Path.Combine(Application.persistentDataPath, DemoFileName);
        }

        public static string RequiredIsolated(string fileName)
        {
            if (string.IsNullOrEmpty(OverrideDirectory))
                throw new InvalidOperationException("Validation mode requires an injected save root.");
            return IsolatedFile(OverrideDirectory, fileName);
        }

        public static string IsolatedFile(string root, string fileName)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(fileName) || fileName != Path.GetFileName(fileName))
                throw new InvalidOperationException("Injected save root and file name are required.");
            string canonicalRoot = SaveRootGuard.Canonicalize(root);
            if (!Directory.Exists(canonicalRoot)) throw new DirectoryNotFoundException("Injected save root does not exist.");
            string file = Path.Combine(canonicalRoot, fileName);
            if (!SaveRootGuard.SameOrInside(SaveRootGuard.Canonicalize(file), canonicalRoot))
                throw new InvalidOperationException("Save path escaped the injected root.");
            return file;
        }

        static string ProductionOrOverride(string fileName)
        {
            if (string.IsNullOrEmpty(OverrideDirectory)) return Path.Combine(Application.persistentDataPath, fileName);
            return IsolatedFile(OverrideDirectory, fileName);
        }
    }
}
