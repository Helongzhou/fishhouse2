using System;
using System.Collections.Generic;

namespace Aquarium.Core.Ecology
{
    [Serializable]
    public sealed class LivingReserveEntry
    {
        public string EntityId;
        public double MgN;
        public LivingReserveEntry Clone() { return (LivingReserveEntry)MemberwiseClone(); }
    }
    [Serializable]
    public sealed class LivingReserveSnapshot
    {
        public int SchemaVersion = 1;
        public LivingReserveEntry[] Entries = new LivingReserveEntry[0];
    }

    // Ownership of ingested nitrogen only. Animal body nitrogen and starvation are outside this model.
    public sealed class LivingReserveLedger
    {
        private readonly SortedDictionary<string, double> reserves = new SortedDictionary<string, double>(StringComparer.Ordinal);
        public double TotalMgN { get { double total = 0; foreach (double value in reserves.Values) total += value; return total; } }
        private static void Entity(string id)
        { if (string.IsNullOrWhiteSpace(id) || id.Length > 512) throw new ArgumentException("A bounded animal entity ID is required."); }
        public void Record(string entityId, double mgN)
        {
            Entity(entityId); EcologyGuard.Nonnegative(mgN, "mgN");
            if (mgN <= 0) throw new ArgumentOutOfRangeException("mgN");
            EcologyGuard.Nonnegative(TotalMgN + mgN, "Total reserve");
            double old; reserves.TryGetValue(entityId, out old); reserves[entityId] = old + mgN;
        }
        public void Reconcile(double actualEcologyLivingMgN)
        {
            EcologyGuard.Nonnegative(actualEcologyLivingMgN, "actualEcologyLivingMgN");
            double total = TotalMgN;
            if (actualEcologyLivingMgN > total + 1e-7) throw new ArgumentException("Unowned living nitrogen cannot be created by reconciliation.");
            if (actualEcologyLivingMgN == 0) { reserves.Clear(); return; }
            if (total == 0) throw new ArgumentException("Nonzero living nitrogen has no owner.");
            double factor = actualEcologyLivingMgN / total;
            var ids = new List<string>(reserves.Keys);
            double remaining = actualEcologyLivingMgN;
            for (int i = 0; i < ids.Count; i++) {
                double mass = i == ids.Count - 1 ? remaining : Math.Min(remaining, reserves[ids[i]] * factor);
                reserves[ids[i]] = mass; remaining -= mass;
                if (mass == 0) reserves.Remove(ids[i]);
            }
        }
        public double Remove(string entityId)
        {
            Entity(entityId); double mass;
            if (!reserves.TryGetValue(entityId, out mass)) return 0;
            reserves.Remove(entityId); return mass;
        }
        public LivingReserveSnapshot Capture()
        {
            var entries = new LivingReserveEntry[reserves.Count]; int index = 0;
            foreach (var pair in reserves) entries[index++] = new LivingReserveEntry { EntityId = pair.Key, MgN = pair.Value };
            return new LivingReserveSnapshot { Entries = entries };
        }
        public static LivingReserveLedger Restore(LivingReserveSnapshot snapshot, double expectedLivingMgN)
        {
            EcologyGuard.Nonnegative(expectedLivingMgN, "expectedLivingMgN");
            if (snapshot == null || snapshot.SchemaVersion != 1 || snapshot.Entries == null || snapshot.Entries.Length > 131072)
                throw new ArgumentException("Incomplete or unsupported ingested reserve snapshot.");
            var ledger = new LivingReserveLedger();
            foreach (var entry in snapshot.Entries) {
                if (entry == null) throw new ArgumentException("Missing ingested reserve entry.");
                Entity(entry.EntityId); EcologyGuard.Nonnegative(entry.MgN, "reserve mass");
                if (entry.MgN <= 0 || ledger.reserves.ContainsKey(entry.EntityId)) throw new ArgumentException("Duplicate animal or empty reserve entry.");
                ledger.reserves.Add(entry.EntityId, entry.MgN);
            }
            if (Math.Abs(ledger.TotalMgN - expectedLivingMgN) > 1e-7) throw new ArgumentException("Animal ingested reserves disagree with ecology living nitrogen.");
            return ledger;
        }
    }
}
