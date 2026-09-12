using System;
using System.Collections.Generic;
using System.Linq;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class DoctorsTaxiQueueWorldTests
    {
        private GameObject host;
        private ClinicWorld world;
        private ClinicState state;

        [SetUp] public void SetUp()
        {
            host = new GameObject("Doctors taxi waiting verification");
            world = host.AddComponent<ClinicWorld>();
            world.Initialize(ClinicLocation.DoctorsClinic);
            state = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic).State;
            state.Staff.Clear(); state.Patients.Clear(); state.TaxiRides.Clear();
            state.Amenity(ClinicAmenity.Taxi).Level = 6;
        }

        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

        [Test] public void WaitingReservationsUseSeparateSharedAnchorsAndKeepTheCurbFree()
        {
            AddWaiters(); world.Render(state, 0);
            var saved = JsonUtility.ToJson(state);
            for (int slot = 0; slot < ClinicRules.TaxiWaitingCapacity; slot++)
            {
                var name = ClinicRules.TaxiWaitingAnchor(slot);
                var point = ClinicDoctorsNavigation.Anchor(name);
                var actor = Actor(slot);
                Assert.That(actor.position.x, Is.EqualTo(point.x).Within(.002f));
                Assert.That(actor.position.z, Is.EqualTo(point.z).Within(.002f));
                Assert.That(actor.position, Is.EqualTo(world.GetAnchorPoint(name)));
                Assert.That(Vector3.Dot(actor.forward, Vector3.back), Is.GreaterThan(.99f));
                Assert.That(actor.gameObject.activeInHierarchy, Is.True);
                for (int other = 0; other < slot; other++)
                    Assert.That(Vector3.Distance(actor.position, Actor(other).position), Is.GreaterThan(.70f));
                for (int dock = 0; dock < 2; dock++)
                    Assert.That(Vector3.Distance(actor.position, world.GetAnchorPoint(ClinicRules.TaxiPatientAnchor(dock))), Is.GreaterThan(2));
            }
            Assert.That(world.MovingActorCount, Is.Zero);
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(saved), "Rendering must not assign or release waiting reservations.");
        }

        [Test] public void EveryWaitingAndBoardingRouteClearsStandingPeopleAndActualFurnishings()
        {
            world.Render(state, 0);
            var obstacles = host.GetComponentsInChildren<Renderer>()
                .Where(r => r.bounds.max.x > 14.4f && r.bounds.min.x < 27f
                    && r.bounds.max.z > -10.3f && r.bounds.min.z < -5.9f
                    && r.bounds.max.y > .30f && r.bounds.min.y < 1.60f)
                .Select(r => (r.name, r.bounds)).ToArray();
            Assert.That(obstacles.Any(o => o.name == "Taxi waiting bench"), Is.True);
            Assert.That(obstacles.Any(o => o.name == "Taxi shelter upright"), Is.True);
            Assert.That(obstacles.Any(o => o.name == "Notice board oak frame"), Is.True);
            Assert.That(obstacles.Any(o => o.name == "Taxi routes board support"), Is.True);
            var pavement = host.GetComponentsInChildren<Transform>().Single(t => t.name == "Taxi waiting promenade")
                .GetComponent<Renderer>().bounds;
            for (int slot = 0; slot < ClinicRules.TaxiWaitingCapacity; slot++)
            {
                var routes = new List<List<ClinicMovementPoint>>();
                for (int station = 0; station < 2; station++)
                    routes.Add(ClinicDoctorsNavigation.ArrivalPath(ClinicRules.StationPatientAnchor(ClinicStaffRole.Pharmacist, station), ClinicRules.TaxiWaitingAnchor(slot)));
                for (int dock = 0; dock < 2; dock++)
                    routes.Add(ClinicDoctorsNavigation.ArrivalPath(ClinicRules.TaxiWaitingAnchor(slot), ClinicRules.TaxiPatientAnchor(dock)));
                foreach (var path in routes)
                {
                    for (int sample = 0; sample <= 300; sample++)
                    {
                        var point = ClinicDoctorsNavigation.SampleArrivalPath(path, sample / 300d);
                        if (point.X < 14.4f || point.Z < -10.3f) continue;
                        var body = new Bounds(new Vector3(point.X, .94f, point.Z), new Vector3(.70f, 1.60f, .70f));
                        foreach (var obstacle in obstacles)
                            Assert.That(obstacle.bounds.Intersects(body), Is.False,
                                "Taxi route for slot " + slot + " clips " + obstacle.name + " at " + point.X + "," + point.Z);
                        for (int other = 0; other < ClinicRules.TaxiWaitingCapacity; other++)
                        {
                            if (other == slot) continue;
                            var waiting = world.GetAnchorPoint(ClinicRules.TaxiWaitingAnchor(other));
                            Assert.That(Vector2.Distance(new Vector2(point.X, point.Z), new Vector2(waiting.x, waiting.z)), Is.GreaterThan(.70f),
                                "Moving passenger crosses occupied taxi waiting slot " + other);
                        }
                        if (point.Z >= -8.5f)
                        {
                            Assert.That(point.X, Is.InRange(pavement.min.x + .35f, pavement.max.x - .35f));
                            Assert.That(point.Z, Is.InRange(pavement.min.z + .35f, pavement.max.z - .35f));
                        }
                    }
                }
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void CalledPassengerWalksAloneToItsTaxiBeforeHidingForBoarding(bool reduced)
        {
            AddWaiters();
            // These fixtures isolate projection of the authoritative call. Core tests
            // separately prove admission limits and serialization of calls across docks.
            foreach (int slot in new[] { 0, 3, 7 })
            {
                foreach (var patient in state.Patients)
                {
                    patient.Phase = ClinicPatientPhase.WaitingForTaxi;
                    patient.FromAnchor = patient.ToAnchor = ClinicRules.TaxiWaitingAnchor(patient.TaxiWaitingSlot);
                    patient.PhaseStartedTick = patient.PhaseEndsTick = 0;
                }
                state.Tick = 0; state.SubTick = 0; state.TaxiRides.Clear();
                world.ResetActorPlacement(); world.Render(state, 0, reduced);
                var actors = state.Patients.Select((p, i) => Actor(i)).ToArray();
                var called = state.Patients[slot];
                int dock = slot % 2;
                called.Phase = ClinicPatientPhase.WalkingToTaxiBoarding;
                called.ToAnchor = ClinicRules.TaxiPatientAnchor(dock);
                called.PhaseEndsTick = ClinicDoctorsNavigation.WalkTicks(called.FromAnchor, called.ToAnchor);
                var ride = new ClinicTaxiState { Id = called.Id * 2L + 1, PatientId = called.Id, DockId = dock,
                    Pickup = true, Phase = ClinicTaxiPhase.WaitingForPassenger };
                state.TaxiRides.Add(ride);
                world.Render(state, 0, reduced);
                var actor = actors[slot];
                var foot = actor.GetComponentInChildren<SkinnedMeshRenderer>().bones.Single(b => b.name == "foot.L");
                var firstFoot = actor.InverseTransformPoint(foot.position);
                float articulation = 0;
                var path = ClinicDoctorsNavigation.ArrivalPath(called.FromAnchor, called.ToAnchor);
                var taxi = host.GetComponentsInChildren<Transform>().Single(t => t.name == "Patient taxi " + dock);
                var parked = taxi.position;
                for (int tick = 1; tick <= called.PhaseEndsTick; tick++)
                {
                    var previous = actor.position;
                    state.Tick = tick; world.Render(state, .1f, reduced);
                    var expected = ClinicDoctorsNavigation.SampleArrivalPath(path, tick / (double)called.PhaseEndsTick);
                    Assert.That(actor.gameObject.activeInHierarchy, Is.True, "A called passenger remains visible throughout the walk.");
                    Assert.That(actor.position.x, Is.EqualTo(expected.X).Within(.002f));
                    Assert.That(actor.position.z, Is.EqualTo(expected.Z).Within(.002f));
                    Assert.That(Vector3.Distance(previous, actor.position), Is.LessThanOrEqualTo(.171f));
                    Assert.That(taxi.position, Is.EqualTo(parked), "Taxi waits for the actual passenger walk.");
                    Assert.That(world.MovingActorCount, Is.EqualTo(tick < called.PhaseEndsTick ? 1 : 0));
                    articulation = Mathf.Max(articulation, Vector3.Distance(firstFoot, actor.InverseTransformPoint(foot.position)));
                    for (int other = 0; other < state.Patients.Count; other++)
                    {
                        if (other == slot) continue;
                        Assert.That(actors[other].position, Is.EqualTo(world.GetAnchorPoint(ClinicRules.TaxiWaitingAnchor(other))));
                        Assert.That(Vector3.Distance(actor.position, actors[other].position), Is.GreaterThan(.70f));
                    }
                }
                Assert.That(articulation, Is.GreaterThan(.03f), "Essential walking remains articulated with reduced motion enabled.");
                Assert.That(Vector3.Distance(actor.position, world.GetAnchorPoint(called.ToAnchor)), Is.LessThan(.002f));
                called.Phase = ClinicPatientPhase.TaxiPickingUp; ride.Phase = ClinicTaxiPhase.Boarding;
                world.Render(state, 0, reduced);
                Assert.That(actor.gameObject.activeSelf, Is.False, "Hide only after the passenger reaches the vehicle.");
                Assert.That(world.MovingActorCount, Is.Zero);
            }
        }

        [Test] public void ResumedBoardingWalkUsesSavedProgressAndPreservesOtherReservations()
        {
            AddWaiters(); world.Render(state, 0);
            var patient = state.Patients[7];
            patient.Phase = ClinicPatientPhase.WalkingToTaxiBoarding;
            patient.ToAnchor = ClinicRules.TaxiPatientAnchor(0);
            patient.PhaseStartedTick = 50;
            patient.PhaseEndsTick = 50 + ClinicDoctorsNavigation.WalkTicks(patient.FromAnchor, patient.ToAnchor);
            state.Tick = 50 + (patient.PhaseEndsTick - 50) / 2; state.SubTick = .5;
            var saved = JsonUtility.ToJson(state);
            world.ResetActorPlacement(); world.Render(state, 0);
            var path = ClinicDoctorsNavigation.ArrivalPath(patient.FromAnchor, patient.ToAnchor);
            var expected = ClinicDoctorsNavigation.SampleArrivalPath(path,
                (state.Tick + state.SubTick - 50) / (patient.PhaseEndsTick - 50));
            Assert.That(Actor(7).position.x, Is.EqualTo(expected.X).Within(.002f));
            Assert.That(Actor(7).position.z, Is.EqualTo(expected.Z).Within(.002f));
            Assert.That(world.MovingActorCount, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(state), Is.EqualTo(saved));
        }

        private void AddWaiters()
        {
            for (int slot = 0; slot < ClinicRules.TaxiWaitingCapacity; slot++)
                state.Patients.Add(new ClinicPatientState { Id = 980 + slot, AppearanceId = slot,
                    Phase = ClinicPatientPhase.WaitingForTaxi, UsesTaxi = true, TaxiDockId = slot % 2,
                    TaxiWaitingReserved = true, TaxiWaitingSlot = slot,
                    FromAnchor = ClinicRules.TaxiWaitingAnchor(slot), ToAnchor = ClinicRules.TaxiWaitingAnchor(slot) });
        }

        private Transform Actor(int slot) => host.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Patient " + (980 + slot));
    }
}
