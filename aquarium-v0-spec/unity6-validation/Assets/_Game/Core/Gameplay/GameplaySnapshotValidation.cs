using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aquarium.Core.Gameplay
{
    public sealed partial class GameplaySession
    {
        private static GameplaySnapshot Clone(GameplaySnapshot s)
        {
            GameplaySnapshot c = new GameplaySnapshot { SchemaVersion = s.SchemaVersion, SaveId = s.SaveId, WalletCoin = s.WalletCoin,
                Stage = s.Stage, Paused = s.Paused, Filled = s.Filled, WaterlineHeight = s.WaterlineHeight, Layout = s.Layout.Clone(),
                Batches = new PurchaseBatch[s.Batches.Length], Receipts = new CommandReceipt[s.Receipts.Length],
                ClaimedRewardIds = (string[])s.ClaimedRewardIds.Clone(), UndoHistory = new LayoutState[s.UndoHistory.Length],
                RedoHistory = new LayoutState[s.RedoHistory.Length] };
            for (int i = 0; i < c.Batches.Length; i++) c.Batches[i] = s.Batches[i].Clone();
            for (int i = 0; i < c.Receipts.Length; i++) c.Receipts[i] = s.Receipts[i].Clone();
            for (int i = 0; i < c.UndoHistory.Length; i++) c.UndoHistory[i] = s.UndoHistory[i].Clone();
            for (int i = 0; i < c.RedoHistory.Length; i++) c.RedoHistory[i] = s.RedoHistory[i].Clone();
            return c;
        }
        public static GameplaySession Restore(GameplaySnapshot snapshot, GameplayCatalog catalog, TankBounds tank)
        {
            GameplaySession candidate = new GameplaySession(catalog, tank);
            GameplaySnapshot normalized = NormalizeOptionalLayouts(snapshot);
            candidate.ValidateSnapshot(normalized); candidate.state = Clone(normalized); return candidate;
        }
        // Unity serializes null ordinary inline classes as empty objects. Presence is explicit;
        // only an exactly empty absent payload is normalized, never a zero-height present substrate.
        private static LayoutState NormalizeOptionalSubstrate(LayoutState layout)
        {
            if (layout == null) return null;
            SubstrateProfile p = layout.Substrate;
            if (layout.HasSubstrate)
            { if (p == null) Invalid(); }
            else if (p != null && (!string.IsNullOrEmpty(p.PurchaseBatchId) || p.FrontLeftHeight != 0 ||
                p.FrontRightHeight != 0 || p.BackLeftHeight != 0 || p.BackRightHeight != 0)) Invalid();
            return new LayoutState { HasSubstrate = layout.HasSubstrate, Substrate = layout.HasSubstrate ? p : null, Placements = layout.Placements };
        }
        private static LayoutState[] NormalizeOptionalSubstrates(LayoutState[] layouts)
        {
            if (layouts == null) return null;
            if (layouts.Length > HistoryLimit) Invalid();
            var result = new LayoutState[layouts.Length];
            for (int i = 0; i < result.Length; i++) result[i] = NormalizeOptionalSubstrate(layouts[i]);
            return result;
        }
        private static GameplaySnapshot NormalizeOptionalLayouts(GameplaySnapshot s)
        {
            if (s == null) return null;
            return new GameplaySnapshot { SchemaVersion = s.SchemaVersion, SaveId = s.SaveId, WalletCoin = s.WalletCoin,
                Stage = s.Stage, Paused = s.Paused, Filled = s.Filled, WaterlineHeight = s.WaterlineHeight,
                Layout = NormalizeOptionalSubstrate(s.Layout), Batches = s.Batches, Receipts = s.Receipts,
                ClaimedRewardIds = s.ClaimedRewardIds, UndoHistory = NormalizeOptionalSubstrates(s.UndoHistory),
                RedoHistory = NormalizeOptionalSubstrates(s.RedoHistory) };
        }
        private static void Invalid() { throw new ArgumentException("Invalid or inconsistent gameplay snapshot."); }
        private static bool ValidArray<T>(T[] values, int max) { return values != null && values.Length <= max; }
        private void ValidateSnapshot(GameplaySnapshot s)
        {
            if (s == null || s.SchemaVersion != 2 || string.IsNullOrWhiteSpace(s.SaveId) || s.SaveId.Length > 100 ||
                s.WalletCoin < 0 || !Enum.IsDefined(typeof(GameplayStage), s.Stage) ||
                s.Filled != (s.Stage >= GameplayStage.Filled) || !Scalar.IsFinite(s.WaterlineHeight) ||
                Math.Abs(s.WaterlineHeight - (tank.Height - .03)) > .0000001 ||
                !ValidArray(s.Batches, ReceiptLimit) || !ValidArray(s.Receipts, ReceiptLimit) || !ValidArray(s.ClaimedRewardIds, 2) ||
                !ValidArray(s.UndoHistory, HistoryLimit) || !ValidArray(s.RedoHistory, HistoryLimit) ||
                s.Layout == null || !ValidArray(s.Layout.Placements, ReceiptLimit)) Invalid();
            HashSet<string> rewards = new HashSet<string>();
            foreach (string reward in s.ClaimedRewardIds)
                if ((reward != "starter_grant" && reward != "trial_reward") || !rewards.Add(reward)) Invalid();
            if (!rewards.Contains("starter_grant") || rewards.Contains("trial_reward") != (s.Stage >= GameplayStage.MainUnlocked)) Invalid();
            Dictionary<string, CommandReceipt> receipts = new Dictionary<string, CommandReceipt>();
            HashSet<string> refundBatches = new HashSet<string>(), saleBatches = new HashSet<string>();
            foreach (CommandReceipt receipt in s.Receipts)
            {
                if (receipt == null || string.IsNullOrWhiteSpace(receipt.TransactionId) || receipt.TransactionId.Length > 160 ||
                    string.IsNullOrEmpty(receipt.Fingerprint) || receipt.Result == null || !receipt.Result.Success ||
                    receipt.Result.Code != "gameplay.ok" || receipt.Result.Args == null || receipt.Result.EntityIds == null ||
                    receipts.ContainsKey(receipt.TransactionId)) Invalid();
                receipts.Add(receipt.TransactionId, receipt);
                if (receipt.Fingerprint.StartsWith("refund:", StringComparison.Ordinal))
                { if (receipt.Result.BatchId == null || !refundBatches.Add(receipt.Result.BatchId) || receipt.Fingerprint != "refund:" + Part(receipt.Result.BatchId)) Invalid(); }
                if (receipt.Fingerprint.StartsWith("sell:", StringComparison.Ordinal))
                { if (receipt.Result.BatchId == null || !saleBatches.Add(receipt.Result.BatchId) || receipt.Fingerprint != "sell:" + Part(receipt.Result.BatchId)) Invalid(); }
            }
            HashSet<string> ids = new HashSet<string>(), entities = new HashSet<string>();
            long expectedWallet = catalog.StarterGrantCoin + (rewards.Contains("trial_reward") ? (long)catalog.TrialRewardCoin : 0);
            foreach (PurchaseBatch batch in s.Batches)
            {
                ItemDefinition item = null; CommandReceipt receipt = null;
                if (batch == null || string.IsNullOrEmpty(batch.TransactionId) || batch.Id != "batch:" + batch.TransactionId || !ids.Add(batch.Id) ||
                    batch.ItemId == null || !catalog.Items.TryGetValue(batch.ItemId, out item) || item.ContentStatus != "shipping" || item.Kind == "care" ||
                    batch.PaidCoin != item.PurchasePriceCoin || batch.DeliveredUnits != item.UnitsPerPurchase || batch.SaleCoin < 0 || batch.SaleCoin > batch.PaidCoin ||
                    (batch.Refunded && batch.Sold) || !receipts.TryGetValue(batch.TransactionId, out receipt) ||
                    receipt.Fingerprint != "buy:" + Part(batch.ItemId) || receipt.Result.BatchId != batch.Id ||
                    batch.EntityIds == null || batch.EntityIds.Length != (IsAnimal(item) ? batch.DeliveredUnits : 0) ||
                    receipt.Result.EntityIds.Length != batch.EntityIds.Length || batch.Refunded != refundBatches.Contains(batch.Id) ||
                    batch.Sold != saleBatches.Contains(batch.Id) || batch.SaleCoin != (batch.Sold ? Math.Min(batch.PaidCoin, item.SellPriceCoin) : 0)) Invalid();
                for (int i = 0; i < batch.EntityIds.Length; i++)
                    if (batch.EntityIds[i] != s.SaveId + "/" + batch.Id + "/" + i.ToString(CultureInfo.InvariantCulture) ||
                        !entities.Add(batch.EntityIds[i]) || receipt.Result.EntityIds[i] != batch.EntityIds[i]) Invalid();
                if (!batch.Refunded) expectedWallet -= batch.PaidCoin;
                if (batch.Sold) expectedWallet += batch.SaleCoin;
                if (IsAnimal(item) && s.Stage < GameplayStage.CyclePassed) Invalid();
                if (item.RouteRole == "required_mainfish" && s.Stage != GameplayStage.Complete) Invalid();
            }
            foreach (string id in refundBatches) if (!ids.Contains(id)) Invalid();
            foreach (string id in saleBatches) if (!ids.Contains(id)) Invalid();
            foreach (CommandReceipt receipt in s.Receipts)
                if (receipt.Fingerprint.StartsWith("buy:", StringComparison.Ordinal) &&
                    (receipt.Result.BatchId == null || !ids.Contains(receipt.Result.BatchId))) Invalid();
            if (expectedWallet != s.WalletCoin || expectedWallet < 0 || expectedWallet > int.MaxValue) Invalid();
            ValidateSavedLayout(s, s.Layout);
            foreach (LayoutState layout in s.UndoHistory) { ValidateSavedLayout(s, layout); ValidateHistoryScope(s, layout); }
            foreach (LayoutState layout in s.RedoHistory) { ValidateSavedLayout(s, layout); ValidateHistoryScope(s, layout); }
            if (s.Stage >= GameplayStage.Confirmed && s.Stage != GameplayStage.Complete)
                foreach (RouteRequirement r in catalog.Requirements)
                {
                    ItemDefinition item = catalog.Items[r.ItemId]; if (item.RouteRole != "required_base") continue;
                    int deployed = 0;
                    if (s.Layout.Substrate != null && FindBatch(s, s.Layout.Substrate.PurchaseBatchId).ItemId == item.Id) deployed++;
                    foreach (LayoutPlacement p in s.Layout.Placements) if (FindBatch(s, p.PurchaseBatchId).ItemId == item.Id) deployed++;
                    if (deployed < r.PurchaseCount) Invalid();
                }
            if (s.Stage >= GameplayStage.TrialObserving && !HasHistoricalRole(s, "required_trial")) Invalid();
            if (s.Stage == GameplayStage.Complete && !HasHistoricalRole(s, "required_mainfish")) Invalid();
            double litres = LayoutGeometry.EffectiveLiters(s.Layout, catalog, tank, s.WaterlineHeight, id => FindBatch(s, id));
            if (!Scalar.IsFinite(litres) || litres <= 0) Invalid();
        }
        private bool HasHistoricalRole(GameplaySnapshot s, string role)
        { foreach (PurchaseBatch b in s.Batches) if (!b.Refunded && catalog.Items[b.ItemId].RouteRole == role) return true; return false; }
        private void ValidateSavedLayout(GameplaySnapshot s, LayoutState layout)
        {
            if (layout == null || !ValidArray(layout.Placements, ReceiptLimit) ||
                !LayoutGeometry.Validate(layout, catalog, tank, id => FindBatch(s, id)).Success) Invalid();
        }
        private void ValidateHistoryScope(GameplaySnapshot s, LayoutState layout)
        {
            if (s.Stage == GameplayStage.Editing) return;
            SubstrateProfile a = s.Layout.Substrate, b = layout.Substrate;
            if ((a == null) != (b == null)) Invalid();
            if (a != null && (a.PurchaseBatchId != b.PurchaseBatchId || a.FrontLeftHeight != b.FrontLeftHeight ||
                a.FrontRightHeight != b.FrontRightHeight || a.BackLeftHeight != b.BackLeftHeight || a.BackRightHeight != b.BackRightHeight)) Invalid();
            if (layout.Placements.Length != s.Layout.Placements.Length) Invalid();
            foreach (LayoutPlacement placement in layout.Placements)
            {
                LayoutPlacement live = FindPlacement(s.Layout, placement.Id);
                if (live == null || live.PurchaseBatchId != placement.PurchaseBatchId || Vec3.Distance(live.Size, placement.Size) > LayoutGeometry.Tolerance) Invalid();
                if (catalog.Items[FindBatch(s, placement.PurchaseBatchId).ItemId].Kind != "plant" && PlacementKey(live) != PlacementKey(placement)) Invalid();
            }
        }
    }
}
