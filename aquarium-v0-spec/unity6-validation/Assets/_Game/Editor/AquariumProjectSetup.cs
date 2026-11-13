#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aquarium.UnityEditorTools
{
    public static class AquariumProjectSetup
    {
        private const string PipelinePath = "Assets/_Game/Rendering/AquariumURP.asset";
        private const string ScenePath = "Assets/_Game/Scenes/Aquarium.unity";
        [MenuItem("Aquarium/Prepare Project")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/_Game/Rendering");
            AssetDatabase.Refresh();
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                UniversalRendererData renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, "Assets/_Game/Rendering/AquariumRenderer.asset");
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            PlayerSettings.companyName = "Aquarium Journal";
            PlayerSettings.productName = "Aquarium Journal";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Directory.CreateDirectory("Assets/_Game/Resources/Rendering");AssetDatabase.Refresh();
            Shader shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new System.Exception("URP Lit shader is missing after package import.");
            EnsureMaterial("Assets/_Game/Resources/Rendering/AquariumOpaque.mat",shader,false);
            EnsureMaterial("Assets/_Game/Resources/Rendering/AquariumWater.mat",shader,true);
            AssetDatabase.SaveAssets();
            Debug.Log("Aquarium source project prepared. Editor Play and player validation remain required.");
        }
        private static void EnsureMaterial(string path,Shader shader,bool transparent)
        {
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.color=Color.white;
            if(transparent){material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=3000;}
            EditorUtility.SetDirty(material);
        }
        [MenuItem("Aquarium/Open Aquarium Scene")]
        public static void OpenScene() { Prepare(); EditorSceneManager.OpenScene(ScenePath); }
        [MenuItem("Aquarium/Build Windows x64")]
        public static void BuildWindows()
        {
            Prepare();Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new[] { ScenePath }, "Builds/Windows/AquariumJournal.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Windows build failed: " + report.summary.result);
        }
        [Serializable]
        private sealed class MacBuildEvidence
        {
            public string unityVersion, target = "StandaloneOSX", architecture, backend, options = "None";
            public string outputPath, result = "Failed", failure;
            public int totalErrors, totalWarnings;
            public ulong totalSizeBytes;
            public double totalSeconds;
            public string[] messages = new string[0];
        }
        private static string BuildArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
        [MenuItem("Aquarium/Build macOS ARM64 Mono")]
        public static void BuildMacOS()
        {
            string output = Path.GetFullPath(BuildArgument("-aquariumBuildOutput") ?? Path.Combine(Application.dataPath, "../Builds/macOS/AquariumJournal.app"));
            string evidencePath = Path.GetFullPath(BuildArgument("-aquariumBuildReport") ?? Path.Combine(Application.dataPath, "../../verification/unity/unity6-validation/macos-build-report.json"));
            var evidence = new MacBuildEvidence { unityVersion = Application.unityVersion, outputPath = output };
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                    throw new InvalidOperationException("Installed Editor has no macOS standalone build support.");
                Prepare();
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64;
                UnityEditor.OSXStandalone.UserBuildSettings.createXcodeProject = false;
                evidence.backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone).ToString();
                evidence.architecture = UnityEditor.OSXStandalone.UserBuildSettings.architecture.ToString();
                AssetDatabase.SaveAssets();
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                BuildReport report = BuildPipeline.BuildPlayer(new[] { ScenePath }, output, BuildTarget.StandaloneOSX, BuildOptions.None);
                evidence.result = report.summary.result.ToString();
                evidence.outputPath = report.summary.outputPath;
                evidence.totalErrors = report.summary.totalErrors; evidence.totalWarnings = report.summary.totalWarnings;
                evidence.totalSizeBytes = report.summary.totalSize; evidence.totalSeconds = report.summary.totalTime.TotalSeconds;
                var messages = new List<string>();
                foreach (BuildStep step in report.steps)
                    foreach (BuildStepMessage message in step.messages) messages.Add(step.name + ": " + message.type + ": " + message.content);
                evidence.messages = messages.ToArray();
                if(report.summary.result == BuildResult.Succeeded)
                {
                    string notices=Path.Combine(output,"Contents/Resources/ThirdPartyNotices/NotoSansCJK");
                    Directory.CreateDirectory(notices);
                    foreach(string name in new[]{"OFL-1.1.txt","NOTICE.txt"})
                        File.Copy(Path.Combine(Application.dataPath,"_Game/Resources/Fonts/"+name),Path.Combine(notices,name),true);
                    // Readable notices are added after BuildPlayer, so refresh the local ad-hoc seal.
                    var signing=new System.Diagnostics.ProcessStartInfo("/usr/bin/codesign") { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true };
                    foreach(string argument in new[]{"--force","--sign","-","--timestamp=none",output})signing.ArgumentList.Add(argument);
                    using(var process=System.Diagnostics.Process.Start(signing))
                    {
                        string signOutput=process.StandardOutput.ReadToEnd()+process.StandardError.ReadToEnd();process.WaitForExit();
                        if(process.ExitCode!=0)throw new InvalidOperationException("Local ad-hoc signing after notices failed: "+signOutput);
                    }
                }
                Debug.Log("AQUARIUM_MAC_BUILD " + evidence.result + " " + evidence.architecture + " " + evidence.backend + " " + evidence.outputPath);
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException("macOS build failed: " + report.summary.result + "; " + report.summary.totalErrors + " errors.");
            }
            catch (Exception error) { evidence.failure = error.ToString(); throw; }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(evidencePath));
                File.WriteAllText(evidencePath, JsonUtility.ToJson(evidence, true));
            }
        }
    }
}
#endif
