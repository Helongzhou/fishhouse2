using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Aquarium.Core;
using Aquarium.Core.Configuration;
using Aquarium.Core.Gameplay;
using Aquarium.Core.Session;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aquarium.UnityAdapter
{
    // Geometry is a true 3D placeholder. Hardscape navigation and authored species art remain separate work.
    public sealed class AquariumGameEntry : MonoBehaviour
    {
        private RuntimeCatalog catalog;
        private GameSession session;
        private readonly DemoLocalization text = new DemoLocalization();
        private readonly Dictionary<string, Transform> animals = new Dictionary<string, Transform>();
        private readonly Dictionary<long, Transform> foods = new Dictionary<long, Transform>();
        private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        private readonly List<Material> materials = new List<Material>();
        private Transform root, layoutRoot;
        private Mesh substrateMesh;
        private Camera view;
        private Light lamp;
        private Material stone, green, sand, metal, ember, neon, shrimp, food, selectedMaterial, waterMaterial;
        private string selectedBatch, selectedPlacement, statusKey = "game.ready";
        private object[] statusArgs = new object[0];
        private string status {get{return statusKey;}set{statusKey=value;statusArgs=new object[0];}}
        private Vector2 shopScroll;
        private Vector3 mouseDown;
        private float yaw, pitch = 17, distance = .88f;
        private float fl = .015f, fr = .015f, bl = .045f, br = .045f;
        private bool placing;
        private bool applicationPaused, applicationFocused = true, skipResumeFrame;
        private double approachCooldown;
        private GUISkin uiSkin;
        private GUIStyle productButtonStyle;
        private string loggedUiLayout;
        public bool EnableDevelopmentQaLocales;
        private const float DesignWidth = 1280f, DesignPanelWidth = 288f;
        private const string LanguageKey = "aquarium.language";
        private string SavePath { get { return Path.Combine(Application.persistentDataPath, "aquarium-session-v1.json"); } }
        private static string Tx() { return Guid.NewGuid().ToString("N"); }
        private string T(string key, params object[] args) { return text.Text(key, args); }
        private void Awake()
        {
            ResourceCatalogLoader.LoadAvailable(text, EnableDevelopmentQaLocales);
            text.ResolveStartupLanguage(PlayerPrefs.GetString(LanguageKey, ""), null, CultureInfo.CurrentUICulture.Name);
            TextAsset resource = Resources.Load<TextAsset>("Configuration/runtime");
            if (resource == null) throw new InvalidOperationException("Missing generated runtime configuration. Run tools/build_runtime_config.py.");
            catalog = JsonUtility.FromJson<RuntimeCatalog>(resource.text); catalog.Validate(); session = new GameSession(catalog, SessionMode.Prototype);
            root = new GameObject("Aquarium 3D").transform; root.SetParent(transform, false);
            stone = Material(new Color(.23f,.26f,.24f)); green = Material(new Color(.18f,.43f,.23f)); sand = Material(new Color(.48f,.42f,.29f));
            metal = Material(new Color(.15f,.19f,.20f)); ember = Material(new Color(.91f,.36f,.13f)); neon = Material(new Color(.12f,.62f,.91f));
            shrimp = Material(new Color(.89f,.13f,.09f)); food = Material(new Color(.91f,.70f,.23f));
            selectedMaterial=Material(new Color(.52f,.62f,.36f));
            Material waterAsset=GraphicsSettings.currentRenderPipeline==null?null:Resources.Load<Material>("Rendering/AquariumWater");
            waterMaterial=waterAsset==null?Material(new Color(.13f,.43f,.50f,.22f)):new Material(waterAsset){color=new Color(.13f,.43f,.50f,.22f)};
            if(waterAsset!=null)materials.Add(waterMaterial);
            BuildTank(); RebuildLayout();
        }
        private Material Material(Color color)
        {
            Shader shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("No compatible aquarium shader was imported.");
            Material basis=GraphicsSettings.currentRenderPipeline==null?null:Resources.Load<Material>("Rendering/AquariumOpaque");
            var material = basis==null?new Material(shader) { color = color }:new Material(basis){color=color}; materials.Add(material); return material;
        }
        private GameObject Shape(Transform parent, string name, PrimitiveType kind, Vector3 position, Vector3 size, Material material, bool collider = false)
        {
            GameObject go = GameObject.CreatePrimitive(kind); go.name = name; go.transform.SetParent(parent,false); go.transform.localPosition = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) { Collider c=go.GetComponent<Collider>(); if(c!=null)Destroy(c); }
            return go;
        }
        private void BuildTank()
        {
            Shape(root,"Tank base",PrimitiveType.Cube,new Vector3(0,-.008f,0),new Vector3(.61f,.016f,.31f),Material(new Color(.38f,.43f,.44f)));
            Shape(root,"Tank back",PrimitiveType.Cube,new Vector3(0,.18f,.158f),new Vector3(.61f,.36f,.008f),Material(new Color(.16f,.25f,.28f)));
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Shape(root,"Glass edge",PrimitiveType.Cube,new Vector3(x*.3f,.18f,z*.15f),new Vector3(.002f,.36f,.002f),Material(new Color(.44f,.53f,.55f)));
            var cameraObject=new GameObject("Aquarium camera");cameraObject.transform.SetParent(root,false);view=cameraObject.AddComponent<Camera>();
            view.nearClipPlane=.01f;view.farClipPlane=10;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.065f,.10f,.115f);
            var lightObject=new GameObject("Aquarium light");lightObject.transform.SetParent(root,false);lamp=lightObject.AddComponent<Light>();lamp.type=LightType.Directional;
            lightObject.transform.localRotation=Quaternion.Euler(45,-35,0);lamp.intensity=.55f;
            // Room illumination is presentation-only: a dry or unlit tank must remain usable.
            // Set SH explicitly because runtime geometry has no baked probes or skybox lighting.
            Color roomLight=new Color(.42f,.46f,.48f);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=roomLight;
            var roomProbe=new SphericalHarmonicsL2();roomProbe.AddAmbientLight(roomLight.linear);
            RenderSettings.ambientProbe=roomProbe;RenderSettings.sun=lamp;
            UpdateCamera();
        }
        private void RebuildLayout()
        {
            if(substrateMesh!=null){Destroy(substrateMesh);substrateMesh=null;}
            if(layoutRoot!=null){layoutRoot.gameObject.SetActive(false);Destroy(layoutRoot.gameObject);}
            layoutRoot=new GameObject("Placed aquascape").transform;layoutRoot.SetParent(root,false);
            GameplaySnapshot state=session.Gameplay.Capture();
            if(state.Layout.Substrate!=null)BuildSubstrate(state.Layout.Substrate);
            foreach(LayoutPlacement p in state.Layout.Placements)
            {
                ItemConfig item=catalog.Item(Batch(p.PurchaseBatchId).ItemId);
                GameObject go=Shape(layoutRoot,p.Id,item.ItemKind=="plant"?PrimitiveType.Sphere:PrimitiveType.Cube,
                    U(p.Position)+(item.ItemKind=="plant"?Vector3.up*(float)p.Size.Y/2:Vector3.zero),U(p.Size),item.ItemKind=="plant"?green:item.ItemKind=="equipment"?metal:stone,true);
                go.transform.localRotation=Quaternion.Euler(0,(float)p.YawDegrees,0);
                var handle=go.AddComponent<AquariumPlacementHandle>();handle.PlacementId=p.Id;handle.BatchId=p.PurchaseBatchId;
                if(p.Id==selectedPlacement)go.GetComponent<Renderer>().sharedMaterial=selectedMaterial;
            }
            if(state.Filled)
            {
                // A thin translucent surface communicates waterline without filling the view with an opaque box.
                Material water=waterMaterial;
                if(water.HasProperty("_Surface")){water.SetFloat("_Surface",1);water.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);water.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);water.SetFloat("_ZWrite",0);water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");}
                else {water.SetFloat("_Mode",3);water.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);water.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);water.SetInt("_ZWrite",0);water.EnableKeyword("_ALPHABLEND_ON");}
                water.renderQueue=3000;Shape(layoutRoot,"Water surface",PrimitiveType.Cube,new Vector3(0,(float)state.WaterlineHeight,0),new Vector3(.596f,.001f,.296f),water);
            }
        }
        private void BuildSubstrate(SubstrateProfile profile)
        {
            GameObject go=new GameObject("Sloped substrate");go.transform.SetParent(layoutRoot,false);
            // Match the gameplay bilinear surface; a single quad's two triangles disagree at saddle slopes.
            const int columns=16, rows=8;
            Vector3[] vertices=new Vector3[(columns+1)*(rows+1)];int[] triangles=new int[columns*rows*6];
            double width=session.World.Tank.Width, depth=session.World.Tank.Depth;
            for(int z=0;z<=rows;z++)for(int x=0;x<=columns;x++)
            {
                double u=(double)x/columns,v=(double)z/rows;
                double height=(profile.FrontLeftHeight*(1-u)+profile.FrontRightHeight*u)*(1-v)+(profile.BackLeftHeight*(1-u)+profile.BackRightHeight*u)*v;
                vertices[z*(columns+1)+x]=new Vector3((float)((u-.5)*width),(float)height,(float)((v-.5)*depth));
            }
            int index=0;for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
            {int a=z*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;triangles[index++]=a;triangles[index++]=c;triangles[index++]=b;triangles[index++]=b;triangles[index++]=c;triangles[index++]=d;}
            Mesh mesh=new Mesh();substrateMesh=mesh;mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=sand;go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        private void Update()
        {
            if(session==null)return;
            double elapsed=Time.unscaledDeltaTime;
            if(applicationPaused||!applicationFocused){UpdateAnimals();UpdateFood();return;}
            if(skipResumeFrame){elapsed=0;skipResumeFrame=false;}
            AdvanceSimulation(elapsed);approachCooldown-=elapsed;
            if(Input.GetKeyDown(KeyCode.Space))session.SetPaused(!session.Paused);
            if(Input.GetKeyDown(KeyCode.F5))Save();if(Input.GetKeyDown(KeyCode.F9))Load();
            if(Input.GetKeyDown(KeyCode.F))Apply(session.Feed(Tx(),new Vec3(0,.29,0)),false);
            if(Input.GetKeyDown(KeyCode.L))Apply(session.SetLight(Tx(),!session.World.LightIsOn),false);
            if(Input.GetKeyDown(KeyCode.R))RotateSelected();
            if(Input.GetKeyDown(KeyCode.Escape)){placing=false;selectedBatch=null;selectedPlacement=null;RebuildLayout();}
            bool inView=Input.mousePosition.x>PanelPixels();
            float scroll=inView?Input.mouseScrollDelta.y:0;
            distance=Mathf.Clamp(distance-scroll*.035f,.36f,1.3f);
            if(inView&&Input.GetMouseButton(1)){yaw=Mathf.Clamp(yaw+Input.GetAxis("Mouse X")*2,-55,55);pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-4,48);}
            if(!session.Paused&&distance<.47f&&scroll>0&&approachCooldown<=0){Apply(session.ApproachGlass(Tx(),new Vec3(0,.18,-.15)),false);approachCooldown=7;}
            UpdateCamera();
            if(inView&&Input.GetMouseButtonDown(0))
            {
                mouseDown=Input.mousePosition;
                if(placing){PlaceAtCursor();}
                else
                {
                    RaycastHit hit;if(Physics.Raycast(view.ScreenPointToRay(Input.mousePosition),out hit,4))
                    {var handle=hit.collider.GetComponent<AquariumPlacementHandle>();if(handle!=null){selectedPlacement=handle.PlacementId;selectedBatch=handle.BatchId;RebuildLayout();}}
                }
            }
            if(inView&&Input.GetMouseButtonUp(0)&&!placing&&selectedPlacement!=null&&(Input.mousePosition-mouseDown).sqrMagnitude>25)PlaceAtCursor();
            UpdateAnimals();UpdateFood();lamp.intensity=session.World.LightIsOn?1.15f:.55f;
        }
        private void OnApplicationPause(bool paused)
        { applicationPaused = paused; if (!paused) skipResumeFrame = true; }
        private void OnApplicationFocus(bool focused)
        { applicationFocused = focused; if (focused) skipResumeFrame = true; }
        // Host frame time is not a simulation speed limit. Long frames use the same real-second
        // timeline as short frames; this never reads UTC or applies time while the game is closed.
        public void AdvanceSimulation(double elapsedRealSeconds)
        {
            if (!Scalar.IsFinite(elapsedRealSeconds) || elapsedRealSeconds < 0 || elapsedRealSeconds > 3600) throw new ArgumentOutOfRangeException("elapsedRealSeconds");
            if (session == null) throw new InvalidOperationException("The aquarium has not initialized.");
            double remaining = elapsedRealSeconds;
            while (remaining > 0)
            {
                double step = Math.Min(60, remaining);
                session.Advance(step); remaining -= step;
            }
        }
        private void UpdateCamera()
        {
            float left = PanelPixels() / Mathf.Max(1, Screen.width);
            view.rect=new Rect(left,0,1-left,1);
            Vector3 target=new Vector3(0,.17f,0);view.transform.localPosition=target+Quaternion.Euler(pitch,yaw,0)*new Vector3(0,0,-distance);view.transform.LookAt(root.TransformPoint(target));
        }
        private float PanelWidth()
        { return Screen.width<=DesignWidth?DesignPanelWidth:Mathf.Clamp(Screen.width*.25f,DesignPanelWidth,960f); }
        private float PanelPixels()
        { return 12f+PanelWidth()+12f; }
        private int UiFontSize()
        { return Screen.width<=DesignWidth?14:Mathf.Clamp(Mathf.RoundToInt(14f*(1f+.35f*(Screen.width/DesignWidth-1f))),14,24); }
        private void UpdateAnimals()
        {
            var live=new HashSet<string>();
            foreach(AquariumAgent agent in session.World.Agents)
            {
                string id=agent.State.EntityId;live.Add(id);Transform body;
                if(!animals.TryGetValue(id,out body))
                {
                    bool isShrimp=agent.Profile.Kind==AgentKind.Shrimp;
                    body=Shape(root,id,PrimitiveType.Sphere,U(agent.State.Position),isShrimp?new Vector3(.007f,.006f,.018f):new Vector3(.011f,.015f,.032f),isShrimp?shrimp:agent.State.SpeciesId=="ember_tetra"?ember:neon).transform;animals.Add(id,body);
                }
                Vector3 displayPosition=U(agent.State.Position);if(agent.Profile.Kind==AgentKind.Shrimp)displayPosition.y=Mathf.Max(displayPosition.y,(float)session.Gameplay.GetSubstrateHeight(agent.State.Position.X,agent.State.Position.Z)+.003f);
                body.localPosition=displayPosition;Vector3 velocity=U(agent.State.Velocity);
                if(velocity.sqrMagnitude>.000001f)body.localRotation=Quaternion.Slerp(body.localRotation,Quaternion.LookRotation(velocity),Time.unscaledDeltaTime*7);
            }
            var remove=new List<string>();foreach(var pair in animals)if(!live.Contains(pair.Key)){Destroy(pair.Value.gameObject);remove.Add(pair.Key);}foreach(string id in remove)animals.Remove(id);
        }
        private void UpdateFood()
        {
            var live=new HashSet<long>();foreach(StimulusState s in session.World.Stimuli)if(s.Kind==StimulusKind.Food)
            {live.Add(s.Id);if(!foods.ContainsKey(s.Id))foods.Add(s.Id,Shape(root,"Food",PrimitiveType.Sphere,U(s.Position),Vector3.one*.006f,food).transform);Vector3 displayFood=U(s.Position);displayFood.y=Mathf.Max(displayFood.y,(float)session.Gameplay.GetSubstrateHeight(s.Position.X,s.Position.Z)+.003f);foods[s.Id].localPosition=displayFood;}
            var remove=new List<long>();foreach(var p in foods)if(!live.Contains(p.Key)){Destroy(p.Value.gameObject);remove.Add(p.Key);}foreach(long id in remove)foods.Remove(id);
        }
        private PurchaseBatch Batch(string id){foreach(PurchaseBatch b in session.Gameplay.Capture().Batches)if(b.Id==id)return b;return null;}
        private LayoutPlacement Placement(string id){foreach(LayoutPlacement p in session.Gameplay.Capture().Layout.Placements)if(p.Id==id)return p;return null;}
        private static Vector3 U(Vec3 v){return new Vector3((float)v.X,(float)v.Y,(float)v.Z);}
        private void Apply(CommandResult result,bool rebuild=true)
        {status=result.Code;statusArgs=new object[result.Args.Length];for(int i=0;i<result.Args.Length;i++){double number;if(double.TryParse(result.Args[i],NumberStyles.Float,CultureInfo.InvariantCulture,out number))statusArgs[i]=number;else statusArgs[i]=result.Args[i];}if(result.Success&&rebuild)RebuildLayout();}
        private Vec3 AssetSize(ItemConfig item)
        {
            if(item.ItemId=="stone_main")return new Vec3(.09,.11,.07);if(item.ItemId=="stone_secondary")return new Vec3(.06,.05,.05);
            if(item.ItemId=="wood_optional")return new Vec3(.10,.07,.04);if(item.ItemId=="filter_hangon_basic")return new Vec3(.04,.06,.04);if(item.ItemId=="light_low")return new Vec3(.06,.015,.15);
            return item.ItemId=="plant_java_fern"?new Vec3(.02,.05,.02):item.ItemId=="plant_java_moss"?new Vec3(.025,.015,.025):new Vec3(.02,.035,.02);
        }
        private void PlaceAtCursor()
        {
            PurchaseBatch batch=Batch(selectedBatch);if(batch==null)return;ItemConfig item=catalog.Item(batch.ItemId);if(item.ItemKind=="fish"||item.ItemKind=="shrimp")return;
            if(item.ItemKind=="substrate"){ApplySlope(batch.Id);return;}
            LayoutPlacement old=Placement(selectedPlacement);Vec3 size=old==null?AssetSize(item):old.Size;double rotation=old==null?0:old.YawDegrees;
            Ray ray=view.ScreenPointToRay(Input.mousePosition);Vec3 position;string host=null;
            if(item.ItemKind=="plant")
            {
                RaycastHit[] hits=Physics.RaycastAll(ray,4);Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));LayoutPlacement support=null;
                foreach(RaycastHit hit in hits){var handle=hit.collider.GetComponent<AquariumPlacementHandle>();if(handle==null)continue;var p=Placement(handle.PlacementId);if(p!=null&&catalog.Item(Batch(p.PurchaseBatchId).ItemId).ItemKind=="hardscape"){support=p;break;}}
                if(support==null){status="gameplay.support_required";return;}
                host=support.Id;position=new Vec3(support.Position.X,support.Position.Y+support.Size.Y/2,support.Position.Z);
            }
            else
            {
                float enter;Plane plane=new Plane(Vector3.up,Vector3.zero);if(!plane.Raycast(ray,out enter))return;Vector3 point=ray.GetPoint(enter);position=new Vec3(point.x,0,point.z);
                position.Y=item.ItemKind=="equipment"?(item.ItemId=="light_low"?.33:.28):session.Gameplay.SuggestedBottomHeight(position,size,rotation)+size.Y/2;
            }
            string id=old==null?"layout:"+batch.Id:old.Id;CommandResult result=session.Place(Tx(),new LayoutPlacement{Id=id,PurchaseBatchId=batch.Id,Position=position,Size=size,YawDegrees=rotation,SupportPlacementId=host});
            if(result.Success){selectedPlacement=id;placing=false;}Apply(result);
        }
        private void RotateSelected()
        {
            LayoutPlacement p=Placement(selectedPlacement);if(p==null)return;ItemConfig item=catalog.Item(Batch(p.PurchaseBatchId).ItemId);double rotation=p.YawDegrees+15;
            Vec3 pos=p.Position;if(item.ItemKind=="hardscape")pos.Y=session.Gameplay.SuggestedBottomHeight(pos,p.Size,rotation)+p.Size.Y/2;
            Apply(session.Move(Tx(),p.Id,pos,rotation,p.SupportPlacementId));
        }
        private void ApplySlope(string batch)
        {Apply(session.SetSubstrate(Tx(),new SubstrateProfile{PurchaseBatchId=batch,FrontLeftHeight=fl,FrontRightHeight=fr,BackLeftHeight=bl,BackRightHeight=br}));placing=false;}
        private void SelectBatch(PurchaseBatch batch)
        {
            selectedBatch=batch.Id;selectedPlacement=null;foreach(LayoutPlacement p in session.Gameplay.Capture().Layout.Placements)if(p.PurchaseBatchId==batch.Id)selectedPlacement=p.Id;
            string kind=catalog.Item(batch.ItemId).ItemKind;placing=selectedPlacement==null&&kind!="fish"&&kind!="shrimp";status=kind=="fish"||kind=="shrimp"?"gameplay.ok":kind=="plant"?"game.place_plant":"game.place";RebuildLayout();
        }
        private void Template()
        {
            // Places only already purchased items through the same validated commands used by direct input.
            var batches=new Dictionary<string,PurchaseBatch>();foreach(PurchaseBatch b in session.Gameplay.Capture().Batches)if(!b.Refunded&&!b.Sold&&!batches.ContainsKey(b.ItemId))batches.Add(b.ItemId,b);
            PurchaseBatch substrate;if(!batches.TryGetValue("substrate_natural_sand",out substrate)){status="gameplay.required_deployment_missing";return;}ApplySlope(substrate.Id);
            foreach(string itemId in new[]{"stone_main","stone_secondary","filter_hangon_basic","light_low","plant_anubias_nana","plant_java_fern","plant_java_moss"})
            {
                PurchaseBatch b;if(!batches.TryGetValue(itemId,out b))continue;ItemConfig item=catalog.Item(itemId);Vec3 size=AssetSize(item);Vec3 pos;string host=null;
                if(item.ItemKind=="plant")
                {
                    PurchaseBatch supportBatch;if(!batches.TryGetValue(itemId=="plant_java_moss"?"stone_secondary":"stone_main",out supportBatch))continue;
                    LayoutPlacement support=Placement("layout:"+supportBatch.Id);if(support==null)continue;host=support.Id;
                    pos=new Vec3(support.Position.X+(itemId=="plant_anubias_nana"?-.02:itemId=="plant_java_fern"?.02:0),support.Position.Y+support.Size.Y/2,support.Position.Z);
                }
                else{pos=itemId=="stone_main"?new Vec3(-.12,0,.02):itemId=="stone_secondary"?new Vec3(.09,0,-.01):itemId=="filter_hangon_basic"?new Vec3(.24,.28,.11):new Vec3(-.20,.33,0);if(item.ItemKind=="hardscape")pos.Y=session.Gameplay.SuggestedBottomHeight(pos,size,0)+size.Y/2;}
                Apply(session.Place(Tx(),new LayoutPlacement{Id="layout:"+b.Id,PurchaseBatchId=b.Id,Position=pos,Size=size,SupportPlacementId=host}));
            }
            placing=false;
        }
        private AtomicSaveStore Store()
        {return new AtomicSaveStore(SavePath,payload=>{try{GameSession.Restore(catalog,JsonUtility.FromJson<SessionSnapshot>(payload),SessionMode.Prototype);return true;}catch{return false;}});}
        private void Save(){try{Store().Save(JsonUtility.ToJson(session.Capture(),true));status="ui.save_ok";}catch(Exception e){status="ui.save_error";Debug.LogException(e);}}
        private void Load()
        {try{string source;string payload=Store().Load(out source);GameSession candidate=GameSession.Restore(catalog,JsonUtility.FromJson<SessionSnapshot>(payload));session=candidate;selectedBatch=selectedPlacement=null;placing=false;RebuildLayout();status=source==SavePath?"game.loaded":"game.recovered";}catch(Exception e){status="ui.load_error";Debug.LogException(e);}}
        private void OnGUI()
        {
            if(session==null)return;GUISkin priorSkin=GUI.skin;float panelWidth=PanelWidth();
            if(uiSkin==null)uiSkin=Instantiate(priorSkin);
            if(productButtonStyle==null)productButtonStyle=new GUIStyle(uiSkin.button);
            uiSkin.font=FontFor();int fontSize=UiFontSize();
            foreach(GUIStyle style in new[]{uiSkin.label,uiSkin.button,uiSkin.box,productButtonStyle})
            {style.font=uiSkin.font;style.fontSize=fontSize;style.fixedHeight=0;style.wordWrap=true;style.padding.top=6;style.padding.bottom=6;}
            productButtonStyle.alignment=TextAnchor.MiddleLeft;
            GUI.skin=uiSkin;
            string layoutKey=Screen.width+"x"+Screen.height+":"+text.Language;
            if(loggedUiLayout!=layoutKey && Event.current.type==EventType.Repaint)
            {
                loggedUiLayout=layoutKey;
                Debug.Log("AQUARIUM_UI_LAYOUT "+layoutKey+" panel="+panelWidth+" cameraLeft="+(PanelPixels()/Screen.width)+" font="+uiSkin.font.name+" fontSize="+fontSize+" labelLineHeight="+uiSkin.label.lineHeight+" fixedHeight="+uiSkin.label.fixedHeight+" wordWrap="+uiSkin.label.wordWrap+" matrix="+GUI.matrix);
            }
            try
            {
            GUILayout.BeginArea(new Rect(12,12,panelWidth,Screen.height-24),GUI.skin.box);
            UiLabel(T("game.title"));UiLabel(T("game.prototype"));UiLabel(T("ui.wallet",session.Gameplay.WalletCoin));
            GUILayout.BeginHorizontal();if(GUILayout.Button(T(session.Paused?"ui.resume":"ui.pause")))session.SetPaused(!session.Paused);
            foreach(int rate in new[]{1,2,4})if(GUILayout.Button(T("game.rate",rate)))session.SetEcologyRate(rate);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(GUILayout.Button(T("ui.save")))Save();if(GUILayout.Button(T("ui.load")))Load();if(GUILayout.Button(T("ui.language"))){text.CycleLanguage();PlayerPrefs.SetString(LanguageKey,text.CaptureLanguagePreference());PlayerPrefs.Save();}GUILayout.EndHorizontal();
            GameplaySnapshot guiState=session.Gameplay.Capture();
            shopScroll=GUILayout.BeginScrollView(shopScroll);
            foreach(ItemConfig item in catalog.Items)if(item.ContentStatus=="shipping"&&item.ItemKind!="care"&&item.ItemId!="wood_optional")
            {
                PurchaseBatch owned=null;int count=0;foreach(PurchaseBatch b in guiState.Batches)if(b.ItemId==item.ItemId&&!b.Refunded&&!b.Sold){owned=b;count++;}
                GUIContent productLabel=new GUIContent(T("game.product",T("item."+item.ItemId+".name"),count)),priceLabel=new GUIContent(T("ui.price",item.PurchasePriceCoin));
                float priceWidth=Mathf.Ceil(uiSkin.button.CalcSize(priceLabel).x)+4;
                float productWidth=Mathf.Max(80,panelWidth-uiSkin.box.padding.horizontal-uiSkin.verticalScrollbar.fixedWidth-12-priceWidth-productButtonStyle.margin.horizontal-uiSkin.button.margin.horizontal);
                GUILayout.BeginHorizontal();if(GUILayout.Button(productLabel,productButtonStyle,GUILayout.Width(productWidth),GUILayout.Height(Mathf.Ceil(productButtonStyle.CalcHeight(productLabel,productWidth))))&&owned!=null)SelectBatch(owned);
                bool oldEnabled=GUI.enabled;GUI.enabled=!session.Paused&&(session.Gameplay.Stage==GameplayStage.Editing||item.ItemKind=="fish"||item.ItemKind=="shrimp");
                if(GUILayout.Button(priceLabel,GUILayout.Width(priceWidth)))
                {CommandResult result=session.Buy(Tx(),item.ItemId);Apply(result);if(result.Success&&item.ItemKind!="fish"&&item.ItemKind!="shrimp")SelectBatch(Batch(result.BatchId));}
                GUI.enabled=oldEnabled;GUILayout.EndHorizontal();
            }
            if(session.Gameplay.Stage==GameplayStage.Editing)
            {
                if(GUILayout.Button(T("game.template")))Template();
            }
            GUILayout.BeginHorizontal();if(GUILayout.Button(T("ui.undo")))Apply(session.Undo(Tx()));if(GUILayout.Button(T("ui.redo")))Apply(session.Redo(Tx()));GUILayout.EndHorizontal();
            PurchaseBatch selected=Batch(selectedBatch);
            if(selected!=null)
            {
                GUILayout.Space(6);UiLabel(T("item."+selected.ItemId+".name"));
                if(catalog.Item(selected.ItemId).ItemKind=="substrate"&&session.Gameplay.Stage==GameplayStage.Editing)
                {
                    UiLabel(T("game.front"));GUILayout.BeginHorizontal();fl=GUILayout.HorizontalSlider(fl,.005f,.055f);fr=GUILayout.HorizontalSlider(fr,.005f,.055f);GUILayout.EndHorizontal();
                    UiLabel(T("game.back"));GUILayout.BeginHorizontal();bl=GUILayout.HorizontalSlider(bl,.005f,.055f);br=GUILayout.HorizontalSlider(br,.005f,.055f);GUILayout.EndHorizontal();
                    if(GUILayout.Button(T("game.apply_slope")))ApplySlope(selected.Id);
                }
                else if(catalog.Item(selected.ItemId).ItemKind!="fish"&&catalog.Item(selected.ItemId).ItemKind!="shrimp")
                {GUILayout.BeginHorizontal();if(GUILayout.Button(T("game.move"))){placing=true;status="game.place";}if(GUILayout.Button(T("game.rotate")))RotateSelected();GUILayout.EndHorizontal();}
                if(session.Gameplay.Stage==GameplayStage.Editing){if(GUILayout.Button(T("game.refund")))Apply(session.Refund(Tx(),selected.Id));}
                else if(session.Gameplay.Stage==GameplayStage.Complete){if(GUILayout.Button(T("ui.sell")))Apply(session.Sell(Tx(),selected.Id));}
            }
            GUILayout.EndScrollView();
            UiLabel(T("game.stage."+session.Gameplay.Stage));
            GUI.enabled=!session.Paused;
            if(session.Gameplay.Stage==GameplayStage.Editing){if(GUILayout.Button(T("ui.confirm")))Apply(session.ConfirmLayout(Tx()));}
            else if(session.Gameplay.Stage==GameplayStage.Confirmed){if(GUILayout.Button(T("guide.fill")))Apply(session.Fill(Tx()));}
            else if(session.Gameplay.Stage==GameplayStage.Filled||session.Gameplay.Stage==GameplayStage.Cycling){if(GUILayout.Button(T("game.challenge")))Apply(session.BeginChallenge(Tx()));}
            if(session.Gameplay.Filled)
            {
                GUILayout.BeginHorizontal();if(GUILayout.Button(T("ui.feed")))Apply(session.Feed(Tx(),new Vec3(0,.29,0)),false);if(GUILayout.Button(T("ui.water_change")))Apply(session.ChangeWater(Tx(),session.Ecology.VolumeLiters*.2),false);GUILayout.EndHorizontal();
                if(GUILayout.Button(T(session.World.LightIsOn?"ui.light_off":"ui.light_on")))Apply(session.SetLight(Tx(),!session.World.LightIsOn),false);
                if(GUILayout.Button(T("ui.retry")))Apply(session.Retry(Tx()));
            }
            GUI.enabled=true;UiLabel(T(status,statusArgs));GUILayout.EndArea();
            float overlayWidth=Mathf.Max(260,Screen.width-panelWidth-40);
            GUILayout.BeginArea(new Rect(panelWidth+28,16,overlayWidth,Mathf.Max(160,UiFontSize()*8)));
            UiLabel(T("game.clock",session.World.EcologyDays,session.EcologyRate,session.Gameplay.CalculateEffectiveWaterLiters()),overlayWidth);
            if(session.Gameplay.Filled)UiLabel(T("game.water",session.Ecology.TanMgNPerLiter,session.Ecology.NitriteMgNPerLiter,session.Ecology.NitrateMgNPerLiter,T(session.Ecology.WaterIsStable?"ui.stable":"ui.needs_attention")),overlayWidth);
            UiLabel(T(placing?"game.place":"game.camera"),overlayWidth);GUILayout.EndArea();
            }
            finally { GUI.enabled=true;GUI.skin=priorSkin; }
        }
        private void UiLabel(string content,float width=0)
        {
            float available=width>0?width:PanelWidth()-36;
            float height=Mathf.Ceil(Mathf.Max(uiSkin.label.lineHeight+uiSkin.label.padding.vertical,uiSkin.label.CalcHeight(new GUIContent(content),available)));
            GUILayout.Label(content,uiSkin.label,GUILayout.MinHeight(height));
        }
        private Font FontFor()
        {
            string group=text.CurrentLocale.font_group??"latin";Font font;if(fonts.TryGetValue(group,out font))return font;
            // Every shipped locale uses the bundled OFL Noto Sans CJK SC face. It covers Latin and zh-CN;
            // planned locales retain their metadata but need separate glyph/layout QA before release.
            font=Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
            if(font==null)throw new InvalidOperationException("Missing bundled font asset for font_group "+group+".");
            fonts.Add(group,font);return font;
        }
        private void OnDestroy(){if(substrateMesh!=null)Destroy(substrateMesh);foreach(Material m in materials)if(m!=null)Destroy(m);if(uiSkin!=null)Destroy(uiSkin);fonts.Clear();}
    }
}
