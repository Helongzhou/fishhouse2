using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Controlled viewing rig. It does not save. Screenshots are taken by the editor driver.
    public sealed class TechProxyVisualRig : MonoBehaviour
    {
        public int attachedCount, boneCount, swayPlayingCount;
        public bool fishClipPlaying;
        public float fishClipTime, swayTime;
        public Camera front, back, side;

        void Awake()
        {
            BuildRuler();
            BuildCameras();
            var lightObject = new GameObject("visual-light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(38f, -28f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            TechProxyVisuals.Present(transform, TechProxyVisuals.FishFile, TechProxyVisuals.SwimClip, false, new Vector3(0f, 0.08f, 0f));
            TechProxyVisuals.Present(transform, TechProxyVisuals.AnubiasFile, TechProxyVisuals.SwayClip, true, new Vector3(-0.22f, 0f, 0f));
            TechProxyVisuals.Present(transform, TechProxyVisuals.FernFile, TechProxyVisuals.SwayClip, true, new Vector3(0.22f, 0f, 0f));
        }

        void Update()
        {
            attachedCount = 0;
            boneCount = 0;
            swayPlayingCount = 0;
            fishClipPlaying = false;
            fishClipTime = 0f;
            swayTime = 0f;
            foreach (TechProxyBinding binding in GetComponentsInChildren<TechProxyBinding>(true))
                if (binding.Outcome == ProxyOutcome.Attached) attachedCount++;
            foreach (SkinnedMeshRenderer skin in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.bones != null) boneCount += skin.bones.Length;
            foreach (Animation animation in GetComponentsInChildren<Animation>(true))
            {
                if (animation.GetClip(TechProxyVisuals.SwimClip) != null && animation.IsPlaying(TechProxyVisuals.SwimClip))
                {
                    fishClipPlaying = true;
                    AnimationState state = animation[TechProxyVisuals.SwimClip];
                    if (state != null) fishClipTime = state.time;
                }
                if (animation.GetClip(TechProxyVisuals.SwayClip) != null && animation.IsPlaying(TechProxyVisuals.SwayClip))
                {
                    swayPlayingCount++;
                    AnimationState state = animation[TechProxyVisuals.SwayClip];
                    if (state != null) swayTime = state.time;
                }
            }
        }

        void BuildRuler()
        {
            Mark("ruler-0cm", Vector3.zero, new Color(0.95f, 0.15f, 0.1f));
            Mark("ruler-10cm", new Vector3(0.10f, 0f, 0f), new Color(0.95f, 0.85f, 0.1f));
            Mark("ruler-20cm", new Vector3(0.20f, 0f, 0f), new Color(0.1f, 0.85f, 0.25f));
        }

        void Mark(string name, Vector3 position, Color color)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = name;
            mark.transform.SetParent(transform, false);
            mark.transform.localPosition = position + new Vector3(0f, 0.005f, 0.12f);
            mark.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            mark.GetComponent<Renderer>().material.color = color;
            Collider collider = mark.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        void BuildCameras()
        {
            front = MakeCamera("visual-front", new Vector3(0f, 0.16f, 0.75f));
            back = MakeCamera("visual-back", new Vector3(0f, 0.16f, -0.75f));
            side = MakeCamera("visual-side", new Vector3(0.75f, 0.16f, 0f));
        }

        Camera MakeCamera(string name, Vector3 position)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = position;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 4f;
            camera.fieldOfView = 35f;
            camera.enabled = false;
            camera.transform.LookAt(transform.TransformPoint(new Vector3(0f, 0.08f, 0f)));
            return camera;
        }
    }
}
