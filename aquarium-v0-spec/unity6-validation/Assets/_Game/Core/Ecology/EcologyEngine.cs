using System;
using System.Collections.Generic;

namespace Aquarium.Core.Ecology
{
    // A calibrated game model, not a scientifically validated aquarium predictor.
    public sealed class EcologyEngine
    {
        private readonly EcologyParameters parameters;
        private NitrogenState state = new NitrogenState();
        private NitrogenAudit audit = new NitrogenAudit();
        private FishlessChallengeState challenge = new FishlessChallengeState();
        private TrialObservationState trial = new TrialObservationState();
        private readonly List<NitrogenLedgerEntry> ledger = new List<NitrogenLedgerEntry>();
        private double accumulator;
        private long nextSequence = 1;
        public double GameDays { get; private set; }
        public double VolumeLiters { get { return parameters.VolumeLiters; } }
        public bool FilterEnabled { get; private set; } = true;
        public double TanCapacityMgNPerDay { get; private set; }
        public double NitriteCapacityMgNPerDay { get; private set; }
        public EcologyParameters Parameters { get { return parameters.Clone(); } }
        public NitrogenState State { get { return state.Clone(); } }
        public NitrogenAudit Audit { get { return audit.Clone(); } }
        public FishlessChallengeState Challenge { get { return challenge.Clone(); } }
        public TrialObservationState TrialObservation { get { return trial.Clone(); } }
        public double TanMgNPerLiter { get { return state.TanMgN / VolumeLiters; } }
        public double NitriteMgNPerLiter { get { return state.NitriteMgN / VolumeLiters; } }
        public double NitrateMgNPerLiter { get { return state.NitrateMgN / VolumeLiters; } }
        public bool WaterIsStable { get { return FilterEnabled && TanMgNPerLiter <= parameters.MaximumStableTanMgNPerLiter && NitriteMgNPerLiter <= parameters.MaximumStableNitriteMgNPerLiter; } }

