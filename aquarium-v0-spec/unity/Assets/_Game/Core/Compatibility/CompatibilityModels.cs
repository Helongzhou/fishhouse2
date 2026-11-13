using System;
using System.Collections.Generic;

namespace Aquarium.Compatibility
{
    public enum WaterType { Unknown, Freshwater, Brackish, Saltwater }
    public enum ContentPolicy { Unknown, PurchasableV0, Planned, ReferenceOnly }
    public enum LifeStage { Unknown, Juvenile, Adult }
    public enum Sex { Unknown, Male, Female }
    public enum SubstrateType { Unknown, SoftSand, SmoothFineGravel, CoarseSharpGravel, BareBottom }
    public enum ReasonSeverity { HardBlock, HighRisk, Conditional, PendingVerification }
    public enum ReasonDomain { Ecology, Husbandry, ContentPolicy, DataQuality }
    public enum AssessmentStatus { Blocked, HighRisk, PendingVerification, ConditionsUnmet, Monitor }

    public struct ParameterRange
    {
        public double Min { get; private set; }
        public double Max { get; private set; }
        public ParameterRange(double min, double max) { Min = min; Max = max; }
        public bool IsValid { get { return IsFinite(Min) && IsFinite(Max) && Min <= Max; } }
        public bool Contains(double value) { return IsValid && IsFinite(value) && value >= Min && value <= Max; }
        internal static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public override string ToString() { return Min.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "–" + Max.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    }

    // These are reviewed species baselines, not values inferred from category or display name.
    public sealed class SpeciesProfile
    {
        public string Id { get; set; }
        public WaterType WaterType { get; set; }
        public ContentPolicy Policy { get; set; }
        public ParameterRange? TemperatureC { get; set; }
        public ParameterRange? Ph { get; set; }
        public ParameterRange? GhDgh { get; set; }
        public double? AdultMinTankLengthCm { get; set; }
        public double? AdultMinTankHeightCm { get; set; }
        public double? AdultMinSwimLengthCm { get; set; }
        public double? AdultMinSwimHeightCm { get; set; }
        public double? AdultMinUsableSwimVolumeL { get; set; }
        public int? RecommendedSameSpeciesGroupMin { get; set; }
        public bool? CanPreyOnSmallerAnimals { get; set; }
        public double? AdultGapeMm { get; set; }
        public double? AdultBodyCrossSectionMm { get; set; }
        public ISet<string> Tags { get; private set; }
        public SpeciesProfile() { Tags = new HashSet<string>(StringComparer.Ordinal); }
        public bool HasTag(string tag) { return Tags.Contains(tag); }
    }

    // A homogeneous cohort; split adult males/females/juveniles into separate groups.
    public sealed class StockGroup
    {
        public string Id { get; set; }
        public SpeciesProfile Species { get; set; }
        public int Count { get; set; }
        public LifeStage Stage { get; set; }
        public Sex Sex { get; set; }
        public double? CurrentGapeMm { get; set; }
        public double? CurrentBodyCrossSectionMm { get; set; }
    }

    public sealed class TankEnvironment
    {
        public WaterType WaterType { get; set; }
        public double? TemperatureC { get; set; }
        public double? Ph { get; set; }
        public double? GhDgh { get; set; }
        public double? LengthCm { get; set; }
        public double? HeightCm { get; set; }
        public double? UsableSwimLengthCm { get; set; }
        public double? UsableSwimHeightCm { get; set; }
        public double? UsableSwimVolumeL { get; set; }
        public SubstrateType Substrate { get; set; }
        public bool? HasAppropriateSinkingFood { get; set; }
    }

    public sealed class CompatibilityReason
    {
        public string Code { get; private set; }
        public string MessageKey { get; private set; }
        public string RepairKey { get; private set; }
        public IDictionary<string, string> Parameters { get; private set; }
        public ReasonSeverity Severity { get; private set; }
        public ReasonDomain Domain { get; private set; }
        public string SourceSpeciesId { get; private set; }
        // Namespaced id: species:<id>, group:<id>, tank:<parameter>, community, or purchase:<id>.
        public string TargetId { get; private set; }
        public string TargetSpeciesId { get; private set; }
        public IList<string> AffectedSpeciesIds { get; private set; }
        internal CompatibilityReason(string code, ReasonSeverity severity, ReasonDomain domain,
            string source, string target, string targetSpecies, IList<string> affected, IDictionary<string, string> parameters)
        {
            Code = code; MessageKey = "compatibility.reason." + code + ".message"; RepairKey = "compatibility.reason." + code + ".repair";
            Parameters = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(parameters));
            Severity = severity; Domain = domain; SourceSpeciesId = source;
            TargetId = target; TargetSpeciesId = targetSpecies;
            AffectedSpeciesIds = new List<string>(affected ?? new List<string>()).AsReadOnly();
        }
    }

    public sealed class CommunityRange
    {
        public string Parameter { get; private set; }
        public ParameterRange? KnownIntersection { get; private set; }
        public bool AllSpeciesVerified { get; private set; }
        public bool IsEmpty { get; private set; }
        internal CommunityRange(string name, ParameterRange? intersection, bool verified, bool empty)
        { Parameter = name; KnownIntersection = intersection; AllSpeciesVerified = verified; IsEmpty = empty; }
    }

    public sealed class CompatibilityAssessment
    {
        public IList<CompatibilityReason> Reasons { get; private set; }
        public IList<CommunityRange> CommunityRanges { get; private set; }
        public AssessmentStatus Status { get; private set; }
        public string Summary { get; private set; }
        public string SummaryKey { get; private set; }
        public bool HasHardBlock { get; private set; }
        public bool HasHighRisk { get; private set; }
        public bool HasPendingVerification { get; private set; }
        public bool HasUnmetConditions { get; private set; }
        // False for missing data as well as conflicts. A true value is not a survival guarantee.
        public bool IsEligibleForIntroduction { get { return !HasHardBlock && !HasHighRisk && !HasPendingVerification && !HasUnmetConditions; } }
        internal CompatibilityAssessment(List<CompatibilityReason> reasons, List<CommunityRange> ranges)
        {
            Reasons = reasons.AsReadOnly(); CommunityRanges = ranges.AsReadOnly();
            foreach (CompatibilityReason reason in reasons)
            {
                HasHardBlock |= reason.Severity == ReasonSeverity.HardBlock;
                HasHighRisk |= reason.Severity == ReasonSeverity.HighRisk;
                HasPendingVerification |= reason.Severity == ReasonSeverity.PendingVerification;
                HasUnmetConditions |= reason.Severity == ReasonSeverity.Conditional;
            }
            Status = HasHardBlock ? AssessmentStatus.Blocked : HasHighRisk ? AssessmentStatus.HighRisk :
                HasPendingVerification ? AssessmentStatus.PendingVerification : HasUnmetConditions ? AssessmentStatus.ConditionsUnmet : AssessmentStatus.Monitor;
            SummaryKey = Status == AssessmentStatus.Monitor ? "compatibility.summary.monitor" : "compatibility.summary.action_required";
            Summary = Status == AssessmentStatus.Monitor
                ? "No modeled conflict found. Quarantine, staged introduction, monitoring and an isolation plan are still required."
                : "Review every blocking, risk, unmet-condition and unverified-data reason. Resolve and reassess before introduction.";
        }
    }
}
