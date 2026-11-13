using System;
using System.Collections.Generic;

namespace Aquarium.Compatibility
{
    public static class CompatibilityEvaluator
    {
        private sealed class Context
        {
            public readonly List<CompatibilityReason> Reasons = new List<CompatibilityReason>();
            public readonly List<CommunityRange> Ranges = new List<CommunityRange>();
            public readonly Dictionary<string, SpeciesProfile> Species = new Dictionary<string, SpeciesProfile>(StringComparer.Ordinal);
            public readonly Dictionary<string, long> Counts = new Dictionary<string, long>(StringComparer.Ordinal);
            public readonly List<StockGroup> Groups = new List<StockGroup>();
            public void Add(string code, ReasonSeverity severity, ReasonDomain domain, string source,
                string target, string targetSpecies = null, IList<string> affected = null, IDictionary<string, string> parameters = null)
            {
                Dictionary<string, string> args = new Dictionary<string, string>(StringComparer.Ordinal);
                args["source"] = source ?? "unknown"; args["target"] = target ?? "unknown";
                args["source_species_id"] = source ?? "unknown"; args["target_id"] = target ?? "unknown";
                args["target_species_id"] = targetSpecies ?? "";
                args["parameter"] = target != null && target.StartsWith("tank:", StringComparison.Ordinal) ? target.Substring(5) : "";
                args["required_count"] = source != null && Species.ContainsKey(source) && Species[source].RecommendedSameSpeciesGroupMin.HasValue ? Species[source].RecommendedSameSpeciesGroupMin.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
                args["source_count"] = source != null && Counts.ContainsKey(source) ? Counts[source].ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
                if (parameters != null) foreach (KeyValuePair<string, string> entry in parameters) args[entry.Key] = entry.Value;
                Reasons.Add(new CompatibilityReason(code, severity, domain, source ?? "unknown", target,
                    targetSpecies, affected ?? new List<string> { source ?? "unknown" }, args));
            }
        }

        public static CompatibilityAssessment Evaluate(TankEnvironment tank, IEnumerable<StockGroup> composition)
        {
            if (tank == null) throw new ArgumentNullException("tank");
            if (composition == null) throw new ArgumentNullException("composition");
            Context c = new Context();
            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (StockGroup g in composition)
            {
                if (g == null || g.Species == null || string.IsNullOrWhiteSpace(g.Species.Id) ||
                    string.IsNullOrWhiteSpace(g.Id) || g.Count <= 0 || !groupIds.Add(g.Id))
                {
                    c.Add("INVALID_STOCK_GROUP", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality,
                        g != null && g.Species != null ? g.Species.Id : "unknown", g == null ? "community" : "group:" + g.Id);
                    continue;
                }
                if (c.Species.ContainsKey(g.Species.Id) && !ReferenceEquals(c.Species[g.Species.Id], g.Species))
                {
                    c.Add("INCONSISTENT_SPECIES_PROFILE", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality,
                        g.Species.Id, "group:" + g.Id);
                }
                else c.Species[g.Species.Id] = g.Species;
                c.Groups.Add(g);
                c.Counts[g.Species.Id] = c.Counts.ContainsKey(g.Species.Id) ? c.Counts[g.Species.Id] + g.Count : g.Count;
            }
            if (c.Groups.Count == 0)
                c.Add("NO_VALID_STOCK", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, "unknown", "community");

            CheckWaterType(c, tank);
            CheckRange(c, "temperature_c", delegate(SpeciesProfile p) { return p.TemperatureC; }, tank.TemperatureC);
            CheckRange(c, "ph", delegate(SpeciesProfile p) { return p.Ph; }, tank.Ph);
            CheckRange(c, "gh_dgh", delegate(SpeciesProfile p) { return p.GhDgh; }, tank.GhDgh);
            foreach (KeyValuePair<string, SpeciesProfile> pair in c.Species)
            {
                SpeciesProfile p = pair.Value;
                CheckPolicy(c, p);
                CheckDimension(c, p, "tank_length_cm", p.AdultMinTankLengthCm, tank.LengthCm);
                CheckDimension(c, p, "tank_height_cm", p.AdultMinTankHeightCm, tank.HeightCm);
                CheckDimension(c, p, "swim_length_cm", p.AdultMinSwimLengthCm, tank.UsableSwimLengthCm);
                CheckDimension(c, p, "swim_height_cm", p.AdultMinSwimHeightCm, tank.UsableSwimHeightCm);
                CheckDimension(c, p, "usable_swim_volume_l", p.AdultMinUsableSwimVolumeL, tank.UsableSwimVolumeL);
                CheckShoal(c, p);
                CheckBottomSifter(c, p, tank);
                CheckBetta(c, p);
            }
            CheckFinNipping(c);
            CheckPredation(c);
            return new CompatibilityAssessment(c.Reasons, c.Ranges);
        }

