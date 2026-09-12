using System;
using System.Collections.Generic;
using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Second-location set: room footprints double without enlarging furniture or people.</summary>
    internal sealed class DoctorsClinicWorld
    {
        internal static readonly Bounds[] RoomBounds={
            B(-6.85f,-5.50f,10.70f,4.70f),B(-6.85f,.60f,10.70f,4.70f),B(5.85f,-4.575f,8.70f,6.55f),
            B(-1f,6.8f,22.4f,4.4f),B(5.85f,1.425f,8.70f,3.05f)};
        private readonly ClinicArt art;private readonly Transform parent;private readonly ClinicWorld world;
        private readonly Action<string,Vector3,Vector3> anchor;private readonly Action<Transform,string,Vector3,Vector3> sockets;
        private readonly GameObject[,] workstations=new GameObject[4,4],futureStations=new GameObject[4,4];private readonly GameObject[,] cash=new GameObject[4,6];
        private readonly GameObject[] seats=new GameObject[30],toilets=new GameObject[2];
        private readonly List<Detail> details=new List<Detail>();private readonly List<ClinicDoor> doors=new List<ClinicDoor>();
        private readonly Transform selection;private readonly DoctorsClinicTransport transport;private readonly ClinicConstruction construction;
        private readonly GameObject vending,vendingCash,waitingCrates;private readonly Transform vendingButton,vendingTip;
        private readonly long[] tills=new long[4];private long vendingTill;private bool waitingBuilt;
        internal int DoorOpeningCount { get {int count=0;foreach(var door in doors)count+=door.OpeningCount;return count;} }
        internal Vector3 VendingCashPoint=>new Vector3(9.63f,1.38f,-.79f);
        private static Bounds B(float x,float z,float width,float depth)=>new Bounds(new Vector3(x,.14f,z),new Vector3(width,.02f,depth));
        internal DoctorsClinicWorld(ClinicArt art,Transform parent,ClinicWorld world,Action<string,Vector3,Vector3> anchor,Action<Transform,string,Vector3,Vector3> sockets)
        {
            this.art=art;this.parent=parent;this.world=world;this.anchor=anchor;this.sockets=sockets;
            Architecture();Furniture();StagedWorkstations();Anchors();ProgressionDetails();Neighbourhood();
            selection=art.Group("Doctors selection",parent);art.Box("Selection inset",selection,new Vector3(0,.16f,0),new Vector3(1,.015f,1),"Gold");selection.gameObject.SetActive(false);
            waitingCrates=art.Group("Future doctors waiting room",parent,new Vector3(5.9f,0,-4.5f)).gameObject;
            art.Box("Lounge delivery crate",waitingCrates.transform,new Vector3(0,.4f,0),new Vector3(1.4f,.6f,1),"Wood");
            for(int i=0;i<2;i++)toilets[i]=Toilet(i);
            var machine=art.Group("Doctors refreshment station",parent,new Vector3(9.05f,.14f,-.70f));vending=machine.gameObject;
            art.Box("Vending cabinet",machine,new Vector3(0,.84f,0),new Vector3(.95f,1.68f,.72f),"Apricot");
            art.Box("Vending window",machine,new Vector3(-.12f,1.0f,-.37f),new Vector3(.59f,1.10f,.025f),"SageDark");
            art.Box("Vending delivery slot",machine,new Vector3(0,.22f,-.37f),new Vector3(.60f,.17f,.04f),"Ink");
            vendingButton=art.Box("Vending button",machine,new Vector3(.35f,.9f,-.38f),new Vector3(.11f,.16f,.04f),"Gold").transform;
            for(int level=1;level<=6;level++)
            {var d=art.Group("Vending tier "+level,machine);for(int n=0;n<3;n++)art.Cylinder("Refreshment bottle",d,new Vector3(-.32f+n*.19f,.52f+level*.17f,-.40f),new Vector3(.11f,.13f,.08f),level%2==0?"Blue":"Gold");details.Add(Detail.Amenity(d.gameObject,ClinicAmenity.Vending,level));}
            art.Cylinder("Vending tip cup",parent,VendingCashPoint-Vector3.up*.17f,new Vector3(.24f,.27f,.24f),"Sage");
            vendingCash=art.Group("Doctors collectable vending tips",parent,VendingCashPoint).gameObject;
            for(int n=0;n<4;n++)art.Cylinder("Tip coin",vendingCash.transform,new Vector3((n%2)*.045f,n*.018f,0),new Vector3(.13f,.03f,.13f),"Gold");
            vendingTip=art.Cylinder("Doctors patient tip",parent,VendingCashPoint,new Vector3(.14f,.035f,.14f),"Gold").transform;
            transport=new DoctorsClinicTransport(art,parent);construction=new ClinicConstruction(art,parent,new[]{new Vector3(-12.6f,.14f,-5.5f),new Vector3(-12.6f,.14f,.6f),new Vector3(10.6f,.14f,-2.0f),new Vector3(10.6f,.14f,6.9f),new Vector3(10.6f,.14f,1.5f)});
        }
        internal Vector3 RoomPoint(ClinicRoom room)=>RoomBounds[(int)room].center+Vector3.up*1.1f;
        internal Vector3 WorkstationPoint(ClinicStaffRole role,int id)=>workstations[(int)role,Mathf.Clamp(id,0,role==ClinicStaffRole.Pharmacist?1:3)].transform.position+new Vector3(0,1.10f,-.15f);
        internal Vector3 AmenityPoint(ClinicAmenity kind)=>kind==ClinicAmenity.Parking?DoctorsParkingLayout.SignPoint:kind==ClinicAmenity.Toilet?new Vector3(12.2f,1.2f,-4.65f):kind==ClinicAmenity.Vending?new Vector3(9.05f,1.1f,-.70f):new Vector3(19.8f,1.4f,-8.90f);
        internal void Render(ClinicState state,ClinicActors actors,float delta,bool reduced)
        {
            waitingBuilt=state.Room(ClinicRoom.Waiting)?.Built==true;waitingCrates.SetActive(!waitingBuilt);
            int capacity=waitingBuilt?Mathf.Min(30,8+2*((state.Room(ClinicRoom.Waiting)?.FacilitiesLevel??1)-1)):0;
            for(int i=0;i<30;i++)seats[i].SetActive(i<capacity);
            for(int role=0;role<4;role++)for(int i=0;i<(role==3?2:4);i++)
            {int level=StationLevel(state,(ClinicStaffRole)role,i);workstations[role,i].SetActive(level>0);futureStations[role,i].SetActive(level==0);}
            for(int i=0;i<4;i++)
            {
                var desk=state.ReceptionDesks.Find(d=>d.Id==i);tills[i]=desk?.Till??0;
                int count=tills[i]>0?Mathf.Clamp(1+(int)Math.Log10(Math.Max(1,tills[i])),1,6):0;
                for(int n=0;n<6;n++)cash[i,n].SetActive(n<count);
            }
            foreach(var detail in details)detail.Render(state);
            int toiletLevel=state.Amenity(ClinicAmenity.Toilet)?.Level??0,vendingLevel=state.Amenity(ClinicAmenity.Vending)?.Level??0;
            for(int i=0;i<2;i++)toilets[i].SetActive(toiletLevel>0);
            vending.SetActive(vendingLevel>0);vendingTill=state.Amenity(ClinicAmenity.Vending)?.Till??0;vendingCash.SetActive(vendingLevel>0&&vendingTill>0);
            ClinicPatientState user=null;foreach(var p in state.Patients)if(p.Phase==ClinicPatientPhase.UsingAmenity&&p.VisitingAmenity==ClinicAmenity.Vending)user=p;
            vendingTip.gameObject.SetActive(user!=null&&!reduced);
            if(user!=null){float t=Mathf.Clamp01((float)((state.Tick+state.SubTick-user.PhaseStartedTick)/Math.Max(1,user.PhaseEndsTick-user.PhaseStartedTick)));vendingTip.position=Vector3.Lerp(world.GetAnchorPoint(user.ToAnchor)+Vector3.up,VendingCashPoint,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.22f;}
            vendingButton.localScale=new Vector3(.11f,.16f,.04f)*(user==null||reduced?1:.94f+.06f*Mathf.Cos((float)(state.Tick+state.SubTick)*.3f));
            foreach(var door in doors)door.Render(actors,delta,reduced);
            transport.Render(state,reduced);construction.Render(state,reduced);
        }
        private void Architecture()
        {
            art.Box("Doctors meadow",parent,new Vector3(0,-.30f,0),new Vector3(120,.15f,120),"Meadow");
            art.Box("Doctors clinic foundation",parent,new Vector3(-1,-.015f,-.55f),new Vector3(22.8f,.27f,19.7f),"Clay");
            art.Box("Doctors continuous corridor floor",parent,new Vector3(-1,.10f,-.60f),new Vector3(22.4f,.06f,19.4f),"TilePeach");
            for(int r=0;r<5;r++)
            {
                var b=RoomBounds[r];var root=art.Group(((ClinicRoom)r)+" room footprint",parent,b.center-Vector3.up*.14f);
                art.Box("Doctors room floor",root,new Vector3(0,.135f,0),new Vector3(b.size.x,.02f,b.size.z),r==0?"TilePeach":r==2?"TileSage":"TileBlue");
                for(float z=-b.extents.z+.5f;z<b.extents.z;z+=.65f)art.Box("Doctors grout seam",root,new Vector3(0,.148f,z),new Vector3(b.size.x,.004f,.01f),"Ivory");
            }
            Wall("Doctors west external wall",-12.28f,-10.30f,-12.28f,9.1f,1.95f);
            Wall("Doctors north external wall",-12.36f,9.05f,10.35f,9.05f,2.05f);
            Opening("Doctors toilet access",-10.30f,9.1f,10.28f,-7.05f,1.30f,true,.65f);
            Opening("Doctors entrance",-12.36f,10.36f,-10.30f,0,2.20f,false,.65f,true);
            // Separately framed entrances keep both desk aisles accessible without walking through the counter.
            Wall("Reception back partition",-12.28f,-3.15f,-1.50f,-3.15f,.82f);
            VerticalTwoDoors("Reception corridor wall",-1.50f,-7.85f,-3.15f,-6.95f,-3.90f);
            Wall("First aid rear partition",-12.28f,2.95f,-1.5f,2.95f,.82f);
            Wall("First aid front partition",-12.28f,-1.75f,-1.5f,-1.75f,.82f);
            Opening("First aid corridor doorway",-1.75f,2.95f,-1.50f,-.70f,1.65f,true,.82f);
            Opening("Waiting corridor doorway",-7.85f,-1.30f,1.50f,-7.0f,1.65f,true,.82f);
            Wall("Waiting front wall",1.50f,-7.85f,10.28f,-7.85f,.62f);
            Opening("Waiting refreshment doorway",1.50f,10.28f,-1.30f,5.85f,1.65f,false,.82f);
            Opening("Pharmacy corridor doorway",-.10f,2.95f,1.50f,.85f,1.65f,true,.82f);
            Wall("Pharmacy front wall",1.5f,-.10f,10.28f,-.10f,.62f);Wall("Pharmacy rear wall",1.5f,2.95f,10.28f,2.95f,1.30f);
            for(int i=0;i<4;i++)
            {
                float left=-12.2f+i*5.6f,right=left+5.6f,cx=(left+right)*.5f;
                if(i>0)Wall("Private consultation partition "+i,left,4.6f,left,9.05f,1.48f);
                Opening("Consultation "+(i+1)+" doorway",left,right,4.6f,cx,1.70f,false,.85f);
                Notice("Consultation "+(i+1)+" board",new Vector3(left+.75f,1.30f,8.92f));
            }
            Notice("Clinic welcome and appointments",new Vector3(-9f,1.3f,-3.23f));Notice("Waiting community notice board",new Vector3(7f,1.4f,-1.41f));
        }
        private void Furniture()
        {
            for(int i=0;i<4;i++)
            {
                float x=-10.72f+i*2.60f;
                workstations[0,i]=art.Model("ReceptionDesk",parent,new Vector3(x,.14f,-5.45f));sockets(workstations[0,i].transform,"reception.desk."+i+".",Vector3.forward,Vector3.back);
                var cp=world.GetAnchorPoint("reception.desk."+i+".cash");
                for(int n=0;n<6;n++)cash[i,n]=art.Box("Desk "+i+" collectable cash",parent,cp+new Vector3((n%2)*.13f-.06f,(n/2)*.055f,0),new Vector3(.20f,.043f,.16f),"Gold");
                workstations[1,i]=art.Model("TreatmentBay",parent,new Vector3(x,.14f,.86f));sockets(workstations[1,i].transform,"firstaid.station."+i+".",Vector3.back,Vector3.left);
                if(i<3){Screen("Reception desk divider "+i,new Vector3(x+1.28f,.14f,-5.45f),1.55f,.85f);Screen("Nursing privacy partition "+i,new Vector3(x+1.3f,.14f,1.10f),2.10f,1.38f);}
                var consult=art.Group("Consultation workstation "+i,parent,new Vector3(-9.4f+i*5.6f,.14f,7.05f));workstations[2,i]=consult.gameObject;
                ClinicFurnishings.Cabinet(art,consult,new Vector3(.40f,0,0),new Vector3(1.45f,.8f,.76f),"Sage");
                art.Box("Consultation desk top",consult,new Vector3(.40f,.84f,0),new Vector3(1.64f,.08f,.89f),"Wood");
                art.Box("Doctor monitor",consult,new Vector3(.75f,1.09f,0),new Vector3(.43f,.36f,.08f),"Ink");
                art.Box("Monitor display",consult,new Vector3(.75f,1.10f,-.048f),new Vector3(.36f,.27f,.012f),"Blue");
                art.Model("Seat",consult,new Vector3(-.12f,0,-1.0f));
                anchor("consultation.station."+i+".patient",consult.position+new Vector3(-.12f,0,-1.0f),Vector3.forward);
                anchor("consultation.station."+i+".staff",consult.position+new Vector3(.28f,0,.95f),Vector3.back);
                art.Box("Consultation examination couch",consult,new Vector3(-1.72f,.55f,.4f),new Vector3(.90f,.12f,1.85f),"Linen");
                for(int end=-1;end<=1;end+=2)art.Box("Examination couch feet",consult,new Vector3(-1.72f,.26f,.4f+end*.72f),new Vector3(.74f,.50f,.08f),"SageDark");
                art.Box("Examination pillow",consult,new Vector3(-1.72f,.67f,1.05f),new Vector3(.74f,.14f,.48f),"TileBlue");
                CabinetSupplies(new Vector3(-11.5f+i*5.6f,.14f,8.58f),.75f);
            }
            for(int i=0;i<2;i++)
            {
                var desk=art.Model("ReceptionDesk",parent,new Vector3(4.05f+i*3.50f,.14f,1.20f));workstations[3,i]=desk;
                sockets(desk.transform,"pharmacy.station."+i+".",Vector3.forward,Vector3.back);
                var bag=art.Group("Dispensing paper bag",desk.transform,new Vector3(-.5f,1.12f,0));art.Box("Prescription bag",bag,new Vector3(0,.18f,0),new Vector3(.27f,.36f,.17f),"Linen");
                art.Box("Pharmacy bag cross",bag,new Vector3(0,.2f,-.09f),new Vector3(.16f,.045f,.014f),"Sage");art.Box("Pharmacy bag cross",bag,new Vector3(0,.2f,-.10f),new Vector3(.045f,.16f,.014f),"Sage");
            }
            for(int shelf=0;shelf<5;shelf++)
            {float x=2.25f+shelf*1.55f;CabinetSupplies(new Vector3(x,.14f,2.68f),1.1f);for(int row=0;row<2;row++)for(int n=0;n<4;n++)art.Box("Pharmacy labelled medicine",parent,new Vector3(x-.35f+n*.23f,1.25f+row*.28f,2.71f),new Vector3(.16f,.21f,.13f),(row+n)%2==0?"Linen":"Apricot");}
            for(int i=0;i<30;i++)
            {
                int col=i%6,row=i/6;float x=2.2f+(col/2)*2.8f+(col%2)*1.80f,z=-6.0f+row*1.0f;bool east=col%2==0;
                seats[i]=art.Model("Seat",parent,new Vector3(x,.14f,z),Quaternion.Euler(0,east?-90:90,0));
                // Authored seat sockets remain the source of seated root positions.
                foreach(var node in seats[i].GetComponentsInChildren<Transform>())if(node.name.Contains("__patient")){anchor("waiting.seat."+i,node.position,east?Vector3.right:Vector3.left);break;}
            }
            for(int i=0;i<6;i++)
            {art.Model("Plant",parent,new Vector3(-11.5f+i*4.1f,.14f,i%2==0?-2.5f:3.70f));}

        }
        private void StagedWorkstations()
        {
            for(int role=0;role<4;role++)for(int station=0;station<(role==3?2:4);station++)
            {
                var p=workstations[role,station].transform.position;var root=art.Group(((ClinicStaffRole)role)+" future station "+station,parent,p);futureStations[role,station]=root.gameObject;
                art.Box("Reserved workstation inset",root,new Vector3(0,.01f,0),new Vector3(1.70f,.015f,1.50f),"TileSage");
                art.Box("Equipment delivery pallet",root,new Vector3(0,.09f,0),new Vector3(1.12f,.15f,.82f),"Wood");
                art.Box("Covered equipment delivery",root,new Vector3(0,.43f,0),new Vector3(1.02f,.53f,.72f),"TileBlue");
                for(int side=-1;side<=1;side+=2)art.Box("Delivery securing strap",root,new Vector3(side*.31f,.43f,0),new Vector3(.065f,.57f,.75f),"Gold");
                art.Box("Fitout care mark",root,new Vector3(0,.45f,-.375f),new Vector3(.29f,.075f,.025f),"Linen");art.Box("Fitout care mark",root,new Vector3(0,.45f,-.389f),new Vector3(.075f,.29f,.025f),"Linen");
            }
        }
        private void Anchors()
        {
            anchor("entrance",new Vector3(.38f,.14f,-10.90f),Vector3.forward);anchor("exit",new Vector3(-.38f,.14f,-10.90f),Vector3.back);
            for(int i=0;i<23;i++)
            {string name="reception.queue."+i;var point=ClinicDoctorsNavigation.Anchor(name);anchor(name,new Vector3(point.x,.14f,point.z),Vector3.forward);}
            for(int i=0;i<4;i++)anchor("firstaid.standing."+i,new Vector3(-2.20f,.14f,-1.05f+i*.72f),Vector3.left);
            for(int i=0;i<12;i++)anchor("parking.bay."+i+".patient",DoctorsParkingLayout.BayDoor(i),Vector3.right);
            for(int i=0;i<2;i++){anchor("waiting.toilet."+i+".patient",new Vector3(11.35f+i*1.75f,.14f,-3.70f),Vector3.back);anchor("taxi.dock."+i+".patient",DoctorsParkingLayout.TaxiDoor(i),Vector3.forward);}
            for(int i=0;i<ClinicRules.TaxiWaitingCapacity;i++)
            {string name=ClinicRules.TaxiWaitingAnchor(i);var point=ClinicDoctorsNavigation.Anchor(name);anchor(name,new Vector3(point.x,.14f,point.z),Vector3.back);}
            anchor("waiting.vending.patient",new Vector3(9.05f,.14f,-1.82f),Vector3.forward);anchor("waiting.vending.cash",VendingCashPoint,Vector3.forward);
            foreach(ClinicRoom room in Enum.GetValues(typeof(ClinicRoom)))anchor(room==ClinicRoom.FirstAid?"firstaid.progress":room.ToString().ToLowerInvariant()+".progress",RoomPoint(room),Vector3.forward);
        }
        private GameObject Toilet(int index)
        {
            float x=11.35f+index*1.75f;var root=art.Group("Doctors toilet cubicle "+index,parent);
            art.Box("Toilet annex foundation",root,new Vector3(x,.05f,-5.45f),new Vector3(1.75f,.2f,4.7f),"Clay");art.Box("Toilet washable floor",root,new Vector3(x,.15f,-5.45f),new Vector3(1.72f,.03f,4.7f),"TileBlue");
            Wall("Toilet cubicle north "+index,x-.875f,-3.1f,x+.875f,-3.1f,1.9f);
            Wall("Toilet cubicle divider "+index,x+.875f,-5.2f,x+.875f,-3.1f,1.5f);
            // Both cubicles have independent approach paths from the waiting room's service aisle.
            Opening("Toilet "+index+" privacy doorway",x-.875f,x+.875f,-5.2f,x,1.25f,false,1.6f);
            art.Orb("Toilet bowl",root,new Vector3(x,.56f,-3.70f),new Vector3(.64f,.24f,.75f),"Linen");art.Cylinder("Toilet pedestal",root,new Vector3(x,.33f,-3.67f),new Vector3(.4f,.40f,.5f),"Linen");art.Box("Toilet cistern",root,new Vector3(x,.82f,-3.25f),new Vector3(.62f,.70f,.22f),"Linen");
            art.Orb("Toilet seat",root,new Vector3(x,.685f,-3.75f),new Vector3(.42f,.015f,.48f),"Blue");
            ClinicFurnishings.Cabinet(art,root,new Vector3(x,.14f,-7.42f),new Vector3(1.2f,.67f,.50f),"Sage");art.Box("Toilet wash basin",root,new Vector3(x,.88f,-7.40f),new Vector3(1.2f,.12f,.50f),"Linen");
            for(int level=1;level<=6;level++)
            {var detail=art.Group("Toilet tier "+level,root);art.Box(level%2==0?"Toilet folded towel":"Toilet care dispenser",detail,new Vector3(x-.49f+(level-1)*.19f,1.02f,-7.40f),new Vector3(.13f,.15f,.16f),level%2==0?"Linen":"Apricot");details.Add(Detail.Amenity(detail.gameObject,ClinicAmenity.Toilet,level));}
            return root.gameObject;
        }
        private void ProgressionDetails()
        {
            for(int room=0;room<5;room++)
            {
                var bounds=RoomBounds[room];
                for(int tier=2;tier<=6;tier++)
                {
                    var d=art.Group(((ClinicRoom)room)+" tier "+tier+" installed fittings",parent);float x=bounds.min.x+.60f+(tier-2)*Mathf.Min(1.35f,(bounds.size.x-1.2f)/5);
                    art.Box("Tier oak valance",d,new Vector3(x,1.88f,bounds.max.z-.16f),new Vector3(1.05f,.12f,.13f),"Wood");
                    art.Orb("Tier warm wall lamp",d,new Vector3(x,1.65f,bounds.max.z-.23f),new Vector3(.22f,.25f,.15f),"Linen");details.Add(Detail.RoomTier(d.gameObject,(ClinicRoom)room,tier));
                }
                for(int track=0;track<3;track++)for(int level=2;level<=12;level++)
                {
                    var d=art.Group(((ClinicRoom)room)+" "+((UpgradeTrack)track)+" level "+level,parent);
                    ComponentFitting(d,(ClinicRoom)room,(UpgradeTrack)track,level);
                    details.Add(Detail.Component(d.gameObject,(ClinicRoom)room,(UpgradeTrack)track,level));
                }
            }
            for(int role=0;role<4;role++)for(int station=0;station<(role==3?2:4);station++)for(int level=2;level<=12;level++)
            {
                var d=art.Group(((ClinicStaffRole)role)+" station "+station+" equipment "+level,workstations[role,station].transform);
                int n=level-2;float y=role==1?1.17f:.92f;
                art.Box("Individual upgraded instrument",d,new Vector3(-.67f+(n%6)*.12f,y+(n/6)*.12f,.10f),new Vector3(.075f,.105f,.11f),level%2==0?"Blue":"Gold");
                details.Add(Detail.Station(d.gameObject,(ClinicStaffRole)role,station,level));
            }
        }
        private void ComponentFitting(Transform root,ClinicRoom room,UpgradeTrack track,int level)
        {
            // Four furnishing clusters keep progression readable. Every new level improves
            // a recognisable object; waiting-room facilities also add the actual seat pairs.
            var b=RoomBounds[(int)room];int n=level-2,cluster=n/3,step=n%3;
            float x=b.min.x+.85f+cluster*(b.size.x-1.70f)/3,z=b.max.z-.22f;
            bool lounge=room==ClinicRoom.Waiting;bool pharmacy=room==ClinicRoom.Pharmacy;
            if(track==UpgradeTrack.Equipment)
            {
                if(lounge)
                {
                    if(step==0){for(int side=-1;side<=1;side+=2)art.Box("Reading shelf support",root,new Vector3(x+side*.27f,.55f,z),new Vector3(.035f,.82f,.035f),"Gold");art.Box("Lounge reading shelf",root,new Vector3(x,.95f,z),new Vector3(.62f,.07f,.24f),"Wood");for(int book=0;book<3;book++)art.Box("Lounge lending book",root,new Vector3(x-.18f+book*.16f,1.07f,z),new Vector3(.10f,.19f,.15f),book%2==0?"Apricot":"Blue");}
                    else if(step==1){art.Box("Patient call display support",root,new Vector3(x,1.30f,z+.04f),new Vector3(.035f,.66f,.035f),"Gold");art.Box("Patient call display",root,new Vector3(x,1.52f,z),new Vector3(.49f,.25f,.045f),"SageDark");for(int dot=0;dot<3;dot++)art.Orb("Call display light",root,new Vector3(x-.12f+dot*.12f,1.53f,z-.03f),new Vector3(.045f,.055f,.012f),"Gold");}
                    else {art.Box("Reading light arm",root,new Vector3(x+.27f,1.18f,z),new Vector3(.035f,.35f,.035f),"Gold");art.Orb("Reading lamp shade",root,new Vector3(x+.27f,1.35f,z-.08f),new Vector3(.22f,.16f,.19f),"Linen");}
                }
                else
                {
                    float y=pharmacy?1.73f:1.06f;
                    if(step==0){art.Box("Clinical display stand",root,new Vector3(x,(y+.14f)*.5f,z+.06f),new Vector3(.055f,y-.14f,.055f),"Gold");art.Box("Instrument shelf",root,new Vector3(x,y,z),new Vector3(1.0f,.07f,.25f),"Wood");art.Box("Clinical monitor frame",root,new Vector3(x,y+.21f,z),new Vector3(.46f,.32f,.06f),"Ink");art.Box("Clinical monitor display",root,new Vector3(x,y+.21f,z-.04f),new Vector3(.37f,.23f,.016f),"Blue");}
                    else if(step==1){for(int bottle=0;bottle<2;bottle++)art.Cylinder(pharmacy?"Prescription stock bottle":"Care instrument",root,new Vector3(x+.31f+bottle*.14f,y+.13f,z-.02f),new Vector3(.10f,.18f,.10f),bottle==0?"Apricot":"Linen");}
                    else {art.Box("Appointment record tray",root,new Vector3(x-.38f,y+.065f,z-.02f),new Vector3(.22f,.035f,.22f),"Gold");art.Box("Clean record cards",root,new Vector3(x-.38f,y+.093f,z-.02f),new Vector3(.17f,.025f,.17f),"Linen");}
                }
            }
            else if(track==UpgradeTrack.Facilities)
            {
                if(lounge)
                {
                    if(step==0){for(int side=-1;side<=1;side+=2)art.Box("Magazine ledge support",root,new Vector3(x+side*.24f,.30f,z),new Vector3(.035f,.32f,.035f),"Gold");art.Box("Lounge oak magazine ledge",root,new Vector3(x,.45f,z),new Vector3(.58f,.09f,.27f),"Wood");art.Box("Lounge magazine",root,new Vector3(x,.51f,z-.04f),new Vector3(.36f,.035f,.21f),"Apricot");}
                    else if(step==1){art.Box("Lounge water cup tray",root,new Vector3(x+.18f,.57f,z),new Vector3(.21f,.035f,.18f),"Gold");art.Cylinder("Fresh water cup",root,new Vector3(x+.18f,.66f,z),new Vector3(.10f,.14f,.10f),"Linen");}
                    else art.Box("Lounge comfort cushion",root,new Vector3(x-.13f,.61f,z-.02f),new Vector3(.27f,.12f,.21f),"TileBlue");
                }
                else if(pharmacy)
                {
                    if(step==0)art.Box("Pharmacy labelled storage drawer",root,new Vector3(x,.45f,z-.25f),new Vector3(.42f,.19f,.025f),"TileSage");
                    else if(step==1)art.Box("Prescription drawer label",root,new Vector3(x,.46f,z-.273f),new Vector3(.19f,.07f,.012f),"Linen");
                    else art.Box("Medicine drawer handle",root,new Vector3(x,.38f,z-.29f),new Vector3(.16f,.026f,.020f),"Gold");
                }
                else
                {
                    if(step==0)ClinicFurnishings.Cabinet(art,root,new Vector3(x,.14f,z),new Vector3(.75f,.66f,.34f),"Sage");
                    else if(step==1)art.Box("Folded linen supplies",root,new Vector3(x,.85f,z-.01f),new Vector3(.56f,.09f,.25f),"Linen");
                    else {art.Box("Linen supply basket",root,new Vector3(x+.27f,.92f,z),new Vector3(.18f,.11f,.21f),"Wood");art.Box("Fresh linen roll",root,new Vector3(x+.27f,1.00f,z),new Vector3(.13f,.06f,.15f),"Linen");}
                }
            }
            else
            {
                if(n==0||n==4||n==8)
                {art.Box("Botanical oak frame",root,new Vector3(x-.32f,1.63f,z),new Vector3(.37f,.46f,.035f),"Wood");art.Box("Botanical artwork",root,new Vector3(x-.32f,1.63f,z-.025f),new Vector3(.31f,.40f,.012f),"Linen");art.Orb("Botanical leaf print",root,new Vector3(x-.32f,1.66f,z-.036f),new Vector3(.15f,.26f,.015f),"Sage");}
                else if(n==2||n==6||n==10)
                {art.Box("Greenery stand stem",root,new Vector3(x+.38f,.53f,z),new Vector3(.035f,.78f,.035f),"Gold");art.Box("Greenery stand top",root,new Vector3(x+.38f,.94f,z),new Vector3(.22f,.045f,.22f),"Wood");art.Cylinder("Ceramic greenery pot",root,new Vector3(x+.38f,1.04f,z),new Vector3(.18f,.21f,.18f),"Clay");art.Orb("Indoor greenery",root,new Vector3(x+.38f,1.25f,z),new Vector3(.31f,.35f,.25f),"Leaf");}
                else if(n==5)
                {art.Orb("Wall clock frame",root,new Vector3(x,1.84f,z),new Vector3(.36f,.36f,.035f),"Wood");art.Orb("Wall clock face",root,new Vector3(x,1.84f,z-.027f),new Vector3(.30f,.30f,.012f),"Linen");art.Box("Clock hands",root,new Vector3(x+.035f,1.87f,z-.04f),new Vector3(.12f,.025f,.012f),"Ink");}
                else if(n==9){art.Box("Community message ribbon",root,new Vector3(x,1.40f,z),new Vector3(.38f,.12f,.020f),"Apricot");}
                else {art.Box("Flower stand stem",root,new Vector3(x-.18f,.53f,z),new Vector3(.035f,.78f,.035f),"Gold");art.Box("Flower stand top",root,new Vector3(x-.18f,.94f,z),new Vector3(.20f,.045f,.20f),"Wood");art.Cylinder("Small flower vase",root,new Vector3(x-.18f,1.04f,z),new Vector3(.12f,.20f,.12f),"Blue");art.Orb("Fresh flower",root,new Vector3(x-.18f,1.21f,z),new Vector3(.18f,.15f,.17f),"Apricot");}
            }
        }
        private void Neighbourhood()
        {
            art.Box("Doctors neighbourhood road",parent,new Vector3(0,-.17f,-14.0f),new Vector3(100,.10f,3.3f),"Asphalt");
            for(int i=-14;i<15;i++)art.Box("Street centre marking",parent,new Vector3(i*3.2f,-.11f,-14.0f),new Vector3(1.8f,.015f,.09f),"Linen");
            art.Box("Clinic front promenade west",parent,new Vector3(-27.25f,.06f,-10.675f),new Vector3(10.5f,.12f,3.35f),"TilePeach");
            art.Box("Clinic entrance promenade",parent,new Vector3(.775f,.06f,-10.675f),new Vector3(30.45f,.12f,3.35f),"TilePeach");
            art.Box("Clinic front promenade east",parent,new Vector3(28.05f,.06f,-10.675f),new Vector3(8.9f,.12f,3.35f),"TilePeach");
            art.Box("Taxi sheltered passenger pavement",parent,new Vector3(19.8f,.06f,-9.45f),new Vector3(7.6f,.12f,.90f),"TilePeach");
            art.Box("Taxi waiting promenade",parent,new Vector3(20.45f,.06f,-7.475f),new Vector3(12.1f,.12f,3.05f),"TilePeach");
            for(int i=0;i<ClinicRules.TaxiWaitingCapacity;i++)
            {
                var point=ClinicDoctorsNavigation.Anchor(ClinicRules.TaxiWaitingAnchor(i));
                for(int side=-1;side<=1;side+=2)art.Orb("Taxi waiting footprint",parent,new Vector3(point.x+side*.115f,.13f,point.z),new Vector3(.105f,.016f,.22f),"TileSage");
            }
            art.Box("Taxi layby passenger kerb",parent,new Vector3(19.8f,.03f,-9.94f),new Vector3(7.6f,.14f,.08f),"Clay");
            art.Box("Far pavement",parent,new Vector3(0,.04f,-16.45f),new Vector3(90,.17f,1.55f),"Linen");
            for(int stripe=0;stripe<8;stripe++)art.Box("Doctors road crossing",parent,new Vector3(1.4f,-.105f,-12.65f-stripe*.39f),new Vector3(1.3f,.018f,.19f),"Linen");
            for(int i=0;i<11;i++)
            {
                float x=-29+i*5.4f;var building=art.Group("Neighbourhood premises "+i,parent,new Vector3(x,0,-20.0f-(i%2)*1.2f));
                art.Box("Terracotta building",building,new Vector3(0,1.55f,0),new Vector3(3.6f,3.1f,3.4f),i%3==0?"Clay":i%3==1?"TileSage":"TilePeach");
                art.Box("Neighbourhood roof",building,new Vector3(0,3.18f,0),new Vector3(3.9f,.18f,3.7f),"SageDark");
                art.Box("Shop door",building,new Vector3(0,.87f,1.72f),new Vector3(.75f,1.7f,.06f),"Wood");
                for(int side=-1;side<=1;side+=2)art.Box("Shop window",building,new Vector3(side*1.08f,1.42f,1.73f),new Vector3(.95f,1.15f,.045f),"Blue");
                var canopy=art.Group("Shop striped canopy",building,new Vector3(0,2.45f,1.98f));for(int n=0;n<8;n++)art.Box("Canopy stripe",canopy,new Vector3(-1.58f+n*.45f,0,0),new Vector3(.45f,.10f,.8f),n%2==0?"Sage":"Linen");
            }
            for(int i=0;i<15;i++)
            {
                float x=-30+i*4.2f,z=i%2==0?12.0f:15.5f;var tree=art.Group("Doctors neighbourhood tree "+i,parent,new Vector3(x,.1f,z));
                art.Cylinder("Tree trunk",tree,new Vector3(0,.82f,0),new Vector3(.23f,1.64f,.23f),"Wood");
                art.Orb("Tree crown",tree,new Vector3(0,2.15f,0),new Vector3(1.60f,2.25f,1.55f),i%2==0?"Leaf":"Sage");
                if(i%3==0){art.Box("Garden planter",parent,new Vector3(x,.23f,10.3f),new Vector3(2.7f,.40f,.58f),"Clay");for(int n=0;n<5;n++)art.Orb("Garden flowering shrub",parent,new Vector3(x-1+n*.5f,.61f,10.3f),new Vector3(.6f,.5f,.62f),n%2==0?"Leaf":"Apricot");}
            }
            for(int i=0;i<4;i++)
            {
                float x=16+i*4.0f;art.Box("Garden bench seat",parent,new Vector3(x,.57f,-5.5f),new Vector3(2.2f,.12f,.58f),"Wood");art.Box("Garden bench back",parent,new Vector3(x,.94f,-5.22f),new Vector3(2.2f,.62f,.10f),"Wood");
                for(int side=-1;side<=1;side+=2)art.Box("Bench foot",parent,new Vector3(x+side*.83f,.28f,-5.5f),new Vector3(.09f,.55f,.48f),"SageDark");
                art.Model("Plant",parent,new Vector3(x,.14f,i<3?-4.7f:-6.65f));
            }
        }
        private void CabinetSupplies(Vector3 position,float width)
        {
            ClinicFurnishings.Cabinet(art,parent,position,new Vector3(width,.78f,.50f),"Sage");
            art.Box("Clean linen stack",parent,position+new Vector3(0,.84f,0),new Vector3(width*.72f,.08f,.33f),"Linen");
        }
        private void Notice(string name,Vector3 position)=>ClinicFurnishings.NoticeBoard(art,parent,name,position,0,.8f);
        private void Screen(string name,Vector3 p,float depth,float height)
        {var r=art.Group(name,parent,p);art.Box("Privacy partition",r,new Vector3(0,height*.5f,0),new Vector3(.06f,height,depth),"Linen");for(int side=-1;side<=1;side+=2)art.Box("Privacy upright",r,new Vector3(0,height*.5f,side*depth*.5f),new Vector3(.065f,height+.04f,.045f),"Gold");}
        private void Wall(string name,float x1,float z1,float x2,float z2,float height)
        {
            var mid=new Vector3((x1+x2)*.5f,.14f+height*.5f,(z1+z2)*.5f);var size=new Vector3(Mathf.Max(.14f,Mathf.Abs(x2-x1)),height,Mathf.Max(.14f,Mathf.Abs(z2-z1)));
            art.Box(name+" wall",parent,mid,size,"Ivory");size.y=.055f;art.Box(name+" cap",parent,new Vector3(mid.x,.14f+height,mid.z),size,"Sage");size.y=.10f;art.Box(name+" skirting",parent,new Vector3(mid.x,.19f,mid.z),size,"SageDark");
        }
        private void Opening(string name,float from,float to,float line,float center,float width,bool vertical,float height,bool entrance=false)
        {
            float edge=width*.5f+.08f;
            if(vertical){Wall(name+" south",line,from,line,center-edge,height);Wall(name+" north",line,center+edge,line,to,height);}
            else {Wall(name+" west",from,line,center-edge,line,height);Wall(name+" east",center+edge,line,to,line,height);}
            doors.Add(new ClinicDoor(art,parent,entrance,new Vector3(vertical?line:center,.14f,vertical?center:line),vertical?90:0,width,name));
        }
        private void VerticalTwoDoors(string name,float x,float min,float max,float a,float b)
        {
            const float width=1.65f,edge=width*.5f+.08f,staffWidth=1.30f,staffEdge=staffWidth*.5f+.08f;
            Wall(name+" south",x,min,x,a-edge,.82f);Wall(name+" middle",x,a+edge,x,b-staffEdge,.82f);if(b+staffEdge<max)Wall(name+" north",x,b+staffEdge,x,max,.82f);
            doors.Add(new ClinicDoor(art,parent,position:new Vector3(x,.14f,a),yaw:90,openingWidth:width,name:"Reception public doorway"));doors.Add(new ClinicDoor(art,parent,position:new Vector3(x,.14f,b),yaw:90,openingWidth:staffWidth,name:"Reception staff doorway"));
        }
        internal ClinicHit Pick(Vector2 uv)
        {
            var ray=world.SceneCamera.ViewportPointToRay(uv);
            for(int i=0;i<4;i++)if(tills[i]>0&&new Bounds(world.GetCashPoint(i),new Vector3(.7f,.7f,.6f)).IntersectRay(ray))return new ClinicHit(ClinicHitKind.Cash,i);
            if(vendingTill>0&&new Bounds(VendingCashPoint,new Vector3(.5f,.5f,.5f)).IntersectRay(ray))return new ClinicHit(ClinicHitKind.VendingCash);
            for(int role=0;role<4;role++)for(int i=0;i<(role==3?2:4);i++)if(workstations[role,i].activeSelf&&new Bounds(WorkstationPoint((ClinicStaffRole)role,i),new Vector3(.8f,.7f,.8f)).IntersectRay(ray))return new ClinicHit(role==0?ClinicHitKind.Desk:role==1?ClinicHitKind.Station:role==2?ClinicHitKind.DoctorStation:ClinicHitKind.PharmacyStation,i);
            for(int kind=0;kind<4;kind++)if(new Bounds(AmenityPoint((ClinicAmenity)kind),new Vector3(1.2f,1.4f,1.2f)).IntersectRay(ray))return new ClinicHit(kind==0?ClinicHitKind.Parking:kind==1?ClinicHitKind.Toilet:kind==2?ClinicHitKind.Vending:ClinicHitKind.Taxi,kind);
            if(!world.TryViewportToGround(uv,out var p))return default;
            if(p.x<-14.3f&&p.x>-23&&p.z>-9.5f&&p.z<10)return new ClinicHit(ClinicHitKind.Parking);
            if(p.x>10.25f&&p.x<14.0f&&p.z>-7.9f&&p.z<-3.1f)return new ClinicHit(ClinicHitKind.Toilet);
            if(p.x>16.0f&&p.x<26.5f&&p.z>-11.8f&&p.z<-5.95f)return new ClinicHit(ClinicHitKind.Taxi);
            for(int i=0;i<5;i++){var b=RoomBounds[i];if(p.x>=b.min.x&&p.x<=b.max.x&&p.z>=b.min.z&&p.z<=b.max.z)return new ClinicHit(i==0?ClinicHitKind.Reception:i==1?ClinicHitKind.Treatment:i==2?(waitingBuilt?ClinicHitKind.Waiting:ClinicHitKind.Expansion):i==3?ClinicHitKind.Consultation:ClinicHitKind.Pharmacy,i);}
            return default;
        }
        internal void Select(ClinicHit hit)
        {
            bool active=hit.Kind!=ClinicHitKind.None&&hit.Kind!=ClinicHitKind.Cash&&hit.Kind!=ClinicHitKind.VendingCash;selection.gameObject.SetActive(active);if(!active)return;
            if(hit.Kind==ClinicHitKind.Desk||hit.Kind==ClinicHitKind.Station||hit.Kind==ClinicHitKind.DoctorStation||hit.Kind==ClinicHitKind.PharmacyStation)
            {int role=hit.Kind==ClinicHitKind.Desk?0:hit.Kind==ClinicHitKind.Station?1:hit.Kind==ClinicHitKind.DoctorStation?2:3;selection.position=workstations[role,Mathf.Clamp(hit.Id,0,role==3?1:3)].transform.position;selection.localScale=new Vector3(1.9f,1,1.9f);}
            else if(hit.Kind==ClinicHitKind.Parking||hit.Kind==ClinicHitKind.Toilet||hit.Kind==ClinicHitKind.Vending||hit.Kind==ClinicHitKind.Taxi)
            {var kind=hit.Kind==ClinicHitKind.Parking?ClinicAmenity.Parking:hit.Kind==ClinicHitKind.Toilet?ClinicAmenity.Toilet:hit.Kind==ClinicHitKind.Taxi?ClinicAmenity.Taxi:ClinicAmenity.Vending;var p=AmenityPoint(kind);selection.position=new Vector3(p.x,0,p.z);selection.localScale=kind==ClinicAmenity.Parking?new Vector3(8.4f,1,18.9f):kind==ClinicAmenity.Toilet?new Vector3(3.5f,1,4.7f):kind==ClinicAmenity.Taxi?new Vector3(7,1,2):new Vector3(1.1f,1,1.0f);}
            else {var b=RoomBounds[Mathf.Clamp(hit.Id,0,4)];selection.position=new Vector3(b.center.x,0,b.center.z);selection.localScale=new Vector3(b.size.x,1,b.size.z);}
        }
        private static int StationLevel(ClinicState s,ClinicStaffRole role,int id)
        {
            if(role==ClinicStaffRole.Receptionist){for(int i=0;i<s.ReceptionDesks.Count;i++)if(s.ReceptionDesks[i].Id==id)return s.ReceptionDesks[i].EquipmentLevel;return 0;}
            var list=role==ClinicStaffRole.Nurse?s.TreatmentStations:role==ClinicStaffRole.Doctor?s.ConsultationStations:s.PharmacyStations;
            for(int i=0;i<list.Count;i++)if(list[i].Id==id)return list[i].EquipmentLevel;return 0;
        }
        private sealed class Detail
        {
            private GameObject root;private ClinicRoom room;private UpgradeTrack track;private ClinicStaffRole role;private ClinicAmenity amenity;private int level,index,kind;
            internal static Detail Component(GameObject r,ClinicRoom room,UpgradeTrack t,int l)=>new Detail{root=r,room=room,track=t,level=l,kind=0};
            internal static Detail RoomTier(GameObject r,ClinicRoom room,int l)=>new Detail{root=r,room=room,level=l,kind=1};
            internal static Detail Station(GameObject r,ClinicStaffRole role,int i,int l)=>new Detail{root=r,role=role,index=i,level=l,kind=2};
            internal static Detail Amenity(GameObject r,ClinicAmenity a,int l)=>new Detail{root=r,amenity=a,level=l,kind=3};
            internal void Render(ClinicState s)
            {
                bool active=false;
                if(kind==2)active=StationLevel(s,role,index)>=level;
                else if(kind==3){for(int i=0;i<s.Amenities.Count;i++)if(s.Amenities[i].Kind==amenity){active=s.Amenities[i].Level>=level;break;}}
                else {for(int i=0;i<s.Rooms.Count;i++)if(s.Rooms[i].Kind==room){var data=s.Rooms[i];active=data.Built&&(kind==0?data.Level(track):data.Tier)>=level;break;}}
                if(root.activeSelf!=active)root.SetActive(active);
            }
        }
    }
}
