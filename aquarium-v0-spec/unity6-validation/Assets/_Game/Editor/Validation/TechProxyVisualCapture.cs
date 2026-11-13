#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Aquarium.UnityAdapter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aquarium.Validation
{
    // Batch visual capture stays out of the player scene and does not use the older play entry.
    [InitializeOnLoad]
    public static class TechProxyVisualCapture
    {
        const string ScenePath = "Assets/_Game/TechProxies/TechProxyVisualCapture.unity";
        const string ArmedKey = "aquarium.visual.armed";
        const string SaveKey = "aquarium.visual.save";
        const string OutKey = "aquarium.visual.out";
        const string ReportKey = "aquarium.visual.report";
        const string CommitKey = "aquarium.visual.commit";
        const string ArmedAtKey = "aquarium.visual.armedAt";
        const int Width = 640, Height = 360;
        static readonly string[] Cameras = { "front", "back", "side" };
        static readonly float[] Samples = { 0f, 0.4f, 0.8f };
        static bool started;
        static float readyAt;
        static int sample;
        static int sealedExit = -1;
        static readonly List<byte[]> pngs = new List<byte[]>();
        static readonly List<string> notes = new List<string>();

        static TechProxyVisualCapture()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!SessionSaveLocation.VisualCommandPresent())
            {
                TechProxyRunReport rejected = NewReport();
                Stage(rejected, "visual_isolation", false, "Missing -aquariumVisual. Play was not entered and no save directory was created.");
                NotRun(rejected, "visual_structure", "visual_frames", "visual_review", "visual_video");
                rejected.failure = "Missing -aquariumVisual.";
                Finish(rejected, 1);
                return;
            }
            SessionSaveLocation.ValidationMode = true;
            SessionSaveLocation.FailClosed = true;
            SessionSaveLocation.OverrideDirectory = null;
            TechProxyRunReport report = NewReport();
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("No graphics device. Refusing to invent frames.");
                string created = SaveRootGuard.CreateFreshDirectory(Argument("-aquariumSaveRoot"), SessionSaveLocation.ForbiddenRoots());
                SessionSaveLocation.AdoptExistingVisualRoot(created);
                SessionSaveLocation.FailClosed = false;
                string frames = PrepareFrames(Argument("-aquariumOut"));
                SessionState.SetBool(ArmedKey, true);
                SessionState.SetString(SaveKey, created);
                SessionState.SetString(OutKey, frames);
                SessionState.SetString(ReportKey, Argument("-aquariumReport") ?? "");
                SessionState.SetString(CommitKey, Argument("-aquariumCommit") ?? "");
                SessionState.SetString(ArmedAtKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
                EditorSceneManager.OpenScene(ScenePath);
                if (EditorSceneManager.GetActiveScene().path != ScenePath)
                    throw new InvalidOperationException("Visual scene was not opened.");
                EditorApplication.isPlaying = true;
            }
            catch (Exception error)
            {
                SessionState.SetBool(ArmedKey, false);
                Stage(report, "visual_isolation", false, error.GetType().Name + ": " + error.Message);
                NotRun(report, "visual_structure", "visual_frames", "visual_review", "visual_video");
                report.failure = error.Message;
                Finish(report, 1);
            }
        }

        static void Tick()
        {
            if (sealedExit >= 0) return;
            if (SessionSaveLocation.VisualIsolationFailed)
            {
                Fail("Visual isolation failed closed before scene load.", null);
                return;
            }
            string isolation;
            double armedAt;
            double.TryParse(SessionState.GetString(ArmedAtKey, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out armedAt);
            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.timeSinceStartup - armedAt > 45d) Fail("Play mode did not start.", null);
                return;
            }
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Fail("No graphics device after entering Play.", null);
                return;
            }
            TechProxyVisualRig rig = UnityEngine.Object.FindFirstObjectByType<TechProxyVisualRig>();
            if (rig == null)
            {
                if (EditorApplication.timeSinceStartup - armedAt > 45d) Fail("Visual rig is missing.", null);
                return;
            }
            if (!IsolationReady(out isolation))
            {
                Fail(isolation, rig);
                return;
            }
            if (!started)
            {
                if (rig.attachedCount >= 3 && rig.boneCount > 0)
                {
                    started = true;
                    readyAt = Time.time;
                }
                else if (EditorApplication.timeSinceStartup - armedAt > 90d)
                {
                    Fail("Proxies were not attached. attached=" + rig.attachedCount + "; bones=" + rig.boneCount, rig);
                }
                return;
            }
            if (sample >= Samples.Length) return;
            if (Time.time < readyAt + Samples[sample]) return;
            if (!IsolationReady(out isolation))
            {
                Fail(isolation, rig);
                return;
            }
            Camera[] cameras = { rig.front, rig.back, rig.side };
            for (int i = 0; i < cameras.Length; i++)
            {
                if (!IsolationReady(out isolation))
                {
                    Fail(isolation, rig);
                    return;
                }
                byte[] png = Render(cameras[i]);
                if (png == null || Blank(png))
                {
                    Fail("Camera " + Cameras[i] + " produced no usable frame at sample " + sample + ".", rig);
                    return;
                }
                pngs.Add(png);
                notes.Add(Cameras[i] + " sample=" + Samples[sample].ToString("0.0", CultureInfo.InvariantCulture)
                    + "s bones=" + rig.boneCount
                    + " fish=" + TechProxyVisuals.SwimClip + ":" + rig.fishClipPlaying
                    + " fishTime=" + rig.fishClipTime.ToString("0.000", CultureInfo.InvariantCulture)
                    + " sway=" + TechProxyVisuals.SwayClip + ":" + rig.swayPlayingCount
                    + " swayTime=" + rig.swayTime.ToString("0.000", CultureInfo.InvariantCulture));
            }
            sample++;
            if (sample < Samples.Length) return;
            Complete(rig);
        }

        static void Complete(TechProxyVisualRig rig)
        {
            if (sealedExit >= 0) return;
            string isolation;
            if (!IsolationReady(out isolation))
            {
                Fail(isolation, rig);
                return;
            }
            EditorApplication.update -= Tick;
            string frames = SessionState.GetString(OutKey, "");
            var manifest = new StringBuilder();
            manifest.Append("{\"test_name\":\"visual_capture\",\"commit\":\"").Append(SessionState.GetString(CommitKey, ""))
                .Append("\",\"width\":").Append(Width).Append(",\"height\":").Append(Height)
                .Append(",\"sample_seconds\":[0,0.4,0.8],\"views\":[\"front\",\"back\",\"side\"]")
                .Append(",\"gpu\":\"").Append(SystemInfo.graphicsDeviceName.Replace("\"", "'"))
                .Append("\",\"gpu_type\":\"").Append(SystemInfo.graphicsDeviceType)
                .Append("\",\"bones\":").Append(rig.boneCount)
                .Append(",\"fish_clip\":\"").Append(TechProxyVisuals.SwimClip)
                .Append("\",\"fish_playing\":").Append(rig.fishClipPlaying ? "true" : "false")
                .Append(",\"fish_time\":").Append(rig.fishClipTime.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(",\"sway_clip\":\"").Append(TechProxyVisuals.SwayClip)
                .Append("\",\"sway_playing_count\":").Append(rig.swayPlayingCount)
                .Append(",\"sway_time\":").Append(rig.swayTime.ToString("0.000", CultureInfo.InvariantCulture))
                .Append(",\"frames\":[");
            for (int i = 0; i < pngs.Count; i++)
            {
                string name = i.ToString("00", CultureInfo.InvariantCulture) + "-" + notes[i].Split(' ')[0] + ".png";
                string path = Path.Combine(frames, name);
                File.WriteAllBytes(path, pngs[i]);
                if (i > 0) manifest.Append(',');
                manifest.Append("{\"file\":\"").Append(name).Append("\",\"sha256\":\"").Append(Hash(pngs[i]))
                    .Append("\",\"note\":\"").Append(notes[i]).Append("\"}");
            }
            manifest.Append("]}");
            File.WriteAllText(Path.Combine(frames, "frames.json"), manifest.ToString());
            bool structure = rig.attachedCount >= 3 && rig.boneCount > 0 && rig.fishClipPlaying && rig.swayPlayingCount >= 2;
            bool isolated = IsolationReady(out isolation);
            TechProxyRunReport report = NewReport();
            Stage(report, "visual_isolation", isolated, isolated ? "save=" + SessionState.GetString(SaveKey, "") : isolation);
            Stage(report, "visual_structure", structure, "attached=" + rig.attachedCount + "; bones=" + rig.boneCount
                + "; " + TechProxyVisuals.SwimClip + " playing=" + rig.fishClipPlaying
                + "; " + TechProxyVisuals.SwayClip + " playing=" + rig.swayPlayingCount + ". Not a visual approval.");
            Stage(report, "visual_frames", pngs.Count == 9, "Sparse samples at 0, 0.4 and 0.8 seconds from front, back and side. Not a visual approval.");
            NotRun(report, "visual_review", "visual_video");
            Finish(report, isolated && structure && pngs.Count == 9 ? 0 : 1);
        }

        static void Fail(string detail, TechProxyVisualRig rig)
        {
            if (sealedExit >= 0) return;
            EditorApplication.update -= Tick;
            TechProxyRunReport report = NewReport();
            Stage(report, "visual_isolation", false, detail);
            Stage(report, "visual_structure", false, rig == null ? detail : "attached=" + rig.attachedCount + "; bones=" + rig.boneCount
                + "; fish=" + rig.fishClipPlaying + "; sway=" + rig.swayPlayingCount);
            Stage(report, "visual_frames", false, "Sampling stopped. No success frames were committed.");
            NotRun(report, "visual_review", "visual_video");
            report.failure = detail;
            Finish(report, 1);
        }

        static byte[] Render(Camera camera)
        {
            if (camera == null) return null;
            RenderTexture texture = RenderTexture.GetTemporary(Width, Height, 24);
            RenderTexture previous = camera.targetTexture;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = texture;
            Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            byte[] png = image.EncodeToPNG();
            RenderTexture.active = active;
            camera.targetTexture = previous;
            RenderTexture.ReleaseTemporary(texture);
            UnityEngine.Object.Destroy(image);
            return png;
        }

        static bool Blank(byte[] png)
        {
            Texture2D image = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!image.LoadImage(png))
            {
                UnityEngine.Object.Destroy(image);
                return true;
            }
            Color32[] pixels = image.GetPixels32();
            UnityEngine.Object.Destroy(image);
            int interesting = 0;
            for (int i = 0; i < pixels.Length; i += 32)
                if (pixels[i].r > 24 || pixels[i].g > 24 || pixels[i].b > 24) interesting++;
            return interesting < 12;
        }

        static string PrepareFrames(string requested)
        {
            if (string.IsNullOrWhiteSpace(requested)) throw new InvalidOperationException("Visual mode requires -aquariumOut.");
            string parent = SaveRootGuard.Canonicalize(requested);
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("Visual output directory must already exist.");
            SaveRootGuard.RejectInside(parent, SessionSaveLocation.ForbiddenRoots());
            string frames = Path.Combine(parent, "frames");
            if (Directory.Exists(frames) || File.Exists(frames))
                throw new InvalidOperationException("Frame directory already exists.");
            Directory.CreateDirectory(frames);
            return frames;
        }

        static string Hash(byte[] data)
        {
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(data);
            var text = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) text.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            return text.ToString();
        }

        static TechProxyRunReport NewReport()
        {
            return new TechProxyRunReport
            {
                actual_run = true,
                test_name = "visual_capture",
                started_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                commit = SessionState.GetString(CommitKey, Argument("-aquariumCommit") ?? "not_supplied"),
                editor_version = Application.unityVersion,
                stages = new List<TechProxyStage>()
            };
        }

        static void Stage(TechProxyRunReport report, string name, bool passed, string detail)
        { report.stages.Add(new TechProxyStage { name = name, status = passed ? "pass" : "fail", detail = detail }); }
        static void NotRun(TechProxyRunReport report, params string[] names)
        { foreach (string name in names) report.stages.Add(new TechProxyStage { name = name, status = "not_run", detail = "not a human visual approval and not a video" }); }
        static bool IsolationReady(out string detail)
        {
            detail = "";
            if (!SessionSaveLocation.VisualCommandPresent())
            {
                detail = "Missing -aquariumVisual.";
                return false;
            }
            if (SessionSaveLocation.VisualIsolationFailed || SessionSaveLocation.FailClosed || !SessionSaveLocation.ValidationMode
                || string.IsNullOrEmpty(SessionSaveLocation.OverrideDirectory))
            {
                detail = "Visual isolation is not restored.";
                return false;
            }
            try
            {
                string canonical = SaveRootGuard.Canonicalize(SessionSaveLocation.OverrideDirectory);
                if (!Directory.Exists(canonical))
                {
                    detail = "Restored save root does not exist.";
                    return false;
                }
                SaveRootGuard.RejectInside(canonical, SessionSaveLocation.ForbiddenRoots());
                string armed = SaveRootGuard.Canonicalize(SessionState.GetString(SaveKey, ""));
                if (!string.Equals(canonical.TrimEnd(Path.DirectorySeparatorChar), armed.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                {
                    detail = "Restored save root does not match the armed root.";
                    return false;
                }
                return true;
            }
            catch (Exception error)
            {
                detail = error.Message;
                return false;
            }
        }

        static void Finish(TechProxyRunReport report, int exit)
        {
            if (sealedExit >= 0) return;
            sealedExit = exit == 0 ? 0 : 1;
            SessionState.SetBool(ArmedKey, false);
            report.process_exit_code = sealedExit;
            report.finished_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            try
            {
                WriteReport(report);
            }
            catch (Exception error)
            {
                Debug.LogError("Visual report was not written: " + error.Message);
                if (sealedExit == 0) sealedExit = 1;
                report.failure = error.Message;
            }
            EditorApplication.Exit(sealedExit);
        }

        static void WriteReport(TechProxyRunReport report)
        {
            string path = SessionState.GetString(ReportKey, Argument("-aquariumReport"));
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Visual mode requires -aquariumReport.");
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                throw new DirectoryNotFoundException("Report directory must already exist.");
            string canonicalParent = SaveRootGuard.Canonicalize(parent);
            if (!Directory.Exists(canonicalParent))
                throw new DirectoryNotFoundException("Report directory must already exist.");
            string full = Path.Combine(canonicalParent, Path.GetFileName(path));
            SaveRootGuard.RejectInside(full, SessionSaveLocation.ForbiddenRoots());
            File.WriteAllText(full, JsonUtility.ToJson(report, true));
        }
        static string Argument(string name)
        { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
    }
}
#endif
