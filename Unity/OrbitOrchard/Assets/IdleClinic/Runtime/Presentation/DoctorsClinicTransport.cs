using System;
using System.Collections.Generic;
using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    internal static class DoctorsParkingLayout
    {
        internal static readonly Vector3 Offset=new Vector3(-8,0,-4);
        internal static readonly Vector3 SignPoint=ClinicParkingLayout.SignPoint+Offset;
        internal static Vector3 BayCenter(int bay)=>ClinicParkingLayout.BayCenter(bay)+Offset;
        internal static Vector3 BayDoor(int bay)=>ClinicParkingLayout.BayDoor(bay)+Offset;
        internal static Vector3 TaxiDoor(int dock)=>new Vector3(18f+dock*3.6f,.14f,-9.80f);
        internal static Vector3 TaxiCenter(int dock)=>new Vector3(18f+dock*3.6f,-.11f,-10.80f);
    }
    /// <summary>Each taxi is a saved ride, allowing an empty drop-off car to depart during care.</summary>
    internal sealed class DoctorsClinicTransport
    {
        private readonly ClinicParkingPresentation parking;private readonly ClinicStreetLife street;
        private readonly TaxiVehicle[] taxis=new TaxiVehicle[2];private readonly GameObject[] tiers=new GameObject[6];
        internal DoctorsClinicTransport(ClinicArt art,Transform parent)
        {
            parking=new ClinicParkingPresentation(art,parent,true);street=new ClinicStreetLife(art,parent,new Vector3(0,0,-4),-.90f,-1.0f);
            var stand=art.Group("Doctors taxi stand",parent,new Vector3(12.6f,0,1.1f));
            art.Box("Taxi street connection",stand,new Vector3(7.2f,-.075f,-13.1f),new Vector3(7.6f,.07f,2.4f),"Ink");
            art.Box("Taxi layby",stand,new Vector3(7.2f,-.075f,-11.82f),new Vector3(7.0f,.07f,1.8f),"Ink");
            for(int dock=0;dock<2;dock++)
            {
                float x=DoctorsParkingLayout.TaxiCenter(dock).x-12.6f;
                for(int side=-1;side<=1;side+=2)art.Box("Taxi bay marking",stand,new Vector3(x+side*1.5f,-.032f,-11.9f),new Vector3(.065f,.014f,1.30f),"Gold");
                var car=ClinicStreetLife.Car(art,stand,"Patient taxi "+dock,Vector3.zero,3);taxis[dock]=new TaxiVehicle(car,dock);
                art.Box("Taxi roof sign",car.transform,new Vector3(0,1.18f,-.07f),new Vector3(.57f,.18f,.25f),"Gold");
                for(int n=0;n<5;n++)art.Box("Taxi checker",car.transform,new Vector3(-.23f+n*.115f,1.19f,.068f),new Vector3(.06f,.075f,.018f),n%2==0?"Ink":"Linen");
            }
            for(int level=1;level<=6;level++)
            {
                var d=art.Group("Taxi stand tier "+level,stand);tiers[level-1]=d.gameObject;
                if(level==1)
                {art.Box("Taxi call post",d,new Vector3(4.0f,.85f,-10.15f),new Vector3(.12f,1.7f,.12f),"Gold");art.Box("Taxi sign",d,new Vector3(4.0f,1.65f,-10.15f),new Vector3(.83f,.44f,.08f),"SageDark");art.Box("Taxi pictogram body",d,new Vector3(4.0f,1.63f,-10.2f),new Vector3(.52f,.12f,.018f),"Gold");}
                else if(level==2)
                {
                    for(int side=-1;side<=1;side+=2)
                    {
                        art.Box("Taxi shelter upright",d,new Vector3(7.2f+side*2.4f,1.10f,-9.80f),new Vector3(.10f,2.2f,.10f),"SageDark");
                        art.Box("Taxi canopy long rim",d,new Vector3(7.2f,2.20f,-10.10f+side*.58f),new Vector3(5.2f,.10f,.09f),"Wood");
                        art.Box("Taxi canopy end rim",d,new Vector3(7.2f+side*2.4f,2.20f,-10.10f),new Vector3(.10f,.10f,1.25f),"Wood");
                        art.Box("Taxi canopy crossmember",d,new Vector3(7.2f+side*1.5f,2.20f,-10.10f),new Vector3(.06f,.07f,1.16f),"Wood");
                    }
                }
                else if(level==3)
                {art.Box("Taxi waiting bench",d,new Vector3(12.8f,.56f,-8.35f),new Vector3(2.1f,.13f,.48f),"Wood");for(int side=-1;side<=1;side+=2)art.Box("Taxi bench foot",d,new Vector3(12.8f+side*.8f,.29f,-8.35f),new Vector3(.1f,.54f,.4f),"SageDark");}
                else if(level==4)
                {
                    ClinicFurnishings.NoticeBoard(art,d,"Taxi routes board",new Vector3(12.2f,1.3f,-9.85f),0,.7f);
                    art.Box("Taxi routes board support",d,new Vector3(12.2f,.59f,-9.85f),new Vector3(.08f,.94f,.08f),"SageDark");
                }
                else if(level==5){art.Model("Plant",d,new Vector3(3.0f,.14f,-10.0f));art.Model("Plant",d,new Vector3(11.1f,.14f,-10.0f));}
                else {for(int i=0;i<3;i++)art.Orb("Taxi shelter downlight",d,new Vector3(5.8f+i*1.4f,2.10f,-10.68f),new Vector3(.20f,.12f,.2f),"Linen");}
            }
        }
        internal void Render(ClinicState state,bool reduced)
        {
            parking.Render(state,state.Amenity(ClinicAmenity.Parking)?.Level??0,reduced);street.Render(state,reduced);
            int level=state.Amenity(ClinicAmenity.Taxi)?.Level??0;for(int i=0;i<6;i++)tiers[i].SetActive(i<level);
            for(int dock=0;dock<2;dock++)
            {ClinicTaxiState ride=null;for(int i=0;i<state.TaxiRides.Count;i++)if(state.TaxiRides[i].DockId==dock){ride=state.TaxiRides[i];break;}taxis[dock].Render(ride,state.Tick+state.SubTick);}
        }
        private sealed class TaxiVehicle
        {
            private readonly GameObject root;private readonly Vector3 center;private readonly ClinicParkingPath arrival,departure;private readonly Transform[] wheels;
            internal TaxiVehicle(GameObject root,int dock)
            {
                this.root=root;center=DoctorsParkingLayout.TaxiCenter(dock);
                arrival=new ClinicParkingPath(new Vector3(-36,-.11f,-13.2f),new Vector3(center.x-3,-.11f,-13.2f),center);
                departure=new ClinicParkingPath(center,new Vector3(center.x+3,-.11f,-13.2f),new Vector3(38,-.11f,-13.2f));
                var list=new List<Transform>();foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name=="Car tyre"||t.name=="Car hubcap")list.Add(t);wheels=list.ToArray();
            }
            internal void Render(ClinicTaxiState ride,double tick)
            {
                root.SetActive(ride!=null);if(ride==null)return;
                var p=center;var direction=Vector3.right;float distance=0;
                float t=Mathf.Clamp01((float)((tick-ride.PhaseStartedTick)/Math.Max(1,ride.PhaseEndsTick-ride.PhaseStartedTick)));
                if(ride.Phase==ClinicTaxiPhase.Approaching)p=arrival.Sample(t,out direction,out distance);
                else if(ride.Phase==ClinicTaxiPhase.Departing)p=departure.Sample(t,out direction,out distance);
                root.transform.SetPositionAndRotation(p,Quaternion.LookRotation(direction));
                for(int i=0;i<wheels.Length;i++)wheels[i].localRotation=Quaternion.Euler(distance/.175f*Mathf.Rad2Deg,0,0)*Quaternion.Euler(0,0,90);
            }
        }
    }
}