        public EcologyEngine(EcologyParameters parameters = null)
        {
            this.parameters = (parameters ?? new EcologyParameters()).Clone(); this.parameters.Validate();
            TanCapacityMgNPerDay = this.parameters.InitialTanCapacityMgNPerDay;
            NitriteCapacityMgNPerDay = this.parameters.InitialNitriteCapacityMgNPerDay;
        }
        public void SetFilterEnabled(bool enabled)
        {
            FilterEnabled = enabled;
            if (!enabled) ResetStableWindows();
        }
        public void AddNitrogen(NitrogenPool pool, double mgN, string sourceId)
        {
            ValidateAmount(pool, mgN, sourceId); ValidateAddition(pool, mgN);
            InvalidateChallenge("additional_input");
            Input(pool, mgN, NitrogenOperation.Input, sourceId); RecheckWindows();
        }
        public void TransferNitrogen(NitrogenPool from, NitrogenPool to, double mgN, string sourceId)
        {
            ValidateAmount(from, mgN, sourceId); state.Get(to);
            if (from == to || mgN > state.Get(from)) throw new ArgumentException("Transfer must move available nitrogen to another pool.");
            EcologyGuard.Nonnegative(state.Get(to) + mgN, "Resulting mass");
            InvalidateChallenge("manual_transfer"); Transfer(from, to, mgN, sourceId); RecheckWindows();
        }
        public void RemoveNitrogen(NitrogenPool pool, double mgN, string sourceId)
        {
            ValidateAmount(pool, mgN, sourceId);
            if (mgN > state.Get(pool)) throw new ArgumentException("Removal exceeds available nitrogen.");
            EcologyGuard.Nonnegative(audit.CumulativeOutputMgN + mgN, "Cumulative output");
            InvalidateChallenge("manual_removal"); Output(pool, mgN, NitrogenOperation.Output, sourceId); RecheckWindows();
        }
        public void ChangeWater(double liters, WaterComposition incoming)
        {
            EcologyGuard.Nonnegative(liters, "liters");
            if (liters <= 0 || liters > VolumeLiters || incoming == null) throw new ArgumentException("Water exchange must be positive and no greater than the filled volume.");
            incoming.Validate();
            double fraction = liters / VolumeLiters;
            NitrogenPool[] pools = { NitrogenPool.Organic, NitrogenPool.Tan, NitrogenPool.Nitrite, NitrogenPool.Nitrate };
            double[] concentrations = { incoming.OrganicMgNPerLiter, incoming.TanMgNPerLiter, incoming.NitriteMgNPerLiter, incoming.NitrateMgNPerLiter };
            double input = 0, output = 0;
            for (int i = 0; i < pools.Length; i++) {
                double mass = concentrations[i] * liters; input += mass; output += state.Get(pools[i]) * fraction;
                EcologyGuard.Nonnegative(mass + state.Get(pools[i]) * (1 - fraction), "Exchange result");
            }
            EcologyGuard.Nonnegative(audit.CumulativeInputMgN + input, "Cumulative input");
            EcologyGuard.Nonnegative(audit.CumulativeOutputMgN + output, "Cumulative output");
            InvalidateChallenge("water_exchange"); ResetStableWindows();
            for (int i = 0; i < pools.Length; i++) {
                Output(pools[i], state.Get(pools[i]) * fraction, NitrogenOperation.WaterOutput, "water-exchange");
                Input(pools[i], concentrations[i] * liters, NitrogenOperation.WaterInput, "replacement-water");
            }
        }
        public void BeginFishlessChallenge(int liveAnimalCount)
        {
            if (liveAnimalCount != 0 || state.LivingMgN > 1e-8 || state.FoodMgN > 1e-8 || state.OrganicMgN > 1e-8 ||
                state.TanMgN > 1e-8 || state.NitriteMgN > 1e-8 || !FilterEnabled ||
                (challenge.Status != FishlessChallengeStatus.NotStarted && challenge.Status != FishlessChallengeStatus.Invalidated))
                throw new InvalidOperationException("A fishless challenge requires no animals, food, organic residues, TAN or nitrite, an active filter, and no active/passed challenge.");
            double dose = parameters.ChallengeDoseMgNPerLiter * VolumeLiters;
            ValidateAddition(NitrogenPool.Tan, dose);
            challenge = new FishlessChallengeState { Status = FishlessChallengeStatus.Processing, DoseMgN = dose,
                StartedGameDay = GameDays, DoseSequence = nextSequence };
            Input(NitrogenPool.Tan, dose, NitrogenOperation.ChallengeDose, "fishless-challenge");
        }
        public void BeginTrialObservation(int liveAnimalCount)
        {
            if (liveAnimalCount <= 0 || challenge.Status != FishlessChallengeStatus.Passed || trial.Status != TrialObservationStatus.NotStarted)
                throw new InvalidOperationException("Stocked observation requires a completed fishless challenge and introduced animals.");
            trial = new TrialObservationState { Status = TrialObservationStatus.Observing, StartedGameDay = GameDays, LiveAnimalCount = liveAnimalCount };
        }
        public void AdvanceGameDays(double gameDays)
        {
            EcologyGuard.Nonnegative(gameDays, "gameDays");
            if (gameDays > 365 || GameDays + gameDays > 1000000) throw new ArgumentOutOfRangeException("gameDays");
            accumulator += gameDays;
            double step = parameters.FixedStepGameDays;
            while (accumulator + 1e-12 >= step) {
                Tick(step); accumulator -= step; if (accumulator < 0) accumulator = 0;
            }
        }
        private void Tick(double dt)
        {
            bool stableAtStart = WaterIsStable;
            bool processingProvenAtStart = challenge.OxidizedTanMgN + 1e-9 >= challenge.DoseMgN * parameters.RequiredProcessedFraction &&
                challenge.OxidizedNitriteMgN + 1e-9 >= challenge.DoseMgN * parameters.RequiredProcessedFraction;
            GameDays += dt;
            Transfer(NitrogenPool.Organic, NitrogenPool.Tan, state.OrganicMgN * (1 - Math.Exp(-parameters.OrganicMineralizationPerDay * dt)), "mineralization");
            Transfer(NitrogenPool.Living, NitrogenPool.Tan, state.LivingMgN * (1 - Math.Exp(-parameters.LivingMetabolismPerDay * dt)), "ingested-reserve-metabolism");
            double tan = 0, nitrite = 0;
            if (FilterEnabled) {
                tan = Math.Min(state.TanMgN, TanCapacityMgNPerDay * dt);
                Transfer(NitrogenPool.Tan, NitrogenPool.Nitrite, tan, "tan-oxidation");
                nitrite = Math.Min(state.NitriteMgN, NitriteCapacityMgNPerDay * dt);
                Transfer(NitrogenPool.Nitrite, NitrogenPool.Nitrate, nitrite, "nitrite-oxidation");
            }
            TanCapacityMgNPerDay = Math.Min(parameters.MaximumCapacityMgNPerDay,
                TanCapacityMgNPerDay * Math.Exp(-parameters.CapacityDecayPerDay * dt) + tan * parameters.CapacityGainPerProcessedMgN);
            NitriteCapacityMgNPerDay = Math.Min(parameters.MaximumCapacityMgNPerDay,
                NitriteCapacityMgNPerDay * Math.Exp(-parameters.CapacityDecayPerDay * dt) + nitrite * parameters.CapacityGainPerProcessedMgN);
            Transfer(NitrogenPool.Nitrate, NitrogenPool.Plant, Math.Min(state.NitrateMgN, parameters.PlantUptakeMgNPerDay * dt), "plant-uptake");
            bool active = challenge.Status == FishlessChallengeStatus.Processing || challenge.Status == FishlessChallengeStatus.Stabilizing;
            if (active) {
                challenge.OxidizedTanMgN += tan; challenge.OxidizedNitriteMgN += nitrite;
                bool processed = challenge.OxidizedTanMgN + 1e-9 >= challenge.DoseMgN * parameters.RequiredProcessedFraction &&
                    challenge.OxidizedNitriteMgN + 1e-9 >= challenge.DoseMgN * parameters.RequiredProcessedFraction;
                if (processed && processingProvenAtStart && stableAtStart && WaterIsStable) {
                    challenge.Status = FishlessChallengeStatus.Stabilizing;
                    challenge.StableGameHours += dt * 24;
                    if (challenge.StableGameHours + 1e-9 >= parameters.StableGameHours) challenge.Status = FishlessChallengeStatus.Passed;
                } else { challenge.StableGameHours = 0; challenge.Status = FishlessChallengeStatus.Processing; }
            }
            if (trial.Status == TrialObservationStatus.Observing) {
                trial.StableGameHours = stableAtStart && WaterIsStable ? trial.StableGameHours + dt * 24 : 0;
                if (trial.StableGameHours + 1e-9 >= parameters.StableGameHours) trial.Status = TrialObservationStatus.Passed;
            }
        }
        private void ValidateAmount(NitrogenPool pool, double mgN, string source)
        {
            state.Get(pool); EcologyGuard.Nonnegative(mgN, "mgN"); EcologyGuard.Source(source);
            if (mgN <= 0) throw new ArgumentOutOfRangeException("mgN");
        }
        private void ValidateAddition(NitrogenPool pool, double amount)
        {
            EcologyGuard.Nonnegative(state.Get(pool) + amount, "Resulting mass");
            EcologyGuard.Nonnegative(audit.CumulativeInputMgN + amount, "Cumulative input");
        }
        private void Input(NitrogenPool pool, double amount, NitrogenOperation op, string source)
        {
            if (amount <= 0) return;
            state.Set(pool, state.Get(pool) + amount); audit.CumulativeInputMgN += amount; audit.InputByPoolMgN[(int)pool] += amount;
            Record(op, pool, pool, amount, source);
        }
        private void Output(NitrogenPool pool, double amount, NitrogenOperation op, string source)
        {
            if (amount <= 0) return;
            state.Set(pool, state.Get(pool) - amount); audit.CumulativeOutputMgN += amount; audit.OutputByPoolMgN[(int)pool] += amount;
            Record(op, pool, pool, amount, source);
        }
        private void Transfer(NitrogenPool from, NitrogenPool to, double amount, string source)
        {
            if (amount <= 0) return;
            state.Set(from, state.Get(from) - amount); state.Set(to, state.Get(to) + amount);
            audit.TransferMgN[(int)from * 7 + (int)to] += amount;
            Record(NitrogenOperation.Transfer, from, to, amount, source);
        }
        private void Record(NitrogenOperation operation, NitrogenPool from, NitrogenPool to, double mass, string source)
        {
            if (ledger.Count >= parameters.RecentLedgerLimit) ledger.RemoveAt(0);
            ledger.Add(new NitrogenLedgerEntry { Sequence = nextSequence++, GameDay = GameDays, Operation = operation, From = from, To = to, MgN = mass, SourceId = source });
        }
        private void InvalidateChallenge(string reason)
        {
            if (challenge.Status == FishlessChallengeStatus.Processing || challenge.Status == FishlessChallengeStatus.Stabilizing) {
                challenge.Status = FishlessChallengeStatus.Invalidated; challenge.StableGameHours = 0; challenge.InvalidatedReason = reason;
            }
        }
        private void ResetStableWindows()
        {
            if (challenge.Status == FishlessChallengeStatus.Stabilizing) challenge.Status = FishlessChallengeStatus.Processing;
            if (challenge.Status == FishlessChallengeStatus.Processing) challenge.StableGameHours = 0;
            if (trial.Status == TrialObservationStatus.Observing) trial.StableGameHours = 0;
        }
        private void RecheckWindows() { if (!WaterIsStable) ResetStableWindows(); }

