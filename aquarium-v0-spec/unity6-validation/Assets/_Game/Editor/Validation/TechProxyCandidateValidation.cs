#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Aquarium.Core.Configuration;
using Aquarium.Core.Session;
using Aquarium.UnityAdapter;
using UnityEditor;
using UnityEngine;

namespace Aquarium.Validation
{
    [Serializable] public sealed class TechProxyStage { public string name, status, detail; }
    [Serializable] public sealed class TechProxyRunReport
    {
        public bool actual_run;
        public string test_name, started_utc, finished_utc, commit, save_root, log_path, result_path, failure;
        public string editor_version, urp_version;
        public int process_exit_code;
        public List<TechProxyStage> stages = new List<TechProxyStage>();
    }

    public static class TechProxyCandidateValidation
    {
        const string ExpectedEditor = "6000.6.8f1";
        const string ExpectedUrp = "17.6.0";
        const string ExpectedGltf = "6.16.1";

        public static void CompileProbe() { Run("compile_probe", CompileBody); }
        public static void RunProxyChecks() { Run("proxy_checks", ProxyBody); }
        public static void RunIsolatedSave() { Run("isolated_save", SaveBody); }

        static bool holdExit;
        static void Run(string testName, Action<TechProxyRunReport> body)
        {
            TechProxyRunReport report = NewReport(testName);
            holdExit = false;
            int exit = 1;
            try
            {
                body(report);
                if (holdExit) return;
                exit = AnyFail(report) ? 1 : 0;
            }
            catch (Exception error)
            {
                report.failure = error.GetType().Name + ": " + error.Message;
                exit = 1;
            }
            report.process_exit_code = exit;
            report.finished_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            TryWrite(report);
            EditorApplication.Exit(exit);
        }

        static void CompileBody(TechProxyRunReport report)
        {
            Stage(report, "script_compile", true, "executeMethod was reached, so the editor finished script compilation for this process.");
            bool editorOk = Application.unityVersion == ExpectedEditor;
            Stage(report, "editor_version", editorOk, "expected " + ExpectedEditor + "; actual " + Application.unityVersion);
            string manifest = File.ReadAllText(Path.Combine(Application.dataPath, "../Packages/manifest.json"));
            bool urp = manifest.Contains("\"com.unity.render-pipelines.universal\": \"" + ExpectedUrp + "\"");
            bool gltf = manifest.Contains("\"com.unity.cloud.gltfast\": \"" + ExpectedGltf + "\"");
            report.urp_version = urp ? ExpectedUrp : "manifest pin missing";
            Stage(report, "urp_pin", urp, "manifest pin " + ExpectedUrp + "; package resolution and import were not executed as a separate measured stage.");
            Stage(report, "gltfast_pin", gltf, "manifest pin " + ExpectedGltf + ". Reaching this method does not prove a GLB imported.");
            NotRun(report, "proxy_off", "proxy_on_structure", "proxy_import_failure_fallback", "proxy_missing_file_fallback", "save_isolated_roundtrip", "visual_screenshot");
        }

