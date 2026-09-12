using IdleClinic.App;
using IdleClinic.Core;
using IdleClinic.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Accessibility;
using System.Collections.Generic;
using System.Linq;

namespace IdleClinic.Tests
{
    public sealed class ClinicHUDTests
    {
        [Test]
        public void WideMarkerModeHasHysteresisInsteadOfFlippingAtOneZoomBoundary()
        {
            Assert.That(ClinicMarkerPresentation.IsWide(8.2f,false),Is.False);
            Assert.That(ClinicMarkerPresentation.IsWide(8.3f,false),Is.True);
            Assert.That(ClinicMarkerPresentation.IsWide(8.0f,true),Is.True);
            Assert.That(ClinicMarkerPresentation.IsWide(7.7f,true),Is.False);
        }
        [Test]
        public void CrowdedOnScreenCountersUseIconsEvenAtCloseCameraZoom()
        {
            var viewport=new Rect(0,0,375,667);var first=new Vector2(100,300);
            Assert.That(ClinicMarkerPresentation.CrowdedReception(first,new Vector2(179,305),viewport),Is.True);
            Assert.That(ClinicMarkerPresentation.CrowdedReception(first,new Vector2(181,305),viewport),Is.False);
            Assert.That(ClinicMarkerPresentation.CrowdedReception(new Vector2(-10,300),new Vector2(20,305),viewport),Is.False,
                "An off-screen counter cannot force or shift the visible counter's badge.");
        }
        [TestCase(122,142)][TestCase(142,122)][TestCase(122,122)]
        public void CoinOnlyReceptionTargetsSeparateWithoutDriftingOrReversing(float firstX,float secondX)
        {
            var first=new Vector2(firstX,300);var second=new Vector2(secondX,308);
            ClinicMarkerPresentation.SeparateReception(first,second,out var a,out var b);
            Assert.That(Mathf.Abs(a.x-b.x),Is.EqualTo(48));
            Assert.That((a.x+b.x)/2,Is.EqualTo((firstX+secondX)/2));
            Assert.That(a.y,Is.EqualTo(first.y));Assert.That(b.y,Is.EqualTo(second.y));
            Assert.That(a.x<=b.x,Is.EqualTo(firstX<=secondX));
            ClinicMarkerPresentation.SeparateReception(first,second,out var repeatA,out var repeatB);
            Assert.That(repeatA,Is.EqualTo(a));Assert.That(repeatB,Is.EqualTo(b));
            ClinicMarkerPresentation.SeparateReception(a,b,out var settledA,out var settledB);
            Assert.That(settledA,Is.EqualTo(a));Assert.That(settledB,Is.EqualTo(b));
        }
        [Test]
        public void AlreadySeparatedCounterBadgesStayAtTheirProjectedOrigins()
        {
            var first=new Vector2(100,300);var second=new Vector2(170,305);
            ClinicMarkerPresentation.SeparateReception(first,second,out var a,out var b);
            Assert.That(a,Is.EqualTo(first));Assert.That(b,Is.EqualTo(second));
        }
        [Test]
        public void CoinOnlyCashRetainsFortyFourPointTargetsAndItsLiveAccessibleAmount()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var amount=new Label("50");amount.AddToClassList("cash-amount");ax.Marker.Add(amount);
                ax.Simulation.Advance(60);ax.UpdateValues();
                ClinicMarkerPresentation.CashDetail(ax.Marker,true);
                Assert.That(amount.style.display.value,Is.EqualTo(DisplayStyle.None));
                Assert.That(ax.Marker.style.width.value.value,Is.EqualTo(44));
                Assert.That(ax.Marker.style.height.value.value,Is.EqualTo(44));
                Assert.That(ax.Node.value,Is.EqualTo("50 coins"));
                ax.Simulation.Collect(0);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("0 coins"));
                ClinicMarkerPresentation.CashDetail(ax.Marker,false);
                Assert.That(amount.style.display.value,Is.EqualTo(DisplayStyle.Flex));
                Assert.That(ax.Marker.ClassListContains("compact-cash"),Is.False);
            }
        }
        [Test]
        public void WideProgressIndicatorsKeepActualProgressWhileRemovingLargeClockAndRingVisuals()
        {
            var construction=new VisualElement();var progress=new ClinicProgress(26){Progress=.4f};construction.Add(progress);
            var clock=new Label("54s");clock.AddToClassList("construction-clock");construction.Add(clock);
            ClinicMarkerPresentation.ConstructionDetail(construction,true);
            Assert.That(clock.style.display.value,Is.EqualTo(DisplayStyle.None));
            Assert.That(progress.Progress,Is.EqualTo(.4f));
            var patient=new ClinicProgress(28){Progress=.6f};ClinicMarkerPresentation.PatientDetail(patient,true);
            Assert.That(patient.style.scale.value.value.x,Is.EqualTo(18f/28).Within(.0001));
            Assert.That(patient.Progress,Is.EqualTo(.6f));
            ClinicMarkerPresentation.ConstructionDetail(construction,false);ClinicMarkerPresentation.PatientDetail(patient,false);
            Assert.That(clock.style.display.value,Is.EqualTo(DisplayStyle.Flex));
            Assert.That(patient.style.scale.value.value.x,Is.EqualTo(1));
        }

        [TestCase(ClinicPatientPhase.CheckingIn,true)]
        [TestCase(ClinicPatientPhase.Treating,true)]
        [TestCase(ClinicPatientPhase.UsingAmenity,true)]
        [TestCase(ClinicPatientPhase.Seated,false)]
        [TestCase(ClinicPatientPhase.WalkingToAmenity,false)]
        public void ProgressRingsRepresentTimedServiceOnly(ClinicPatientPhase phase,bool visible)
            =>Assert.That(ClinicServicePresentation.HasProgress(phase),Is.EqualTo(visible));

        [TestCase(101,322)][TestCase(143,322)][TestCase(122,301)][TestCase(122,343)]
        public void OrdinaryObjectTapsUseTheEntireFortyFourPointTarget(float x,float y)
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.Simulation.State.Tutorial=ClinicTutorialStep.Complete;
                ax.AddObjectTarget(new ClinicHit(ClinicHitKind.Desk,0),new Rect(100,300,44,44));
                var hit=ax.PickObject(new Vector2(x,y));
                Assert.That(hit.HasValue,Is.True,"A tap near any target edge must not depend on the smaller 3D collider.");
                Assert.That(hit.Value.Kind,Is.EqualTo(ClinicHitKind.Desk));Assert.That(hit.Value.Id,Is.Zero);
                Assert.That(ax.PickObject(new Vector2(144.1f,322)).HasValue,Is.False,"Floor outside the target stays available.");
                Assert.That(ax.Simulation.State.Wallet,Is.Zero,"Selection cannot purchase or collect.");
            }
        }
        [Test]
        public void OverlappingObjectTargetsChooseNearestCenterThenStableKindAndId()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.Simulation.State.Tutorial=ClinicTutorialStep.Complete;
                ax.Simulation.State.ReceptionDesks.Add(new ReceptionDeskState{Id=1});
                // Reverse registration deliberately: tie breaking must not use dictionary order.
                ax.AddObjectTarget(new ClinicHit(ClinicHitKind.Desk,1),new Rect(120,300,44,44));
                ax.AddObjectTarget(new ClinicHit(ClinicHitKind.Desk,0),new Rect(100,300,44,44));
                Assert.That(ax.PickObject(new Vector2(138,322)).Value.Id,Is.EqualTo(1));
                Assert.That(ax.PickObject(new Vector2(126,322)).Value.Id,Is.Zero);
                Assert.That(ax.PickObject(new Vector2(132,322)).Value.Id,Is.Zero);
            }
        }
        [Test]
        public void ExpandedObjectTargetsRespectCashPriorityVisibilityAndTutorialGates()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var marker=ax.AddObjectTarget(new ClinicHit(ClinicHitKind.Desk,0),new Rect(100,300,44,44));
                var point=new Vector2(143,322);
                Assert.That(ax.PickObject(point).HasValue,Is.False);
                ax.Simulation.State.Tutorial=ClinicTutorialStep.Complete;
                Assert.That(ax.PickObject(point).HasValue,Is.True);
                ax.PlacePriorityMarker(0,new Rect(136,300,56,44));
                Assert.That(ax.PickObject(point).HasValue,Is.False,"Cash wins even when it covers only the expanded target edge.");
                ax.PlacePriorityMarker(0,new Rect(250,400,56,44));marker.style.display=DisplayStyle.None;
                Assert.That(ax.PickObject(point).HasValue,Is.False);
                marker.style.display=DisplayStyle.Flex;marker.SetEnabled(false);
                Assert.That(ax.PickObject(point).HasValue,Is.False);
                marker.SetEnabled(true);ax.CoverObject(new Rect(100,300,44,44));
                Assert.That(ax.PickObject(point).HasValue,Is.False,"An opaque dock cannot expose an underlying object hit area.");
            }
        }
        [Test]
        public void RoomAccessibilityYieldsToTheExpandedPhysicalObjectTarget()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.Simulation.State.Tutorial=ClinicTutorialStep.Complete;
                ax.ConfigureRoom(new Rect(121,300,44,44));
                var target=ax.AddObjectTarget(new ClinicHit(ClinicHitKind.Desk,0),new Rect(100,300,44,44));
                ax.UpdateRoom();
                Assert.That(ax.RoomNode.isActive,Is.False,"A room label cannot tap the edge of an expanded workstation target.");
                Assert.That(ax.RoomNode.frame,Is.EqualTo(Rect.zero));
                target.style.display=DisplayStyle.None;ax.UpdateRoom();
                Assert.That(ax.RoomNode.isActive,Is.True);
            }
        }
        [Test]
        public void WorkstationSelectionKeepsTheIndividualStationAndNeverBypassesOnboarding()
        {
            var state=ClinicSimulation.CreateNew().State;
            var first=new ClinicHit(ClinicHitKind.Desk,0);
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,first),Is.False);
            state.Tutorial=ClinicTutorialStep.Complete;
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,first),Is.True);
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(ClinicHitKind.Desk,1)),Is.False);
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=1});
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(ClinicHitKind.Desk,1)),Is.True);
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(ClinicHitKind.Station,0)),Is.True);
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(ClinicHitKind.Station,1)),Is.False);
        }
        [Test]
        public void WaitingAmenitiesBecomeSelectableOnlyWhenTheWaitingRoomIsBuilt()
        {
            var state=ClinicSimulation.CreateNew().State;state.Tutorial=ClinicTutorialStep.Complete;
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(ClinicHitKind.Parking)),Is.True);
            foreach(var kind in new[]{ClinicHitKind.Toilet,ClinicHitKind.Vending})
                Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(kind)),Is.False);
            state.Room(ClinicRoom.Waiting).Built=true;
            foreach(var kind in new[]{ClinicHitKind.Toilet,ClinicHitKind.Vending})
                Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(kind)),Is.True);
            Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(ClinicHitKind.VendingCash)),Is.False,
                "A cash hit must collect through the authoritative command, not open an upgrade panel.");
        }
        [Test]
        public void IndividualUpgradeReadoutUsesAssignedStaffAndTheOwningRoomCap()
        {
            var state=ClinicSimulation.CreateNew().State;state.Tutorial=ClinicTutorialStep.Complete;
            state.ReceptionDesks.Add(new ReceptionDeskState{Id=1,EquipmentLevel=2});
            state.Staff.Add(new ClinicStaffState{Id=7,Role=ClinicStaffRole.Receptionist,StationId=1,TrainingLevel=2});
            var first=ClinicWorkstationReadout.Create(state,ClinicStaffRole.Receptionist,0);
            var second=ClinicWorkstationReadout.Create(state,ClinicStaffRole.Receptionist,1);
            Assert.That(first.EquipmentLevel,Is.EqualTo(1));Assert.That(first.EquipmentPrice,Is.EqualTo(90));
            Assert.That(first.EquipmentCapped,Is.False);Assert.That(first.TrainingCapped,Is.False);
            Assert.That(second.StaffId,Is.EqualTo(7));Assert.That(second.EquipmentCapped,Is.True);
            Assert.That(second.TrainingCapped,Is.True);Assert.That(second.ServiceTicks,Is.LessThan(first.ServiceTicks));
            Assert.That(first.NextEquipmentTicks,Is.LessThan(first.ServiceTicks));
            Assert.That(first.NextTrainingTicks,Is.LessThan(first.ServiceTicks));
            Assert.That(state.ReceptionDesks[0].EquipmentLevel,Is.EqualTo(1),"Reading the next timer cannot purchase an upgrade.");
            Assert.That(state.Staff[0].TrainingLevel,Is.EqualTo(1));
        }
        [Test]
        public void WorkstationDockBindsSecondDeskAndItsAssignedStaffControls()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var state=ax.Simulation.State;state.Tutorial=ClinicTutorialStep.Complete;
                state.Room(ClinicRoom.Reception).Tier=2;
                state.ReceptionDesks.Add(new ReceptionDeskState{Id=1,EquipmentLevel=2});
                state.Staff.Add(new ClinicStaffState{Id=7,Role=ClinicStaffRole.Receptionist,StationId=1,TrainingLevel=2});
                var dock=ax.BuildObjectDock(new ClinicHit(ClinicHitKind.Desk,1));
                Assert.That(dock.Q<Button>("station-equipment-Receptionist-1"),Is.Not.Null);
                Assert.That(dock.Q<Button>("staff-training-7"),Is.Not.Null);
                Assert.That(dock.Q<Button>("station-equipment-Receptionist-0"),Is.Null);
                Assert.That(dock.Q<Button>("staff-training-0"),Is.Null);
                Assert.That(dock.Q<Button>("station-equipment-Receptionist-1").tooltip,Does.Contain("162 coins"));
                Assert.That(dock.Q<Button>("staff-training-7").tooltip,Does.Contain("144 coins"));
                Assert.That(dock.Q<Button>("manage-reception"),Is.Not.Null,"Room-wide upgrades remain reachable.");
            }
        }
        [Test]
        public void RoomEquipmentPreviewIncludesTheExistingStationAndStaffImprovements()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var state=ax.Simulation.State;
                state.ReceptionDesks[0].EquipmentLevel=2;state.Staff[0].TrainingLevel=2;
                Assert.That(ax.EquipmentBenefit(ClinicRoom.Reception),Is.EqualTo("10.3s"));
                state.ReceptionDesks.Add(new ReceptionDeskState{Id=1});
                Assert.That(ax.EquipmentBenefit(ClinicRoom.Reception),Is.EqualTo("10.3–12.2s"),"Both desks retain their individual service speeds.");
                Assert.That(ax.EquipmentBenefit(ClinicRoom.Waiting),Is.EqualTo("1.8s"),"Preview uses the same tenth-second rounding as service.");
                Assert.That(state.Room(ClinicRoom.Reception).EquipmentLevel,Is.EqualTo(1));
            }
        }

        [Test]
        public void VendingDockExposesRoomCapAndParkingKeepsItsIndependentUpgrade()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var state=ax.Simulation.State;state.Tutorial=ClinicTutorialStep.Complete;
                state.Room(ClinicRoom.Waiting).Built=true;state.Amenity(ClinicAmenity.Vending).Level=1;
                var vending=ax.BuildObjectDock(new ClinicHit(ClinicHitKind.Vending));
                Assert.That(vending.Q<Button>("upgrade-vending-machine"),Is.Null);
                Assert.That(vending.Query<Label>().ToList().Any(l=>l.text=="Needs room 2"),Is.True);
                Assert.That(vending.Q<Button>("collect-vending-tips"),Is.Not.Null);
                var parking=ax.BuildObjectDock(new ClinicHit(ClinicHitKind.Parking));
                Assert.That(parking.Q<Button>("build-car-park"),Is.Not.Null);
                Assert.That(parking.Q<Button>("build-car-park").tooltip,Does.Contain("220 coins"));
                Assert.That(parking.Query<Label>().ToList().Any(l=>l.text=="2 bays · 5 coins/car"),Is.True);
            }
        }
        [Test]
        public void VendingCashFlightRecognizesOnlyCollectedVendingMoney()
        {
            var tip=new ClinicEvent{Kind=ClinicEventKind.TipReceived,Amenity=ClinicAmenity.Vending,DeskId=-1,Amount=5};
            Assert.That(ClinicCashPresentation.IsVendingCollection(tip),Is.False);
            tip.Kind=ClinicEventKind.CashCollected;
            Assert.That(ClinicCashPresentation.IsVendingCollection(tip),Is.True);
            tip.DeskId=0;tip.Amenity=ClinicAmenity.Parking;
            Assert.That(ClinicCashPresentation.IsVendingCollection(tip),Is.False);
        }
        [Test]
        public void VendingCashPriorityHidesOverlappingRoomAccessibilityWithoutCollecting()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.ConfigureRoom(new Rect(128,447,44,44));
                ax.PlacePriorityMarker(3,new Rect(128,447,88,44));ax.UpdateRoom();
                Assert.That(ax.RoomNode.isActive,Is.False);
                Assert.That(ax.RoomNode.state,Is.EqualTo(AccessibilityState.Disabled));
                Assert.That(ax.RoomNode.frame,Is.EqualTo(Rect.zero));
                Assert.That(ax.Simulation.State.Wallet,Is.Zero);
            }
        }
        [Test]
        public void HiddenCashNodeClearsItsPreviousHitFrameAndStaysZeroWhenUnityRefreshesFrames()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var oldFrame=new Rect(104,354,60.5f,44);
                ax.Node.frame=oldFrame;
                ax.Marker.style.display=DisplayStyle.None;
                ax.UpdateNode();
                Assert.That(ax.Node.isActive,Is.False);
                Assert.That(ax.Node.state,Is.EqualTo(AccessibilityState.Disabled));
                Assert.That(ax.Node.frame,Is.EqualTo(Rect.zero),"Hiding a previously visible stack must remove its stale native hit point.");
                Assert.That(ax.Node.frameGetter(),Is.EqualTo(Rect.zero));

                ax.Node.frame=oldFrame;
                ax.Hierarchy.RefreshNodeFrames();
                Assert.That(ax.Node.frame,Is.EqualTo(Rect.zero),"Unity's own frame refresh must not restore hidden geometry.");
                ax.UpdateValues();
                Assert.That(ax.Node.state,Is.EqualTo(AccessibilityState.Disabled),"Readout refresh cannot re-enable hidden nodes.");
                Assert.That(ax.Hierarchy.rootNodes.Contains(ax.Node),Is.True,"Visibility changes preserve the same hierarchy node.");
            }
        }
        [Test]
        public void CashAccessibilityValueTracksPaymentCollectionAndTheCurrentRestoredSimulation()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                Assert.That(ax.Node.value,Is.EqualTo("0 coins"));
                ax.Simulation.Advance(60);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("50 coins"));
                Assert.That(ax.Simulation.Collect(0).Success,Is.True);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("0 coins"),"Collection clears the announced till even while its native node still exists.");

                var restored=ClinicSimulation.CreateNew();restored.State.ReceptionDesks[0].Till=20000;
                ax.ReplaceSimulation(restored);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("20,000 coins"),"Read the current simulation, without the visible label's K/M abbreviation.");
            }
        }
        [TestCase(-20,false)]
        [TestCase(0,true)]
        [TestCase(56,true)]
        [TestCase(57,false)]
        [TestCase(120,false)]
        public void ScrollViewportDoesNotExposeClippedControlsOverTheHeaderOrWorld(float localY,bool visible)
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.PlaceInClippedViewport(new Rect(40,200,200,100),new Rect(10,localY,56,44));
                ax.UpdateNode();
                Assert.That(ax.Node.isActive,Is.EqualTo(visible));
                Assert.That(ax.Node.frameGetter(),visible?Is.Not.EqualTo(Rect.zero):Is.EqualTo(Rect.zero));
            }
        }
        [Test]
        public void ExplicitlyClippedContainerDoesNotExposeItsHiddenAction()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.PlaceInClippedViewport(new Rect(40,200,200,100),new Rect(10,120,56,44),false);
                ax.UpdateNode();
                Assert.That(ax.Node.isActive,Is.False);
                Assert.That(ax.Node.frameGetter(),Is.EqualTo(Rect.zero));
            }
        }
        [TestCase(0)][TestCase(1)][TestCase(2)]
        public void RoomAccessibilityYieldsToEitherCashDeskOrExpansionWhenProjectedMarkersOverlap(int priorityMarker)
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                ax.Simulation.State.ReceptionDesks[0].Till=120;
                ax.Simulation.State.ReceptionDesks.Add(new ReceptionDeskState{Id=1,Till=60});
                ax.ConfigureRoom(new Rect(128.4f,447.4f,44,44));
                ax.PlacePriorityMarker(0,new Rect(100.5f,354,67,44));
                ax.PlacePriorityMarker(1,new Rect(184.5f,360,61,44));
                ax.UpdateRoom();
                Assert.That(ax.RoomNode.isActive,Is.True,"The new Home floor point is clear while both counters hold cash.");

                // At extreme zoom, fixed-size overlay controls can cover a projected
                // room center. Exercise the actual UI binding, not only Rect.Contains.
                ax.PlacePriorityMarker(priorityMarker,new Rect(128,447,88,44));ax.UpdateRoom();
                Assert.That(ax.RoomNode.isActive,Is.False);
                Assert.That(ax.RoomNode.state,Is.EqualTo(AccessibilityState.Disabled));
                Assert.That(ax.RoomNode.frame,Is.EqualTo(Rect.zero));
                Assert.That(ax.RoomNode.frameGetter(),Is.EqualTo(Rect.zero));
                ax.UpdateValues();
                Assert.That(ax.RoomNode.state,Is.EqualTo(AccessibilityState.Disabled));

                ax.PlacePriorityMarker(priorityMarker,new Rect(10,250,88,44));ax.UpdateRoom();
                Assert.That(ax.RoomNode.isActive,Is.True,"The same room node becomes usable again once its center is clear.");
                Assert.That(ax.RoomNode.frame,Is.Not.EqualTo(Rect.zero));
                Assert.That(ax.Simulation.State.Wallet,Is.Zero);
                Assert.That(ax.Simulation.State.ReceptionDesks[0].Till,Is.EqualTo(120));
                Assert.That(ax.Simulation.State.ReceptionDesks[1].Till,Is.EqualTo(60));
            }
        }
        private sealed class CashAccessibilityFixture:System.IDisposable
        {
            public readonly VisualElement Marker=new VisualElement();
            public readonly AccessibilityHierarchy Hierarchy=new AccessibilityHierarchy();
            public readonly AccessibilityNode Node;
            public AccessibilityNode RoomNode;
            public ClinicSimulation Simulation=ClinicSimulation.CreateNew();
            private readonly VisualElement root;
            private VisualElement roomTarget;
            private readonly GameObject owner;
            private readonly ClinicApp app;
            private const System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            public CashAccessibilityFixture(VisualElement root)
            {
                this.root=root;
                // Keep the component inactive so this narrow binding test does not
                // start the game, open saves, or initialize Apple services.
                owner=new GameObject("Clinic accessibility binding test");owner.SetActive(false);
                app=owner.AddComponent<ClinicApp>();root.Add(Marker);
                Set("root",root);Set("simulation",Simulation);Set("accessibility",Hierarchy);
                Set("walletNode",Hierarchy.AddNode("Wallet"));Set("hintNode",Hierarchy.AddNode("Hint"));Set("hintLabel",new Label());
                Invoke("RegisterCashAccessibility",Marker,0);
                var bindings=(System.Collections.IList)typeof(ClinicApp).GetField("accessible",Private).GetValue(app);
                var binding=bindings[0];
                Node=(AccessibilityNode)binding.GetType().GetField("Node").GetValue(binding);
            }
            private void Set(string field,object value)=>typeof(ClinicApp).GetField(field,Private).SetValue(app,value);
            private void Invoke(string method,params object[] args)=>typeof(ClinicApp).GetMethod(method,Private).Invoke(app,args);
            public VisualElement AddObjectTarget(ClinicHit hit,Rect bounds)
            {
                Layout(root.panel.visualTree,new Rect(0,0,375,667));Layout(root,new Rect(0,0,375,667));Set("overlay",root);
                var target=new VisualElement();root.Add(target);Layout(target,bounds);
                var targets=(Dictionary<VisualElement,ClinicHit>)typeof(ClinicApp).GetField("objectHits",Private).GetValue(app);
                targets.Add(target,hit);return target;
            }
            public ClinicHit? PickObject(Vector2 point)
            {
                var arguments=new object[]{point,default(ClinicHit)};
                var found=(bool)typeof(ClinicApp).GetMethod("TryPickManagementTarget",Private).Invoke(app,arguments);
                return found?(ClinicHit?)arguments[1]:null;
            }
            public void CoverObject(Rect bounds)
            {
                var cover=new VisualElement();root.Add(cover);Layout(cover,bounds);Set("dock",cover);
            }
            public string EquipmentBenefit(ClinicRoom room)=>(string)typeof(ClinicApp).GetMethod("NextBenefit",Private)
                .Invoke(app,new object[]{Simulation.State.Room(room),UpgradeTrack.Equipment});
            public VisualElement BuildObjectDock(ClinicHit hit)
            {
                var dock=new VisualElement();root.Add(dock);Set("dock",dock);Set("selectedObject",(ClinicHit?)hit);
                Invoke("RebuildDock");return dock;
            }
            public void UpdateValues()=>Invoke("UpdateAccessibilityValues");
            public void UpdateNode()=>Invoke("UpdateAccessibilityNode",Node,Marker);
            public void PlaceInClippedViewport(Rect viewportBounds,Rect markerBounds,bool useScrollView=true)
            {
                Layout(root.panel.visualTree,new Rect(0,0,375,667));Layout(root,new Rect(0,0,375,667));
                if(useScrollView)
                {
                    var scroll=new ScrollView(ScrollViewMode.Vertical);root.Add(scroll);scroll.Add(Marker);
                    Layout(scroll,viewportBounds);
                    var localViewport=new Rect(0,0,viewportBounds.width,viewportBounds.height);
                    Layout(scroll.contentViewport.parent,localViewport);Layout(scroll.contentViewport,localViewport);
                    Layout(scroll.contentContainer,new Rect(0,0,viewportBounds.width,400));Layout(Marker,markerBounds);
                    return;
                }
                var viewport=new VisualElement();viewport.style.overflow=Overflow.Hidden;root.Add(viewport);
                Layout(viewport,viewportBounds);viewport.Add(Marker);Layout(Marker,markerBounds);
            }
            public void ReplaceSimulation(ClinicSimulation simulation){Simulation=simulation;Set("simulation",simulation);}
            public void ConfigureRoom(Rect bounds)
            {
                Layout(root.panel.visualTree,new Rect(0,0,375,667));Layout(root,new Rect(0,0,375,667));
                roomTarget=new VisualElement();root.Add(roomTarget);Layout(roomTarget,bounds);
                var targets=(Dictionary<ClinicRoom,VisualElement>)typeof(ClinicApp).GetField("roomTargets",Private).GetValue(app);
                targets.Add(ClinicRoom.Reception,roomTarget);
                Invoke("RegisterAccessibleButton",roomTarget,"Select Reception",(System.Action)(()=>{}),null);
                var bindings=(System.Collections.IList)typeof(ClinicApp).GetField("accessible",Private).GetValue(app);
                RoomNode=(AccessibilityNode)bindings[bindings.Count-1].GetType().GetField("Node").GetValue(bindings[bindings.Count-1]);
            }
            public void PlacePriorityMarker(int index,Rect bounds)
            {
                VisualElement marker;
                if(index>=2)
                {
                    var field=index==2?"waitingMarker":"vendingCashMarker";
                    marker=(VisualElement)typeof(ClinicApp).GetField(field,Private).GetValue(app);
                    if(marker==null){marker=new VisualElement();root.Add(marker);Set(field,marker);}
                }
                else
                {
                    var markers=(Dictionary<int,VisualElement>)typeof(ClinicApp).GetField("cashMarkers",Private).GetValue(app);
                    if(!markers.TryGetValue(index,out marker)){marker=index==0?Marker:new VisualElement();root.Add(marker);markers.Add(index,marker);}
                }
                Layout(marker,bounds);
            }
            // Unity's internal manual-layout setter makes the real worldBound data
            // deterministic in an EditMode binding test without opening an Editor window.
            private static void Layout(VisualElement element,Rect bounds)
                =>typeof(VisualElement).GetProperty("layout").SetValue(element,bounds);
            public void UpdateRoom()=>Invoke("UpdateAccessibilityNode",RoomNode,roomTarget);
            public void Dispose(){Invoke("DisposeAccessibility");Marker.RemoveFromHierarchy();roomTarget?.RemoveFromHierarchy();Object.DestroyImmediate(owner);}
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CapturedDispatchFinishesMixedPinchBeforeTheNextControlTap(bool worldFirst)
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,worldFirst?ui.Board:ui.Control,worldFirst);
                ui.Begin(1,worldFirst?ui.Control:ui.Board,!worldFirst);
                foreach(var pointer in ui.Arbiter.ActivePointers)
                {
                    ui.Gesture.Begin(pointer.Key,pointer.Value);
                    ui.Captured.Add(pointer.Key);ui.Board.CapturePointer(pointer.Key);
                }
                var moved=new Vector2(260,200);
                using(var e=PointerMoveEvent.GetPooled(new Touch{fingerId=1,phase=TouchPhase.Moved,position=moved}))
                    ui.Root.SendEvent(e);
                Assert.That(ui.Arbiter.ActivePointers.Single(p=>p.Key==PointerId.touchPointerIdBase+1).Value,Is.EqualTo(moved));

                ui.End(0);ui.End(1);
                Assert.That(ui.AncestorReleases,Is.Zero,"Unity skips ancestor callbacks for captured dispatch; target listeners must clean up.");
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty,"Neither captured release may strand a finger in the next gesture.");
                Assert.That(ui.Gesture.PointerCount,Is.Zero);
                Assert.That(ui.Captured,Is.Empty);
                Assert.That(ui.ControlReleases,Is.Zero,"The transferred control must not receive a purchase release.");

                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1),"An independent control tap must reach its target after the pinch.");
                Assert.That(ui.Arbiter.BlocksControlActivations,Is.False);
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
            }
        }
        [Test]
        public void DraggingAnUpgradeControlAndReturningToItsStartCannotPurchase()
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,ui.Control,false);
                foreach(var position in new[]{new Vector2(124,200),new Vector2(100,200)})
                    using(var e=PointerMoveEvent.GetPooled(new Touch{fingerId=0,phase=TouchPhase.Moved,position=position}))
                        ui.Root.SendEvent(e);
                ui.End(0);
                Assert.That(ui.ControlReleases,Is.Zero,"Dragging back onto an upgrade must not become a purchase.");
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1),"A new deliberate tap is still usable.");
            }
        }

        [Test]
        public void CapturedControlReleaseDoesNotLeaveAStaleFingerForTheNextWorldTap()
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1));
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
                ui.Begin(1,ui.Board,true);
                Assert.That(ui.Arbiter.BlocksControlActivations,Is.False,"A completed button tap cannot turn a later world tap into a pinch.");
                ui.End(1);
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
            }
        }
        [Test]
        public void CapturedCancellationClearsTheEntireTouchSequence()
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,ui.Board,true);ui.Begin(1,ui.Control,false);
                using(var e=PointerCancelEvent.GetPooled(new Touch{fingerId=0,phase=TouchPhase.Canceled,position=Vector2.one}))
                    ui.Root.SendEvent(e);
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
                Assert.That(ui.Gesture.PointerCount,Is.Zero);
                Assert.That(ui.Captured,Is.Empty);
                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1));
            }
        }

        // These tests use a real runtime panel and PointerEvent dispatch. Pure arbiter
        // tests cannot expose Unity's deliberate omission of capture-target ancestors.
        private sealed class CapturedTouchPanel:System.IDisposable
        {
            public readonly ClinicTouchArbiter Arbiter=new ClinicTouchArbiter();
            public readonly ClinicGesture Gesture=new ClinicGesture();
            public readonly HashSet<int> Captured=new HashSet<int>();
            public readonly VisualElement Root,Board=new VisualElement();
            public readonly Button Control=new Button();
            public int AncestorReleases,ControlReleases;
            private readonly GameObject owner;
            private readonly PanelSettings settings;
            public CapturedTouchPanel()
            {
                settings=ScriptableObject.CreateInstance<PanelSettings>();
                owner=new GameObject("Clinic captured pointer test");
                var document=owner.AddComponent<UIDocument>();document.panelSettings=settings;
                Root=document.rootVisualElement;Root.Add(Board);Root.Add(Control);
                Assert.That(Root.panel,Is.Not.Null);
                ClinicTouchCaptureLifecycle.Bind(Root,Arbiter,Gesture,Captured,Cancel);
                ClinicTouchCaptureLifecycle.Bind(Board,Arbiter,Gesture,Captured,Cancel);
                ClinicTouchCaptureLifecycle.Bind(Control,Arbiter,Gesture,Captured,Cancel);
                Root.RegisterCallback<PointerUpEvent>(_=>AncestorReleases++,TrickleDown.TrickleDown);
                Control.RegisterCallback<PointerUpEvent>(_=>ControlReleases++,TrickleDown.TrickleDown);
            }
            public void Begin(int finger,VisualElement capture,bool world)
            {
                // Initialize Unity's pressed-button state as an actual touch down does.
                using(var e=PointerDownEvent.GetPooled(new Touch{fingerId=finger,phase=TouchPhase.Began,position=new Vector2(100+finger*100,200)}))
                {
                    Arbiter.Begin(e.pointerId,e.position,world);
                    if(world){Gesture.Begin(e.pointerId,e.position);Captured.Add(e.pointerId);}
                    capture.CapturePointer(e.pointerId);
                }
            }
            public void End(int finger)
            {
                using(var e=PointerUpEvent.GetPooled(new Touch{fingerId=finger,phase=TouchPhase.Ended,position=new Vector2(100+finger*100,200)}))
                    Root.SendEvent(e);
            }
            private void Cancel()
            {
                var ids=Arbiter.ActivePointers.Select(p=>p.Key).Concat(Captured).Distinct().ToArray();
                Arbiter.Cancel();Gesture.Cancel();Captured.Clear();
                foreach(var id in ids)(Root.panel?.GetCapturingElement(id) as VisualElement)?.ReleasePointer(id);
            }
            public void Dispose()
            {
                Cancel();
                // Release Unity's synthetic test touch state even when an assertion fails.
                for(var finger=0;finger<2;finger++)
                    using(var e=PointerUpEvent.GetPooled(new Touch{fingerId=finger,phase=TouchPhase.Ended})){}
                Object.DestroyImmediate(owner);Object.DestroyImmediate(settings);
            }
        }

        [TestCase(ClinicTutorialStep.FirstArrival,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.FirstArrival,ClinicRoom.FirstAid,false)]
        [TestCase(ClinicTutorialStep.FirstArrival,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.CollectFirstPayment,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.CollectFirstPayment,ClinicRoom.FirstAid,false)]
        [TestCase(ClinicTutorialStep.CollectFirstPayment,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.HireFirstNurse,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.HireFirstNurse,ClinicRoom.FirstAid,true)]
        [TestCase(ClinicTutorialStep.HireFirstNurse,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.FirstTreatment,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.FirstTreatment,ClinicRoom.FirstAid,false)]
        [TestCase(ClinicTutorialStep.FirstTreatment,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.Complete,ClinicRoom.Reception,true)]
        [TestCase(ClinicTutorialStep.Complete,ClinicRoom.FirstAid,true)]
        [TestCase(ClinicTutorialStep.Complete,ClinicRoom.Waiting,true)]
        public void RoomSelectionPreservesTheRequiredTutorialAction(ClinicTutorialStep tutorial,ClinicRoom room,bool allowed)
        {
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(tutorial,room),Is.EqualTo(allowed));
        }
        [Test]
        public void RepeatedFirstPaymentCannotReplaceTheNurseActionWithReceptionControls()
        {
            var clinic=ClinicSimulation.CreateNew();clinic.Advance(60);
            Assert.That(clinic.State.Tutorial,Is.EqualTo(ClinicTutorialStep.CollectFirstPayment));
            Assert.That(clinic.Collect(0).Success,Is.True);
            Assert.That(clinic.State.Wallet,Is.EqualTo(50));
            Assert.That(clinic.State.Tutorial,Is.EqualTo(ClinicTutorialStep.HireFirstNurse));

            // The empty counter can pick its reception room on the second physical
            // tap. That pick must leave the already-open nurse action untouched.
            Assert.That(clinic.Collect(0).Success,Is.False);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(clinic.State.Tutorial,ClinicRoom.Reception),Is.False);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(clinic.State.Tutorial,ClinicRoom.FirstAid),Is.True);
            Assert.That(clinic.HireNurse().Success,Is.True);
            clinic.Advance(60);
            Assert.That(clinic.State.Tutorial,Is.EqualTo(ClinicTutorialStep.Complete));
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(clinic.State.Tutorial,ClinicRoom.Reception),Is.True);
        }
        [TestCase(true,true)]
        [TestCase(true,false)]
        [TestCase(false,true)]
        [TestCase(false,false)]
        public void WorldAndButtonTouchesBecomeACameraGestureAndConsumeBothReleases(bool worldFirst,bool worldReleasedFirst)
        {
            var arbiter=new ClinicTouchArbiter();var cameraGesture=new ClinicGesture();
            var first=new Vector2(100,200);var second=new Vector2(200,200);
            arbiter.Begin(1,first,worldFirst);
            Assert.That(arbiter.BlocksControlActivations,Is.False,"An ordinary first press must remain usable.");
            arbiter.Begin(2,second,!worldFirst);
            Assert.That(arbiter.BlocksControlActivations,Is.True,"A control press must be cancelled in either start order.");
            Assert.That(arbiter.RoutesToWorld,Is.True);

            foreach(var pointer in arbiter.ActivePointers)cameraGesture.Begin(pointer.Key,pointer.Value);
            Assert.That(cameraGesture.Move(2,new Vector2(250,200),out _,out _,out var zoom),Is.True);
            Assert.That(zoom,Is.EqualTo(2f/3).Within(.001),"The control-origin finger still participates in the pinch.");

            var worldId=worldFirst?1:2;var buttonId=worldFirst?2:1;
            var releasedFirst=worldReleasedFirst?worldId:buttonId;var releasedLast=worldReleasedFirst?buttonId:worldId;
            Assert.That(arbiter.End(releasedFirst),Is.True,"The root must stop the first release before a button can click.");
            Assert.That(cameraGesture.End(releasedFirst,Vector2.zero),Is.False);
            Assert.That(arbiter.BlocksControlActivations,Is.True,"Lifting one finger cannot re-enable pending purchases.");
            Assert.That(arbiter.End(releasedLast),Is.True,"The last release must also be stopped.");
            Assert.That(cameraGesture.End(releasedLast,Vector2.zero),Is.False);
            Assert.That(arbiter.BlocksControlActivations,Is.False);

            arbiter.Begin(3,Vector2.one,false);
            Assert.That(arbiter.End(3),Is.False,"The next independent button tap must reach Clickable normally.");
        }
        [Test]
        public void TwoControlTouchesCancelPurchasesWithoutMovingTheWorld()
        {
            var arbiter=new ClinicTouchArbiter();
            arbiter.Begin(1,Vector2.zero,false);arbiter.Begin(2,Vector2.one,false);
            Assert.That(arbiter.BlocksControlActivations,Is.True);
            Assert.That(arbiter.RoutesToWorld,Is.False);
            Assert.That(arbiter.End(1),Is.True);Assert.That(arbiter.End(2),Is.True);
        }
        [Test]
        public void AddingAnotherFingerBeforeTheLastReleaseKeepsPurchasesCancelled()
        {
            var arbiter=new ClinicTouchArbiter();
            arbiter.Begin(1,Vector2.zero,true);arbiter.Begin(2,Vector2.one,false);
            arbiter.End(1);arbiter.Begin(3,new Vector2(30,30),false);
            Assert.That(arbiter.RoutesToWorld,Is.True);
            Assert.That(arbiter.End(2),Is.True);Assert.That(arbiter.End(3),Is.True);
        }
        [Test]
        public void FocusLossClearsTouchesAcrossControlsAndWorld()
        {
            var arbiter=new ClinicTouchArbiter();
            arbiter.Begin(1,Vector2.zero,false);arbiter.Begin(2,Vector2.one,true);arbiter.Cancel();
            Assert.That(arbiter.ActivePointers,Is.Empty);
            Assert.That(arbiter.BlocksControlActivations,Is.False);
            arbiter.Begin(3,Vector2.one,false);Assert.That(arbiter.End(3),Is.False);
        }
        [Test]
        public void ScrollIntentDependsOnSourceAndModifiersNotMomentumSpeed()
        {
            Assert.That(ClinicScrollPolicy.ShouldPan(true,false,false),Is.True);
            Assert.That(ClinicScrollPolicy.ShouldPan(false,false,false),Is.False);
            Assert.That(ClinicScrollPolicy.ShouldPan(true,false,true),Is.False);
            Assert.That(ClinicScrollPolicy.ShouldPan(false,true,false),Is.True);
        }
        [Test]
        public void AStillReleaseRemainsATap()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,new Vector2(100,200));
            Assert.That(gesture.End(1,new Vector2(102,201)),Is.True);
        }
        [Test]
        public void PanningFromCashNeverCollectsEvenAfterReturningToStart()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,new Vector2(100,200));
            Assert.That(gesture.Move(1,new Vector2(120,200),out _,out _,out _),Is.True);
            gesture.Move(1,new Vector2(100,200),out _,out _,out _);
            Assert.That(gesture.End(1,new Vector2(100,200)),Is.False);
        }
        [Test]
        public void PinchKeepsBothFingerReleasesFromBecomingPurchases()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,new Vector2(100,200));gesture.Begin(2,new Vector2(200,200));
            Assert.That(gesture.Move(2,new Vector2(250,200),out var from,out var to,out var factor),Is.True);
            Assert.That(from,Is.EqualTo(new Vector2(150,200)));Assert.That(to,Is.EqualTo(new Vector2(175,200)));
            Assert.That(factor,Is.EqualTo(2f/3).Within(.001));
            Assert.That(gesture.End(2,new Vector2(250,200)),Is.False);
            Assert.That(gesture.End(1,new Vector2(100,200)),Is.False);
        }
        [Test]
        public void FocusLossCancelsPendingTapAndNextGestureWorks()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,Vector2.one);gesture.Cancel();
            Assert.That(gesture.End(1,Vector2.one),Is.False);
            gesture.Begin(3,Vector2.one);Assert.That(gesture.End(3,Vector2.one),Is.True);
        }
        [Test]
        public void MovingBeforePointerDownCannotMoveTheCamera()
        {
            var gesture=new ClinicGesture();
            Assert.That(gesture.Move(9,Vector2.one,out _,out _,out _),Is.False);
            Assert.That(gesture.End(9,Vector2.one),Is.False);
        }
    }
}
