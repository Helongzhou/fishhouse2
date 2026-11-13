using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aquarium.Compatibility;

internal static class Program
{
    private static int passed;
    private static int failed;
    private static readonly HashSet<string> observedCodes = new HashSet<string>(StringComparer.Ordinal);
    private static readonly List<CompatibilityReason> observedReasons = new List<CompatibilityReason>();
    private static readonly HashSet<string> observedKeys = new HashSet<string>(StringComparer.Ordinal);
    private static void Main(string[] args)
    {
        Run("complete modeled profile means monitor, never a safety guarantee", delegate {
            CompatibilityAssessment a = Assess(Fish("a"));
            Equal(AssessmentStatus.Monitor, a.Status); True(a.IsEligibleForIntroduction); True(a.Summary.Contains("monitoring"));
        });
        Run("three-species temperature intersection, not one favored pair", delegate {
            SpeciesProfile a = Fish("a"), b = Fish("b"), d = Fish("c");
            a.TemperatureC = new ParameterRange(20, 24); b.TemperatureC = new ParameterRange(22, 26); d.TemperatureC = new ParameterRange(25, 28);
            CompatibilityAssessment r = Assess(a, b, d);
            CompatibilityReason e = Reason(r, "COMMUNITY_RANGE_EMPTY");
            Equal("c", e.SourceSpeciesId); Equal("a", e.TargetSpeciesId); Equal(3, e.AffectedSpeciesIds.Count); True(r.HasHardBlock);
            True(r.CommunityRanges.Single(x => x.Parameter == "temperature_c").IsEmpty);
        });
        Run("all species pH ranges must intersect even with freshwater labels", delegate {
            SpeciesProfile a = Fish("acid"), b = Fish("alkaline"); a.Ph = new ParameterRange(5.5, 6.5); b.Ph = new ParameterRange(7.3, 8.2);
            True(Assess(a, b).Reasons.Any(x => x.Code == "COMMUNITY_RANGE_EMPTY" && x.TargetId == "species:acid"));
        });
        Run("GH intersection and tank value both matter", delegate {
            SpeciesProfile a = Fish("soft"), b = Fish("hard"); a.GhDgh = new ParameterRange(2, 5); b.GhDgh = new ParameterRange(10, 15);
            CompatibilityAssessment r = Assess(a, b); True(r.HasHardBlock); True(r.CommunityRanges.Single(x => x.Parameter == "gh_dgh").IsEmpty);
            True(r.Reasons.Count(x => x.Code == "TANK_PARAMETER_OUTSIDE_SPECIES_RANGE") == 2);
        });
        Run("range boundary is inclusive", delegate {
            SpeciesProfile a = Fish("a"); a.TemperatureC = new ParameterRange(24, 24); Equal(AssessmentStatus.Monitor, Assess(a).Status);
        });
        Run("unknown species range is pending, never default freshwater approval", delegate {
            SpeciesProfile a = Fish("a"); a.Ph = null; CompatibilityAssessment r = Assess(a); Has(r, "SPECIES_RANGE_UNKNOWN"); False(r.IsEligibleForIntroduction);
            False(r.CommunityRanges.Single(x => x.Parameter == "ph").AllSpeciesVerified);
        });
        Run("invalid range and non-finite values are unverified", delegate {
            SpeciesProfile a = Fish("a"); a.Ph = new ParameterRange(8, 6); a.GhDgh = new ParameterRange(double.NaN, 10); CompatibilityAssessment r = Assess(a); Equal(2, r.Reasons.Count(x => x.Code == "SPECIES_RANGE_UNKNOWN"));
        });
        Run("unknown tank measurement remains pending with known intersection", delegate {
            TankEnvironment t = Tank(); t.TemperatureC = null; CompatibilityAssessment r = Evaluate(t, Group(Fish("a"))); Has(r, "TANK_PARAMETER_UNKNOWN"); False(r.IsEligibleForIntroduction);
        });
        Run("non-finite tank measurement is pending", delegate {
            TankEnvironment t = Tank(); t.Ph = double.PositiveInfinity; Has(Evaluate(t, Group(Fish("a"))), "TANK_PARAMETER_UNKNOWN");
        });
        Run("unknown species water type is pending", delegate {
            SpeciesProfile a = Fish("a"); a.WaterType = WaterType.Unknown; Has(Assess(a), "SPECIES_WATER_TYPE_UNKNOWN");
        });
        Run("mixed water types blocked even when tank water type is unknown", delegate {
            SpeciesProfile a = Fish("fresh"), b = Fish("salt"); b.WaterType = WaterType.Saltwater; TankEnvironment t = Tank(); t.WaterType = WaterType.Unknown;
            CompatibilityAssessment r = Evaluate(t, Group(a), Group(b)); Has(r, "COMMUNITY_WATER_TYPE_MISMATCH"); Has(r, "TANK_WATER_TYPE_UNKNOWN"); True(r.HasHardBlock);
        });
        Run("tank water mismatch is a hard block", delegate {
            SpeciesProfile a = Fish("salt"); a.WaterType = WaterType.Saltwater; Has(Assess(a), "TANK_WATER_TYPE_MISMATCH");
        });
        Run("adult space still applies to a juvenile", delegate {
            SpeciesProfile a = Fish("tall_adult"); a.AdultMinTankLengthCm = 90; a.AdultMinTankHeightCm = 50;
            StockGroup g = Group(a); g.Stage = LifeStage.Juvenile; CompatibilityAssessment r = Evaluate(Tank(), g); Equal(2, r.Reasons.Count(x => x.Code == "ADULT_SPACE_INSUFFICIENT"));
        });
        Run("hardscape reducing usable swimming space can block large fish", delegate {
            SpeciesProfile a = Fish("a"); a.AdultMinSwimLengthCm = 58; a.AdultMinSwimHeightCm = 30; a.AdultMinUsableSwimVolumeL = 50;
            Equal(3, Assess(a).Reasons.Count(x => x.Code == "ADULT_SPACE_INSUFFICIENT"));
        });
        Run("unknown adult dimensions are pending", delegate {
            SpeciesProfile a = Fish("a"); a.AdultMinTankHeightCm = null; Has(Assess(a), "ADULT_SPACE_UNKNOWN");
        });
        Run("other fish and shrimp cannot fill a same-species school minimum", delegate {
            SpeciesProfile cory = Fish("cory"), other = Fish("other"), shrimp = Fish("shrimp"); cory.RecommendedSameSpeciesGroupMin = 6;
            CompatibilityAssessment r = Evaluate(Tank(), Group(cory, 3), Group(other, 12), Group(shrimp, 20)); CompatibilityReason e = Reason(r, "SAME_SPECIES_GROUP_TOO_SMALL");
            Equal("cory", e.SourceSpeciesId); Equal("3", e.Parameters["source_count"]); Equal(ReasonSeverity.Conditional, e.Severity);
        });
        Run("same species cohorts count together", delegate {
            SpeciesProfile cory = Fish("cory"); cory.RecommendedSameSpeciesGroupMin = 6; StockGroup a = Group(cory, 3), b = Group(cory, 3); b.Id = "cory_b";
            False(Evaluate(Tank(), a, b).Reasons.Any(x => x.Code == "SAME_SPECIES_GROUP_TOO_SMALL"));
        });
        Run("same-species population totals do not overflow a 32-bit integer", delegate {
            SpeciesProfile p = Fish("many"); StockGroup a = Group(p, int.MaxValue), b = Group(p, int.MaxValue); b.Id = "many_b";
            p.Tags.Add("fin_nipper"); SpeciesProfile target = Fish("longfin"); target.Tags.Add("long_fin");
            CompatibilityReason r = Reason(Evaluate(Tank(), a, b, Group(target)), "DIRECTIONAL_FIN_NIPPING_RISK"); Equal("4294967294", r.Parameters["source_count"]);
        });
        Run("unknown grouping baseline remains pending", delegate {
            SpeciesProfile a = Fish("a"); a.RecommendedSameSpeciesGroupMin = null; Has(Assess(a), "SPECIES_GROUP_MIN_UNKNOWN");
        });
        Run("two adult male bettas across cohorts blocked", delegate {
            SpeciesProfile p = Betta(); StockGroup a = Group(p), b = Group(p); b.Id = "betta_b"; a.Sex = b.Sex = Sex.Male;
            CompatibilityAssessment r = Evaluate(Tank(), a, b); Equal(ReasonSeverity.HardBlock, Reason(r, "ADULT_MALE_BETTA_CONSPECIFIC_BLOCK").Severity);
        });
        Run("unknown betta sex cannot be silently approved", delegate {
            StockGroup g = Group(Betta()); g.Sex = Sex.Unknown; CompatibilityAssessment r = Evaluate(Tank(), g); Has(r, "BETTA_SEX_OR_STAGE_UNKNOWN"); False(r.IsEligibleForIntroduction);
        });
        Run("unknown betta stage cannot be silently approved", delegate {
            StockGroup g = Group(Betta()); g.Stage = LifeStage.Unknown; Has(Evaluate(Tank(), g), "BETTA_SEX_OR_STAGE_UNKNOWN");
        });
        Run("female bettas do not become an all-fish-must-fight claim", delegate {
            StockGroup g = Group(Betta(), 2); g.Sex = Sex.Female; CompatibilityAssessment r = Evaluate(Tank(), g);
            False(r.Reasons.Any(x => x.Code == "ADULT_MALE_BETTA_CONSPECIFIC_BLOCK")); Has(r, "BETTA_CONSPECIFIC_CONDITIONS_REVIEW");
        });
        Run("a single known male with unrelated fish is not blanket blocked", delegate {
            StockGroup g = Group(Betta()); g.Sex = Sex.Male; CompatibilityAssessment r = Evaluate(Tank(), g, Group(Fish("other"))); False(r.HasHardBlock);
        });
        Run("fin nipping is directional and identifies the victim", delegate {
            SpeciesProfile n = Fish("barb"), l = Fish("longfin"); n.Tags.Add("fin_nipper"); l.Tags.Add("long_fin"); n.RecommendedSameSpeciesGroupMin = 6;
            CompatibilityAssessment r = Evaluate(Tank(), Group(n, 8), Group(l)); CompatibilityReason e = Reason(r, "DIRECTIONAL_FIN_NIPPING_RISK");
            Equal("barb", e.SourceSpeciesId); Equal("longfin", e.TargetSpeciesId); Equal("species:longfin", e.TargetId); Equal(ReasonSeverity.HighRisk, e.Severity); False(r.HasHardBlock);
            Equal(1, r.Reasons.Count(x => x.Code == "DIRECTIONAL_FIN_NIPPING_RISK"));
        });
        Run("fin nipper label alone does not invent a long-fin victim", delegate {
            SpeciesProfile n = Fish("barb"); n.Tags.Add("fin_nipper"); False(Assess(n, Fish("shortfin")).HasHighRisk);
        });
        Run("bottom sifter requires soft sand and appropriate sinking food", delegate {
            SpeciesProfile p = Fish("cory"); p.Tags.Add("bottom_sifter"); TankEnvironment t = Tank(); t.Substrate = SubstrateType.CoarseSharpGravel; t.HasAppropriateSinkingFood = false;
            CompatibilityAssessment r = Evaluate(t, Group(p)); Has(r, "BOTTOM_SIFTER_NEEDS_SOFT_SAND"); Has(r, "BOTTOM_SIFTER_NEEDS_SINKING_FOOD");
        });
        Run("unknown substrate and food are unverified, not assumed correct", delegate {
            SpeciesProfile p = Fish("cory"); p.Tags.Add("bottom_sifter"); TankEnvironment t = Tank(); t.Substrate = SubstrateType.Unknown; t.HasAppropriateSinkingFood = null;
            CompatibilityAssessment r = Evaluate(t, Group(p)); Has(r, "BOTTOM_SIFTER_SUBSTRATE_UNKNOWN"); Has(r, "BOTTOM_SIFTER_FOOD_UNKNOWN");
        });
        Run("proper bottom-sifter provisions satisfy the modeled conditions", delegate {
            SpeciesProfile p = Fish("cory"); p.Tags.Add("bottom_sifter"); Equal(AssessmentStatus.Monitor, Assess(p).Status);
        });
        Run("gape and body cross-section flag juvenile prey risk", delegate {
            SpeciesProfile predator = Fish("predator"), prey = Fish("shrimp"); predator.CanPreyOnSmallerAnimals = true;
            StockGroup p = Group(predator), s = Group(prey, 10); p.CurrentGapeMm = 3; s.Stage = LifeStage.Juvenile; s.CurrentBodyCrossSectionMm = 1;
            CompatibilityReason e = Reason(Evaluate(Tank(), p, s), "GAPE_PREDATION_RISK"); Equal("predator", e.SourceSpeciesId); Equal("shrimp", e.TargetSpeciesId); Equal(ReasonSeverity.HighRisk, e.Severity);
        });
        Run("missing current juvenile body dimension is pending, not category extermination", delegate {
            SpeciesProfile predator = Fish("predator"); predator.CanPreyOnSmallerAnimals = true; StockGroup p = Group(predator), s = Group(Fish("shrimp")); s.Stage = LifeStage.Juvenile;
            CompatibilityAssessment r = Evaluate(Tank(), p, s); Has(r, "PREDATION_SIZE_UNKNOWN"); False(r.Reasons.Any(x => x.Code == "GAPE_PREDATION_RISK")); False(r.IsEligibleForIntroduction);
        });
        Run("unknown gape does not fall back to taxonomy", delegate {
            SpeciesProfile p = Fish("predator"); p.CanPreyOnSmallerAnimals = true; p.AdultGapeMm = null;
            Has(Assess(p, Fish("small_fish")), "PREDATION_SIZE_UNKNOWN");
        });
        Run("a larger cross-section does not trigger this swallowing model", delegate {
            SpeciesProfile p = Fish("predator"); p.CanPreyOnSmallerAnimals = true; p.AdultGapeMm = 1; SpeciesProfile s = Fish("shrimp"); s.AdultBodyCrossSectionMm = 4;
            False(Assess(p, s).Reasons.Any(x => x.Code == "GAPE_PREDATION_RISK"));
        });
        Run("unknown predation baseline remains unverified", delegate {
            SpeciesProfile p = Fish("unknown_diet"); p.CanPreyOnSmallerAnimals = null; Has(Assess(p, Fish("other")), "PREDATION_PROFILE_UNKNOWN");
        });
        Run("same-species adult can pose a size risk to juvenile cohort", delegate {
            SpeciesProfile p = Fish("cannibal_capability"); p.CanPreyOnSmallerAnimals = true; StockGroup a = Group(p), j = Group(p); j.Id = "juvenile"; j.Stage = LifeStage.Juvenile; j.CurrentBodyCrossSectionMm = 0.5; j.CurrentGapeMm = 0.1;
            True(Evaluate(Tank(), a, j).Reasons.Any(x => x.Code == "GAPE_PREDATION_RISK" && x.TargetId == "group:juvenile"));
        });
        Run("planned/reference-only species blocked by content policy, not ecology", delegate {
            SpeciesProfile a = Fish("planned"), b = Fish("reference"); a.Policy = ContentPolicy.Planned; b.Policy = ContentPolicy.ReferenceOnly;
            CompatibilityAssessment r = Assess(a, b); Equal(2, r.Reasons.Count(x => x.Code == "V0_CONTENT_NOT_PURCHASABLE")); True(r.Reasons.All(x => x.Domain == ReasonDomain.ContentPolicy));
        });
        Run("unknown content policy is unverified", delegate {
            SpeciesProfile p = Fish("p"); p.Policy = ContentPolicy.Unknown; Has(Assess(p), "CONTENT_POLICY_UNKNOWN");
        });
        Run("returns hard blocks, high risk, conditions and unknowns together", delegate {
            SpeciesProfile a = Fish("nipper"), b = Fish("longfin"); a.Tags.Add("fin_nipper"); b.Tags.Add("long_fin"); a.Policy = ContentPolicy.Planned;
            b.Ph = null; b.RecommendedSameSpeciesGroupMin = 6; CompatibilityAssessment r = Assess(a, b);
            True(r.HasHardBlock && r.HasHighRisk && r.HasPendingVerification && r.HasUnmetConditions); Equal(AssessmentStatus.Blocked, r.Status);
        });
        Run("invalid or duplicate stock cannot produce a clean result", delegate {
            SpeciesProfile p = Fish("a"); StockGroup a = Group(p), b = Group(p); Has(Evaluate(Tank(), a, b), "INVALID_STOCK_GROUP");
            Has(Evaluate(Tank(), new StockGroup()), "NO_VALID_STOCK");
        });
        Run("same species id with inconsistent profile instances is unverified", delegate {
            StockGroup a = Group(Fish("a")), b = Group(Fish("a")); b.Id = "a_other"; Has(Evaluate(Tank(), a, b), "INCONSISTENT_SPECIES_PROFILE");
        });
        Run("every emitted reason has structured source, target, keys and parameters", delegate {
            Equal(28, observedCodes.Count);
        });
        Run("registry-driven compatibility message coverage for every ready and QA locale", delegate { CheckCatalogs(); });
        Console.WriteLine("Compatibility checks: " + passed + " passed, " + failed + " failed; observed " + observedCodes.Count + " reason codes.");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    private static SpeciesProfile Fish(string id)
    {
        return new SpeciesProfile { Id = id, WaterType = WaterType.Freshwater, Policy = ContentPolicy.PurchasableV0,
            TemperatureC = new ParameterRange(20, 28), Ph = new ParameterRange(6, 8), GhDgh = new ParameterRange(2, 15),
            AdultMinTankLengthCm = 30, AdultMinTankHeightCm = 20, AdultMinSwimLengthCm = 20,
            AdultMinSwimHeightCm = 15, AdultMinUsableSwimVolumeL = 10, RecommendedSameSpeciesGroupMin = 1,
            CanPreyOnSmallerAnimals = false, AdultGapeMm = 2, AdultBodyCrossSectionMm = 5 };
    }
    private static SpeciesProfile Betta() { SpeciesProfile p = Fish("betta"); p.Tags.Add("betta_male_conspecific_isolation"); return p; }
    private static StockGroup Group(SpeciesProfile p, int count = 1) { return new StockGroup { Id = p.Id + "_group", Species = p, Count = count, Stage = LifeStage.Adult, Sex = Sex.Female }; }
    private static TankEnvironment Tank() { return new TankEnvironment { WaterType = WaterType.Freshwater, TemperatureC = 24, Ph = 7, GhDgh = 8, LengthCm = 60, HeightCm = 36, UsableSwimLengthCm = 55, UsableSwimHeightCm = 25, UsableSwimVolumeL = 45, Substrate = SubstrateType.SoftSand, HasAppropriateSinkingFood = true }; }
    private static CompatibilityAssessment Assess(params SpeciesProfile[] profiles) { return Evaluate(Tank(), profiles.Select(x => Group(x)).ToArray()); }
    private static CompatibilityAssessment Evaluate(TankEnvironment t, params StockGroup[] groups)
    {
        CompatibilityAssessment a = CompatibilityEvaluator.Evaluate(t, groups);
        foreach (CompatibilityReason r in a.Reasons)
        {
            observedReasons.Add(r); observedCodes.Add(r.Code); observedKeys.Add(r.MessageKey); observedKeys.Add(r.RepairKey);
            True(!string.IsNullOrWhiteSpace(r.SourceSpeciesId)); True(!string.IsNullOrWhiteSpace(r.TargetId));
            Equal("compatibility.reason." + r.Code + ".message", r.MessageKey); Equal("compatibility.reason." + r.Code + ".repair", r.RepairKey);
            True(r.Parameters.ContainsKey("source") && r.Parameters.ContainsKey("target") && r.Parameters.ContainsKey("source_count"));
        }
        observedKeys.Add(a.SummaryKey);
        return a;
    }
    private static CompatibilityReason Reason(CompatibilityAssessment a, string code) { CompatibilityReason r = a.Reasons.FirstOrDefault(x => x.Code == code); if (r == null) throw new Exception("Missing reason: " + code); return r; }
    private static void Has(CompatibilityAssessment a, string code) { Reason(a, code); }
    private static void True(bool value) { if (!value) throw new Exception("Expected true"); }
    private static void False(bool value) { if (value) throw new Exception("Expected false"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    private static void CheckCatalogs()
    {
        string root = FindRoot();
        using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "data", "localization", "locales.json"))))
        foreach (JsonElement locale in manifest.RootElement.GetProperty("locales").EnumerateArray())
        {
            string language = locale.GetProperty("code").GetString();
            string status = locale.GetProperty("status").GetString();
            if (status == "planned") continue;
            bool requireComplete = status == "ready" || status == "qa" || locale.GetProperty("release_required").GetBoolean();
            string path = Path.Combine(root, "data", "localization", language + ".json");
            if (!File.Exists(path)) throw new Exception("Missing shared catalog: " + path);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path)))
                foreach (string key in observedKeys)
                {
                    JsonElement value;
                    if (!doc.RootElement.TryGetProperty(key, out value))
                    {
                        if (!requireComplete) continue; // Drafts are checked against their registered fallback by the catalog validator.
                        throw new Exception(language + " missing required string for " + key);
                    }
                    if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                        throw new Exception(language + " missing nonempty string for " + key);
                    foreach (CompatibilityReason reason in observedReasons.Where(x => x.MessageKey == key || x.RepairKey == key))
                        foreach (Match match in Regex.Matches(value.GetString(), @"\{([a-z_]+)\}"))
                            True(reason.Parameters.ContainsKey(match.Groups[1].Value));
                }
        }
    }
    private static string FindRoot()
    {
        DirectoryInfo d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null) { if (Directory.Exists(Path.Combine(d.FullName, "data")) && Directory.Exists(Path.Combine(d.FullName, "unity"))) return d.FullName; d = d.Parent; }
        throw new Exception("Cannot find project root");
    }
}
