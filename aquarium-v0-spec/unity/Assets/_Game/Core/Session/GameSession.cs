using System;
using System.Collections.Generic;
using System.Globalization;
using Aquarium.Core.Configuration;
using Aquarium.Core.Ecology;
using Aquarium.Core.Gameplay;

namespace Aquarium.Core.Session
{
    public sealed class GameSession
    {
        public const double FoodMgNPerPortion = .12; // Prototype game tuning; no real feeding guidance.
        private readonly RuntimeCatalog catalog;
        private readonly GameplayCatalog gameplayCatalog;
        private readonly Dictionary<string, SpeciesProfile> profiles;
        private readonly SessionMode mode;
        private readonly Dictionary<long, FoodBinding> food = new Dictionary<long, FoodBinding>();
        private readonly List<SessionReceipt> receipts = new List<SessionReceipt>();
        private SessionStateSnapshot checkpoint;
        private LivingReserveLedger reserves = new LivingReserveLedger();
        private double ecologyRate = 1;
        public AquariumWorld World { get; private set; }
        public GameplaySession Gameplay { get; private set; }
        public EcologyEngine Ecology { get; private set; }
        public SessionMode Mode { get { return mode; } }
        public double EcologyRate { get { return ecologyRate; } }
        public bool Paused { get { return Gameplay.Paused; } }
        public GameSession(RuntimeCatalog catalog, SessionMode mode = SessionMode.Prototype)
        {
            if (catalog == null) throw new ArgumentNullException("catalog"); catalog.Validate();
            this.catalog = catalog; this.mode = mode; gameplayCatalog = catalog.CreateGameplayCatalog(); profiles = catalog.CreateBehaviorProfiles();
            Gameplay = new GameplaySession(gameplayCatalog, catalog.CreateTank());
            World = new AquariumWorld(catalog.CreateTank()); World.SetEcologyRate(0);
            Ecology = new EcologyEngine(CreateEcologyParameters(catalog.Tanks[0].EffectiveVolumeLDesign));
            SubscribeFood();
        }
        private EcologyParameters CreateEcologyParameters(double volume)
        {
            double stable=-1; foreach(GateConfig gate in catalog.Gates) if(gate.GateId=="gate_cycle"||gate.GateId=="gate_mainfish")
            { if(stable>=0 && stable!=gate.MinStableGameHours)throw new ArgumentException("This prototype requires equal cycle and observation stability windows."); stable=gate.MinStableGameHours; }
            if(stable<=0)throw new ArgumentException("Missing configured ecological stability windows.");
            return new EcologyParameters {VolumeLiters=volume,StableGameHours=stable};
        }
        private void SubscribeFood()
        {
            World.FoodConsumed += OnFoodConsumed;
            World.FoodRemoved += OnFoodRemoved;
        }
        private void OnFoodConsumed(long source, string entity, int portions) { double before=Ecology.State.LivingMgN; MoveFood(source, portions, NitrogenPool.Living, "ingested:" + source); reserves.Record(entity,Ecology.State.LivingMgN-before); }
        private void OnFoodRemoved(long source, int portions, string reason) { MoveFood(source, portions, NitrogenPool.Organic, reason + ":" + source); }
        private void MoveFood(long source, int portions, NitrogenPool to, string reason)
        {
            FoodBinding binding;
            if (!food.TryGetValue(source, out binding) || portions <= 0 || binding.RemainingPortions < portions)
                throw new InvalidOperationException("Behavior food event does not match its nitrogen binding.");
            double mass = portions * binding.MgNPerPortion;
            // Roundoff can leave the last representable fraction below the exact portion sum.
            if (mass > Ecology.State.FoodMgN && mass - Ecology.State.FoodMgN < 1e-8) mass = Ecology.State.FoodMgN;
            Ecology.TransferNitrogen(NitrogenPool.Food, to, mass, reason);
            binding.RemainingPortions -= portions; if (binding.RemainingPortions == 0) food.Remove(source);
        }
        private static string K(params string[] values) { string result=""; foreach(string v in values)result+=v==null?"-1:":v.Length.ToString(CultureInfo.InvariantCulture)+":"+v; return result; }
        private static string F(double value) { return value.ToString("R", CultureInfo.InvariantCulture); }
        private CommandResult Execute(string transactionId, string fingerprint, Func<GameSession, CommandResult> operation)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || transactionId.Length > 120) return CommandResult.Fail("transaction_invalid");
            if (Paused) return CommandResult.Fail("paused");
            foreach (SessionReceipt receipt in receipts) if (receipt.TransactionId == transactionId)
                return receipt.Fingerprint == fingerprint ? receipt.Result.Clone() : CommandResult.Fail("transaction_conflict");
            if(receipts.Count>=8192)return CommandResult.Fail("ledger_full");
            GameSession candidate = Restore(catalog, Capture(), mode);
            CommandResult result;
            try { result = operation(candidate); }
            catch (ArgumentException) { return CommandResult.Fail("command_invalid"); }
            catch (InvalidOperationException) { return CommandResult.Fail("condition_unmet"); }
            if (!result.Success) return result;
            candidate.receipts.Add(new SessionReceipt { TransactionId = transactionId, Fingerprint = fingerprint, Result = result.Clone() });
            candidate.ValidateCrossDomain(); Adopt(candidate); return result;
        }
        private void Adopt(GameSession candidate)
        {
            candidate.World.FoodConsumed -= candidate.OnFoodConsumed; candidate.World.FoodRemoved -= candidate.OnFoodRemoved;
            World = candidate.World; Gameplay = candidate.Gameplay; Ecology = candidate.Ecology; ecologyRate = candidate.ecologyRate;
            food.Clear(); foreach (var pair in candidate.food) food.Add(pair.Key, pair.Value);
            receipts.Clear(); receipts.AddRange(candidate.receipts); checkpoint = candidate.checkpoint; reserves = candidate.reserves;
            SubscribeFood();
        }
        public CommandResult Buy(string tx, string itemId)
        {
            return Execute(tx, K("buy",itemId), c => {
                ItemConfig item = c.catalog.Item(itemId);
                if(c.Gameplay.Stage!=GameplayStage.Editing && item.ItemKind!="fish" && item.ItemKind!="shrimp" && item.ItemKind!="care")return CommandResult.Fail("layout_locked");
                if (item.ContentStatus != "shipping") return CommandResult.Fail("content_unavailable");
                if (!string.IsNullOrEmpty(item.SpeciesId))
                {
                    SpeciesConfig species = c.catalog.SpeciesById(item.SpeciesId);
                    if (species.ContentStatus != "shipping" || species.InclusionScope != "v0_default") return CommandResult.Fail("content_unavailable");
                    if ((item.ItemKind == "fish" || item.ItemKind == "shrimp") && item.SpeciesId!="ember_tetra" && item.SpeciesId!="neon_tetra" && item.SpeciesId!="neocaridina_red") return CommandResult.Fail("content_unavailable");
                    if ((item.ItemKind == "fish" || item.ItemKind == "shrimp") && c.mode == SessionMode.Release)
                        return CommandResult.Fail("ecology_review_pending");
                }
                if(item.ItemKind=="fish"||item.ItemKind=="shrimp")
                {int current=0;foreach(AquariumAgent a in c.World.Agents)if(a.State.SpeciesId==item.SpeciesId)current++;
                 if(current+item.UnitsPerPurchase>c.StockingLimit(item.SpeciesId))return CommandResult.Fail("stocking_limit");}
                if ((item.ItemKind == "fish" || item.ItemKind == "shrimp") && !c.Ecology.WaterIsStable) return CommandResult.Fail("water_unstable");
                CommandResult r = c.Gameplay.Buy(tx, itemId); if (!r.Success) return r;
                foreach (string id in r.EntityIds) if (!ContainsAgent(c.World, id))
                    c.World.AddAgent(c.profiles[item.SpeciesId], id, Seed(id));
                if (item.RouteRole == "required_trial" && c.Ecology.TrialObservation.Status == TrialObservationStatus.NotStarted)
                    c.Ecology.BeginTrialObservation(c.Gameplay.LiveAnimalCount());
                return r;
            });
        }
        public CommandResult Refund(string tx, string batchId) { return Execute(tx, K("refund",batchId), c => c.Gameplay.Refund(tx, batchId)); }
        public CommandResult Sell(string tx, string batchId)
        {
            return Execute(tx, K("sell",batchId), c => {
                CommandResult r = c.Gameplay.Sell(tx, batchId); if (!r.Success) return r;
                c.reserves.Reconcile(c.Ecology.State.LivingMgN);
                foreach(string id in r.EntityIds){double mass=c.reserves.Remove(id);if(mass>c.Ecology.State.LivingMgN&&mass-c.Ecology.State.LivingMgN<1e-8)mass=c.Ecology.State.LivingMgN;if(mass>0)c.Ecology.RemoveNitrogen(NitrogenPool.Living,mass,"animal-sale");}
                c.SynchronizeAgents(); return r;
            });
        }
        public CommandResult Place(string tx, LayoutPlacement p)
        {
            if (p == null) return CommandResult.Fail("command_invalid");
            string fingerprint = K("place",p.Id,p.PurchaseBatchId,F(p.Position.X),F(p.Position.Y),F(p.Position.Z),F(p.Size.X),F(p.Size.Y),F(p.Size.Z),F(p.YawDegrees),p.SupportPlacementId,p.RhizomeBuried.ToString());
            return Execute(tx, fingerprint, c => c.Gameplay.Place(tx, p));
        }
        public CommandResult Remove(string tx, string placementId) { return Execute(tx, K("remove",placementId), c => c.Gameplay.Remove(tx, placementId)); }
        public CommandResult SetSubstrate(string tx, SubstrateProfile s)
        {
            if (s == null) return CommandResult.Fail("command_invalid");
            return Execute(tx, K("substrate",s.PurchaseBatchId,F(s.FrontLeftHeight),F(s.FrontRightHeight),F(s.BackLeftHeight),F(s.BackRightHeight)), c => c.Gameplay.SetSubstrate(tx,s));
        }
        public CommandResult Undo(string tx) { return Execute(tx,"undo",c => c.Gameplay.Undo(tx)); }
        public CommandResult Redo(string tx) { return Execute(tx,"redo",c => c.Gameplay.Redo(tx)); }
        public CommandResult ConfirmLayout(string tx)
        {
            return Execute(tx,"confirm",c => { SessionSnapshot before = c.Capture(false); CommandResult r=c.Gameplay.ConfirmLayout(tx); if(r.Success)c.checkpoint=StateOnly(before); return r; });
        }
        public CommandResult Fill(string tx)
        {
            return Execute(tx,"fill",c => { CommandResult r=c.Gameplay.Fill(tx); if(!r.Success)return r;
                c.Ecology = new EcologyEngine(c.CreateEcologyParameters(c.Gameplay.CalculateEffectiveWaterLiters()));
                c.World.SetEcologyRate(c.ecologyRate); return r; });
        }
        public CommandResult BeginChallenge(string tx)
        {
            return Execute(tx,"challenge",c => {
                if(!c.Gameplay.Filled)return CommandResult.Fail("wet_required");
                c.Ecology.BeginFishlessChallenge(c.Gameplay.LiveAnimalCount());
                c.Gameplay.RefreshProgress("progress:challenge:"+tx,c.Ecology); return CommandResult.Ok(); });
        }
        public CommandResult Feed(string tx, Vec3 position, int portions = 12)
        {
            if (!Scalar.IsFinite(position) || portions <= 0 || portions > 1000) return CommandResult.Fail("command_invalid");
            return Execute(tx,K("feed",F(position.X),F(position.Y),F(position.Z),portions.ToString(CultureInfo.InvariantCulture)),c => {
                if(!c.Gameplay.Filled)return CommandResult.Fail("wet_required");
                long source=c.World.Feed(position,portions);
                c.Ecology.AddNitrogen(NitrogenPool.Food,portions*FoodMgNPerPortion,"feed:"+source);
                c.food.Add(source,new FoodBinding { SourceId=source,RemainingPortions=portions,MgNPerPortion=FoodMgNPerPortion }); return CommandResult.Ok(); });
        }
        public CommandResult ChangeWater(string tx, double liters)
        {
            return Execute(tx,K("water",F(liters)),c => {
                if(!c.Gameplay.Filled)return CommandResult.Fail("wet_required");
                c.Ecology.ChangeWater(liters,new WaterComposition()); return CommandResult.Ok(); });
        }
        public CommandResult SetLight(string tx,bool on) { return Execute(tx,K("light",on.ToString()),c => { if(!c.Gameplay.Filled)return CommandResult.Fail("wet_required"); c.World.SetLight(on); return CommandResult.Ok(); }); }
        public CommandResult ApproachGlass(string tx,Vec3 position)
        { return Execute(tx,K("glass",F(position.X),F(position.Y),F(position.Z)),c=>{c.World.ApproachGlass(position);return CommandResult.Ok();}); }
        public void SetPaused(bool paused) { Gameplay.SetPaused(paused); }
        public void SetEcologyRate(double rate)
        {
            if (rate!=1 && rate!=2 && rate!=4) throw new ArgumentOutOfRangeException("rate");
            ecologyRate=rate; World.SetEcologyRate(Gameplay.Filled ? rate : 0);
        }
        public void Advance(double realSeconds)
        {
            if (!Scalar.IsFinite(realSeconds) || realSeconds<0 || realSeconds>60) throw new ArgumentOutOfRangeException("realSeconds");
            if(Paused)return;
            // Interleave at the behavior fixed step so expiry/ingestion occur at the same ecological instant
            // for a long host frame and many small host frames. No serialization or whole-game copies here.
            double remaining=realSeconds;
            while(remaining>1e-12)
            {
                double dt=Math.Min(AquariumWorld.FixedStep,remaining); remaining-=dt;
                double before=World.EcologyDays; World.Advance(dt);
                if(Gameplay.Filled) { Ecology.AdvanceGameDays(World.EcologyDays-before); reserves.Reconcile(Ecology.State.LivingMgN); }
                GameplayStage stage=Gameplay.Stage;
                if(Ecology.WaterIsStable && (((stage==GameplayStage.Filled||stage==GameplayStage.Cycling)&&Ecology.Challenge.Status==FishlessChallengeStatus.Passed)||
                    stage==GameplayStage.TrialObserving&&Ecology.TrialObservation.Status==TrialObservationStatus.Passed))
                    Gameplay.RefreshProgress("progress:"+stage,Ecology);
            }
        }
        public CommandResult Move(string tx,string placementId,Vec3 position,double yawDegrees,string supportPlacementId=null)
        {
            foreach(LayoutPlacement p in Gameplay.Capture().Layout.Placements)if(p.Id==placementId)
            {p.Position=position;p.YawDegrees=yawDegrees;p.SupportPlacementId=supportPlacementId;return Place(tx,p);}
            return CommandResult.Fail("placement_missing");
        }

        public CommandResult Retry(string tx)
        {
            if(string.IsNullOrWhiteSpace(tx)||tx.Length>120)return CommandResult.Fail("transaction_invalid");
            if(Paused)return CommandResult.Fail("paused");
            if(checkpoint==null)return CommandResult.Fail("retry_unavailable");
            foreach(SessionReceipt r in receipts)if(r.TransactionId==tx)return r.Fingerprint=="retry"?r.Result.Clone():CommandResult.Fail("transaction_conflict");
            GameSession candidate=Restore(catalog,Envelope(checkpoint),mode); candidate.checkpoint=checkpoint;
            HashSet<string> consumed=new HashSet<string>();foreach(SessionReceipt old in candidate.receipts)consumed.Add(old.TransactionId);
            foreach(SessionReceipt old in receipts)if(old.Fingerprint=="retry"&&consumed.Add(old.TransactionId))candidate.receipts.Add(old.Clone());
            candidate.receipts.Add(new SessionReceipt {TransactionId=tx,Fingerprint="retry",Result=CommandResult.Ok()});
            Adopt(candidate); return CommandResult.Ok();
        }
        private static uint Seed(string id) { uint seed=2166136261;foreach(char c in id)seed=unchecked((seed^c)*16777619);return seed==0?1:seed; }
        private static bool ContainsAgent(AquariumWorld world,string id) {foreach(AquariumAgent a in world.Agents)if(a.State.EntityId==id)return true;return false;}
        private void SynchronizeAgents()
        {
            var expected=ExpectedAgents(); WorldSnapshot w=World.Capture();List<AgentState> keep=new List<AgentState>();
            foreach(AgentState a in w.Agents)if(expected.ContainsKey(a.EntityId))keep.Add(a);w.Agents=keep.ToArray();
            World=AquariumWorld.Restore(w,catalog.CreateTank(),profiles);SubscribeFood();
        }
        private int StockingLimit(string speciesId)
        {int limit=0;foreach(RouteConfig step in catalog.Route)if(step.RequiredForV0&&step.Action=="purchase")
         {ItemConfig item=catalog.Item(step.ItemId);if(item.SpeciesId==speciesId&&(item.ItemKind=="fish"||item.ItemKind=="shrimp"))limit+=step.PurchaseCount*item.UnitsPerPurchase;}return limit;}
        private Dictionary<string,string> ExpectedAgents()
        {
            Dictionary<string,string> expected=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(PurchaseBatch batch in Gameplay.Capture().Batches)if(!batch.Sold&&!batch.Refunded)
            {
                ItemConfig item=catalog.Item(batch.ItemId);
                if(item.ItemKind=="fish"||item.ItemKind=="shrimp")
                {
                    SpeciesConfig species=catalog.SpeciesById(item.SpeciesId);
                    if(item.ContentStatus!="shipping"||species.ContentStatus!="shipping"||species.InclusionScope!="v0_default"||
                        item.SpeciesId!="ember_tetra"&&item.SpeciesId!="neon_tetra"&&item.SpeciesId!="neocaridina_red")
                        throw new ArgumentException("Animal batch is outside the explicit prototype teaching scope.");
                }
                foreach(string id in batch.EntityIds)if(!expected.TryAdd(id,item.SpeciesId))throw new ArgumentException("Duplicate delivered animal.");
            }
            return expected;
        }
        public SessionSnapshot Capture(bool includeCheckpoint=true)
        {
            List<FoodBinding> bindings=new List<FoodBinding>();foreach(FoodBinding f in food.Values)bindings.Add(f.Clone());bindings.Sort((a,b)=>a.SourceId.CompareTo(b.SourceId));
            List<SessionReceipt> ledger=new List<SessionReceipt>();foreach(SessionReceipt r in receipts)ledger.Add(r.Clone());
            return new SessionSnapshot {ContentVersion=catalog.ContentVersion,Mode=mode,EcologyRate=ecologyRate,World=World.Capture(),Gameplay=Gameplay.Capture(),Ecology=Ecology.Capture(),LivingReserves=reserves.Capture(),Food=bindings.ToArray(),Receipts=ledger.ToArray(),
                HasRetryCheckpoint=includeCheckpoint&&checkpoint!=null,RetryCheckpoint=includeCheckpoint&&checkpoint!=null?StateOnly(Restore(catalog,Envelope(checkpoint),mode).Capture(false)):null};
        }
        private static SessionStateSnapshot StateOnly(SessionStateSnapshot s)
        {return new SessionStateSnapshot {SchemaVersion=s.SchemaVersion,ContentVersion=s.ContentVersion,Mode=s.Mode,EcologyRate=s.EcologyRate,World=s.World,Gameplay=s.Gameplay,Ecology=s.Ecology,LivingReserves=s.LivingReserves,Food=s.Food,Receipts=s.Receipts};}
        private static SessionSnapshot Envelope(SessionStateSnapshot s)
        {return new SessionSnapshot {SchemaVersion=s.SchemaVersion,ContentVersion=s.ContentVersion,Mode=s.Mode,EcologyRate=s.EcologyRate,World=s.World,Gameplay=s.Gameplay,Ecology=s.Ecology,LivingReserves=s.LivingReserves,Food=s.Food,Receipts=s.Receipts};}
        public static GameSession Restore(RuntimeCatalog catalog,SessionSnapshot snapshot,SessionMode mode=SessionMode.Prototype)
        {
            if(snapshot==null || snapshot.SchemaVersion!=1 || snapshot.ContentVersion!=catalog.ContentVersion || snapshot.Mode!=mode || snapshot.World==null || snapshot.Gameplay==null || snapshot.Ecology==null || snapshot.Food==null || snapshot.Receipts==null)
                throw new ArgumentException("Incomplete, incompatible or wrong-mode full session snapshot.");
            GameSession session=new GameSession(catalog,mode);
            session.Gameplay=GameplaySession.Restore(snapshot.Gameplay,session.gameplayCatalog,catalog.CreateTank());
            session.Ecology=EcologyEngine.Restore(snapshot.Ecology);
            session.reserves=LivingReserveLedger.Restore(snapshot.LivingReserves,session.Ecology.State.LivingMgN);
            session.World=AquariumWorld.Restore(snapshot.World,catalog.CreateTank(),session.profiles);
            session.ecologyRate=snapshot.EcologyRate;
            foreach(FoodBinding f in snapshot.Food)
            {
                if(f==null || f.SourceId<1 || f.RemainingPortions<1 || !Scalar.IsFinite(f.MgNPerPortion) || Math.Abs(f.MgNPerPortion-FoodMgNPerPortion)>1e-12 || session.food.ContainsKey(f.SourceId))throw new ArgumentException("Invalid food binding.");
                session.food.Add(f.SourceId,f.Clone());
            }
            HashSet<string> ids=new HashSet<string>();
            foreach(SessionReceipt r in snapshot.Receipts)
            {if(r==null||string.IsNullOrWhiteSpace(r.TransactionId)||!ids.Add(r.TransactionId)||string.IsNullOrEmpty(r.Fingerprint)||r.Result==null||!r.Result.Success)throw new ArgumentException("Invalid session receipt.");session.receipts.Add(r.Clone());}
            if(snapshot.HasRetryCheckpoint)
            {
                if(snapshot.RetryCheckpoint==null)throw new ArgumentException("Missing flagged retry checkpoint.");
                GameSession retry=Restore(catalog,Envelope(snapshot.RetryCheckpoint),mode);
                if(retry.Gameplay.Capture().Stage!=GameplayStage.Editing || retry.Gameplay.Capture().SaveId!=session.Gameplay.Capture().SaveId)throw new ArgumentException("Retry checkpoint does not belong to this pre-confirm session.");
                session.checkpoint=StateOnly(retry.Capture(false));
            }
            session.ValidateCrossDomain();session.SubscribeFood();return session;
        }
        private void ValidateCrossDomain()
        {
            GameplaySnapshot g=Gameplay.Capture();
            if(ecologyRate!=1&&ecologyRate!=2&&ecologyRate!=4 || World.EcologyRate!=(g.Filled?ecologyRate:0))throw new ArgumentException("Inconsistent session clock rates.");
            EcologySnapshot e=Ecology.Capture();
            EcologyParameters canonical=CreateEcologyParameters(Ecology.VolumeLiters);
            foreach(System.Reflection.FieldInfo field in typeof(EcologyParameters).GetFields())
                if(!object.Equals(field.GetValue(canonical),field.GetValue(e.Parameters)))throw new ArgumentException("Saved ecology parameters differ from the trusted session model: "+field.Name);
            if(Math.Abs(World.EcologyDays-e.GameDays-e.FixedAccumulatorGameDays)>1e-7)throw new ArgumentException("Ecology and behavior calendars disagree.");
            double volume=g.Filled?Gameplay.CalculateEffectiveWaterLiters():catalog.Tanks[0].EffectiveVolumeLDesign;
            if(Math.Abs(Ecology.VolumeLiters-volume)>1e-7)throw new ArgumentException("Water volume disagrees with confirmed layout.");
            var expected=ExpectedAgents();if(expected.Count!=World.Agents.Count)throw new ArgumentException("Animal delivery and world disagree.");
            var stockCounts=new Dictionary<string,int>();foreach(string speciesId in expected.Values){int stockCount;stockCounts.TryGetValue(speciesId,out stockCount);stockCounts[speciesId]=stockCount+1;}
            foreach(var stock in stockCounts)if(stock.Value>StockingLimit(stock.Key))throw new ArgumentException("Stocking exceeds the approved prototype route.");
            foreach(LivingReserveEntry entry in reserves.Capture().Entries)if(!expected.ContainsKey(entry.EntityId))throw new ArgumentException("Ingested reserve belongs to an animal outside the tank.");
            if(Math.Abs(reserves.TotalMgN-Ecology.State.LivingMgN)>1e-7)throw new ArgumentException("Ingested reserves disagree with ecology.");
            foreach(AquariumAgent a in World.Agents){string species;if(!expected.TryGetValue(a.State.EntityId,out species)||species!=a.State.SpeciesId)throw new ArgumentException("Wrong delivered animal identity.");}
            double total=0;int count=0;foreach(StimulusState s in World.Stimuli)if(s.Kind==StimulusKind.Food){FoodBinding f;if(!food.TryGetValue(s.Id,out f)||s.Portions!=f.RemainingPortions)throw new ArgumentException("Food portions disagree.");total+=f.RemainingPortions*f.MgNPerPortion;count++;}
            if(count!=food.Count||Math.Abs(total-Ecology.State.FoodMgN)>1e-7)throw new ArgumentException("Food nitrogen does not match remaining world food.");
            if(!g.Filled&&(World.Agents.Count>0||Ecology.GameDays>0||food.Count>0))throw new ArgumentException("Dry tank contains wet simulation state.");
            if(g.Stage>=GameplayStage.CyclePassed&&Ecology.Challenge.Status!=FishlessChallengeStatus.Passed)throw new ArgumentException("Progress has no valid nitrogen challenge.");
            if(g.Stage>=GameplayStage.MainUnlocked&&Ecology.TrialObservation.Status!=TrialObservationStatus.Passed)throw new ArgumentException("Progress has no valid stocked observation.");
            if(mode==SessionMode.Release&&World.Agents.Count>0)throw new ArgumentException("Release stocking remains blocked by pending ecological research.");
        }
    }
}
