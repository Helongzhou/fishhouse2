using System;
using System.Collections.Generic;

namespace Aquarium.Core
{
    // Pure C#. Distances are metres and agent time is always real seconds.
    public sealed class AquariumWorld
    {
        public const double FixedStep = 1.0 / 30.0;
        public const double LightBurstWindowSeconds = 8;
        public const double SafeLightWindowSeconds = 30;
        public const double MaximumLightHabituation = .35;
        private readonly List<AquariumAgent> agents = new List<AquariumAgent>();
        private readonly List<StimulusState> stimuli = new List<StimulusState>();
        private long nextEventId = 1;
        private double accumulator;
        public readonly TankBounds Tank;
        public IReadOnlyList<AquariumAgent> Agents { get { return agents; } }
        public IReadOnlyList<StimulusState> Stimuli { get { return stimuli; } }
        public bool LightIsOn { get; private set; }
        public double RealSeconds { get; private set; }
        public double EcologyDays { get; private set; }
        public double EcologyRate { get; private set; } = 1;
        public readonly bool AllowIsolationFixtures;
        // IDs are the saved stimulus IDs. Subscribers stage cross-domain accounting on a candidate world.
        public event Action<long, string, int> FoodConsumed;
        public event Action<long, int, string> FoodRemoved;
        public AquariumWorld(TankBounds tank, bool allowIsolationFixtures = false)
        {
            if (tank == null) throw new ArgumentNullException("tank");
            tank.Validate(); Tank = tank.Clone(); AllowIsolationFixtures = allowIsolationFixtures;
        }

        public void AddAgent(SpeciesProfile profile, string id, uint seed)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            profile.Validate(Tank);
            if (profile.IsolationOnly && !AllowIsolationFixtures)
                throw new InvalidOperationException("Isolation fixture cannot be stocked in the community demo.");
            if (string.IsNullOrEmpty(id) || agents.Exists(a => a.State.EntityId == id))
                throw new ArgumentException("Agent IDs must be unique and nonempty.");
            agents.Add(new AquariumAgent(profile, id, seed, Tank));
        }
        public void SetEcologyRate(double rate)
        {
            if (rate != 0 && rate != 1 && rate != 2 && rate != 4) throw new ArgumentOutOfRangeException("rate");
            EcologyRate = rate;
        }
        public void SetLight(bool on)
        {
            if (on == LightIsOn) return;
            AddStimulus(on ? StimulusKind.LightOn : StimulusKind.LightOff, new Vec3(0, Tank.Height, 0), on ? 1 : .35, 1.5, 0);
            LightIsOn = on;
        }
        public void ApproachGlass(Vec3 position)
        {
            Scalar.RequireFinite(position, "position");
            // This is a game glass-proximity proxy, not a biological claim about camera tracking.
            position = new Vec3(Scalar.Clamp(position.X, -Tank.Width / 2, Tank.Width / 2),
                Scalar.Clamp(position.Y, 0, Tank.Height), Scalar.Clamp(position.Z, -Tank.Depth / 2, Tank.Depth / 2));
            AddStimulus(StimulusKind.GlassApproach, position, 1, 1.0, 0);
        }
        public long Feed(Vec3 position, int portions = 12)
        {
            Scalar.RequireFinite(position, "position");
            if (portions <= 0) throw new ArgumentOutOfRangeException("portions");
            position = new Vec3(Scalar.Clamp(position.X, -Tank.Width / 2 + .02, Tank.Width / 2 - .02),
                Scalar.Clamp(position.Y, .01, Tank.Height - .01),
                Scalar.Clamp(position.Z, -Tank.Depth / 2 + .02, Tank.Depth / 2 - .02));
            return AddStimulus(StimulusKind.Food, position, 1, 35, portions);
        }
        private long AddStimulus(StimulusKind kind, Vec3 position, double strength, double lifetime, int portions)
        {
            // UI spam must not grow an unbounded event list.
            if (stimuli.Count >= 64)
            {
                StimulusState evicted = stimuli[0];
                if (evicted.Kind == StimulusKind.Food && evicted.Portions > 0 && FoodRemoved != null)
                    FoodRemoved(evicted.Id, evicted.Portions, "capacity_evicted");
                stimuli.RemoveAt(0);
            }
            long id = nextEventId++;
            stimuli.Add(new StimulusState { Id = id, Kind = kind, Position = position,
                Strength = strength, Remaining = lifetime, Portions = portions });
            return id;
        }

