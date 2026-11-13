using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Aquarium.Core;
using Aquarium.Core.Configuration;
using Aquarium.Core.Session;
using Aquarium.Core.Gameplay;
using Aquarium.Core.Ecology;

internal static class Program
{
    private static int passed;
    private static int sequence;
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields=true,IgnoreReadOnlyProperties=true };
    private static string Tx(){return "test-"+(++sequence);}
    private static void Check(bool condition,string title){if(!condition)throw new Exception("FAIL "+title);Console.WriteLine("PASS "+title);passed++;}
    private static void Ok(CommandResult result){if(!result.Success)throw new Exception(result.Code+" "+string.Join(",",result.Args));}
    private static string Encode(SessionSnapshot s){return JsonSerializer.Serialize(s,Json);}
    private static SessionSnapshot Decode(string s){return JsonSerializer.Deserialize<SessionSnapshot>(s,Json);}
    private static bool Reject(Action action){try{action();return false;}catch(ArgumentException){return true;}catch(InvalidOperationException){return true;}}
    private static RuntimeCatalog Catalog;
    private static double layoutLitres, layoutDose;
    private static string releaseEvidence;
    private static void Place(GameSession s,string id,string batch,Vec3 pos,Vec3 size,string host=null)
    {Ok(s.Place(Tx(),new LayoutPlacement{Id=id,PurchaseBatchId=batch,Position=pos,Size=size,SupportPlacementId=host}));}
    internal static void Main()
    {
        string root=Directory.GetCurrentDirectory();while(!Directory.Exists(Path.Combine(root,"data")))root=Directory.GetParent(root).FullName;
        Catalog=JsonSerializer.Deserialize<RuntimeCatalog>(File.ReadAllText(Path.Combine(root,"unity/Assets/_Game/Resources/Configuration/runtime.json")),Json);Catalog.Validate();
        Check(Catalog.Items.Length==17&&Catalog.Species.Length==9&&Catalog.Route.Length==21,"all nine runtime tables imported and validated");
        GameSession s=new GameSession(Catalog);Check(s.Gameplay.Capture().WalletCoin==1500&&s.World.Agents.Count==0,"new session is an empty independent 1500-coin tank");
        SessionSnapshot fresh=s.Capture();fresh.RetryCheckpoint=new SessionStateSnapshot();
        Check(!fresh.HasRetryCheckpoint&&!Reject(()=>GameSession.Restore(Catalog,Decode(Encode(fresh)))),"new-save checkpoint absence is explicit even if the serializer emits an empty inline object");
        fresh.HasRetryCheckpoint=true;Check(Reject(()=>GameSession.Restore(Catalog,fresh)),"a flagged but invalid checkpoint is rejected");
        s.Advance(60);Check(s.Ecology.GameDays==0&&s.World.EcologyDays==0,"dry tank never advances ecology");
        string before=Encode(s.Capture());Check(!s.Buy(Tx(),"fish_trial_pack").Success&&Encode(s.Capture())==before,"early fish purchase rejects atomically");
        foreach(string unavailable in new[]{"debug_fish_spawn","filter_top","filter_sump"})Check(!s.Buy(Tx(),unavailable).Success,"unavailable content cannot enter through direct ID "+unavailable);
        Check(!s.Buy(Tx(),"wood_optional").Success,"optional purchase reserves required-route funding");
        var batches=new Dictionary<string,string>();
        foreach(ItemConfig item in Catalog.Items)if(item.RouteRole=="required_base"){CommandResult r=s.Buy(Tx(),item.ItemId);Ok(r);batches.Add(item.ItemId,r.BatchId);}
        Check(s.Gameplay.Capture().WalletCoin==390,"configuration prices produce 1110 base cost");
        Ok(s.SetSubstrate(Tx(),new SubstrateProfile{PurchaseBatchId=batches["substrate_natural_sand"],FrontLeftHeight=.015,FrontRightHeight=.015,BackLeftHeight=.045,BackRightHeight=.045}));
        Vec3 mainSize=new Vec3(.09,.11,.07),secondSize=new Vec3(.06,.05,.05);Vec3 main=new Vec3(-.12,0,.02),second=new Vec3(.09,0,-.01);
        main.Y=s.Gameplay.SuggestedBottomHeight(main,mainSize,0)+mainSize.Y/2;second.Y=s.Gameplay.SuggestedBottomHeight(second,secondSize,0)+secondSize.Y/2;
        Place(s,"main",batches["stone_main"],main,mainSize);Place(s,"second",batches["stone_secondary"],second,secondSize);
        Place(s,"filter",batches["filter_hangon_basic"],new Vec3(.24,.28,.11),new Vec3(.04,.06,.04));
        Place(s,"light",batches["light_low"],new Vec3(-.20,.33,0),new Vec3(.06,.015,.15));
        Place(s,"anubias",batches["plant_anubias_nana"],new Vec3(main.X-.02,main.Y+mainSize.Y/2,main.Z),new Vec3(.02,.035,.02),"main");
        Place(s,"fern",batches["plant_java_fern"],new Vec3(main.X+.02,main.Y+mainSize.Y/2,main.Z),new Vec3(.02,.05,.02),"main");
        Place(s,"moss",batches["plant_java_moss"],new Vec3(second.X,second.Y+secondSize.Y/2,second.Z),new Vec3(.025,.015,.025),"second");
        Ok(s.ConfirmLayout(Tx()));Ok(s.Fill(Tx()));Check(s.Ecology.VolumeLiters<54&&s.Ecology.VolumeLiters>50,"filled volume uses actual slope and hardscape displacement");
        s.SetPaused(true);before=Encode(s.Capture());
        Check(!s.Feed(Tx(),new Vec3()).Success&&!s.SetLight(Tx(),true).Success&&!s.BeginChallenge(Tx()).Success,"pause rejects all cross-domain business stimuli");s.Advance(60);Check(before==Encode(s.Capture()),"pause stops both clocks without changing state");s.SetPaused(false);
        s.SetEcologyRate(4);Ok(s.BeginChallenge(Tx()));
        for(int i=0;i<600&&s.Gameplay.Capture().Stage!=GameplayStage.CyclePassed;i++)s.Advance(60);
        Check(s.Ecology.Challenge.Status==FishlessChallengeStatus.Passed&&s.Gameplay.Capture().Stage==GameplayStage.CyclePassed,"real nonzero nitrogen challenge unlocks first stocking");
        Check(Math.Abs(s.Ecology.Challenge.DoseMgN-0.5*s.Ecology.VolumeLiters)<1e-6,"filled layout dose is 0.5 mgN per actual litre");
        layoutLitres=s.Ecology.VolumeLiters; layoutDose=s.Ecology.Challenge.DoseMgN;
        SessionSnapshot cycleReady=s.Capture();
        string buyTx=Tx();CommandResult first=s.Buy(buyTx,"fish_trial_pack");Ok(first);Ok(s.Buy(buyTx,"fish_trial_pack"));
        Check(s.World.Agents.Count==8&&s.Gameplay.Capture().WalletCoin==270,"duplicate purchase creates exactly one stable eight-fish delivery");
        Ok(s.Buy(Tx(),"shrimp_pack"));Check(s.World.Agents.Count==14,"six shrimp bind to gameplay batches and world entities");
        string feedTx=Tx();Ok(s.Feed(feedTx,new Vec3(0,.29,0),12));double input=s.Ecology.Audit.CumulativeInputMgN;Ok(s.Feed(feedTx,new Vec3(0,.29,0),12));
        Check(s.Ecology.Audit.CumulativeInputMgN==input,"feed command replay cannot duplicate nitrogen input");
        GameSession longFrame=GameSession.Restore(Catalog,s.Capture()), smallFrames=GameSession.Restore(Catalog,s.Capture());
        longFrame.Advance(60);for(int i=0;i<1800;i++)smallFrames.Advance(AquariumWorld.FixedStep);
        Check(Encode(longFrame.Capture())==Encode(smallFrames.Capture()),"one 60-second frame equals 1800 fixed frames including ingestion and expiry");
        GameSession evicted=GameSession.Restore(Catalog,s.Capture());
        for(int i=0;i<65;i++)Ok(evicted.Feed("capacity-"+i,new Vec3(0,.29,0),1));
        Check(evicted.Capture().Food.Length==64&&evicted.Ecology.State.OrganicMgN>0,"food evicted by behavior event capacity becomes organic nitrogen");
        Check(!s.Buy(Tx(),"unknown:species:id").Success,"unknown IDs reject without crossing configuration boundary");
        GameSession loaded=GameSession.Restore(Catalog,Decode(Encode(s.Capture())));s.Advance(40);loaded.Advance(40);
        Check(Encode(s.Capture())==Encode(loaded.Capture()),"full save reload preserves food bindings, RNG, trajectories and ecology");
        Check(s.Ecology.State.FoodMgN<1e-8&&s.Ecology.State.LivingMgN+s.Ecology.State.OrganicMgN>0,"consumption and expiry transfer food nitrogen exactly once");
        for(int i=0;i<10&&s.Gameplay.Capture().Stage!=GameplayStage.MainUnlocked;i++)s.Advance(60);
        Check(s.Gameplay.Capture().Stage==GameplayStage.MainUnlocked&&s.Gameplay.Capture().WalletCoin==540,"actual stocked observation grants the 450 reward once");
        Ok(s.ChangeWater(Tx(),s.Ecology.VolumeLiters*.2));Ok(s.Buy(Tx(),"fish_main_pack"));
        Check(s.World.Agents.Count==22&&s.Gameplay.Capture().WalletCoin==280,"full 8 Ember + 6 shrimp + 8 Neon route ends with 280 coins");
        GameSession polluted=GameSession.Restore(Catalog,s.Capture());
        for(int i=0;i<20;i++)Ok(polluted.Feed(Tx(),new Vec3(0,.29,0),1000));
        polluted.Advance(60);for(int i=0;i<10&&polluted.Ecology.WaterIsStable;i++)polluted.Advance(60);
        Check(!polluted.Ecology.WaterIsStable&&!polluted.Buy(Tx(),"fish_trial_pack").Success,"past challenge does not permit stocking into currently polluted water");
        Check(!s.Buy(Tx(),"fish_trial_pack").Success&&!s.Buy(Tx(),"shrimp_pack").Success&&!s.Buy(Tx(),"fish_main_pack").Success,"prototype teaching permit caps the actual 8/6/8 population");
        Check(!s.Buy(Tx(),"plant_anubias_nana").Success&&!s.Buy(Tx(),"stone_main").Success,"confirmed aquarium cannot buy undeployable new scenery");
        GameSession sale=GameSession.Restore(Catalog,s.Capture());LivingReserveSnapshot reserves=sale.Capture().LivingReserves;
        Check(reserves.Entries.Length>0,"consumed nitrogen is attributed to actual animal entities");
        PurchaseBatch saleBatch=null;foreach(PurchaseBatch batch in sale.Gameplay.Capture().Batches)foreach(string id in batch.EntityIds)if(id==reserves.Entries[0].EntityId)saleBatch=batch;
        double soldReserve=0;foreach(LivingReserveEntry reserve in reserves.Entries)foreach(string id in saleBatch.EntityIds)if(id==reserve.EntityId)soldReserve+=reserve.MgN;
        double livingBefore=sale.Ecology.State.LivingMgN,outputBefore=sale.Ecology.Audit.CumulativeOutputMgN;
        Ok(sale.Sell(Tx(),saleBatch.Id));Check(Math.Abs(sale.Ecology.State.LivingMgN-(livingBefore-soldReserve))<1e-7&&Math.Abs(sale.Ecology.Audit.CumulativeOutputMgN-outputBefore-soldReserve)<1e-7,"selling a batch exports exactly its remaining ingested nitrogen");
        Ok(sale.Buy(Tx(),saleBatch.ItemId));Check(sale.World.Agents.Count==22,"a sold whole animal pack may be repurchased within the teaching limit");
        SessionSnapshot tampered=s.Capture();tampered.Ecology.Parameters.StableGameHours=.01;
        Check(Reject(()=>GameSession.Restore(Catalog,tampered)),"restore rejects loosened ecology parameters despite valid engine ranges");
        SessionSnapshot snapshot=s.Capture();snapshot.Gameplay.WalletCoin++;
        Check(Reject(()=>GameSession.Restore(Catalog,snapshot)),"semantic restore rejects forged wallet even with valid JSON");
        snapshot=s.Capture();snapshot.World.Agents[0].SpeciesId="white_cloud";
        Check(Reject(()=>GameSession.Restore(Catalog,snapshot)),"planned species cannot be introduced by save mutation");
        snapshot=s.Capture();snapshot.Ecology.GameDays+=1;
        Check(Reject(()=>GameSession.Restore(Catalog,snapshot)),"restore rejects cross-domain clock mismatch");
        string releaseModeMessage=null; bool releaseModeRejected=false;
        try { GameSession.Restore(Catalog,s.Capture(),SessionMode.Release); }
        catch (ArgumentException error) { releaseModeRejected=true; releaseModeMessage=error.Message; }
        catch (InvalidOperationException error) { releaseModeRejected=true; releaseModeMessage=error.Message; }
        Check(releaseModeRejected,"prototype snapshot cannot bypass the release mode gate");
        SessionSnapshot switched=s.Capture(); switched.Mode=SessionMode.Release;
        if(switched.RetryCheckpoint!=null) switched.RetryCheckpoint.Mode=SessionMode.Release;
        string animalMessage=null; bool animalRejected=false;
        try { GameSession.Restore(Catalog,switched,SessionMode.Release); }
        catch (ArgumentException error) { animalRejected=true; animalMessage=error.Message; }
        catch (InvalidOperationException error) { animalRejected=true; animalMessage=error.Message; }
        Check(animalRejected&&s.World.Agents.Count>0,"prototype save with animals cannot be restored as Release");
        GameSession release=new GameSession(Catalog,SessionMode.Release);
        CommandResult releaseFish=release.Buy(Tx(),"fish_trial_pack");
        CommandResult releaseShrimp=release.Buy(Tx(),"shrimp_pack");
        Check(!releaseFish.Success&&releaseFish.Code=="gameplay.ecology_review_pending","Release mode blocks the fish pack before delivery");
        Check(!releaseShrimp.Success&&releaseShrimp.Code=="gameplay.ecology_review_pending","Release mode blocks the shrimp pack before delivery");
        Check(Catalog.Tanks[0].SimulationReady==false,"loaded catalog keeps simulation_ready false");
        releaseEvidence="{\"fish\":{\"success\":false,\"code\":\""+releaseFish.Code+"\"},\"shrimp\":{\"success\":false,\"code\":\""+releaseShrimp.Code+"\"},\"prototype_restore_as_release\":{\"rejected\":true,\"message\":\""+releaseModeMessage+"\",\"animal_count\":"+s.World.Agents.Count+"},\"mode_switched_save_with_animals\":{\"rejected\":true,\"message\":\""+animalMessage+"\",\"animal_count\":"+s.World.Agents.Count+"},\"simulation_ready\":false,\"layout_litres\":"+layoutLitres.ToString("G17",CultureInfo.InvariantCulture)+",\"layout_dose_mgN\":"+layoutDose.ToString("G17",CultureInfo.InvariantCulture)+",\"dose_mgN_per_l\":0.5,\"gold_volume_l\":54}";
        string temporary=Path.Combine(Path.GetTempPath(),"aquarium-integration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temporary);
        try
        {
            string path=Path.Combine(temporary,"session.json");Func<string,bool> valid=text=>{try{GameSession.Restore(Catalog,Decode(text));return true;}catch{return false;}};
            var store=new AtomicSaveStore(path,valid);string a=Encode(s.Capture());store.Save(a);s.Advance(1);string b=Encode(s.Capture());store.Save(b);s.Advance(1);store.Save(Encode(s.Capture()));s.Advance(1);store.Save(Encode(s.Capture()));
            Check(File.Exists(path+".bak1")&&File.Exists(path+".bak2")&&File.Exists(path+".bak3"),"three verified historical saves are retained");
            snapshot=s.Capture();snapshot.Gameplay.WalletCoin++;File.WriteAllText(path,Encode(snapshot));string recovered;string recovery=store.Load(out recovered);
            Check(recovered==path+".bak1"&&valid(recovery),"semantic bad primary recovers a valid historical session");
            File.WriteAllText(path+".bak1","{bad");store.Load(out recovered);Check(recovered==path+".bak2","corrupt newest backup falls back to older valid history");
            store.Save(a);string retained=File.ReadAllText(path);
            foreach(string point in new[]{"before_temp_write","after_temp_flush","before_commit"})
            {
                var failing=new AtomicSaveStore(path,valid,p=>{if(p==point)throw new IOException("injected "+p);});
                try{failing.Save(b);}catch(IOException){}
                Check(File.ReadAllText(path)==retained&&valid(retained)&&!File.Exists(path+".tmp"),"I/O failure at "+point+" retains the valid committed save");
            }
        }
        finally{Directory.Delete(temporary,true);}
        Ok(s.Retry("retry-once"));Check(s.Gameplay.Capture().Stage==GameplayStage.Editing&&s.World.Agents.Count==0&&s.Gameplay.Capture().WalletCoin==390,"free retry atomically restores pre-confirm economy, layout, animals and ecology");
        before=Encode(s.Capture());Ok(s.Retry("retry-once"));Check(before==Encode(s.Capture()),"duplicate retry request is idempotent");
        Ok(s.Retry("retry-twice"));var moved=s.Gameplay.Capture().Layout.Placements[0];Ok(s.Move(Tx(),moved.Id,moved.Position,moved.YawDegrees+5));
        string afterRetry=Encode(s.Capture());Ok(s.Retry("retry-once"));
        Check(afterRetry==Encode(s.Capture()),"older retry transaction remains consumed after a different retry");
        Ok(s.ConfirmLayout("confirm-again"));Ok(s.Retry("retry-third"));
        Check(!Reject(()=>GameSession.Restore(Catalog,Decode(Encode(s.Capture())))),"retry then reconfirm then retry keeps a unique recoverable receipt ledger");
        GameSession delayed=GameSession.Restore(Catalog,cycleReady);Ok(delayed.Buy(Tx(),"fish_trial_pack"));
        for(int i=0;i<5&&delayed.Ecology.TrialObservation.Status!=TrialObservationStatus.Passed;i++)delayed.Advance(60);
        Check(delayed.Ecology.TrialObservation.Status==TrialObservationStatus.Passed&&delayed.Gameplay.Stage==GameplayStage.TrialObserving,"observation waits for the still-missing required shrimp purchase");
        for(int i=0;i<20;i++)Ok(delayed.Feed(Tx(),new Vec3(0,.29,0),1000));delayed.Advance(60);
        Check(!delayed.Ecology.WaterIsStable,"delayed reward scenario reaches unstable water");
        Ok(delayed.ChangeWater(Tx(),delayed.Ecology.VolumeLiters));
        // Remove dissolved/organic residues; ingested reserve remains, then wait until stable again.
        for(int i=0;i<500&&!delayed.Ecology.WaterIsStable;i++){delayed.Advance(60);Ok(delayed.ChangeWater(Tx(),delayed.Ecology.VolumeLiters));}
        Ok(delayed.Buy(Tx(),"shrimp_pack"));delayed.Advance(1);
        Check(delayed.Gameplay.Stage==GameplayStage.MainUnlocked,"unstable no-op cannot consume the observation progress transaction and softlock rewards");
        string directory=Environment.GetEnvironmentVariable("AQUARIUM_EVIDENCE_DIR");
        if(!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"integration.json"),"{\"new_run\":true,\"unity\":\"not_run\",\"checks_passed\":"+passed.ToString(CultureInfo.InvariantCulture)+",\"item\":"+releaseEvidence+"}");
        }
        Console.WriteLine("Integration checks: "+passed+" passed.");
    }
}