        static void ProxyBody(TechProxyRunReport report)
        {
            Stage(report, "script_compile", true, "executeMethod was reached.");
            NotRun(report, "editor_version", "urp_pin", "gltfast_pin", "save_isolated_roundtrip", "visual_screenshot");
            var off = new GameObject("proxy-off");
            ProxyOutcome skipped = TechProxyVisuals.ConsiderFish(off.transform, TechProxyVisuals.FishSpeciesId, false);
            bool offOk = skipped == ProxyOutcome.NotAttempted && off.GetComponent<TechProxyBinding>() == null && off.GetComponent<Renderer>() == null;
            Stage(report, "proxy_off", offOk, "enabled=false returns NotAttempted and adds no binding. Fallback is not involved.");
            UnityEngine.Object.DestroyImmediate(off);

            var failed = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            failed.name = "proxy-simulated-failure";
            TechProxyVisuals.SimulateImportFailure = true;
            ProxyOutcome failure = TechProxyVisuals.ConsiderFish(failed.transform, TechProxyVisuals.FishSpeciesId, true);
            TechProxyBinding failureBinding = failed.GetComponent<TechProxyBinding>();
            bool failureOk = failure == ProxyOutcome.ImportFailed && failureBinding != null && failureBinding.Attempted
                && failed.GetComponent<Renderer>().enabled && failed.transform.Find("tech-proxy-" + TechProxyVisuals.FishFile) == null;
            TechProxyVisuals.SimulateImportFailure = false;
            Stage(report, "proxy_import_failure_fallback", failureOk, failureOk
                ? "Simulated importer failure was attempted and the placeholder renderer stayed enabled."
                : "Simulated failure did not both attempt and leave the placeholder visible.");
            UnityEngine.Object.DestroyImmediate(failed);

            string missingDir = Path.Combine(Path.GetTempPath(), "aquarium-u6-missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(missingDir);
            TechProxyVisuals.ModelDirectoryOverride = missingDir;
            var missing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            missing.name = "proxy-missing-file";
            ProxyOutcome missingOutcome = TechProxyVisuals.ConsiderFish(missing.transform, TechProxyVisuals.FishSpeciesId, true);
            TechProxyBinding missingBinding = missing.GetComponent<TechProxyBinding>();
            bool missingOk = missingOutcome == ProxyOutcome.MissingFile && missingBinding != null && missingBinding.Attempted
                && missing.GetComponent<Renderer>().enabled;
            TechProxyVisuals.ModelDirectoryOverride = null;
            Stage(report, "proxy_missing_file_fallback", missingOk, "Missing GLB path " + missingDir + " must keep the placeholder.");
            UnityEngine.Object.DestroyImmediate(missing);

            var live = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            live.name = "proxy-live";
            live.transform.localScale = new Vector3(0.011f, 0.015f, 0.032f);
            ProxyOutcome started = TechProxyVisuals.ConsiderFish(live.transform, TechProxyVisuals.FishSpeciesId, true);
            TechProxyBinding liveBinding = live.GetComponent<TechProxyBinding>();
            if (started == ProxyOutcome.MissingFile || liveBinding == null || !liveBinding.Attempted)
            {
                Stage(report, "proxy_on_structure", false, "Live GLB was not attempted. Placeholder fallback is not counted as an on-success.");
                UnityEngine.Object.DestroyImmediate(live);
                return;
            }
            double deadline = EditorApplication.timeSinceStartup + 60;
            EditorApplication.CallbackFunction wait = null;
            wait = () =>
            {
                if (liveBinding.Outcome == ProxyOutcome.Pending && EditorApplication.timeSinceStartup < deadline) return;
                EditorApplication.update -= wait;
                bool attached = liveBinding.Outcome == ProxyOutcome.Attached && !live.GetComponent<Renderer>().enabled
                    && live.GetComponentInChildren<SkinnedMeshRenderer>() != null;
                Stage(report, "proxy_on_structure", attached, "outcome=" + liveBinding.Outcome + ". This is a structure check, not a screenshot.");
                UnityEngine.Object.DestroyImmediate(live);
                int exit = AnyFail(report) ? 1 : 0;
                report.process_exit_code = exit;
                report.finished_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                TryWrite(report);
                EditorApplication.Exit(exit);
            };
            EditorApplication.update += wait;
            holdExit = true;
        }

        static void SaveBody(TechProxyRunReport report)
        {
            Stage(report, "script_compile", true, "executeMethod was reached.");
            NotRun(report, "editor_version", "urp_pin", "gltfast_pin", "proxy_off", "proxy_on_structure", "proxy_import_failure_fallback", "proxy_missing_file_fallback", "visual_screenshot");
            string requested = Argument("-aquariumSaveRoot");
            if (string.IsNullOrWhiteSpace(requested))
            {
                report.save_root = "";
                Stage(report, "save_isolated_roundtrip", false, "Missing -aquariumSaveRoot. No player save was opened.");
                return;
            }
            string full = Path.GetFullPath(requested);
            report.save_root = full;
            SessionSaveLocation.RejectPlayerSaveRoot(full);
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectPrefix = project.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(projectPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Save root must be outside the Unity project.");
            string parent = Path.GetDirectoryName(full);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                throw new InvalidOperationException("Parent of the save root must already exist. Refusing to create parent directories.");
            if (Directory.Exists(full) || File.Exists(full))
                throw new InvalidOperationException("Save root already exists. Refusing to delete or reuse it.");
            Directory.CreateDirectory(full);
            if (Directory.GetFileSystemEntries(full).Length != 0)
                throw new InvalidOperationException("Newly created save root is not empty.");
            SessionSaveLocation.OverrideDirectory = full;
            string file = SessionSaveLocation.IsolatedFile(full);
            TextAsset resource = Resources.Load<TextAsset>("Configuration/runtime");
            if (resource == null) throw new InvalidOperationException("Missing runtime configuration.");
            RuntimeCatalog catalog = JsonUtility.FromJson<RuntimeCatalog>(resource.text);
            catalog.Validate();
            GameSession session = new GameSession(catalog, SessionMode.Prototype);
            Func<string, bool> valid = payload => { try { GameSession.Restore(catalog, JsonUtility.FromJson<SessionSnapshot>(payload), SessionMode.Prototype); return true; } catch { return false; } };
            var store = new AtomicSaveStore(file, valid);
            store.Save(JsonUtility.ToJson(session.Capture(), true));
            session.Advance(1);
            store.Save(JsonUtility.ToJson(session.Capture(), true));
            File.WriteAllText(file, "{invalid-json");
            string recoveredFrom;
            string payload = store.Load(out recoveredFrom);
            string prefix = full.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            bool inside = recoveredFrom.StartsWith(prefix, StringComparison.Ordinal) && file.StartsWith(prefix, StringComparison.Ordinal);
            bool backup = recoveredFrom == file + ".bak1" && valid(payload);
            bool onlyOwned = true;
            foreach (string entry in Directory.GetFileSystemEntries(full))
            {
                string name = Path.GetFileName(entry);
                if (name != SessionSaveLocation.FileName && name != SessionSaveLocation.FileName + ".bak1"
                    && name != SessionSaveLocation.FileName + ".bak2" && name != SessionSaveLocation.FileName + ".bak3"
                    && name != SessionSaveLocation.FileName + ".tmp") onlyOwned = false;
            }
            SessionSaveLocation.OverrideDirectory = null;
            Stage(report, "save_isolated_roundtrip", inside && backup && onlyOwned,
                "root=" + full + "; recovered=" + recoveredFrom + ". Player persistentDataPath was not used. Nothing outside this new directory was deleted.");
        }

        static TechProxyRunReport NewReport(string testName)
        {
            return new TechProxyRunReport
            {
                actual_run = true,
                test_name = testName,
                started_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                commit = Argument("-aquariumCommit") ?? "not_supplied",
                log_path = Argument("-aquariumLog") ?? "not_supplied",
                save_root = "",
                editor_version = Application.unityVersion,
                stages = new List<TechProxyStage>()
            };
        }

        static void Stage(TechProxyRunReport report, string name, bool passed, string detail)
        { report.stages.Add(new TechProxyStage { name = name, status = passed ? "pass" : "fail", detail = detail }); }
        static void NotRun(TechProxyRunReport report, params string[] names)
        { foreach (string name in names) report.stages.Add(new TechProxyStage { name = name, status = "not_run", detail = "not executed in this process" }); }
        static bool AnyFail(TechProxyRunReport report)
        { foreach (TechProxyStage stage in report.stages) if (stage.status == "fail") return true; return report.failure != null; }
        static string Argument(string name)
        { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        static void TryWrite(TechProxyRunReport report)
        {
            string path = Argument("-aquariumReport");
            report.result_path = path ?? "not_supplied";
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string full = Path.GetFullPath(path);
                SessionSaveLocation.RejectPlayerSaveRoot(full);
                string parent = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                File.WriteAllText(full, JsonUtility.ToJson(report, true));
            }
            catch (Exception error) { Debug.LogError("Could not write the test report: " + error.Message); }
        }
    }
}
#endif
