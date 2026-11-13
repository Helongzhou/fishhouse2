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
        public const string VisualFlag = "-aquariumVisual";
        public static bool ValidationMode;
        public static bool FailClosed;
        public static bool VisualIsolationFailed;
        public static string OverrideDirectory;

        public static bool VisualCommandPresent()
        {
            return CommandLineHas(VisualFlag);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RestoreVisualIsolationBeforeScene()
        {
            if (!CommandLineHas(VisualFlag)) return;
            ValidationMode = true;
            OverrideDirectory = null;
            FailClosed = true;
            VisualIsolationFailed = true;
            try
            {
                AdoptExistingVisualRoot(CommandLineValue("-aquariumSaveRoot"));
                FailClosed = false;
                VisualIsolationFailed = false;
            }
            catch (Exception error)
            {
                OverrideDirectory = null;
                FailClosed = true;
                VisualIsolationFailed = true;
                Debug.LogError("Visual save isolation failed closed before scene load: " + error.Message);
            }
        }

        public static void AdoptExistingVisualRoot(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) throw new InvalidOperationException("Visual mode requires -aquariumSaveRoot.");
            string canonical = SaveRootGuard.Canonicalize(requested);
            if (!Directory.Exists(canonical)) throw new DirectoryNotFoundException("Visual save root does not exist.");
            SaveRootGuard.RejectInside(canonical, ForbiddenRoots());
            ValidationMode = true;
            OverrideDirectory = canonical;
        }

        public static string[] ForbiddenRoots()
        {
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[]
            {
                project,
                Application.persistentDataPath,
                Path.Combine(home, "Library/Application Support/Aquarium Journal"),
                Path.Combine(home, "Library/Application Support/unity.Aquarium Journal.Aquarium Journal"),
                Path.Combine(home, ".config/unity3d/Aquarium Journal")
            };
        }

        public static string Resolve()
        {
            if (FailClosed) throw new InvalidOperationException("Validation save root is not available.");
            return ValidationMode ? RequiredIsolated(FileName) : ProductionOrOverride(FileName);
        }

        public static string ResolveDemo()
        {
            if (FailClosed || ValidationMode) return RequiredIsolated(DemoFileName);
            return Path.Combine(Application.persistentDataPath, DemoFileName);
        }

        static bool CommandLineHas(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++) if (args[i] == name) return true;
            return false;
        }

        static string CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
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
