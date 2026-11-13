using System;
using System.Collections.Generic;

namespace Aquarium.Core.Gameplay
{
    public sealed partial class GameplaySession
    {
        private static LayoutState[] Push(LayoutState[] history, LayoutState layout)
        {
            List<LayoutState> list = new List<LayoutState>(history); list.Add(layout.Clone());
            if (list.Count > HistoryLimit) list.RemoveAt(0); return list.ToArray();
        }
        private CommandResult ChangeLayout(string tx, string fingerprint, Func<GameplaySnapshot, LayoutState, CommandResult> change)
        {
            return Execute(tx, fingerprint, s =>
            {
                LayoutState layout = s.Layout.Clone(); CommandResult result = change(s, layout);
                if (!result.Success) return result;
                CommandResult valid = LayoutGeometry.Validate(layout, catalog, tank, id => FindBatch(s, id));
                if (!valid.Success) return valid;
                s.UndoHistory = Push(s.UndoHistory, s.Layout); s.RedoHistory = new LayoutState[0]; s.Layout = layout;
                return CommandResult.Ok();
            });
        }
        public CommandResult Place(string transactionId, LayoutPlacement placement)
        {
            LayoutPlacement requested = placement == null ? null : placement.Clone();
            return ChangeLayout(transactionId, "place:" + PlacementKey(requested), (s, layout) =>
            {
                if (requested == null) return CommandResult.Fail("placement_invalid");
                PurchaseBatch batch = FindBatch(s, requested.PurchaseBatchId);
                if (batch == null || !Active(batch)) return CommandResult.Fail("batch_unavailable");
                ItemDefinition item = catalog.Items[batch.ItemId];
                LayoutPlacement old = FindPlacement(layout, requested.Id);
                if (s.Stage != GameplayStage.Editing && (item.Kind != "plant" || old == null)) return CommandResult.Fail("layout_locked");
                if (old != null && old.PurchaseBatchId != requested.PurchaseBatchId) return CommandResult.Fail("placement_id_conflict");
                if (old != null && Vec3.Distance(old.Size, requested.Size) > LayoutGeometry.Tolerance)
                    return CommandResult.Fail("placement_size_locked");
                List<LayoutPlacement> placements = new List<LayoutPlacement>(layout.Placements);
                if (old == null) placements.Add(requested.Clone());
                else
                {
                    placements[placements.IndexOf(old)] = requested.Clone();
                    // A stone move carries its attached plants with the same rigid transform.
                    foreach (LayoutPlacement child in placements)
                        if (child.SupportPlacementId == old.Id)
                        {
                            child.Position = requested.Position + LayoutGeometry.RotateOffset(child.Position - old.Position,
                                requested.YawDegrees - old.YawDegrees);
                            child.YawDegrees += requested.YawDegrees - old.YawDegrees;
                        }
                }
                layout.Placements = placements.ToArray(); return CommandResult.Ok();
            });
        }
        public CommandResult Remove(string transactionId, string placementId)
        {
            return ChangeLayout(transactionId, "remove:" + Part(placementId), (s, layout) =>
            {
                LayoutPlacement placement = FindPlacement(layout, placementId);
                if (placement == null) return CommandResult.Fail("placement_missing");
                if (s.Stage != GameplayStage.Editing) return CommandResult.Fail("layout_locked");
                foreach (LayoutPlacement child in layout.Placements)
                    if (child.SupportPlacementId == placementId) return CommandResult.Fail("support_in_use", placementId);
                List<LayoutPlacement> placements = new List<LayoutPlacement>(layout.Placements); placements.Remove(placement);
                layout.Placements = placements.ToArray(); return CommandResult.Ok();
            });
        }
        public CommandResult SetSubstrate(string transactionId, SubstrateProfile substrate)
        {
            SubstrateProfile requested = substrate == null ? null : substrate.Clone();
            string key = requested == null ? "null" : Part(requested.PurchaseBatchId) + Number(requested.FrontLeftHeight) + "," +
                Number(requested.FrontRightHeight) + "," + Number(requested.BackLeftHeight) + "," + Number(requested.BackRightHeight);
            return ChangeLayout(transactionId, "substrate:" + key, (s, layout) =>
            {
                if (s.Stage != GameplayStage.Editing) return CommandResult.Fail("layout_locked");
                if (requested == null || !LayoutGeometry.ValidSubstrate(requested, tank)) return CommandResult.Fail("substrate_invalid");
                Dictionary<string, double> deltas = new Dictionary<string, double>();
                foreach (LayoutPlacement p in layout.Placements)
                {
                    PurchaseBatch batch = FindBatch(s, p.PurchaseBatchId);
                    if (batch != null && catalog.Items[batch.ItemId].Kind == "hardscape")
                    {
                        double target = LayoutGeometry.BottomHeight(p, requested, tank) + p.Size.Y / 2;
                        deltas[p.Id] = target - p.Position.Y; p.Position.Y = target;
                    }
                }
                foreach (LayoutPlacement p in layout.Placements)
                {
                    double dy;
                    if (p.SupportPlacementId != null && deltas.TryGetValue(p.SupportPlacementId, out dy)) p.Position.Y += dy;
                }
                layout.Substrate = requested.Clone(); return CommandResult.Ok();
            });
        }
        public CommandResult Undo(string transactionId) { return NavigateHistory(transactionId, true); }
        public CommandResult Redo(string transactionId) { return NavigateHistory(transactionId, false); }
        private CommandResult NavigateHistory(string tx, bool undo)
        {
            return Execute(tx, undo ? "undo" : "redo", s =>
            {
                LayoutState[] source = undo ? s.UndoHistory : s.RedoHistory;
                if (source.Length == 0) return CommandResult.Fail(undo ? "nothing_to_undo" : "nothing_to_redo");
                LayoutState target = source[source.Length - 1].Clone();
                CommandResult valid = LayoutGeometry.Validate(target, catalog, tank, id => FindBatch(s, id));
                if (!valid.Success) return valid;
                LayoutState[] remainder = new LayoutState[source.Length - 1]; Array.Copy(source, remainder, remainder.Length);
                if (undo) { s.UndoHistory = remainder; s.RedoHistory = Push(s.RedoHistory, s.Layout); }
                else { s.RedoHistory = remainder; s.UndoHistory = Push(s.UndoHistory, s.Layout); }
                s.Layout = target; return CommandResult.Ok();
            });
        }
    }
}
