using System;

namespace Aquarium.Core.Ecology
{
    public enum NitrogenPool { Food, Organic, Tan, Nitrite, Nitrate, Living, Plant }
    public enum FishlessChallengeStatus { NotStarted, Processing, Stabilizing, Passed, Invalidated }
    public enum TrialObservationStatus { NotStarted, Observing, Passed }
    public enum NitrogenOperation { Input, Output, Transfer, WaterInput, WaterOutput, ChallengeDose }

    // All masses are mg of nitrogen, never mg of the compound. Rates use game days.
    [Serializable]
    public sealed class EcologyParameters
    {
        public double VolumeLiters = 54;
        public double FixedStepGameDays = 1.0 / 1440;
        public double OrganicMineralizationPerDay = .65;
        public double LivingMetabolismPerDay = .35;
        public double InitialTanCapacityMgNPerDay = 1.5;
        public double InitialNitriteCapacityMgNPerDay = 1.5;
        public double MaximumCapacityMgNPerDay = 54;
        public double CapacityGainPerProcessedMgN = .6;
        public double CapacityDecayPerDay = .02;
        public double PlantUptakeMgNPerDay = 0;
        public double ChallengeDoseMgNPerLiter = .5;
        public double MaximumStableTanMgNPerLiter = .05;
        public double MaximumStableNitriteMgNPerLiter = .05;
        public double RequiredProcessedFraction = .9;
        public double StableGameHours = 24;
        public int RecentLedgerLimit = 256;
        public EcologyParameters Clone() { return (EcologyParameters)MemberwiseClone(); }
        public void Validate()
        {
            double[] values = { VolumeLiters, FixedStepGameDays, OrganicMineralizationPerDay,
                LivingMetabolismPerDay, InitialTanCapacityMgNPerDay, InitialNitriteCapacityMgNPerDay,
                MaximumCapacityMgNPerDay, CapacityGainPerProcessedMgN, CapacityDecayPerDay,
                PlantUptakeMgNPerDay, ChallengeDoseMgNPerLiter, MaximumStableTanMgNPerLiter,
                MaximumStableNitriteMgNPerLiter, RequiredProcessedFraction, StableGameHours };
            foreach (double v in values) EcologyGuard.Nonnegative(v, "Ecology parameter");
            if (VolumeLiters < 1 || VolumeLiters > 100000 || FixedStepGameDays <= 0 || FixedStepGameDays > 1.0 / 24 ||
                MaximumCapacityMgNPerDay < InitialTanCapacityMgNPerDay || MaximumCapacityMgNPerDay < InitialNitriteCapacityMgNPerDay ||
                ChallengeDoseMgNPerLiter <= 0 || RequiredProcessedFraction <= 0 || RequiredProcessedFraction > 1 ||
                StableGameHours <= 0 || RecentLedgerLimit < 1 || RecentLedgerLimit > 4096)
                throw new ArgumentException("Ecology parameters violate supported game-model ranges.");
        }
    }

