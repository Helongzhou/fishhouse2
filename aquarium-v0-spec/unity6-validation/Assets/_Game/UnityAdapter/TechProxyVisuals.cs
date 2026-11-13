using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace Aquarium.UnityAdapter
{
    public enum ProxyOutcome { NotAttempted, Pending, MissingFile, ImportFailed, Attached }

    // Unity 6 candidate copy of the 2022 technical-proxy presentation. Core and save schema are not involved.
    public static class TechProxyVisuals
    {
        public const string FishSpeciesId = "ember_tetra";
        public const string CarpetPlantItemId = "plant_anubias_nana";
        public const string LeafPlantItemId = "plant_java_fern";
        public const string FishFile = "fh_v0_tetra_proxy_L0.glb";
        public const string AnubiasFile = "pl_v0_2_anubias_proxy.glb";
        public const string FernFile = "pl_v0_2_java_fern_proxy.glb";
        public const string SwimClip = "swim_tail_loop";
        public const string SwayClip = "sway_loop";

        public static bool SimulateImportFailure;
        public static string ModelDirectoryOverride;
        static readonly Dictionary<string, Task<GltfImport>> Imports = new Dictionary<string, Task<GltfImport>>();
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        public static ProxyOutcome ConsiderFish(Transform driver, string speciesId, bool enabled)
        {
            if (!enabled || speciesId != FishSpeciesId) return ProxyOutcome.NotAttempted;
            return Begin(driver, FishFile, SwimClip, false, Vector3.zero);
        }

        public static ProxyOutcome ConsiderPlant(Transform driver, string itemId, bool enabled)
        {
            if (!enabled) return ProxyOutcome.NotAttempted;
            if (itemId == CarpetPlantItemId) return Begin(driver, AnubiasFile, SwayClip, true, new Vector3(0f, -0.5f, 0f));
            if (itemId == LeafPlantItemId) return Begin(driver, FernFile, SwayClip, true, new Vector3(0f, -0.5f, 0f));
            return ProxyOutcome.NotAttempted;
        }

        public static void Present(Transform parent, string fileName, string clip, bool plantMaterial, Vector3 localPosition)
        {
            var anchor = new GameObject("tech-proxy-anchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = Quaternion.identity;
            anchor.transform.localScale = Vector3.one;
            Begin(anchor.transform, fileName, clip, plantMaterial, Vector3.zero);
        }

        public static string ModelPath(string fileName)
        {
            string directory = string.IsNullOrEmpty(ModelDirectoryOverride)
                ? Path.Combine(Application.dataPath, "_Game/TechProxies/Models")
                : ModelDirectoryOverride;
            return Path.Combine(directory, fileName);
        }

        static ProxyOutcome Begin(Transform driver, string fileName, string clip, bool plantMaterial, Vector3 localPosition)
        {
            if (driver == null) return ProxyOutcome.NotAttempted;
            TechProxyBinding existing = driver.GetComponent<TechProxyBinding>();
            if (existing != null) return existing.Outcome;
            TechProxyBinding marker = driver.gameObject.AddComponent<TechProxyBinding>();
            marker.Attempted = true;
            if (SimulateImportFailure)
            {
                marker.Outcome = ProxyOutcome.ImportFailed;
                return marker.Outcome;
            }
            string path = ModelPath(fileName);
            if (!File.Exists(path))
            {
                marker.Outcome = ProxyOutcome.MissingFile;
                Debug.LogWarning("Technical proxy file is absent, placeholder remains: " + path);
                return marker.Outcome;
            }
            marker.Outcome = ProxyOutcome.Pending;
            BindAsync(driver, marker, path, fileName, clip, plantMaterial, localPosition);
            return marker.Outcome;
        }

        static async void BindAsync(Transform driver, TechProxyBinding marker, string path, string fileName, string clip, bool plantMaterial, Vector3 localPosition)
        {
            try
            {
                GltfImport gltf = await Load(path, fileName);
                if (driver == null || marker == null) return;
                if (gltf == null)
                {
                    marker.Outcome = ProxyOutcome.ImportFailed;
                    return;
                }
                var holder = new GameObject("tech-proxy-" + fileName);
                holder.transform.SetParent(driver, false);
                holder.transform.localPosition = localPosition;
                holder.transform.localRotation = Quaternion.identity;
                Vector3 scale = driver.localScale;
                holder.transform.localScale = new Vector3(
                    Mathf.Abs(scale.x) > 1e-6f ? 1f / scale.x : 1f,
                    Mathf.Abs(scale.y) > 1e-6f ? 1f / scale.y : 1f,
                    Mathf.Abs(scale.z) > 1e-6f ? 1f / scale.z : 1f);
                await Gate.WaitAsync();
                bool created;
                try { created = await gltf.InstantiateMainSceneAsync(holder.transform); }
                finally { Gate.Release(); }
                if (driver == null) { Object.Destroy(holder); return; }
                if (!created || holder.transform.childCount == 0)
                {
                    marker.Outcome = ProxyOutcome.ImportFailed;
                    Debug.LogWarning("Technical proxy produced no scene: " + fileName);
                    Object.Destroy(holder);
                    return;
                }
                foreach (Collider collider in holder.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
                foreach (SkinnedMeshRenderer skin in holder.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.quality = SkinQuality.Bone4;
                foreach (Animator animator in holder.GetComponentsInChildren<Animator>(true)) animator.applyRootMotion = false;
                if (plantMaterial) ReinforcePlantMaterials(holder);
                PlayClip(holder, clip);
                Renderer placeholder = driver.GetComponent<Renderer>();
                if (placeholder != null) placeholder.enabled = false;
                marker.Outcome = ProxyOutcome.Attached;
            }
            catch (System.Exception exception)
            {
                if (marker != null) marker.Outcome = ProxyOutcome.ImportFailed;
                Debug.LogWarning("Technical proxy stayed on the placeholder for " + fileName + ": " + exception.Message);
            }
        }

        static Task<GltfImport> Load(string path, string fileName)
        {
            Task<GltfImport> existing;
            if (Imports.TryGetValue(path, out existing)) return existing;
            Task<GltfImport> created = LoadNew(path, fileName);
            Imports[path] = created;
            return created;
        }

        static async Task<GltfImport> LoadNew(string path, string fileName)
        {
            var gltf = new GltfImport();
            bool loaded = await gltf.LoadFile(path);
            if (!loaded)
            {
                Debug.LogWarning("glTFast did not load " + fileName + ". Placeholder remains.");
                gltf.Dispose();
                return null;
            }
            return gltf;
        }

        static void PlayClip(GameObject holder, string clip)
        {
            Animation animation = holder.GetComponentInChildren<Animation>(true);
            if (animation == null || animation.GetClip(clip) == null)
            {
                Debug.LogWarning("Technical proxy has no Animation clip " + clip + ".");
                return;
            }
            AnimationState state = animation[clip];
            state.wrapMode = WrapMode.Loop;
            animation.wrapMode = WrapMode.Loop;
            animation.Play(clip);
        }

        static void ReinforcePlantMaterials(GameObject holder)
        {
            foreach (Renderer renderer in holder.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material material = materials[i];
                    if (material == null) continue;
                    if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                    if (material.HasProperty("alphaCutoff") && material.GetFloat("alphaCutoff") <= 0f) material.SetFloat("alphaCutoff", 0.4f);
                    if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
                    material.EnableKeyword("_ALPHATEST_ON");
                }
                renderer.materials = materials;
            }
        }
    }

    public sealed class TechProxyBinding : MonoBehaviour
    {
        public bool Attempted;
        public ProxyOutcome Outcome = ProxyOutcome.NotAttempted;
    }
}
