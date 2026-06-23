using System;

namespace Proto.Core
{
    /// <summary>
    /// The pure core's only math type — a 2D vector with no <c>UnityEngine</c> dependency, so game rules
    /// stay headlessly testable. The presentation layer maps it to world space however the proto needs
    /// (the convention here: X → world X, Y → world Z, i.e. the ground plane).
    /// </summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float Length => MathF.Sqrt(X * X + Y * Y);
        public float SqrLength => X * X + Y * Y;

        public Vec2 Normalized
        {
            get
            {
                float len = Length;
                return len < 1e-6f ? Zero : new Vec2(X / len, Y / len);
            }
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        /// <summary>Unsigned angle between two vectors in degrees, in [0, 180]. Returns 180 for a zero vector so it never matches.</summary>
        public static float AngleDeg(Vec2 a, Vec2 b)
        {
            float denom = a.Length * b.Length;
            if (denom < 1e-9f) return 180f;
            float cos = Math.Clamp(Dot(a, b) / denom, -1f, 1f);
            return MathF.Acos(cos) * (180f / MathF.PI);
        }

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Vec2 v && Equals(v);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
