using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Aquarium.Core;
using UnityEngine;

namespace Aquarium.UnityAdapter
{
    // Add to an empty GameObject in a new URP scene; no authored art assets required.
    public sealed class AquariumInteractionDemo : MonoBehaviour
    {
        private AquariumWorld world;
        private readonly Dictionary<string, Transform> bodies = new Dictionary<string, Transform>();
        private readonly Dictionary<long, Transform> foodMarkers = new Dictionary<long, Transform>();
        private readonly Dictionary<string, SpeciesProfile> profiles = new Dictionary<string, SpeciesProfile>();
        private Transform generatedRoot;
        private Camera view;
        private Light aquariumLight;
        private Material fishMaterial, shrimpMaterial, foodMaterial, pandaMaterial;
        private bool paused;
        private float cameraYaw = 0, cameraPitch = 12, cameraDistance = .85f, approachTimer;
        private readonly DemoLocalization localization = new DemoLocalization();
        private const string LanguagePreferenceKey = "aquarium.language";
        public bool EnableDevelopmentQaLocales;
        private string statusKey = "demo.status.ready", statusDetail = "";
        private readonly Dictionary<string, Font> localeFonts = new Dictionary<string, Font>();
        private readonly Vec3 feedPoint = new Vec3(0, .29, -.02);
        private string SavePath { get { return Path.Combine(Application.persistentDataPath, "aquarium-behaviour-demo-v2.json"); } }

