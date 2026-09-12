using System;
using System.Linq;
using IdleClinic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleClinic.App
{
    /// <summary>Read-only travel eligibility; the profile transaction repeats every gate before saving.</summary>
    public static class ClinicLocationReadout
    {
        public static bool CanOpen(ClinicState starter,long wallet,bool alreadyOpen)
            =>!alreadyOpen&&starter!=null&&wallet>=ClinicRules.DoctorsClinicUnlockCost&&ClinicRules.StarterCompletion(starter).Count==0;
        public static string Name(ClinicLocation location)=>location==ClinicLocation.DoctorsClinic?"Doctors clinic":"Starter clinic";
        public static string Income(ClinicLocation location)=>location==ClinicLocation.DoctorsClinic?"2× income":"1× income";
    }

    public sealed partial class ClinicApp
    {
        private bool locationsOpen;
        private Button locationControl;
        private Label locationMultiplier;

        private void BuildLocationControl(VisualElement parent)
        {
            locationControl=IconButton(parent,ClinicGlyph.Locations,"Locations",ToggleLocations,"round-control location-control",
                ()=>ClinicLocationReadout.Name(State.Location)+", "+ClinicLocationReadout.Income(State.Location));
            locationControl.name="clinic-locations";
            locationMultiplier=Text(locationControl,"1×","location-multiplier",true);
        }
        private void UpdateLocationReadouts()
        {
            if(locationControl==null)return;
            locationControl.style.display=State.Tutorial==ClinicTutorialStep.Complete?DisplayStyle.Flex:DisplayStyle.None;
            locationMultiplier.text=State.Location==ClinicLocation.DoctorsClinic?"2×":"1×";
        }
        private void ToggleLocations()
        {
            locationsOpen=!locationsOpen;selectedRoom=null;selectedObject=null;settingsOpen=false;
            world.SelectRoom(default(IdleClinic.Presentation.ClinicHit));dockKey="";UpdateReadouts();
        }
        private void BuildLocationsDock()
        {
            dock.AddToClassList("locations-dock");
            var body=new ScrollView(ScrollViewMode.Vertical){name="clinic-locations-content",horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            body.AddToClassList("bounded-dock-content");dock.Add(body);
            BindTouchCaptureLifecycle(body.contentContainer);BindTouchCaptureLifecycle(body.contentViewport);
            var unlocked=profile.doctorsState!=null;
            Text(body,ClinicLocationReadout.Name(State.Location)+" · "+ClinicLocationReadout.Income(State.Location),"room-detail");
            if(unlocked)
            {
                var row=Box(body,"location-destinations");
                LocationDestination(row,ClinicLocation.StarterClinic,ClinicGlyph.Home);
                LocationDestination(row,ClinicLocation.DoctorsClinic,ClinicGlyph.Doctor);
                Text(body,"Both clinics keep caring while you travel.","location-note");
                return;
            }
            var starter=profile.state;
            var remaining=ClinicRules.StarterCompletion(starter).Count;
            var top=Box(body,"location-preview");top.Add(new ClinicIcon(ClinicGlyph.Doctor,30));
            var words=Box(top,"location-preview-copy");
            Text(words,"Small Doctors Clinic","location-name",true);
            Text(words,"2× income · 4 doctors · pharmacy","location-note");
            if(remaining>0)
            {
                Text(body,remaining+" improvements left to unlock","location-progress",true);
                var checklist=Box(body,"location-checklist");checklist.name="clinic-unlock-checklist";
                foreach(var room in starter.Rooms)
                {
                    var kind=room.Kind;
                    var complete=room.Built&&room.Tier==ClinicRules.MaximumTier(starter)
                        &&room.EquipmentLevel==6&&room.FacilitiesLevel==6&&room.DecorationLevel==6
                        &&!starter.Construction.Any(c=>c.Room==kind);
                    UnlockRequirement(checklist,RoomName(kind),complete,()=>Select(kind));
                }
                foreach(var role in new[]{ClinicStaffRole.Receptionist,ClinicStaffRole.Nurse})
                {
                    var capturedRole=role;
                    for(var station=0;station<ClinicRules.MaximumStaff(starter,role);station++)
                    {
                        var id=station;
                        var staff=starter.Staff.FirstOrDefault(s=>s.Role==role&&s.StationId==id);
                        var exists=StationIds(starter,role).Contains(id);
                        var complete=exists&&ClinicRules.StationLevel(starter,role,id)==6&&staff!=null&&staff.TrainingLevel==6;
                        UnlockRequirement(checklist,(role==ClinicStaffRole.Receptionist?"Reception":"Nursing")+" team "+(id+1),complete,
                            ()=>{if(exists)SelectObject(StationHit(capturedRole,id));else Select(ClinicRules.RoomForRole(capturedRole));});
                    }
                }
                foreach(var kind in new[]{ClinicAmenity.Parking,ClinicAmenity.Toilet,ClinicAmenity.Vending})
                {
                    var amenity=kind;
                    UnlockRequirement(checklist,kind==ClinicAmenity.Parking?"Car park":kind==ClinicAmenity.Toilet?"Toilet":"Vending machine",
                        starter.Amenity(kind).Level==ClinicRules.MaximumAmenityLevel(starter,kind),
                        ()=>{if(amenity==ClinicAmenity.Parking||starter.Room(ClinicRoom.Waiting).Built)SelectObject(AmenityHit(amenity));else Select(ClinicRoom.Waiting);});
                }
            }
            else Text(body,"Everything is ready for your next clinic.","location-progress",true);
            var open=IconButton(body,ClinicGlyph.Locations,"Open doctors clinic for 100,000 coins",()=>TryOpenDoctorsClinic(),"location-open primary-action");
            open.name="open-doctors-clinic";
            var purchaseWords=Box(open,"location-purchase-copy");
            Text(purchaseWords,remaining==0?"Open doctors clinic":"Complete the starter clinic","purchase-detail");
            var cost=Box(purchaseWords,"cost-row");cost.Add(new ClinicIcon(ClinicGlyph.Coin,17));Text(cost,"100,000","purchase-price",true);
            readouts.Add(()=>open.SetEnabled(ClinicLocationReadout.CanOpen(profile.state,State.Wallet,profile.doctorsState!=null)));
        }
        private void LocationDestination(VisualElement parent,ClinicLocation location,ClinicGlyph glyph)
        {
            var current=State.Location==location;
            var button=IconButton(parent,glyph,"Travel to "+ClinicLocationReadout.Name(location),()=>TrySelectLocation(location),"location-destination");
            button.name=location==ClinicLocation.StarterClinic?"travel-starter-clinic":"travel-doctors-clinic";
            Text(button,location==ClinicLocation.StarterClinic?"Starter clinic":"Doctors clinic","location-name",true);
            Text(button,current?"Here · "+ClinicLocationReadout.Income(location):ClinicLocationReadout.Income(location),"location-note");
            button.SetEnabled(!current);
        }
        private void UnlockRequirement(VisualElement parent,string name,bool complete,Action action)
        {
            var row=IconButton(parent,complete?ClinicGlyph.Check:ClinicGlyph.Upgrade,name+(complete?", complete":", improvements needed"),action,"unlock-requirement");
            Text(row,name,"unlock-name");
            Text(row,complete?"Done":"Improve","unlock-status",true);
            row.EnableInClassList("requirement-complete",complete);
        }

        // Called only after the complete campaign switch is saved and the active world is rebound.
        private void ResetLocationPresentation()
        {
            CancelWorldGesture();
            selectedRoom=null;selectedObject=null;settingsOpen=false;locationsOpen=false;dockKey="";
            lastTutorial=State.Tutorial;wideWorldMarkers=false;
            ClearLocationMarkersAndFlights();
            world.Home(ReducedMotion);
            UpdateReadouts();UpdateAccessibilityFrames();
        }
        private void ClearLocationMarkersAndFlights()
        {
            foreach(var flight in flights){flight.Icon.RemoveFromHierarchy();coinPool.Push(flight.Icon);}flights.Clear();
            walletPulseUntil=0;if(wallet!=null)wallet.style.scale=new Scale(Vector3.one);
            foreach(var marker in cashMarkers.Values)marker.RemoveFromHierarchy();cashMarkers.Clear();
            foreach(var marker in patientRings.Values)marker.RemoveFromHierarchy();patientRings.Clear();
            foreach(var marker in constructionMarkers.Values)marker.RemoveFromHierarchy();constructionMarkers.Clear();
            foreach(var marker in roomTargets.Values)marker.RemoveFromHierarchy();roomTargets.Clear();
            foreach(var marker in objectTargets.Values)marker.RemoveFromHierarchy();objectTargets.Clear();objectHits.Clear();
            waitingMarker?.RemoveFromHierarchy();waitingMarker=null;
            vendingCashMarker?.RemoveFromHierarchy();vendingCashMarker=null;
        }
    }
}
