using System;
using System.Linq;
using NUnit.Framework;

namespace OrbitOrchard.Core.Tests
{
    public sealed class OrchardGameTests
    {
        [Test]
        public void NewHarvestStartsEmptyWithThreeHearts()
        {
            var game = new OrchardGame(42);
            Assert.That(game.Mode, Is.EqualTo(GameMode.Classic));
            Assert.That(game.Score, Is.Zero);
            Assert.That(game.Carried, Is.Zero);
            Assert.That(game.Hearts, Is.EqualTo(3));
            Assert.That(game.TimeRemaining, Is.EqualTo(90));
            Assert.That(game.IsFinished, Is.False);
        }

        [Test]
        public void SteeringWrapsAndIgnoresInvalidAngles()
        {
            var game = new OrchardGame(42);
            game.Steer(-0.1);
            Assert.That(game.CatcherAngle, Is.EqualTo(2 * Math.PI - 0.1).Within(1e-12));
            game.Steer(4 * Math.PI + 0.2);
            game.Steer(double.NaN);
            game.Steer(double.PositiveInfinity);
            Assert.That(game.CatcherAngle, Is.EqualTo(0.2).Within(1e-12));
        }

        [Test]
        public void CatchesOnlyBecomeScoreWhenBanked()
        {
            var game = new OrchardGame(42);
            CatchNextSeed(game);
            Assert.That(game.Carried, Is.EqualTo(1));
            Assert.That(game.Score, Is.Zero);
            Assert.That(game.Streak, Is.EqualTo(1));
            Assert.That(game.DrainEvents().Any(e => e.Kind == GameEventKind.Caught && e.Amount == 1), Is.True);
            game.Bank();
            Assert.That(game.Score, Is.EqualTo(10));
            Assert.That(game.Carried, Is.Zero);
            Assert.That(game.BankedSeeds, Is.EqualTo(1));
            Assert.That(game.Streak, Is.Zero);
            Assert.That(game.Multiplier, Is.EqualTo(1));
            Assert.That(game.DrainEvents().Single().Kind, Is.EqualTo(GameEventKind.Banked));
            game.Bank();
            Assert.That(game.DrainEvents(), Is.Empty);
        }

        [Test]
        public void FiveConsecutiveCatchesDoubleTheBank()
        {
            var game = new OrchardGame(19);
            for (var index = 0; index < 5; index++) CatchNextSeed(game);
            Assert.That(game.Carried, Is.EqualTo(5));
            Assert.That(game.Multiplier, Is.EqualTo(2));
            Assert.That(game.BasketValue, Is.EqualTo(100));
            game.Bank();
            Assert.That(game.Score, Is.EqualTo(100));
            Assert.That(game.Multiplier, Is.EqualTo(1));
        }

        [Test]
        public void MissingSpillsTheBasketAndPreservesBankedScore()
        {
            var game = new OrchardGame(42);
            CatchNextSeed(game);
            game.Bank();
            CatchNextSeed(game);
            MissNextSeed(game);
            Assert.That(game.Score, Is.EqualTo(10));
            Assert.That(game.Carried, Is.Zero);
            Assert.That(game.Streak, Is.Zero);
            Assert.That(game.Hearts, Is.EqualTo(2));
            Assert.That(game.Events.Any(e => e.Kind == GameEventKind.Missed), Is.True);
        }

        [Test]
        public void CatchingAcrossTheAngleSeamWorks()
        {
            var game = new OrchardGame(42);
            game.Steer(2 * Math.PI - 0.05);
            while (game.Elapsed < 5 && game.Carried == 0) game.Update(1.0 / 60);
            Assert.That(game.Carried, Is.EqualTo(1));
        }

