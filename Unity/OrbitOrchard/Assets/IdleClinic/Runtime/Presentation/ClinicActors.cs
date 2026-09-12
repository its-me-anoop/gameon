using System;
using System.Collections.Generic;
using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Pooled skinned actors. Authoritative simulation ticks drive both travel and authored poses.</summary>
    internal sealed class ClinicActors
    {
        private static readonly string[] PoseNames={"Idle","Walk","CheckIn","Treat","Sit","Call"};
        private readonly ClinicArt art;
        private readonly Transform parent;
        private readonly ClinicWorld world;
        private readonly Dictionary<int,Actor> patients=new Dictionary<int,Actor>();
        private readonly Dictionary<int,Actor> staff=new Dictionary<int,Actor>();
        private readonly Stack<Actor> patientPool=new Stack<Actor>();
        private readonly List<int> retired=new List<int>(40);
        private readonly Dictionary<string,AnimationClip[]> clips=new Dictionary<string,AnimationClip[]>();
        internal ClinicActors(ClinicArt art,Transform parent,ClinicWorld world)
        { this.art=art;this.parent=parent;this.world=world; }

        internal void Render(ClinicState state,bool reducedMotion)
        {
            double tick=state.Tick+state.SubTick;
            retired.Clear();
            foreach(var entry in patients)
            {
                bool present=false;for(int i=0;i<state.Patients.Count;i++)if(state.Patients[i].Id==entry.Key){present=true;break;}
                if(!present)retired.Add(entry.Key);
            }
            for(int i=0;i<retired.Count;i++)
            { var actor=patients[retired[i]];actor.Root.gameObject.SetActive(false);patientPool.Push(actor);patients.Remove(retired[i]); }
            for(int i=0;i<state.Patients.Count;i++)
            {
                var person=state.Patients[i];
                if(!patients.TryGetValue(person.Id,out var actor))
                { actor=patientPool.Count>0?patientPool.Pop():Create("Patient");patients.Add(person.Id,actor);actor.Root.name="Patient "+person.Id;actor.Root.gameObject.SetActive(true); }
                bool walking=person.Phase==ClinicPatientPhase.Arriving||person.Phase==ClinicPatientPhase.WalkingToReception||
                    person.Phase==ClinicPatientPhase.WalkingToWaiting||person.Phase==ClinicPatientPhase.WalkingToTreatment||person.Phase==ClinicPatientPhase.Leaving;
                bool seated=(person.Phase==ClinicPatientPhase.Seated||person.Phase==ClinicPatientPhase.Treating)&&world.HasSeatAt(person.ToAnchor);
                int pose=walking?1:person.Phase==ClinicPatientPhase.CheckingIn?2:seated?4:0;
                Place(actor,person.FromAnchor,person.ToAnchor,Progress(tick,person.PhaseStartedTick,person.PhaseEndsTick),walking,false);
                actor.Sample(pose,tick*.1+person.Id*.17,reducedMotion,person.Phase==ClinicPatientPhase.Leaving);
            }
            foreach(var entry in staff)
            {
                bool present=false;for(int i=0;i<state.Staff.Count;i++)if(state.Staff[i].Id==entry.Key){present=true;break;}
                if(!present&&entry.Value.Root.gameObject.activeSelf)entry.Value.Root.gameObject.SetActive(false);
            }
            for(int i=0;i<state.Staff.Count;i++)
            {
                var member=state.Staff[i];
                if(!staff.TryGetValue(member.Id,out var actor))
                { actor=Create(member.Role==ClinicStaffRole.Nurse?"Nurse":"Receptionist");staff.Add(member.Id,actor);actor.Root.name="Staff "+member.Id; }
                if(!actor.Root.gameObject.activeSelf)actor.Root.gameObject.SetActive(true);
                bool walking=tick<member.MoveEndsTick;
                int pose=walking?1:member.PatientId<0?0:member.Role==ClinicStaffRole.Receptionist?2:3;
                if(!walking&&member.Role==ClinicStaffRole.Nurse&&member.PatientId>=0)
                    for(int p=0;p<state.Patients.Count;p++)if(state.Patients[p].Id==member.PatientId&&state.Patients[p].Phase==ClinicPatientPhase.WalkingToTreatment){pose=5;break;}
                Place(actor,member.FromAnchor,member.ToAnchor,Progress(tick,member.MoveStartedTick,member.MoveEndsTick),walking,true);
                actor.Sample(pose,tick*.1+member.Id*.13,reducedMotion);
            }
        }
        private static float Progress(double tick,long from,long until)=>until<=from?1:Mathf.Clamp01((float)((tick-from)/(until-from)));
        private void Place(Actor actor,string from,string to,float progress,bool walking,bool isStaff)
        {
            actor.Moving=walking;
            if(!walking)
            { actor.Root.position=world.GetAnchorPoint(to);actor.Root.rotation=Quaternion.LookRotation(world.Facing(to));return; }
            if(actor.From!=from||actor.To!=to||actor.IsStaff!=isStaff)
            { actor.From=from;actor.To=to;actor.IsStaff=isStaff;actor.Route.Build(world,from,to,isStaff); }
            actor.Root.position=actor.Route.Sample(progress,out var direction);
            if(direction.sqrMagnitude>.0001f)actor.Root.rotation=Quaternion.LookRotation(direction);
        }
        internal bool ApproachesDoor(Vector3 point,float halfWidth,float distance)
        {
            foreach(var entry in patients)if(Near(entry.Value,point,halfWidth,distance))return true;
            foreach(var entry in staff)if(Near(entry.Value,point,halfWidth,distance))return true;
            return false;
        }
        private static bool Near(Actor actor,Vector3 point,float halfWidth,float distance)
        {
            if(!actor.Moving||!actor.Root.gameObject.activeSelf)return false;
            var delta=actor.Root.position-point;return Mathf.Abs(delta.x)<=halfWidth&&Mathf.Abs(delta.z)<=distance;
        }
        private Actor Create(string model)
        {
            var root=art.Model(model,parent,Vector3.zero).transform;
            var animation=root.GetComponentInChildren<Animation>();
            if(animation==null)animation=(root.childCount>0?root.GetChild(0):root).gameObject.AddComponent<Animation>();
            animation.playAutomatically=false;animation.Stop();
            if(!clips.TryGetValue(model,out var source))
            { source=Resources.LoadAll<AnimationClip>("Clinic/Models/"+model);clips.Add(model,source); }
            var states=new AnimationState[PoseNames.Length];
            for(int p=0;p<PoseNames.Length;p++)for(int i=0;i<source.Length;i++)
            {
                if(!source[i].name.EndsWith(PoseNames[p],StringComparison.Ordinal))continue;
                animation.AddClip(source[i],PoseNames[p]);states[p]=animation[PoseNames[p]];
                states[p].enabled=false;states[p].wrapMode=WrapMode.Loop;states[p].weight=1;break;
            }
            // All animation bounds include the bent arms and seated knees; culling remains enabled.
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                skin.localBounds=new Bounds(new Vector3(0,.80f,0),new Vector3(2.2f,2.2f,2.2f));
            Transform head=null,relief=null;AnimationState departureWave=null;
            if(model=="Patient")
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                if(skin!=null)
                {
                    head=Array.Find(skin.bones,b=>b.name=="head");var arm=Array.Find(skin.bones,b=>b.name=="upper_arm.R");
                    if(arm!=null&&states[5]!=null)
                    { departureWave=states[5];departureWave.AddMixingTransform(arm,true);departureWave.layer=1; }
                }
                relief=art.Group("Departure relief",root);
                art.Box("Forehead dressing",relief,new Vector3(0,.075f,.164f),new Vector3(.135f,.065f,.027f),"Linen");
                art.Box("Dressing pad",relief,new Vector3(.018f,.075f,.183f),new Vector3(.050f,.042f,.014f),"Apricot");
                for(int i=-2;i<=2;i++)
                    art.Box("Relieved smile",relief,new Vector3(i*.015f,-.074f+.006f*i*i,.163f),new Vector3(.019f,.011f,.011f),"Ink");
                relief.gameObject.SetActive(false);
            }
            return new Actor(root,animation,states,head,relief,departureWave);
        }
        private sealed class Actor
        {
            internal readonly Transform Root;
            internal readonly ClinicRoute Route=new ClinicRoute();
            internal string From,To;
            internal bool IsStaff,Moving;
            private readonly Animation animation;
            private readonly AnimationState[] states;
            private readonly Transform head,relief;
            private readonly AnimationState departureWave;
            private int current=-1;
            internal Actor(Transform root,Animation animation,AnimationState[] states,Transform head,Transform relief,AnimationState departureWave)
            { Root=root;this.animation=animation;this.states=states;this.head=head;this.relief=relief;this.departureWave=departureWave; }
            internal void Sample(int pose,double seconds,bool reducedMotion,bool departing=false)
            {
                if(states[pose]==null)pose=0;if(states[pose]==null)return;
                if(current!=pose)
                { if(current>=0&&states[current]!=null)states[current].enabled=false;current=pose;states[current].enabled=true; }
                var state=states[current];state.time=reducedMotion||pose==4?0:(float)(seconds%Math.Max(.001f,state.length));
                if(departureWave!=null)
                { departureWave.enabled=departing&&!reducedMotion;departureWave.time=(float)(seconds%Math.Max(.001f,departureWave.length)); }
                animation.Sample();
                if(relief!=null)
                {
                    if(relief.gameObject.activeSelf!=departing)relief.gameObject.SetActive(departing);
                    if(departing&&head!=null){relief.position=head.position;relief.rotation=Root.rotation;}
                }
            }
        }
    }

    /// <summary>Routes use the open front aisle and central corridor, never a straight line through furniture.</summary>
    internal sealed class ClinicRoute
    {
        private readonly Vector3[] points=new Vector3[12];
        private int count;
        private float length;
        internal void Build(ClinicWorld world,string from,string to,bool staff)
        {
            count=0;length=0;var start=world.GetAnchorPoint(from);var end=world.GetAnchorPoint(to);Add(start);
            if(from==to){Add(end);return;}
            float lane=staff?.95f:.40f;
            bool fromDesk=Starts(from,"reception.desk."),toDesk=Starts(to,"reception.desk.");
            bool fromQueue=Starts(from,"reception.queue."),toQueue=Starts(to,"reception.queue.");
            bool fromSeat=Starts(from,"waiting.seat."),toSeat=Starts(to,"waiting.seat.");
            bool fromCare=Starts(from,"firstaid.station."),toCare=Starts(to,"firstaid.station.");
            if(fromDesk) { float z=staff?-1.15f:-4.05f;Add(new Vector3(start.x,start.y,z));Add(new Vector3(lane,start.y,z)); }
            else if(fromSeat) { Add(new Vector3(3.40f,start.y,start.z));Add(new Vector3(3.40f,start.y,.35f));Add(new Vector3(lane,start.y,.35f)); }
            else if(fromCare) { Add(new Vector3(start.x,start.y,1.03f));Add(new Vector3(lane,start.y,1.03f)); }
            else if(fromQueue && !toDesk && !toQueue) { Add(new Vector3(start.x,start.y,-5.82f));Add(new Vector3(lane,start.y,-5.82f)); }
            else if(!fromQueue)Add(new Vector3(lane,start.y,start.z));
            if(toDesk)
            {
                float z=staff?-1.15f:-4.05f;
                if(!fromQueue)Add(new Vector3(lane,end.y,z));
                else Add(new Vector3(start.x,end.y,z));
                Add(new Vector3(end.x,end.y,z));
            }
            else if(toSeat) { Add(new Vector3(lane,end.y,.35f));Add(new Vector3(3.40f,end.y,.35f));Add(new Vector3(3.40f,end.y,end.z)); }
            else if(toCare) { Add(new Vector3(lane,end.y,1.03f));Add(new Vector3(end.x,end.y,1.03f)); }
            else if(toQueue) { Add(new Vector3(lane,end.y,end.z)); }
            else Add(new Vector3(lane,end.y,end.z));
            Add(end);
        }
        private static bool Starts(string value,string prefix)=>value!=null&&value.StartsWith(prefix,StringComparison.Ordinal);
        private void Add(Vector3 point)
        { if(count>0){float distance=Vector3.Distance(points[count-1],point);if(distance<.001f)return;length+=distance;}points[count++]=point; }
        internal Vector3 Sample(float progress,out Vector3 direction)
        {
            direction=Vector3.forward;if(count==0)return Vector3.zero;if(count==1)return points[0];
            float remaining=length*Mathf.Clamp01(progress);
            for(int i=1;i<count;i++)
            { var delta=points[i]-points[i-1];float distance=delta.magnitude;if(remaining<=distance||i==count-1){direction=delta.normalized;return Vector3.Lerp(points[i-1],points[i],distance==0?1:remaining/distance);}remaining-=distance; }
            return points[count-1];
        }
    }
}
