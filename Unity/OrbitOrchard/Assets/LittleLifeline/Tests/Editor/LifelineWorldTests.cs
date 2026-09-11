using NUnit.Framework;
using LittleLifeline.Core;
using LittleLifeline.Presentation;
using UnityEngine;

namespace LittleLifeline.Tests
{
    public sealed class LifelineWorldTests
    {
        private GameObject host;
        private LifelineWorld world;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("Lifeline renderer test");
            world = host.AddComponent<LifelineWorld>();
            world.Initialize();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        [Test]
        public void AuthoredTrainAndCareAssetsAreIncluded()
        {
            foreach (string name in new[] { "Locomotive", "Carriage", "Consultation", "Diagnostics", "Recovery", "Resident", "Crew", "Station", "Rail", "Platform", "Tree", "Garden" })
            {
                var asset = Resources.Load<GameObject>("Models/" + name);
                Assert.That(asset, Is.Not.Null, name);
                Assert.That(asset.GetComponentsInChildren<MeshFilter>().Length, Is.GreaterThan(0), name);
            }
        }

        [TestCase(320, 550)]
        [TestCase(390, 600)]
        [TestCase(430, 430)]
        [TestCase(393, 852)]
        public void AllFourCarriagesAreVisibleAndSelectableInOverview(int width, int height)
        {
            world.SetRenderSize(width, height);
            world.Focus(-1, true);
            for (int slot = 0; slot < 4; slot++)
            {
                Vector3 point = world.SceneCamera.WorldToViewportPoint(world.CarriageCenter(slot));
                Assert.That(point.x, Is.InRange(.06f, .94f), "Slot " + slot);
                Assert.That(point.y, Is.InRange(.08f, .92f), "Slot " + slot);
                Assert.That(world.PickCarriage(point), Is.EqualTo(slot));
            }
        }

        [Test]
        public void PortraitOverviewKeepsTheTrainReadableBetweenHudAndNavigation()
        {
            world.SetRenderSize(393,852);
            world.Focus(-1,true);
            var engineEnd=world.SceneCamera.WorldToViewportPoint(new Vector3(0,.78f,-9.8f));
            var trainEnd=world.SceneCamera.WorldToViewportPoint(new Vector3(0,.78f,8.4f));
            Assert.That(Mathf.Abs(engineEnd.y-trainEnd.y),Is.GreaterThan(.48f),"The train should occupy at least half of the portrait world height.");
            Assert.That(engineEnd.y,Is.InRange(.08f,.92f));
            Assert.That(trainEnd.y,Is.InRange(.08f,.92f));
        }

        [TestCase(320, 280)]
        [TestCase(390, 380)]
        public void FocusKeepsTheSelectedRoomVisibleAndPickable(int width, int height)
        {
            world.SetRenderSize(width, height);
            for (int slot = 0; slot < 4; slot++)
            {
                world.Focus(slot, true);
                Vector3 point = world.SceneCamera.WorldToViewportPoint(world.CarriageCenter(slot));
                Assert.That(point.x, Is.InRange(.25f, .75f));
                Assert.That(point.y, Is.InRange(.25f, .75f));
                Assert.That(world.PickCarriage(point), Is.EqualTo(slot));
                Assert.That(world.SelectedSlot, Is.EqualTo(slot));
            }
        }

        [Test]
        public void PicksOutsideTheImageDoNotSelectRooms()
        {
            world.SetRenderSize(320, 550);
            Assert.That(world.PickCarriage(new Vector2(-.1f, .5f)), Is.EqualTo(-1));
            Assert.That(world.PickCarriage(new Vector2(.5f, 1.1f)), Is.EqualTo(-1));
        }

        [Test]
        public void BoardTextureIsReusedAndItsMaximumSizeIsBounded()
        {
            var original = world.SetRenderSize(390, 600);
            Assert.That(world.SetRenderSize(390, 600), Is.SameAs(original));
            var large = world.SetRenderSize(1800, 3000);
            Assert.That(large.height, Is.LessThanOrEqualTo(1536));
            Assert.That(large.width / (float)large.height, Is.EqualTo(.6f).Within(.002f));
        }

        [Test]
        public void LostBoardTextureIsRecreatedWithoutReplacingItsUiReference()
        {
            var texture=world.SetRenderSize(393,852);
            texture.Release();
            Assert.That(texture.IsCreated(),Is.False);
            Assert.That(world.SetRenderSize(393,852),Is.SameAs(texture));
            Assert.That(texture.IsCreated(),Is.True,"Returning from a lost graphics surface should restore the existing board image.");
        }

