using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Aquarium.Core;
using Aquarium.Core.Ecology;

internal static class Program
{
    private static int checks;
    private static readonly List<string> gapItems = new List<string>();
    private static string Num(double value) { return value.ToString("G17", CultureInfo.InvariantCulture); }
    private static void GapRaw(string json) { gapItems.Add(json); }
    private static void Gap(string id, string note, EcologyEngine engine)
    {
        GapRaw("{\"id\":\"" + id + "\",\"note\":\"" + note + "\"" + PoolFields(engine) + "}");
    }
    private static string PoolFields(EcologyEngine engine)
    {
        NitrogenState state = engine.State;
        NitrogenAudit audit = engine.Audit;
        double residual = state.TotalMgN - audit.CumulativeInputMgN + audit.CumulativeOutputMgN;
        var text = new StringBuilder();
        text.Append(",\"pools\":{");
        for (int i = 0; i < 7; i++)
        {
            if (i > 0) text.Append(',');
            text.Append('"').Append((NitrogenPool)i).Append("\":").Append(Num(state.Get((NitrogenPool)i)));
        }
        text.Append("},\"input_by_pool\":{");
        for (int i = 0; i < 7; i++)
        {
            if (i > 0) text.Append(',');
            text.Append('"').Append((NitrogenPool)i).Append("\":").Append(Num(audit.InputByPoolMgN[i]));
        }
        text.Append("},\"output_by_pool\":{");
        for (int i = 0; i < 7; i++)
        {
            if (i > 0) text.Append(',');
            text.Append('"').Append((NitrogenPool)i).Append("\":").Append(Num(audit.OutputByPoolMgN[i]));
        }
        text.Append("},\"residual_mgN\":").Append(Num(residual));
        text.Append(",\"volume_l\":").Append(Num(engine.VolumeLiters));
        text.Append(",\"plant_uptake_mgN_per_day\":").Append(Num(engine.Parameters.PlantUptakeMgNPerDay));
        text.Append(",\"fixed_step_game_days\":").Append(Num(engine.Parameters.FixedStepGameDays));
        return text.ToString();
    }
    private static void WriteGapEvidence()
    {
        string directory = Environment.GetEnvironmentVariable("AQUARIUM_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var body = new StringBuilder();
        body.Append("{\"new_run\":true,\"unity\":\"not_run\",\"items\":[");
        for (int i = 0; i < gapItems.Count; i++)
        {
            if (i > 0) body.Append(',');
            body.Append(gapItems[i]);
        }
        body.Append("]}");
        File.WriteAllText(Path.Combine(directory, "ecology.json"), body.ToString());
    }
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
    private static void Near(double actual, double expected, string name, double tolerance = 1e-7) { Check(Math.Abs(actual - expected) <= tolerance, name + ": " + actual + " != " + expected); }
    private static void Reject(Action action, string name)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Check(rejected, name); }
    private static void Balance(EcologyEngine e, string name)
    { Near(e.State.TotalMgN, e.Audit.CumulativeInputMgN - e.Audit.CumulativeOutputMgN, name, 1e-6); EcologyEngine.Restore(e.Capture()); }
    private static EcologyEngine Mature()
    { var e = new EcologyEngine(); e.BeginFishlessChallenge(0); e.AdvanceGameDays(7); Check(e.Challenge.Status == FishlessChallengeStatus.Passed, "nonzero challenge passes with processing and window"); return e; }
    public static int Main()
    {
        var empty = new EcologyEngine(); empty.AdvanceGameDays(7);
        Check(empty.Challenge.Status == FishlessChallengeStatus.NotStarted, "empty zeros never pass"); Balance(empty, "empty seven days");
        var e = Mature(); Near(e.Challenge.DoseMgN, 27, "default challenge dose");
        Check(e.Challenge.OxidizedTanMgN >= 24.3 && e.Challenge.OxidizedNitriteMgN >= 24.3, "both oxidation stages proven");
        Check(e.Challenge.StableGameHours >= 24 - 1e-7, "continuous challenge window"); Balance(e, "challenge conserved");
        Reject(() => new EcologyEngine().BeginFishlessChallenge(1), "fish not challenge probes");
        foreach (string operation in new[] { "input", "transfer", "remove", "water" }) {
            var challenge = new EcologyEngine(); challenge.BeginFishlessChallenge(0);
            if (operation == "input") challenge.AddNitrogen(NitrogenPool.Organic, 1, "extra");
            if (operation == "transfer") challenge.TransferNitrogen(NitrogenPool.Tan, NitrogenPool.Nitrate, 1, "bypass");
            if (operation == "remove") challenge.RemoveNitrogen(NitrogenPool.Tan, 1, "siphon");
            if (operation == "water") challenge.ChangeWater(27, new WaterComposition());
            Check(challenge.Challenge.Status == FishlessChallengeStatus.Invalidated, operation + " invalidates active challenge");
            challenge.AdvanceGameDays(7); Check(challenge.Challenge.Status != FishlessChallengeStatus.Passed, "invalidated challenge never silently passes"); Balance(challenge, operation + " balance");
        }
        var retry = new EcologyEngine(); retry.BeginFishlessChallenge(0); retry.ChangeWater(27, new WaterComposition());
        retry.ChangeWater(54, new WaterComposition()); retry.BeginFishlessChallenge(0); retry.AdvanceGameDays(7);
        Check(retry.Challenge.Status == FishlessChallengeStatus.Passed, "fully drained invalidated challenge can restart"); Balance(retry, "drain retry balance");
        retry = new EcologyEngine(); retry.BeginFishlessChallenge(0); retry.AddNitrogen(NitrogenPool.Tan, 1, "invalidating-input");
        retry.AdvanceGameDays(7); retry.BeginFishlessChallenge(0); retry.AdvanceGameDays(7);
        Check(retry.Challenge.Status == FishlessChallengeStatus.Passed, "waiting for processed residues permits challenge retry"); Balance(retry, "wait retry balance");
        var water = new EcologyEngine(); water.AddNitrogen(NitrogenPool.Tan, 54, "fixture"); water.AddNitrogen(NitrogenPool.Food, 10, "solid-food");
        water.ChangeWater(27, new WaterComposition { TanMgNPerLiter = .2, NitrateMgNPerLiter = 1 });
        Near(water.State.TanMgN, 32.4, "drain old mass then replacement input"); Near(water.State.NitrateMgN, 27, "new water contains nitrate");
        Near(water.State.FoodMgN, 10, "water change does not remove solid portions"); Near(water.Audit.CumulativeOutputMgN, 27, "drained mass audit"); Balance(water, "water balance");
        var feed = new EcologyEngine(); feed.AddNitrogen(NitrogenPool.Food, 30, "meal");
        feed.TransferNitrogen(NitrogenPool.Food, NitrogenPool.Living, 20, "consumed"); feed.TransferNitrogen(NitrogenPool.Food, NitrogenPool.Organic, 10, "expired");
        feed.SetFilterEnabled(false); feed.AdvanceGameDays(1);
        Near(feed.State.LivingMgN, 20 * Math.Exp(-.35), "stored ingestion metabolism uses ecological days", 1e-6);
        Near(feed.State.OrganicMgN, 10 * Math.Exp(-.65), "organic decay uses ecological days", 1e-6);
        Check(feed.State.TanMgN > 0 && feed.State.NitrateMgN == 0, "filter stop retains TAN"); Balance(feed, "feeding conserved");
        feed.SetFilterEnabled(true); feed.AdvanceGameDays(6); Check(feed.State.NitrateMgN > 0, "restored filter processes backlog"); Balance(feed, "filter recovery seven days");
        foreach (double load in new[] { .5, 500.0 }) {
            var loadWorld = new EcologyEngine(); loadWorld.AddNitrogen(NitrogenPool.Organic, load, "load-fixture"); loadWorld.AdvanceGameDays(7); Balance(loadWorld, "seven-day load " + load);
        }
        var plant = new EcologyEngine(new EcologyParameters { PlantUptakeMgNPerDay = 2 }); plant.AddNitrogen(NitrogenPool.Nitrate, 10, "plant-fixture"); plant.AdvanceGameDays(2);
        Near(plant.State.PlantMgN, 4, "plants store nitrogen rather than delete"); Balance(plant, "plant balance");
        Gap("plant-nitrate-to-plant", "Existing fixture: uptake 2 mgN/day, 10 mgN nitrate, 2 game days. Default uptake remains 0.", plant);
        var stopped = new EcologyEngine(); stopped.BeginFishlessChallenge(0); stopped.AdvanceGameDays(5);
        double beforeHours = stopped.Challenge.StableGameHours, beforeTan = stopped.Challenge.OxidizedTanMgN, beforeNit = stopped.Challenge.OxidizedNitriteMgN;
        Check(beforeHours > 0 && stopped.Challenge.Status != FishlessChallengeStatus.Passed, "filter-off snapshot starts during a nonzero unfinished window");
        stopped.SetFilterEnabled(false);
        Near(stopped.Challenge.StableGameHours, 0, "filter off resets continuous window");
        Near(stopped.Challenge.OxidizedTanMgN, beforeTan, "switching the filter off does not oxidize TAN");
        Near(stopped.Challenge.OxidizedNitriteMgN, beforeNit, "switching the filter off does not oxidize nitrite");
        stopped.AdvanceGameDays(2);
        Near(stopped.Challenge.OxidizedTanMgN, beforeTan, "filter off adds no TAN oxidation");
        Near(stopped.Challenge.OxidizedNitriteMgN, beforeNit, "filter off adds no nitrite oxidation");
        Check(stopped.Challenge.Status != FishlessChallengeStatus.Passed, "filter off cannot pass");
        double offTan = stopped.Challenge.OxidizedTanMgN, offNit = stopped.Challenge.OxidizedNitriteMgN, offHours = stopped.Challenge.StableGameHours;
        stopped.SetFilterEnabled(true); stopped.AdvanceGameDays(2);
        Check(stopped.Challenge.Status == FishlessChallengeStatus.Passed, "restart obtains a new continuous window");
        Check(stopped.Challenge.StableGameHours + 1e-9 >= 24, "restored filter reaches a full 24-hour window");
        GapRaw("{\"id\":\"C-filter-window\",\"before_off\":{\"stable_game_hours\":" + Num(beforeHours) + ",\"oxidized_tan_mgN\":" + Num(beforeTan) + ",\"oxidized_nitrite_mgN\":" + Num(beforeNit)
            + "},\"after_off\":{\"stable_game_hours\":" + Num(offHours) + ",\"oxidized_tan_mgN\":" + Num(offTan) + ",\"oxidized_nitrite_mgN\":" + Num(offNit)
            + ",\"delta_oxidized_tan_mgN\":" + Num(offTan - beforeTan) + ",\"delta_oxidized_nitrite_mgN\":" + Num(offNit - beforeNit)
            + "},\"after_restore\":{\"status\":\"" + stopped.Challenge.Status + "\",\"stable_game_hours\":" + Num(stopped.Challenge.StableGameHours) + ",\"filter_on\":true},\"declared_zero_tolerance\":0}");
        e.BeginTrialObservation(8); e.AdvanceGameDays(.5); Check(e.TrialObservation.Status == TrialObservationStatus.Observing, "half day cannot pass trial");
        e.AddNitrogen(NitrogenPool.Tan, 54, "trial-overfeed"); Near(e.TrialObservation.StableGameHours, 0, "unsafe chemistry resets trial");
        e.SetFilterEnabled(false); e.AdvanceGameDays(1); Check(e.TrialObservation.Status == TrialObservationStatus.Observing, "unsafe trial cannot pass");
        e.SetFilterEnabled(true); e.AdvanceGameDays(7); Check(e.TrialObservation.Status == TrialObservationStatus.Passed, "stable stocked observation eventually passes"); Balance(e, "trial balance");
        var waterTrial = Mature(); waterTrial.BeginTrialObservation(8); waterTrial.AdvanceGameDays(.5);
        Check(waterTrial.TrialObservation.StableGameHours > 11.9, "trial accrued a window before exchange");
        waterTrial.ChangeWater(27, new WaterComposition()); Near(waterTrial.TrialObservation.StableGameHours, 0, "water exchange resets stocked window");
        waterTrial.AdvanceGameDays(23.0 / 24); Check(waterTrial.TrialObservation.Status == TrialObservationStatus.Observing, "old trial time not reused after exchange");
        waterTrial.AdvanceGameDays(1.0 / 24); Check(waterTrial.TrialObservation.Status == TrialObservationStatus.Passed, "new complete window after exchange passes");
        var noFood = new EcologyEngine(); noFood.AdvanceGameDays(30); Near(noFood.State.TotalMgN, 0, "fasting without ingested reserve creates no nitrogen");
        var baseClock = new EcologyEngine(); baseClock.AddNitrogen(NitrogenPool.Organic, 40, "rate-fixture");
        var clockResults = new List<EcologySnapshot>();
        foreach (double rate in new[] { 1.0, 2.0, 4.0 }) {
            var clock = EcologyEngine.Restore(baseClock.Capture());
            // Same seven ecological days, driven by different counts of real-second frames.
            int realSeconds = (int)(7 * 480 / rate);
            for (int s = 0; s < realSeconds; s++) clock.AdvanceGameDays(rate / 480);
            clockResults.Add(clock.Capture()); Balance(clock, "rate " + rate);
        }
        foreach (var result in clockResults) {
            Near(result.State.TanMgN, clockResults[0].State.TanMgN, "same ecology time TAN");
            Near(result.State.NitriteMgN, clockResults[0].State.NitriteMgN, "same ecology time nitrite");
            Near(result.State.NitrateMgN, clockResults[0].State.NitrateMgN, "same ecology time nitrate");
            Near(result.TanCapacityMgNPerDay, clockResults[0].TanCapacityMgNPerDay, "same ecology time capacity");
        }
        var continuous = new EcologyEngine(); continuous.BeginFishlessChallenge(0); continuous.AdvanceGameDays(2.123456);
        var restored = EcologyEngine.Restore(continuous.Capture()); continuous.AdvanceGameDays(4); restored.AdvanceGameDays(4);
        Near(restored.State.NitrateMgN, continuous.State.NitrateMgN, "save resume chemistry"); Near(restored.Challenge.StableGameHours, continuous.Challenge.StableGameHours, "save resume window");
        Check(restored.Capture().RecentLedger.Length <= 256, "bounded ledger");
        foreach (Action<EcologySnapshot> corrupt in new Action<EcologySnapshot>[] {
            s => s.State.TanMgN = double.NaN, s => s.State.NitrateMgN += 1,
            s => s.Audit.TransferMgN[0] = -1, s => s.TanCapacityMgNPerDay = -1,
            s => s.RecentLedger[0].Sequence++, s => s.FixedAccumulatorGameDays = 1,
            s => s.Challenge.DoseMgN = 0, s => s.Challenge.StableGameHours = 10000,
            s => s.Challenge.OxidizedNitriteMgN = 10000 }) {
            var broken = restored.Capture(); corrupt(broken); Reject(() => EcologyEngine.Restore(broken), "reject corrupted ecology save");
        }
        var invalid = new EcologyEngine(); invalid.BeginFishlessChallenge(0); double before = invalid.State.TotalMgN;
        Reject(() => invalid.AddNitrogen(NitrogenPool.Tan, double.NaN, "bad"), "reject NaN input");
        Reject(() => invalid.TransferNitrogen(NitrogenPool.Tan, NitrogenPool.Nitrate, 1000, "bad"), "reject excessive transfer");
        Reject(() => invalid.ChangeWater(55, new WaterComposition()), "reject excessive exchange");
        Reject(() => invalid.ChangeWater(20, new WaterComposition { NitrateMgNPerLiter = double.PositiveInfinity }), "reject bad incoming water");
        Reject(() => invalid.AdvanceGameDays(-1), "reject reversed time"); Near(invalid.State.TotalMgN, before, "rejected operations do not mutate mass");
        Check(invalid.Challenge.Status == FishlessChallengeStatus.Processing, "rejected operations do not invalidate challenge");
        FoodEvents();
        LivingReserves();
        GateLedger();
        WriteCurves();
        WriteGapEvidence();
        Console.WriteLine("Ecology checks passed: " + checks + ". Pure C#/.NET only; Unity import, Play and builds remain unverified.");
        return 0;
    }
    private static void LivingReserves()
    {
        var e = new EcologyEngine(); var reserve = new LivingReserveLedger();
        foreach (var meal in new[] { new LivingReserveEntry { EntityId = "fish-a", MgN = 2 }, new LivingReserveEntry { EntityId = "fish-b", MgN = 3 }, new LivingReserveEntry { EntityId = "fish-a", MgN = 4 } }) {
            e.AddNitrogen(NitrogenPool.Food, meal.MgN, "meal"); e.TransferNitrogen(NitrogenPool.Food, NitrogenPool.Living, meal.MgN, "ingested");
            reserve.Record(meal.EntityId, meal.MgN); Near(reserve.TotalMgN, e.State.LivingMgN, "split meals keep ownership total");
        }
        e.AdvanceGameDays(2); reserve.Reconcile(e.State.LivingMgN);
        var snapshot = reserve.Capture(); Near(snapshot.Entries[0].MgN, 6 * Math.Exp(-.7), "individual reserve follows ecological metabolism", 1e-6);
        Near(snapshot.Entries[1].MgN, 3 * Math.Exp(-.7), "second reserve follows same decay", 1e-6);
        var restored = LivingReserveLedger.Restore(snapshot, e.State.LivingMgN);
        snapshot.Entries[0].MgN = 900; Near(restored.TotalMgN, e.State.LivingMgN, "reserve snapshots isolate ownership state");
        double sold = restored.Remove("fish-a"); e.RemoveNitrogen(NitrogenPool.Living, sold, "sold:fish-a");
        Near(restored.TotalMgN, e.State.LivingMgN, "sold ingested reserve leaves tank"); Near(e.Audit.CumulativeOutputMgN, sold, "sale nitrogen included in cumulative output");
        e.AdvanceGameDays(1); restored.Reconcile(e.State.LivingMgN); Near(restored.TotalMgN, e.State.LivingMgN, "remaining animal continues metabolism");
        sold = restored.Remove("fish-b"); e.RemoveNitrogen(NitrogenPool.Living, sold, "sold:fish-b");
        Near(restored.TotalMgN, 0, "all sold reserve ownership empty"); Near(e.State.LivingMgN, 0, "all sold reserves exported"); Balance(e, "sold animal nitrogen balance");
        Near(restored.Remove("never-fed"), 0, "unfed animal has no reserve output");
        Reject(() => restored.Reconcile(1), "unowned reserve growth rejected"); Reject(() => restored.Record("fish", double.NaN), "bad ingestion rejected");
        reserve.Reconcile(0); Near(reserve.TotalMgN, 0, "zero reserve clears ownership");
        var duplicate = new LivingReserveSnapshot { Entries = new[] { new LivingReserveEntry { EntityId = "fish", MgN = 1 }, new LivingReserveEntry { EntityId = "fish", MgN = 1 } } };
        Reject(() => LivingReserveLedger.Restore(duplicate, 2), "duplicate reserve owner rejected");
        Reject(() => LivingReserveLedger.Restore(new LivingReserveSnapshot(), 1), "reserve ecology mismatch rejected");
    }
    private static double Moved(EcologyEngine e, NitrogenPool from, NitrogenPool to)
    { return e.Audit.TransferMgN[(int)from * 7 + (int)to]; }
    private static double Left(EcologyEngine e, NitrogenPool pool) { return e.Audit.OutputByPoolMgN[(int)pool]; }
    private static void GateLedger()
    {
        var report = new StringBuilder();
        report.Append("{\"prototype\":true,\"husbandry_guidance\":false,\"parameters\":{\"volume_l\":54,\"dose_mgN_per_l\":0.5,\"dose_mgN\":27,\"stable_mgN_per_l\":0.05,\"required_oxidized_fraction\":0.9,\"required_oxidized_mgN\":24.3,\"stable_game_hours\":24},\"pools\":[\"Food\",\"Organic\",\"Tan\",\"Nitrite\",\"Nitrate\",\"Living\",\"Plant\"],\"cases\":[");
        bool first = true;
        Action<string, string, EcologyEngine, string> emit = (id, rule, engine, note) =>
        {
            if (!first) report.Append(',');
            first = false;
            var c = engine.Challenge;
            var item = new StringBuilder();
            item.Append("{\"id\":\"").Append(id).Append("\",\"rule\":\"").Append(rule).Append("\",\"note\":\"").Append(note)
                .Append("\",\"game_day\":").Append(Num(engine.GameDays))
                .Append(",\"filter_on\":").Append(engine.FilterEnabled ? "true" : "false")
                .Append(",\"water_stable\":").Append(engine.WaterIsStable ? "true" : "false")
                .Append(",\"challenge\":\"").Append(c.Status).Append("\",\"stable_game_hours\":").Append(Num(c.StableGameHours))
                .Append(",\"dose_mgN\":").Append(Num(c.DoseMgN))
                .Append(",\"oxidized_tan_mgN\":").Append(Num(c.OxidizedTanMgN))
                .Append(",\"oxidized_nitrite_mgN\":").Append(Num(c.OxidizedNitriteMgN))
                .Append(",\"tan_to_nitrite_mgN\":").Append(Num(Moved(engine, NitrogenPool.Tan, NitrogenPool.Nitrite)))
                .Append(",\"nitrite_to_nitrate_mgN\":").Append(Num(Moved(engine, NitrogenPool.Nitrite, NitrogenPool.Nitrate)))
                .Append(",\"tan_output_mgN\":").Append(Num(Left(engine, NitrogenPool.Tan)))
                .Append(",\"tan_mgN\":").Append(Num(engine.State.TanMgN))
                .Append(",\"total_mgN\":").Append(Num(engine.State.TotalMgN))
                .Append(",\"input_mgN\":").Append(Num(engine.Audit.CumulativeInputMgN))
                .Append(",\"output_mgN\":").Append(Num(engine.Audit.CumulativeOutputMgN))
                .Append(PoolFields(engine))
                .Append("}");
            report.Append(item);
            Balance(engine, id);
            GapRaw(item.ToString());
        };
        var frozen = new EcologyParameters();
        Near(frozen.VolumeLiters, 54, "frozen volume");
        Near(frozen.ChallengeDoseMgNPerLiter, .5, "frozen dose per litre");
        Near(frozen.MaximumStableTanMgNPerLiter, .05, "frozen TAN limit");
        Near(frozen.MaximumStableNitriteMgNPerLiter, .05, "frozen nitrite limit");
        Near(frozen.RequiredProcessedFraction, .9, "frozen oxidation fraction");
        Near(frozen.StableGameHours, 24, "frozen stable hours");
        Check((int)NitrogenPool.Plant == 6, "seven existing nitrogen pools and no extra pool");
        var empty = new EcologyEngine(); empty.AdvanceGameDays(10);
        Check(empty.WaterIsStable && empty.State.TanMgN == 0 && empty.Challenge.Status == FishlessChallengeStatus.NotStarted && empty.Challenge.OxidizedTanMgN == 0, "zero ammonia without a challenge is not a pass");
        emit("A-zero-ammonia", "A", empty, "Ten game days at zero TAN stay NotStarted.");
        var proven = new EcologyEngine(); proven.BeginFishlessChallenge(0); proven.AdvanceGameDays(7);
        Near(proven.Challenge.DoseMgN, 27, "54 L times 0.5 mgN/L");
        Check(proven.Challenge.OxidizedTanMgN + 1e-9 >= 24.3 && proven.Challenge.OxidizedNitriteMgN + 1e-9 >= 24.3, "both stages reach the 24.3 mgN oxidation floor");
        Check(proven.Challenge.OxidizedTanMgN <= proven.Challenge.DoseMgN + 1e-6 && Moved(proven, NitrogenPool.Tan, NitrogenPool.Nitrite) + 1e-6 >= proven.Challenge.OxidizedTanMgN, "TAN oxidation is biological transfer, not removal");
        Check(Moved(proven, NitrogenPool.Nitrite, NitrogenPool.Nitrate) + 1e-6 >= proven.Challenge.OxidizedNitriteMgN && Left(proven, NitrogenPool.Tan) == 0, "passed challenge has no water-change TAN removal");
        Check(proven.Challenge.Status == FishlessChallengeStatus.Passed && proven.Challenge.StableGameHours + 1e-9 >= 24, "24 game hours after real oxidation");
        emit("B-oxidized-27", "B", proven, "7 game days; 27 mgN dose; oxidation floor 24.3 mgN.");
        var diluted = new EcologyEngine(); diluted.BeginFishlessChallenge(0); diluted.AdvanceGameDays(1);
        double oxTan = diluted.Challenge.OxidizedTanMgN, oxNit = diluted.Challenge.OxidizedNitriteMgN;
        double tanToNit = Moved(diluted, NitrogenPool.Tan, NitrogenPool.Nitrite), nitToNitrate = Moved(diluted, NitrogenPool.Nitrite, NitrogenPool.Nitrate);
        double tanBefore = diluted.State.TanMgN;
        diluted.ChangeWater(27, new WaterComposition());
        Near(diluted.Challenge.OxidizedTanMgN, oxTan, "water change does not increase TAN oxidation");
        Near(diluted.Challenge.OxidizedNitriteMgN, oxNit, "water change does not increase nitrite oxidation");
        Near(Moved(diluted, NitrogenPool.Tan, NitrogenPool.Nitrite), tanToNit, "water change does not add a TAN transfer");
        Near(Moved(diluted, NitrogenPool.Nitrite, NitrogenPool.Nitrate), nitToNitrate, "water change does not add a nitrite transfer");
        Near(Left(diluted, NitrogenPool.Tan), tanBefore * .5, "half the TAN leaves as water output", 1e-6);
        Check(diluted.Challenge.Status == FishlessChallengeStatus.Invalidated, "dilution invalidates the active challenge");
        diluted.AdvanceGameDays(7);
        Check(diluted.Challenge.Status != FishlessChallengeStatus.Passed, "diluted challenge does not become a pass");
        emit("B-water-change", "B", diluted, "50 percent exchange after 1 game day, then 7 more days.");
        var stopped = new EcologyEngine(); stopped.BeginFishlessChallenge(0); stopped.AdvanceGameDays(1); stopped.SetFilterEnabled(false);
        double stoppedOx = stopped.Challenge.OxidizedTanMgN;
        stopped.AdvanceGameDays(3);
        Near(stopped.Challenge.OxidizedTanMgN, stoppedOx, "filter off adds no oxidation");
        Near(stopped.Challenge.StableGameHours, 0, "filter off keeps the effective window at zero");
        Check(stopped.Challenge.Status != FishlessChallengeStatus.Passed && !stopped.WaterIsStable, "filter off is not stable and not passed");
        emit("C-filter-off", "C", stopped, "Filter disabled after 1 game day, then 3 game days.");
        var kept = new EcologyEngine(); kept.BeginFishlessChallenge(0); kept.AdvanceGameDays(7);
        double keptHours = kept.Challenge.StableGameHours;
        kept.BeginTrialObservation(8); kept.AdvanceGameDays(.5);
        Check(kept.TrialObservation.StableGameHours > 1, "trial window started");
        kept.AddNitrogen(NitrogenPool.Tan, 54, "new-bioload");
        Check(kept.Challenge.Status == FishlessChallengeStatus.Passed && Math.Abs(kept.Challenge.StableGameHours - keptHours) < 1e-6, "passed challenge and its completed window stay");
        Check(kept.TrialObservation.Status == TrialObservationStatus.Observing && kept.TrialObservation.StableGameHours == 0, "current trial window restarts without erasing the trial");
        Check(!kept.WaterIsStable, "new TAN load is currently unstable");
        Reject(() => kept.BeginFishlessChallenge(0), "completed challenge is not restarted");
        emit("D-new-load", "D", kept, "Passed challenge, half-day trial, then 54 mgN TAN.");
        foreach (double litres in new[] { 27.0, 54.0, 108.0 })
        {
            var scaled = new EcologyEngine(new EcologyParameters { VolumeLiters = litres });
            scaled.BeginFishlessChallenge(0);
            Near(scaled.Challenge.DoseMgN, .5 * litres, "dose follows volume " + litres);
            Check(scaled.State.TanMgN >= 0 && scaled.State.TotalMgN >= 0, "dose mass is nonnegative");
            GapRaw("{\"id\":\"parameter-dose-" + litres.ToString("G17", CultureInfo.InvariantCulture) + "\",\"kind\":\"parameter_volume_not_layout\",\"volume_l\":" + Num(litres) + ",\"dose_mgN\":" + Num(scaled.Challenge.DoseMgN) + ",\"dose_mgN_per_l\":0.5}");
        }
        var lumped = new EcologyEngine(); lumped.BeginFishlessChallenge(0); lumped.AdvanceGameDays(3);
        var stepped = new EcologyEngine(); stepped.BeginFishlessChallenge(0);
        double left = 3, step = stepped.Parameters.FixedStepGameDays;
        int externalCalls = 0;
        while (left > 1e-12) { double dt = Math.Min(step, left); stepped.AdvanceGameDays(dt); left -= dt; externalCalls++; }
        const double stepTolerance = 1e-6;
        Near(stepped.Challenge.OxidizedTanMgN, lumped.Challenge.OxidizedTanMgN, "fixed steps match one three-day advance", stepTolerance);
        Near(stepped.State.NitrateMgN, lumped.State.NitrateMgN, "fixed steps match nitrate", stepTolerance);
        emit("time-step", "consistency", stepped, "3 game days as fixed ecology steps versus one advance. The paired object records both sides.");
        GapRaw("{\"id\":\"external-chunk-pair\",\"internal_fixed_step_game_days\":" + Num(step)
            + ",\"declared_tolerance\":" + Num(stepTolerance)
            + ",\"lumped\":{\"external_calls\":[3],\"result\":{" + PoolFields(lumped).TrimStart(',') + "}}"
            + ",\"stepped\":{\"external_chunk_game_days\":" + Num(step) + ",\"external_call_count\":" + externalCalls.ToString(CultureInfo.InvariantCulture)
            + ",\"result\":{" + PoolFields(stepped).TrimStart(',') + "}}"
            + ",\"difference\":{\"oxidized_tan_mgN\":" + Num(stepped.Challenge.OxidizedTanMgN - lumped.Challenge.OxidizedTanMgN)
            + ",\"nitrate_mgN\":" + Num(stepped.State.NitrateMgN - lumped.State.NitrateMgN)
            + ",\"total_mgN\":" + Num(stepped.State.TotalMgN - lumped.State.TotalMgN) + "}}");
        report.Append("]}");
        string path = Environment.GetEnvironmentVariable("AQUARIUM_GATE_EVIDENCE");
        if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, report.ToString());
    }
    private static void WriteCurves()
    {
        var csv = new StringBuilder("scenario,game_day,food_mgN,organic_mgN,living_reserve_mgN,TAN_mgN_L,nitrite_mgN_L,nitrate_mgN_L,total_mgN,input_mgN,output_mgN,balance_error_mgN,tan_capacity_mgN_day,nitrite_capacity_mgN_day,filter_on,challenge\n");
        foreach (string scenario in new[] { "empty", "low_load", "high_load", "fishless_challenge", "filter_stop_restore", "overfeed_water_exchange" }) {
            var e = new EcologyEngine();
            if (scenario == "fishless_challenge") e.BeginFishlessChallenge(0);
            if (scenario == "low_load") e.AddNitrogen(NitrogenPool.Organic, .5, scenario);
            if (scenario == "high_load") e.AddNitrogen(NitrogenPool.Organic, 500, scenario);
            if (scenario == "filter_stop_restore" || scenario == "overfeed_water_exchange") e.AddNitrogen(NitrogenPool.Organic, 40, scenario);
            for (int sample = 0; sample <= 28; sample++) {
                if (scenario == "filter_stop_restore" && sample == 8) e.SetFilterEnabled(false);
                if (scenario == "filter_stop_restore" && sample == 16) e.SetFilterEnabled(true);
                if (scenario == "overfeed_water_exchange" && sample == 8) e.AddNitrogen(NitrogenPool.Organic, 200, "overfeed");
                if (scenario == "overfeed_water_exchange" && sample == 16) e.ChangeWater(27, new WaterComposition { NitrateMgNPerLiter = .1 });
                var s = e.State; var a = e.Audit;
                double[] values = { e.GameDays, s.FoodMgN, s.OrganicMgN, s.LivingMgN, e.TanMgNPerLiter, e.NitriteMgNPerLiter,
                    e.NitrateMgNPerLiter, s.TotalMgN, a.CumulativeInputMgN, a.CumulativeOutputMgN,
                    s.TotalMgN - a.CumulativeInputMgN + a.CumulativeOutputMgN, e.TanCapacityMgNPerDay, e.NitriteCapacityMgNPerDay };
                csv.Append(scenario);
                foreach (double v in values) csv.Append(',').Append(v.ToString("G17", CultureInfo.InvariantCulture));
                csv.Append(',').Append(e.FilterEnabled ? "true" : "false").Append(',').Append(e.Challenge.Status).Append('\n');
                Balance(e, "curve mass " + scenario + " " + sample);
                if (sample < 28) e.AdvanceGameDays(.25);
            }
        }
        string directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../results"));
        Directory.CreateDirectory(directory); File.WriteAllText(Path.Combine(directory, "regression-curves.csv"), csv.ToString());
    }
    private static void FoodEvents()
    {
        var tank = new TankBounds(); var w = new AquariumWorld(tank); long removedId = 0; int removedPortions = 0; string reason = null;
        w.FoodRemoved += (id, portions, why) => { removedId = id; removedPortions += portions; reason = why; };
        long source = w.Feed(new Vec3(0, .1, 0), 12); w.Advance(36);
        Check(removedId == source && removedPortions == 12 && reason == "expired", "expiry reports original source and remaining portions");
        var restored = AquariumWorld.Restore(w.Capture(), tank, new Dictionary<string, SpeciesProfile>()); Check(restored.Capture().SchemaVersion == 2, "food events preserve schema two");
        w = new AquariumWorld(tank); removedId = 0; removedPortions = 0;
        w.FoodRemoved += (id, portions, why) => { removedId = id; removedPortions += portions; reason = why; };
        source = w.Feed(new Vec3(0, .1, 0), 3); for (int i = 0; i < 64; i++) w.ApproachGlass(new Vec3());
        Check(removedId == source && removedPortions == 3 && reason == "capacity_evicted", "capacity eviction reports remaining food");
        var profile = DemoProfiles.CommunityFish(); w = new AquariumWorld(tank); w.AddAgent(profile, "eater", 77);
        var snapshot = w.Capture(); var a = snapshot.Agents[0]; a.Position = new Vec3(0, .1, 0); a.Target = a.Position;
        a.Action = ActionKind.SeekFood; a.MinActionRemaining = 100; a.NextDecision = 100; a.Hunger = 1;
        w = AquariumWorld.Restore(snapshot, tank, new Dictionary<string, SpeciesProfile> { { profile.Id, profile } });
        long eatenId = 0; int eaten = 0; string eater = null;
        w.FoodConsumed += (id, entity, portions) => { eatenId = id; eater = entity; eaten += portions; };
        source = w.Feed(a.Position, 1); w.Advance(AquariumWorld.FixedStep);
        Check(eatenId == source && eaten == 1 && eater == "eater", "consumption reports source entity and exact portions");
        Check(w.Stimuli.Count == 0, "consumed food removed once");
        var ec = new EcologyEngine(); ec.AddNitrogen(NitrogenPool.Food, 1, "food-1");
        var physical = new AquariumWorld(tank); physical.FoodRemoved += (id, portions, why) => ec.TransferNitrogen(NitrogenPool.Food, NitrogenPool.Organic, portions, id.ToString());
        physical.Feed(new Vec3(0, .1, 0), 1); physical.Advance(36);
        Near(ec.State.FoodMgN, 0, "event integration clears available food"); Near(ec.State.OrganicMgN, 1, "event integration preserves expired nitrogen"); Balance(ec, "food event balance");
    }
}