        public EcologySnapshot Capture()
        {
            var entries = new NitrogenLedgerEntry[ledger.Count];
            for (int i = 0; i < entries.Length; i++) entries[i] = ledger[i].Clone();
            return new EcologySnapshot { Parameters = parameters.Clone(), State = state.Clone(), Audit = audit.Clone(), GameDays = GameDays,
                FixedAccumulatorGameDays = accumulator, TanCapacityMgNPerDay = TanCapacityMgNPerDay, NitriteCapacityMgNPerDay = NitriteCapacityMgNPerDay,
                FilterEnabled = FilterEnabled, NextSequence = nextSequence, Challenge = challenge.Clone(), TrialObservation = trial.Clone(), RecentLedger = entries };
        }
        public static EcologyEngine Restore(EcologySnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            var engine = new EcologyEngine(snapshot.Parameters) { state = snapshot.State.Clone(), audit = snapshot.Audit.Clone(),
                GameDays = snapshot.GameDays, accumulator = snapshot.FixedAccumulatorGameDays, TanCapacityMgNPerDay = snapshot.TanCapacityMgNPerDay,
                NitriteCapacityMgNPerDay = snapshot.NitriteCapacityMgNPerDay, FilterEnabled = snapshot.FilterEnabled,
                nextSequence = snapshot.NextSequence, challenge = snapshot.Challenge.Clone(), trial = snapshot.TrialObservation.Clone() };
            foreach (var entry in snapshot.RecentLedger) engine.ledger.Add(entry.Clone());
            return engine;
        }
        private static void ValidateSnapshot(EcologySnapshot s)
        {
            if (s == null || s.SchemaVersion != 1 || s.Parameters == null || s.State == null || s.Audit == null ||
                s.Challenge == null || s.TrialObservation == null || s.RecentLedger == null)
                throw new ArgumentException("Incomplete ecology snapshot.");
            s.Parameters.Validate();
            EcologyGuard.Nonnegative(s.GameDays, "GameDays"); EcologyGuard.Nonnegative(s.FixedAccumulatorGameDays, "accumulator");
            EcologyGuard.Nonnegative(s.TanCapacityMgNPerDay, "TAN capacity"); EcologyGuard.Nonnegative(s.NitriteCapacityMgNPerDay, "Nitrite capacity");
            if (s.GameDays > 1000000 || s.FixedAccumulatorGameDays >= s.Parameters.FixedStepGameDays || s.NextSequence < 1 ||
                s.TanCapacityMgNPerDay > s.Parameters.MaximumCapacityMgNPerDay || s.NitriteCapacityMgNPerDay > s.Parameters.MaximumCapacityMgNPerDay ||
                s.RecentLedger.Length != Math.Min(s.NextSequence - 1, s.Parameters.RecentLedgerLimit)) throw new ArgumentException("Invalid ecology clock/capacity/ledger.");
            NitrogenAudit a = s.Audit;
            if (a.InputByPoolMgN == null || a.InputByPoolMgN.Length != 7 || a.OutputByPoolMgN == null || a.OutputByPoolMgN.Length != 7 || a.TransferMgN == null || a.TransferMgN.Length != 49)
                throw new ArgumentException("Incomplete audit totals.");
            double totalIn = 0, totalOut = 0;
            foreach (double v in a.TransferMgN) EcologyGuard.Nonnegative(v, "Audit transfer");
            for (int p = 0; p < 7; p++) {
                if (a.TransferMgN[p * 7 + p] != 0) throw new ArgumentException("Self-transfers are not nitrogen transformations.");
                EcologyGuard.Nonnegative(s.State.Get((NitrogenPool)p), "Pool mass");
                EcologyGuard.Nonnegative(a.InputByPoolMgN[p], "Audit input"); EcologyGuard.Nonnegative(a.OutputByPoolMgN[p], "Audit output");
                totalIn += a.InputByPoolMgN[p]; totalOut += a.OutputByPoolMgN[p];
                double expected = a.InputByPoolMgN[p] - a.OutputByPoolMgN[p];
                for (int q = 0; q < 7; q++) expected += a.TransferMgN[q * 7 + p] - a.TransferMgN[p * 7 + q];
                if (!EcologyGuard.Near(expected, s.State.Get((NitrogenPool)p))) throw new ArgumentException("Pool mass contradicts cumulative audit.");
            }
            EcologyGuard.Nonnegative(a.CumulativeInputMgN, "Total input"); EcologyGuard.Nonnegative(a.CumulativeOutputMgN, "Total output");
            if (!EcologyGuard.Near(totalIn, a.CumulativeInputMgN) || !EcologyGuard.Near(totalOut, a.CumulativeOutputMgN) ||
                !EcologyGuard.Near(s.State.TotalMgN, totalIn - totalOut)) throw new ArgumentException("Total nitrogen is not conserved.");
            long sequence = s.NextSequence - s.RecentLedger.Length; double lastDay = 0;
            var recentIn = new double[7]; var recentOut = new double[7]; var recentTransfer = new double[49];
            foreach (var e in s.RecentLedger) {
                if (e == null || e.Sequence != sequence++ || e.GameDay < lastDay || e.GameDay > s.GameDays ||
                    !Enum.IsDefined(typeof(NitrogenOperation), e.Operation) || !Enum.IsDefined(typeof(NitrogenPool), e.From) || !Enum.IsDefined(typeof(NitrogenPool), e.To))
                    throw new ArgumentException("Invalid recent nitrogen entry.");
                EcologyGuard.Nonnegative(e.GameDay, "Ledger day"); EcologyGuard.Nonnegative(e.MgN, "Ledger mass"); EcologyGuard.Source(e.SourceId); lastDay = e.GameDay;
                if (e.MgN <= 0) throw new ArgumentException("Audit entries must move nonzero nitrogen.");
                if (e.Operation == NitrogenOperation.Transfer) {
                    if (e.From == e.To) throw new ArgumentException("Self transfer in recent audit.");
                    recentTransfer[(int)e.From * 7 + (int)e.To] += e.MgN;
                } else {
                    if (e.From != e.To) throw new ArgumentException("External audit entry spans two internal pools.");
                    if (e.Operation == NitrogenOperation.Output || e.Operation == NitrogenOperation.WaterOutput) recentOut[(int)e.From] += e.MgN;
                    else recentIn[(int)e.To] += e.MgN;
                }
            }
            for (int p = 0; p < 7; p++)
                if (recentIn[p] > a.InputByPoolMgN[p] + 1e-7 || recentOut[p] > a.OutputByPoolMgN[p] + 1e-7)
                    throw new ArgumentException("Recent input/output exceeds cumulative audit.");
            for (int p = 0; p < 49; p++)
                if (recentTransfer[p] > a.TransferMgN[p] + 1e-7) throw new ArgumentException("Recent transformation exceeds cumulative audit.");
            var c = s.Challenge; var t = s.TrialObservation;
            EcologyGuard.Nonnegative(c.DoseMgN, "Challenge dose"); EcologyGuard.Nonnegative(c.OxidizedTanMgN, "Challenge TAN oxidation");
            EcologyGuard.Nonnegative(c.OxidizedNitriteMgN, "Challenge nitrite oxidation"); EcologyGuard.Nonnegative(c.StableGameHours, "Challenge stable time");
            EcologyGuard.Nonnegative(c.StartedGameDay, "Challenge start"); EcologyGuard.Nonnegative(t.StartedGameDay, "Trial start"); EcologyGuard.Nonnegative(t.StableGameHours, "Trial time");
            if (!Enum.IsDefined(typeof(FishlessChallengeStatus), c.Status) || !Enum.IsDefined(typeof(TrialObservationStatus), t.Status) ||
                c.StartedGameDay > s.GameDays || t.StartedGameDay > s.GameDays || c.StableGameHours > (s.GameDays - c.StartedGameDay) * 24 + 1e-7 ||
                t.StableGameHours > (s.GameDays - t.StartedGameDay) * 24 + 1e-7 || t.LiveAnimalCount < 0)
                throw new ArgumentException("Invalid observation state or elapsed time.");
            if (c.Status != FishlessChallengeStatus.NotStarted &&
                (c.DoseMgN <= 0 || !EcologyGuard.Near(c.DoseMgN, s.Parameters.ChallengeDoseMgNPerLiter * s.Parameters.VolumeLiters) ||
                c.DoseSequence < 1 || c.DoseSequence >= s.NextSequence || c.DoseMgN > a.InputByPoolMgN[(int)NitrogenPool.Tan] + 1e-7 ||
                c.OxidizedTanMgN > c.DoseMgN + 1e-7 || c.OxidizedNitriteMgN > c.DoseMgN + 1e-7 ||
                c.OxidizedTanMgN > a.TransferMgN[(int)NitrogenPool.Tan * 7 + (int)NitrogenPool.Nitrite] + 1e-7 ||
                c.OxidizedNitriteMgN > a.TransferMgN[(int)NitrogenPool.Nitrite * 7 + (int)NitrogenPool.Nitrate] + 1e-7))
                throw new ArgumentException("Challenge lacks a nonzero nitrogen/processing audit.");
            if ((c.Status == FishlessChallengeStatus.NotStarted && (c.DoseMgN != 0 || c.DoseSequence != 0 || c.OxidizedTanMgN != 0 || c.OxidizedNitriteMgN != 0 || c.StableGameHours != 0)) ||
                (c.Status == FishlessChallengeStatus.Invalidated && (c.StableGameHours != 0 || string.IsNullOrEmpty(c.InvalidatedReason))) ||
                (t.Status == TrialObservationStatus.NotStarted && (t.StableGameHours != 0 || t.LiveAnimalCount != 0)))
                throw new ArgumentException("Inactive observation contains contradictory evidence.");
            bool processingProven = c.OxidizedTanMgN + 1e-7 >= c.DoseMgN * s.Parameters.RequiredProcessedFraction && c.OxidizedNitriteMgN + 1e-7 >= c.DoseMgN * s.Parameters.RequiredProcessedFraction;
            if ((c.Status == FishlessChallengeStatus.Passed && (!processingProven || c.StableGameHours + 1e-7 < s.Parameters.StableGameHours)) ||
                (c.Status == FishlessChallengeStatus.Stabilizing && !processingProven) ||
                (t.Status != TrialObservationStatus.NotStarted && (c.Status != FishlessChallengeStatus.Passed || t.LiveAnimalCount <= 0)) ||
                (t.Status == TrialObservationStatus.Passed && t.StableGameHours + 1e-7 < s.Parameters.StableGameHours))
                throw new ArgumentException("Completed progress lacks continuous observation evidence.");
        }
    }
}