        public void Advance(double realSeconds)
        {
            if (double.IsNaN(realSeconds) || double.IsInfinity(realSeconds) || realSeconds < 0 || realSeconds > 60)
                throw new ArgumentOutOfRangeException("realSeconds", "Advance supports 0..60 real seconds per call.");
            ValidateRuntime();
            accumulator += realSeconds;
            while (accumulator + 1e-12 >= FixedStep)
            {
                Tick(FixedStep);
                accumulator -= FixedStep;
                if (accumulator < 0) accumulator = 0;
            }
        }

        private void Tick(double dt)
        {
            RealSeconds += dt;
            EcologyDays += dt * EcologyRate / 480.0;
            // Food settles in real time, producing a moving rather than fixed attraction point.
            for (int i = 0; i < stimuli.Count; i++)
                if (stimuli[i].Kind == StimulusKind.Food)
                    stimuli[i].Position.Y = Math.Max(.012, stimuli[i].Position.Y - .004 * dt);
            for (int i = 0; i < agents.Count; i++) UpdateAgent(agents[i], dt);
            for (int i = stimuli.Count - 1; i >= 0; i--)
            {
                stimuli[i].Remaining -= dt;
                if (stimuli[i].Remaining <= 0 || (stimuli[i].Kind == StimulusKind.Food && stimuli[i].Portions <= 0))
                {
                    if (stimuli[i].Kind == StimulusKind.Food && stimuli[i].Portions > 0 && FoodRemoved != null)
                        FoodRemoved(stimuli[i].Id, stimuli[i].Portions, "expired");
                    stimuli.RemoveAt(i);
                }
            }
        }

        private void UpdateAgent(AquariumAgent agent, double dt)
        {
            AgentState s = agent.State;
            s.Hunger = Scalar.Clamp01(s.Hunger + agent.Profile.HungerRate * dt);
            s.Stress = Scalar.Clamp01(s.Stress - .045 * dt);
            s.ApproachMemory = Math.Max(0, s.ApproachMemory - dt);
            s.GlassCooldown = Math.Max(0, s.GlassCooldown - dt);
            s.MinActionRemaining -= dt; s.NextDecision -= dt; s.NextTargetRefresh -= dt; s.NextProbe -= dt;
            Perceive(agent);
            UpdateLightMemory(agent, dt);
            if (s.StartleDelay >= 0)
            {
                s.StartleDelay -= dt;
                if (s.StartleDelay <= 0)
                {
                    // A strong new stimulus can interrupt even the minimum action commitment.
                    BeginAction(agent, ActionKind.Flee);
                    s.StartleDelay = -1;
                    s.Startles++;
                }
            }
            if (s.MinActionRemaining <= 0 && s.NextDecision <= 0)
                BeginAction(agent, SelectAction(agent));
            if (s.NextTargetRefresh <= 0 && s.Action != ActionKind.Flee &&
                s.Action != ActionKind.BottomProbe && s.Action != ActionKind.Graze)
            {
                s.Target = TargetFor(agent, s.Action);
                s.NextTargetRefresh = agent.Random.Range(.8, 2.8);
            }
            Move(agent, dt);
            if ((s.Action == ActionKind.BottomProbe || s.Action == ActionKind.Graze) &&
                Vec3.Distance(s.Position, s.Target) < .015 && s.NextProbe <= 0)
            {
                if (s.Action == ActionKind.BottomProbe) s.ProbeBursts++; else s.GrazeDwells++;
                s.NextProbe = agent.Random.Range(.55, 1.4);
            }
            StimulusState food = ClosestFood(s.Position);
            if (food != null && s.Action == ActionKind.SeekFood && Vec3.Distance(s.Position, food.Position) < .022 && s.Hunger > .08)
            {
                if (FoodConsumed != null) FoodConsumed(food.Id, s.EntityId, 1);
                food.Portions--; s.Hunger = Math.Max(0, s.Hunger - .28); s.Meals++;
            }
            s.RngState = agent.Random.State;
        }

