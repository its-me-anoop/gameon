using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IdleClinic.App;
using IdleClinic.Core;
using IdleClinic.Presentation;
using IdleClinic.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleClinic.Tests
{
    public sealed class DoctorsHUDTests
    {
        [TestCase(ClinicPatientPhase.Consulting,true)]
        [TestCase(ClinicPatientPhase.Dispensing,true)]
        [TestCase(ClinicPatientPhase.WalkingToConsultation,false)]
        [TestCase(ClinicPatientPhase.WalkingToPharmacy,false)]
        public void NewProgressRingsFollowActualServicePhases(ClinicPatientPhase phase,bool shown)
            =>Assert.That(ClinicServicePresentation.HasProgress(phase),Is.EqualTo(shown));

        [TestCase(ClinicStaffRole.Doctor,ClinicHitKind.DoctorStation,200)]
        [TestCase(ClinicStaffRole.Pharmacist,ClinicHitKind.PharmacyStation,300)]
        public void NewWorkstationDockShowsItsAssignedStaffAndAuthoritativePrices(ClinicStaffRole role,ClinicHitKind hit,int id)
        {
            using(var ui=new DockFixture())
            {
                var state=ui.Simulation.State;
                var dock=ui.ObjectDock(new ClinicHit(hit,0));
                Assert.That(dock.Q<Button>("station-equipment-"+role+"-0"),Is.Not.Null);
                Assert.That(dock.Q<Button>("staff-training-"+id),Is.Not.Null);
                Assert.That(dock.Q<Button>("staff-training-"+id).tooltip,
                    Does.Contain(ClinicRules.StaffTrainingCost(state,state.Staff.Single(s=>s.Id==id))+" coins"));
                Assert.That(dock.Q<Button>("station-equipment-"+role+"-0").tooltip,
                    Does.Contain(ClinicRules.StationUpgradeCost(state,role,0)+" coins"));
                Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(hit,0)),Is.True);
                Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(hit,3)),Is.False,
                    "An unbuilt workstation cannot be selected by a stale marker.");
            }
        }
        [TestCase(ClinicStaffRole.Receptionist,ClinicHitKind.Desk,3)]
        [TestCase(ClinicStaffRole.Nurse,ClinicHitKind.Station,3)]
        [TestCase(ClinicStaffRole.Doctor,ClinicHitKind.DoctorStation,3)]
        [TestCase(ClinicStaffRole.Pharmacist,ClinicHitKind.PharmacyStation,1)]
        public void LastWorkstationAndLevelTwelveRemainReachable(ClinicStaffRole role,ClinicHitKind hit,int last)
        {
            using(var ui=new DockFixture())
            {
                var state=ui.Simulation.State;
                var room=state.Room(ClinicRules.RoomForRole(role));room.Tier=6;
                if(role==ClinicStaffRole.Receptionist)state.ReceptionDesks.Add(new ReceptionDeskState{Id=last,EquipmentLevel=11});
                else ClinicRules.Stations(state,role).Add(new TreatmentStationState{Id=last,EquipmentLevel=11});
                state.Staff.Add(new ClinicStaffState{Id=ClinicRules.StaffId(role,last),Role=role,StationId=last,TrainingLevel=11});
                var view=ClinicWorkstationReadout.Create(state,role,last);
                Assert.That(view.EquipmentCapped,Is.False);Assert.That(view.TrainingCapped,Is.False);
                var dock=ui.ObjectDock(new ClinicHit(hit,last));
                Assert.That(dock.Q<Button>("station-equipment-"+role+"-"+last),Is.Not.Null);
                Assert.That(dock.Q<Button>("staff-training-"+ClinicRules.StaffId(role,last)),Is.Not.Null);
                Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(hit,last)),Is.True);
                if(role==ClinicStaffRole.Receptionist)state.ReceptionDesks.Single(s=>s.Id==last).EquipmentLevel=12;
                else ClinicRules.Stations(state,role).Single(s=>s.Id==last).EquipmentLevel=12;
                state.Staff.Single(s=>s.Id==ClinicRules.StaffId(role,last)).TrainingLevel=12;
                var maxed=ClinicWorkstationReadout.Create(state,role,last);
                Assert.That(maxed.EquipmentCapped,Is.True);Assert.That(maxed.TrainingCapped,Is.True);
                Assert.That(ui.ObjectDock(new ClinicHit(hit,last)).Query<Label>().ToList().Count(l=>l.text=="Max"),Is.EqualTo(2));
            }
        }
        [TestCase(ClinicRoom.Reception)] [TestCase(ClinicRoom.FirstAid)] [TestCase(ClinicRoom.Consultation)] [TestCase(ClinicRoom.Pharmacy)]
        public void RoomRenovationAndUpgradesContinuePastOldTierThree(ClinicRoom kind)
        {
            using(var ui=new DockFixture())
            {
                var room=ui.Simulation.State.Room(kind);room.Tier=5;room.EquipmentLevel=10;
                var dock=ui.RoomDock(kind);
                var expand=dock.Q<Button>("expand-room");Assert.That(expand,Is.Not.Null);
                Assert.That(dock.Query<Label>().ToList().Any(l=>l.text=="Room 6 · 162m"),Is.True);
                Assert.That(dock.Q<Button>("upgrade-"+kind.ToString().ToLowerInvariant()+"-equipment").tooltip,Does.Contain("Requires room 6"));
                room.Tier=6;
                Assert.That(ui.RoomDock(kind).Q<Button>("expand-room"),Is.Null);
            }
        }
        [TestCase(ClinicAmenity.Parking,ClinicHitKind.Parking,"upgrade-car-park")]
        [TestCase(ClinicAmenity.Toilet,ClinicHitKind.Toilet,"upgrade-toilet")]
        [TestCase(ClinicAmenity.Vending,ClinicHitKind.Vending,"upgrade-vending-machine")]
        [TestCase(ClinicAmenity.Taxi,ClinicHitKind.Taxi,"upgrade-taxi-stand")]
        public void AmenitiesUseSixLevelsAndCurrentLocationPrice(ClinicAmenity kind,ClinicHitKind hit,string button)
        {
            using(var ui=new DockFixture())
            {
                var state=ui.Simulation.State;state.Room(ClinicRoom.Waiting).Tier=6;state.Amenity(kind).Level=5;
                Assert.That(ui.ObjectDock(new ClinicHit(hit)).Q<Button>(button),Is.Not.Null);
                state.Amenity(kind).Level=6;
                var dock=ui.ObjectDock(new ClinicHit(hit));
                Assert.That(dock.Q<Button>(button),Is.Null);
                Assert.That(dock.Query<Label>().ToList().Any(l=>l.text=="Fully improved"),Is.True);
                Assert.That(ClinicSelectionPolicy.CanSelectObject(state,new ClinicHit(hit)),Is.True);
            }
        }
        [Test]
        public void NewRoleHiringWaitsForDistinctStationsAndUsesTheRealExponentialPrice()
        {
            using(var ui=new DockFixture())
            {
                var state=ui.Simulation.State;
                Assert.That(ui.RoomDock(ClinicRoom.Consultation).Q<Button>("hire-doctor"),Is.Null);
                state.Room(ClinicRoom.Consultation).Tier=2;
                Assert.That(ui.RoomDock(ClinicRoom.Consultation).Q<Button>("add-consultation-room"),Is.Not.Null);
                state.ConsultationStations.Add(new TreatmentStationState{Id=1});state.Room(ClinicRoom.Consultation).StationCount=2;
                var hire=ui.RoomDock(ClinicRoom.Consultation).Q<Button>("hire-doctor");
                Assert.That(hire,Is.Not.Null);Assert.That(hire.tooltip,Does.Contain("2,700 coins"));
            }
        }
        [Test]
        public void TravelReadoutRequiresEveryStarterImprovementAndExactBoundary()
        {
            var state=MaxedStarter();
            Assert.That(ClinicLocationReadout.CanOpen(state,99999,false),Is.False);
            Assert.That(ClinicLocationReadout.CanOpen(state,100000,false),Is.True);
            Assert.That(ClinicLocationReadout.CanOpen(state,100000,true),Is.False);
            state.Staff[0].TrainingLevel=5;
            Assert.That(ClinicLocationReadout.CanOpen(state,100000,false),Is.False);
            Assert.That(state.Wallet,Is.Zero,"Eligibility is a readout, never the opening debit.");
        }
        [Test]
        public void LockedLocationOffersAnOnDemandChecklistAndOneDisabledPurchase()
        {
            using(var ui=new DockFixture())
            {
                var starterSimulation=ClinicSimulation.CreateNew();
                ui.Profile.doctorsState=null;ui.Profile.state=starterSimulation.State;
                ui.Profile.state.Tutorial=ClinicTutorialStep.Complete;
                ui.ReplaceSimulation(starterSimulation);
                var dock=ui.LocationsDock();
                Assert.That(dock.Q<ScrollView>("clinic-unlock-checklist"),Is.Not.Null);
                Assert.That(dock.Q<Button>("open-doctors-clinic").enabledSelf,Is.False);
                Assert.That(dock.Query<Button>().ToList().Count(b=>b.ClassListContains("unlock-requirement")),Is.EqualTo(10));
                MaxedStarter(starterSimulation.State);ui.Profile.state.Wallet=100000;
                dock=ui.LocationsDock();
                Assert.That(dock.Q<ScrollView>("clinic-unlock-checklist"),Is.Null);
                Assert.That(dock.Q<Button>("open-doctors-clinic").enabledSelf,Is.True);
            }
        }
        [Test]
        public void UnlockedTravelOffersBothLocationsAndDoesNotSellTheClinicAgain()
        {
            using(var ui=new DockFixture())
            {
                var dock=ui.LocationsDock();
                Assert.That(dock.Q<Button>("travel-starter-clinic").enabledSelf,Is.True);
                Assert.That(dock.Q<Button>("travel-doctors-clinic").enabledSelf,Is.False);
                Assert.That(dock.Q<Button>("open-doctors-clinic"),Is.Null);
                Assert.That(dock.Query<Label>().ToList().Any(l=>l.text=="Here · 2× income"),Is.True);
            }
        }
        [Test]
        public void QueuedRoomSelectionFromAnotherLocationCannotSelectMissingRooms()
        {
            var starter=ClinicSimulation.CreateNew().State;starter.Tutorial=ClinicTutorialStep.Complete;
            var doctors=ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic).State;
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(starter,ClinicRoom.Consultation),Is.False);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(starter,ClinicRoom.Pharmacy),Is.False);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(doctors,ClinicRoom.Consultation),Is.True);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(doctors,ClinicRoom.Pharmacy),Is.True);
        }
        [Test]
        public void SettingsExposeSeparateMusicAndEffectsPreferences()
        {
            using(var ui=new DockFixture())
            {
                ui.Profile.preferences.music=false;ui.Profile.preferences.sound=true;
                var dock=ui.SettingsDock();
                Assert.That(dock.Q<Button>("music").Q<Label>(className:"preference-value").text,Is.EqualTo("Off"));
                Assert.That(dock.Q<Button>("effects").Q<Label>(className:"preference-value").text,Is.EqualTo("On"));
                Assert.That(dock.Q<Button>("haptics"),Is.Not.Null);
                Assert.That(dock.Q<Button>("less-motion"),Is.Not.Null);
            }
        }
        [TestCase(false)]
        [TestCase(true)]
        public void SettingsAlwaysExposePrivacyAndSupportBeforeAnyPurchase(bool doctorsClinic)
        {
            using(var ui=new DockFixture())
            {
                if(!doctorsClinic)ui.ReplaceSimulation(ClinicSimulation.CreateNew());
                var dock=ui.SettingsDock();
                var privacy=dock.Q<Button>("privacy-policy");
                var support=dock.Q<Button>("contact-support");
                Assert.That(privacy,Is.Not.Null);
                Assert.That(support,Is.Not.Null);
                Assert.That(privacy.enabledSelf&&support.enabledSelf,Is.True);
                Assert.That(ui.AccessibleLabel(privacy),Is.EqualTo("Privacy policy, opens in browser"));
                Assert.That(ui.AccessibleLabel(support),Is.EqualTo("Contact support, opens in browser"));
                Assert.That(privacy.Q<Label>().text,Is.EqualTo("Privacy policy"));
                Assert.That(support.Q<Label>().text,Is.EqualTo("Contact support"));
            }
        }
        [TestCase(375,667,0,0,0,20)]
        [TestCase(1024,1366,0,20,0,24)]
        [TestCase(1194,834,0,20,0,24)]
        [TestCase(667,375,44,21,44,0)]
        [TestCase(393,852,0,34,0,59)]
        public void WindowLayoutKeepsControlsInsideSafeInsetsAndTheDockCompact(int width,int height,int left,int bottom,int right,int top)
        {
            var safe=ClinicViewportLayout.SafePanelArea(new Vector2(width,height),new Vector2(width*2,height*2),
                new Rect(left*2,bottom*2,(width-left-right)*2,(height-top-bottom)*2));
            Assert.That(safe.xMin,Is.EqualTo(left));Assert.That(safe.yMin,Is.EqualTo(top));
            Assert.That(safe.xMax,Is.EqualTo(width-right));Assert.That(safe.yMax,Is.EqualTo(height-bottom));
            var dock=ClinicViewportLayout.DockArea(safe);
            Assert.That(dock.width,Is.InRange(44,520));
            Assert.That(dock.center.x,Is.EqualTo(safe.center.x).Within(.001));
            Assert.That(dock.xMin,Is.GreaterThan(safe.xMin));Assert.That(dock.xMax,Is.LessThan(safe.xMax));
            Assert.That(dock.yMin,Is.GreaterThanOrEqualTo(safe.yMin+124),"Keep the wallet and camera toolbar clear.");
            Assert.That(dock.yMax,Is.LessThan(safe.yMax));
        }
        [TestCase(true)]
        [TestCase(false)]
        public void ResizingTheWindowCancelsOldCoordinateGesturesButRelayoutDoesNot(bool resized)
        {
            using(var ui=new DockFixture())
            {
                ui.VerifyViewportGestureLifecycle(resized);
            }
        }
        [TestCase(375,667,180,310)] [TestCase(320,568,10,10)] [TestCase(375,667,370,660)]
        public void FourOverlappingCashTargetsStaySeparateBoundedAndDeterministic(int width,int height,int x,int y)
        {
            var origins=Enumerable.Repeat(new Vector2(x,y),4).ToArray();var viewport=new Rect(0,0,width,height);
            var positions=ClinicMarkerPresentation.SeparateCashTargets(origins,viewport);
            Assert.That(positions,Is.EqualTo(ClinicMarkerPresentation.SeparateCashTargets(origins,viewport)),"Projection layout cannot drift frame to frame.");
            for(var i=0;i<4;i++)
            {
                var box=new Rect(positions[i]-Vector2.one*22,Vector2.one*44);
                Assert.That(box.xMin,Is.GreaterThanOrEqualTo(0));Assert.That(box.xMax,Is.LessThanOrEqualTo(width));
                Assert.That(box.yMin,Is.GreaterThanOrEqualTo(0));Assert.That(box.yMax,Is.LessThanOrEqualTo(height));
                for(var j=0;j<i;j++)Assert.That(box.Overlaps(new Rect(positions[j]-Vector2.one*22,Vector2.one*44)),Is.False);
            }
        }
        [Test]
        public void AlreadyDistinctFourCashTargetsKeepTheirExactProjections()
        {
            var origins=new[]{new Vector2(50,200),new Vector2(120,200),new Vector2(210,280),new Vector2(290,280)};
            Assert.That(ClinicMarkerPresentation.SeparateCashTargets(origins,new Rect(0,0,375,667)),Is.EqualTo(origins));
        }

        [Test]
        public void TravelClearsOldWorldTargetsAndPooledFlightsBeforeNewDeskIdsAreBound()
        {
            using(var ui=new DockFixture())ui.VerifyTravelCleanup();
        }

        private static ClinicState MaxedStarter(ClinicState state=null)
        {
            state=state??ClinicSimulation.CreateNew().State;state.Tutorial=ClinicTutorialStep.Complete;state.WaitingRoomUnlocked=true;
            foreach(var room in state.Rooms){room.Built=true;room.Tier=3;room.EquipmentLevel=room.FacilitiesLevel=room.DecorationLevel=6;room.StationCount=room.Kind==ClinicRoom.Waiting?0:2;}
            state.ReceptionDesks.Clear();state.TreatmentStations.Clear();state.Staff.Clear();
            for(var i=0;i<2;i++)
            {
                state.ReceptionDesks.Add(new ReceptionDeskState{Id=i,EquipmentLevel=6});
                state.TreatmentStations.Add(new TreatmentStationState{Id=i,EquipmentLevel=6});
                foreach(var role in new[]{ClinicStaffRole.Receptionist,ClinicStaffRole.Nurse})
                    state.Staff.Add(new ClinicStaffState{Id=ClinicRules.StaffId(role,i),Role=role,StationId=i,TrainingLevel=6});
            }
            foreach(var amenity in state.Amenities)amenity.Level=3;
            return state;
        }
        private sealed class DockFixture:IDisposable
        {
            public ClinicSimulation Simulation=ClinicSimulation.CreateForLocation(ClinicLocation.DoctorsClinic);
            public ClinicProfile Profile;
            private readonly GameObject owner;
            private readonly ClinicApp app;
            private readonly VisualElement root=new VisualElement();
            private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
            public DockFixture()
            {
                owner=new GameObject("Doctors clinic HUD test");owner.SetActive(false);app=owner.AddComponent<ClinicApp>();
                Profile=new ClinicProfile{state=MaxedStarter(),doctorsState=Simulation.State,activeLocation=ClinicLocation.DoctorsClinic};
                Set("root",root);Set("simulation",Simulation);Set("profile",Profile);
            }
            private void Set(string name,object value)=>typeof(ClinicApp).GetField(name,Private).SetValue(app,value);
            private void Invoke(string name)=>typeof(ClinicApp).GetMethod(name,Private).Invoke(app,null);
            public void ReplaceSimulation(ClinicSimulation simulation){Simulation=simulation;Set("simulation",simulation);}
            private VisualElement Build(ClinicHit? hit=null,ClinicRoom? room=null,bool locations=false,bool settings=false)
            {
                var dock=new VisualElement();root.Clear();root.Add(dock);Set("dock",dock);
                Set("selectedRoom",room);Set("selectedObject",hit);Set("locationsOpen",locations);Set("settingsOpen",settings);
                Invoke("RebuildDock");
                foreach(var update in (List<Action>)typeof(ClinicApp).GetField("readouts",Private).GetValue(app))update();
                return dock;
            }
            public VisualElement ObjectDock(ClinicHit hit)=>Build(hit);
            public VisualElement RoomDock(ClinicRoom room)=>Build(room:room);
            public VisualElement LocationsDock()=>Build(locations:true);
            public VisualElement SettingsDock()=>Build(settings:true);
            private object AccessibleBinding(Button button)=>((System.Collections.IList)typeof(ClinicApp).GetField("accessible",Private).GetValue(app))
                .Cast<object>().Single(binding=>(VisualElement)binding.GetType().GetField("Element").GetValue(binding)==button);
            public string AccessibleLabel(Button button)
            {
                var binding=AccessibleBinding(button);return (string)binding.GetType().GetField("Label").GetValue(binding);
            }
            public void VerifyViewportGestureLifecycle(bool resized)
            {
                var gesture=(ClinicGesture)typeof(ClinicApp).GetField("gesture",Private).GetValue(app);
                var arbiter=(ClinicTouchArbiter)typeof(ClinicApp).GetField("touchArbiter",Private).GetValue(app);
                var captured=(HashSet<int>)typeof(ClinicApp).GetField("capturedPointers",Private).GetValue(app);
                gesture.Begin(7,Vector2.one);arbiter.Begin(7,Vector2.one,true);captured.Add(7);
                var oldBounds=new Rect(0,0,375,667);var newBounds=resized?new Rect(0,0,667,375):oldBounds;
                using(var change=GeometryChangedEvent.GetPooled(oldBounds,newBounds))
                    typeof(ClinicApp).GetMethod("OnViewportGeometryChanged",Private).Invoke(app,new object[]{change});
                Assert.That(gesture.PointerCount,Is.EqualTo(resized?0:1));
                Assert.That(arbiter.ActivePointers.Count(),Is.EqualTo(resized?0:1));
                Assert.That(captured.Count,Is.EqualTo(resized?0:1));
                if(resized)Assert.That(gesture.End(7,Vector2.one),Is.False,"An old-coordinate release cannot become a collection tap.");
            }
            public void VerifyTravelCleanup()
            {
                var cash=(Dictionary<int,VisualElement>)typeof(ClinicApp).GetField("cashMarkers",Private).GetValue(app);
                var rooms=(Dictionary<ClinicRoom,VisualElement>)typeof(ClinicApp).GetField("roomTargets",Private).GetValue(app);
                var objects=(Dictionary<string,VisualElement>)typeof(ClinicApp).GetField("objectTargets",Private).GetValue(app);
                var hits=(Dictionary<VisualElement,ClinicHit>)typeof(ClinicApp).GetField("objectHits",Private).GetValue(app);
                var marker=new VisualElement();root.Add(marker);cash.Add(0,marker);
                var room=new VisualElement();root.Add(room);rooms.Add(ClinicRoom.Consultation,room);
                var station=new VisualElement();root.Add(station);objects.Add("DoctorStation:0",station);hits.Add(station,new ClinicHit(ClinicHitKind.DoctorStation,0));
                var coin=new ClinicIcon(ClinicGlyph.Coin);root.Add(coin);
                var flightType=typeof(ClinicApp).GetNestedType("CoinFlight",BindingFlags.NonPublic);
                var flight=Activator.CreateInstance(flightType,true);flightType.GetField("Icon").SetValue(flight,coin);
                var flights=(System.Collections.IList)typeof(ClinicApp).GetField("flights",Private).GetValue(app);flights.Add(flight);
                Invoke("ClearLocationMarkersAndFlights");
                Assert.That(cash,Is.Empty);Assert.That(rooms,Is.Empty);Assert.That(objects,Is.Empty);Assert.That(hits,Is.Empty);Assert.That(flights,Is.Empty);
                Assert.That(marker.parent,Is.Null);Assert.That(room.parent,Is.Null);Assert.That(station.parent,Is.Null);Assert.That(coin.parent,Is.Null);
                Assert.That(((Stack<ClinicIcon>)typeof(ClinicApp).GetField("coinPool",Private).GetValue(app)).Count,Is.EqualTo(1));
                Assert.That(Simulation.State.Wallet,Is.Zero,"Clearing presentation cannot award or remove money.");
            }
            public void Dispose()=>UnityEngine.Object.DestroyImmediate(owner);
        }
    }
}
