using System;
using System.Collections.Generic;

namespace Aquarium.Core.Gameplay
{
    public enum GameplayStage { Editing, Confirmed, Filled, Cycling, CyclePassed, TrialObserving, MainUnlocked, Complete }

    [Serializable]
    public sealed class ItemDefinition
    {
        public string Id, Kind, ContentStatus, SpeciesId, UnlockGateId, RouteRole;
        public int PurchasePriceCoin, UnitsPerPurchase, SellPriceCoin;
        public ItemDefinition Clone() { return (ItemDefinition)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class PlantPlacementRule
    {
        public string ItemId, PlantingMethod;
        public string[] AllowedSurfaces = new string[0];
        public bool RhizomeExposed;
        public PlantPlacementRule Clone()
        { PlantPlacementRule c = (PlantPlacementRule)MemberwiseClone(); c.AllowedSurfaces = (string[])AllowedSurfaces.Clone(); return c; }
    }
    [Serializable]
    public sealed class RouteRequirement
    {
        public string ItemId;
        public int PurchaseCount;
        public bool BeforeTrialReward;
        public RouteRequirement Clone() { return (RouteRequirement)MemberwiseClone(); }
    }
    public sealed class GameplayCatalog
    {
        internal readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        internal readonly Dictionary<string, PlantPlacementRule> Plants = new Dictionary<string, PlantPlacementRule>(StringComparer.Ordinal);
        internal readonly List<RouteRequirement> Requirements = new List<RouteRequirement>();
        public int StarterGrantCoin { get; private set; }
        public int TrialRewardCoin { get; private set; }
        public GameplayCatalog(ItemDefinition[] items, PlantPlacementRule[] plants, RouteRequirement[] requiredPurchases,
            int starterGrantCoin, int trialRewardCoin)
        {
            if (items == null || plants == null || requiredPurchases == null || starterGrantCoin < 0 || trialRewardCoin < 0)
                throw new ArgumentException("Incomplete gameplay catalog.");
            StarterGrantCoin = starterGrantCoin; TrialRewardCoin = trialRewardCoin;
            foreach (ItemDefinition item in items)
            {
                if (item == null || string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Kind) ||
                    string.IsNullOrEmpty(item.ContentStatus) || item.UnitsPerPurchase <= 0 || item.PurchasePriceCoin < 0 ||
                    item.SellPriceCoin < 0 || item.SellPriceCoin > item.PurchasePriceCoin || Items.ContainsKey(item.Id))
                    throw new ArgumentException("Invalid or duplicate gameplay item.");
                Items.Add(item.Id, item.Clone());
            }
            foreach (PlantPlacementRule plant in plants)
            {
                ItemDefinition item;
                if (plant == null || plant.ItemId == null || !Items.TryGetValue(plant.ItemId, out item) || item.Kind != "plant" ||
                    plant.AllowedSurfaces == null || plant.AllowedSurfaces.Length == 0 || Plants.ContainsKey(plant.ItemId))
                    throw new ArgumentException("Invalid plant placement rule.");
                Plants.Add(plant.ItemId, plant.Clone());
            }
            HashSet<string> required = new HashSet<string>();
            foreach (RouteRequirement requirement in requiredPurchases)
            {
                ItemDefinition item;
                if (requirement == null || requirement.ItemId == null || !Items.TryGetValue(requirement.ItemId, out item) ||
                    item.ContentStatus != "shipping" || requirement.PurchaseCount <= 0 || !required.Add(requirement.ItemId))
                    throw new ArgumentException("Invalid required route purchase.");
                Requirements.Add(requirement.Clone());
            }
        }
        public ItemDefinition[] GetItems()
        { List<ItemDefinition> items = new List<ItemDefinition>(); foreach (ItemDefinition item in Items.Values) items.Add(item.Clone()); return items.ToArray(); }
    }
    [Serializable]
    public sealed class CommandResult
    {
        public bool Success;
        public string Code;
        public string[] Args = new string[0];
        public string BatchId;
        public string[] EntityIds = new string[0];
        public CommandResult Clone()
        {
            CommandResult c = (CommandResult)MemberwiseClone(); c.Args = (string[])Args.Clone();
            c.EntityIds = (string[])EntityIds.Clone(); return c;
        }
        internal static CommandResult Ok(string batchId = null, string[] entityIds = null)
        { return new CommandResult { Success = true, Code = "gameplay.ok", BatchId = batchId, EntityIds = entityIds ?? new string[0] }; }
        internal static CommandResult Fail(string code, params string[] args)
        { return new CommandResult { Code = "gameplay." + code, Args = args }; }
    }
    [Serializable]
    public sealed class PurchaseBatch
    {
        public string Id, ItemId, TransactionId;
        public int PaidCoin, DeliveredUnits, SaleCoin;
        public bool Refunded, Sold;
        public string[] EntityIds = new string[0];
        public PurchaseBatch Clone()
        { PurchaseBatch c = (PurchaseBatch)MemberwiseClone(); c.EntityIds = (string[])EntityIds.Clone(); return c; }
    }
    [Serializable]
    public sealed class LayoutPlacement
    {
        public string Id, PurchaseBatchId, SupportPlacementId;
        // Solid positions are box centres. Attached plants use a surface anchor.
        public Vec3 Position, Size;
        public double YawDegrees;
        public bool RhizomeBuried;
        public LayoutPlacement Clone() { return (LayoutPlacement)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class SubstrateProfile
    {
        public string PurchaseBatchId;
        public double FrontLeftHeight, FrontRightHeight, BackLeftHeight, BackRightHeight;
        public SubstrateProfile Clone() { return (SubstrateProfile)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class LayoutState
    {
        public SubstrateProfile Substrate;
        public LayoutPlacement[] Placements = new LayoutPlacement[0];
        public LayoutState Clone()
        {
            LayoutState c = new LayoutState { Substrate = Substrate == null ? null : Substrate.Clone(), Placements = new LayoutPlacement[Placements.Length] };
            for (int i = 0; i < Placements.Length; i++) c.Placements[i] = Placements[i].Clone(); return c;
        }
    }
    [Serializable]
    public sealed class CommandReceipt
    {
        public string TransactionId, Fingerprint;
        public CommandResult Result;
        public CommandReceipt Clone() { return new CommandReceipt { TransactionId = TransactionId, Fingerprint = Fingerprint, Result = Result.Clone() }; }
    }
    [Serializable]
    public sealed class GameplaySnapshot
    {
        public int SchemaVersion = 1;
        public string SaveId;
        public int WalletCoin;
        public GameplayStage Stage;
        public bool Paused, Filled;
        public double WaterlineHeight;
        public LayoutState Layout = new LayoutState();
        public PurchaseBatch[] Batches = new PurchaseBatch[0];
        public CommandReceipt[] Receipts = new CommandReceipt[0];
        public string[] ClaimedRewardIds = new string[0];
        public LayoutState[] UndoHistory = new LayoutState[0], RedoHistory = new LayoutState[0];
    }
}
