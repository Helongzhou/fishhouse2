using System;
using System.Collections.Generic;

namespace Aquarium.Core.Configuration
{
    // Generated field contract mirrors the nine CSV tables. Text is rendered through locale keys.
    [Serializable]
    public sealed partial class RuntimeCatalog
    {
        public int SchemaVersion = 1;
        public string ContentVersion;
        public TankDefinition[] Tanks;
        public ItemConfig[] Items;
        public SpeciesConfig[] Species;
        public PlantConfig[] Plants;
        public BehaviorConfig[] Behaviors;
        public GateConfig[] Gates;
        public EconomyConfig[] Economy;
        public RouteConfig[] Route;
        public CompatibilityConfig[] Compatibility;
    }

    [Serializable]
    public sealed class TankDefinition
    {
        public string TankId;
        public string DisplayName;
        public string DisplayNameEn;
        public string DisplayNameZhCn;
        public string ContentStatus;
        public double InnerLengthCm;
        public double InnerWidthCm;
        public double InnerHeightCm;
        public double GrossVolumeL;
        public double EffectiveVolumeLDesign;
        public string WaterType;
        public double GameBioloadCapacity;
        public string SimulationModelId;
        public bool SimulationReady;
        public string Notes;
    }

    [Serializable]
    public sealed class ItemConfig
    {
        public string ItemId;
        public string DisplayName;
        public string DisplayNameEn;
        public string DisplayNameZhCn;
        public string ItemKind;
        public string ContentStatus;
        public string SpeciesId;
        public string UnlockGateId;
        public int PurchasePriceCoin;
        public int UnitsPerPurchase;
        public string UnitName;
        public int PostConfirmSellPricePerPurchaseCoin;
        public double PreFirstConfirmRefundRatio;
        public double GameFilterCapacity;
        public string GameLightOutputClass;
        public string RouteRole;
        public string Notes;
    }

    [Serializable]
    public sealed class SpeciesConfig
    {
        public string SpeciesId;
        public string DisplayName;
        public string DisplayNameEn;
        public string DisplayNameZhCn;
        public string ScientificName;
        public string ActorKind;
        public string ContentStatus;
        public string InclusionScope;
        public string WaterType;
        public string BehaviorProfileId;
        public double GameBioloadPerIndividual;
        public int GameSchoolMin;
        public int GameSchoolMax;
        public string GameLightDemandClass;
        public string SourceStatus;
        public string EcologyReference;
        public string Notes;
        public string SourceNameEn;
        public string SourceId;
        public string Aliases;
        public string SourceVerification;
    }

    [Serializable]
    public sealed class PlantConfig
    {
        public string Id;
        public string DisplayNameEn;
        public string DisplayNameZhCn;
        public string ScientificName;
        public string SourceNameEn;
        public string Aliases;
        public string PlacementTags;
        public string GrowthForm;
        public string PlantingMethod;
        public string AllowedSurfaces;
        public string RhizomeExposed;
        public string LightRequirement;
        public string Co2Requirement;
        public string GrowthSpeed;
        public string ContentStatus;
        public string SourceId;
        public string SourceVerification;
    }

    [Serializable]
    public sealed class BehaviorConfig
    {
        public string BehaviorProfileId;
        public string ContentStatus;
        public string ActorKind;
        public string TimeDomain;
        public double StartleSensitivity01;
        public double RecoveryMinRealS;
        public double RecoveryMaxRealS;
        public double ApproachProbabilityMin01;
        public double ApproachProbabilityMax01;
        public double ApproachCooldownRealS;
        public double MinBehaviorRealS;
        public bool SupportsSchooling;
        public string ParameterBasis;
        public string Notes;
    }

    [Serializable]
    public sealed class GateConfig
    {
        public string GateId;
        public string ContentStatus;
        public string PrerequisiteGateId;
        public string EventKey;
        public bool RequiresWetTank;
        public double MinStableGameHours;
        public string ConditionContractStatus;
        public string Notes;
    }

    [Serializable]
    public sealed class EconomyConfig
    {
        public string EconomyId;
        public string ContentStatus;
        public string EventKey;
        public int AmountCoin;
        public bool Repeatable;
        public string RequiredGateId;
        public string Policy;
        public string Notes;
    }

    [Serializable]
    public sealed class RouteConfig
    {
        public string RouteId;
        public int StepIndex;
        public string Action;
        public string ItemId;
        public int PurchaseCount;
        public string EconomyId;
        public string RequiredGateId;
        public string CompletesGateId;
        public bool RequiredForV0;
        public string Notes;
    }

    [Serializable]
    public sealed class CompatibilityConfig
    {
        public string RuleId;
        public string ContentStatus;
        public string SubjectKind;
        public string SubjectId;
        public string ObjectKind;
        public string ObjectId;
        public string Decision;
        public string SourceStatus;
        public string Notes;
    }
}
