using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Optional technical-proxy meshes. The gameplay transform, collider and simulation stay on the caller.
    // These files are a generic tetra and two plant stand-ins, not restored species.
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

        static readonly Dictionary<string, Task<GltfImport>> Imports = new Dictionary<string, Task<GltfImport>>();
        static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);

        public static void AttachFish(Transform driver, string speciesId)
        {
            if (speciesId != FishSpeciesId) return;
            Bind(driver, FishFile, SwimClip, false, Vector3.zero);
        }

        public static void AttachPlant(Transform driver, string itemId)
        {
            if (itemId == CarpetPlantItemId) Bind(driver, AnubiasFile, SwayClip, true, new Vector3(0f, -0.5f, 0f));
            else if (itemId == LeafPlantItemId) Bind(driver, FernFile, SwayClip, true, new Vector3(0f, -0.5f, 0f));
        }

        public static void Present(Transform parent, string fileName, string clip, bool plantMaterial, Vector3 localPosition)
        {
            var anchor = new GameObject("tech-proxy-anchor");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = Quaternion.identity;
            anchor.transform.localScale = Vector3.one;
            Bind(anchor.transform, fileName, clip, plantMaterial, Vector3.zero);
        }

        static void Bind(Transform driver, string fileName, string clip, bool plantMaterial, Vector3 localPosition)
        {
            if (driver == null || driver.GetComponent<TechProxyBinding>() != null) return;
            driver.gameObject.AddComponent<TechProxyBinding>();
            BindAsync(driver, fileName, clip, plantMaterial, localPosition);
        }

        static async void BindAsync(Transform driver, string fileName, string clip, bool plantMaterial, Vector3 localPosition)
        {
            try
            {
                GltfImport gltf = await Load(fileName);
                if (driver == null || gltf == null) return;
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
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Technical proxy stayed on the placeholder for " + fileName + ": " + exception.Message);
            }
        }

        static Task<GltfImport> Load(string fileName)
        {
            Task<GltfImport> existing;
            if (Imports.TryGetValue(fileName, out existing)) return existing;
            Task<GltfImport> created = LoadNew(fileName);
            Imports[fileName] = created;
            return created;
        }

        static async Task<GltfImport> LoadNew(string fileName)
        {
            string path = Path.Combine(Application.dataPath, "_Game/TechProxies/Models", fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning("Technical proxy file is absent, placeholder remains: " + path);
                return null;
            }
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
            // glTFast's URP generator already sets these for MASK + doubleSided. Repeat only when the shader exposes them.
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

        sealed class TechProxyBinding : MonoBehaviour { }
    }
}
