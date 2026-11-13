#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Aquarium.Core;
using Aquarium.Core.Configuration;
using Aquarium.Core.Ecology;
using Aquarium.Core.Gameplay;
using Aquarium.Core.Session;
using Aquarium.UnityAdapter;
using Aquarium.UnityEditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Aquarium.Validation
{
    [Serializable] public sealed class AcceptanceCheck { public string name, detail; public bool passed; }
    [Serializable] public sealed class UnityAcceptanceReport
    {
        public string status = "running", mode, projectPath, expectedUnityVersion, expectedUrpVersion, unityVersion, urpVersion, startedUtc, finishedUtc, failure;
        public string coverage = "Real Unity APIs and production domain/adapter calls. No human input, visual quality, Windows player, font-layout or Steam certification.";
        public List<AcceptanceCheck> checks = new List<AcceptanceCheck>();
        public List<SnapshotComparison> snapshotComparisons = new List<SnapshotComparison>();
        public List<string> startupObservations = new List<string>();
        public List<string> errors = new List<string>(), warnings = new List<string>();
    }

    // Invoked only in an explicitly scheduled, exclusive Unity editor run.
    [InitializeOnLoad]
    public static class UnitySliceAcceptance
    {
        private const string ScenePath = "Assets/_Game/Scenes/Aquarium.unity";
        private const string ActiveKey = "aquarium.validation.play.active", ReportKey = "aquarium.validation.report";
        private const string PhaseKey = "aquarium.validation.play.phase", StartedKey = "aquarium.validation.play.started";
        private static UnityAcceptanceReport report;
        private static string reportPath;
        private static int sequence, playFrames;
        private static double playStarted;
        private static bool runtimeChecked;
        private static bool startupDelayCompleted;
        private static int stableStartupUpdates;
        private static string lastStartupState;
        static UnitySliceAcceptance()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            reportPath = SessionState.GetString(ReportKey, "");
            if (File.Exists(reportPath)) report = JsonUtility.FromJson<UnityAcceptanceReport>(File.ReadAllText(reportPath));
            Subscribe();
            if (!double.TryParse(SessionState.GetString(StartedKey, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out playStarted))
                playStarted = EditorApplication.timeSinceStartup;
            if (SessionState.GetString(PhaseKey, "play") == "startup") ScheduleStartupDelay();
            EditorApplication.update += PlayUpdate;
        }
        public static void RunBatch()
        {
            Begin("editor-batch");
            try
            {
                PrepareAndInspect(); RuntimeCatalog catalog = LoadCatalog();
                ValidateJsonAndRoute(catalog); ValidateLocales();
                Check(report.errors.Count == 0, "no Unity errors/exceptions", "Error/Assert/Exception log callback remains empty.");
                Finish(true);
            }
            catch (Exception error) { Finish(false, error); if (Application.isBatchMode) EditorApplication.Exit(1); else throw; }
        }
        public static void RunPlaySmoke()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Play smoke is a scheduled batch-only entry point.");
            Begin("editor-play-smoke");
            try
            {
                PrepareAndInspect();
                SessionState.SetString(ReportKey, reportPath); SessionState.SetBool(ActiveKey, true);
                SessionState.SetString(PhaseKey, "startup");
                playStarted = EditorApplication.timeSinceStartup;
                SessionState.SetString(StartedKey, playStarted.ToString("R", CultureInfo.InvariantCulture));
                ScheduleStartupDelay(); Persist();
                EditorApplication.update -= PlayUpdate; EditorApplication.update += PlayUpdate;
            }
            catch (Exception error)
            { SessionState.SetBool(ActiveKey, false); EditorApplication.update -= PlayUpdate; Finish(false, error); EditorApplication.Exit(1); }
        }
        private static string Argument(string name)
        { string[] args = Environment.GetCommandLineArgs(); for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1]; return null; }
        private static void Begin(string mode)
        {
            string projectName = new DirectoryInfo(Application.dataPath).Parent.Name;
            reportPath = Argument("-aquariumReport") ?? Path.GetFullPath(Path.Combine(Application.dataPath, "../../verification/unity/" + projectName + "/" + mode + ".json"));
            report = new UnityAcceptanceReport { mode = mode, projectPath = new DirectoryInfo(Application.dataPath).Parent.FullName, unityVersion = Application.unityVersion,
                startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) };
            Subscribe(); Persist();
        }
        private static void Subscribe()
        {
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
            AssemblyReloadEvents.beforeAssemblyReload -= PersistActiveReport;
            AssemblyReloadEvents.beforeAssemblyReload += PersistActiveReport;
        }
        private static void PersistActiveReport()
        { if (report != null && SessionState.GetBool(ActiveKey, false)) Persist(); }
        private static void Log(string message, string stack, LogType type)
        {
            if (report == null) return;
            if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) report.errors.Add(type + ": " + message + "\n" + stack);
            else if (type == LogType.Warning) report.warnings.Add(message);
        }
        private static void Check(bool condition, string name, string detail = "")
        {
            report.checks.Add(new AcceptanceCheck { name = name, passed = condition, detail = detail });
            if (!condition) throw new InvalidOperationException("Acceptance failed: " + name + ". " + detail);
            Debug.Log("AQUARIUM_ACCEPT_PASS " + name);
        }
        private static void Persist()
        { Directory.CreateDirectory(Path.GetDirectoryName(reportPath)); File.WriteAllText(reportPath, JsonUtility.ToJson(report, true)); }
        private static void Finish(bool success, Exception error = null)
        {
            report.status = success ? "passed" : "failed"; report.failure = error == null ? null : error.ToString();
            report.finishedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture); Persist();
            Application.logMessageReceived -= Log;
            AssemblyReloadEvents.beforeAssemblyReload -= PersistActiveReport;
            EditorApplication.delayCall -= StartupDelayCompleted;
            Debug.Log("AQUARIUM_ACCEPT_RESULT " + report.status + " " + report.checks.Count + " checks; " + reportPath);
        }
        private static string Tx() { return "unity-accept-" + (++sequence).ToString(CultureInfo.InvariantCulture); }
        private static void Ok(CommandResult result)
        { if (!result.Success) throw new InvalidOperationException(result.Code + " " + string.Join(",", result.Args)); }
        private static string Encode(GameSession session) { return JsonUtility.ToJson(session.Capture()); }
        private static GameSession Decode(RuntimeCatalog catalog, string payload)
        { return GameSession.Restore(catalog, JsonUtility.FromJson<SessionSnapshot>(payload), SessionMode.Prototype); }
        private static RuntimeCatalog LoadCatalog()
        {
            TextAsset asset = Resources.Load<TextAsset>("Configuration/runtime");
            Check(asset != null, "runtime resource imported"); RuntimeCatalog catalog = JsonUtility.FromJson<RuntimeCatalog>(asset.text); catalog.Validate();
            Check(catalog.Items.Length == 17 && catalog.Route.Length == 21, "nine-table runtime configuration validates through JsonUtility"); return catalog;
        }
        private static void PrepareAndInspect()
        {
            string projectVersion = File.ReadAllText(Path.Combine(Application.dataPath, "../ProjectSettings/ProjectVersion.txt"));
            Match editorPin = Regex.Match(projectVersion, @"(?m)^m_EditorVersion:\s*(\S+)\s*$");
            report.expectedUnityVersion = editorPin.Success ? editorPin.Groups[1].Value : "missing";
            Check(report.expectedUnityVersion == Application.unityVersion, "Unity editor matches selected project's ProjectVersion", report.expectedUnityVersion + " expected; " + Application.unityVersion + " actual");
            string manifest = File.ReadAllText(Path.Combine(Application.dataPath, "../Packages/manifest.json"));
            Match urpPin = Regex.Match(manifest, "\"com\\.unity\\.render-pipelines\\.universal\"\\s*:\\s*\"([^\"]+)\"");
            report.expectedUrpVersion = urpPin.Success ? urpPin.Groups[1].Value : "missing";
            Check(Regex.IsMatch(report.expectedUrpVersion, @"^\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?$"), "selected project freezes an exact URP version", report.expectedUrpVersion);
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UniversalRenderPipelineAsset).Assembly);
            report.urpVersion = package == null ? "not-found" : package.version;
            Check(report.urpVersion == report.expectedUrpVersion, "selected project's pinned URP package resolved", report.expectedUrpVersion + " expected; " + report.urpVersion + " actual");
            AquariumProjectSetup.Prepare();