        [Test]
        public void WaitingResidentsRemainBesideTheirActualCareEndpoint()
        {
            var state = new SimulationState();
            state.Patients.Add(new PatientState { Id = 42, Phase = PatientPhase.Waiting, FromSlot = 2 });
            world.Render(state,true);
            var resident = host.transform.Find("Railway diorama/Resident 42");
            Assert.That(resident, Is.Not.Null);
            Assert.That(resident.position.z, Is.InRange(world.CarriageCenter(2).z-1.7f,world.CarriageCenter(2).z+1.7f));
            Assert.That(resident.position.x, Is.LessThan(-1.4f), "Waiting residents use the platform, leaving treatment clear.");
        }

        [Test]
        public void WalkingCrewFollowSimulationProgressBetweenCarriages()
        {
            var state = new SimulationState { Tick = 50 };
            state.Crew.Add(new CrewState { Id = 4, FromSlot = 0, ToSlot = 3, MoveStartedTick = 0, MoveEndsTick = 100 });
            world.Render(state,true);
            var person = host.transform.Find("Railway diorama/Crew 4");
            Assert.That(person, Is.Not.Null);
            Assert.That(person.position.z, Is.InRange(world.CarriageCenter(0).z+1,world.CarriageCenter(3).z-1));
            Assert.That(person.position.x, Is.LessThan(-1.4f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReplacingAllActorsReusesTheExistingPoolWithoutGrowingIt(bool workers)
        {
            var state=new SimulationState();
            int count=workers ? 4 : LifelineRules.MaxPatients;
            for(int i=0;i<count;i++)
            {
                if(workers) state.Crew.Add(new CrewState { Id=i });
                else state.Patients.Add(new PatientState { Id=i,Phase=PatientPhase.Waiting });
            }
            world.Render(state,true);
            var before=ActorInstanceIds(workers);
            for(int i=0;i<count;i++)
            {
                if(workers) state.Crew[i].Id+=count;
                else state.Patients[i].Id+=count;
            }
            world.Render(state,true);
            Assert.That(ActorInstanceIds(workers),Is.EquivalentTo(before),"Removed actors should be available before replacements are acquired.");
        }

        private System.Collections.Generic.List<int> ActorInstanceIds(bool workers)
        {
            var result=new System.Collections.Generic.List<int>();
            foreach(Transform child in host.transform.Find("Railway diorama"))
                if(child.name.StartsWith(workers ? "Crew" : "Resident",System.StringComparison.Ordinal)) result.Add(child.GetInstanceID());
            return result;
        }

        [Test]
        public void DiagnosticsPatientLiesWithTheirHeadOnTheScannerPillow()
        {
            var state=new SimulationState();
            state.Carriages.Add(new CarriageState { Id=8,Slot=1,Kind=RoomKind.Diagnostics });
            state.Patients.Add(new PatientState { Id=42,RoomId=8,ToSlot=1,Phase=PatientPhase.Treating });
            world.Render(state,true);
            var resident=host.transform.Find("Railway diorama/Resident 42");
            Transform head=null;
            foreach(var part in resident.GetComponentsInChildren<Transform>())
                if(part.name.StartsWith("Head",System.StringComparison.Ordinal)) { head=part;break; }
            Assert.That(head,Is.Not.Null);
            Assert.That(head.position.z-world.CarriageCenter(1).z,Is.InRange(-.55f,-.15f));
            Assert.That(head.position.y,Is.InRange(1.20f,1.50f));
        }

        [TestCase(RoomKind.Diagnostics)]
        [TestCase(RoomKind.Recovery)]
        public void RestingPatientsFaceUpToKeepTheirCarePoseReadable(RoomKind kind)
        {
            var state=new SimulationState();
            state.Carriages.Add(new CarriageState { Id=8,Slot=1,Kind=kind });
            state.Patients.Add(new PatientState { Id=42,RoomId=8,ToSlot=1,Phase=PatientPhase.Treating });
            world.Render(state,true);
            var resident=host.transform.Find("Railway diorama/Resident 42");
            Assert.That(Vector3.Dot(resident.forward,Vector3.up),Is.GreaterThan(.99f));
        }

        [Test]
        public void StationEntranceFacesTheOverviewCamera()
        {
            world.Focus(-1,true);
            var station = host.transform.Find("Railway diorama/Station");
            Assert.That(Vector3.Dot(station.forward,world.SceneCamera.transform.position-station.position),Is.GreaterThan(0));
        }

        [TestCase(TownId.Willowbank, "River", "Station garden", "Village school")]
        [TestCase(TownId.Copperhill, "Rock terraces", "Community workshop", "Clock tower")]
        [TestCase(TownId.Seabrook, "Coastal water", "Seaside clinic", "Lighthouse path")]
        public void DestinationsHaveDistinctSettingsAndVisibleProjectLandmarks(TownId town, string setting, string first, string second)
        {
            var state = new SimulationState { Town = town };
            world.SetRenderSize(393,852);
            world.Render(state,true);
            var townRoot = host.transform.Find("Railway diorama/Town scenery/" + town);
            Assert.That(townRoot.gameObject.activeInHierarchy,Is.True);
            Assert.That(townRoot.Find(setting),Is.Not.Null);
            foreach (string name in new[] {first,second})
            {
                var landmark = townRoot.Find(name);
                Assert.That(landmark,Is.Not.Null,name);
                Assert.That(landmark.GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0));
                var point = world.SceneCamera.WorldToViewportPoint(landmark.position+Vector3.up);
                Assert.That(point.x,Is.InRange(.03f,.97f),name);
                Assert.That(point.y,Is.InRange(.08f,.92f),name);
            }
            foreach (Transform other in townRoot.parent)
                Assert.That(other.gameObject.activeSelf,Is.EqualTo(other==townRoot));
        }

        [TestCase(TownId.Willowbank, "Station garden", "Village school", 1)]
        [TestCase(TownId.Willowbank, "Station garden", "Village school", 2)]
        [TestCase(TownId.Copperhill, "Community workshop", "Clock tower", 1)]
        [TestCase(TownId.Copperhill, "Community workshop", "Clock tower", 2)]
        [TestCase(TownId.Seabrook, "Seaside clinic", "Lighthouse path", 1)]
        [TestCase(TownId.Seabrook, "Seaside clinic", "Lighthouse path", 2)]
        public void EachProjectBitRestoresOnlyItsCorrespondingLandmark(TownId town,string first,string second,int mask)
        {
            var state = new SimulationState { Town = town };
            state.Towns.Add(new TownProgress { Town = town, ProjectCompletionMask = mask, CompletedProjects = 1 });
            world.Render(state,true);
            string[] names = {first,second};
            for (int index=0;index<2;index++)
            {
                var landmark = host.transform.Find("Railway diorama/Town scenery/" + town + "/" + names[index]);
                Assert.That(landmark.Find("Restored").gameObject.activeInHierarchy,Is.EqualTo((mask&(1<<index))!=0));
                Assert.That(landmark.Find("Unrestored").gameObject.activeInHierarchy,Is.EqualTo((mask&(1<<index))==0));
            }
        }

        [Test]
        public void ReturningToATownPreservesItsOwnRestorationAndReusesTheScene()
        {
            var state = new SimulationState { Town = TownId.Willowbank };
            state.Towns.Add(new TownProgress { Town = TownId.Willowbank,ProjectCompletionMask = 2,CompletedProjects = 1 });
            state.Towns.Add(new TownProgress { Town = TownId.Copperhill,ProjectCompletionMask = 1,CompletedProjects = 1 });
            world.Render(state,true);
            var school=host.transform.Find("Railway diorama/Town scenery/Willowbank/Village school/Restored");
            int instance=school.GetInstanceID();
            state.Town=TownId.Copperhill;world.Render(state,true);
            Assert.That(school.gameObject.activeInHierarchy,Is.False);
            state.Town=TownId.Willowbank;world.Render(state,true);
            Assert.That(school.gameObject.activeInHierarchy,Is.True);
            Assert.That(school.GetInstanceID(),Is.EqualTo(instance));
        }

        [Test]
        public void CarriageFocusClearsForegroundSceneryAndReservesTheBottomDock()
        {
            world.SetRenderSize(393,852);
            world.Render(new SimulationState(),true);
            world.Focus(1,true);
            Assert.That(host.transform.Find("Railway diorama/Town scenery").gameObject.activeInHierarchy,Is.False);
            Assert.That(host.transform.Find("Railway diorama/Overview trees").gameObject.activeInHierarchy,Is.False);
            var point=world.SceneCamera.WorldToViewportPoint(world.CarriageCenter(1));
            Assert.That(point.y,Is.InRange(.54f,.64f));
            Assert.That(world.PickCarriage(point),Is.EqualTo(1));
            world.Focus(-1,true);
            Assert.That(host.transform.Find("Railway diorama/Town scenery").gameObject.activeInHierarchy,Is.True);
        }
    }
}
