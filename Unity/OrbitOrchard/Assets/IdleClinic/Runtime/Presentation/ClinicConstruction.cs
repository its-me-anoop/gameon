using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Work appears only while the authoritative room job exists; its meter uses that job's ticks.</summary>
    internal sealed class ClinicConstruction
    {
        private readonly GameObject[] roots;
        private readonly Transform[] progress,tool;
        internal ClinicConstruction(ClinicArt art,Transform parent,Vector3[] positions=null)
        {
            int count=positions?.Length??3;roots=new GameObject[count];progress=new Transform[count];tool=new Transform[count];
            for(int i=0;i<count;i++)
            {
                string room=((ClinicRoom)i).ToString();
                var center=positions!=null?positions[i]:i==0?new Vector3(-5.96f,.14f,-2.35f):i==1?new Vector3(-5.96f,.14f,2.4f):new Vector3(6.07f,.14f,2.6f);
                var root=art.Group(room+" renovation scaffold",parent,center);roots[i]=root.gameObject;
                for(int end=-1;end<=1;end+=2)
                {
                    art.Cylinder("Scaffold upright",root,new Vector3(0,1.25f,end*1.2f),new Vector3(.065f,2.5f,.065f),"Gold");
                    art.Box("Scaffold foot",root,new Vector3(0,.04f,end*1.2f),new Vector3(.42f,.08f,.42f),"SageDark");
                }
                for(int level=0;level<3;level++)
                {
                    art.Box("Scaffold safety rail",root,new Vector3(0,.45f+level*.86f,0),new Vector3(.055f,.055f,2.46f),"Gold");
                    if(level<2)art.Box("Scaffold platform",root,new Vector3(0,.32f+level*.95f,0),new Vector3(.55f,.08f,2.50f),"Wood");
                }
                var brace=art.Box("Scaffold diagonal brace",root,new Vector3(-.04f,1.15f,0),new Vector3(.045f,.045f,3.00f),"SageDark");brace.transform.localRotation=Quaternion.Euler(44,0,0);
                art.Box("Renovation hanging cloth",root,new Vector3(.01f,.96f,.91f),new Vector3(.56f,.82f,.035f),"TileBlue");
                var worker=art.Group("Renovation worker",root,new Vector3(0,.37f,-.54f));
                art.Box("Worker overalls",worker,new Vector3(0,.7f,0),new Vector3(.35f,.48f,.26f),"Denim");
                art.Orb("Worker head",worker,new Vector3(0,1.09f,0),new Vector3(.29f,.32f,.27f),"SkinBrown");
                art.Orb("Worker hard hat",worker,new Vector3(0,1.23f,0),new Vector3(.37f,.19f,.34f),"Gold");
                for(int side=-1;side<=1;side+=2)art.Box("Worker boots",worker,new Vector3(side*.10f,.17f,.05f),new Vector3(.14f,.39f,.20f),"Ink");
                tool[i]=art.Group("Worker roller arm",worker,new Vector3(i==2?-.23f:.23f,.91f,0));
                art.Box("Worker sleeve",tool[i],new Vector3(0,.10f,0),new Vector3(.12f,.38f,.13f),"Apricot");
                art.Cylinder("Paint roller handle",tool[i],new Vector3(0,.42f,0),new Vector3(.035f,.38f,.035f),"Wood");
                var roller=art.Cylinder("Paint roller",tool[i],new Vector3(0,.62f,0),new Vector3(.13f,.34f,.13f),"TileSage");roller.transform.localRotation=Quaternion.Euler(90,0,0);
                var meter=art.Group("Construction progress board",root,new Vector3(i==2?-.44f:.44f,2.43f,0));
                art.Box("Construction meter frame",meter,Vector3.zero,new Vector3(.13f,.20f,1.42f),"SageDark");
                progress[i]=art.Group(room+" construction progress",meter,new Vector3(0,0,-.62f));progress[i].localRotation=Quaternion.Euler(0,-90,0);
                art.Box("Construction meter fill",progress[i],new Vector3(.62f,0,0),new Vector3(1.24f,.12f,.15f),"Gold");
                root.gameObject.SetActive(false);
            }
        }
        internal void Render(ClinicState state,bool reducedMotion)
        {
            for(int room=0;room<roots.Length;room++)
            {
                ClinicConstructionState job=null;
                for(int i=0;i<state.Construction.Count;i++)if((int)state.Construction[i].Room==room){job=state.Construction[i];break;}
                roots[room].SetActive(job!=null);if(job==null)continue;
                double duration=job.EndsTick-job.StartedTick;
                float fraction=duration<=0?0:Mathf.Clamp01((float)((state.Tick+state.SubTick-job.StartedTick)/duration));
                progress[room].localScale=new Vector3(fraction,1,1);
                tool[room].localRotation=Quaternion.Euler(reducedMotion?0:Mathf.Sin((float)(state.Tick+state.SubTick)*.23f)*26,0,room==2?22:-22);
            }
        }
    }
}