        private void Perceive(AquariumAgent a)
        {
            AgentState s = a.State;
            for (int i = 0; i < stimuli.Count; i++)
            {
                StimulusState e = stimuli[i];
                if ((e.Kind == StimulusKind.LightOn || e.Kind == StimulusKind.LightOff) && e.Id > s.LastLightId)
                {
                    s.LastLightId = e.Id;
                    bool rapidChange = s.SecondsSinceLightChange < LightBurstWindowSeconds;
                    if (rapidChange) s.LightBurstLoad = Scalar.Clamp01(s.LightBurstLoad + .25);
                    s.SecondsSinceLightChange = 0;
                    s.SafeLightSeconds = 0;
                    double fear = Scalar.Clamp01(a.Profile.FearSensitivity / s.Personality.Boldness);
                    // Long safe exposure has a small bounded effect. Rapid changes increase pressure;
                    // pressing the light key repeatedly never grants fast habituation.
                    double response = Scalar.Clamp01(fear * e.Strength * (1 - .4 * s.LightHabituation) * (1 + .35 * s.LightBurstLoad));
                    s.Stress = Scalar.Clamp01(s.Stress + response * .65 + (rapidChange ? .12 * s.LightBurstLoad : 0));
                    if (a.Random.Chance(response) && s.StartleDelay < 0)
                        s.StartleDelay = a.Random.Range(.05, .65);
                }
                else if (e.Kind == StimulusKind.GlassApproach && e.Id > s.LastGlassId)
                {
                    s.LastGlassId = e.Id;
                    if (s.GlassCooldown <= 0 && a.Profile.Kind == AgentKind.Fish && !a.Profile.HasTag("surface_grazer"))
                    {
                        double chance = a.Profile.Curiosity * s.Personality.Curiosity * (.35 + s.Hunger * .65) * (1 - s.Stress);
                        if (a.Random.Chance(chance))
                        { s.ApproachMemory = a.Random.Range(1.2, 3.2); s.GlassTarget = e.Position; }
                        s.GlassCooldown = a.Random.Range(5, 10);
                    }
                }
                else if (e.Kind == StimulusKind.Food && e.Id > s.LastFoodId)
                { s.LastFoodId = e.Id; s.NextDecision = Math.Min(s.NextDecision, a.Random.Range(.05, 1)); }
            }
        }

        private void UpdateLightMemory(AquariumAgent a, double dt)
        {
            AgentState s = a.State;
            s.SecondsSinceLightChange = Math.Min(1000000, s.SecondsSinceLightChange + dt);
            s.LightBurstLoad = Math.Max(0, s.LightBurstLoad - .02 * dt);
            bool safe = LightIsOn && s.SecondsSinceLightChange >= SafeLightWindowSeconds &&
                s.Stress <= .15 && s.StartleDelay < 0 && s.Action != ActionKind.Flee && s.LightBurstLoad <= .05;
            s.SafeLightSeconds = safe ? Math.Min(300, s.SafeLightSeconds + dt) : 0;
            if (s.SafeLightSeconds >= SafeLightWindowSeconds)
                s.LightHabituation = Math.Min(MaximumLightHabituation, s.LightHabituation + .0015 * dt);
            else if (!LightIsOn)
                s.LightHabituation = Math.Max(0, s.LightHabituation - .0001 * dt);
        }

        private bool HasConspecific(AquariumAgent a)
        {
            for (int i = 0; i < agents.Count; i++)
                if (agents[i] != a && agents[i].Profile.Id == a.Profile.Id) return true;
            return false;
        }

