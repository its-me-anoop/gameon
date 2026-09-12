using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Small fixed ambient pools. The crossing owns the road for its whole pedestrian interval.</summary>
    internal sealed class ClinicStreetLife
    {
        private readonly Vector3 offset;private readonly float northPavementOffset,roadOffset;
        private readonly Transform[] cars=new Transform[4],pedestrians=new Transform[6],leftLegs=new Transform[6],rightLegs=new Transform[6];
        private readonly Transform[][] trafficWheels=new Transform[4][];
        private static readonly string[] CarColors={"Apricot","Sage","Blue","Mustard","Rose","Denim"};
        internal ClinicStreetLife(ClinicArt art,Transform parent,Vector3 offset=default,float northPavementOffset=0,float roadOffset=0)
        {
            this.offset=offset;this.northPavementOffset=northPavementOffset;this.roadOffset=roadOffset;var root=art.Group("Neighbourhood street life",parent);
            for(int i=0;i<4;i++)
            {
                cars[i]=Car(art,root,"Traffic car "+i,Vector3.zero,i).transform;
                var wheelParts=new System.Collections.Generic.List<Transform>();
                foreach(var part in cars[i].GetComponentsInChildren<Transform>())if(part.name=="Car tyre"||part.name=="Car hubcap")wheelParts.Add(part);
                trafficWheels[i]=wheelParts.ToArray();
            }
            for(int i=0;i<6;i++)
            {
                var person=art.Group("Street pedestrian "+i,root);pedestrians[i]=person;
                person.localScale=Vector3.one*(i==4?.83f:i==5?.94f:.98f);
                art.Orb("Pedestrian head",person,new Vector3(0,1.40f,0),new Vector3(.32f,.36f,.30f),i%3==0?"SkinDeep":i%3==1?"SkinLight":"SkinBrown");
                art.Orb("Pedestrian hair",person,new Vector3(0,1.52f,-.02f),new Vector3(.33f,.17f,.30f),i==5?"HairSilver":"HairChestnut");
                art.Box("Pedestrian coat",person,new Vector3(0,.99f,0),new Vector3(.43f,.58f,.27f),CarColors[i]);
                art.Orb("Pedestrian arm",person,new Vector3(-.29f,.93f,0),new Vector3(.13f,.49f,.15f),CarColors[i]);
                art.Orb("Pedestrian arm",person,new Vector3(.29f,.93f,0),new Vector3(.13f,.49f,.15f),CarColors[i]);
                leftLegs[i]=art.Group("Pedestrian left leg",person,new Vector3(-.13f,.72f,0));
                rightLegs[i]=art.Group("Pedestrian right leg",person,new Vector3(.13f,.72f,0));
                foreach(var leg in new[]{leftLegs[i],rightLegs[i]})
                {
                    art.Box("Pedestrian trousers",leg,new Vector3(0,-.27f,0),new Vector3(.16f,.53f,.18f),"Denim");
                    art.Orb("Pedestrian shoe",leg,new Vector3(0,-.59f,.055f),new Vector3(.20f,.14f,.30f),"Ink");
                }
                if(i%2==0)art.Box("Pedestrian shoulder bag",person,new Vector3(.32f,.73f,.08f),new Vector3(.16f,.26f,.23f),"Wood");
                if(i==3)art.Orb("Pedestrian hair bun",person,new Vector3(0,1.56f,-.18f),new Vector3(.22f,.21f,.23f),"HairChestnut");
            }
        }
        internal void Render(ClinicState state,bool reducedMotion)
        {
            double seconds=reducedMotion?0:(ClinicRules.TrafficTick(state)+state.SubTick)*.1;
            float cycle=(float)(seconds%40);
            // Ambient cars use the far lane; the near lane belongs to patient vehicles.
            // Far-lane traffic yields throughout the zebra crossing interval.
            float west=cycle<10?Mathf.Lerp(55,2.90f,cycle/10):cycle<20?2.90f:Mathf.Lerp(2.90f,-55,(cycle-20)/20);
            for(int i=0;i<4;i++)
            {
                float x=west+i*4.5f;
                cars[i].position=new Vector3(x,-.11f,-9.75f+roadOffset)+offset;cars[i].rotation=Quaternion.Euler(0,-90,0);
                var roll=Quaternion.Euler((55-x)/.175f*Mathf.Rad2Deg,0,0)*Quaternion.Euler(0,0,90);
                for(int wheel=0;wheel<trafficWheels[i].Length;wheel++)trafficWheels[i][wheel].localRotation=roll;
            }
            for(int i=0;i<6;i++)
            {
                float step=(float)(seconds*.78+i*1.2);Vector3 point;Vector3 direction;
                if(i==0)
                {
                    float journey=(float)(seconds%80);float half=journey%40;bool returning=journey>=40;
                    float t=Mathf.Clamp01((half-11)/7);
                    float north=-6.97f+northPavementOffset,south=-11.08f+roadOffset;
                    point=new Vector3(1.38f,.14f,Mathf.Lerp(returning?north:south,returning?south:north,t));
                    direction=returning?Vector3.back:Vector3.forward;
                    if(half>=18&&half<32)
                    {
                        float stroll=half-18;bool outwards=stroll<7;
                        point.x+=(returning?-1:1)*(outwards?stroll:14-stroll)*.45f;
                        direction=(outwards!=returning)?Vector3.right:Vector3.left;
                    }
                }
                else
                {
                    // The reception-side pavement stays available for arriving parking passengers.
                    float lower=i%2==0?-12f:2.6f,span=11-lower;
                    float distance=(float)((seconds*.47+i*4.1)%(span*2));bool forward=distance<span;
                    float x=lower+(forward?distance:span*2-distance);
                    point=new Vector3(x,.14f,i%2==0?-11.06f+roadOffset:-6.95f+northPavementOffset);direction=forward?Vector3.right:Vector3.left;
                }
                pedestrians[i].position=point+offset;pedestrians[i].rotation=Quaternion.LookRotation(direction);
                float swing=reducedMotion?0:Mathf.Sin(step*5)*24;
                if(i==0&&(cycle<11||cycle>=32))swing=0;
                leftLegs[i].localRotation=Quaternion.Euler(swing,0,0);rightLegs[i].localRotation=Quaternion.Euler(-swing,0,0);
            }
        }
        internal static GameObject Car(ClinicArt art,Transform parent,string name,Vector3 position,int appearance)
        {
            var root=art.Group(name,parent,position);string color=CarColors[appearance%CarColors.Length];
            art.Box("Car rounded body",root,new Vector3(0,.41f,0),new Vector3(.95f,.42f,1.85f),color);
            art.Orb("Car cabin",root,new Vector3(0,.75f,-.12f),new Vector3(.88f,.68f,1.02f),color);
            art.Box("Car windscreen",root,new Vector3(0,.76f,.25f),new Vector3(.70f,.31f,.045f),"Blue").transform.localRotation=Quaternion.Euler(24,0,0);
            art.Box("Car rear window",root,new Vector3(0,.76f,-.51f),new Vector3(.66f,.25f,.035f),"Blue").transform.localRotation=Quaternion.Euler(-20,0,0);
            for(int side=-1;side<=1;side+=2)
            {
                art.Box("Car side window",root,new Vector3(side*.445f,.76f,-.12f),new Vector3(.025f,.29f,.49f),"Blue");
                for(int wheel=-1;wheel<=1;wheel+=2)
                {
                    var tire=art.Cylinder("Car tyre",root,new Vector3(side*.48f,.25f,wheel*.58f),new Vector3(.35f,.12f,.35f),"Ink");tire.transform.localRotation=Quaternion.Euler(0,0,90);
                    var hub=art.Cylinder("Car hubcap",root,new Vector3(side*.55f,.25f,wheel*.58f),new Vector3(.17f,.014f,.17f),"Gold");hub.transform.localRotation=Quaternion.Euler(0,0,90);
                    art.Box("Car wheel spoke",hub.transform,new Vector3(0,-side*1.06f,0),new Vector3(.78f,.12f,.11f),"SageDark");
                    art.Box("Car wheel spoke",hub.transform,new Vector3(0,-side*1.06f,0),new Vector3(.11f,.12f,.78f),"SageDark");
                }
                art.Box("Car headlamp",root,new Vector3(side*.30f,.42f,.94f),new Vector3(.20f,.13f,.025f),"Linen");
                art.Box("Car tail lamp",root,new Vector3(side*.32f,.44f,-.94f),new Vector3(.16f,.10f,.025f),"Rose");
            }
            art.Box("Car front bumper",root,new Vector3(0,.26f,.96f),new Vector3(.75f,.07f,.06f),"Wood");
            return root.gameObject;
        }
    }
}
