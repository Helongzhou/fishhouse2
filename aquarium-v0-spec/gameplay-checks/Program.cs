using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Aquarium.Core;
using Aquarium.Core.Gameplay;
using Aquarium.Core.Ecology;

internal static class Program
{
    private static int checks, sequence;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, IgnoreReadOnlyProperties = true };
    private static string Tx() { return "check-" + (++sequence).ToString(CultureInfo.InvariantCulture); }
    private static void Check(bool value, string label)
    { if (!value) throw new Exception("FAIL: " + label); checks++; Console.WriteLine("PASS: " + label); }
    private static void Ok(CommandResult result) { if (!result.Success) throw new Exception(result.Code + " " + string.Join(",", result.Args)); }
    private static string Serialized(GameplaySession s) { return JsonSerializer.Serialize(s.Capture(), Json); }
    private static bool Rejects(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
    private static string[] CsvLine(string line)
    {
        List<string> cells = new List<string>(); StringBuilder cell = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
            else if (c == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); } else cell.Append(c);
        }
        cells.Add(cell.ToString()); return cells.ToArray();
    }
    private static List<Dictionary<string, string>> Rows(string name)
    {
        string[] lines = File.ReadAllLines(Path.Combine("data", name + ".csv")); string[] header = CsvLine(lines[0].TrimStart('\uFEFF'));
        List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
        foreach (string line in lines.Skip(1).Where(x => x.Length != 0))
        {
            string[] cells = CsvLine(line); Dictionary<string, string> row = new Dictionary<string, string>();
            for (int i = 0; i < header.Length; i++) row.Add(header[i], cells[i]); result.Add(row);
        }
        return result;
    }
    private static int Int(string value) { return int.Parse(value, CultureInfo.InvariantCulture); }
    private static GameplayCatalog Catalog()
    {
        List<Dictionary<string, string>> items = Rows("items"), plants = Rows("plants"), route = Rows("route"), economy = Rows("economy");
        int rewardStep = Int(route.Single(r => r["economy_id"] == "trial_reward")["step_index"]);
        return new GameplayCatalog(items.Select(i => new ItemDefinition { Id = i["item_id"], Kind = i["item_kind"],
            ContentStatus = i["content_status"], SpeciesId = i["species_id"], UnlockGateId = i["unlock_gate_id"],
            PurchasePriceCoin = Int(i["purchase_price_coin"]), SellPriceCoin = Int(i["post_confirm_sell_price_per_purchase_coin"]),
            UnitsPerPurchase = Int(i["units_per_purchase"]), RouteRole = i["route_role"] }).ToArray(),
            plants.Where(p => items.Any(i => i["species_id"] == p["id"] && i["item_kind"] == "plant")).Select(p => new PlantPlacementRule {
                ItemId = items.Single(i => i["species_id"] == p["id"] && i["item_kind"] == "plant")["item_id"],
                PlantingMethod = p["planting_method"], AllowedSurfaces = p["allowed_surfaces"].Split('|'), RhizomeExposed = p["rhizome_exposed"] == "true" }).ToArray(),
            route.Where(r => r["action"] == "purchase" && r["required_for_v0"] == "true").Select(r => new RouteRequirement {
                ItemId = r["item_id"], PurchaseCount = Int(r["purchase_count"]), BeforeTrialReward = Int(r["step_index"]) < rewardStep }).ToArray(),
            Int(economy.Single(e => e["economy_id"] == "starter_grant")["amount_coin"]),
            Int(economy.Single(e => e["economy_id"] == "trial_reward")["amount_coin"]));
    }
    private static PurchaseBatch Batch(GameplaySession session, string item)
    { return session.Capture().Batches.Last(b => b.ItemId == item && !b.Refunded && !b.Sold); }
    private static LayoutPlacement Placement(GameplaySession session, string id)
    { return session.Capture().Layout.Placements.Single(p => p.Id == id); }
    private static LayoutPlacement Box(GameplaySession session, string item, string id, Vec3 position, Vec3 size, string support = null)
    { return new LayoutPlacement { Id = id, PurchaseBatchId = Batch(session, item).Id, Position = position, Size = size, SupportPlacementId = support }; }
    private static void Base(GameplaySession session)
    {
        foreach (string id in new[] { "substrate_natural_sand", "stone_main", "stone_secondary", "filter_hangon_basic", "light_low",
            "plant_anubias_nana", "plant_java_fern", "plant_java_moss" }) Ok(session.Buy(Tx(), id));
        Ok(session.SetSubstrate(Tx(), new SubstrateProfile { PurchaseBatchId = Batch(session, "substrate_natural_sand").Id,
            FrontLeftHeight = .02, FrontRightHeight = .02, BackLeftHeight = .02, BackRightHeight = .02 }));
        Ok(session.Place(Tx(), Box(session, "stone_main", "main", new Vec3(-.11, .07, 0), new Vec3(.12, .10, .10))));
        Ok(session.Place(Tx(), Box(session, "stone_secondary", "secondary", new Vec3(.12, .05, .02), new Vec3(.08, .06, .08))));
        Ok(session.Place(Tx(), Box(session, "filter_hangon_basic", "filter", new Vec3(.24, .25, .10), new Vec3(.06, .10, .06))));
        Ok(session.Place(Tx(), Box(session, "light_low", "light", new Vec3(0, .34, 0), new Vec3(.28, .02, .10))));
        Ok(session.Place(Tx(), Box(session, "plant_anubias_nana", "anubias", new Vec3(-.13, .12, 0), new Vec3(.025, .04, .025), "main")));
        Ok(session.Place(Tx(), Box(session, "plant_java_fern", "fern", new Vec3(-.09, .12, 0), new Vec3(.025, .05, .025), "main")));
        Ok(session.Place(Tx(), Box(session, "plant_java_moss", "moss", new Vec3(.12, .08, .02), new Vec3(.025, .02, .025), "secondary")));
    }
    private static void Checks(GameplayCatalog catalog)
    {
        GameplaySession session = new GameplaySession(catalog, new TankBounds());
        Check(session.WalletCoin == 1500 && session.Capture().SaveId != new GameplaySession(catalog, new TankBounds()).Capture().SaveId,
            "new saves have independent identities and CSV starter grant");
        string before = Serialized(session);
        Check(session.Buy(Tx(), "wood_optional").Code == "gameplay.budget_reserved" && Serialized(session) == before,
            "optional driftwood cannot consume the 1410-coin pre-reward route reserve");
        Check(!session.Buy(Tx(), "filter_top").Success && !session.Buy(Tx(), "debug_fish_spawn").Success &&
            !session.Buy(Tx(), "missing").Success, "planned, fixture and unknown IDs cannot enter default inventory");
        Check(session.Buy(Tx(), "fish_trial_pack").Code == "gameplay.gate_locked", "first fish stay locked before real fishless challenge");
        string tx = Tx(); CommandResult sand = session.Buy(tx, "substrate_natural_sand"); Ok(sand);
        Check(session.Buy(tx, "substrate_natural_sand").BatchId == sand.BatchId && session.WalletCoin == 1380 && session.Capture().Batches.Length == 1,
            "repeated purchase ID returns original delivery without charging twice");
        Check(session.Buy(tx, "stone_main").Code == "gameplay.transaction_id_conflict", "transaction ID cannot be reused for a different command");
        string refundTx = Tx(); Ok(session.Refund(refundTx, sand.BatchId)); Ok(session.Refund(refundTx, sand.BatchId));
        Check(session.WalletCoin == 1500 && !session.Refund(Tx(), sand.BatchId).Success, "refund uses paid batch once and duplicate refund cannot mint coins");
        GameplaySnapshot refunded = session.Capture();
        Check(GameplaySession.Restore(refunded, catalog, new TankBounds()).WalletCoin == 1500, "refund ledger restores with balanced wallet");
        Base(session); Check(session.WalletCoin == 390, "CSV base route costs exactly 1110 coins");
        Check(Math.Abs(session.CalculateEffectiveWaterLiters() - 54.216) < 1e-8, "water volume subtracts substrate and immersed hardscape boxes rather than forcing 54L");
        before = Serialized(session); LayoutPlacement bad = Placement(session, "secondary"); bad.Position.X = -.11; bad.Position.Y = .05; bad.Position.Z = 0;
        Check(session.Place(Tx(), bad).Code == "gameplay.hardscape_overlap" && Serialized(session) == before,
            "colliding hardscape rejects atomically");
        bad = Placement(session, "main"); bad.Position.X = .27; bad.YawDegrees = 45;
        Check(session.Place(Tx(), bad).Code == "gameplay.outside_tank" && Serialized(session) == before,
            "rotated solid corners cannot protrude beyond glass");
        bad = Placement(session, "anubias"); bad.RhizomeBuried = true;
        Check(session.Place(Tx(), bad).Code == "gameplay.rhizome_buried" && Serialized(session) == before,
            "exposed-rhizome rule is distinct from moss attachment");
        bad = Placement(session, "fern"); bad.SupportPlacementId = "filter";
        Check(session.Place(Tx(), bad).Code == "gameplay.support_invalid", "epiphytes reject equipment surfaces");
        Check(session.Remove(Tx(), "main").Code == "gameplay.support_in_use", "removing a support cannot orphan attached plants");
        LayoutPlacement main = Placement(session, "main"); main.Position.X -= .02; main.YawDegrees = 15; Ok(session.Place(Tx(), main));
        Check(Placement(session, "anubias").Position.X < -.14 && Placement(session, "anubias").Position.Z > 0,
            "support transform carries plants with the same positive-yaw convention as Unity");
        Ok(session.Undo(Tx())); Check(Math.Abs(Placement(session, "main").Position.X + .11) < 1e-10, "undo restores whole layout transform and dependent plants");
        Ok(session.Redo(Tx())); Check(Math.Abs(Placement(session, "main").Position.X + .13) < 1e-10, "redo reapplies validated transform");
        Ok(session.Undo(Tx()));
        string invalidBefore = Serialized(session);
        Check(!session.SetSubstrate(Tx(), new SubstrateProfile { PurchaseBatchId = Batch(session, "substrate_natural_sand").Id,
            FrontLeftHeight = double.NaN }).Success && Serialized(session) == invalidBefore, "non-finite substrate rejects without modifying undo history");
        Ok(session.SetSubstrate(Tx(), new SubstrateProfile { PurchaseBatchId = Batch(session, "substrate_natural_sand").Id,
            FrontLeftHeight = .02, FrontRightHeight = .025, BackLeftHeight = .035, BackRightHeight = .04 }));
        Check(Placement(session, "main").Position.Y > .07 && Placement(session, "anubias").Position.Y > .12,
            "bounded substrate slope reanchors stones and their attachments");
        Ok(session.Undo(Tx())); Ok(session.ConfirmLayout(Tx()));
        Check(session.Refund(Tx(), Batch(session, "stone_main").Id).Code == "gameplay.refund_closed", "first confirmation permanently closes full-refund mode");
        Check(session.Place(Tx(), Placement(session, "main")).Code == "gameplay.layout_locked", "confirmed hardscape cannot silently change water displacement");
        string fillTx = Tx(); Ok(session.Fill(fillTx)); Ok(session.Fill(fillTx));
        Check(session.Fill(Tx()).Code == "gameplay.already_filled", "fill is idempotent and a new command cannot refill the aquarium");
        LayoutPlacement fern = Placement(session, "fern"); fern.Position = new Vec3(.13, .08, .02); fern.SupportPlacementId = "secondary"; fern.YawDegrees = 65;
        Ok(session.Place(Tx(), fern)); Ok(session.Undo(Tx())); Ok(session.Redo(Tx()));
        Check(Placement(session, "fern").SupportPlacementId == "secondary", "wet tank supports plant reattachment with undo and redo");
        session.SetPaused(true); before = Serialized(session);
        Check(session.Place(Tx(), fern).Code == "gameplay.paused" && Serialized(session) == before, "paused session rejects business commands without state changes");
        session.SetPaused(false);
        EcologyEngine ecology = new EcologyEngine(new EcologyParameters { VolumeLiters = session.CalculateEffectiveWaterLiters() });
        ecology.AdvanceGameDays(5); Ok(session.RefreshProgress(Tx(), ecology));
        Check(session.Stage == GameplayStage.Filled && ecology.WaterIsStable && ecology.Challenge.Status == FishlessChallengeStatus.NotStarted && ecology.State.TanMgN == 0 && !session.Buy(Tx(), "fish_trial_pack").Success,
            "stable zero TAN without a completed challenge does not open the fish gate");
        ecology.BeginFishlessChallenge(0); Ok(session.RefreshProgress(Tx(), ecology));
        Check(session.Stage == GameplayStage.Cycling && ecology.Challenge.DoseMgN > 0, "real nonzero nitrogen dose starts cycling stage");
        for (int day = 0; day < 60 && ecology.Challenge.Status != FishlessChallengeStatus.Passed; day++) ecology.AdvanceGameDays(1);
        Check(ecology.Challenge.Status == FishlessChallengeStatus.Passed, "default ecology actually processes challenge dose and observes continuous stable window");
        ecology.SetFilterEnabled(false); Ok(session.RefreshProgress(Tx(), ecology));
        Check(session.Stage == GameplayStage.Cycling, "historical passed evidence cannot unlock fish while filter is currently off");
        ecology.SetFilterEnabled(true); Ok(session.RefreshProgress(Tx(), ecology));
        string trialTx = Tx(); CommandResult fish = session.Buy(trialTx, "fish_trial_pack"); Ok(fish);
        Check(fish.EntityIds.Length == 8 && session.WalletCoin == 270 && session.LiveAnimalCount() == 8, "120 coins deliver one 8-fish Ember pack");
        ecology.BeginTrialObservation(8); CommandResult shrimp = session.Buy(Tx(), "shrimp_pack"); Ok(shrimp);
        Check(shrimp.EntityIds.Length == 6 && session.WalletCoin == 90 && session.LiveAnimalCount() == 14, "180 coins deliver one 6-shrimp pack");
        ecology.AddNitrogen(NitrogenPool.Food, 1, "gameplay-check-feeding"); ecology.AdvanceGameDays(1);
        Ok(session.RefreshProgress(Tx(), ecology));
        Check(session.Stage == GameplayStage.MainUnlocked && session.WalletCoin == 540 && session.Capture().ClaimedRewardIds.Count(r => r == "trial_reward") == 1,
            "real stable stocked observation atomically unlocks main fish and grants 450 once");
        int wallet = session.WalletCoin; Ok(session.RefreshProgress(Tx(), ecology));
        Check(session.WalletCoin == wallet, "repeated observation completion never repeats the stage reward");
        ecology.AddNitrogen(NitrogenPool.Tan, 54, "post-unlock-load");
        Check(!ecology.WaterIsStable && ecology.Challenge.Status == FishlessChallengeStatus.Passed, "new load is unstable and does not erase the passed challenge");
        Ok(session.RefreshProgress(Tx(), ecology));
        Check(session.Stage == GameplayStage.MainUnlocked && session.WalletCoin == wallet, "current instability does not revoke the unlocked stage or its reward");
        GameplaySession optional = GameplaySession.Restore(session.Capture(), catalog, new TankBounds());
        Ok(optional.Buy(Tx(), "wood_optional")); Ok(optional.Buy(Tx(), "wood_optional"));
        Check(optional.Buy(Tx(), "wood_optional").Code == "gameplay.budget_reserved" && optional.WalletCoin == 260,
            "post-reward budget permits surplus spending while preserving the 260-coin main pack");
        CommandResult neon = session.Buy(Tx(), "fish_main_pack"); Ok(neon);
        Check(neon.EntityIds.Length == 8 && session.WalletCoin == 280 && session.Stage == GameplayStage.Complete && session.LiveAnimalCount() == 22,
            "authoritative CSV route finishes with 8 Ember, 8 Neon, 6 shrimp and 280 coins");
        double Per(string id) { return double.Parse(Rows("species").Single(r => r["species_id"] == id)["game_bioload_per_individual"], CultureInfo.InvariantCulture); }
        double load = 8 * Per("ember_tetra") + 8 * Per("neon_tetra") + 6 * Per("neocaridina_red");
        double capacity = double.Parse(Rows("tanks").Single(r => r["tank_id"] == "tank_60cm")["game_bioload_capacity"], CultureInfo.InvariantCulture);
        Check(Math.Abs(load - 8.4) < 1e-9 && Math.Abs(capacity - 12) < 1e-9 && Math.Abs((capacity - load) / capacity - .3) < 1e-9,
            "current teaching route bioload is 8.4 of capacity 12, margin 0.3");
        Check(Rows("tanks").Single(r => r["tank_id"] == "tank_60cm")["simulation_ready"] == "false", "simulation_ready stays false");
        Check(session.Buy(Tx(), "food_basic").Code == "gameplay.care_requires_simulation", "free care must execute ecology and cannot masquerade as inert inventory");
        GameplaySnapshot valid = session.Capture(); string serialized = Serialized(session);
        GameplaySession loaded = GameplaySession.Restore(JsonSerializer.Deserialize<GameplaySnapshot>(serialized, Json), catalog, new TankBounds());
        Check(Serialized(loaded) == serialized, "serialized gameplay state restores exactly including receipts and stable IDs");
        GameplaySnapshot corrupt = session.Capture(); corrupt.WalletCoin++;
        Check(Rejects(() => GameplaySession.Restore(corrupt, catalog, new TankBounds())), "wallet not matching grant and batch ledgers rejects");
        corrupt = session.Capture(); corrupt.Batches.Last().EntityIds[0] = "forged-individual";
        Check(Rejects(() => GameplaySession.Restore(corrupt, catalog, new TankBounds())), "corrupt individual identity rejects");
        corrupt = session.Capture(); corrupt.Batches.Last().PaidCoin--; corrupt.WalletCoin++;
        Check(Rejects(() => GameplaySession.Restore(corrupt, catalog, new TankBounds())), "balanced wallet cannot conceal a corrupted frozen purchase price");
        corrupt = session.Capture(); corrupt.ClaimedRewardIds = new[] { "starter_grant", "trial_reward", "trial_reward" };
        Check(Rejects(() => GameplaySession.Restore(corrupt, catalog, new TankBounds())), "duplicate reward ledger rejects");
        corrupt = session.Capture(); corrupt.Layout.Placements[0].Size.X = double.PositiveInfinity;
        Check(Rejects(() => GameplaySession.Restore(corrupt, catalog, new TankBounds())), "non-finite saved geometry rejects");
        corrupt = session.Capture(); corrupt.Receipts[0].Result = null;
        Check(Rejects(() => GameplaySession.Restore(corrupt, catalog, new TankBounds())), "incomplete receipt rejects before live state replacement");
        Check(Serialized(session) == serialized && valid.WalletCoin == 280, "failed restore candidates leave the live session unchanged");
        string checkpoint = Serialized(session); string saleTx = Tx(); Ok(session.Sell(saleTx, neon.BatchId)); Ok(session.Sell(saleTx, neon.BatchId));
        Check(session.WalletCoin == 410 && !session.Sell(Tx(), neon.BatchId).Success && session.LiveAnimalCount() == 14,
            "whole-pack sale pays once and removes all eight main fish IDs");
        GameplaySnapshot badSale = session.Capture(); badSale.Batches.Last().SaleCoin++; badSale.WalletCoin++;
        Check(Rejects(() => GameplaySession.Restore(badSale, catalog, new TankBounds())), "balanced wallet cannot conceal a corrupted whole-pack sale price");
        for (int i = 0; i < 3; i++) session = GameplaySession.Restore(JsonSerializer.Deserialize<GameplaySnapshot>(checkpoint, Json), catalog, new TankBounds());
        Check(session.WalletCoin == 280 && session.LiveAnimalCount() == 22 && Serialized(session) == checkpoint,
            "domain checkpoint replacement restores wallet and batches together without retaining sale money");
    }
    private static int Main()
    {
        try { Checks(Catalog()); Console.WriteLine("Gameplay checks passed: " + checks); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
