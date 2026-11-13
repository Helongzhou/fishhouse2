using System;
using System.Collections.Generic;
using System.Globalization;
using Aquarium.Core.Ecology;

namespace Aquarium.Core.Gameplay
{
    public sealed partial class GameplaySession
    {
        private readonly GameplayCatalog catalog;
        private readonly TankBounds tank;
        private GameplaySnapshot state;
        private const int HistoryLimit = 32, ReceiptLimit = 20000;
        public bool Paused { get { return state.Paused; } }
        public bool Filled { get { return state.Filled; } }
        public GameplayStage Stage { get { return state.Stage; } }
        public int WalletCoin { get { return state.WalletCoin; } }

        public GameplaySession(GameplayCatalog catalog, TankBounds tank)
        {
            if (catalog == null || tank == null) throw new ArgumentNullException("catalog/tank");
            tank.Validate(); this.catalog = catalog; this.tank = tank.Clone();
            state = new GameplaySnapshot { SaveId = Guid.NewGuid().ToString("N"), WalletCoin = catalog.StarterGrantCoin,
                WaterlineHeight = tank.Height - .03, ClaimedRewardIds = new[] { "starter_grant" } };
        }
        public GameplaySnapshot Capture() { return Clone(state); }
        public void SetPaused(bool paused) { state.Paused = paused; }
        public double CalculateEffectiveWaterLiters()
        { return LayoutGeometry.EffectiveLiters(state.Layout, catalog, tank, state.WaterlineHeight, id => FindBatch(state, id)); }
        public double GetSubstrateHeight(double x, double z)
        {
            if (!Scalar.IsFinite(x) || !Scalar.IsFinite(z)) throw new ArgumentException("Non-finite location.");
            return LayoutGeometry.Height(state.Layout.Substrate, tank, x, z);
        }
        public double SuggestedBottomHeight(Vec3 position, Vec3 size, double yawDegrees)
        {
            if (!Scalar.IsFinite(position) || !Scalar.IsFinite(size) || !Scalar.IsFinite(yawDegrees))
                throw new ArgumentException("Non-finite placement.");
            return LayoutGeometry.BottomHeight(new LayoutPlacement { Position = position, Size = size, YawDegrees = yawDegrees }, state.Layout.Substrate, tank);
        }
        public int ReservedRequiredCoin() { return Reserve(state); }
        public int LiveAnimalCount()
        {
            int count = 0;
            foreach (PurchaseBatch batch in state.Batches)
                if (Active(batch) && IsAnimal(catalog.Items[batch.ItemId])) count += batch.DeliveredUnits;
            return count;
        }
        private static bool IsAnimal(ItemDefinition item) { return item.Kind == "fish" || item.Kind == "shrimp"; }
        private static bool Active(PurchaseBatch batch) { return !batch.Refunded && !batch.Sold; }
        private static PurchaseBatch FindBatch(GameplaySnapshot s, string id)
        { foreach (PurchaseBatch b in s.Batches) if (b.Id == id) return b; return null; }
        private static LayoutPlacement FindPlacement(LayoutState s, string id)
        { foreach (LayoutPlacement p in s.Placements) if (p.Id == id) return p; return null; }
        private static bool Claimed(GameplaySnapshot s, string reward) { return Array.IndexOf(s.ClaimedRewardIds, reward) >= 0; }
        private int Owned(GameplaySnapshot s, string itemId)
        { int n = 0; foreach (PurchaseBatch b in s.Batches) if (Active(b) && b.ItemId == itemId) n++; return n; }
        private int Reserve(GameplaySnapshot s)
        {
            if (s.Stage == GameplayStage.Complete) return 0;
            long required = 0; bool reward = Claimed(s, "trial_reward");
            foreach (RouteRequirement r in catalog.Requirements)
                if (r.BeforeTrialReward != reward)
                    required += (long)Math.Max(0, r.PurchaseCount - Owned(s, r.ItemId)) * catalog.Items[r.ItemId].PurchasePriceCoin;
            if (required > int.MaxValue) throw new ArgumentException("Required budget exceeds integer range.");
            return (int)required;
        }
        private static string Number(double n) { return n.ToString("R", CultureInfo.InvariantCulture); }
        private static string Part(string s) { return s == null ? "-1:" : s.Length.ToString(CultureInfo.InvariantCulture) + ":" + s; }
        private static string Vector(Vec3 p) { return Number(p.X) + "," + Number(p.Y) + "," + Number(p.Z); }
        private static string PlacementKey(LayoutPlacement p)
        { return p == null ? "null" : Part(p.Id) + Part(p.PurchaseBatchId) + Part(p.SupportPlacementId) + Vector(p.Position) + ";" + Vector(p.Size) + ";" + Number(p.YawDegrees) + ";" + p.RhizomeBuried; }
        private CommandResult Execute(string transactionId, string fingerprint, Func<GameplaySnapshot, CommandResult> operation)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Length > 160) return CommandResult.Fail("transaction_id_invalid");
            foreach (CommandReceipt receipt in state.Receipts)
                if (receipt.TransactionId == transactionId)
                    return receipt.Fingerprint == fingerprint ? receipt.Result.Clone() : CommandResult.Fail("transaction_id_conflict");
            if (state.Paused) return CommandResult.Fail("paused");
            if (state.Receipts.Length >= ReceiptLimit) return CommandResult.Fail("ledger_full");
            GameplaySnapshot candidate = Clone(state); CommandResult result = operation(candidate);
            if (!result.Success) return result;
            List<CommandReceipt> receipts = new List<CommandReceipt>(candidate.Receipts);
            receipts.Add(new CommandReceipt { TransactionId = transactionId, Fingerprint = fingerprint, Result = result.Clone() });
            candidate.Receipts = receipts.ToArray(); state = candidate; return result.Clone();
        }
        private static void ClearHistory(GameplaySnapshot s) { s.UndoHistory = new LayoutState[0]; s.RedoHistory = new LayoutState[0]; }
        private static bool Unlocked(GameplaySnapshot s, string gate)
        {
            if (string.IsNullOrEmpty(gate) || gate == "none") return true;
            if (gate == "gate_setup") return s.Stage >= GameplayStage.Confirmed;
            if (gate == "gate_filled") return s.Filled;
            if (gate == "gate_cycle") return s.Stage >= GameplayStage.CyclePassed;
            if (gate == "gate_trial") return s.Stage >= GameplayStage.TrialObserving;
            if (gate == "gate_mainfish") return s.Stage >= GameplayStage.MainUnlocked;
            if (gate == "gate_complete") return s.Stage == GameplayStage.Complete;
            return false;
        }
        public CommandResult Buy(string transactionId, string itemId)
        {
            return Execute(transactionId, "buy:" + Part(itemId), s =>
            {
                ItemDefinition item;
                if (itemId == null || !catalog.Items.TryGetValue(itemId, out item) || item.ContentStatus != "shipping")
                    return CommandResult.Fail("item_unavailable", itemId ?? "");
                if (item.Kind == "care") return CommandResult.Fail("care_requires_simulation", itemId);
                if (!Unlocked(s, item.UnlockGateId)) return CommandResult.Fail("gate_locked", item.UnlockGateId ?? "");
                if (s.WalletCoin < item.PurchasePriceCoin) return CommandResult.Fail("insufficient_coin", item.PurchasePriceCoin.ToString(CultureInfo.InvariantCulture));
                string batchId = "batch:" + transactionId;
                string[] entities = new string[IsAnimal(item) ? item.UnitsPerPurchase : 0];
                for (int i = 0; i < entities.Length; i++) entities[i] = s.SaveId + "/" + batchId + "/" + i.ToString(CultureInfo.InvariantCulture);
                PurchaseBatch batch = new PurchaseBatch { Id = batchId, TransactionId = transactionId, ItemId = itemId,
                    PaidCoin = item.PurchasePriceCoin, DeliveredUnits = item.UnitsPerPurchase, EntityIds = entities };
                List<PurchaseBatch> batches = new List<PurchaseBatch>(s.Batches); batches.Add(batch); s.Batches = batches.ToArray();
                s.WalletCoin -= item.PurchasePriceCoin;
                int reserve = Reserve(s);
                if (s.WalletCoin < reserve) return CommandResult.Fail("budget_reserved", reserve.ToString(CultureInfo.InvariantCulture));
                if (item.RouteRole == "required_trial" && s.Stage == GameplayStage.CyclePassed) s.Stage = GameplayStage.TrialObserving;
                if (item.RouteRole == "required_mainfish" && s.Stage == GameplayStage.MainUnlocked) s.Stage = GameplayStage.Complete;
                ClearHistory(s); return CommandResult.Ok(batchId, entities);
            });
        }
        public CommandResult Refund(string transactionId, string purchaseBatchId)
        { return ReturnBatch(transactionId, purchaseBatchId, true); }
        public CommandResult Sell(string transactionId, string purchaseBatchId)
        { return ReturnBatch(transactionId, purchaseBatchId, false); }
        private CommandResult ReturnBatch(string tx, string batchId, bool refund)
        {
            return Execute(tx, (refund ? "refund:" : "sell:") + Part(batchId), s =>
            {
                PurchaseBatch batch = FindBatch(s, batchId);
                if (batch == null || !Active(batch)) return CommandResult.Fail("batch_unavailable");
                if (refund && s.Stage != GameplayStage.Editing) return CommandResult.Fail("refund_closed");
                if (!refund && s.Stage == GameplayStage.Editing) return CommandResult.Fail("use_refund");
                ItemDefinition item = catalog.Items[batch.ItemId];
                if (!refund && s.Stage != GameplayStage.Complete)
                    foreach (RouteRequirement r in catalog.Requirements)
                        if (r.ItemId == item.Id && Owned(s, item.Id) <= r.PurchaseCount) return CommandResult.Fail("required_item_protected", item.Id);
                foreach (LayoutPlacement p in s.Layout.Placements)
                    if (p.PurchaseBatchId == batchId)
                        foreach (LayoutPlacement dependent in s.Layout.Placements)
                            if (dependent.SupportPlacementId == p.Id) return CommandResult.Fail("support_in_use", p.Id);
                List<LayoutPlacement> placements = new List<LayoutPlacement>();
                foreach (LayoutPlacement p in s.Layout.Placements)
                    if (p.PurchaseBatchId != batchId) placements.Add(p);
                if (!refund && s.Stage != GameplayStage.Editing && item.Kind != "plant" && placements.Count != s.Layout.Placements.Length)
                    return CommandResult.Fail("layout_locked");
                s.Layout.Placements = placements.ToArray();
                if (s.Layout.Substrate != null && s.Layout.Substrate.PurchaseBatchId == batchId)
                {
                    if (s.Layout.Placements.Length != 0) return CommandResult.Fail("support_in_use", "substrate");
                    if (!refund) return CommandResult.Fail("layout_locked");
                    s.Layout.Substrate = null;
                }
                int amount = refund ? batch.PaidCoin : Math.Min(batch.PaidCoin, item.SellPriceCoin);
                if ((long)s.WalletCoin + amount > int.MaxValue) return CommandResult.Fail("coin_overflow");
                s.WalletCoin += amount; batch.Refunded = refund; batch.Sold = !refund; batch.SaleCoin = refund ? 0 : amount;
                ClearHistory(s); return CommandResult.Ok(batch.Id, batch.EntityIds);
            });
        }
        public CommandResult ConfirmLayout(string transactionId)
        {
            return Execute(transactionId, "confirm", s =>
            {
                if (s.Stage != GameplayStage.Editing) return CommandResult.Fail("stage_invalid");
                CommandResult valid = LayoutGeometry.Validate(s.Layout, catalog, tank, id => FindBatch(s, id));
                if (!valid.Success) return valid;
                foreach (RouteRequirement r in catalog.Requirements)
                {
                    ItemDefinition item = catalog.Items[r.ItemId]; if (item.RouteRole != "required_base") continue;
                    int deployed = 0;
                    if (s.Layout.Substrate != null && FindBatch(s, s.Layout.Substrate.PurchaseBatchId).ItemId == item.Id) deployed++;
                    foreach (LayoutPlacement p in s.Layout.Placements) if (FindBatch(s, p.PurchaseBatchId).ItemId == item.Id) deployed++;
                    if (deployed < r.PurchaseCount) return CommandResult.Fail("required_deployment_missing", item.Id);
                }
                double litres = LayoutGeometry.EffectiveLiters(s.Layout, catalog, tank, s.WaterlineHeight, id => FindBatch(s, id));
                if (!Scalar.IsFinite(litres) || litres <= 0) return CommandResult.Fail("water_volume_invalid");
                s.Stage = GameplayStage.Confirmed; ClearHistory(s); return CommandResult.Ok();
            });
        }
        public CommandResult Fill(string transactionId)
        {
            return Execute(transactionId, "fill", s =>
            {
                if (s.Filled) return CommandResult.Fail("already_filled");
                if (s.Stage != GameplayStage.Confirmed) return CommandResult.Fail("stage_invalid");
                s.Filled = true; s.Stage = GameplayStage.Filled; ClearHistory(s); return CommandResult.Ok();
            });
        }
        public CommandResult RefreshProgress(string transactionId, EcologyEngine engine)
        {
            if (engine == null) return CommandResult.Fail("ecology_required");
            return Execute(transactionId, "refresh", s =>
            {
                if (!s.Filled) return CommandResult.Fail("stage_invalid");
                double litres = LayoutGeometry.EffectiveLiters(s.Layout, catalog, tank, s.WaterlineHeight, id => FindBatch(s, id));
                if (Math.Abs(litres - engine.VolumeLiters) > .000001) return CommandResult.Fail("water_volume_mismatch");
                if (s.Stage == GameplayStage.Filled || s.Stage == GameplayStage.Cycling)
                {
                    if (engine.Challenge.Status == FishlessChallengeStatus.Passed && engine.WaterIsStable) s.Stage = GameplayStage.CyclePassed;
                    else if (engine.Challenge.Status == FishlessChallengeStatus.Processing || engine.Challenge.Status == FishlessChallengeStatus.Stabilizing)
                        s.Stage = GameplayStage.Cycling;
                }
                if (s.Stage == GameplayStage.TrialObserving && engine.TrialObservation.Status == TrialObservationStatus.Passed && engine.WaterIsStable)
                {
                    foreach (RouteRequirement r in catalog.Requirements)
                        if (r.BeforeTrialReward && Owned(s, r.ItemId) < r.PurchaseCount)
                            return CommandResult.Fail("required_purchase_missing", r.ItemId);
                    if (!Claimed(s, "trial_reward"))
                    {
                        if ((long)s.WalletCoin + catalog.TrialRewardCoin > int.MaxValue) return CommandResult.Fail("coin_overflow");
                        s.WalletCoin += catalog.TrialRewardCoin;
                        List<string> rewards = new List<string>(s.ClaimedRewardIds); rewards.Add("trial_reward"); s.ClaimedRewardIds = rewards.ToArray();
                    }
                    s.Stage = GameplayStage.MainUnlocked;
                }
                return CommandResult.Ok();
            });
        }
    }
}