        private void Awake()
        {
            generatedRoot = new GameObject("Generated aquarium demo").transform;
            generatedRoot.SetParent(transform, false);
            ResourceCatalogLoader.LoadAvailable(localization, EnableDevelopmentQaLocales);
            string savedLanguage = PlayerPrefs.HasKey(LanguagePreferenceKey) ? PlayerPrefs.GetString(LanguagePreferenceKey) : null;
            // Steam SDK is not connected: its language input is explicitly null for now.
            localization.ResolveStartupLanguage(savedLanguage, null, CultureInfo.CurrentUICulture.Name);
            profiles.Add("neon_tetra_demo", DemoProfiles.CommunityFish());
            profiles.Add("neocaridina_demo", DemoProfiles.Shrimp());
            profiles.Add("panda_corydoras_demo", DemoProfiles.BottomSifter());
            world = new AquariumWorld(new TankBounds());
            for (int i = 0; i < 10; i++) world.AddAgent(profiles["neon_tetra_demo"], "fish-" + i, (uint)(9481 + i * 7919));
            for (int i = 0; i < 4; i++) world.AddAgent(profiles["neocaridina_demo"], "shrimp-" + i, (uint)(23003 + i * 6151));
            for (int i = 0; i < 6; i++) world.AddAgent(profiles["panda_corydoras_demo"], "panda-" + i, (uint)(41003 + i * 5003));
            fishMaterial = MakeMaterial(new Color(.16f, .65f, .92f));
            shrimpMaterial = MakeMaterial(new Color(.95f, .25f, .18f));
            foodMaterial = MakeMaterial(new Color(.95f, .7f, .18f));
            pandaMaterial = MakeMaterial(new Color(.83f, .83f, .72f));
            BuildTank(); BuildBodies();
        }
        private Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = color };
        }
        private GameObject Shape(string label, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(primitive); go.name = label;
            go.transform.SetParent(generatedRoot, false); go.transform.localPosition = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = go.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            return go;
        }
        private void BuildTank()
        {
            Material sand = MakeMaterial(new Color(.42f, .36f, .24f));
            Material back = MakeMaterial(new Color(.045f, .12f, .16f));
            Material frame = MakeMaterial(new Color(.32f, .46f, .48f));
            Material rock = MakeMaterial(new Color(.22f, .27f, .25f));
            Shape("Sand floor", PrimitiveType.Cube, new Vector3(0, -.012f, 0), new Vector3(.60f, .024f, .30f), sand);
            Shape("Blue back", PrimitiveType.Cube, new Vector3(0, .18f, .158f), new Vector3(.61f, .36f, .01f), back);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Shape("Tank edge", PrimitiveType.Cube, new Vector3(x * .30f, .18f, z * .15f), new Vector3(.003f, .36f, .003f), frame);
            // Cover proxies align with the core's two placeholder hiding anchors.
            Shape("Left cover proxy", PrimitiveType.Cube, new Vector3(-.19f, .025f, .11f), new Vector3(.07f, .05f, .045f), rock);
            Shape("Right cover proxy", PrimitiveType.Cube, new Vector3(.19f, .025f, .11f), new Vector3(.07f, .05f, .045f), rock);
            GameObject cameraObject = new GameObject("Demo camera"); cameraObject.transform.SetParent(generatedRoot, false);
            view = cameraObject.AddComponent<Camera>(); view.nearClipPlane = .01f; view.farClipPlane = 10;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f, .04f, .05f);
            GameObject lightObject = new GameObject("Switchable aquarium light"); lightObject.transform.SetParent(generatedRoot, false);
            aquariumLight = lightObject.AddComponent<Light>(); aquariumLight.type = LightType.Directional;
            lightObject.transform.localRotation = Quaternion.Euler(45, -35, 0); aquariumLight.intensity = .18f;
            RenderSettings.ambientLight = new Color(.24f, .28f, .30f);
            UpdateCamera();
        }
        private void BuildBodies()
        {
            foreach (Transform body in bodies.Values) if (body != null) Destroy(body.gameObject);
            bodies.Clear();
            foreach (AquariumAgent agent in world.Agents)
            {
                bool shrimp = agent.Profile.Kind == AgentKind.Shrimp;
                bool panda = agent.Profile.HasTag("bottom_sifter");
                GameObject body = Shape(agent.State.EntityId, PrimitiveType.Sphere, ToUnity(agent.State.Position),
                    shrimp ? new Vector3(.007f, .006f, .018f) : new Vector3(.012f, .018f, .035f),
                    shrimp ? shrimpMaterial : panda ? pandaMaterial : fishMaterial);
                bodies.Add(agent.State.EntityId, body.transform);
            }
        }
        private void Update()
        {
            // Time.unscaledDeltaTime: ecology compression never speeds up reactions or camera movement.
            float dt = Mathf.Min(Time.unscaledDeltaTime, .25f);
            approachTimer -= dt;
            if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;
            if (Input.GetKeyDown(KeyCode.T))
            {
                localization.CycleLanguage();
                PlayerPrefs.SetString(LanguagePreferenceKey, localization.CaptureLanguagePreference());
                PlayerPrefs.Save();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) world.SetEcologyRate(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) world.SetEcologyRate(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) world.SetEcologyRate(4);
            if (Input.GetKeyDown(KeyCode.F5)) Save();
            if (Input.GetKeyDown(KeyCode.F9)) Load();
            if (!paused)
            {
                if (Input.GetKeyDown(KeyCode.L)) ToggleLight();
                if (Input.GetKeyDown(KeyCode.G)) Approach();
                if (Input.GetKeyDown(KeyCode.F)) world.Feed(feedPoint);
                world.Advance(dt);
            }
            float scroll = Input.mouseScrollDelta.y;
            cameraDistance = Mathf.Clamp(cameraDistance - scroll * .035f, .35f, 1.3f);
            if (Input.GetMouseButton(1))
            {
                cameraYaw = Mathf.Clamp(cameraYaw + Input.GetAxis("Mouse X") * 2, -55, 55);
                cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * 2, -5, 45);
            }
            if (!paused && cameraDistance < .47f && scroll > 0 && approachTimer <= 0) Approach();
            UpdateCamera();
            foreach (AquariumAgent agent in world.Agents)
            {
                Transform body = bodies[agent.State.EntityId]; body.localPosition = ToUnity(agent.State.Position);
                Vector3 velocity = ToUnity(agent.State.Velocity);
                if (velocity.sqrMagnitude > .000001f)
                    body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.LookRotation(velocity), dt * 7);
                if (agent.State.Action == ActionKind.BottomProbe && Vec3.Distance(agent.State.Position, agent.State.Target) < .015)
                    body.localRotation = Quaternion.Euler(12 + 8 * Mathf.Sin((float)(world.RealSeconds * 12 + agent.State.Personality.Activity)), body.localEulerAngles.y, 0);
            }
            UpdateFoodMarkers();
        }
        private void ToggleLight()
        {
            world.SetLight(!world.LightIsOn);
            aquariumLight.intensity = world.LightIsOn ? 1.1f : .18f;
        }
        private void Approach()
        { world.ApproachGlass(new Vec3(0, .18, -.15)); approachTimer = 7; }
        private void UpdateCamera()
        {
            Vector3 target = new Vector3(0, .17f, 0);
            view.transform.localPosition = target + Quaternion.Euler(cameraPitch, cameraYaw, 0) * new Vector3(0, 0, -cameraDistance);
            view.transform.LookAt(transform.TransformPoint(target));
        }
        private void UpdateFoodMarkers()
        {
            HashSet<long> live = new HashSet<long>();
            foreach (StimulusState stimulus in world.Stimuli)
            {
                if (stimulus.Kind != StimulusKind.Food) continue;
                live.Add(stimulus.Id);
                if (!foodMarkers.ContainsKey(stimulus.Id))
                    foodMarkers.Add(stimulus.Id, Shape("Food source", PrimitiveType.Sphere, ToUnity(stimulus.Position), Vector3.one * .007f, foodMaterial).transform);
                foodMarkers[stimulus.Id].localPosition = ToUnity(stimulus.Position);
            }
            List<long> remove = new List<long>();
            foreach (KeyValuePair<long, Transform> pair in foodMarkers)
                if (!live.Contains(pair.Key)) { Destroy(pair.Value.gameObject); remove.Add(pair.Key); }
            foreach (long id in remove) foodMarkers.Remove(id);
        }
        private void Save()
        {
            try
            {
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(world.Capture(), true));
                if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".bak");
                else File.Move(temp, SavePath);
                statusKey = "demo.status.saved"; statusDetail = SavePath;
            }
            catch (Exception error) { statusKey = "demo.status.save_failed"; Debug.LogException(error); }
        }
        private void Load()
        {
            try
            {
                WorldSnapshot snapshot = JsonUtility.FromJson<WorldSnapshot>(File.ReadAllText(SavePath));
                world = AquariumWorld.Restore(snapshot, new TankBounds(), profiles);
                BuildBodies(); aquariumLight.intensity = world.LightIsOn ? 1.1f : .18f;
                statusKey = "demo.status.loaded";
            }
            catch (Exception error) { statusKey = "demo.status.load_failed"; Debug.LogException(error); }
        }
        private void OnGUI()
        {
            Font originalFont = GUI.skin.font;
            Font internationalFont = FontFor(localization.CurrentLocale);
            if (internationalFont != null) GUI.skin.font = internationalFont;
            GUI.Box(new Rect(12, 12, 700, 154), localization.Text("demo.title"));
            GUI.Label(new Rect(24, 36, 675, 24), localization.Text("demo.clock",
                world.RealSeconds, world.EcologyDays, world.EcologyRate,
                localization.Text(world.LightIsOn ? "demo.light.on" : "demo.light.off"),
                localization.Text(paused ? "demo.state.paused" : "demo.state.running")));
            GUI.Label(new Rect(24, 61, 675, 24), localization.Text("demo.controls.events"));
            GUI.Label(new Rect(24, 86, 675, 24), localization.Text("demo.controls.camera"));
            GUI.Label(new Rect(24, 111, 675, 46), localization.Text(statusKey, statusDetail));
            GUI.skin.font = originalFont;
        }
        private Font FontFor(LocaleMetadata locale)
        {
            string group = locale.font_group ?? "latin";
            Font cached;
            if (localeFonts.TryGetValue(group, out cached)) return cached;
            // Use only the bundled OFL face; never depend on an OS font installed on the machine.
            // It covers Latin and zh-CN. Other planned locales require separate glyph/layout QA.
            cached = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if (cached == null) throw new InvalidOperationException("Missing bundled font asset for font_group " + group + ".");
            localeFonts.Add(group, cached); return cached;
        }
        private void OnDestroy()
        { localeFonts.Clear(); }
        private static Vector3 ToUnity(Vec3 v) { return new Vector3((float)v.X, (float)v.Y, (float)v.Z); }
    }
}
