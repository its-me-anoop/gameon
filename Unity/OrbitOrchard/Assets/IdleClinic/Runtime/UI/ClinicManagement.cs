using System;
using System.Linq;
using IdleClinic.Core;
using IdleClinic.Presentation;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleClinic.App
{
    public static class ClinicServicePresentation
    {
        public static bool HasProgress(ClinicPatientPhase phase)=>phase==ClinicPatientPhase.CheckingIn
            ||phase==ClinicPatientPhase.Treating||phase==ClinicPatientPhase.UsingAmenity;
    }

    public static class ClinicCashPresentation
    {
        public static bool IsVendingCollection(ClinicEvent change)=>change.Kind==ClinicEventKind.CashCollected
            &&change.DeskId==-1&&change.Amenity==ClinicAmenity.Vending;
    }

    /// <summary>Read-only control values from the same rules used to schedule service.</summary>
    public sealed class ClinicWorkstationReadout
    {
        public int EquipmentLevel,TrainingLevel,StaffId,ServiceTicks,NextEquipmentTicks,NextTrainingTicks;
        public long EquipmentPrice,TrainingPrice;
        public bool EquipmentCapped,TrainingCapped;
        public static ClinicWorkstationReadout Create(ClinicState state,ClinicStaffRole role,int stationId)
        {
            var staff=state.Staff.FirstOrDefault(s=>s.Role==role&&s.StationId==stationId);
            var cap=ClinicRules.TrackCap(state.Room(role==ClinicStaffRole.Nurse?ClinicRoom.FirstAid:ClinicRoom.Reception).Tier);
            var level=ClinicRules.StationLevel(state,role,stationId);
            return new ClinicWorkstationReadout
            {
                EquipmentLevel=level,TrainingLevel=staff?.TrainingLevel??0,StaffId=staff?.Id??-1,
                EquipmentPrice=ClinicRules.StationUpgradeCost(state,role,stationId),TrainingPrice=staff==null?0:ClinicRules.StaffTrainingCost(staff),
                EquipmentCapped=level>=cap,TrainingCapped=staff!=null&&staff.TrainingLevel>=cap,
                ServiceTicks=ClinicRules.StationServiceTicks(state,role,stationId),
                NextEquipmentTicks=ClinicRules.StationServiceTicks(state,role,stationId,equipmentLevelsAdded:1),
                NextTrainingTicks=ClinicRules.StationServiceTicks(state,role,stationId,trainingLevelsAdded:1)
            };
        }
    }

    public sealed partial class ClinicApp
    {
        private static ClinicAmenity AmenityKind(ClinicHitKind kind)=>kind==ClinicHitKind.Parking?ClinicAmenity.Parking:
            kind==ClinicHitKind.Toilet?ClinicAmenity.Toilet:ClinicAmenity.Vending;
        private static ClinicHit AmenityHit(ClinicAmenity kind)=>new ClinicHit(kind==ClinicAmenity.Parking?ClinicHitKind.Parking:
            kind==ClinicAmenity.Toilet?ClinicHitKind.Toilet:ClinicHitKind.Vending,(int)kind);
        private static ClinicGlyph AmenityGlyph(ClinicAmenity kind)=>kind==ClinicAmenity.Parking?ClinicGlyph.Parking:
            kind==ClinicAmenity.Toilet?ClinicGlyph.Toilet:ClinicGlyph.Vending;
        private static string ObjectName(ClinicHit hit)=>hit.Kind==ClinicHitKind.Desk?"Reception desk "+(hit.Id+1):
            hit.Kind==ClinicHitKind.Station?"Nursing station "+(hit.Id+1):hit.Kind==ClinicHitKind.Parking?"Car park":
            hit.Kind==ClinicHitKind.Toilet?"Waiting room toilet":"Vending machine";
        private static string ServiceTime(int ticks)=>(ticks/(double)ClinicRules.TicksPerSecond)
            .ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"s";

        private void SelectObject(ClinicHit hit)
        {
            if(!ClinicSelectionPolicy.CanSelectObject(State,hit))return;
            settingsOpen=false;selectedRoom=null;selectedObject=hit;world.SelectRoom(hit);dockKey="";UpdateReadouts();
        }
        private void CollectVending()=>Run(()=>simulation.CollectVendingTips());

        private void BuildRoomShortcuts(VisualElement heading,ClinicRoom room)
        {
            if(room==ClinicRoom.Reception)
                foreach(var desk in State.ReceptionDesks)ObjectShortcut(heading,new ClinicHit(ClinicHitKind.Desk,desk.Id),ClinicGlyph.Reception,desk.Id+1);
            else if(room==ClinicRoom.FirstAid)
                foreach(var station in State.TreatmentStations)ObjectShortcut(heading,new ClinicHit(ClinicHitKind.Station,station.Id),ClinicGlyph.Bed,station.Id+1);
            else if(State.Room(room).Built)
            {
                ObjectShortcut(heading,AmenityHit(ClinicAmenity.Toilet),ClinicGlyph.Toilet);
                ObjectShortcut(heading,AmenityHit(ClinicAmenity.Vending),ClinicGlyph.Vending);
            }
        }
        private void ObjectShortcut(VisualElement parent,ClinicHit hit,ClinicGlyph glyph,int number=0)
        {
            var button=IconButton(parent,glyph,"Manage "+ObjectName(hit),()=>SelectObject(hit),"object-shortcut");
            if(number>0)Text(button,number.ToString(),"shortcut-number",true);
        }

        private void BuildManagementDock(ClinicHit hit)
        {
            dock.AddToClassList("upgrade-dock");dock.AddToClassList("management-dock");
            if(hit.Kind==ClinicHitKind.Desk||hit.Kind==ClinicHitKind.Station)BuildWorkstationDock(hit);
            else BuildAmenityDock(AmenityKind(hit.Kind));
        }
        private void BuildWorkstationDock(ClinicHit hit)
        {
            var role=hit.Kind==ClinicHitKind.Desk?ClinicStaffRole.Receptionist:ClinicStaffRole.Nurse;
            var room=State.Room(role==ClinicStaffRole.Nurse?ClinicRoom.FirstAid:ClinicRoom.Reception);
            var view=ClinicWorkstationReadout.Create(State,role,hit.Id);
            Text(dock,ServiceTime(view.ServiceTicks)+" per patient · Room "+room.Tier,"room-detail");
            var upgrades=Box(dock,"upgrades");
            WorkstationUpgrade(upgrades,ClinicGlyph.Equipment,"Equipment",view.EquipmentLevel,view.EquipmentPrice,view.EquipmentCapped,
                view.NextEquipmentTicks,room,()=>simulation.UpgradeStation(role,hit.Id),"station-equipment-"+role+"-"+hit.Id);
            if(view.StaffId>=0)
                WorkstationUpgrade(upgrades,ClinicGlyph.Training,"Staff training",view.TrainingLevel,view.TrainingPrice,view.TrainingCapped,
                    view.NextTrainingTicks,room,()=>simulation.TrainStaff(view.StaffId),"staff-training-"+view.StaffId);
            else
                Purchase(upgrades,ClinicGlyph.Nurse,"Hire nurse",ClinicRules.HireNurseCost(State.Staff.Count(s=>s.Role==ClinicStaffRole.Nurse)),
                    ()=>true,()=>simulation.HireNurse(),"Staff this station","primary-action");
            var actions=Box(dock,"action-row");
            RoomShortcut(actions,room.Kind);
            var other=role==ClinicStaffRole.Nurse?State.TreatmentStations.Select(s=>s.Id):State.ReceptionDesks.Select(d=>d.Id);
            foreach(var id in other.Where(id=>id!=hit.Id))ObjectShortcut(actions,new ClinicHit(hit.Kind,id),role==ClinicStaffRole.Nurse?ClinicGlyph.Bed:ClinicGlyph.Reception,id+1);
        }
        private void WorkstationUpgrade(VisualElement parent,ClinicGlyph glyph,string label,int level,long price,bool capped,
            int nextTicks,ClinicRoomState room,Func<ClinicCommandResult> action,string controlName)
        {
            Action activate=()=>{if(capped){Select(room.Kind);FocusRenovation(room);}else Run(action);};
            var button=new Button(activate){name=controlName};button.AddToClassList("upgrade-button");
            var top=Box(button,"upgrade-top");top.Add(new ClinicIcon(glyph,24));Text(top,level.ToString(),"level",true);
            Text(button,capped?label:ServiceTime(nextTicks)+" / patient","upgrade-label");
            Text(button,capped?(room.Tier<3?"Room "+(room.Tier+1):"Max"):Money(price),"upgrade-price",true);
            button.tooltip=label+" level "+level+". "+(capped?(room.Tier<3?"Requires room "+(room.Tier+1):"Fully improved"):
                Money(price)+" coins. Next service "+ServiceTime(nextTicks));
            parent.Add(button);readouts.Add(()=>button.SetEnabled(capped||State.Wallet>=price));
            RegisterAccessibleButton(button,button.tooltip,activate);
        }
        private void RoomShortcut(VisualElement parent,ClinicRoom room)
        {
            var button=IconButton(parent,ClinicGlyph.Room,"Manage "+RoomName(room),()=>Select(room),"room-shortcut");
            Text(button,"Room upgrades","purchase-detail");
        }
        private void BuildAmenityDock(ClinicAmenity kind)
        {
            var amenity=State.Amenity(kind);var level=amenity.Level;
            var cap=kind==ClinicAmenity.Parking?3:State.Room(ClinicRoom.Waiting).Tier;
            var maxed=level>=3;var capped=level>=cap;
            Text(dock,level==0?"Make visits more comfortable":"Level "+level+" · "+AmenityBenefit(kind,level),"room-detail");
            var row=Box(dock,"action-row");
            if(capped)
            {
                if(maxed)Text(row,"Fully improved","max-room");
                else
                {
                    var upgrade=IconButton(row,ClinicGlyph.Upgrade,"Expand waiting room to upgrade "+ObjectName(new ClinicHit(
                        kind==ClinicAmenity.Toilet?ClinicHitKind.Toilet:ClinicHitKind.Vending)),
                        ()=>{Select(ClinicRoom.Waiting);FocusRenovation(State.Room(ClinicRoom.Waiting));},"room-shortcut");
                    Text(upgrade,"Needs room "+(cap+1),"purchase-detail");
                }
            }
            else
                Purchase(row,AmenityGlyph(kind),(level==0?"Build ":"Upgrade ")+AmenityName(kind),ClinicRules.AmenityUpgradeCost(kind,level),
                    ()=>true,()=>simulation.UpgradeAmenity(kind),AmenityBenefit(kind,level+1),"primary-action");
            if(kind==ClinicAmenity.Vending&&level>0)
            {
                var collect=IconButton(row,ClinicGlyph.Coin,"Collect vending tips",CollectVending,"vending-collect",
                    ()=>State.Amenity(ClinicAmenity.Vending).Till.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" coins");
                var label=Text(collect,"","purchase-price",true);
                readouts.Add(()=>{label.text=Money(State.Amenity(ClinicAmenity.Vending).Till);collect.SetEnabled(State.Amenity(ClinicAmenity.Vending).Till>0);});
            }
            if(kind!=ClinicAmenity.Parking)
            {
                var footer=Box(dock,"management-footer");RoomShortcut(footer,ClinicRoom.Waiting);
                var other=kind==ClinicAmenity.Toilet?ClinicHitKind.Vending:ClinicHitKind.Toilet;
                ObjectShortcut(footer,AmenityHit(AmenityKind(other)),kind==ClinicAmenity.Toilet?ClinicGlyph.Vending:ClinicGlyph.Toilet);
            }
        }
        private static string AmenityName(ClinicAmenity kind)=>kind==ClinicAmenity.Parking?"car park":kind==ClinicAmenity.Toilet?"toilet":"vending machine";
        private static string AmenityBenefit(ClinicAmenity kind,int level)=>kind==ClinicAmenity.Parking?(level*2)+" bays · "+(level*5)+" coins/car":
            kind==ClinicAmenity.Vending?(level*5)+" coins/tip":"More comfort · better tips";

        private void UpdateManagementMarkers()
        {
            var unlocked=State.Tutorial==ClinicTutorialStep.Complete;
            foreach(var desk in State.ReceptionDesks)
                ManagementMarker(new ClinicHit(ClinicHitKind.Desk,desk.Id),world.GetDeskPoint(desk.Id),unlocked);
            foreach(var station in State.TreatmentStations)
                ManagementMarker(new ClinicHit(ClinicHitKind.Station,station.Id),world.GetStationPoint(station.Id),unlocked);
            foreach(var kind in new[]{ClinicAmenity.Parking,ClinicAmenity.Toilet,ClinicAmenity.Vending})
            {
                var hit=AmenityHit(kind);
                ManagementMarker(hit,world.GetAmenityPoint(kind),unlocked&&(kind==ClinicAmenity.Parking||State.Room(ClinicRoom.Waiting).Built));
            }
            if(vendingCashMarker==null)
            {
                vendingCashMarker=Box(overlay,"cash-marker");vendingCashMarker.pickingMode=PickingMode.Ignore;
                vendingCashMarker.Add(new ClinicIcon(ClinicGlyph.Coin,19,new Color(.46f,.28f,.07f)));
                Text(vendingCashMarker,"","cash-amount",true);
                RegisterAccessibleButton(vendingCashMarker,"Collect vending machine tips",CollectVending,
                    ()=>State.Amenity(ClinicAmenity.Vending).Till.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" coins");
            }
            var till=State.Amenity(ClinicAmenity.Vending).Till;
            vendingCashMarker.Q<Label>().text=Money(till);
            ClinicMarkerPresentation.CashDetail(vendingCashMarker,wideWorldMarkers);
            PositionMarker(vendingCashMarker,world.WorldToViewport(world.GetVendingCashPoint()),till>0,-30,
                wideWorldMarkers?new Vector2(44,44):(Vector2?)null);
        }
        // Physical taps use the same minimum-size rectangles as accessibility.
        // Cash is checked before this lookup; nearby objects share space by distance.
        private bool TryPickManagementTarget(Vector2 point,out ClinicHit hit)
        {
            hit=default(ClinicHit);
            if(root==null||!root.worldBound.Contains(point)||WorldPointIsCovered(point)||RoomPointHasHigherPriorityAction(point))return false;
            var found=false;var nearest=float.PositiveInfinity;
            foreach(var pair in objectHits)
            {
                var target=pair.Key;var candidate=pair.Value;
                if(!target.enabledInHierarchy||!ClinicSelectionPolicy.CanSelectObject(State,candidate)
                    ||!target.worldBound.Contains(point)||!IsAccessible(target))continue;
                var distance=(point-target.worldBound.center).sqrMagnitude;
                var tied=Mathf.Approximately(distance,nearest);
                if(found&&(distance>nearest&&!tied||tied&&((int)candidate.Kind>(int)hit.Kind
                    ||candidate.Kind==hit.Kind&&candidate.Id>=hit.Id)))continue;
                hit=candidate;nearest=distance;found=true;
            }
            return found;
        }

        private void ManagementMarker(ClinicHit hit,Vector3 point,bool visible)
        {
            var key=hit.Kind+":"+hit.Id;
            if(!objectTargets.TryGetValue(key,out var target))
            {
                target=new VisualElement{pickingMode=PickingMode.Ignore};target.style.position=Position.Absolute;
                target.style.width=44;target.style.height=44;overlay.Add(target);objectTargets.Add(key,target);objectHits.Add(target,hit);
                RegisterAccessibleButton(target,"Manage "+ObjectName(hit),()=>SelectObject(hit));
            }
            PositionMarker(target,world.WorldToViewport(point),visible);
        }
    }
}