        [Test]
        public void ALongFrameCannotTunnelThroughTheCatcher()
        {
            var game = new OrchardGame(42);
            game.Update(0.5);
            var seed = game.Bodies[0];
            game.Steer(seed.Angle);
            game.Update((seed.Radius - OrchardGame.CatchRadius) / seed.Speed + 0.02);
            Assert.That(game.Carried, Is.GreaterThanOrEqualTo(1));
            Assert.That(game.Bodies.Any(b => b.Id == seed.Id), Is.False);
            Assert.That(game.Events.Any(e => e.Kind == GameEventKind.Caught && e.Body.HasValue && e.Body.Value.Id == seed.Id), Is.True);
        }

        [Test]
        public void ThreeMissesFinishExactlyOnceAndFreezeTheGame()
        {
            var game = new OrchardGame(42);
            for (var index = 0; index < 3; index++) MissNextSeed(game);
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.Hearts, Is.Zero);
            Assert.That(game.DrainEvents().Count(e => e.Kind == GameEventKind.Finished), Is.EqualTo(1));
            var elapsed = game.Elapsed;
            var angle = game.CatcherAngle;
            game.Update(10);
            game.Steer(angle + 1);
            game.Bank();
            Assert.That(game.Elapsed, Is.EqualTo(elapsed));
            Assert.That(game.CatcherAngle, Is.EqualTo(angle));
            Assert.That(game.DrainEvents(), Is.Empty);
        }

        [Test]
        public void PracticeNeverRunsOutOfTimeOrHearts()
        {
            var game = new OrchardGame(42, GameMode.Practice);
            for (var index = 0; index < 5; index++) MissNextSeed(game);
            game.Update(100);
            Assert.That(game.Hearts, Is.EqualTo(3));
            Assert.That(game.TimeRemaining, Is.EqualTo(double.PositiveInfinity));
            Assert.That(game.Elapsed, Is.GreaterThan(90));
            Assert.That(game.IsFinished, Is.False);
        }

        [Test]
        public void OpeningHasNoHazardsAndAReadableApproachLane()
        {
            var game = new OrchardGame(5, GameMode.Practice);
            var seen = new System.Collections.Generic.HashSet<int>();
            var angles = new System.Collections.Generic.List<double>();
            for (var frame = 0; frame < 1800; frame++)
            {
                game.Update(1.0 / 120);
                foreach (var body in game.Bodies)
                {
                    Assert.That(body.Radius, Is.GreaterThan(OrchardGame.CatchRadius));
                    Assert.That(body.Radius, Is.LessThanOrEqualTo(OrchardGame.SpawnRadius));
                    if (seen.Add(body.Id))
                    {
                        Assert.That(body.Kind, Is.EqualTo(SeedKind.Seed));
                        angles.Add(body.Angle);
                    }
                }
            }
            Assert.That(angles.Count, Is.GreaterThanOrEqualTo(8));
            Assert.That(angles[0], Is.Zero);
            for (var index = 1; index < angles.Count; index++)
                Assert.That(AngularDistance(angles[index], angles[index - 1]), Is.LessThanOrEqualTo(0.65));
        }

        [Test]
        public void PerfectHarvestAutoBanksAndMatchesTheSwiftGoldenFixture()
        {
            // Generated by the Swift implementation on 2026-09-11, using seed17
            // and exactly the same nearest-arrival pilot at120 simulation steps/s.
            var game = new OrchardGame(17);
            Pilot(game, 91);
            Assert.That(game.IsFinished, Is.True);
            Assert.That(game.Elapsed, Is.EqualTo(90));
            Assert.That(game.TimeRemaining, Is.Zero);
            Assert.That(game.Score, Is.EqualTo(4550));
            Assert.That(game.BankedSeeds, Is.EqualTo(91));
            Assert.That(game.Hearts, Is.EqualTo(3));
            Assert.That(game.Carried, Is.Zero);
            var events = game.DrainEvents();
            Assert.That(events[events.Length - 2].Kind, Is.EqualTo(GameEventKind.Banked));
            Assert.That(events[events.Length - 1].Kind, Is.EqualTo(GameEventKind.Finished));
        }

