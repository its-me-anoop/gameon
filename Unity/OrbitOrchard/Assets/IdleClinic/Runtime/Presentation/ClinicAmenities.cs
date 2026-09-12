using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Amenity tiers and occupancy are projections of simulation state, never an income source.</summary>
    internal sealed class ClinicAmenities
    {
        internal static readonly Vector3 ParkingPoint=ClinicParkingLayout.SignPoint;
        internal static readonly Vector3 ToiletPoint=new Vector3(4.65f,1.3f,5.75f);
        internal static readonly Vector3 VendingPoint=new Vector3(5.17f,1.15f,-2.55f);
        internal static readonly Vector3 VendingCashPoint=new Vector3(5.56f,1.38f,-2.65f);
        private readonly ClinicParkingPresentation parkingWorld;
        private readonly GameObject[,] tierDetails=new GameObject[3,3];
        private readonly GameObject toilet,vending,toiletPlot,vendingPlot,toiletClosure,cash,tipCup;
        private readonly Transform toiletDoor,vendingButton,vendingTip;
        internal long VendingTill { get; private set; }
        internal static Vector3 BayPatient(int bay)=>ClinicParkingLayout.BayDoor(bay);
        internal ClinicAmenities(ClinicArt art,Transform parent)
        {
            var root=art.Group("Clinic amenities",parent);
            parkingWorld=new ClinicParkingPresentation(art,root);
            var annex=art.Group("Waiting toilet annex",root);
            art.Box("Toilet annex foundation",annex,new Vector3(4.2f,.05f,6.05f),new Vector3(3.15f,.2f,2.3f),"Clay");
            art.Box("Toilet tile floor",annex,new Vector3(4.2f,.15f,6.05f),new Vector3(3.1f,.03f,2.25f),"TileBlue");
            toiletPlot=art.Group("Toilet build marker",annex,ToiletPoint).gameObject;
            art.Box("Toilet plot placard",toiletPlot.transform,Vector3.zero,new Vector3(.7f,.55f,.07f),"Sage");
            art.Orb("Toilet pictogram head",toiletPlot.transform,new Vector3(0,.12f,-.055f),new Vector3(.11f,.11f,.035f),"Linen");
            art.Box("Toilet pictogram body",toiletPlot.transform,new Vector3(0,-.04f,-.055f),new Vector3(.16f,.18f,.035f),"Linen");
            toiletClosure=art.Box("Future toilet doorway closure",annex,new Vector3(3.4375f,.94f,4.97f),new Vector3(.90f,1.60f,.08f),"Wood");
            art.Box("Toilet construction crossbar",toiletClosure.transform,new Vector3(0,.10f,-.56f),new Vector3(.85f,.09f,.10f),"Apricot");
            toilet=art.Group("Patient toilet",annex).gameObject;
            art.Box("Toilet privacy back",toilet.transform,new Vector3(4.2f,1.15f,7.20f),new Vector3(3.15f,2.0f,.14f),"TileSage");
            art.Box("Toilet privacy east",toilet.transform,new Vector3(5.75f,.80f,6.1f),new Vector3(.14f,1.3f,2.15f),"TileSage");
            art.Box("Toilet privacy west",toilet.transform,new Vector3(2.63f,.85f,6.1f),new Vector3(.12f,1.4f,2.15f),"Sage");
            art.Box("Toilet privacy front",toilet.transform,new Vector3(4.83f,1.0f,5.03f),new Vector3(1.76f,1.7f,.11f),"TileSage");
            toiletDoor=art.Group("Toilet sliding privacy door",toilet.transform,new Vector3(3.43f,.14f,5.0f));
            art.Box("Toilet door panel",toiletDoor,new Vector3(0,.8f,0),new Vector3(.93f,1.60f,.055f),"Sage");
            art.Box("Toilet occupied indicator",toiletDoor,new Vector3(.28f,1.13f,-.035f),new Vector3(.15f,.09f,.02f),"Apricot");
            art.Box("Toilet door pull",toiletDoor,new Vector3(.30f,.78f,-.05f),new Vector3(.025f,.22f,.035f),"Gold");
            art.Cylinder("Toilet pedestal",toilet.transform,new Vector3(4.80f,.34f,6.30f),new Vector3(.40f,.40f,.53f),"Linen");
            art.Orb("Toilet bowl",toilet.transform,new Vector3(4.80f,.52f,6.25f),new Vector3(.65f,.25f,.76f),"Linen");
            art.Orb("Toilet seat inset",toilet.transform,new Vector3(4.80f,.646f,6.20f),new Vector3(.43f,.014f,.47f),"Blue");
            art.Box("Toilet cistern",toilet.transform,new Vector3(4.80f,.78f,6.73f),new Vector3(.65f,.74f,.22f),"Linen");
            art.Box("Toilet flush",toilet.transform,new Vector3(4.97f,1.16f,6.73f),new Vector3(.12f,.035f,.08f),"Gold");
            var wash=art.Group("Toilet washstand",toilet.transform,new Vector3(3.16f,.14f,6.80f));
            ClinicFurnishings.Cabinet(art,wash,Vector3.zero,new Vector3(.60f,.68f,.45f),"Sage");
            art.Box("Toilet sink",wash,new Vector3(0,.74f,0),new Vector3(.66f,.12f,.48f),"Linen");
            art.Box("Toilet sink water",wash,new Vector3(0,.81f,-.03f),new Vector3(.45f,.01f,.29f),"Blue");
            art.Cylinder("Toilet sink tap",wash,new Vector3(0,.93f,.17f),new Vector3(.04f,.22f,.04f),"Gold");
            art.Box("Toilet mirror",toilet.transform,new Vector3(3.17f,1.45f,7.09f),new Vector3(.62f,.61f,.035f),"Blue");
            for(int level=1;level<=3;level++)
            {
                var detail=art.Group("Toilet tier "+level,toilet.transform);tierDetails[1,level-1]=detail.gameObject;
                if(level==1)art.Box("Toilet towel",detail,new Vector3(3.72f,1.05f,7.09f),new Vector3(.30f,.36f,.035f),"Linen");
                if(level==2)art.Box("Toilet soap dispenser",detail,new Vector3(2.92f,1.14f,7.06f),new Vector3(.16f,.29f,.12f),"Apricot");
                if(level==3)art.Model("Plant",detail,new Vector3(5.39f,.14f,6.82f));
            }
            var machine=art.Group("Waiting vending amenity",root,new Vector3(5.05f,.14f,-2.58f));
            vendingPlot=art.Group("Vending build marker",machine).gameObject;
            art.Box("Future vending crate",vendingPlot.transform,new Vector3(0,.38f,0),new Vector3(.90f,.74f,.70f),"Wood");
            art.Box("Future vending drink icon",vendingPlot.transform,new Vector3(0,.55f,-.36f),new Vector3(.19f,.30f,.025f),"Blue");
            vending=art.Group("Vending machine",machine).gameObject;
            art.Box("Vending cabinet",vending.transform,new Vector3(0,.83f,0),new Vector3(.93f,1.65f,.70f),"Apricot");
            art.Box("Vending window",vending.transform,new Vector3(-.13f,1.05f,-.365f),new Vector3(.56f,.84f,.025f),"SageDark");
            art.Box("Vending delivery slot",vending.transform,new Vector3(-.1f,.30f,-.375f),new Vector3(.63f,.24f,.05f),"Ink");
            vendingButton=art.Group("Vending purchase button",vending.transform,new Vector3(.32f,.94f,-.385f));
            art.Box("Vending button",vendingButton,Vector3.zero,new Vector3(.12f,.12f,.035f),"Gold");
            art.Box("Vending crown",vending.transform,new Vector3(0,1.72f,0),new Vector3(.99f,.13f,.76f),"Wood");
            for(int level=1;level<=3;level++)
            {
                var detail=art.Group("Vending tier "+level,vending.transform);tierDetails[2,level-1]=detail.gameObject;
                for(int bottle=0;bottle<3;bottle++)
                {
                    float x=-.30f+bottle*.18f,y=.72f+(level-1)*.27f;
                    art.Cylinder("Vending refreshment",detail,new Vector3(x,y,-.386f),new Vector3(.105f,.17f,.09f),level==1?"Blue":level==2?"Gold":"Rose");
                    art.Box("Refreshment label",detail,new Vector3(x,y,-.439f),new Vector3(.08f,.05f,.015f),"Linen");
                }
            }
            var tipRoot=art.Group("Vending tip cup",root,VendingCashPoint-new Vector3(0,.20f,0));tipCup=tipRoot.gameObject;
            art.Box("Vending tip shelf",tipRoot,new Vector3(-.08f,-.21f,0),new Vector3(.42f,.06f,.35f),"Wood");
            art.Cylinder("Tip cup",tipRoot,new Vector3(0,-.05f,0),new Vector3(.23f,.29f,.23f),"Sage");
            cash=art.Group("Collectable vending tips",tipRoot).gameObject;
            for(int i=0;i<3;i++)art.Cylinder("Vending tip coin",cash.transform,new Vector3((i-1)*.045f,.105f+i*.025f,0),new Vector3(.12f,.03f,.12f),"Gold");
            vendingTip=art.Cylinder("Patient leaves a tip",root,VendingCashPoint,new Vector3(.14f,.035f,.14f),"Gold").transform;
            toilet.SetActive(false);vending.SetActive(false);cash.SetActive(false);vendingTip.gameObject.SetActive(false);
        }
        internal void Render(ClinicState state,bool reducedMotion)
        {
            int parking=0,toiletLevel=0,vendingLevel=0;VendingTill=0;
            for(int i=0;i<state.Amenities.Count;i++)
            {
                var amenity=state.Amenities[i];int level=Mathf.Clamp(amenity.Level,0,3);
                if(amenity.Kind==ClinicAmenity.Parking)parking=level;
                else if(amenity.Kind==ClinicAmenity.Toilet)toiletLevel=level;
                else { vendingLevel=level;VendingTill=amenity.Till; }

            }
            for(int kind=1;kind<3;kind++)for(int tier=1;tier<=3;tier++)tierDetails[kind,tier-1].SetActive(tier<=(kind==0?parking:kind==1?toiletLevel:vendingLevel));
            parkingWorld.Render(state,parking,reducedMotion);
            toiletClosure.SetActive(toiletLevel==0);
            toilet.SetActive(toiletLevel>0);toiletPlot.SetActive(toiletLevel==0);vending.SetActive(vendingLevel>0);vendingPlot.SetActive(vendingLevel==0);
            tipCup.SetActive(vendingLevel>0);cash.SetActive(vendingLevel>0&&VendingTill>0);
            bool toiletApproach=false,toiletOccupied=false;ClinicPatientState vendingUser=null;
            for(int i=0;i<state.Patients.Count;i++)
            {
                var patient=state.Patients[i];bool visit=patient.Phase==ClinicPatientPhase.UsingAmenity;
                bool walking=patient.Phase==ClinicPatientPhase.WalkingToAmenity||patient.Phase==ClinicPatientPhase.ReturningFromAmenity;
                if(patient.VisitingAmenity==ClinicAmenity.Toilet) { toiletOccupied|=visit;toiletApproach|=walking; }
                if(patient.VisitingAmenity==ClinicAmenity.Vending&&visit)vendingUser=patient;
            }
            toiletDoor.localPosition=new Vector3(3.43f+(toiletApproach&&!toiletOccupied?1.03f:0),.14f,5f);
            bool tipping=vendingUser!=null&&!reducedMotion;vendingTip.gameObject.SetActive(tipping);
            if(tipping)
            {
                float t=Mathf.Clamp01((float)((state.Tick+state.SubTick-vendingUser.PhaseStartedTick)/Mathf.Max(1,vendingUser.PhaseEndsTick-vendingUser.PhaseStartedTick)));
                vendingTip.position=Vector3.Lerp(new Vector3(5.05f,1.06f,-3.42f),VendingCashPoint,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.25f;
            }
            vendingButton.localScale=Vector3.one*(vendingUser==null||reducedMotion?1:.91f+.09f*Mathf.Cos((float)(state.Tick+state.SubTick)*.25f));
        }
    }
}
