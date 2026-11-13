#if UNITY_EDITOR
using System.IO;
using UnityEditor;
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
            GraphicsSettings.renderPipelineAsset = pipeline;
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
    }
}
#endif
