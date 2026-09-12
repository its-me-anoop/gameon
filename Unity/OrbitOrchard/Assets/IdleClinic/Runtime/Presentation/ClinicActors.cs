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

        internal int MovingCount { get {int count=0;foreach(var a in patients.Values)if(a.Moving&&a.Root.gameObject.activeSelf)count++;foreach(var a in staff.Values)if(a.Moving&&a.Root.gameObject.activeSelf)count++;return count;} }
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
                { actor=patientPool.Count>0?patientPool.Pop():Create("Patient");patients.Add(person.Id,actor);actor.Root.name="Patient "+person.Id;actor.ResetPlacement(person.Id*.17); }
                bool inVehicle=person.Phase==ClinicPatientPhase.WaitingToPark||person.Phase==ClinicPatientPhase.DrivingToParking||
                    person.Phase==ClinicPatientPhase.WaitingToExit||person.Phase==ClinicPatientPhase.DrivingFromParking||person.Phase==ClinicPatientPhase.TaxiArriving||person.Phase==ClinicPatientPhase.TaxiDroppingOff||person.Phase==ClinicPatientPhase.TaxiPickingUp||person.Phase==ClinicPatientPhase.TaxiDeparting;
                actor.Root.gameObject.SetActive(!inVehicle);
                if(inVehicle){actor.HasPosition=false;actor.Moving=false;continue;}
                actor.Appearance.Apply(person.AppearanceId);
                bool walking=person.Phase==ClinicPatientPhase.Arriving||person.Phase==ClinicPatientPhase.WalkingToReception||
                    person.Phase==ClinicPatientPhase.WalkingToWaiting||person.Phase==ClinicPatientPhase.WalkingToTreatment||person.Phase==ClinicPatientPhase.Leaving||
                    person.Phase==ClinicPatientPhase.WalkingToAmenity||person.Phase==ClinicPatientPhase.ReturningFromAmenity||person.Phase==ClinicPatientPhase.WalkingToConsultation||person.Phase==ClinicPatientPhase.WalkingToPharmacy||person.Phase==ClinicPatientPhase.WalkingToTaxi;
                bool seated=((person.Phase==ClinicPatientPhase.Seated||person.Phase==ClinicPatientPhase.Treating||person.Phase==ClinicPatientPhase.Consulting)&&world.HasSeatAt(person.ToAnchor))||
                    (person.Phase==ClinicPatientPhase.UsingAmenity&&person.VisitingAmenity==ClinicAmenity.Toilet);
                bool doctors=state.Location==ClinicLocation.DoctorsClinic;
                bool queueMovement=doctors&&person.Phase==ClinicPatientPhase.ReceptionQueue&&person.QueueMovePath!=null&&person.QueueMovePath.Count>=2;
                var savedPath=queueMovement?person.QueueMovePath:doctors&&person.Phase==ClinicPatientPhase.Arriving?person.ArrivalPath:null;
                long moveStarted=queueMovement?person.QueueMoveStartedTick:person.PhaseStartedTick;
                long moveEnds=queueMovement?person.QueueMoveEndsTick:person.PhaseEndsTick;
                // The expanded clinic admits only settled queue patients. Render the
                // same saved path and clock so reindexing, saves and fast desks cannot
                // give a rig a shorter visual journey than the simulation reserved.
                Place(actor,person.FromAnchor,person.ToAnchor,Progress(tick,moveStarted,moveEnds),walking||queueMovement,false,tick,
                    !doctors&&person.Phase==ClinicPatientPhase.ReceptionQueue,savedPath,moveStarted);
                int pose=actor.Moving?1:person.Phase==ClinicPatientPhase.CheckingIn||person.Phase==ClinicPatientPhase.Dispensing||person.Phase==ClinicPatientPhase.UsingAmenity&&!seated?2:seated?4:0;
                actor.Sample(pose,tick*.1+person.Id*.17,reducedMotion,person.Phase==ClinicPatientPhase.Leaving||person.Phase==ClinicPatientPhase.WalkingToTaxi,person.FirstAidComplete);actor.Appearance.AfterPose(seated);
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
                { actor=Create(member.Role==ClinicStaffRole.Receptionist?"Receptionist":"Nurse",member.Role);staff.Add(member.Id,actor);actor.Root.name="Staff "+member.Id; }
                if(!actor.Root.gameObject.activeSelf)actor.Root.gameObject.SetActive(true);
                actor.Appearance.Apply(member.Id%12,member.TrainingLevel);
                bool walking=tick<member.MoveEndsTick;
                int pose=walking?1:member.PatientId<0?0:member.Role==ClinicStaffRole.Receptionist?2:3;
                if(!walking&&member.Role==ClinicStaffRole.Nurse&&member.PatientId>=0)
                    for(int p=0;p<state.Patients.Count;p++)if(state.Patients[p].Id==member.PatientId&&state.Patients[p].Phase==ClinicPatientPhase.WalkingToTreatment){pose=5;break;}
                Place(actor,member.FromAnchor,member.ToAnchor,Progress(tick,member.MoveStartedTick,member.MoveEndsTick),walking,true,tick,false);
                actor.Sample(pose,tick*.1+member.Id*.13,reducedMotion);actor.Appearance.AfterPose(false);
            }
        }
        private static float Progress(double tick,long from,long until)=>until<=from?1:Mathf.Clamp01((float)((tick-from)/(until-from)));
        private void Place(Actor actor,string from,string to,float progress,bool walking,bool isStaff,double tick,bool queued,IList<ClinicMovementPoint> arrivalPath=null,long phaseStartedTick=0)
        {
            var before=actor.Root.position;bool placed=actor.HasPosition;
            bool hadSavedPath=actor.SavedArrivalPath!=null;
            float seconds=placed?(float)Math.Max(0,(tick-actor.LastTick)*.1):0;
            if(!walking)
            {
                var destination=world.GetAnchorPoint(to);
                // Queue indices change without a new simulation phase. Walk from the
                // last visible position instead of snapping an idle rig to its next slot.
                if(queued&&placed&&(destination-before).sqrMagnitude>.000001f)
                {
                    if(!actor.QueueMoving||actor.To!=to)
                    { actor.Route.Build(world,actor.To,to,false,before);actor.QueueDistance=0; }
                    actor.QueueDistance=Mathf.Min(actor.Route.Length,actor.QueueDistance+seconds*1.35f);
                    actor.Root.position=actor.Route.Sample(actor.Route.Length<=.001f?1:actor.QueueDistance/actor.Route.Length,out var direction);
                    if(direction.sqrMagnitude>.0001f)actor.Root.rotation=Quaternion.LookRotation(direction);
                    actor.QueueMoving=actor.QueueDistance<actor.Route.Length;
                    actor.Moving=actor.QueueMoving||(actor.Root.position-before).sqrMagnitude>.000001f;
                }
                else
                { actor.Root.position=destination;actor.Root.rotation=Quaternion.LookRotation(world.Facing(to));actor.QueueMoving=false;actor.Moving=false; }
            }
            else if(arrivalPath!=null&&arrivalPath.Count>=2)
            {
                if(!placed||!ReferenceEquals(actor.SavedArrivalPath,arrivalPath)||actor.SavedPhaseStartedTick!=phaseStartedTick)
                {
                    actor.Route.BuildSavedPath(arrivalPath,world.GetAnchorPoint(to).y);actor.SavedArrivalPath=arrivalPath;actor.SavedPhaseStartedTick=phaseStartedTick;actor.RouteStartProgress=0;actor.QueueMoving=false;
                }
                actor.Root.position=actor.Route.Sample(progress,out var direction);
                if(direction.sqrMagnitude>.0001f)actor.Root.rotation=Quaternion.LookRotation(direction);
                actor.Moving=progress<1&&actor.Route.Length>.001f;
            }
            else
            {
                if(!placed||actor.From!=from||actor.To!=to||actor.IsStaff!=isStaff||actor.QueueMoving||hadSavedPath)
                {
                    actor.Route.Build(world,from,to,isStaff,placed?(Vector3?)before:null);
                    actor.RouteStartProgress=placed?progress:0;actor.QueueMoving=false;
                }
                float routeProgress=progress>=1?1:Mathf.InverseLerp(actor.RouteStartProgress,1,progress);
                actor.Root.position=actor.Route.Sample(routeProgress,out var direction);
                if(direction.sqrMagnitude>.0001f)actor.Root.rotation=Quaternion.LookRotation(direction);
                actor.Moving=progress<1&&actor.Route.Length>.001f;
            }
            if(arrivalPath==null)actor.SavedArrivalPath=null;
            if(placed)
            {
                var travelled=actor.Root.position-before;travelled.y=0;
                // The authored feet cover 0.54m per step, two steps per cycle.
                actor.WalkCycles+=travelled.magnitude/(1.08f*Mathf.Max(.1f,actor.Root.localScale.z));
            }
            actor.HasPosition=true;actor.LastTick=tick;actor.From=from;actor.To=to;actor.IsStaff=isStaff;
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
        private Actor Create(string model,ClinicStaffRole role=ClinicStaffRole.Nurse)
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
                states[p].enabled=false;states[p].wrapMode=WrapMode.Loop;states[p].weight=1;states[p].speed=0;break;
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
            return new Actor(root,animation,states,head,relief,departureWave,new ClinicAppearance(art,root,model=="Patient",role));
        }
        private sealed class Actor
        {
            internal readonly Transform Root;
            internal readonly ClinicAppearance Appearance;
            internal readonly ClinicRoute Route=new ClinicRoute();
            internal string From,To;
            internal bool IsStaff,Moving,HasPosition,QueueMoving;
            internal double LastTick,WalkCycles;
            internal float QueueDistance,RouteStartProgress;
            internal IList<ClinicMovementPoint> SavedArrivalPath;internal long SavedPhaseStartedTick;
            private readonly Animation animation;
            private readonly AnimationState[] states;
            private readonly Transform head,relief;
            private readonly AnimationState departureWave;
            private int current=-1;
            internal Actor(Transform root,Animation animation,AnimationState[] states,Transform head,Transform relief,AnimationState departureWave,ClinicAppearance appearance)
            { Appearance=appearance;Root=root;this.animation=animation;this.states=states;this.head=head;this.relief=relief;this.departureWave=departureWave; }
            internal void ResetPlacement(double cycle)
            { From=null;To=null;HasPosition=false;Moving=false;QueueMoving=false;QueueDistance=0;RouteStartProgress=0;SavedArrivalPath=null;SavedPhaseStartedTick=0;WalkCycles=cycle; }
            internal void Sample(int pose,double seconds,bool reducedMotion,bool departing=false,bool bandaged=false)
            {
                if(states[pose]==null)pose=0;if(states[pose]==null)return;
                if(current!=pose)
                { if(current>=0&&states[current]!=null)states[current].enabled=false;current=pose;states[current].enabled=true; }
                var state=states[current];state.enabled=true;
                // Walking explains essential travel, even with reduced motion. Freeze
                // optional idle/service motion, not the feet of a moving character.
                state.time=pose==1?(float)(WalkCycles%1)*state.length:
                    reducedMotion||pose==4?0:(float)(seconds%Math.Max(.001f,state.length));
                if(departureWave!=null)
                { departureWave.enabled=departing&&!reducedMotion;departureWave.time=(float)(seconds%Math.Max(.001f,departureWave.length)); }
                animation.Sample();
                if(relief!=null)
                {
                    if(relief.gameObject.activeSelf!=(departing||bandaged))relief.gameObject.SetActive(departing||bandaged);
                    if((departing||bandaged)&&head!=null){relief.position=head.position;relief.rotation=Root.rotation;}
                }
            }
        }
    }

    /// <summary>Routes use the open front aisle and central corridor, never a straight line through furniture.</summary>
    internal sealed class ClinicRoute
    {
        // Two 0.30m visitor bodies fit inside the existing door jambs. Keep the
        // direction choice tied to graph junctions, not a retargeted visible pose.
        private const float NorthLane=1.02f,SouthLane=.30f;
        private const float WaitingEastLane=.24f,WaitingWestLane=.91f;
        private readonly Vector3[] points=new Vector3[64];
        private int count;
        private float length;
        internal float Length=>length;
        internal void Build(ClinicWorld world,string from,string to,bool staff,Vector3? visibleStart=null)
        {
            count=0;length=0;var start=visibleStart??world.GetAnchorPoint(from);var end=world.GetAnchorPoint(to);Add(start);
            if(from==to&&(end-start).sqrMagnitude<.000001f){Add(end);return;}
            if(world.Location==ClinicLocation.DoctorsClinic){DoctorsClinicRoute.Build(world,from,to,staff,start,Add);return;}
            bool fromDesk=Starts(from,"reception.desk."),toDesk=Starts(to,"reception.desk.");
            bool fromQueue=Starts(from,"reception.queue."),toQueue=Starts(to,"reception.queue.");
            bool fromSeat=Starts(from,"waiting.seat."),toSeat=Starts(to,"waiting.seat.");
            bool fromCare=Starts(from,"firstaid.station."),toCare=Starts(to,"firstaid.station.");
            bool fromParking=Starts(from,"parking.bay."),toParking=Starts(to,"parking.bay.");
            bool fromAmenity=Starts(from,"waiting.toilet.")||Starts(from,"waiting.vending."),toAmenity=Starts(to,"waiting.toilet.")||Starts(to,"waiting.vending.");
            float deskAisle=staff?-1.15f:-4.05f;
            float fromJunction=fromCare?1.03f:fromSeat||fromAmenity?WaitingWestLane:fromDesk?deskAisle:fromParking?-6.90f:world.GetAnchorPoint(from).z;
            float toJunction=toCare?1.03f:toSeat||toAmenity?WaitingEastLane:toDesk?deskAisle:toParking?-6.90f:end.z;
            float lane=toJunction>=fromJunction?NorthLane:SouthLane;
            if(fromQueue&&toQueue)
            {
                if((start.z<-5.13f)!=(end.z<-5.13f))
                { Add(new Vector3(lane,start.y,start.z));Add(new Vector3(lane,end.y,end.z)); }
                Add(end);return;
            }
            if(fromParking)
            {
                // A queue may advance during the walk from a parked car. Rejoin the
                // remaining walkway from the visible position, without walking back
                // to the bay or repeating its first southward step.
                float crossingZ=world.GetAnchorPoint(from).z-.58f;
                if(start.x<-6.651f)
                { Add(new Vector3(start.x,start.y,crossingZ));Add(new Vector3(-6.65f,start.y,crossingZ)); }
                if(start.x<=-6.649f)
                { Add(new Vector3(-6.65f,start.y,-6.90f));Add(new Vector3(.67f,start.y,-6.90f)); }
                else if(start.z<-5.13f&&start.x<.67f)Add(new Vector3(.67f,start.y,-6.90f));
                else Add(new Vector3(lane,start.y,start.z));
            }
            else if(fromAmenity)
            {
                if(Starts(from,"waiting.toilet.")){Add(new Vector3(start.x,start.y,5.65f));Add(new Vector3(3.40f,start.y,5.65f));}
                else {Add(new Vector3(start.x,start.y,-3.42f));Add(new Vector3(3.40f,start.y,-3.42f));}
                if(!toSeat){Add(new Vector3(3.40f,start.y,WaitingWestLane));Add(new Vector3(lane,start.y,WaitingWestLane));}
            }
            else if(fromDesk) { float z=staff?-1.15f:-4.05f;Add(new Vector3(start.x,start.y,z));Add(new Vector3(lane,start.y,z)); }
            else if(fromSeat) { Add(new Vector3(3.40f,start.y,start.z));if(!toAmenity){Add(new Vector3(3.40f,start.y,WaitingWestLane));Add(new Vector3(lane,start.y,WaitingWestLane));} }
            else if(fromCare) { Add(new Vector3(start.x,start.y,1.03f));Add(new Vector3(lane,start.y,1.03f)); }
            else if(fromQueue && !toDesk)
            { Add(new Vector3(lane,start.y,start.z));Add(new Vector3(lane,start.y,-4.05f)); }
            else if(!fromQueue)Add(new Vector3(lane,start.y,start.z));
            if(toParking)
            { Add(new Vector3(lane,end.y,-6.90f));Add(new Vector3(-6.65f,end.y,-6.90f));Add(new Vector3(-6.65f,end.y,end.z-.58f));Add(new Vector3(end.x,end.y,end.z-.58f)); }
            else if(toAmenity)
            {
                if(!fromSeat){Add(new Vector3(lane,end.y,WaitingEastLane));Add(new Vector3(3.40f,end.y,WaitingEastLane));}
                if(Starts(to,"waiting.toilet.")){Add(new Vector3(3.40f,end.y,5.65f));Add(new Vector3(end.x,end.y,5.65f));}
                else {Add(new Vector3(3.40f,end.y,-3.42f));Add(new Vector3(end.x,end.y,-3.42f));}
            }
            else if(toDesk)
            {
                float z=staff?-1.15f:-4.05f;
                if(!fromQueue)Add(new Vector3(lane,end.y,z));
                else if(start.z<-5.13f)
                { Add(new Vector3(lane,start.y,start.z));Add(new Vector3(lane,end.y,z)); }
                else Add(new Vector3(start.x,end.y,z));
                Add(new Vector3(end.x,end.y,z));
            }
            else if(toSeat) { if(!fromAmenity){Add(new Vector3(lane,end.y,WaitingEastLane));Add(new Vector3(3.40f,end.y,WaitingEastLane));}Add(new Vector3(3.40f,end.y,end.z)); }
            else if(toCare) { Add(new Vector3(lane,end.y,1.03f));Add(new Vector3(end.x,end.y,1.03f)); }
            else if(toQueue) { Add(new Vector3(lane,end.y,end.z)); }
            else Add(new Vector3(lane,end.y,end.z));
            Add(end);
        }
        internal void BuildSavedPath(IList<ClinicMovementPoint> path,float y)
        {count=0;length=0;foreach(var point in path)Add(new Vector3(point.X,y,point.Z));}
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
