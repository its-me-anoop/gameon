using System.Linq;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace IdleClinic.Tests
{
    public sealed class DoctorsResumeWorldTests
    {
        private GameObject host;
        private ClinicWorld world;

        [SetUp] public void SetUp()
        {
            host = new GameObject("Doctors resume verification");
            world = host.AddComponent<ClinicWorld>();
            world.Initialize(ClinicLocation.DoctorsClinic);
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(host);

        [TestCase(ClinicPatientPhase.WalkingToTreatment, false)]
        [TestCase(ClinicPatientPhase.WalkingToTreatment, true)]
        [TestCase(ClinicPatientPhase.Leaving, false)]
        [TestCase(ClinicPatientPhase.Leaving, true)]
        public void OfflineProgressIntoALaterJourneyResumesAtItsCurrentPosition(ClinicPatientPhase laterPhase, bool reduced)
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            WaitForPhase(game, ClinicPatientPhase.Consulting, false);
            world.Render(game.State, 0, reduced);
            var actor = PatientZero();
            var previousPosition = actor.position;
            world.Zoom(.75f, new Vector2(.35f, .6f));
            world.Pan(new Vector2(.5f, .5f), new Vector2(.6f, .4f));
            var cameraPosition = world.SceneCamera.transform.position;
            var cameraRotation = world.SceneCamera.transform.rotation;
            var cameraSize = world.SceneCamera.orthographicSize;
            var texture = world.Texture;
            int objectCount = host.GetComponentsInChildren<Transform>(true).Length;

            // No renders occur while away: this actor is still in consultation,
            // although real offline simulation advances it into the later journey.
            WaitForPhase(game, laterPhase, true);
            var patient = game.State.Patients.Single(p => p.Id == 0);
            game.AdvanceOffline((patient.PhaseEndsTick - game.State.Tick) / 20d);
            Assert.That(patient.Phase, Is.EqualTo(laterPhase));
            var expected = Position(game.State, patient);
            Assert.That(Vector3.Distance(previousPosition, expected), Is.GreaterThan(1));
            var savedState = JsonUtility.ToJson(game.State);

            world.ConfigureLocation(game.State.Location);
            world.ResetActorPlacement();
            Assert.That(host.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
            world.Render(game.State, 0, reduced);

            Assert.That(PatientZero(), Is.SameAs(actor));
            Assert.That(Vector3.Distance(actor.position, expected), Is.LessThan(.002f),
                "Resume must use current phase progress, not the pre-background consultation pose.");
            Assert.That(world.Texture, Is.SameAs(texture));
            Assert.That(world.SceneCamera.transform.position, Is.EqualTo(cameraPosition));
            Assert.That(world.SceneCamera.transform.rotation, Is.EqualTo(cameraRotation));
            Assert.That(world.SceneCamera.orthographicSize, Is.EqualTo(cameraSize));
            Assert.That(JsonUtility.ToJson(game.State), Is.EqualTo(savedState), "Presentation cannot rewrite phase clocks or reservations.");

            for (int frame = 0; frame < 5; frame++)
            {
                var before = actor.position;
                game.Advance(.1, false);
                world.Render(game.State, .1f, reduced);
                Assert.That(Vector3.Distance(actor.position, Position(game.State, patient)), Is.LessThan(.002f));
                Assert.That(Vector3.Distance(actor.position, before), Is.LessThanOrEqualTo(.171f),
                    "The resumed actor must retain normal walking speed instead of compressing the skipped journey.");
            }
        }

        [Test] public void ResetPreservesTheSavedArrivalRouteAndItsFractionalProgress()
        {
            var game = ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            world.Render(game.State, 0);
            var actor = PatientZero();
            var patient = game.State.Patients.Single(p => p.Id == 0);
            game.AdvanceOffline((patient.PhaseEndsTick - game.State.Tick) / 25d + .037);
            Assert.That(patient.Phase, Is.EqualTo(ClinicPatientPhase.Arriving));
            var route = patient.ArrivalPath;
            var savedState = JsonUtility.ToJson(game.State);
            var expected = Position(game.State, patient);

            world.ResetActorPlacement();
            world.Render(game.State, 0);

            Assert.That(PatientZero(), Is.SameAs(actor));
            Assert.That(patient.ArrivalPath, Is.SameAs(route));
            Assert.That(Vector3.Distance(actor.position, expected), Is.LessThan(.002f));
            Assert.That(JsonUtility.ToJson(game.State), Is.EqualTo(savedState));
        }

        private Transform PatientZero() => host.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Patient 0");

        private Vector3 Position(ClinicState state, ClinicPatientState patient)
        {
            var path = patient.Phase == ClinicPatientPhase.Arriving ? patient.ArrivalPath
                : ClinicDoctorsNavigation.ArrivalPath(patient.FromAnchor, patient.ToAnchor);
            double progress = (state.Tick + state.SubTick - patient.PhaseStartedTick)
                / (patient.PhaseEndsTick - patient.PhaseStartedTick);
            var point = ClinicDoctorsNavigation.SampleArrivalPath(path, progress);
            return new Vector3(point.X, world.GetAnchorPoint(patient.ToAnchor).y, point.Z);
        }

        private static void WaitForPhase(ClinicSimulation game, ClinicPatientPhase phase, bool offline)
        {
            for (int step = 0; step < 12000; step++)
            {
                var patient = game.State.Patients.FirstOrDefault(p => p.Id == 0);
                Assert.That(patient, Is.Not.Null, "The first patient's journey ended before " + phase);
                if (patient.Phase == phase) return;
                if (offline) game.AdvanceOffline(.1); else game.Advance(.1, false);
            }
            Assert.Fail("The first patient did not reach " + phase);
        }
    }
}
