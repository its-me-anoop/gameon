using System;
using NUnit.Framework;
using OrbitOrchard.Core;
using OrbitOrchard.Presentation;
using UnityEngine;

namespace OrbitOrchard.Tests
{
    public sealed class OrchardWorldTests
    {
        private GameObject host;
        private OrchardWorld world;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Renderer test");
            world = host.AddComponent<OrchardWorld>();
            world.Initialize();
            world.SetRenderSize(512, 512);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
        }

        [Test]
        public void AuthoredBlenderModelsAreIncludedInTheRuntimeResources()
        {
            foreach (string model in new[] { "Island", "Gardener", "Seed", "GoldenSeed", "Stone" })
            {
                var asset = Resources.Load<GameObject>("OrbitOrchard/Models/" + model);
                Assert.That(asset, Is.Not.Null, "Missing Blender model: " + model);
                Assert.That(asset.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0));
            }
        }

        [Test]
        public void ImportedSculpturesUseGameplayUnitsAndHaveVerticalHeight()
        {
            Bounds island = BoundsOf(host.transform.Find("Sculptures/Island"));
            Bounds gardener = BoundsOf(host.transform.Find("Sculptures/Gardener"));
            Assert.That(island.size.x, Is.InRange(1.8f, 2.4f));
            Assert.That(island.size.y, Is.InRange(2.3f, 3.2f));
            Assert.That(gardener.size.x, Is.InRange(0.45f, 0.75f));
            Assert.That(gardener.size.y, Is.InRange(0.60f, 0.85f));
        }

        [Test]
        public void TouchingTheRenderedRingRecoversItsAngle()
        {
            for (int index = 0; index < 32; index++)
            {
                double angle = index * Math.PI * 2 / 32;
                var position = new Vector3((float)Math.Cos(angle) * 1.6f, 0, (float)Math.Sin(angle) * 1.6f);
                var point = world.SceneCamera.WorldToViewportPoint(position);
                Assert.That(world.ViewportPointToAngle(point, out var recovered), Is.True);
                AssertSteeringAccuracy(angle, recovered, point);
            }
        }

        [Test]
        public void NormalizedSteeringWorksAcrossDifferentBoardAspectRatios()
        {
            foreach (Vector2Int size in new[] { new Vector2Int(320, 430), new Vector2Int(800, 400) })
            {
                world.SetRenderSize(size.x, size.y);
                for (int index = 0; index < 16; index++)
                {
                    double angle = index * Math.PI * 2 / 16;
                    var position = new Vector3((float)Math.Cos(angle) * 1.6f, 0, (float)Math.Sin(angle) * 1.6f);
                    var point = world.SceneCamera.WorldToViewportPoint(position);
                    Assert.That(world.ViewportPointToAngle(point, out double recovered), Is.True);
                    AssertSteeringAccuracy(angle, recovered, point);
                }
            }
        }

        [Test]
        public void ResizingTheBoardTextureKeepsIncomingObjectsInFrame()
        {
            var game = new OrchardGame(42, GameMode.Practice);
            foreach (Vector2Int size in new[] { new Vector2Int(800, 400), new Vector2Int(400, 800) })
            {
                world.SetRenderSize(size.x, size.y);
                world.Render(game, 0, "orchard", true);
                Assert.That(world.SceneCamera.aspect, Is.EqualTo(size.x / (float)size.y).Within(0.001));
                for (int index = 0; index < 32; index++)
                {
                    float angle = index / 32f * Mathf.PI * 2;
                    Vector3 point = world.SceneCamera.WorldToViewportPoint(new Vector3(Mathf.Cos(angle) * 2.9f, 0, Mathf.Sin(angle) * 2.9f));
                    Assert.That(point.x, Is.InRange(0.04f, 0.96f));
                    Assert.That(point.y, Is.InRange(0.04f, 0.96f));
                }
            }
        }

        [Test]
        public void TheBoardReusesItsTextureUntilThePixelSizeChanges()
        {
            RenderTexture first = world.SetRenderSize(600, 450);
            Assert.That(world.SetRenderSize(600, 450), Is.SameAs(first));
            RenderTexture larger = world.SetRenderSize(2400, 1800);
            Assert.That(larger, Is.Not.SameAs(first));
            Assert.That(larger.width, Is.LessThanOrEqualTo(1536));
            Assert.That(larger.width / (float)larger.height, Is.EqualTo(4f / 3).Within(0.001));
            Assert.That(world.SceneCamera.targetTexture, Is.SameAs(larger));
        }

        [Test]
        public void TouchingTheCenterDoesNotCauseAnUnstableAngle()
        {
            Vector3 center = world.SceneCamera.WorldToViewportPoint(Vector3.zero);
            Assert.That(world.ViewportPointToAngle(center, out _), Is.False);
            Assert.That(world.ViewportPointToAngle(new Vector2(-0.1f, 0.5f), out _), Is.False);
        }

        [Test]
        public void StaticInitialGardenAppearsFullyGrownAndReusesItsObjects()
        {
            var game = new OrchardGame(42, GameMode.Practice);
            game.Update(0.5);
            world.Render(game, 12, "orchard", false, true);
            var blooms = host.transform.Find("Sculptures/Blooms");
            Assert.That(blooms, Is.Not.Null);
            Assert.That(blooms.childCount, Is.EqualTo(12));
            foreach (Transform bloom in blooms)
                Assert.That(bloom.localScale.x, Is.EqualTo(1).Within(0.0001));
            int firstBloom = blooms.GetChild(0).gameObject.GetInstanceID();
            for (int index = 0; index < 60; index++) world.Render(game, 12, "orchard", false, true);
            Assert.That(blooms.childCount, Is.EqualTo(12));
            Assert.That(blooms.GetChild(0).gameObject.GetInstanceID(), Is.EqualTo(firstBloom));
        }

        [Test]
        public void ReducedMotionFlowersStayGrownWhenMotionIsReenabled()
        {
            var game = new OrchardGame(42, GameMode.Practice);
            world.Render(game, 0, "orchard", false);
            game.Update(1);
            world.Render(game, 2, "orchard", true);
            world.Render(game, 2, "orchard", false);
            foreach (Transform bloom in host.transform.Find("Sculptures/Blooms"))
                Assert.That(bloom.localScale.x, Is.EqualTo(1).Within(0.0001));
        }

        [Test]
        public void ANewRunReusesOldFlowersButStillAnimatesNewHarvests()
        {
            world.Render(new OrchardGame(41, GameMode.Practice), 3, "orchard", false);
            var game = new OrchardGame(42, GameMode.Practice);
            world.Render(game, 0, "orchard", false);
            game.Update(1);
            world.Render(game, 1, "orchard", false);
            Transform bloom = host.transform.Find("Sculptures/Blooms").GetChild(0);
            Assert.That(bloom.localScale.x, Is.EqualTo(0.001f).Within(0.0001));
            game.Update(0.2);
            world.Render(game, 1, "orchard", false);
            Assert.That(bloom.localScale.x, Is.GreaterThan(0.75f));
        }

        [Test]
        public void RearCatchTargetsClearTheTallTreeCanopy()
        {
            var canopyCenter = new Vector3(-0.08f, 1.23f, -0.10f);
            var canopyRadius = new Vector3(0.73f, 0.48f, 0.32f);
            var camera = world.SceneCamera.transform.position;
            for (int degree = 180; degree <= 360; degree++)
            {
                float angle = degree * Mathf.Deg2Rad;
                var target = new Vector3(Mathf.Cos(angle) * 1.6f, 0.13f, Mathf.Sin(angle) * 1.6f);
                var origin = Divide(camera - canopyCenter, canopyRadius);
                var direction = Divide(target - camera, canopyRadius);
                double a = Vector3.Dot(direction, direction);
                double b = 2 * Vector3.Dot(origin, direction);
                double c = Vector3.Dot(origin, origin) - 1;
                double discriminant = b * b - 4 * a * c;
                if (discriminant < 0) continue;
                double hit = (-b - Math.Sqrt(discriminant)) / (2 * a);
                Assert.That(hit <= 0 || hit >= 1, Is.True, "Occluded rear angle: " + degree);
            }
        }

        private static Vector3 Divide(Vector3 left, Vector3 right) =>
            new Vector3(left.x / right.x, left.y / right.y, left.z / right.z);

        private void AssertSteeringAccuracy(double expected, double recovered, Vector3 expectedViewport)
        {
            // Unity camera matrices and ray intersections use floats. Require much less
            // than one pixel of drift, with angular error below 0.06% of the catch window.
            const double maximumAngleDegrees = 0.01;
            double difference = recovered - expected;
            double angleDegrees = Math.Abs(Math.Atan2(Math.Sin(difference), Math.Cos(difference))) * 180 / Math.PI;
            Assert.That(angleDegrees, Is.LessThan(maximumAngleDegrees), "Steering angle at " + expected);

            var recoveredPosition = new Vector3((float)Math.Cos(recovered) * 1.6f, 0, (float)Math.Sin(recovered) * 1.6f);
            Vector3 recoveredViewport = world.SceneCamera.WorldToViewportPoint(recoveredPosition);
            var pixelDifference = new Vector2(
                (recoveredViewport.x - expectedViewport.x) * world.SceneTexture.width,
                (recoveredViewport.y - expectedViewport.y) * world.SceneTexture.height);
            Assert.That(pixelDifference.magnitude, Is.LessThan(0.05f), "Steering pixel drift at " + expected);
        }

        private static Bounds BoundsOf(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Length, Is.GreaterThan(0));
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }
    }
}
