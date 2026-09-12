using System;
using System.Collections.Generic;
using System.Linq;
using IdleClinic.Core;
using IdleClinic.Presentation;
using IdleClinic.Services;
using UnityEngine;
using UnityEngine.UIElements;
using PointerType = UnityEngine.UIElements.PointerType;

namespace IdleClinic.App
{
    public static class ClinicSelectionPolicy
    {
        // Clear floor between the desks and queue, away from both payment targets.
        public static Vector3 ReceptionFloorPoint=>new Vector3(-2.8f,.14f,-3.8f);
        public static bool CanSelectObject(ClinicState state,ClinicHit hit)
        {
            if(state.Tutorial!=ClinicTutorialStep.Complete)return false;
            switch(hit.Kind)
            {
                case ClinicHitKind.Desk:return state.ReceptionDesks.Any(d=>d.Id==hit.Id);
                case ClinicHitKind.Station:return state.TreatmentStations.Any(s=>s.Id==hit.Id);
                case ClinicHitKind.DoctorStation:return state.ConsultationStations.Any(s=>s.Id==hit.Id);
                case ClinicHitKind.PharmacyStation:return state.PharmacyStations.Any(s=>s.Id==hit.Id);
                case ClinicHitKind.Taxi:return state.Location==ClinicLocation.DoctorsClinic;
                case ClinicHitKind.Parking:return true;
                case ClinicHitKind.Toilet:
                case ClinicHitKind.Vending:return state.Room(ClinicRoom.Waiting).Built;
                default:return false;
            }
        }
        public static bool CanSelectRoom(ClinicState state,ClinicRoom room)
            =>state!=null&&state.Room(room)!=null&&CanSelectRoom(state.Tutorial,room);
        public static bool CanSelectRoom(ClinicTutorialStep tutorial,ClinicRoom room)
        {
            if(!Enum.IsDefined(typeof(ClinicRoom),room))return false;
            return tutorial==ClinicTutorialStep.Complete
                ||tutorial==ClinicTutorialStep.HireFirstNurse&&room==ClinicRoom.FirstAid;
        }
    }

    public static class ClinicScrollPolicy
    {
        public static bool ShouldPan(bool preciseSource,bool shift,bool zoomModifier)
            =>shift||preciseSource&&!zoomModifier;
    }

    /// <summary>Tracks the complete touch sequence, including fingers that began over controls.</summary>
    public sealed class ClinicTouchArbiter
    {
        private readonly Dictionary<int,Vector2> pointers=new Dictionary<int,Vector2>();
        private readonly Dictionary<int,Vector2> controlOrigins=new Dictionary<int,Vector2>();
        private readonly HashSet<int> draggedControls=new HashSet<int>();
        private bool multipleTouches,worldParticipated;
        public bool BlocksControlActivations=>multipleTouches;
        public bool RoutesToWorld=>multipleTouches&&worldParticipated;
        public KeyValuePair<int,Vector2>[] ActivePointers=>pointers.ToArray();
        public void Begin(int id,Vector2 position,bool beganOnWorld)
        {
            if(pointers.Count==0){multipleTouches=false;worldParticipated=false;}
            pointers[id]=position;worldParticipated|=beganOnWorld;
            if(!beganOnWorld)controlOrigins[id]=position;
            if(pointers.Count>1)multipleTouches=true;
        }
        public void Move(int id,Vector2 position)
        {
            if(!pointers.ContainsKey(id))return;
            pointers[id]=position;
            if(controlOrigins.TryGetValue(id,out var origin)&&Vector2.Distance(origin,position)>=ClinicGesture.DragThreshold)
                draggedControls.Add(id);
        }
        public bool End(int id)
        {
            var consume=(multipleTouches||draggedControls.Contains(id))&&pointers.ContainsKey(id);
            pointers.Remove(id);controlOrigins.Remove(id);draggedControls.Remove(id);
            if(pointers.Count==0){multipleTouches=false;worldParticipated=false;}
            return consume;
        }
        public void Cancel(){pointers.Clear();controlOrigins.Clear();draggedControls.Clear();multipleTouches=false;worldParticipated=false;}
    }