        private static void CheckPolicy(Context c, SpeciesProfile p)
        {
            if (p.Policy == ContentPolicy.Planned || p.Policy == ContentPolicy.ReferenceOnly)
                c.Add("V0_CONTENT_NOT_PURCHASABLE", ReasonSeverity.HardBlock, ReasonDomain.ContentPolicy, p.Id, "purchase:" + p.Id);
            else if (p.Policy == ContentPolicy.Unknown)
                c.Add("CONTENT_POLICY_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.ContentPolicy, p.Id, "purchase:" + p.Id);
        }

        private static void CheckWaterType(Context c, TankEnvironment tank)
        {
            WaterType? first = null; string firstId = null;
            if (tank.WaterType == WaterType.Unknown)
                c.Add("TANK_WATER_TYPE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, "unknown", "tank:water_type");
            foreach (SpeciesProfile p in c.Species.Values)
            {
                if (p.WaterType == WaterType.Unknown)
                {
                    c.Add("SPECIES_WATER_TYPE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "tank:water_type");
                    continue;
                }
                if (tank.WaterType != WaterType.Unknown && p.WaterType != tank.WaterType)
                    c.Add("TANK_WATER_TYPE_MISMATCH", ReasonSeverity.HardBlock, ReasonDomain.Ecology, p.Id, "tank:water_type");
                if (first.HasValue && first.Value != p.WaterType)
                    c.Add("COMMUNITY_WATER_TYPE_MISMATCH", ReasonSeverity.HardBlock, ReasonDomain.Ecology, p.Id, "species:" + firstId, firstId, new List<string> { firstId, p.Id });
                if (!first.HasValue) { first = p.WaterType; firstId = p.Id; }
            }
        }

        private static void CheckRange(Context c, string name, Func<SpeciesProfile, ParameterRange?> read, double? actual)
        {
            double lower = double.NegativeInfinity, upper = double.PositiveInfinity;
            string lowerId = null, upperId = null;
            bool allKnown = true, anyKnown = false;
            List<string> affected = new List<string>(c.Species.Keys);
            foreach (SpeciesProfile p in c.Species.Values)
            {
                ParameterRange? range = read(p);
                if (!range.HasValue || !range.Value.IsValid)
                {
                    allKnown = false;
                    c.Add("SPECIES_RANGE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "tank:" + name);
                    continue;
                }
                anyKnown = true;
                if (range.Value.Min > lower) { lower = range.Value.Min; lowerId = p.Id; }
                if (range.Value.Max < upper) { upper = range.Value.Max; upperId = p.Id; }
                if (actual.HasValue && ParameterRange.IsFinite(actual.Value) && !range.Value.Contains(actual.Value))
                    c.Add("TANK_PARAMETER_OUTSIDE_SPECIES_RANGE", ReasonSeverity.HardBlock, ReasonDomain.Ecology, p.Id, "tank:" + name);
            }
            bool empty = anyKnown && lower > upper;
            c.Ranges.Add(new CommunityRange(name, anyKnown && !empty ? new ParameterRange?(new ParameterRange(lower, upper)) : null, allKnown && anyKnown, empty));
            if (empty)
                c.Add("COMMUNITY_RANGE_EMPTY", ReasonSeverity.HardBlock, ReasonDomain.Ecology, lowerId, "species:" + upperId, upperId, affected, new Dictionary<string, string> { { "parameter", name }, { "lower_bound", Number(lower) }, { "upper_bound", Number(upper) } });
            if (!actual.HasValue || !ParameterRange.IsFinite(actual.Value))
                c.Add("TANK_PARAMETER_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, "unknown", "tank:" + name, null, affected);
        }

        private static void CheckDimension(Context c, SpeciesProfile p, string dimension, double? required, double? available)
        {
            if (!IsPositive(required) || !IsPositive(available))
                c.Add("ADULT_SPACE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "tank:" + dimension);
            else if (available.Value < required.Value)
                c.Add("ADULT_SPACE_INSUFFICIENT", ReasonSeverity.HardBlock, ReasonDomain.Husbandry, p.Id, "tank:" + dimension, parameters: new Dictionary<string, string> { { "required", Number(required.Value) }, { "available", Number(available.Value) } });
        }

        private static void CheckShoal(Context c, SpeciesProfile p)
        {
            if (!p.RecommendedSameSpeciesGroupMin.HasValue || p.RecommendedSameSpeciesGroupMin.Value < 1)
                c.Add("SPECIES_GROUP_MIN_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "species:" + p.Id, p.Id);
            else if (c.Counts[p.Id] < p.RecommendedSameSpeciesGroupMin.Value)
                c.Add("SAME_SPECIES_GROUP_TOO_SMALL", ReasonSeverity.Conditional, ReasonDomain.Husbandry, p.Id, "species:" + p.Id, p.Id);
        }

        private static void CheckBottomSifter(Context c, SpeciesProfile p, TankEnvironment tank)
        {
            if (!p.HasTag("bottom_sifter")) return;
            if (tank.Substrate == SubstrateType.Unknown)
                c.Add("BOTTOM_SIFTER_SUBSTRATE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "tank:substrate");
            else if (tank.Substrate != SubstrateType.SoftSand)
                c.Add("BOTTOM_SIFTER_NEEDS_SOFT_SAND", ReasonSeverity.Conditional, ReasonDomain.Husbandry, p.Id, "tank:substrate");
            if (!tank.HasAppropriateSinkingFood.HasValue)
                c.Add("BOTTOM_SIFTER_FOOD_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "tank:food");
            else if (!tank.HasAppropriateSinkingFood.Value)
                c.Add("BOTTOM_SIFTER_NEEDS_SINKING_FOOD", ReasonSeverity.Conditional, ReasonDomain.Husbandry, p.Id, "tank:food");
        }

        private static void CheckBetta(Context c, SpeciesProfile p)
        {
            if (!p.HasTag("betta_male_conspecific_isolation")) return;
            long males = 0, unknown = 0;
            foreach (StockGroup g in c.Groups)
            {
                if (g.Species.Id != p.Id) continue;
                if (g.Stage == LifeStage.Adult && g.Sex == Sex.Male) males += g.Count;
                if (g.Sex == Sex.Unknown || g.Stage == LifeStage.Unknown) unknown += g.Count;
            }
            if (males > 1)
                c.Add("ADULT_MALE_BETTA_CONSPECIFIC_BLOCK", ReasonSeverity.HardBlock, ReasonDomain.Husbandry, p.Id, "species:" + p.Id, p.Id);
            if (unknown > 0)
                c.Add("BETTA_SEX_OR_STAGE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, p.Id, "species:" + p.Id, p.Id);
            if (c.Counts[p.Id] > 1 && males <= 1 && unknown == 0)
                c.Add("BETTA_CONSPECIFIC_CONDITIONS_REVIEW", ReasonSeverity.Conditional, ReasonDomain.Husbandry, p.Id, "species:" + p.Id, p.Id);
        }

        private static void CheckFinNipping(Context c)
        {
            foreach (SpeciesProfile source in c.Species.Values)
            {
                if (!source.HasTag("fin_nipper")) continue;
                foreach (SpeciesProfile target in c.Species.Values)
                {
                    if (!target.HasTag("long_fin") || target.Id == source.Id) continue;
                    c.Add("DIRECTIONAL_FIN_NIPPING_RISK", ReasonSeverity.HighRisk, ReasonDomain.Ecology, source.Id, "species:" + target.Id, target.Id, new List<string> { source.Id, target.Id });
                }
            }
        }

        private static void CheckPredation(Context c)
        {
            foreach (StockGroup source in c.Groups)
            {
                if (c.Groups.Count < 2) continue;
                if (!source.Species.CanPreyOnSmallerAnimals.HasValue)
                {
                    c.Add("PREDATION_PROFILE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, source.Species.Id, "group:" + source.Id);
                    continue;
                }
                if (!source.Species.CanPreyOnSmallerAnimals.Value) continue;
                foreach (StockGroup target in c.Groups)
                {
                    if (ReferenceEquals(source, target)) continue;
                    double? gape = source.CurrentGapeMm ?? (source.Stage == LifeStage.Adult ? source.Species.AdultGapeMm : null);
                    double? body = target.CurrentBodyCrossSectionMm ?? (target.Stage == LifeStage.Adult ? target.Species.AdultBodyCrossSectionMm : null);
                    if (!IsPositive(gape) || !IsPositive(body))
                    {
                        c.Add("PREDATION_SIZE_UNKNOWN", ReasonSeverity.PendingVerification, ReasonDomain.DataQuality, source.Species.Id, "group:" + target.Id, target.Species.Id, new List<string> { source.Species.Id, target.Species.Id });
                    }
                    else if (gape.Value >= body.Value)
                        c.Add("GAPE_PREDATION_RISK", ReasonSeverity.HighRisk, ReasonDomain.Ecology, source.Species.Id, "group:" + target.Id, target.Species.Id, new List<string> { source.Species.Id, target.Species.Id });
                }
            }
        }

        private static string Number(double value) { return value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
        private static bool IsPositive(double? value) { return value.HasValue && ParameterRange.IsFinite(value.Value) && value.Value > 0; }
    }
}
