using System;
using System.Collections.Generic;

namespace Aquarium.Core
{
    [Serializable]
    public sealed class AgentState
    {
        public string EntityId, SpeciesId;
        public uint RngState;
        public Personality Personality;
        public Vec3 Position, Velocity, Target, GlassTarget;
        public double Hunger, Stress, MinActionRemaining, NextDecision, NextTargetRefresh;
        public double ApproachMemory, GlassCooldown, LightHabituation, StartleDelay = -1;
        public double SecondsSinceLightChange = 1000000, SafeLightSeconds, LightBurstLoad;
        public ActionKind Action;
        public long LastLightId, LastGlassId, LastFoodId;
        public double NextProbe;
        public int ActionChanges, Meals, Startles, GlassApproaches, ProbeBursts, GrazeDwells;
        public AgentState Clone() { return (AgentState)MemberwiseClone(); }
        public void Validate(TankBounds tank, SpeciesProfile profile)
        {
            if (string.IsNullOrEmpty(EntityId) || SpeciesId != profile.Id || RngState == 0 ||
                !Scalar.IsFinite(Position) || !Scalar.IsFinite(Velocity) || !Scalar.IsFinite(Target) || !Scalar.IsFinite(GlassTarget) ||
                !Scalar.IsFinite(Hunger) || !Scalar.IsFinite(Stress) || !Scalar.IsFinite(MinActionRemaining) ||
                !Scalar.IsFinite(NextDecision) || !Scalar.IsFinite(NextTargetRefresh) || !Scalar.IsFinite(NextProbe) ||
                !Scalar.IsFinite(ApproachMemory) || !Scalar.IsFinite(GlassCooldown) || !Scalar.IsFinite(LightHabituation) ||
                !Scalar.IsFinite(StartleDelay) || !Scalar.IsFinite(SecondsSinceLightChange) ||
                !Scalar.IsFinite(SafeLightSeconds) || !Scalar.IsFinite(LightBurstLoad) ||
                !Scalar.IsFinite(Personality.Boldness) || !Scalar.IsFinite(Personality.Curiosity) ||
                !Scalar.IsFinite(Personality.Sociality) || !Scalar.IsFinite(Personality.Activity) || !Scalar.IsFinite(Personality.FoodDrive))
                throw new ArgumentException("Agent state contains invalid identity or non-finite values.");
            if (Hunger < 0 || Hunger > 1 || Stress < 0 || Stress > 1 || LightHabituation < 0 ||
                LightHabituation > AquariumWorld.MaximumLightHabituation || LightBurstLoad < 0 || LightBurstLoad > 1 ||
                SecondsSinceLightChange < 0 || SafeLightSeconds < 0 || ApproachMemory < 0 || GlassCooldown < 0 ||
                StartleDelay < -1 || Personality.Boldness <= 0 || Personality.Boldness > 10 ||
                Personality.Curiosity <= 0 || Personality.Curiosity > 10 || Personality.Sociality <= 0 || Personality.Sociality > 10 ||
                Personality.Activity <= 0 || Personality.Activity > 10 || Personality.FoodDrive <= 0 || Personality.FoodDrive > 10 ||
                !Enum.IsDefined(typeof(ActionKind), Action) || !tank.Contains(Position, profile) || !tank.Contains(Target, profile))
                throw new ArgumentException("Agent state violates behaviour ranges or tank/swim-layer bounds.");
        }
    }

    [Serializable]
    public sealed class StimulusState
    {
        public long Id;
        public StimulusKind Kind;
        public Vec3 Position;
        public double Strength, Remaining;
        public int Portions;
        public StimulusState Clone() { return (StimulusState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class WorldSnapshot
    {
        public int SchemaVersion = 2;
        public long NextEventId;
        public double RealSeconds, EcologyDays, EcologyRate, FixedAccumulator;
        public bool LightIsOn;
        public AgentState[] Agents;
        public StimulusState[] Stimuli;
    }

    public sealed class AquariumAgent
    {
        public readonly SpeciesProfile Profile;
        public readonly AgentState State;
        internal SavedRandom Random;

        public AquariumAgent(SpeciesProfile profile, string id, uint seed, TankBounds tank)
        {
            profile.Validate(tank);
            Profile = profile.Clone();
            Random = new SavedRandom(seed);
            State = new AgentState { EntityId = id, SpeciesId = profile.Id,
                Personality = Personality.Create(ref Random), Action = ActionKind.Roam,
                Hunger = Random.Range(0.25, 0.65), NextDecision = Random.Range(0.1, 1.0) };
            State.Position = tank.Constrain(new Vec3(Random.Range(-tank.Width / 2, tank.Width / 2),
                Random.Range(tank.Height * profile.SwimLayerMin, tank.Height * profile.SwimLayerMax),
                Random.Range(-tank.Depth / 2, tank.Depth / 2)), profile);
            State.Target = State.Position;
            State.RngState = Random.State;
        }

        internal AquariumAgent(SpeciesProfile profile, AgentState state, TankBounds tank)
        {
            profile.Validate(tank); Profile = profile.Clone(); State = state.Clone();
            State.Validate(tank, Profile);
            Random = new SavedRandom(State.RngState);
        }
    }
}
