using System;
using Aquarium.Core.Ecology;
using Aquarium.Core.Gameplay;

namespace Aquarium.Core.Session
{
    public enum SessionMode { Prototype, Release }
    [Serializable]
    public sealed class FoodBinding
    {
        public long SourceId;
        public int RemainingPortions;
        public double MgNPerPortion;
        public FoodBinding Clone() { return (FoodBinding)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class SessionReceipt
    {
        public string TransactionId, Fingerprint;
        public CommandResult Result;
        public SessionReceipt Clone() { return new SessionReceipt { TransactionId = TransactionId, Fingerprint = Fingerprint, Result = Result.Clone() }; }
    }
    [Serializable]
    public class SessionStateSnapshot
    {
        public int SchemaVersion = 1;
        public string ContentVersion;
        public SessionMode Mode;
        public double EcologyRate = 1;
        public WorldSnapshot World;
        public GameplaySnapshot Gameplay;
        public EcologySnapshot Ecology;
        public LivingReserveSnapshot LivingReserves;
        public FoodBinding[] Food = new FoodBinding[0];
        public SessionReceipt[] Receipts = new SessionReceipt[0];

    }
    [Serializable]
    public sealed class SessionSnapshot : SessionStateSnapshot
    {
        public bool HasRetryCheckpoint;
        public SessionStateSnapshot RetryCheckpoint;
    }
}
