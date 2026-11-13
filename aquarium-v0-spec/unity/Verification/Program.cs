using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Text.Json;
using Aquarium.Core;
using Aquarium.UnityAdapter;

internal static class Program
{
    private static int checks;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("FAIL: " + message); checks++; Console.WriteLine("PASS: " + message); }
    private static Dictionary<string, SpeciesProfile> Profiles()
    {
        SpeciesProfile fish = DemoProfiles.CommunityFish(), shrimp = DemoProfiles.Shrimp(), panda = DemoProfiles.BottomSifter();
        return new Dictionary<string, SpeciesProfile> { { fish.Id, fish }, { shrimp.Id, shrimp }, { panda.Id, panda } };
    }
    private static AquariumWorld Create()
    {
        AquariumWorld w = new AquariumWorld(new TankBounds());
        for (int i = 0; i < 10; i++) w.AddAgent(DemoProfiles.CommunityFish(), "fish-" + i, (uint)(9481 + i * 7919));
        for (int i = 0; i < 4; i++) w.AddAgent(DemoProfiles.Shrimp(), "shrimp-" + i, (uint)(23003 + i * 6151));
        for (int i = 0; i < 6; i++) w.AddAgent(DemoProfiles.BottomSifter(), "panda-" + i, (uint)(41003 + i * 5003));
        return w;
    }
    private static string AgentsJson(AquariumWorld w) { return JsonSerializer.Serialize(w.Capture().Agents, Json); }
    private static void RunFor(AquariumWorld w, double seconds)
    { for (int i = 0; i < (int)Math.Round(seconds * 30); i++) w.Advance(AquariumWorld.FixedStep); }
    private static AquariumWorld SingleFish(uint seed)
    {
        AquariumWorld w = new AquariumWorld(new TankBounds());
        w.AddAgent(DemoProfiles.CommunityFish(), "single-fish", seed);
        return w;
    }
    private static bool Rejects(Action operation)
    { try { operation(); return false; } catch (ArgumentException) { return true; } }
    private static string WorldJson(AquariumWorld w) { return JsonSerializer.Serialize(w.Capture(), Json); }

    private static readonly string[] DemoKeys = { "demo.title", "demo.clock", "demo.controls.events", "demo.controls.camera",
        "demo.status.ready", "demo.status.saved", "demo.status.loaded", "demo.status.save_failed", "demo.status.load_failed",
        "demo.state.paused", "demo.state.running", "demo.light.on", "demo.light.off" };
    private static LocalizationEnvelope DemoEnvelope(string code)
    {
        DemoLocalization english = new DemoLocalization();
        LocalizationEnvelope envelope = new LocalizationEnvelope { language = code,
            entries = DemoKeys.Select(k => new LocalizationEntry { key = k, value = english.Text(k) }).ToArray() };
        if (code != "en") envelope.entries[0].value = "Synthetic localized title: " + code;
        return envelope;
    }
    private static LocaleMetadata Locale(string code, string culture, string status, string steam = "")
    {
        return new LocaleMetadata { code = code, native_name = code, english_name = code, culture = culture,
            status = status, direction = "ltr", font_group = "test-font-requirement", steam_language = steam,
            fallback = code == "en" ? "" : "en", release_required = status == "ready" };
    }
    private static LocalizationManifest SyntheticManifest()
    {
        return new LocalizationManifest { schema_version = 1, default_language = "en", fallback_language = "en", locales = new[] {
            Locale("en", "en-US", "ready", "english"), Locale("zh-CN", "zh-CN", "ready", "schinese"),
            Locale("fr-FR", "fr-FR", "ready", "french"), Locale("fr", "fr-FR", "ready"),
            Locale("de-DE", "de-DE", "draft", "german"), Locale("qps-ploc", "en-US", "qa"), Locale("ja", "ja-JP", "planned", "japanese") } };
    }
    private static LocalizationEnvelope[] SyntheticCatalogs()
    {
        return new[] { DemoEnvelope("en"), DemoEnvelope("zh-CN"), DemoEnvelope("fr-FR"), DemoEnvelope("fr"),
            new LocalizationEnvelope { language = "de-DE", entries = new[] { new LocalizationEntry { key = "demo.title", value = "Entwurf" } } },
            DemoEnvelope("qps-ploc"), DemoEnvelope("ja") };
    }
    private static void VerifyLocalization()
    {
        DemoLocalization runtime = new DemoLocalization();
        string originalThreadCulture = CultureInfo.CurrentCulture.Name;
        Check(runtime.Language == "en" && runtime.SupportedLocales.Count == 1,
            "emergency catalog defaults to complete English without advertising unregistered languages");
        runtime.Configure(SyntheticManifest(), SyntheticCatalogs());
        Check(runtime.SetLanguage("FR_fr") == "fr-FR" && runtime.Text("demo.title").Contains("fr-FR"),
            "a third ready locale registers and selects from data without changing business code");
        string preference = JsonSerializer.Serialize(runtime.CaptureLanguagePreference());
        DemoLocalization restarted = new DemoLocalization(); restarted.Configure(SyntheticManifest(), SyntheticCatalogs());
        Check(restarted.RestoreLanguagePreference(JsonSerializer.Deserialize<string>(preference)) == "fr-FR",
            "an arbitrary supported locale code survives stored preference restoration");
        Check(runtime.Culture.Name == "fr-FR" && runtime.FormatNumber(12.5, "F1") == "12,5" &&
            runtime.Text("demo.clock", 1.2, .3, 4, "on", "running").Contains("1,2") &&
            runtime.FormatDate(new DateTime(2026, 12, 31)) == "31/12/2026",
            "selected locale culture formats decimal numbers, scalar messages and dates");
        Check(CultureInfo.CurrentCulture.Name == originalThreadCulture,
            "UI culture is an explicit format provider and never changes the global parsing culture");
        Check(runtime.SetLanguage("fr-CA") == "fr" && runtime.SetLanguage("fr-CA", false) == "en",
            "optional parent-locale matching is distinct from final English fallback");
        Check(runtime.SetLanguage("unknown-ZZ") == "en" && runtime.SetLanguage("ja") == "en",
            "unknown and planned locales fall back to English rather than becoming supported");
        Check(runtime.SetLanguage("de-DE") == "en" && runtime.SetDevelopmentLanguage("de-DE") == "de-DE" &&
            runtime.Text("demo.title") == "Entwurf" && runtime.Text("demo.status.ready") == new DemoLocalization().Text("demo.status.ready") &&
            runtime.SupportedLocales.All(l => l.status == "ready") && runtime.SelectableLocales.All(l => l.status == "ready"),
            "draft locale requires explicit development selection and missing keys fall back to English");
        Check(runtime.RestoreLanguagePreference("de-DE") == "en", "normal player preferences cannot implicitly enable a draft locale");
        runtime.SetLanguage("en"); HashSet<string> cycle = new HashSet<string>();
        for (int i = 0; i < runtime.SelectableLocales.Count; i++) cycle.Add(runtime.CycleLanguage());
        Check(cycle.Contains("fr-FR") && !cycle.Contains("qps-ploc") &&
            runtime.LoadedLocales.All(l => l.status != "qa" && l.status != "planned"),
            "language cycling includes loaded ready locales and hides QA/planned locales by default");
        DemoLocalization qa = new DemoLocalization(); qa.Configure(SyntheticManifest(), SyntheticCatalogs(), true);
        Check(qa.SetLanguage("qps-ploc") == "qps-ploc" && qa.SelectableLocales.Any(l => l.status == "qa") &&
            qa.SupportedLocales.All(l => l.status == "ready"), "QA locale is selectable only behind the development flag");

        runtime.SetLanguage("fr-FR"); string originalTitle = runtime.Text("demo.title");
        LocalizationEnvelope[] badEnglish = SyntheticCatalogs(); badEnglish[0].entries = badEnglish[0].entries.Skip(1).ToArray();
        bool missingEnglish = Rejects(() => runtime.Configure(SyntheticManifest(), badEnglish));
        LocalizationEnvelope[] duplicate = SyntheticCatalogs();
        duplicate[2].entries = duplicate[2].entries.Concat(new[] { duplicate[2].entries[0] }).ToArray();
        bool duplicateKey = Rejects(() => runtime.Configure(SyntheticManifest(), duplicate));
        LocalizationEnvelope[] badParameter = SyntheticCatalogs();
        badParameter[2].entries.Single(e => e.key == "demo.clock").value = "Missing other clock parameters: {0:F1}";
        bool wrongParameter = Rejects(() => runtime.Configure(SyntheticManifest(), badParameter));
        Check(missingEnglish && duplicateKey && wrongParameter && runtime.Language == "fr-FR" && runtime.Text("demo.title") == originalTitle,
            "missing English, duplicate keys and changed message parameters reject the whole update atomically");
        LocalizationEnvelope[] missingReadyKey = SyntheticCatalogs();
        missingReadyKey[0].entries = missingReadyKey[0].entries.Concat(new[] { new LocalizationEntry { key = "extra.production.key", value = "required" } }).ToArray();
        Check(Rejects(() => runtime.Configure(SyntheticManifest(), missingReadyKey)) && runtime.Text("demo.title") == originalTitle,
            "ready catalogs must cover all English production keys, not just the demo subset");
        LocalizationManifest cycleManifest = SyntheticManifest();
        cycleManifest.locales.Single(l => l.code == "fr-FR").fallback = "de-DE";
        cycleManifest.locales.Single(l => l.code == "de-DE").fallback = "fr-FR";
        Check(Rejects(() => runtime.Configure(cycleManifest, SyntheticCatalogs())) && runtime.Text("demo.title") == originalTitle,
            "fallback graph cycles are rejected without replacing the live registry");
        LocalizationManifest brokenRoot = SyntheticManifest(); brokenRoot.locales.Single(l => l.code == "fr-FR").fallback = "";
        Check(Rejects(() => runtime.Configure(brokenRoot, SyntheticCatalogs())) && runtime.Text("demo.title") == originalTitle,
            "every fallback chain must terminate at English rather than a non-English root");
        LocalizationManifest duplicateSteam = SyntheticManifest(); duplicateSteam.locales.Single(l => l.code == "fr").steam_language = "FRENCH";
        Check(Rejects(() => runtime.Configure(duplicateSteam, SyntheticCatalogs())) && runtime.Text("demo.title") == originalTitle,
            "duplicate nonempty Steam language mappings reject atomically instead of depending on locale order");
        LocalizationManifest requiredDraft = SyntheticManifest(); requiredDraft.locales.Single(l => l.code == "de-DE").release_required = true;
        Check(Rejects(() => runtime.Configure(requiredDraft, SyntheticCatalogs())), "release-required catalogs require full coverage even when marked draft");
        LocalizationManifest chainedManifest = SyntheticManifest(); chainedManifest.locales.Single(l => l.code == "de-DE").fallback = "fr-FR";
        LocalizationEnvelope[] chainedCatalogs = SyntheticCatalogs();
        chainedCatalogs.Single(c => c.language == "fr-FR").entries.Single(e => e.key == "demo.status.ready").value = "French readiness";
        DemoLocalization chained = new DemoLocalization(); chained.Configure(chainedManifest, chainedCatalogs);
        chained.SetDevelopmentLanguage("de-DE");
        Check(chained.Text("demo.status.ready") == "French readiness" && chained.Text("demo.clock", 1.2, .3, 4, "on", "running").Contains("1,2"),
            "draft key fallback walks intermediate locales and formats fallback text using the selected locale culture");
        Check(runtime.ResolveStartupLanguage("en", "french", "fr-CA") == "en" &&
            runtime.ResolveStartupLanguage("zh-CN", "english", "fr-CA") == "zh-CN",
            "an available saved player choice has priority over Steam and OS language");
        Check(runtime.ResolveStartupLanguage(null, "french", "zh-CN") == "fr-FR" &&
            runtime.ResolveStartupLanguage("ja", "FRENCH", "zh-CN") == "fr-FR",
            "registered Steam language codes choose a ready locale when the saved choice is unavailable");
        Check(runtime.ResolveStartupLanguage(null, null, "fr-CA") == "fr" &&
            runtime.ResolveStartupLanguage("unknown", "unknown", "unknown-ZZ") == "en",
            "startup resolves OS region parents and ultimately falls back to English");

        DirectoryInfo packageDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (packageDirectory != null && !Directory.Exists(Path.Combine(packageDirectory.FullName, "Assets", "_Game")))
            packageDirectory = packageDirectory.Parent;
        if (packageDirectory == null) throw new Exception("Could not locate the Unity source package.");
        string directory = Path.Combine(packageDirectory.FullName, "Assets", "_Game", "Resources", "Localization");
        LocalizationManifest sharedManifest = JsonSerializer.Deserialize<LocalizationManifest>(File.ReadAllText(Path.Combine(directory, "locales.json")), Json);
        List<LocalizationEnvelope> sharedCatalogs = new List<LocalizationEnvelope>();
        foreach (LocaleMetadata locale in sharedManifest.locales)
        {
            if (locale.status == "planned") continue;
            string path = Path.Combine(directory, locale.code + ".json");
            if (!File.Exists(path)) continue;
            LocalizationEnvelope catalog = JsonSerializer.Deserialize<LocalizationEnvelope>(File.ReadAllText(path), Json);
            if (!string.Equals(catalog.language, locale.code, StringComparison.OrdinalIgnoreCase)) throw new Exception("Resource filename/language mismatch.");
            sharedCatalogs.Add(catalog);
        }
        DemoLocalization shared = new DemoLocalization(); shared.Configure(sharedManifest, sharedCatalogs, true);
        bool allReadySelectable = true;
        foreach (LocaleMetadata locale in sharedManifest.locales.Where(l => l.status == "ready"))
            allReadySelectable &= shared.SetLanguage(locale.code) == locale.code &&
                !string.IsNullOrEmpty(shared.Text("demo.clock", 1.2, .3, 4, "on", "running"));
        Check(allReadySelectable && shared.LoadedLocales.Count == sharedCatalogs.Count,
            "all nonplanned shared catalogs dynamically load and satisfy registry coverage and demo format contracts");
        shared.Configure(sharedManifest, sharedCatalogs);
        Check(shared.LoadedLocales.All(l => l.status != "qa" && l.status != "planned") &&
            shared.SelectableLocales.All(l => l.status == "ready"), "actual production language menu excludes planned and QA entries");
        string translatedCode = shared.SupportedLocales.First(l => l.code != "en").code;
        shared.SetLanguage(translatedCode); string preservedPrice = shared.Text("ui.price", 120);
        LocalizationEnvelope[] corruptPrice = JsonSerializer.Deserialize<LocalizationEnvelope[]>(JsonSerializer.Serialize(sharedCatalogs, Json), Json);
        corruptPrice.Single(c => c.language == translatedCode).entries.Single(e => e.key == "ui.price").value = "Incorrect token {price}";
        Check(Rejects(() => shared.Configure(sharedManifest, corruptPrice)) && shared.Language == translatedCode && shared.Text("ui.price", 120) == preservedPrice,
            "a real shared ui.price parameter mismatch is rejected atomically outside the demo-key subset");
        LocalizationEnvelope[] corruptNamed = JsonSerializer.Deserialize<LocalizationEnvelope[]>(JsonSerializer.Serialize(sharedCatalogs, Json), Json);
        corruptNamed.Single(c => c.language == translatedCode).entries.Single(e => e.key == "compatibility.reason.ADULT_MALE_BETTA_CONSPECIFIC_BLOCK.message").value += " {source}";
        Check(Rejects(() => shared.Configure(sharedManifest, corruptNamed)) && shared.Text("ui.price", 120) == preservedPrice,
            "named compatibility placeholders preserve occurrence counts across translations");
        LocalizationEnvelope[] unknownKey = SyntheticCatalogs();
        unknownKey.Single(c => c.language == "de-DE").entries = new[] { new LocalizationEntry { key = "invented.unknown.key", value = "unexpected" } };
        LocalizationEnvelope[] blankDraft = SyntheticCatalogs(); blankDraft.Single(c => c.language == "de-DE").entries[0].value = " ";
        Check(Rejects(() => runtime.Configure(SyntheticManifest(), unknownKey)) && Rejects(() => runtime.Configure(SyntheticManifest(), blankDraft)),
            "draft catalogs may omit keys but cannot inject unknown keys or blank translations");
    }

    public static void Main()
    {
        VerifyLocalization();

        SavedRandom r = new SavedRandom(101); r.NextUInt(); uint saved = r.State;
        uint next = r.NextUInt(); SavedRandom recovered = new SavedRandom(saved);
        Check(recovered.NextUInt() == next, "saved RNG continues the same sequence");

        AquariumWorld slow = Create(), fast = Create(); fast.SetEcologyRate(4);
        slow.SetLight(true); fast.SetLight(true); slow.Advance(.25); fast.Advance(.25);
        slow.ApproachGlass(new Vec3(0, .15, -.15)); fast.ApproachGlass(new Vec3(0, .15, -.15));
        RunFor(slow, 30); RunFor(fast, 30);
        Check(AgentsJson(slow) == AgentsJson(fast), "4x ecology does not accelerate agent movement, hunger or reactions");
        Check(Math.Abs(fast.EcologyDays - slow.EcologyDays * 4) < 1e-10, "ecology clock advances independently at 4x");

        AquariumWorld live = Create(); live.SetLight(true); live.Advance(.08);
        live.Feed(new Vec3(0, .29, -.02)); live.ApproachGlass(new Vec3(0, .15, -.15)); live.Advance(.02);
        string snapshotJson = JsonSerializer.Serialize(live.Capture(), Json);
        WorldSnapshot snapshot = JsonSerializer.Deserialize<WorldSnapshot>(snapshotJson, Json);
        AquariumWorld loaded = AquariumWorld.Restore(snapshot, new TankBounds(), Profiles());
        RunFor(live, 40); RunFor(loaded, 40);
        Check(JsonSerializer.Serialize(live.Capture(), Json) == JsonSerializer.Serialize(loaded.Capture(), Json),
            "JSON save/load resumes pending stimuli, individual RNG and subsequent trajectories exactly");

        AquariumWorld tank = Create(); tank.SetLight(true);
        HashSet<ActionKind> observed = new HashSet<ActionKind>(); bool contained = true;
        for (int tick = 0; tick < 180 * 30; tick++)
        {
            if (tick == 20 * 30) tank.ApproachGlass(new Vec3(0, .15, -.15));
            if (tick == 40 * 30) tank.Feed(new Vec3(0, .28, -.02), 18);
            if (tick == 100 * 30) tank.Feed(new Vec3(.1, .015, .03), 10);
            tank.Advance(AquariumWorld.FixedStep);
            foreach (AquariumAgent a in tank.Agents)
            { contained &= tank.Tank.Contains(a.State.Position, a.Profile); observed.Add(a.State.Action); }
        }
        Check(contained, "all trajectories remain in tank bounds and species swim layers for 180 seconds");
        Check(observed.Count >= 7, "weighted choices produce diverse eligible actions rather than a fixed action cycle");
        Check(tank.Agents.Select(a => a.State.RngState).Distinct().Count() == tank.Agents.Count,
            "all agents retain independent RNG states");
        Check(tank.Agents.Where(a => a.Profile.HasTag("bottom_sifter")).Sum(a => a.State.ProbeBursts) > 0,
            "panda corydoras actually stop and probe the bottom");
        Check(tank.Agents.Where(a => a.Profile.HasTag("surface_grazer")).Sum(a => a.State.GrazeDwells) > 0,
            "shrimp actually dwell and graze at the substrate surface");
        Check(tank.Agents.Sum(a => a.State.Meals) > 0, "feeding reaches consumption and reduces hunger");
        Check(tank.Agents.All(a => a.State.Action != ActionKind.ApproachGlass),
            "false feeding anticipation expires after no-food approach");
        Check(tank.Agents.Sum(a => a.State.Startles) > 0 && tank.Agents.Sum(a => a.State.Startles) < tank.Agents.Count,
            "light response differs by individual rather than synchronizing every animal");
        Check(tank.Agents.Sum(a => a.State.GlassApproaches) > 0, "glass approach produces curiosity in some individuals");

        double flashIntegral = 0, steadyIntegral = 0;
        bool flashHigherForEverySeed = true, stableHabituation = true;
        AquariumWorld flashMemoryWorld = null;
        uint[] lightSeeds = { 11, 193, 2027, 9181, 13331, 34337, 65539, 98747 };
        foreach (uint seed in lightSeeds)
        {
            AquariumWorld flashing = SingleFish(seed), steady = SingleFish(seed);
            steady.SetLight(true);
            for (int halfSecond = 0; halfSecond < 40; halfSecond++)
            {
                flashing.SetLight(halfSecond % 2 == 0);
                RunFor(flashing, .5); RunFor(steady, .5);
                flashIntegral += flashing.Agents[0].State.Stress * .5;
                steadyIntegral += steady.Agents[0].State.Stress * .5;
            }
            flashHigherForEverySeed &= flashing.Agents[0].State.Stress > steady.Agents[0].State.Stress + .5 &&
                flashing.Agents[0].State.LightHabituation == 0 && flashing.Agents[0].State.LightBurstLoad > .7;
            RunFor(steady, 340);
            stableHabituation &= steady.Agents[0].State.LightHabituation > .20 &&
                steady.Agents[0].State.LightHabituation <= AquariumWorld.MaximumLightHabituation &&
                steady.Agents[0].State.SafeLightSeconds >= AquariumWorld.SafeLightWindowSeconds;
            flashMemoryWorld = flashing;
        }
        Check(flashHigherForEverySeed && flashIntegral > steadyIntegral * 2,
            "eight fixed seeds: rapid flashing accumulates more stress and grants no short-term habituation");
        Check(stableHabituation,
            "eight fixed seeds: sustained safe illumination slowly grants bounded long-term habituation");
        WorldSnapshot flashSnapshot = JsonSerializer.Deserialize<WorldSnapshot>(WorldJson(flashMemoryWorld), Json);
        AquariumWorld restoredFlash = AquariumWorld.Restore(flashSnapshot, new TankBounds(), Profiles());
        RunFor(flashMemoryWorld, 60); RunFor(restoredFlash, 60);
        Check(WorldJson(flashMemoryWorld) == WorldJson(restoredFlash),
            "flash pressure, safe-light timers and pending recovery persist through JSON reload");

        AquariumWorld mixedSingles = new AquariumWorld(new TankBounds());
        mixedSingles.AddAgent(DemoProfiles.CommunityFish(), "one-fish", 101);
        mixedSingles.AddAgent(DemoProfiles.Shrimp(), "one-shrimp", 283);
        bool invalidShoal = false, shrimpChasedGlass = false;
        for (int tick = 0; tick < 120 * 30; tick++)
        {
            if (tick % (4 * 30) == 0) mixedSingles.ApproachGlass(new Vec3(0, .15, -.15));
            mixedSingles.Advance(AquariumWorld.FixedStep);
            invalidShoal |= mixedSingles.Agents.Any(a => a.State.Action == ActionKind.Shoal);
            shrimpChasedGlass |= mixedSingles.Agents[1].State.Action == ActionKind.ApproachGlass ||
                mixedSingles.Agents[1].State.ApproachMemory > 0;
        }
        Check(!invalidShoal, "individuals without conspecifics cannot select the shoaling action");
        Check(!shrimpChasedGlass, "bottom-grazing shrimp do not use the game glass proxy as a camera-chasing goal");

        AquariumWorld guarded = Create(); string beforeInvalid = WorldJson(guarded);
        bool badEventsRejected = true;
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            badEventsRejected &= Rejects(() => guarded.Feed(new Vec3(invalid, .1, 0))) &&
                Rejects(() => guarded.ApproachGlass(new Vec3(0, invalid, 0))) &&
                Rejects(() => guarded.Advance(invalid)) && Rejects(() => guarded.SetEcologyRate(invalid));
            SpeciesProfile invalidProfile = DemoProfiles.CommunityFish(); invalidProfile.PreferredSpeed = invalid;
            badEventsRejected &= Rejects(() => guarded.AddAgent(invalidProfile, "invalid-speed", 4));
            SpeciesProfile invalidProbability = DemoProfiles.CommunityFish(); invalidProbability.Curiosity = invalid;
            badEventsRejected &= Rejects(() => guarded.AddAgent(invalidProbability, "invalid-probability", 5));
            badEventsRejected &= Rejects(() => new AquariumWorld(new TankBounds { Width = invalid }));
        }
        Check(badEventsRejected && beforeInvalid == WorldJson(guarded),
            "NaN and infinities in events, clocks, dimensions and profiles are rejected without mutating trajectories or saves");
        WorldSnapshot corrupt = guarded.Capture(); corrupt.Agents[0].SafeLightSeconds = double.NaN;
        bool corruptMemoryRejected = Rejects(() => AquariumWorld.Restore(corrupt, new TankBounds(), Profiles()));
        corrupt = guarded.Capture(); corrupt.Stimuli = new[] { new StimulusState { Id = 1, Kind = StimulusKind.Food,
            Position = new Vec3(double.PositiveInfinity, 0, 0), Strength = 1, Remaining = 1, Portions = 1 } }; corrupt.NextEventId = 2;
        Check(corruptMemoryRejected && Rejects(() => AquariumWorld.Restore(corrupt, new TankBounds(), Profiles())) &&
            beforeInvalid == WorldJson(guarded), "non-finite snapshot memory and stimulus positions cannot be restored into a live world");

        bool isolationRejected = false;
        try { tank.AddAgent(DemoProfiles.TerritorialMale(), "betta", 1); } catch (InvalidOperationException) { isolationRejected = true; }
        Check(isolationRejected, "high-conflict isolated fixtures cannot be spawned into the community demo");
        bool invalidLayerRejected = false;
        try { tank.AddAgent(new SpeciesProfile { Id = "bad", SwimLayerMin = .5, SwimLayerMax = .51, BodyRadius = .01 }, "bad", 2); }
        catch (ArgumentException) { invalidLayerRejected = true; }
        Check(invalidLayerRejected, "invalid swim layer/body size is rejected");
        bool futureSaveRejected = false;
        snapshot.SchemaVersion = 999;
        try { AquariumWorld.Restore(snapshot, new TankBounds(), Profiles()); } catch (ArgumentException) { futureSaveRejected = true; }
        Check(futureSaveRejected, "unknown snapshot schema is rejected rather than silently loaded");
        Console.WriteLine("Behavior checks complete: " + checks);
        Console.WriteLine("Observed actions: " + string.Join(", ", observed));
        Console.WriteLine("Meals=" + tank.Agents.Sum(a => a.State.Meals) + " probes=" + tank.Agents.Sum(a => a.State.ProbeBursts) +
            " grazes=" + tank.Agents.Sum(a => a.State.GrazeDwells) + " startles=" + tank.Agents.Sum(a => a.State.Startles));
        Console.WriteLine("Light stress integral across 8 seeds: flashing=" + flashIntegral.ToString("F3") +
            " steady=" + steadyIntegral.ToString("F3"));
    }
}
