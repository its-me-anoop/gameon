using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OrbitOrchard.Core
{
    /// <summary>
    /// Deterministic catch/bank game. Input never changes the generated route.
    /// The owner pauses by withholding Update calls and handles services outside this model.
    /// </summary>
    public sealed class OrchardGame
    {
        public const double CatchRadius = 1.6;
        public const double SpawnRadius = 4.2;
        public const double CatchHalfAngle = 0.30;
        public const double Duration = 90;
        private const int TicksPerSecond = 120;
        private const double Step = 1.0 / TicksPerSecond;
        private const long FinalTick = 90 * TicksPerSecond;
        private const int MaximumQueuedEvents = 512;

        public ulong Seed { get; }
        public GameMode Mode { get; }
        public int Score { get; private set; }
        public int Carried { get; private set; }
        public int Streak { get; private set; }
        public int Hearts { get; private set; } = 3;
        public int BankedSeeds { get; private set; }
        public double CatcherAngle { get; private set; }
        public bool IsFinished { get; private set; }
        public int Multiplier => Math.Min(5, 1 + Streak / 5);
        public int BasketValue => Carried * 10 * Multiplier;
        public double Elapsed => tick * Step;
        public double TimeRemaining => Mode == GameMode.Practice ? double.PositiveInfinity : Math.Max(0, Duration - Elapsed);
        public IReadOnlyList<SeedBody> Bodies => bodyView;
        public IReadOnlyList<GameEvent> Events => eventView;

        private readonly List<SeedBody> bodies = new List<SeedBody>(8);
        private readonly List<GameEvent> events = new List<GameEvent>(8);
        private readonly ReadOnlyCollection<SeedBody> bodyView;
        private readonly ReadOnlyCollection<GameEvent> eventView;
        private long tick;
        private long nextSpawnTick = 60;
        private int nextBodyId;
        private double accumulator;
        private double lastSeedAngle;
        private SplitMix64 random;

        public OrchardGame(ulong seed, GameMode mode = GameMode.Classic)
        {
            Seed = seed;
            Mode = mode;
            random = new SplitMix64(seed);
            bodyView = bodies.AsReadOnly();
            eventView = events.AsReadOnly();
        }

        public void Steer(double angle)
        {
            if (IsFinished || !IsFinite(angle)) return;
            CatcherAngle = Normalize(angle);
        }

        /// <summary>
        /// Advances all radial crossings at120 fixed steps/s, independent of rendering.
        /// A single invalid day-long foreground interval is bounded to120 seconds of CPU work.
        /// </summary>
        public void Update(double dt)
        {
            if (IsFinished || !IsFinite(dt) || dt <= 0) return;
            accumulator += Math.Min(dt, 120);
            while (accumulator + 1e-10 >= Step && !IsFinished)
            {
                accumulator = Math.Max(0, accumulator - Step);
                tick++;
                AdvanceBodies();
                if (IsFinished) break;
                if (Mode != GameMode.Practice && tick >= FinalTick)
                {
                    Bank();
                    Finish();
                    break;
                }
                if (tick >= nextSpawnTick) SpawnBody();
            }
        }

        /// <summary>Secures the basket and starts a new combo. Stored points cannot be lost.</summary>
        public void Bank()
        {
            if (IsFinished || Carried == 0) return;
            var points = BasketValue;
            Score += points;
            BankedSeeds += Carried;
            Carried = 0;
            Streak = 0;
            Emit(new GameEvent(GameEventKind.Banked, amount: points, angle: CatcherAngle));
        }

        public GameEvent[] DrainEvents()
        {
            if (events.Count == 0) return Array.Empty<GameEvent>();
            var result = events.ToArray();
            events.Clear();
            return result;
        }

        private void AdvanceBodies()
        {
            // Compact surviving value objects in place: no garbage on ordinary simulation ticks.
            var survivorCount = 0;
            for (var index = 0; index < bodies.Count; index++)
            {
                var body = bodies[index].WithRadius(bodies[index].Radius - bodies[index].Speed * Step);
                if (body.Radius <= CatchRadius)
                {
                    Resolve(body.WithRadius(CatchRadius));
                    if (IsFinished) return;
                }
                else
                {
                    bodies[survivorCount] = body;
                    survivorCount++;
                }
            }
            if (survivorCount < bodies.Count) bodies.RemoveRange(survivorCount, bodies.Count - survivorCount);
        }

        private void Resolve(SeedBody body)
        {
            var touching = AngularDistance(body.Angle, CatcherAngle) <= CatchHalfAngle;
            if (body.Kind == SeedKind.Stone)
            {
                if (touching) Spill(GameEventKind.Hit, body);
            }
            else if (touching)
            {
                var value = body.Kind == SeedKind.GoldenSeed ? 3 : 1;
                Carried += value;
                Streak++;
                Emit(new GameEvent(GameEventKind.Caught, body, value, body.Angle));
            }
            else
            {
                Spill(GameEventKind.Missed, body);
            }
        }

        private void Spill(GameEventKind kind, SeedBody body)
        {
            var lostSeeds = Carried;
            Carried = 0;
            Streak = 0;
            if (Mode != GameMode.Practice) Hearts--;
            Emit(new GameEvent(kind, body, lostSeeds, body.Angle));
            if (Hearts == 0) Finish();
        }

        private void Finish()
        {
            if (IsFinished) return;
            IsFinished = true;
            bodies.Clear();
            Emit(new GameEvent(GameEventKind.Finished, amount: Score, angle: CatcherAngle));
        }

        private void SpawnBody()
        {
            var difficulty = Math.Min(1, Math.Max(0, (Elapsed - 15) / 65));
            var speed = 0.65 + 0.45 * difficulty;
            var kind = SeedKind.Seed;
            if (Elapsed > 15 && nextBodyId % 5 == 4) kind = SeedKind.Stone;
            else if (Elapsed > 15 && nextBodyId % 9 == 8) kind = SeedKind.GoldenSeed;

            var angle = SpawnAngle(kind, speed, difficulty);
            if (kind != SeedKind.Stone) lastSeedAngle = angle;
            bodies.Add(new SeedBody(nextBodyId, angle, SpawnRadius, speed, kind));
            nextBodyId++;
            nextSpawnTick = tick + (int)((1.25 - 0.57 * difficulty) * TicksPerSecond);
        }

        private double SpawnAngle(SeedKind kind, double speed, double difficulty)
        {
            if (nextBodyId == 0) return 0;
            var travelTime = (SpawnRadius - CatchRadius) / speed;
            var direction = random.UnitDouble() < 0.5 ? -1.0 : 1.0;
            var offset = kind == SeedKind.Stone
                ? direction * random.Range(0.95, 2.0)
                : random.Range(-(0.55 + 0.9 * difficulty), 0.55 + 0.9 * difficulty);
            var proposed = Normalize(lastSeedAngle + offset);
            // A deterministic angular search keeps near-simultaneous opponents
            // separated without consuming random rerolls or depending on player input.
            for (var candidateIndex = 0; candidateIndex < 24; candidateIndex++)
            {
                var candidate = Normalize(proposed + candidateIndex * Math.PI / 12);
                if (HasDodgeSpace(candidate, kind, travelTime)) return candidate;
            }
            // Spawn spacing permits at most two nearby opponents, so a gap exists.
            return proposed;
        }

        private bool HasDodgeSpace(double angle, SeedKind kind, double travelTime)
        {
            foreach (var body in bodies)
            {
                if ((body.Kind == SeedKind.Stone) == (kind == SeedKind.Stone)) continue;
                if (Math.Abs((body.Radius - CatchRadius) / body.Speed - travelTime) >= 1.15) continue;
                if (AngularDistance(angle, body.Angle) < 0.85) return false;
            }
            return true;
        }

        private void Emit(GameEvent value)
        {
            if (events.Count == MaximumQueuedEvents) events.RemoveAt(0);
            events.Add(value);
        }

        private static double Normalize(double angle)
        {
            var result = angle % (2 * Math.PI);
            return result < 0 ? result + 2 * Math.PI : result;
        }

        private static double AngularDistance(double left, double right)
        {
            var difference = Math.Abs(left - right);
            return Math.Min(difference, 2 * Math.PI - difference);
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
