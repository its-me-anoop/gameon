using System;

namespace OrbitOrchard.Core
{
    public enum GameMode { Classic, Daily, Practice }
    public enum SeedKind { Seed, GoldenSeed, Stone }
    public enum GameEventKind { Caught, Missed, Hit, Banked, Finished }

    /// <summary>An incoming object on a radial path; angles are radians in [0, 2π).</summary>
    public readonly struct SeedBody : IEquatable<SeedBody>
    {
        public int Id { get; }
        public double Angle { get; }
        public double Radius { get; }
        public double Speed { get; }
        public SeedKind Kind { get; }

        public SeedBody(int id, double angle, double radius, double speed, SeedKind kind)
        {
            Id = id;
            Angle = angle;
            Radius = radius;
            Speed = speed;
            Kind = kind;
        }

        internal SeedBody WithRadius(double radius) => new SeedBody(Id, Angle, radius, Speed, Kind);

        public bool Equals(SeedBody other) => Id == other.Id && Angle.Equals(other.Angle)
            && Radius.Equals(other.Radius) && Speed.Equals(other.Speed) && Kind == other.Kind;
        public override bool Equals(object obj) => obj is SeedBody other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Id;
                hash = hash * 397 ^ Angle.GetHashCode();
                hash = hash * 397 ^ Radius.GetHashCode();
                hash = hash * 397 ^ Speed.GetHashCode();
                return hash * 397 ^ (int)Kind;
            }
        }
    }

    /// <summary>Semantic presentation feedback, independent of Unity and native services.</summary>
    public readonly struct GameEvent : IEquatable<GameEvent>
    {
        public GameEventKind Kind { get; }
        public SeedBody? Body { get; }
        /// <summary>Seeds caught/spilled, points banked, or final score, according to Kind.</summary>
        public int Amount { get; }
        public double Angle { get; }

        public GameEvent(GameEventKind kind, SeedBody? body = null, int amount = 0, double angle = 0)
        {
            Kind = kind;
            Body = body;
            Amount = amount;
            Angle = angle;
        }

        public bool Equals(GameEvent other) => Kind == other.Kind && Nullable.Equals(Body, other.Body)
            && Amount == other.Amount && Angle.Equals(other.Angle);
        public override bool Equals(object obj) => obj is GameEvent other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind * 397 ^ Body.GetHashCode();
                hash = hash * 397 ^ Amount;
                return hash * 397 ^ Angle.GetHashCode();
            }
        }
    }
}
