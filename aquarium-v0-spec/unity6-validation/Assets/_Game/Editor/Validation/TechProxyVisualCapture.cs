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
        static readonly List<byte[]> pngs = new List<byte[]>();
        static readonly List<string> notes = new List<string>();

        static TechProxyVisualCapture()
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
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
            Camera[] cameras = { rig.front, rig.back, rig.side };
            for (int i = 0; i < cameras.Length; i++)
            {
                byte[] png = Render(cameras[i]);
                if (png == null || Blank(png))
                {
                    Fail("Camera " + Cameras[i] + " produced no usable frame at sample " + sample + ".", rig);
                    return;
                }
                pngs.Add(png);
                notes.Add(Cameras[i] + " t=" + Samples[sample].ToString("0.0", CultureInfo.InvariantCulture)
                    + " bones=" + rig.boneCount + " playing=" + rig.clipPlaying
                    + " clipTime=" + rig.clipTime.ToString("0.000", CultureInfo.InvariantCulture));
            }
            sample++;
            if (sample < Samples.Length) return;
            Complete(rig);
        }

        static void Complete(TechProxyVisualRig rig)
        {
            EditorApplication.update -= Tick;
            string frames = SessionState.GetString(OutKey, "");
            var manifest = new StringBuilder();
            manifest.Append("{\"test_name\":\"visual_capture\",\"commit\":\"").Append(SessionState.GetString(CommitKey, ""))
                .Append("\",\"width\":").Append(Width).Append(",\"height\":").Append(Height)
                .Append(",\"gpu\":\"").Append(SystemInfo.graphicsDeviceName.Replace("\"", "'"))
                .Append("\",\"gpu_type\":\"").Append(SystemInfo.graphicsDeviceType)
                .Append("\",\"bones\":").Append(rig.boneCount)
                .Append(",\"clip_playing\":").Append(rig.clipPlaying ? "true" : "false")
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
            bool structure = rig.attachedCount >= 3 && rig.boneCount > 0 && rig.clipPlaying;
            TechProxyRunReport report = NewReport();
            Stage(report, "visual_isolation", true, "save=" + SessionState.GetString(SaveKey, ""));
            Stage(report, "visual_structure", structure, "attached=" + rig.attachedCount + "; bones=" + rig.boneCount
                + "; " + TechProxyVisuals.SwimClip + " playing=" + rig.clipPlaying + ". Not a visual approval.");
            Stage(report, "visual_frames", pngs.Count == 9, "9 bounded frames at 640x360. Human review is separate.");
            NotRun(report, "visual_review", "visual_video");
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.isPlaying = false;
            Finish(report, structure && pngs.Count == 9 ? 0 : 1);
        }

        static void Fail(string detail, TechProxyVisualRig rig)
        {
            EditorApplication.update -= Tick;
            TechProxyRunReport report = NewReport();
            Stage(report, "visual_isolation", Directory.Exists(SessionState.GetString(SaveKey, "missing-save-root")), detail);
            Stage(report, "visual_structure", false, rig == null ? detail : "attached=" + rig.attachedCount + "; bones=" + rig.boneCount);
            Stage(report, "visual_frames", false, "No success frames were committed.");
            NotRun(report, "visual_review", "visual_video");
            report.failure = detail;
            SessionState.SetBool(ArmedKey, false);
            EditorApplication.isPlaying = false;
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
        static void Finish(TechProxyRunReport report, int exit)
        {
            report.process_exit_code = exit;
            report.finished_utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string path = SessionState.GetString(ReportKey, Argument("-aquariumReport"));
            if (!string.IsNullOrWhiteSpace(path))
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(path));
                string full = Path.Combine(SaveRootGuard.Canonicalize(parent), Path.GetFileName(path));
                SaveRootGuard.RejectInside(full, SessionSaveLocation.ForbiddenRoots());
                File.WriteAllText(full, JsonUtility.ToJson(report, true));
            }
            EditorApplication.Exit(exit);
        }
        static string Argument(string name)
        { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
    }
}
#endif
