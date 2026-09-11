using System;
using System.Collections.Generic;
using LittleLifeline.Core;
using UnityEngine;

namespace LittleLifeline.Presentation
{
    /// <summary>Reuses visual actors; positions and work poses follow the simulation clock.</summary>
    internal sealed class LifelineActors
    {
        private readonly LifelineArt art;
        private readonly Transform parent;
        private readonly Dictionary<int, Actor> patients = new Dictionary<int, Actor>();
        private readonly Dictionary<int, Actor> crew = new Dictionary<int, Actor>();
        private readonly Stack<Actor> sparePatients = new Stack<Actor>();
        private readonly Stack<Actor> spareCrew = new Stack<Actor>();
        private readonly HashSet<int> live = new HashSet<int>();
        private readonly List<int> expired = new List<int>();
        private readonly int[] queueCounts = new int[5];

        internal LifelineActors(LifelineArt art, Transform parent)
        {
            this.art = art; this.parent = parent;
            for (int i = 0; i < 8; i++) sparePatients.Push(Create(false));
            for (int i = 0; i < 4; i++) spareCrew.Push(Create(true));
        }

        internal void Render(SimulationState state, bool reducedMotion)
        {
            double now = state.Tick + state.SubTick;
            live.Clear();
            for (int i = 0; i < state.Patients.Count; i++) live.Add(state.Patients[i].Id);
            Retire(patients,sparePatients);
            Array.Clear(queueCounts,0,queueCounts.Length);
            for (int i = 0; i < state.Patients.Count; i++)
            {
                PatientState patient = state.Patients[i];
                Actor actor = Obtain(patients,sparePatients,patient.Id,false);
                Vector3 position, direction = Vector3.forward;
                bool walking = patient.Phase == PatientPhase.Walking || patient.Phase == PatientPhase.Departing;
                Quaternion pose = Quaternion.identity;
                bool working = patient.Phase == PatientPhase.Treating;
                if (patient.Phase == PatientPhase.Waiting)
                {
                    // Keep residents at their actual waiting endpoint between care stages.
                    int bucket = Mathf.Clamp((int)patient.FromSlot+1,0,4);
                    int waiting = queueCounts[bucket]++;
                    int column = waiting / 4;
                    int row = waiting % 4;
                    float queueZ = bucket == 0 ? -6.15f : LifelineWorld.SlotZ(bucket-1)-.85f;
                    position = new Vector3(-2.10f-column*.42f,.45f,queueZ+row*.53f);
                    direction = Vector3.back;
                }
                else if (walking)
                {
                    Vector3 from = DoorPosition(patient.FromSlot,false);
                    Vector3 to = DoorPosition(patient.ToSlot,false);
                    if (patient.Phase == PatientPhase.Departing) to = new Vector3(-4.85f,.45f,-7.7f);
                    float progress = Progress(now,patient.PhaseStartedTick,patient.PhaseEndsTick);
                    position = WalkRoute(from,to,progress,out direction);
                }
                else
                {
                    CarriageState room = FindRoom(state,patient.RoomId);
                    int slot = room != null ? room.Slot : Mathf.Clamp((int)patient.ToSlot,0,3);
                    float z = LifelineWorld.SlotZ(slot);
                    if (room != null && room.Kind == RoomKind.Diagnostics)
                    {
                        // FBX import mirrors the authored X axis: the mattress center is at -.22.
                        position = new Vector3(-.22f,1.31f,z+.50f);
                        pose = Quaternion.Euler(-90,0,0);
                    }
                    else if (room != null && room.Kind == RoomKind.Recovery)
                    {
                        // The imported pillows and headboards are toward negative X.
                        position = new Vector3(.30f,1.43f,z+(patient.Id%2==0 ? -.76f : .76f));
                        pose = Quaternion.Euler(-90,0,90);
                    }
                    else
                    {
                        position = new Vector3(.62f,.89f,z+.80f);
                        direction = Vector3.left;
                    }
                }
                actor.Apply(position,direction,pose,walking,working,reducedMotion,now*.1,patient.Id);
            }
            live.Clear();
            for (int i = 0; i < state.Crew.Count; i++) live.Add(state.Crew[i].Id);
            Retire(crew,spareCrew);
            for (int i = 0; i < state.Crew.Count; i++)
            {
                CrewState person = state.Crew[i];
                Actor actor = Obtain(crew,spareCrew,person.Id,true);
                bool walking = person.MoveEndsTick > now && person.MoveEndsTick > person.MoveStartedTick;
                bool working = person.TaskPatientId >= 0 && !walking;
                Vector3 position, direction;
                if (walking)
                    position = WalkRoute(DoorPosition(person.FromSlot,true),DoorPosition(person.ToSlot,true),
                        Progress(now,person.MoveStartedTick,person.MoveEndsTick),out direction);
                else
                {
                    var room = FindRoom(state,person.CurrentRoomId);
                    double slot = room != null ? room.Slot : person.PositionSlot;
                    if (slot < 0)
                    {
                        position = new Vector3(-3.50f,.45f,-6.2f+i*.57f);
                        direction = new Vector3(1,0,.18f);
                    }
                    else
                    {
                        // Imported care furniture occupies negative X; the positive aisle
                        // clears both the scanner ring and the recovery mattress edges.
                        float careZ = room != null && room.Kind == RoomKind.Recovery && working
                            ? (person.TaskPatientId%2==0 ? -.76f : .76f) : -.59f;
                        position = new Vector3(.84f,.79f,LifelineWorld.SlotZ(slot)+careZ);
                        direction = Vector3.left;
                    }
                }
                actor.Apply(position,direction,Quaternion.identity,walking,working,reducedMotion,now*.1,person.Id);
            }
        }