        [Test]
        public void GoldenSeedsFillThreeSlotsAndComboCapsAtFive()
        {
            var game = new OrchardGame(17, GameMode.Practice);
            Pilot(game, 65);
            Assert.That(game.Events.Any(e => e.Kind == GameEventKind.Caught && e.Body.HasValue && e.Body.Value.Kind == SeedKind.GoldenSeed && e.Amount == 3), Is.True);
            Assert.That(game.Carried, Is.GreaterThan(game.Streak));
            Assert.That(game.Streak, Is.GreaterThan(25));
            Assert.That(game.Multiplier, Is.EqualTo(5));
        }

        [Test]
        public void StoneCollisionHasADistinctEventAndCostsAHeart()
        {
            var game = new OrchardGame(17);
            while (game.Elapsed < 40 && !game.Events.Any(e => e.Kind == GameEventKind.Hit))
            {
                var next = NextBody(game);
                if (next.HasValue) game.Steer(next.Value.Angle);
                game.Update(1.0 / 120);
            }
            Assert.That(game.Events.Any(e => e.Kind == GameEventKind.Hit), Is.True);
            Assert.That(game.Hearts, Is.EqualTo(2));
            Assert.That(game.Carried, Is.Zero);
        }

        [TestCase(1ul)]
        [TestCase(42ul)]
        [TestCase(9731ul)]
        [TestCase(99999ul)]
        public void NearSimultaneousSeedAndStoneArrivalsHaveDodgeSpace(ulong seed)
        {
            var game = new OrchardGame(seed, GameMode.Practice);
            for (var frame = 0; frame < 10800; frame++)
            {
                game.Update(1.0 / 120);
                foreach (var stone in game.Bodies.Where(b => b.Kind == SeedKind.Stone))
                foreach (var fruit in game.Bodies.Where(b => b.Kind != SeedKind.Stone))
                {
                    var stoneArrival = (stone.Radius - OrchardGame.CatchRadius) / stone.Speed;
                    var fruitArrival = (fruit.Radius - OrchardGame.CatchRadius) / fruit.Speed;
                    if (Math.Abs(stoneArrival - fruitArrival) < 1.1)
                        Assert.That(AngularDistance(stone.Angle, fruit.Angle), Is.GreaterThanOrEqualTo(0.8));
                }
                game.DrainEvents();
            }
        }

        [Test]
        public void RenderFrameRateDoesNotChangeSimulation()
        {
            var sixty = new OrchardGame(77, GameMode.Practice);
            var oneTwenty = new OrchardGame(77, GameMode.Practice);
            var singleFrame = new OrchardGame(77, GameMode.Practice);
            for (var frame = 0; frame < 600; frame++) sixty.Update(1.0 / 60);
            for (var frame = 0; frame < 1200; frame++) oneTwenty.Update(1.0 / 120);
            singleFrame.Update(10);
            CollectionAssert.AreEqual(sixty.Bodies, oneTwenty.Bodies);
            CollectionAssert.AreEqual(sixty.Bodies, singleFrame.Bodies);
            CollectionAssert.AreEqual(sixty.DrainEvents(), singleFrame.DrainEvents());
            Assert.That(sixty.Elapsed, Is.EqualTo(singleFrame.Elapsed));
        }

        [Test]
        public void PlayerInputCannotChangeTheSeedRoute()
        {
            var still = new OrchardGame(19, GameMode.Practice);
            var moving = new OrchardGame(19, GameMode.Practice);
            for (var frame = 0; frame < 4800; frame++)
            {
                moving.Steer(frame * 0.09);
                if (frame % 120 == 0) moving.Bank();
                still.Update(1.0 / 120);
                moving.Update(1.0 / 120);
                CollectionAssert.AreEqual(still.Bodies, moving.Bodies);
            }
        }

