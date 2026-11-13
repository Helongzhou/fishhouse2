using System;
using System.Collections.Generic;
using Aquarium.Core.Gameplay;

namespace Aquarium.Core.Configuration
{
    public sealed partial class RuntimeCatalog
    {
        public void Validate()
        {
            if (SchemaVersion != 1 || string.IsNullOrEmpty(ContentVersion) || ContentVersion.Length != 64)
                throw new ArgumentException("Unsupported or unidentified runtime configuration.");
            Unique(Tanks, x => x.TankId); Unique(Items, x => x.ItemId); Unique(Species, x => x.SpeciesId);
            Unique(Plants, x => x.Id); Unique(Behaviors, x => x.BehaviorProfileId); Unique(Gates, x => x.GateId);
            Unique(Economy, x => x.EconomyId); Unique(Route, x => x.RouteId + ":" + x.StepIndex);
            Unique(Compatibility, x => x.RuleId);
            if (Tanks.Length != 1 || Tanks[0].ContentStatus != "shipping" || Tanks[0].WaterType != "freshwater")
                throw new ArgumentException("A single default freshwater tank is required.");
            CreateTank();
            foreach (SpeciesConfig species in Species)
            {
                Find(Behaviors, x => x.BehaviorProfileId, species.BehaviorProfileId);
                if (!Scalar.IsFinite(species.GameBioloadPerIndividual) || species.GameBioloadPerIndividual < 0)
                    throw new ArgumentException("Invalid bioload tuning.");
            }
            foreach (ItemConfig item in Items)
            {
                if (!string.IsNullOrEmpty(item.SpeciesId)) Find(Species, x => x.SpeciesId, item.SpeciesId);
                if (item.UnlockGateId != "none") Find(Gates, x => x.GateId, item.UnlockGateId);
            }
            CreateGameplayCatalog(); CreateBehaviorProfiles();
        }
        public TankBounds CreateTank()
        {
            TankDefinition tank = Tanks[0];
            TankBounds bounds = new TankBounds { Width = tank.InnerLengthCm / 100, Depth = tank.InnerWidthCm / 100, Height = tank.InnerHeightCm / 100 };
            bounds.Validate();
            if (!Scalar.IsFinite(tank.EffectiveVolumeLDesign) || tank.EffectiveVolumeLDesign <= 0 || tank.EffectiveVolumeLDesign >= tank.GrossVolumeL)
                throw new ArgumentException("Invalid design water volume.");
            return bounds;
        }
        public ItemConfig Item(string id) { return Find(Items, x => x.ItemId, id); }
        public SpeciesConfig SpeciesById(string id) { return Find(Species, x => x.SpeciesId, id); }
        public GameplayCatalog CreateGameplayCatalog()
        {
            List<ItemDefinition> items = new List<ItemDefinition>(); List<PlantPlacementRule> plants = new List<PlantPlacementRule>();
            List<RouteRequirement> requirements = new List<RouteRequirement>();
            foreach (ItemConfig item in Items)
            {
                items.Add(new ItemDefinition { Id = item.ItemId, Kind = item.ItemKind, ContentStatus = item.ContentStatus,
                    SpeciesId = item.SpeciesId, UnlockGateId = item.UnlockGateId, PurchasePriceCoin = item.PurchasePriceCoin,
                    UnitsPerPurchase = item.UnitsPerPurchase, SellPriceCoin = item.PostConfirmSellPricePerPurchaseCoin, RouteRole = item.RouteRole });
                if (item.ItemKind == "plant")
                {
                    PlantConfig plant = Find(Plants, x => x.Id, item.SpeciesId);
                    plants.Add(new PlantPlacementRule { ItemId = item.ItemId, PlantingMethod = plant.PlantingMethod,
                        AllowedSurfaces = plant.AllowedSurfaces.Split('|'), RhizomeExposed = plant.RhizomeExposed == "true" });
                }
            }
            int rewardStep = int.MaxValue;
            foreach (RouteConfig step in Route) if (step.EconomyId == "trial_reward") rewardStep = step.StepIndex;
            foreach (RouteConfig step in Route) if (step.Action == "purchase" && step.RequiredForV0)
                requirements.Add(new RouteRequirement { ItemId = step.ItemId, PurchaseCount = step.PurchaseCount, BeforeTrialReward = step.StepIndex < rewardStep });
            return new GameplayCatalog(items.ToArray(), plants.ToArray(), requirements.ToArray(),
                Find(Economy, x => x.EconomyId, "starter_grant").AmountCoin, Find(Economy, x => x.EconomyId, "trial_reward").AmountCoin);
        }
        public Dictionary<string, SpeciesProfile> CreateBehaviorProfiles()
        {
            Dictionary<string, SpeciesProfile> profiles = new Dictionary<string, SpeciesProfile>(StringComparer.Ordinal);
            foreach (SpeciesConfig species in Species)
            {
                if (species.ContentStatus != "shipping" || species.InclusionScope != "v0_default" || (species.ActorKind != "fish" && species.ActorKind != "shrimp")) continue;
                BehaviorConfig behavior = Find(Behaviors, x => x.BehaviorProfileId, species.BehaviorProfileId);
                // Existing proxy movement defaults are game tuning, not inferred scientific husbandry values.
                SpeciesProfile profile = species.ActorKind == "shrimp" ? DemoProfiles.Shrimp() : DemoProfiles.CommunityFish();
                profile.Id = species.SpeciesId; profile.FearSensitivity = behavior.StartleSensitivity01;
                if (!behavior.SupportsSchooling) profile.ShoalAffinity = .12;
                profile.Validate(CreateTank()); profiles.Add(profile.Id, profile);
            }
            return profiles;
        }
        private static T Find<T>(T[] records, Func<T, string> key, string id) where T : class
        { foreach (T row in records) if (key(row) == id) return row; throw new ArgumentException("Unknown configuration reference: " + id); }
        private static void Unique<T>(T[] records, Func<T, string> key) where T : class
        {
            if (records == null) throw new ArgumentException("Missing configuration table.");
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (T row in records) if (row == null || string.IsNullOrWhiteSpace(key(row)) || !ids.Add(key(row)))
                throw new ArgumentException("Missing or duplicate configuration record.");
        }
    }
}
