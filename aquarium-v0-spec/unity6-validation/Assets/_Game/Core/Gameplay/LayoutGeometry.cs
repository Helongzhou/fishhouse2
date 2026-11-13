using System;
using System.Collections.Generic;

namespace Aquarium.Core.Gameplay
{
    internal static class LayoutGeometry
    {
        internal const double Tolerance = .00001;
        internal static bool ValidSubstrate(SubstrateProfile p, TankBounds tank)
        {
            if (p == null) return true;
            double[] h = { p.FrontLeftHeight, p.FrontRightHeight, p.BackLeftHeight, p.BackRightHeight };
            double min = double.MaxValue, max = 0;
            foreach (double value in h)
            {
                if (!Scalar.IsFinite(value) || value < .005 || value > Math.Min(.08, tank.Height * .3)) return false;
                min = Math.Min(min, value); max = Math.Max(max, value);
            }
            return max - min <= .05 + Tolerance;
        }
        internal static double Height(SubstrateProfile p, TankBounds tank, double x, double z)
        {
            if (p == null) return 0;
            double u = Scalar.Clamp01(x / tank.Width + .5), v = Scalar.Clamp01(z / tank.Depth + .5);
            return (p.FrontLeftHeight * (1 - u) + p.FrontRightHeight * u) * (1 - v) +
                (p.BackLeftHeight * (1 - u) + p.BackRightHeight * u) * v;
        }
        private static Vec3 Rotate(Vec3 p, double yaw)
        {
            double a = yaw * Math.PI / 180, c = Math.Cos(a), s = Math.Sin(a);
            // Positive yaw matches Unity's Quaternion.Euler(0, yaw, 0) in the adapter.
            return new Vec3(c * p.X + s * p.Z, p.Y, -s * p.X + c * p.Z);
        }
        internal static Vec3 RotateOffset(Vec3 p, double yaw) { return Rotate(p, yaw); }
        internal static Vec3[] Corners(LayoutPlacement p)
        {
            Vec3[] result = new Vec3[4]; int i = 0;
            foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 })
                result[i++] = p.Position + Rotate(new Vec3(x * p.Size.X / 2, 0, z * p.Size.Z / 2), p.YawDegrees);
            return result;
        }
        internal static double BottomHeight(LayoutPlacement p, SubstrateProfile substrate, TankBounds tank)
        {
            double max = 0;
            foreach (Vec3 corner in Corners(p)) max = Math.Max(max, Height(substrate, tank, corner.X, corner.Z));
            return max;
        }
        private static bool ContainsXZ(LayoutPlacement box, Vec3 p)
        {
            Vec3 local = Rotate(p - box.Position, -box.YawDegrees);
            return Math.Abs(local.X) <= box.Size.X / 2 + Tolerance && Math.Abs(local.Z) <= box.Size.Z / 2 + Tolerance;
        }
        internal static bool Overlap(LayoutPlacement a, LayoutPlacement b)
        {
            if (Math.Abs(a.Position.Y - b.Position.Y) >= (a.Size.Y + b.Size.Y) / 2 - Tolerance) return false;
            Vec3[] ac = Corners(a), bc = Corners(b);
            foreach (double yaw in new[] { a.YawDegrees, a.YawDegrees + 90, b.YawDegrees, b.YawDegrees + 90 })
            {
                Vec3 axis = Rotate(new Vec3(1, 0, 0), yaw);
                double amin = double.MaxValue, amax = double.MinValue, bmin = double.MaxValue, bmax = double.MinValue;
                foreach (Vec3 p in ac) { double d = p.X * axis.X + p.Z * axis.Z; amin = Math.Min(amin, d); amax = Math.Max(amax, d); }
                foreach (Vec3 p in bc) { double d = p.X * axis.X + p.Z * axis.Z; bmin = Math.Min(bmin, d); bmax = Math.Max(bmax, d); }
                if (amax <= bmin + Tolerance || bmax <= amin + Tolerance) return false;
            }
            return true;
        }
        internal static CommandResult Validate(LayoutState layout, GameplayCatalog catalog, TankBounds tank,
            Func<string, PurchaseBatch> findBatch)
        {
            if (layout == null || layout.Placements == null || layout.HasSubstrate != (layout.Substrate != null) || !ValidSubstrate(layout.Substrate, tank))
                return CommandResult.Fail("substrate_invalid");
            HashSet<string> batches = new HashSet<string>();
            if (layout.Substrate != null)
            {
                PurchaseBatch sb = findBatch(layout.Substrate.PurchaseBatchId);
                if (sb == null || sb.Refunded || sb.Sold || catalog.Items[sb.ItemId].Kind != "substrate")
                    return CommandResult.Fail("batch_unavailable");
                batches.Add(sb.Id);
            }
            Dictionary<string, LayoutPlacement> placements = new Dictionary<string, LayoutPlacement>();
            foreach (LayoutPlacement p in layout.Placements)
            {
                if (p == null || string.IsNullOrEmpty(p.Id) || placements.ContainsKey(p.Id) || !Scalar.IsFinite(p.Position) ||
                    !Scalar.IsFinite(p.Size) || !Scalar.IsFinite(p.YawDegrees) || p.Size.X <= 0 || p.Size.Y <= 0 || p.Size.Z <= 0 ||
                    p.Size.X > tank.Width || p.Size.Y > tank.Height || p.Size.Z > tank.Depth)
                    return CommandResult.Fail("placement_invalid");
                PurchaseBatch batch = findBatch(p.PurchaseBatchId);
                if (batch == null || batch.Refunded || batch.Sold || !batches.Add(batch.Id))
                    return CommandResult.Fail("batch_unavailable");
                string kind = catalog.Items[batch.ItemId].Kind;
                if (kind != "plant" && kind != "hardscape" && kind != "equipment") return CommandResult.Fail("item_not_placeable");
                foreach (Vec3 c in Corners(p))
                    if (Math.Abs(c.X) > tank.Width / 2 + Tolerance || Math.Abs(c.Z) > tank.Depth / 2 + Tolerance)
                        return CommandResult.Fail("outside_tank", p.Id);
                double bottom = kind == "plant" ? p.Position.Y : p.Position.Y - p.Size.Y / 2;
                double top = kind == "plant" ? p.Position.Y + p.Size.Y : p.Position.Y + p.Size.Y / 2;
                if (bottom < -Tolerance || top > tank.Height + Tolerance) return CommandResult.Fail("outside_tank", p.Id);
                placements.Add(p.Id, p);
            }
            foreach (LayoutPlacement p in layout.Placements)
            {
                PurchaseBatch batch = findBatch(p.PurchaseBatchId); ItemDefinition item = catalog.Items[batch.ItemId];
                if (item.Kind == "plant")
                {
                    PlantPlacementRule rule; LayoutPlacement host;
                    if (!catalog.Plants.TryGetValue(item.Id, out rule)) return CommandResult.Fail("plant_rule_missing", item.Id);
                    if (rule.RhizomeExposed && p.RhizomeBuried) return CommandResult.Fail("rhizome_buried", p.Id);
                    if (string.IsNullOrEmpty(p.SupportPlacementId) || !placements.TryGetValue(p.SupportPlacementId, out host))
                        return CommandResult.Fail("support_required", p.Id);
                    ItemDefinition hostItem = catalog.Items[findBatch(host.PurchaseBatchId).ItemId];
                    string surface = hostItem.Kind == "hardscape" ? (hostItem.Id == "wood_optional" ? "wood" : "rock") : "unsupported";
                    if (Array.IndexOf(rule.AllowedSurfaces, surface) < 0 || !ContainsXZ(host, p.Position) ||
                        Math.Abs(p.Position.Y - host.Position.Y - host.Size.Y / 2) > .015 + Tolerance)
                        return CommandResult.Fail("support_invalid", p.Id);
                    if (p.Position.Y < Height(layout.Substrate, tank, p.Position.X, p.Position.Z) - Tolerance)
                        return CommandResult.Fail("plant_buried", p.Id);
                }
                else if (item.Kind == "hardscape")
                {
                    if (layout.Substrate == null || !string.IsNullOrEmpty(p.SupportPlacementId) ||
                        Math.Abs(p.Position.Y - p.Size.Y / 2 - BottomHeight(p, layout.Substrate, tank)) > .004 + Tolerance)
                        return CommandResult.Fail("support_invalid", p.Id);
                    foreach (LayoutPlacement other in layout.Placements)
                        if (other.Id != p.Id && catalog.Items[findBatch(other.PurchaseBatchId).ItemId].Kind == "hardscape" && Overlap(p, other))
                            return CommandResult.Fail("hardscape_overlap", p.Id, other.Id);
                }
                else if (!string.IsNullOrEmpty(p.SupportPlacementId)) return CommandResult.Fail("support_invalid", p.Id);
            }
            return CommandResult.Ok();
        }
        internal static double EffectiveLiters(LayoutState layout, GameplayCatalog catalog, TankBounds tank, double waterline,
            Func<string, PurchaseBatch> findBatch)
        {
            double cubicMetres = tank.Width * tank.Depth * waterline;
            if (layout.Substrate != null)
            {
                SubstrateProfile p = layout.Substrate;
                cubicMetres -= tank.Width * tank.Depth * (p.FrontLeftHeight + p.FrontRightHeight + p.BackLeftHeight + p.BackRightHeight) / 4;
            }
            foreach (LayoutPlacement p in layout.Placements)
                if (catalog.Items[findBatch(p.PurchaseBatchId).ItemId].Kind == "hardscape")
                    cubicMetres -= p.Size.X * p.Size.Z * Math.Max(0, Math.Min(p.Size.Y, waterline - (p.Position.Y - p.Size.Y / 2)));
            return cubicMetres * 1000;
        }
    }
}