        [Test]
        public void InvalidFrameTimesAreIgnoredAndPracticeEventsStayBounded()
        {
            var game = new OrchardGame(19, GameMode.Practice);
            foreach (var dt in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity }) game.Update(dt);
            Assert.That(game.Elapsed, Is.Zero);
            Assert.That(game.Bodies, Is.Empty);
            for (var frame = 0; frame < 11; frame++) game.Update(100);
            Assert.That(game.Events.Count, Is.EqualTo(512));
        }

        [Test]
        public void TenSecondSnapshotMatchesTheSwiftGoldenFixture()
        {
            // Captured directly from OrchardGame.swift(seed:42,mode:.practice).
            var game = new OrchardGame(42, GameMode.Practice);
            game.Update(10);
            Assert.That(game.Carried, Is.EqualTo(2));
            Assert.That(game.Streak, Is.EqualTo(2));
            Assert.That(game.Score, Is.Zero);
            Assert.That(game.Bodies.Count, Is.EqualTo(3));
            var angles = new[] { 0.32058744191840038, 0.31287554629256209, 0.33489017585612624 };
            var radii = new[] { 2.0875000000000004, 2.8999999999999941, 3.7124999999999879 };
            for (var index = 0; index < 3; index++)
            {
                Assert.That(game.Bodies[index].Id, Is.EqualTo(index + 5));
                Assert.That(game.Bodies[index].Angle, Is.EqualTo(angles[index]).Within(1e-12));
                Assert.That(game.Bodies[index].Radius, Is.EqualTo(radii[index]).Within(1e-11));
                Assert.That(game.Bodies[index].Speed, Is.EqualTo(0.65));
                Assert.That(game.Bodies[index].Kind, Is.EqualTo(SeedKind.Seed));
            }
            CollectionAssert.AreEqual(new[] { GameEventKind.Caught, GameEventKind.Missed, GameEventKind.Missed, GameEventKind.Caught, GameEventKind.Caught }, game.Events.Select(e => e.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1, 0, 1, 1 }, game.Events.Select(e => e.Amount).ToArray());
        }

        private static void Pilot(OrchardGame game, double until)
        {
            while (!game.IsFinished && game.Elapsed < until)
            {
                var next = NextBody(game);
                if (next.HasValue) game.Steer(next.Value.Kind == SeedKind.Stone ? next.Value.Angle + Math.PI : next.Value.Angle);
                game.Update(1.0 / 120);
            }
        }

        private static void CatchNextSeed(OrchardGame game)
        {
            var before = game.Streak;
            var deadline = game.Elapsed + 10;
            while (!game.IsFinished && game.Streak == before && game.Elapsed < deadline)
            {
                var next = NextBody(game);
                if (next.HasValue) game.Steer(next.Value.Kind == SeedKind.Stone ? next.Value.Angle + Math.PI : next.Value.Angle);
                game.Update(1.0 / 120);
            }
            Assert.That(game.Streak, Is.GreaterThan(before), "A reachable seed must arrive before the helper deadline.");
        }

        private static void MissNextSeed(OrchardGame game)
        {
            game.DrainEvents();
            var deadline = game.Elapsed + 10;
            while (!game.IsFinished && game.Elapsed < deadline)
            {
                var next = NextBody(game);
                if (next.HasValue) game.Steer(next.Value.Angle + Math.PI);
                game.Update(1.0 / 120);
                if (game.Events.Any(e => e.Kind == GameEventKind.Missed)) return;
            }
            Assert.Fail("A missed seed must be reported before the helper deadline.");
        }

        private static SeedBody? NextBody(OrchardGame game)
        {
            SeedBody? next = null;
            var nearest = double.PositiveInfinity;
            foreach (var body in game.Bodies)
            {
                var arrival = (body.Radius - OrchardGame.CatchRadius) / body.Speed;
                if (arrival < nearest) { nearest = arrival; next = body; }
            }
            return next;
        }

        private static double AngularDistance(double left, double right)
        {
            var difference = Math.Abs(left - right) % (2 * Math.PI);
            return Math.Min(difference, 2 * Math.PI - difference);
        }
    }
}