    [Serializable]
    public sealed class NitrogenState
    {
        public double FoodMgN, OrganicMgN, TanMgN, NitriteMgN, NitrateMgN, LivingMgN, PlantMgN;
        public double TotalMgN { get { return FoodMgN + OrganicMgN + TanMgN + NitriteMgN + NitrateMgN + LivingMgN + PlantMgN; } }
        public NitrogenState Clone() { return (NitrogenState)MemberwiseClone(); }
        public double Get(NitrogenPool pool)
        {
            switch (pool) {
                case NitrogenPool.Food: return FoodMgN; case NitrogenPool.Organic: return OrganicMgN;
                case NitrogenPool.Tan: return TanMgN; case NitrogenPool.Nitrite: return NitriteMgN;
                case NitrogenPool.Nitrate: return NitrateMgN; case NitrogenPool.Living: return LivingMgN;
                case NitrogenPool.Plant: return PlantMgN; default: throw new ArgumentOutOfRangeException("pool"); }
        }
        internal void Set(NitrogenPool pool, double value)
        {
            switch (pool) {
                case NitrogenPool.Food: FoodMgN = value; break; case NitrogenPool.Organic: OrganicMgN = value; break;
                case NitrogenPool.Tan: TanMgN = value; break; case NitrogenPool.Nitrite: NitriteMgN = value; break;
                case NitrogenPool.Nitrate: NitrateMgN = value; break; case NitrogenPool.Living: LivingMgN = value; break;
                case NitrogenPool.Plant: PlantMgN = value; break; default: throw new ArgumentOutOfRangeException("pool"); }
        }
    }
    [Serializable]
    public sealed class WaterComposition
    {
        public double OrganicMgNPerLiter, TanMgNPerLiter, NitriteMgNPerLiter, NitrateMgNPerLiter;
        public WaterComposition Clone() { return (WaterComposition)MemberwiseClone(); }
        internal void Validate()
        {
            EcologyGuard.Nonnegative(OrganicMgNPerLiter, "OrganicMgNPerLiter");
            EcologyGuard.Nonnegative(TanMgNPerLiter, "TanMgNPerLiter");
            EcologyGuard.Nonnegative(NitriteMgNPerLiter, "NitriteMgNPerLiter");
            EcologyGuard.Nonnegative(NitrateMgNPerLiter, "NitrateMgNPerLiter");
        }
    }
    [Serializable]
    public sealed class FishlessChallengeState
    {
        public FishlessChallengeStatus Status;
        public double DoseMgN, OxidizedTanMgN, OxidizedNitriteMgN, StableGameHours, StartedGameDay;
        public long DoseSequence;
        public string InvalidatedReason;
        public FishlessChallengeState Clone() { return (FishlessChallengeState)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class TrialObservationState
    {
        public TrialObservationStatus Status;
        public double StableGameHours, StartedGameDay;
        public int LiveAnimalCount;
        public TrialObservationState Clone() { return (TrialObservationState)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class NitrogenLedgerEntry
    {
        public long Sequence;
        public double GameDay, MgN;
        public NitrogenOperation Operation;
        public NitrogenPool From, To;
        public string SourceId;
        public NitrogenLedgerEntry Clone() { return (NitrogenLedgerEntry)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class NitrogenAudit
    {
        public double CumulativeInputMgN, CumulativeOutputMgN;
        // Per-pool external totals and 7x7 transfer matrix retain the audit even when recent detail rolls off.
        public double[] InputByPoolMgN = new double[7], OutputByPoolMgN = new double[7], TransferMgN = new double[49];
        public NitrogenAudit Clone()
        {
            var result = (NitrogenAudit)MemberwiseClone();
            result.InputByPoolMgN = (double[])InputByPoolMgN.Clone(); result.OutputByPoolMgN = (double[])OutputByPoolMgN.Clone();
            result.TransferMgN = (double[])TransferMgN.Clone(); return result;
        }
    }
    [Serializable]
    public sealed class EcologySnapshot
    {
        public int SchemaVersion = 1;
        public EcologyParameters Parameters;
        public NitrogenState State;
        public NitrogenAudit Audit;
        public double GameDays, FixedAccumulatorGameDays, TanCapacityMgNPerDay, NitriteCapacityMgNPerDay;
        public bool FilterEnabled = true;
        public long NextSequence = 1;
        public FishlessChallengeState Challenge;
        public TrialObservationState TrialObservation;
        public NitrogenLedgerEntry[] RecentLedger;
    }
    internal static class EcologyGuard
    {
        internal static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
        internal static void Nonnegative(double v, string name)
        { if (!Finite(v) || v < 0 || v > 1e12) throw new ArgumentOutOfRangeException(name); }
        internal static void Source(string source)
        { if (string.IsNullOrWhiteSpace(source) || source.Length > 160) throw new ArgumentException("A bounded audit source ID is required."); }
        internal static bool Near(double a, double b) { return Math.Abs(a - b) <= 1e-7 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b))); }
    }
}