#if UNITY_6000_0_OR_NEWER
            Check(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset && QualitySettings.renderPipeline is UniversalRenderPipelineAsset,
#else
            Check(GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset && QualitySettings.renderPipeline is UniversalRenderPipelineAsset,
#endif
                "Prepare assigns actual URP graphics and quality pipelines");
            Material opaque = Resources.Load<Material>("Rendering/AquariumOpaque"), water = Resources.Load<Material>("Rendering/AquariumWater");
            Check(opaque != null && water != null && opaque.shader != null && opaque.shader.name == "Universal Render Pipeline/Lit" &&
                water.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), "opaque and transparent shader materials import");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); int entries = 0, missing = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            { missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject); entries += t.GetComponents<AquariumGameEntry>().Length; }
            Check(scene.isLoaded && entries == 1 && missing == 0, "startup scene imports with one real entry and no missing scripts");
            bool registered = false; foreach (EditorBuildSettingsScene item in EditorBuildSettings.scenes) if (item.path == ScenePath && item.enabled) registered = true;
            Check(registered, "startup scene registered for builds");
        }
        private static Dictionary<string, string> BuildBase(GameSession session, RuntimeCatalog catalog)
        {
            var batches = new Dictionary<string, string>();
            foreach (ItemConfig item in catalog.Items) if (item.RouteRole == "required_base")
            { CommandResult r = session.Buy(Tx(), item.ItemId); Ok(r); batches.Add(item.ItemId, r.BatchId); }
            Ok(session.SetSubstrate(Tx(), new SubstrateProfile { PurchaseBatchId = batches["substrate_natural_sand"],
                FrontLeftHeight = .015, FrontRightHeight = .015, BackLeftHeight = .045, BackRightHeight = .045 }));
            Vec3 mainSize = new Vec3(.09,.11,.07), secondSize = new Vec3(.06,.05,.05);
            Vec3 main = new Vec3(-.12,0,.02), second = new Vec3(.09,0,-.01);
            main.Y = session.Gameplay.SuggestedBottomHeight(main, mainSize, 0) + mainSize.Y/2;
            second.Y = session.Gameplay.SuggestedBottomHeight(second, secondSize, 0) + secondSize.Y/2;
            Place(session,"main",batches["stone_main"],main,mainSize); Place(session,"second",batches["stone_secondary"],second,secondSize);
            Place(session,"filter",batches["filter_hangon_basic"],new Vec3(.24,.28,.11),new Vec3(.04,.06,.04));
            Place(session,"light",batches["light_low"],new Vec3(-.20,.33,0),new Vec3(.06,.015,.15));
            Place(session,"anubias",batches["plant_anubias_nana"],new Vec3(main.X-.02,main.Y+mainSize.Y/2,main.Z),new Vec3(.02,.035,.02),"main");
            Place(session,"fern",batches["plant_java_fern"],new Vec3(main.X+.02,main.Y+mainSize.Y/2,main.Z),new Vec3(.02,.05,.02),"main");
            Place(session,"moss",batches["plant_java_moss"],new Vec3(second.X,second.Y+secondSize.Y/2,second.Z),new Vec3(.025,.015,.025),"second");
            Ok(session.ConfirmLayout(Tx())); Ok(session.Fill(Tx())); return batches;
        }
        private static void Place(GameSession session, string id, string batch, Vec3 position, Vec3 size, string support = null)
        { Ok(session.Place(Tx(), new LayoutPlacement { Id=id, PurchaseBatchId=batch, Position=position, Size=size, SupportPlacementId=support })); }
        private static void ReachFirstStocking(GameSession session)
        {
            session.SetEcologyRate(4); Ok(session.BeginChallenge(Tx()));
            for (int i=0; i<600 && session.Gameplay.Stage!=GameplayStage.CyclePassed; i++) session.Advance(60);
            Check(session.Ecology.Challenge.Status==FishlessChallengeStatus.Passed && session.Gameplay.Stage==GameplayStage.CyclePassed &&
                session.Ecology.Challenge.DoseMgN>0, "real nonzero challenge and continuous window unlock first stocking");
            Ok(session.Buy(Tx(),"fish_trial_pack")); Ok(session.Buy(Tx(),"shrimp_pack"));
            Check(session.World.Agents.Count==14 && session.Gameplay.WalletCoin==90, "first stocking delivers eight Ember and six shrimp");
        }
        private static void EnsureReservesAndFood(GameSession session)
        {
            for (int i=0;i<30 && session.Capture().LivingReserves.Entries.Length==0;i++)
            {
                foreach(AquariumAgent agent in session.World.Agents)
                    if(agent.Profile.Kind==AgentKind.Fish) { Ok(session.Feed(Tx(),agent.State.Position,1)); break; }
                session.Advance(1);
            }
            Ok(session.Feed(Tx(),new Vec3(0,.29,0),12));
            SessionSnapshot state=session.Capture();
            Check(state.Food.Length>0 && state.LivingReserves.Entries.Length>0 && state.Ecology.State.LivingMgN>0,
                "snapshot contains both active food ownership and ingested per-animal nitrogen");
        }
        private static void FinishRoute(GameSession session)
        {
            for(int i=0;i<100 && session.Gameplay.Stage!=GameplayStage.MainUnlocked;i++) session.Advance(60);
            Check(session.Gameplay.Stage==GameplayStage.MainUnlocked && session.Gameplay.WalletCoin==540, "stocked observation grants 450 exactly once");
            Ok(session.ChangeWater(Tx(),session.Ecology.VolumeLiters*.2)); Ok(session.Buy(Tx(),"fish_main_pack"));
            int ember=0,neon=0,shrimp=0;
            foreach(AquariumAgent agent in session.World.Agents) { if(agent.State.SpeciesId=="ember_tetra")ember++;else if(agent.State.SpeciesId=="neon_tetra")neon++;else if(agent.State.SpeciesId=="neocaridina_red")shrimp++; }
            Check(ember==8 && neon==8 && shrimp==6 && session.Gameplay.WalletCoin==280 && session.Gameplay.Stage==GameplayStage.Complete,
                "full real route ends at 8 Ember + 8 Neon + 6 shrimp and 280 coins");
        }
        private static void ValidateJsonAndRoute(RuntimeCatalog catalog)
        {
            GameSession session=new GameSession(catalog);string initial=Encode(session);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"fresh-before-restore.json"),initial);
            SessionSnapshot decodedFresh=JsonUtility.FromJson<SessionSnapshot>(initial);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"fresh-decoded.json"),JsonUtility.ToJson(decodedFresh,true));
            GameSession fresh=GameSession.Restore(catalog,decodedFresh,SessionMode.Prototype);
            Check(!fresh.Capture().HasRetryCheckpoint && fresh.Gameplay.Capture().Layout.Substrate==null && fresh.Gameplay.WalletCoin==1500 && fresh.World.Agents.Count==0,
                "Unity JsonUtility roundtrips initial save with explicit absent checkpoint");
            CompareSnapshots("fresh codec snapshot equality",initial,Encode(fresh));
            ValidateAbsentSubstrateTransitions(catalog,initial);
            session.Advance(60);Check(session.Ecology.GameDays==0, "dry tank ecology remains stopped");
            BuildBase(session,catalog);
            string filled=Encode(session);File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"filled-before-restore.json"),filled);
            GameSession withCheckpoint=Decode(catalog,filled);
            string filledAfter=Encode(withCheckpoint);File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"filled-after-restore.json"),filledAfter);
            Check(withCheckpoint.Capture().HasRetryCheckpoint && withCheckpoint.Capture().RetryCheckpoint.Gameplay.Stage==GameplayStage.Editing,
                "Unity JsonUtility roundtrips populated nonrecursive retry checkpoint");
            CompareSnapshots("filled checkpoint codec snapshot equality",filled,filledAfter);
            session.SetPaused(true);string paused=Encode(session);
            Check(!session.Feed(Tx(),new Vec3()).Success && !session.SetLight(Tx(),true).Success && !session.BeginChallenge(Tx()).Success,
                "paused commands reject feeding, lighting and challenge");
            session.Advance(60);Check(Encode(session)==paused, "pause freezes both clocks and state");session.SetPaused(false);
            ReachFirstStocking(session);EnsureReservesAndFood(session);
            string stocked=Encode(session);File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"stocked-with-food-and-checkpoint.json"),stocked);
            GameSession loaded=Decode(catalog,stocked);
            Check(loaded.Capture().HasRetryCheckpoint && loaded.Capture().Food.Length>0 && loaded.Capture().LivingReserves.Entries.Length>0,
                "Unity JsonUtility preserves checkpoint, active food, reserves, RNG and all domain ledgers");
            CompareSnapshots("stocked food/reserve codec snapshot equality",stocked,Encode(loaded));
            session.Advance(5);loaded.Advance(5);CompareSnapshots("restored session continues matching trajectories and nitrogen for five seconds",Encode(session),Encode(loaded));
            FinishRoute(session);
            string complete=Encode(session);CompareSnapshots("completed full route roundtrips with Unity JsonUtility",complete,Encode(Decode(catalog,complete)));
            ValidateAtomicStore(catalog,session);
            Ok(session.Retry(Tx()));Check(session.Gameplay.Stage==GameplayStage.Editing && session.Gameplay.WalletCoin==390 && session.World.Agents.Count==0,
                "full checkpoint retry replaces wallet, animals, ecology and rewards atomically");
        }
        private static void CompareSnapshots(string name,string expected,string actual)
        {
            SnapshotComparison comparison=TypedSnapshotJson.Compare(name,expected,actual,typeof(SessionSnapshot));report.snapshotComparisons.Add(comparison);
            Check(comparison.passed,name,comparison.passed?comparison.strictlyComparedValues+" strict values; "+comparison.doubleValues+" finite doubles; "+
                comparison.nonzeroDoubleDifferences.Count+" nonzero double differences; maximum absolute error "+comparison.maxAbsoluteError.ToString("R",CultureInfo.InvariantCulture):comparison.failure);
        }
        private static void ValidateAbsentSubstrateTransitions(RuntimeCatalog catalog,string initial)
        {
            // A structural codec fixture: an editing checkpoint at the same new-save state, not a forged progress gate.
            SessionSnapshot checkpointFixture=JsonUtility.FromJson<SessionSnapshot>(initial);
            checkpointFixture.HasRetryCheckpoint=true;
            checkpointFixture.RetryCheckpoint=JsonUtility.FromJson<SessionStateSnapshot>(initial);
            GameSession checkpointSession=Decode(catalog,JsonUtility.ToJson(checkpointFixture));
            Check(checkpointSession.Capture().HasRetryCheckpoint && checkpointSession.Capture().RetryCheckpoint.Gameplay.Layout.Substrate==null,
                "Unity inline checkpoint without laid substrate preserves absence semantics");
            Ok(checkpointSession.Retry(Tx()));
            Check(checkpointSession.Gameplay.WalletCoin==1500 && checkpointSession.Gameplay.Capture().Layout.Substrate==null,
                "retry of empty editing checkpoint does not invent substrate or money");
            GameSession session=Decode(catalog,initial);CommandResult bought=session.Buy(Tx(),"substrate_natural_sand");Ok(bought);
            Ok(session.SetSubstrate(Tx(),new SubstrateProfile{PurchaseBatchId=bought.BatchId,FrontLeftHeight=.02,FrontRightHeight=.02,BackLeftHeight=.02,BackRightHeight=.02}));
            Ok(session.Undo(Tx()));string undone=Encode(session);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(reportPath),"substrate-undone-before-restore.json"),undone);
            session=Decode(catalog,undone);
            Check(session.Gameplay.Capture().Layout.Substrate==null && session.Gameplay.WalletCoin==1380,
                "undo to absent substrate survives Unity JsonUtility without refunding its purchase");
            Ok(session.Redo(Tx()));session=Decode(catalog,Encode(session));
            Check(session.Gameplay.Capture().Layout.Substrate!=null && Math.Abs(session.Gameplay.GetSubstrateHeight(0,0)-.02)<1e-8,
                "redo restores actual substrate after Unity roundtrip of absent state/history");
            SessionSnapshot corrupt=session.Capture();corrupt.Gameplay.Layout.Substrate.FrontLeftHeight=0;bool rejected=false;
            try{Decode(catalog,JsonUtility.ToJson(corrupt));}catch(ArgumentException){rejected=true;}
            Check(rejected,"present substrate with invalid actual height still rejects after absence handling fix");
        }
        private static void ValidateAtomicStore(RuntimeCatalog catalog, GameSession session)
        {
            string path=Path.Combine(Path.GetDirectoryName(reportPath),"save-recovery-fixture.json");
            Func<string,bool> valid=payload=>{try{Decode(catalog,payload);return true;}catch{return false;}};
            var store=new AtomicSaveStore(path,valid);string before=Encode(session);store.Save(before);session.Advance(1);store.Save(Encode(session));
            File.WriteAllText(path,"{invalid-json");string recovered;string payload=store.Load(out recovered);
            Check(recovered==path+".bak1" && valid(payload), "actual host file store recovers corrupt primary through Unity semantic validation");
        }
        private static void ValidateLocales()
        {
            var text=new DemoLocalization();ResourceCatalogLoader.LoadAvailable(text);
            Check(text.SupportedLocales.Count>=2 && text.ResolveStartupLanguage(null,null,"unregistered") == "en", "real Resources locales load with English final fallback");
            foreach(string locale in new[]{"en","zh-CN"})
            {
                Check(text.SetLanguage(locale)==locale, "ready locale selects: "+locale);
                TextAsset asset=Resources.Load<TextAsset>("Localization/"+locale);var envelope=JsonUtility.FromJson<LocalizationEnvelope>(asset.text);int count=0;
                foreach(LocalizationEntry item in envelope.entries)
                {
                    if(!(item.key.StartsWith("game.",StringComparison.Ordinal)||item.key.StartsWith("gameplay.",StringComparison.Ordinal)||
                        item.key.StartsWith("ui.",StringComparison.Ordinal)||item.key.StartsWith("guide.",StringComparison.Ordinal)||item.key.StartsWith("item.",StringComparison.Ordinal)))continue;
                    int highest=-1;foreach(Match match in Regex.Matches(item.value,@"\{([0-9]+)(?:[:,}])"))highest=Math.Max(highest,int.Parse(match.Groups[1].Value,CultureInfo.InvariantCulture));
                    object[] args=new object[highest+1];for(int i=0;i<args.Length;i++)args[i]=12.5;
                    string rendered=text.Text(item.key,args);if(string.IsNullOrEmpty(rendered)||Regex.IsMatch(rendered,@"\{[0-9]+(?:[:,}])"))throw new InvalidOperationException("Unformatted locale key "+locale+":"+item.key);count++;
                }
                Check(count>100,"gameplay UI scalar key coverage: "+locale,count+" keys formatted through actual runtime formatter; font/layout not certified.");
                var restarted=new DemoLocalization();ResourceCatalogLoader.LoadAvailable(restarted);
                Check(restarted.RestoreLanguagePreference(text.CaptureLanguagePreference())==locale,"locale choice survives independent preference restore: "+locale);
            }
        }
        private static object PrivateField(object target,string name)
        {FieldInfo field=target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance);if(field==null)throw new MissingFieldException(target.GetType().Name,name);return field.GetValue(target);}
        private static void SetPrivateField(object target,string name,object value)
        {target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);}
        private static void ScheduleStartupDelay()
        {
            startupDelayCompleted=false; stableStartupUpdates=0;
            EditorApplication.delayCall-=StartupDelayCompleted; EditorApplication.delayCall+=StartupDelayCompleted;
        }
        private static void StartupDelayCompleted() { startupDelayCompleted=true; }
        private static void ObserveStartup(string state)
        {
            if(state==lastStartupState)return;
            lastStartupState=state;
            report.startupObservations.Add(EditorApplication.timeSinceStartup.ToString("F3",CultureInfo.InvariantCulture)+"s "+state);
            Persist();
        }
        private static bool StartupReady()
        {
            if(report.errors.Count>0)throw new InvalidOperationException("Editor startup emitted errors before Play; all captured errors are retained.");
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Play was requested before the startup gate completed.");
            if(!startupDelayCompleted || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                stableStartupUpdates=0;
                ObserveStartup("delayCall="+startupDelayCompleted+" compiling="+EditorApplication.isCompiling+" updating="+EditorApplication.isUpdating);
                return false;
            }
            string state="initial delayCall completed; editor idle";
#if UNITY_6000_0_OR_NEWER
            // Unity 6000.6 defers default Search DB creation to its startup delayCall.
            // SearchDatabase is internal. Inspect existing state only; never invoke its initializer or change Search settings.
            string indexPath=Path.GetFullPath(Path.Combine(Application.dataPath,"../UserSettings/Search.index"));
            if(!File.Exists(indexPath)) { stableStartupUpdates=0; ObserveStartup(state+"; indexPath="+indexPath+" absent"); return false; }
            Type databaseType=typeof(UnityEditor.Search.SearchService).Assembly.GetType("UnityEditor.Search.SearchDatabase",true);
            const BindingFlags properties=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            PropertyInfo pathProperty=databaseType.GetProperty("path",properties), readyProperty=databaseType.GetProperty("ready",properties),
                updatingProperty=databaseType.GetProperty("updating",properties), loadingProperty=databaseType.GetProperty("LoadingState",properties);
            if(pathProperty==null || readyProperty==null || updatingProperty==null || loadingProperty==null)
                throw new MissingMemberException("Installed SearchDatabase lacks the observed path/ready/updating/LoadingState properties.");
            bool found=false,ready=false,updating=true; string loading="not-found";
            foreach(UnityEngine.Object database in Resources.FindObjectsOfTypeAll(databaseType))
            {
                if((string)pathProperty.GetValue(database,null)!="UserSettings/Search.index")continue;
                found=true;ready=(bool)readyProperty.GetValue(database,null);updating=(bool)updatingProperty.GetValue(database,null);
                loading=Convert.ToString(loadingProperty.GetValue(database,null),CultureInfo.InvariantCulture);break;
            }
            state+="; indexPath="+indexPath+" present; defaultDBPath=UserSettings/Search.index found="+found+" ready="+ready+" updating="+updating+" loading="+loading;
            ObserveStartup(state);
            if(!found || !ready || updating) { stableStartupUpdates=0; return false; }
#else
            ObserveStartup(state);
#endif
            // Observe an additional idle update so the gate cannot enter Play from the startup delayCall itself.
            return ++stableStartupUpdates>=2;
        }
        private static void PlayUpdate()
        {
            if(!SessionState.GetBool(ActiveKey,false))return;
            try
            {
                if(EditorApplication.timeSinceStartup-playStarted>180)throw new TimeoutException("Play smoke exceeded 180 seconds; phase="+SessionState.GetString(PhaseKey,"")+"; "+lastStartupState);
                if(SessionState.GetString(PhaseKey,"play")=="startup")
                {
                    if(!StartupReady())return;
                    Check(report.errors.Count==0,"Editor startup completes before entering Play",lastStartupState);
                    SessionState.SetString(PhaseKey,"play");Persist();EditorApplication.isPlaying=true;return;
                }
                if(!EditorApplication.isPlaying||EditorApplication.isPaused)return;
                playFrames++;
                if(!runtimeChecked && playFrames>=30)
                {
                    AquariumGameEntry entry=UnityEngine.Object.FindFirstObjectByType<AquariumGameEntry>();Check(entry!=null,"real scene entry survives Awake and initial Update frames");
                    GameSession session=(GameSession)PrivateField(entry,"session");RuntimeCatalog catalog=(RuntimeCatalog)PrivateField(entry,"catalog");
                    Check(session!=null && session.Gameplay.WalletCoin==1500,"scene Awake starts actual production session");
                    BuildBase(session,catalog);ReachFirstStocking(session);EnsureReservesAndFood(session);FinishRoute(session);
                    var rebuild=typeof(AquariumGameEntry).GetMethod("RebuildLayout",BindingFlags.Instance|BindingFlags.NonPublic);rebuild.Invoke(entry,null);
                    var advance=typeof(AquariumGameEntry).GetMethod("AdvanceSimulation",BindingFlags.Instance|BindingFlags.Public);
                    Check(advance!=null,"real adapter elapsed-time entry exists");
                    string baseline=Encode(session);SetPrivateField(entry,"session",Decode(catalog,baseline));advance.Invoke(entry,new object[]{.5});
                    string longFrame=Encode((GameSession)PrivateField(entry,"session"));SetPrivateField(entry,"session",Decode(catalog,baseline));
                    for(int i=0;i<15;i++)advance.Invoke(entry,new object[]{1.0/30});
                    Check(longFrame==Encode((GameSession)PrivateField(entry,"session")),"real adapter 0.5-second host frame equals fifteen 30Hz frames");
                    // Batch editors can be unfocused; use the actual view methods without fabricating focus/input.
                    typeof(AquariumGameEntry).GetMethod("UpdateAnimals",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(entry,null);
                    typeof(AquariumGameEntry).GetMethod("UpdateFood",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(entry,null);
                    if(!Application.isFocused)report.warnings.Add("Batch editor is unfocused. Real view methods were invoked explicitly; focused physical-input/Update routing remains separate acceptance.");
                    runtimeChecked=true;
                }
                if(runtimeChecked && playFrames>=90)
                {
                    AquariumGameEntry entry=UnityEngine.Object.FindFirstObjectByType<AquariumGameEntry>();var bodies=(Dictionary<string,Transform>)PrivateField(entry,"animals");
                    Check(bodies.Count==22 && UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length>0 && UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None).Length>0,
                        "real adapter view methods build all 22 animal bodies, camera and substrate collider");
                    Check(report.errors.Count==0,"Play smoke emitted no uncaught engine or adapter errors",playFrames+" observed editor updates; no automated physical input/visual-quality or every-OnGUI-path claim.");
                    SessionState.SetBool(ActiveKey,false);EditorApplication.update-=PlayUpdate;Finish(true);EditorApplication.Exit(0);
                }
            }
            catch(Exception error){SessionState.SetBool(ActiveKey,false);EditorApplication.update-=PlayUpdate;Finish(false,error);EditorApplication.Exit(1);}
        }
    }
}
#endif