        private static CarriageState FindRoom(SimulationState state, int id)
        {
            for (int i = 0; i < state.Carriages.Count; i++) if (state.Carriages[i].Id == id) return state.Carriages[i];
            return null;
        }

        private static Vector3 DoorPosition(double slot, bool crew)
        {
            if (slot < 0) return new Vector3(crew ? -3.3f : -2.15f,.45f,-6.2f);
            return new Vector3(-.67f,.79f,LifelineWorld.SlotZ(slot)+(crew ? -.59f : .55f));
        }

        private static float Progress(double now, long start, long end) =>
            end <= start ? 1 : Mathf.Clamp01((float)((now-start)/(end-start)));

        private static Vector3 WalkRoute(Vector3 from, Vector3 to, float progress, out Vector3 direction)
        {
            var turnA = new Vector3(-1.64f,.45f,from.z);
            var turnB = new Vector3(-1.64f,.45f,to.z);
            float first = Vector3.Distance(from,turnA), middle = Vector3.Distance(turnA,turnB), last = Vector3.Distance(turnB,to);
            float distance = progress * (first+middle+last);
            if (distance < first && first > .001f) { direction = turnA-from; return Vector3.Lerp(from,turnA,distance/first); }
            if (distance < first+middle && middle > .001f) { direction = turnB-turnA; return Vector3.Lerp(turnA,turnB,(distance-first)/middle); }
            direction = to-turnB;
            return Vector3.Lerp(turnB,to,last > .001f ? (distance-first-middle)/last : 1);
        }

        private Actor Obtain(Dictionary<int,Actor> active, Stack<Actor> spare, int id, bool worker)
        {
            if (active.TryGetValue(id,out Actor actor)) return actor;
            actor = spare.Count > 0 ? spare.Pop() : Create(worker);
            actor.Root.name = (worker ? "Crew " : "Resident ") + id;
            active.Add(id,actor); actor.Root.gameObject.SetActive(true);
            return actor;
        }

        private Actor Create(bool worker)
        {
            var model = art.Model(worker ? "Crew" : "Resident",parent,Vector3.zero,worker ? .84f : .81f);
            var actor = new Actor(model.transform);
            model.SetActive(false);
            return actor;
        }

        private void Retire(Dictionary<int,Actor> active, Stack<Actor> spare)
        {
            expired.Clear();
            foreach (int id in active.Keys) if (!live.Contains(id)) expired.Add(id);
            for (int i = 0; i < expired.Count; i++)
            {
                var actor = active[expired[i]];
                actor.Root.gameObject.SetActive(false);
                spare.Push(actor); active.Remove(expired[i]);
            }
        }

        private sealed class Actor
        {
            internal readonly Transform Root;
            private readonly Transform[] joints = new Transform[4];
            private readonly Quaternion[] rest = new Quaternion[4];
            internal Actor(Transform root)
            {
                Root = root;
                string[] names = {"Arm_L","Arm_R","Leg_L","Leg_R"};
                foreach (var part in root.GetComponentsInChildren<Transform>())
                    for (int i = 0; i < names.Length; i++)
                        if (part.name.StartsWith(names[i],StringComparison.Ordinal)) { joints[i] = part; rest[i] = part.localRotation; }
            }
            internal void Apply(Vector3 position, Vector3 direction, Quaternion pose, bool walking, bool working, bool reducedMotion, double seconds, int id)
            {
                Root.localPosition = position;
                direction.y = 0;
                Root.localRotation = pose != Quaternion.identity ? pose : direction.sqrMagnitude > .001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
                float cycle = reducedMotion ? 0 : Mathf.Sin((float)seconds*(walking ? 9 : 3.0f)+id*.6f);
                for (int i = 0; i < joints.Length; i++)
                {
                    if (joints[i] == null) continue;
                    float angle = walking ? cycle*(i%2==0 ? 22 : -22) : working && i<2 ? -16+cycle*5 : 0;
                    joints[i].localRotation = rest[i]*Quaternion.AngleAxis(angle,Vector3.right);
                }
            }
        }
    }
}