    /// <summary>Observe releases at the capture target: Unity skips its ancestors during captured dispatch.</summary>
    public static class ClinicTouchCaptureLifecycle
    {
        public static void Bind(VisualElement target,ClinicTouchArbiter arbiter,ClinicGesture gesture,
            ISet<int> capturedPointers,Action cancel)
        {
            target.RegisterCallback<PointerMoveEvent>(e=>
            {
                if(e.pointerType==PointerType.touch)arbiter.Move(e.pointerId,e.position);
            },TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(e=>
            {
                if(e.pointerType!=PointerType.touch)return;
                arbiter.Move(e.pointerId,e.position);
                if(!arbiter.End(e.pointerId))return;
                gesture.End(e.pointerId,e.position);
                capturedPointers.Remove(e.pointerId);
                (target.panel?.GetCapturingElement(e.pointerId) as VisualElement)?.ReleasePointer(e.pointerId);
                // Keep both releases away from Clickable, including the last finger.
                e.StopImmediatePropagation();
            },TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(e=>
            {
                if(e.pointerType!=PointerType.touch)return;
                cancel();e.StopImmediatePropagation();
            },TrickleDown.TrickleDown);
        }
    }

    /// <summary>A release is a tap only if the entire gesture remained a single, still pointer.</summary>
    public sealed class ClinicGesture
    {
        public const float DragThreshold=8;
        private readonly Dictionary<int,Vector2> pointers=new Dictionary<int,Vector2>();
        private Vector2 origin;
        private bool cancelledTap;
        public int PointerCount=>pointers.Count;
        public bool IsDragging=>cancelledTap;
        public void Begin(int id,Vector2 p)
        {
            if(pointers.Count==0){origin=p;cancelledTap=false;}
            pointers[id]=p;if(pointers.Count>1)cancelledTap=true;
        }
        public bool Move(int id,Vector2 p,out Vector2 from,out Vector2 to,out float zoom)
        {
            from=to=p;zoom=1;
            if(!pointers.ContainsKey(id))return false;
            var previous=pointers[id];
            // Additional fingers keep taps cancelled but do not change the camera's
            // pinch pair until there are only two fingers again.
            if(pointers.Count>2){pointers[id]=p;cancelledTap=true;return false;}
            if(pointers.Count>1)
            {
                var other=pointers.First(v=>v.Key!=id).Value;
                from=(previous+other)/2;to=(p+other)/2;
                var before=Vector2.Distance(previous,other);var after=Vector2.Distance(p,other);
                if(before>4 && after>4)zoom=before/after;
                pointers[id]=p;cancelledTap=true;return true;
            }
            pointers[id]=p;
            if(!cancelledTap && Vector2.Distance(origin,p)<DragThreshold)return false;
            from=cancelledTap?previous:origin;to=p;cancelledTap=true;return true;
        }
        public bool End(int id,Vector2 p)
        {
            var tap=pointers.ContainsKey(id)&&pointers.Count==1&&!cancelledTap&&Vector2.Distance(origin,p)<DragThreshold;
            pointers.Remove(id);return tap;
        }
        public void Cancel(){pointers.Clear();cancelledTap=true;}
    }

    public sealed partial class ClinicApp
    {
        private readonly ClinicGesture gesture=new ClinicGesture();
        private readonly ClinicTouchArbiter touchArbiter=new ClinicTouchArbiter();
        private readonly HashSet<int> capturedPointers=new HashSet<int>();
        private readonly List<CoinFlight> flights=new List<CoinFlight>();
        private readonly Stack<ClinicIcon> coinPool=new Stack<ClinicIcon>();
        private double walletPulseUntil;
        private bool wideWorldMarkers;
        private sealed class CoinFlight { public ClinicIcon Icon; public Vector2 Start; public float Age,Delay,Arc; }

        private void BindWorldInput()
        {
            ClinicPlatformInput.Initialize();
            BindRootTouchArbitration();
            BindTouchCaptureLifecycle(board);
            board.RegisterCallback<PointerDownEvent>(e=>
            {
                if(e.button!=0 || !ready)return;
                gesture.Begin(e.pointerId,e.position);board.CapturePointer(e.pointerId);capturedPointers.Add(e.pointerId);e.StopPropagation();
            });
            board.RegisterCallback<PointerMoveEvent>(e=>
            {
                if(!ready)return;
                if(gesture.Move(e.pointerId,e.position,out var from,out var to,out var zoom))
                {
                    world.Pan(ToViewport(from),ToViewport(to));
                    if(Mathf.Abs(zoom-1)>.0001f)world.Zoom(zoom,ToViewport(to));
                    e.StopPropagation();
                }
            });
            board.RegisterCallback<PointerUpEvent>(e=>
            {
                if(!ready)return;
                var tap=gesture.End(e.pointerId,e.position);
                capturedPointers.Remove(e.pointerId);
                if(board.HasPointerCapture(e.pointerId))board.ReleasePointer(e.pointerId);
                if(tap)WorldTap(e.position);
                e.StopPropagation();
            });
            board.RegisterCallback<PointerCancelEvent>(_=>CancelWorldGesture());
            board.RegisterCallback<PointerCaptureOutEvent>(e=>
            {
                if(capturedPointers.Contains(e.pointerId))CancelWorldGesture();
            });
            board.RegisterCallback<WheelEvent>(e=>
            {
                if(!ready)return;
                var uv=ToViewport(e.mousePosition);
                if(ClinicScrollPolicy.ShouldPan(ClinicPlatformInput.IsPreciseScroll,e.shiftKey,e.ctrlKey||e.commandKey))
                    world.Pan(uv,uv+new Vector2(-e.delta.x,e.delta.y)*.008f);
                else world.Zoom(Mathf.Exp(e.delta.y*.055f),uv);
                e.StopPropagation();
            });
        }
        private void BindRootTouchArbitration()
        {
            BindTouchCaptureLifecycle(root);
            root.RegisterCallback<PointerDownEvent>(e=>
            {
                if(e.pointerType!=PointerType.touch||!ready)return;
                var target=e.target as VisualElement;
                touchArbiter.Begin(e.pointerId,e.position,target==board||(target!=null&&board.Contains(target)));
                if(!touchArbiter.BlocksControlActivations)return;

                // Taking capture from the first button cancels its pending Clickable
                // press. The second down is stopped before any other button sees it.
                // Mixed world/control pinches still feed both fingers to the camera.
                foreach(var pointer in touchArbiter.ActivePointers)
                {
                    if(touchArbiter.RoutesToWorld)
                    {
                        gesture.Begin(pointer.Key,pointer.Value);
                        capturedPointers.Add(pointer.Key);
                        board.CapturePointer(pointer.Key);
                    }
                    else root.CapturePointer(pointer.Key);
                }
                e.StopImmediatePropagation();
            },TrickleDown.TrickleDown);
        }
        private void BindTouchCaptureLifecycle(VisualElement target)
            =>ClinicTouchCaptureLifecycle.Bind(target,touchArbiter,gesture,capturedPointers,CancelWorldGesture);
        private void CancelWorldGesture()
        {
            gesture.Cancel();
            var ids=capturedPointers.Concat(touchArbiter.ActivePointers.Select(p=>p.Key)).Distinct().ToArray();
            capturedPointers.Clear();touchArbiter.Cancel();
            foreach(var id in ids)(root?.panel?.GetCapturingElement(id) as VisualElement)?.ReleasePointer(id);
        }
        private Vector2 ToViewport(Vector2 panelPoint)
        {
            var p=board.WorldToLocal(panelPoint);var size=board.contentRect.size;
            return new Vector2(p.x/Mathf.Max(1,size.x),1-p.y/Mathf.Max(1,size.y));
        }
        private Vector2 OverlayPoint(Vector2 uv)
        {
            var local=new Vector2(uv.x*board.contentRect.width,(1-uv.y)*board.contentRect.height);
            return overlay.WorldToLocal(board.LocalToWorld(local));
        }
        private void WorldTap(Vector2 panelPoint)
        {
            foreach(var pair in cashMarkers)
                if(pair.Value.style.display==DisplayStyle.Flex&&pair.Value.worldBound.Contains(panelPoint)){Collect(pair.Key);return;}
            if(vendingCashMarker!=null&&vendingCashMarker.style.display==DisplayStyle.Flex&&vendingCashMarker.worldBound.Contains(panelPoint)){CollectVending();return;}
            if(waitingMarker!=null&&waitingMarker.style.display==DisplayStyle.Flex&&waitingMarker.worldBound.Contains(panelPoint)){Select(ClinicRoom.Waiting);return;}
            var hit=world.Pick(ToViewport(panelPoint));
            if(hit.Kind!=ClinicHitKind.Cash&&hit.Kind!=ClinicHitKind.VendingCash
                &&TryPickManagementTarget(panelPoint,out var expandedTarget))hit=expandedTarget;
            switch(hit.Kind)
            {
                case ClinicHitKind.Cash:Collect(hit.Id);break;
                case ClinicHitKind.VendingCash:CollectVending();break;
                case ClinicHitKind.Desk:
                case ClinicHitKind.Station:
                case ClinicHitKind.DoctorStation:
                case ClinicHitKind.PharmacyStation:
                    if(State.Tutorial==ClinicTutorialStep.HireFirstNurse&&hit.Kind==ClinicHitKind.Station)Select(ClinicRoom.FirstAid);
                    else SelectObject(hit);
                    break;
                case ClinicHitKind.Parking:
                case ClinicHitKind.Toilet:
                case ClinicHitKind.Vending:
                case ClinicHitKind.Taxi:SelectObject(hit);break;
                case ClinicHitKind.Reception:Select(ClinicRoom.Reception);break;
                case ClinicHitKind.Treatment:Select(ClinicRoom.FirstAid);break;
                case ClinicHitKind.Consultation:Select(ClinicRoom.Consultation);break;
                case ClinicHitKind.Pharmacy:Select(ClinicRoom.Pharmacy);break;
                case ClinicHitKind.Waiting:
                case ClinicHitKind.Expansion:Select(ClinicRoom.Waiting);break;
                default:CloseContext();break;
            }
        }

        private void UpdateWorldMarkers()
        {
            wideWorldMarkers=ClinicMarkerPresentation.IsWide(world.SceneCamera.orthographicSize,wideWorldMarkers);
            foreach(var room in State.Rooms)
            {
                if(!roomTargets.TryGetValue(room.Kind,out var target))
                {
                    target=new VisualElement{pickingMode=PickingMode.Ignore};target.style.position=Position.Absolute;
                    target.style.width=44;target.style.height=44;overlay.Add(target);roomTargets.Add(room.Kind,target);
                    var kind=room.Kind;RegisterAccessibleButton(target,"Select "+RoomName(kind),()=>Select(kind));
                }
                var point=room.Kind==ClinicRoom.Reception&&State.Location==ClinicLocation.StarterClinic?ClinicSelectionPolicy.ReceptionFloorPoint:world.GetRoomPoint(room.Kind);
                PositionMarker(target,world.WorldToViewport(point),(room.Built||State.WaitingRoomUnlocked)
                    &&ClinicSelectionPolicy.CanSelectRoom(State,room.Kind));
            }
            var compactReception=wideWorldMarkers||ReceptionCashIsCrowded();
            foreach(var desk in State.ReceptionDesks)
            {
                if(!cashMarkers.TryGetValue(desk.Id,out var marker))
                {
                    marker=Box(overlay,"cash-marker");marker.pickingMode=PickingMode.Ignore;
                    marker.Add(new ClinicIcon(ClinicGlyph.Coin,19,new Color(.46f,.28f,.07f)));
                    Text(marker,"","cash-amount",true);cashMarkers.Add(desk.Id,marker);
                    RegisterCashAccessibility(marker,desk.Id);
                }
                marker.Q<Label>().text=Money(desk.Till);
                ClinicMarkerPresentation.CashDetail(marker,compactReception);
                PositionMarker(marker,world.WorldToViewport(world.GetCashPoint(desk.Id)),desk.Till>0,-30,
                    compactReception?new Vector2(44,44):(Vector2?)null);
            }
            SeparateReceptionCashMarkers(compactReception);
            UpdateManagementMarkers();
            var activeIds=new HashSet<int>();
            foreach(var patient in State.Patients)
            {
                if(!ClinicServicePresentation.HasProgress(patient.Phase))continue;
                activeIds.Add(patient.Id);
                if(!patientRings.TryGetValue(patient.Id,out var ring))
                {
                    ring=new ClinicProgress(28);ring.AddToClassList("patient-ring");overlay.Add(ring);patientRings.Add(patient.Id,ring);
                }
                ClinicMarkerPresentation.PatientDetail(ring,wideWorldMarkers);
                var point=world.GetAnchorPoint(patient.ToAnchor)+Vector3.up*1.65f;
                PositionMarker(ring,world.WorldToViewport(point),true);
                ring.Progress=(State.Tick-patient.PhaseStartedTick)/(float)Math.Max(1,patient.PhaseEndsTick-patient.PhaseStartedTick);
            }
            foreach(var id in patientRings.Keys.Where(id=>!activeIds.Contains(id)).ToArray()){patientRings[id].RemoveFromHierarchy();patientRings.Remove(id);}
            var jobs=new HashSet<int>();
            foreach(var job in State.Construction)
            {
                jobs.Add(job.Id);
                if(!constructionMarkers.TryGetValue(job.Id,out var marker))
                {
                    marker=Box(overlay,"construction-marker");marker.pickingMode=PickingMode.Ignore;
                    marker.Add(new ClinicProgress(26));Text(marker,"","construction-clock",true);constructionMarkers.Add(job.Id,marker);
                }
                ClinicMarkerPresentation.ConstructionDetail(marker,wideWorldMarkers);
                PositionMarker(marker,world.WorldToViewport(world.GetRoomPoint(job.Room)),true,-16,
                    wideWorldMarkers?new Vector2(26,26):(Vector2?)null);
                marker.Q<ClinicProgress>().Progress=(State.Tick-job.StartedTick)/(float)Math.Max(1,job.EndsTick-job.StartedTick);
                marker.Q<Label>().text=TimeLabel((job.EndsTick-State.Tick)/(double)ClinicRules.TicksPerSecond);
            }
            foreach(var id in constructionMarkers.Keys.Where(id=>!jobs.Contains(id)).ToArray()){constructionMarkers[id].RemoveFromHierarchy();constructionMarkers.Remove(id);}
            if(waitingMarker==null)
            {
                waitingMarker=Box(overlay,"waiting-marker");waitingMarker.pickingMode=PickingMode.Ignore;
                waitingMarker.Add(new ClinicIcon(ClinicGlyph.Chair,26));Text(waitingMarker,"160","waiting-cost",true);
                RegisterAccessibleButton(waitingMarker,"Build waiting room, 160 coins",()=>Select(ClinicRoom.Waiting));
            }
            var waitingUV=world.WorldToViewport(world.GetAnchorPoint("waiting.progress"));
            waitingUV=new Vector2(Mathf.Clamp(waitingUV.x,.13f,.87f),Mathf.Clamp(waitingUV.y,.30f,.80f));
            PositionMarker(waitingMarker,waitingUV,
                State.WaitingRoomUnlocked&&!State.Room(ClinicRoom.Waiting).Built&&!State.Construction.Any(c=>c.Room==ClinicRoom.Waiting));
        }

        private bool ReceptionCashIsCrowded()
        {
            var points=State.ReceptionDesks.Where(d=>d.Till>0).Select(d=>OverlayPoint(world.WorldToViewport(world.GetCashPoint(d.Id))))
                .Where(p=>overlay.contentRect.Contains(p)).ToArray();
            for(var i=0;i<points.Length;i++)for(var j=i+1;j<points.Length;j++)
                if(Mathf.Abs(points[i].x-points[j].x)<80&&Mathf.Abs(points[i].y-points[j].y)<48)return true;
            return false;
        }
        private void SeparateReceptionCashMarkers(bool compact)
        {
            if(!compact)return;
            var desks=State.ReceptionDesks.Where(d=>cashMarkers.TryGetValue(d.Id,out var marker)&&marker.style.display==DisplayStyle.Flex)
                .OrderBy(d=>d.Id).ToArray();
            // Recompute from the real counters every frame. No previously displaced coordinate is reused.
            var points=desks.Select(d=>OverlayPoint(world.WorldToViewport(world.GetCashPoint(d.Id)))+new Vector2(0,-30)).ToArray();
            var placed=ClinicMarkerPresentation.SeparateCashTargets(points,overlay.contentRect);
            for(var i=0;i<desks.Length;i++)
            {
                var marker=cashMarkers[desks[i].Id];
                marker.style.left=placed[i].x-ClinicMarkerPresentation.CashSize/2;
                marker.style.top=placed[i].y-ClinicMarkerPresentation.CashSize/2;
            }
        }

        private void PositionMarker(VisualElement marker,Vector2 uv,bool visible,float offsetY=0,Vector2? fixedSize=null)
        {
            visible&=uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1;
            marker.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
            if(!visible)return;
            var p=OverlayPoint(uv);
            var width=fixedSize?.x??marker.resolvedStyle.width;var height=fixedSize?.y??marker.resolvedStyle.height;
            if(float.IsNaN(width))width=40;if(float.IsNaN(height))height=30;
            marker.style.left=p.x-width/2;marker.style.top=p.y-height/2+offsetY;
        }

        private void LaunchCoins(int deskId,long amount)
            =>LaunchCoinsFrom(world.GetCashPoint(deskId));
        private void LaunchCoinsFrom(Vector3 sourcePoint)
        {
            if(world==null||particles==null)return;
            walletPulseUntil=Time.unscaledTimeAsDouble+.65;
            if(ReducedMotion)return;
            var source=OverlayPoint(world.WorldToViewport(sourcePoint));
            // Four reception tills plus vending may be collected within one flight window.
            var count=Math.Min(7,35-flights.Count);
            for(var i=0;i<count;i++)
            {
                var icon=coinPool.Count>0?coinPool.Pop():new ClinicIcon(ClinicGlyph.Coin,22,new Color(.67f,.42f,.06f));
                icon.style.position=Position.Absolute;icon.style.left=0;icon.style.top=0;icon.style.opacity=1;
                particles.Add(icon);
                flights.Add(new CoinFlight{Icon=icon,Start=source,Age=0,Delay=i*.035f,Arc=(i%2==0?1:-1)*(30+i*7)});
            }
        }
        private void UpdateFlights(float delta)
        {
            if(wallet==null)return;
            var remaining=(float)Math.Max(0,walletPulseUntil-Time.unscaledTimeAsDouble);
            wallet.style.scale=new Scale(Vector3.one*(ReducedMotion?1:1+.035f*remaining/.65f));
            var target=overlay.WorldToLocal(wallet.LocalToWorld(new Vector2(26,wallet.contentRect.height/2)));
            for(var i=flights.Count-1;i>=0;i--)
            {
                var flight=flights[i];flight.Age+=delta;
                var t=Mathf.Clamp01((flight.Age-flight.Delay)/.6f);
                var eased=1-Mathf.Pow(1-t,3);
                var p=Vector2.Lerp(flight.Start,target,eased)+new Vector2(Mathf.Sin(t*Mathf.PI)*flight.Arc,-Mathf.Sin(t*Mathf.PI)*30);
                flight.Icon.style.translate=new Translate(p.x-11,p.y-11);
                flight.Icon.style.scale=new Scale(Vector3.one*(1-.25f*t));
                if(t>=1||ReducedMotion){flight.Icon.RemoveFromHierarchy();coinPool.Push(flight.Icon);flights.RemoveAt(i);}
            }
        }
    }
}
