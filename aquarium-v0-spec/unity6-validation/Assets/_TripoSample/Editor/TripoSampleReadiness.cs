#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Aquarium.TripoSample
{
    // Read-only readiness inspection. Never creates scenes, prefabs, materials or gameplay state.
    public static class TripoSampleReadiness
    {
        [Serializable] private sealed class Manifest { public int schema_version; public string batch_id; public Asset[] assets; }
        [Serializable] private sealed class Asset { public string asset_id; public LocalFiles local_files; }
        [Serializable] private sealed class LocalFiles { public string model; }
        [Serializable] private sealed class Item
        {
            public string asset_id, model, status = "missing", import = "not_run", lod = "not_run", rig_animation = "not_run";
            public string detail;
        }
        [Serializable] private sealed class Report
        {
            public string status = "failed", purpose = "Readiness inspector validation; not sample acceptance", unity_version, started_utc, finished_utc;
            public int schema_version, expected_assets = 11, found_assets, missing_models;
            public bool schema_valid;
            public string glb_import = "pending: use compatible Unity glTFast after package/license verification, or offline FBX export; no package installed";
            public string scene = "not_created", visual_qa = "not_run", failure;
            public List<Item> assets = new List<Item>();
        }
        public static void RunBatch()
        {
            var report = new Report { unity_version = Application.unityVersion, started_utc = DateTime.UtcNow.ToString("O") };
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            string output = Path.Combine(root, "verification/tripo-sample-01/unity-readiness/report.json");
            try
            {
                Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(root, "art/tripo-sample-01/manifest.json")));
                if (manifest == null || manifest.schema_version != 1) throw new InvalidDataException("Unsupported or missing schema_version; supported: 1.");
                report.schema_version = manifest.schema_version;
                if (manifest.assets == null || manifest.assets.Length != 11) throw new InvalidDataException("Sample requires 11 asset entries.");
                report.found_assets = manifest.assets.Length;
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (Asset asset in manifest.assets)
                {
                    if (asset == null || String.IsNullOrWhiteSpace(asset.asset_id) || !ids.Add(asset.asset_id)) throw new InvalidDataException("Missing or duplicate asset_id.");
                    if (asset.local_files == null) throw new InvalidDataException("Missing local_files: " + asset.asset_id);
                }
                report.schema_valid = true;
                foreach (Asset asset in manifest.assets)
                {
                    var item = new Item { asset_id = asset.asset_id, model = asset.local_files.model };
                    report.assets.Add(item);
                    if (String.IsNullOrWhiteSpace(item.model)) { report.missing_models++; item.detail = "No generated local model assigned."; continue; }
                    string path = Path.GetFullPath(Path.Combine(root, item.model));
                    if (!File.Exists(path) || new FileInfo(path).Length == 0) { report.missing_models++; item.detail = "Model missing or empty."; continue; }
                    item.status = "pending_import";
                    string assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
                    if (!path.StartsWith(assetsRoot, StringComparison.Ordinal)) { item.detail = "File exists outside Unity Assets; deliberate import required."; continue; }
                    string assetPath = "Assets/" + path.Substring(assetsRoot.Length).Replace('\\', '/');
                    GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (model == null) { item.import = "failed"; item.detail = "No imported GameObject; FBX/OBJ preferred, GLB importer pending."; continue; }
                    int meshCount = 0;
                    foreach (UnityEngine.Object part in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                    { Mesh mesh = part as Mesh; if (mesh != null && mesh.vertexCount > 0 && mesh.subMeshCount > 0 && mesh.GetIndexCount(0) > 0) meshCount++; }
                    item.import = meshCount > 0 ? "passed" : "failed";
                    item.detail = "Nonempty imported meshes: " + meshCount + "; LOD/rig/animation and morphology require separate acceptance.";
                    item.status = "acceptance_pending";
                }
                report.failure = report.missing_models > 0 ? report.missing_models + " of 11 models missing. No sample created." : "Readiness only; LOD, rig/animation, materials and visual acceptance not performed.";
            }
            catch (Exception error) { report.failure = error.ToString(); }
            finally
            {
                report.finished_utc = DateTime.UtcNow.ToString("O");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("TRIPO_SAMPLE_READINESS failed; " + report.failure);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
#endif
