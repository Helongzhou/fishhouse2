using System;

namespace Aquarium.Core
{
    public enum AgentKind { Fish, Shrimp }
    public enum ActionKind { Roam, Forage, Shoal, Hide, ApproachGlass, SeekFood, Graze, Rest, Flee, BottomProbe }
    public enum StimulusKind { LightOn, GlassApproach, Food, LightOff }

    [Serializable]
    public sealed class SpeciesProfile
    {
        public string Id;
        public AgentKind Kind;
        public string[] Tags = new string[0];
        public double SwimLayerMin = 0.15, SwimLayerMax = 0.90;
        public double PreferredSpeed = 0.035, BodyRadius = 0.008;
        public double FearSensitivity = 0.65, Curiosity = 0.55, ShoalAffinity = 0.70;
        public double HungerRate = 0.002;
        public bool IsolationOnly;
        public bool HasTag(string tag) { return Array.IndexOf(Tags, tag) >= 0; }
        public void Validate(TankBounds tank)
        {
            if (tank == null) throw new ArgumentNullException("tank");
            tank.Validate();
            if (string.IsNullOrEmpty(Id)) throw new ArgumentException("Species ID is required.");
            if (Tags == null || !Enum.IsDefined(typeof(AgentKind), Kind)) throw new ArgumentException("Species tags and kind are invalid.");
            if (!Scalar.IsFinite(PreferredSpeed) || !Scalar.IsFinite(BodyRadius) || !Scalar.IsFinite(HungerRate) ||
                PreferredSpeed <= 0 || PreferredSpeed > 10 || BodyRadius <= 0 || HungerRate < 0 || HungerRate > 1)
                throw new ArgumentException("Invalid finite speed, radius or hunger rate.");
            if (!Scalar.IsFinite(FearSensitivity) || !Scalar.IsFinite(Curiosity) || !Scalar.IsFinite(ShoalAffinity) ||
                FearSensitivity < 0 || FearSensitivity > 1 || Curiosity < 0 || Curiosity > 1 || ShoalAffinity < 0 || ShoalAffinity > 1)
                throw new ArgumentException("Behaviour probabilities must be finite values in 0..1.");
            if (!Scalar.IsFinite(SwimLayerMin) || !Scalar.IsFinite(SwimLayerMax)) throw new ArgumentException("Swim layers must be finite.");
            if (SwimLayerMin < 0 || SwimLayerMax > 1 || SwimLayerMin >= SwimLayerMax ||
                (SwimLayerMax - SwimLayerMin) * tank.Height <= 2 * BodyRadius ||
                tank.Width <= 2 * BodyRadius || tank.Depth <= 2 * BodyRadius)
                throw new ArgumentException("Swim layer must fit body radius in tank bounds.");
        }
        internal SpeciesProfile Clone()
        {
            SpeciesProfile copy = (SpeciesProfile)MemberwiseClone();
            copy.Tags = (string[])Tags.Clone();
            return copy;
        }
    }

    public static class DemoProfiles
    {
        public static SpeciesProfile CommunityFish()
        {
            return new SpeciesProfile { Id = "neon_tetra_demo", Tags = new[] { "shoaling" },
                FearSensitivity = 0.68, Curiosity = 0.64, PreferredSpeed = 0.040,
                SwimLayerMin = .25, SwimLayerMax = .80 };
        }
        public static SpeciesProfile Shrimp()
        {
            return new SpeciesProfile { Id = "neocaridina_demo", Kind = AgentKind.Shrimp,
                Tags = new[] { "surface_grazer" }, SwimLayerMin = 0, SwimLayerMax = 0.06,
                PreferredSpeed = 0.012, BodyRadius = 0.004, FearSensitivity = 0.82,
                Curiosity = 0.24, ShoalAffinity = 0.12, HungerRate = 0.001 };
        }
        // Panda corydoras are bottom sifters; this is not a pygmy-cory midwater profile.
        public static SpeciesProfile BottomSifter()
        {
            return new SpeciesProfile { Id = "panda_corydoras_demo", Tags = new[] { "bottom_sifter" },
                SwimLayerMin = 0.01, SwimLayerMax = 0.20, BodyRadius = 0.009,
                PreferredSpeed = .025, ShoalAffinity = .45, Curiosity = .35 };
        }
        // These are fixtures for isolated behaviour tests, not community stocking advice.
        public static SpeciesProfile FinNipper()
        {
            return new SpeciesProfile { Id = "tiger_barb_isolated_fixture", Tags = new[] { "fin_nipper" },
                PreferredSpeed = 0.055, IsolationOnly = true };
        }
        public static SpeciesProfile CalmWater()
        {
            return new SpeciesProfile { Id = "angelfish_isolated_fixture", Tags = new[] { "calm_water" },
                BodyRadius = 0.020, PreferredSpeed = 0.028, IsolationOnly = true };
        }
        public static SpeciesProfile TerritorialMale()
        {
            return new SpeciesProfile { Id = "betta_male_isolated_fixture", Tags = new[] { "territorial_male" },
                ShoalAffinity = 0.02, PreferredSpeed = 0.025, IsolationOnly = true };
        }
    }

    [Serializable]
    public struct Personality
    {
        public double Boldness, Curiosity, Sociality, Activity, FoodDrive;
        public static Personality Create(ref SavedRandom random)
        {
            return new Personality { Boldness = random.Range(0.65, 1.35), Curiosity = random.Range(0.65, 1.35),
                Sociality = random.Range(0.65, 1.35), Activity = random.Range(0.70, 1.30), FoodDrive = random.Range(0.7, 1.3) };
        }
    }
}
