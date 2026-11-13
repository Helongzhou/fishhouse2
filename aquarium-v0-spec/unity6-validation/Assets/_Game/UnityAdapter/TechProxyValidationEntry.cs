using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Manual viewing scene only. It does not save and is not in the player build list.
    public sealed class TechProxyValidationEntry : MonoBehaviour
    {
        void Awake()
        {
            var cameraObject = new GameObject("Technical proxy camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0.18f, 0.55f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.06f);
            camera.nearClipPlane = 0.01f;
            camera.transform.LookAt(transform.TransformPoint(new Vector3(0f, 0.08f, 0f)));
            var lightObject = new GameObject("Technical proxy light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(40f, -30f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            TechProxyVisuals.Present(transform, TechProxyVisuals.FishFile, TechProxyVisuals.SwimClip, false, new Vector3(0f, 0.08f, 0f));
            TechProxyVisuals.Present(transform, TechProxyVisuals.AnubiasFile, TechProxyVisuals.SwayClip, true, new Vector3(-0.22f, 0f, 0f));
            TechProxyVisuals.Present(transform, TechProxyVisuals.FernFile, TechProxyVisuals.SwayClip, true, new Vector3(0.22f, 0f, 0f));
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12f, 12f, 720f, 140f), GUI.skin.box);
            GUILayout.Label("Technical proxy validation. Generic tetra L0 plus two plant stand-ins. Not species art.");
            GUILayout.Label("This scene does not read or write the player save. BatchMode cannot prove the picture.");
            GUILayout.Label("Screenshot and in-person review stay not_run until a person records them.");
            GUILayout.EndArea();
        }
    }
}
