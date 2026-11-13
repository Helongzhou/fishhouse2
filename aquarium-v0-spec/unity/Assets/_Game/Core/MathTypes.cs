using System;

namespace Aquarium.Core
{
    [Serializable]
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator *(Vec3 a, double b) { return new Vec3(a.X * b, a.Y * b, a.Z * b); }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public Vec3 Normalized { get { double n = Length; return n < 1e-9 ? new Vec3() : this * (1 / n); } }
        public static double Distance(Vec3 a, Vec3 b) { return (a - b).Length; }
        public static Vec3 Lerp(Vec3 a, Vec3 b, double t) { return a + (b - a) * Scalar.Clamp01(t); }
        public override string ToString() { return string.Format("({0:F3}, {1:F3}, {2:F3})", X, Y, Z); }
    }

    public static class Scalar
    {
        public static bool IsFinite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public static bool IsFinite(Vec3 p) { return IsFinite(p.X) && IsFinite(p.Y) && IsFinite(p.Z); }
        public static void RequireFinite(Vec3 p, string name)
        { if (!IsFinite(p)) throw new ArgumentException("Position must contain only finite values.", name); }
        public static double Clamp(double n, double min, double max) { return Math.Max(min, Math.Min(max, n)); }
        public static double Clamp01(double n) { return Clamp(n, 0, 1); }
    }

    // Every agent owns this state. No shared RNG and no UnityEngine.Random dependency.
    [Serializable]
    public struct SavedRandom
    {
        public uint State;
        public SavedRandom(uint seed) { State = seed == 0 ? 0x9E3779B9u : seed; }
        public uint NextUInt()
        {
            uint x = State == 0 ? 0x9E3779B9u : State;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            State = x;
            return x;
        }
        public double Unit() { return (NextUInt() + 0.5) / 4294967296.0; }
        public double Range(double min, double max) { return min + (max - min) * Unit(); }
        public bool Chance(double probability) { return Unit() < Scalar.Clamp01(probability); }
    }

    [Serializable]
    public sealed class TankBounds
    {
        // Metres. A 60 cm tank is not assumed to contain 60 litres.
        public double Width = 0.60, Height = 0.36, Depth = 0.30;
        public void Validate()
        {
            if (!Scalar.IsFinite(Width) || !Scalar.IsFinite(Height) || !Scalar.IsFinite(Depth) ||
                Width < .05 || Height < .05 || Depth < .05 || Width > 10 || Height > 10 || Depth > 10)
                throw new ArgumentException("Tank dimensions must be finite metres in the supported 0.05..10 range.");
        }
        public TankBounds Clone() { return (TankBounds)MemberwiseClone(); }
        public Vec3 Constrain(Vec3 p, SpeciesProfile species)
        {
            double r = species.BodyRadius;
            return new Vec3(Scalar.Clamp(p.X, -Width / 2 + r, Width / 2 - r),
                Scalar.Clamp(p.Y, Height * species.SwimLayerMin + r, Height * species.SwimLayerMax - r),
                Scalar.Clamp(p.Z, -Depth / 2 + r, Depth / 2 - r));
        }
        public bool Contains(Vec3 p, SpeciesProfile species)
        { return Vec3.Distance(p, Constrain(p, species)) < 1e-8; }
    }
}