        private ActionKind SelectAction(AquariumAgent a)
        {
            AgentState s = a.State;
            ActionKind[] options = { ActionKind.Roam, ActionKind.Forage, ActionKind.Shoal, ActionKind.Hide,
                ActionKind.ApproachGlass, ActionKind.SeekFood, ActionKind.Graze, ActionKind.Rest };
            if (a.Profile.HasTag("bottom_sifter")) options[1] = ActionKind.BottomProbe;
            double[] weights = {
                .65 * s.Personality.Activity * (1 - .7 * s.Stress),
                (.16 + s.Hunger * .8) * (a.Profile.HasTag("bottom_sifter") ? 2.0 : 1.0),
                a.Profile.ShoalAffinity * s.Personality.Sociality * (HasConspecific(a) ? .7 : 0),
                s.Stress * s.Stress * 4,
                s.ApproachMemory > 0 && a.Profile.Kind == AgentKind.Fish && !a.Profile.HasTag("surface_grazer")
                    ? 2.8 * a.Profile.Curiosity * (1 - s.Stress) : 0,
                ClosestFood(s.Position) != null ? s.Hunger * 7 * s.Personality.FoodDrive * (1 - .5 * s.Stress) : 0,
                a.Profile.HasTag("surface_grazer") ? 1.2 + .5 * s.Hunger : 0,
                .12 / s.Personality.Activity
            };
            double sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                // A current action has a modest hysteresis bias, never an absolute priority lock.
                weights[i] *= a.Random.Range(.70, 1.30) * (options[i] == s.Action ? 1.6 : 1);
                sum += weights[i];
            }
            double ticket = a.Random.Range(0, sum);
            for (int i = 0; i < weights.Length; i++)
            { if (weights[i] <= 0) continue; ticket -= weights[i]; if (ticket <= 0) return options[i]; }
            return ActionKind.Roam;
        }

        private void BeginAction(AquariumAgent a, ActionKind action)
        {
            AgentState s = a.State;
            if (s.Action != action) s.ActionChanges++;
            s.Action = action;
            s.MinActionRemaining = action == ActionKind.Flee ? a.Random.Range(.45, 1.2) : a.Random.Range(1.2, 3.8);
            if (action == ActionKind.BottomProbe || action == ActionKind.Graze)
                s.MinActionRemaining = a.Random.Range(3.5, 7.0);
            if (action == ActionKind.ApproachGlass)
            { s.MinActionRemaining = Math.Min(s.MinActionRemaining, Math.Max(.3, s.ApproachMemory)); s.GlassApproaches++; }
            s.NextDecision = s.MinActionRemaining + a.Random.Range(.15, 1.2);
            s.NextTargetRefresh = a.Random.Range(.7, 2.0);
            s.Target = TargetFor(a, action);
        }

        private Vec3 TargetFor(AquariumAgent a, ActionKind action)
        {
            Vec3 p = a.State.Position;
            if (action == ActionKind.SeekFood)
            { StimulusState food = ClosestFood(p); if (food != null) return Tank.Constrain(food.Position, a.Profile); }
            if (action == ActionKind.ApproachGlass && a.State.ApproachMemory > 0)
                return Tank.Constrain(a.State.GlassTarget + new Vec3(a.Random.Range(-.08, .08),
                    a.Random.Range(-.03, .03), .008), a.Profile);
            if (action == ActionKind.Hide || action == ActionKind.Flee)
            {
                // Placeholder cover anchors. Adapter can replace these with hardscape cover sampling later.
                return Tank.Constrain(new Vec3(p.X >= 0 ? Tank.Width * .32 : -Tank.Width * .32,
                    Tank.Height * a.Profile.SwimLayerMin + .025, Tank.Depth * .30) +
                    new Vec3(a.Random.Range(-.025, .025), a.Random.Range(-.012, .012), a.Random.Range(-.02, .02)), a.Profile);
            }
            if (action == ActionKind.Shoal)
            {
                AquariumAgent nearest = null; double best = double.MaxValue;
                for (int i = 0; i < agents.Count; i++)
                {
                    AquariumAgent other = agents[i];
                    if (other == a || other.Profile.Id != a.Profile.Id) continue;
                    double distance = Vec3.Distance(p, other.State.Position);
                    if (distance < best) { best = distance; nearest = other; }
                }
                if (nearest != null)
                    return Tank.Constrain(nearest.State.Position + new Vec3(a.Random.Range(-.06, .06),
                        a.Random.Range(-.025, .025), a.Random.Range(-.05, .05)), a.Profile);
            }
            if (action == ActionKind.Rest) return p;
            double y = p.Y + a.Random.Range(-.07, .07);
            if (action == ActionKind.Graze || action == ActionKind.BottomProbe)
                y = Tank.Height * a.Profile.SwimLayerMin + a.Profile.BodyRadius + a.Random.Range(.001, .008);
            double radius = action == ActionKind.Graze ? .025 : action == ActionKind.BottomProbe ? .045 : .15;
            return Tank.Constrain(new Vec3(p.X + a.Random.Range(-radius, radius), y,
                p.Z + a.Random.Range(-radius * .7, radius * .7)), a.Profile);
        }

        private void Move(AquariumAgent a, double dt)
        {
            AgentState s = a.State;
            Vec3 direction = s.Target - s.Position;
            Vec3 separation = new Vec3();
            for (int i = 0; i < agents.Count; i++)
            {
                if (agents[i] == a) continue;
                Vec3 delta = s.Position - agents[i].State.Position; double d = delta.Length;
                if (d > .0001 && d < .025) separation += delta.Normalized * ((.025 - d) / .025);
            }
            double multiplier = s.Action == ActionKind.Flee ? 2.8 : s.Action == ActionKind.Rest ? .12 :
                s.Action == ActionKind.Graze || s.Action == ActionKind.Hide ? .35 : 1;
            double speed = a.Profile.PreferredSpeed * s.Personality.Activity * multiplier;
            if ((s.Action == ActionKind.BottomProbe || s.Action == ActionKind.Graze) && direction.Length < .012)
                speed = 0;
            Vec3 desired = (direction.Normalized + separation * .65).Normalized * Math.Min(speed, direction.Length / dt);
            s.Velocity = Vec3.Lerp(s.Velocity, desired, 1 - Math.Exp(-dt * (s.Action == ActionKind.Flee ? 12 : 4)));
            s.Position = Tank.Constrain(s.Position + s.Velocity * dt, a.Profile);
        }

        private StimulusState ClosestFood(Vec3 position)
        {
            StimulusState best = null; double distance = double.MaxValue;
            for (int i = 0; i < stimuli.Count; i++)
            {
                StimulusState e = stimuli[i]; if (e.Kind != StimulusKind.Food || e.Portions <= 0) continue;
                double d = Vec3.Distance(position, e.Position);
                if (d < distance) { distance = d; best = e; }
            }
            return best;
        }

        public WorldSnapshot Capture()
        {
            ValidateRuntime();
            WorldSnapshot save = new WorldSnapshot { NextEventId = nextEventId, RealSeconds = RealSeconds,
                EcologyDays = EcologyDays, EcologyRate = EcologyRate, FixedAccumulator = accumulator,
                LightIsOn = LightIsOn, Agents = new AgentState[agents.Count], Stimuli = new StimulusState[stimuli.Count] };
            for (int i = 0; i < agents.Count; i++) save.Agents[i] = agents[i].State.Clone();
            for (int i = 0; i < stimuli.Count; i++) save.Stimuli[i] = stimuli[i].Clone();
            return save;
        }
        public static AquariumWorld Restore(WorldSnapshot save, TankBounds tank, IDictionary<string, SpeciesProfile> profiles,
            bool allowIsolationFixtures = false)
        {
            if (save == null || save.SchemaVersion != 2 || save.Agents == null || save.Stimuli == null || profiles == null)
                throw new ArgumentException("Unsupported or incomplete behaviour snapshot.");
            if (!Scalar.IsFinite(save.RealSeconds) || !Scalar.IsFinite(save.EcologyDays) || !Scalar.IsFinite(save.FixedAccumulator) ||
                save.RealSeconds < 0 || save.EcologyDays < 0 || save.FixedAccumulator < 0 || save.FixedAccumulator >= FixedStep || save.NextEventId < 1)
                throw new ArgumentException("Snapshot clocks and sequence values are invalid.");
            AquariumWorld world = new AquariumWorld(tank, allowIsolationFixtures);
            world.SetEcologyRate(save.EcologyRate);
            world.RealSeconds = save.RealSeconds; world.EcologyDays = save.EcologyDays;
            world.accumulator = save.FixedAccumulator; world.LightIsOn = save.LightIsOn; world.nextEventId = save.NextEventId;
            HashSet<string> ids = new HashSet<string>();
            foreach (AgentState state in save.Agents)
            {
                if (state == null || string.IsNullOrEmpty(state.SpeciesId)) throw new ArgumentException("Missing saved agent.");
                SpeciesProfile profile;
                if (!profiles.TryGetValue(state.SpeciesId, out profile)) throw new ArgumentException("Unknown species: " + state.SpeciesId);
                if (!ids.Add(state.EntityId)) throw new ArgumentException("Duplicate agent ID in snapshot.");
                if (profile.IsolationOnly && !allowIsolationFixtures) throw new ArgumentException("Isolation fixture not allowed.");
                world.agents.Add(new AquariumAgent(profile, state, tank));
            }
            foreach (StimulusState stimulus in save.Stimuli)
            {
                if (stimulus == null) throw new ArgumentException("Missing saved stimulus.");
                world.stimuli.Add(stimulus.Clone());
            }
            world.ValidateRuntime();
            return world;
        }

        private void ValidateRuntime()
        {
            Tank.Validate();
            if (!Scalar.IsFinite(RealSeconds) || !Scalar.IsFinite(EcologyDays) || !Scalar.IsFinite(accumulator))
                throw new ArgumentException("Runtime clocks must be finite.");
            for (int i = 0; i < agents.Count; i++)
            {
                agents[i].Profile.Validate(Tank);
                agents[i].State.Validate(Tank, agents[i].Profile);
            }
            for (int i = 0; i < stimuli.Count; i++)
            {
                StimulusState s = stimuli[i];
                if (!Scalar.IsFinite(s.Position) || !Scalar.IsFinite(s.Strength) || !Scalar.IsFinite(s.Remaining) ||
                    s.Strength < 0 || s.Strength > 1 || s.Remaining <= 0 || s.Remaining > 35 ||
                    s.Id < 1 || s.Id >= nextEventId || s.Portions < 0 || !Enum.IsDefined(typeof(StimulusKind), s.Kind))
                    throw new ArgumentException("Stimulus contains invalid or non-finite values.");
            }
        }
    }
}
