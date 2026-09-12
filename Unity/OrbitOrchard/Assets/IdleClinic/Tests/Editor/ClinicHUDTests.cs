using IdleClinic.App;
using IdleClinic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Accessibility;
using System.Collections.Generic;
using System.Linq;

namespace IdleClinic.Tests
{
    public sealed class ClinicHUDTests
    {
        [Test]
        public void HiddenCashNodeClearsItsPreviousHitFrameAndStaysZeroWhenUnityRefreshesFrames()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                var oldFrame=new Rect(104,354,60.5f,44);
                ax.Node.frame=oldFrame;
                ax.Marker.style.display=DisplayStyle.None;
                ax.UpdateNode();
                Assert.That(ax.Node.isActive,Is.False);
                Assert.That(ax.Node.state,Is.EqualTo(AccessibilityState.Disabled));
                Assert.That(ax.Node.frame,Is.EqualTo(Rect.zero),"Hiding a previously visible stack must remove its stale native hit point.");
                Assert.That(ax.Node.frameGetter(),Is.EqualTo(Rect.zero));

                ax.Node.frame=oldFrame;
                ax.Hierarchy.RefreshNodeFrames();
                Assert.That(ax.Node.frame,Is.EqualTo(Rect.zero),"Unity's own frame refresh must not restore hidden geometry.");
                ax.UpdateValues();
                Assert.That(ax.Node.state,Is.EqualTo(AccessibilityState.Disabled),"Readout refresh cannot re-enable hidden nodes.");
                Assert.That(ax.Hierarchy.rootNodes.Contains(ax.Node),Is.True,"Visibility changes preserve the same hierarchy node.");
            }
        }
        [Test]
        public void CashAccessibilityValueTracksPaymentCollectionAndTheCurrentRestoredSimulation()
        {
            using(var ui=new CapturedTouchPanel())
            using(var ax=new CashAccessibilityFixture(ui.Root))
            {
                Assert.That(ax.Node.value,Is.EqualTo("0 coins"));
                ax.Simulation.Advance(60);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("50 coins"));
                Assert.That(ax.Simulation.Collect(0).Success,Is.True);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("0 coins"),"Collection clears the announced till even while its native node still exists.");

                var restored=ClinicSimulation.CreateNew();restored.State.ReceptionDesks[0].Till=20000;
                ax.ReplaceSimulation(restored);ax.UpdateValues();
                Assert.That(ax.Node.value,Is.EqualTo("20,000 coins"),"Read the current simulation, without the visible label's K/M abbreviation.");
            }
        }
        private sealed class CashAccessibilityFixture:System.IDisposable
        {
            public readonly VisualElement Marker=new VisualElement();
            public readonly AccessibilityHierarchy Hierarchy=new AccessibilityHierarchy();
            public readonly AccessibilityNode Node;
            public ClinicSimulation Simulation=ClinicSimulation.CreateNew();
            private readonly GameObject owner;
            private readonly ClinicApp app;
            private const System.Reflection.BindingFlags Private=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            public CashAccessibilityFixture(VisualElement root)
            {
                // Keep the component inactive so this narrow binding test does not
                // start the game, open saves, or initialize Apple services.
                owner=new GameObject("Clinic accessibility binding test");owner.SetActive(false);
                app=owner.AddComponent<ClinicApp>();root.Add(Marker);
                Set("root",root);Set("simulation",Simulation);Set("accessibility",Hierarchy);
                Set("walletNode",Hierarchy.AddNode("Wallet"));Set("hintNode",Hierarchy.AddNode("Hint"));Set("hintLabel",new Label());
                Invoke("RegisterCashAccessibility",Marker,0);
                var bindings=(System.Collections.IList)typeof(ClinicApp).GetField("accessible",Private).GetValue(app);
                var binding=bindings[0];
                Node=(AccessibilityNode)binding.GetType().GetField("Node").GetValue(binding);
            }
            private void Set(string field,object value)=>typeof(ClinicApp).GetField(field,Private).SetValue(app,value);
            private void Invoke(string method,params object[] args)=>typeof(ClinicApp).GetMethod(method,Private).Invoke(app,args);
            public void UpdateValues()=>Invoke("UpdateAccessibilityValues");
            public void UpdateNode()=>Invoke("UpdateAccessibilityNode",Node,Marker);
            public void ReplaceSimulation(ClinicSimulation simulation){Simulation=simulation;Set("simulation",simulation);}
            public void Dispose(){Invoke("DisposeAccessibility");Marker.RemoveFromHierarchy();Object.DestroyImmediate(owner);}
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CapturedDispatchFinishesMixedPinchBeforeTheNextControlTap(bool worldFirst)
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,worldFirst?ui.Board:ui.Control,worldFirst);
                ui.Begin(1,worldFirst?ui.Control:ui.Board,!worldFirst);
                foreach(var pointer in ui.Arbiter.ActivePointers)
                {
                    ui.Gesture.Begin(pointer.Key,pointer.Value);
                    ui.Captured.Add(pointer.Key);ui.Board.CapturePointer(pointer.Key);
                }
                var moved=new Vector2(260,200);
                using(var e=PointerMoveEvent.GetPooled(new Touch{fingerId=1,phase=TouchPhase.Moved,position=moved}))
                    ui.Root.SendEvent(e);
                Assert.That(ui.Arbiter.ActivePointers.Single(p=>p.Key==PointerId.touchPointerIdBase+1).Value,Is.EqualTo(moved));

                ui.End(0);ui.End(1);
                Assert.That(ui.AncestorReleases,Is.Zero,"Unity skips ancestor callbacks for captured dispatch; target listeners must clean up.");
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty,"Neither captured release may strand a finger in the next gesture.");
                Assert.That(ui.Gesture.PointerCount,Is.Zero);
                Assert.That(ui.Captured,Is.Empty);
                Assert.That(ui.ControlReleases,Is.Zero,"The transferred control must not receive a purchase release.");

                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1),"An independent control tap must reach its target after the pinch.");
                Assert.That(ui.Arbiter.BlocksControlActivations,Is.False);
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
            }
        }
        [Test]
        public void CapturedControlReleaseDoesNotLeaveAStaleFingerForTheNextWorldTap()
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1));
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
                ui.Begin(1,ui.Board,true);
                Assert.That(ui.Arbiter.BlocksControlActivations,Is.False,"A completed button tap cannot turn a later world tap into a pinch.");
                ui.End(1);
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
            }
        }
        [Test]
        public void CapturedCancellationClearsTheEntireTouchSequence()
        {
            using(var ui=new CapturedTouchPanel())
            {
                ui.Begin(0,ui.Board,true);ui.Begin(1,ui.Control,false);
                using(var e=PointerCancelEvent.GetPooled(new Touch{fingerId=0,phase=TouchPhase.Canceled,position=Vector2.one}))
                    ui.Root.SendEvent(e);
                Assert.That(ui.Arbiter.ActivePointers,Is.Empty);
                Assert.That(ui.Gesture.PointerCount,Is.Zero);
                Assert.That(ui.Captured,Is.Empty);
                ui.Begin(0,ui.Control,false);ui.End(0);
                Assert.That(ui.ControlReleases,Is.EqualTo(1));
            }
        }

        // These tests use a real runtime panel and PointerEvent dispatch. Pure arbiter
        // tests cannot expose Unity's deliberate omission of capture-target ancestors.
        private sealed class CapturedTouchPanel:System.IDisposable
        {
            public readonly ClinicTouchArbiter Arbiter=new ClinicTouchArbiter();
            public readonly ClinicGesture Gesture=new ClinicGesture();
            public readonly HashSet<int> Captured=new HashSet<int>();
            public readonly VisualElement Root,Board=new VisualElement();
            public readonly Button Control=new Button();
            public int AncestorReleases,ControlReleases;
            private readonly GameObject owner;
            private readonly PanelSettings settings;
            public CapturedTouchPanel()
            {
                settings=ScriptableObject.CreateInstance<PanelSettings>();
                owner=new GameObject("Clinic captured pointer test");
                var document=owner.AddComponent<UIDocument>();document.panelSettings=settings;
                Root=document.rootVisualElement;Root.Add(Board);Root.Add(Control);
                Assert.That(Root.panel,Is.Not.Null);
                ClinicTouchCaptureLifecycle.Bind(Root,Arbiter,Gesture,Captured,Cancel);
                ClinicTouchCaptureLifecycle.Bind(Board,Arbiter,Gesture,Captured,Cancel);
                ClinicTouchCaptureLifecycle.Bind(Control,Arbiter,Gesture,Captured,Cancel);
                Root.RegisterCallback<PointerUpEvent>(_=>AncestorReleases++,TrickleDown.TrickleDown);
                Control.RegisterCallback<PointerUpEvent>(_=>ControlReleases++,TrickleDown.TrickleDown);
            }
            public void Begin(int finger,VisualElement capture,bool world)
            {
                // Initialize Unity's pressed-button state as an actual touch down does.
                using(var e=PointerDownEvent.GetPooled(new Touch{fingerId=finger,phase=TouchPhase.Began,position=new Vector2(100+finger*100,200)}))
                {
                    Arbiter.Begin(e.pointerId,e.position,world);
                    if(world){Gesture.Begin(e.pointerId,e.position);Captured.Add(e.pointerId);}
                    capture.CapturePointer(e.pointerId);
                }
            }
            public void End(int finger)
            {
                using(var e=PointerUpEvent.GetPooled(new Touch{fingerId=finger,phase=TouchPhase.Ended,position=new Vector2(100+finger*100,200)}))
                    Root.SendEvent(e);
            }
            private void Cancel()
            {
                var ids=Arbiter.ActivePointers.Select(p=>p.Key).Concat(Captured).Distinct().ToArray();
                Arbiter.Cancel();Gesture.Cancel();Captured.Clear();
                foreach(var id in ids)(Root.panel?.GetCapturingElement(id) as VisualElement)?.ReleasePointer(id);
            }
            public void Dispose()
            {
                Cancel();
                // Release Unity's synthetic test touch state even when an assertion fails.
                for(var finger=0;finger<2;finger++)
                    using(var e=PointerUpEvent.GetPooled(new Touch{fingerId=finger,phase=TouchPhase.Ended})){}
                Object.DestroyImmediate(owner);Object.DestroyImmediate(settings);
            }
        }

        [TestCase(ClinicTutorialStep.FirstArrival,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.FirstArrival,ClinicRoom.FirstAid,false)]
        [TestCase(ClinicTutorialStep.FirstArrival,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.CollectFirstPayment,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.CollectFirstPayment,ClinicRoom.FirstAid,false)]
        [TestCase(ClinicTutorialStep.CollectFirstPayment,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.HireFirstNurse,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.HireFirstNurse,ClinicRoom.FirstAid,true)]
        [TestCase(ClinicTutorialStep.HireFirstNurse,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.FirstTreatment,ClinicRoom.Reception,false)]
        [TestCase(ClinicTutorialStep.FirstTreatment,ClinicRoom.FirstAid,false)]
        [TestCase(ClinicTutorialStep.FirstTreatment,ClinicRoom.Waiting,false)]
        [TestCase(ClinicTutorialStep.Complete,ClinicRoom.Reception,true)]
        [TestCase(ClinicTutorialStep.Complete,ClinicRoom.FirstAid,true)]
        [TestCase(ClinicTutorialStep.Complete,ClinicRoom.Waiting,true)]
        public void RoomSelectionPreservesTheRequiredTutorialAction(ClinicTutorialStep tutorial,ClinicRoom room,bool allowed)
        {
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(tutorial,room),Is.EqualTo(allowed));
        }
        [Test]
        public void RepeatedFirstPaymentCannotReplaceTheNurseActionWithReceptionControls()
        {
            var clinic=ClinicSimulation.CreateNew();clinic.Advance(60);
            Assert.That(clinic.State.Tutorial,Is.EqualTo(ClinicTutorialStep.CollectFirstPayment));
            Assert.That(clinic.Collect(0).Success,Is.True);
            Assert.That(clinic.State.Wallet,Is.EqualTo(50));
            Assert.That(clinic.State.Tutorial,Is.EqualTo(ClinicTutorialStep.HireFirstNurse));

            // The empty counter can pick its reception room on the second physical
            // tap. That pick must leave the already-open nurse action untouched.
            Assert.That(clinic.Collect(0).Success,Is.False);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(clinic.State.Tutorial,ClinicRoom.Reception),Is.False);
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(clinic.State.Tutorial,ClinicRoom.FirstAid),Is.True);
            Assert.That(clinic.HireNurse().Success,Is.True);
            clinic.Advance(60);
            Assert.That(clinic.State.Tutorial,Is.EqualTo(ClinicTutorialStep.Complete));
            Assert.That(ClinicSelectionPolicy.CanSelectRoom(clinic.State.Tutorial,ClinicRoom.Reception),Is.True);
        }
        [TestCase(true,true)]
        [TestCase(true,false)]
        [TestCase(false,true)]
        [TestCase(false,false)]
        public void WorldAndButtonTouchesBecomeACameraGestureAndConsumeBothReleases(bool worldFirst,bool worldReleasedFirst)
        {
            var arbiter=new ClinicTouchArbiter();var cameraGesture=new ClinicGesture();
            var first=new Vector2(100,200);var second=new Vector2(200,200);
            arbiter.Begin(1,first,worldFirst);
            Assert.That(arbiter.BlocksControlActivations,Is.False,"An ordinary first press must remain usable.");
            arbiter.Begin(2,second,!worldFirst);
            Assert.That(arbiter.BlocksControlActivations,Is.True,"A control press must be cancelled in either start order.");
            Assert.That(arbiter.RoutesToWorld,Is.True);

            foreach(var pointer in arbiter.ActivePointers)cameraGesture.Begin(pointer.Key,pointer.Value);
            Assert.That(cameraGesture.Move(2,new Vector2(250,200),out _,out _,out var zoom),Is.True);
            Assert.That(zoom,Is.EqualTo(2f/3).Within(.001),"The control-origin finger still participates in the pinch.");

            var worldId=worldFirst?1:2;var buttonId=worldFirst?2:1;
            var releasedFirst=worldReleasedFirst?worldId:buttonId;var releasedLast=worldReleasedFirst?buttonId:worldId;
            Assert.That(arbiter.End(releasedFirst),Is.True,"The root must stop the first release before a button can click.");
            Assert.That(cameraGesture.End(releasedFirst,Vector2.zero),Is.False);
            Assert.That(arbiter.BlocksControlActivations,Is.True,"Lifting one finger cannot re-enable pending purchases.");
            Assert.That(arbiter.End(releasedLast),Is.True,"The last release must also be stopped.");
            Assert.That(cameraGesture.End(releasedLast,Vector2.zero),Is.False);
            Assert.That(arbiter.BlocksControlActivations,Is.False);

            arbiter.Begin(3,Vector2.one,false);
            Assert.That(arbiter.End(3),Is.False,"The next independent button tap must reach Clickable normally.");
        }
        [Test]
        public void TwoControlTouchesCancelPurchasesWithoutMovingTheWorld()
        {
            var arbiter=new ClinicTouchArbiter();
            arbiter.Begin(1,Vector2.zero,false);arbiter.Begin(2,Vector2.one,false);
            Assert.That(arbiter.BlocksControlActivations,Is.True);
            Assert.That(arbiter.RoutesToWorld,Is.False);
            Assert.That(arbiter.End(1),Is.True);Assert.That(arbiter.End(2),Is.True);
        }
        [Test]
        public void AddingAnotherFingerBeforeTheLastReleaseKeepsPurchasesCancelled()
        {
            var arbiter=new ClinicTouchArbiter();
            arbiter.Begin(1,Vector2.zero,true);arbiter.Begin(2,Vector2.one,false);
            arbiter.End(1);arbiter.Begin(3,new Vector2(30,30),false);
            Assert.That(arbiter.RoutesToWorld,Is.True);
            Assert.That(arbiter.End(2),Is.True);Assert.That(arbiter.End(3),Is.True);
        }
        [Test]
        public void FocusLossClearsTouchesAcrossControlsAndWorld()
        {
            var arbiter=new ClinicTouchArbiter();
            arbiter.Begin(1,Vector2.zero,false);arbiter.Begin(2,Vector2.one,true);arbiter.Cancel();
            Assert.That(arbiter.ActivePointers,Is.Empty);
            Assert.That(arbiter.BlocksControlActivations,Is.False);
            arbiter.Begin(3,Vector2.one,false);Assert.That(arbiter.End(3),Is.False);
        }
        [Test]
        public void ScrollIntentDependsOnSourceAndModifiersNotMomentumSpeed()
        {
            Assert.That(ClinicScrollPolicy.ShouldPan(true,false,false),Is.True);
            Assert.That(ClinicScrollPolicy.ShouldPan(false,false,false),Is.False);
            Assert.That(ClinicScrollPolicy.ShouldPan(true,false,true),Is.False);
            Assert.That(ClinicScrollPolicy.ShouldPan(false,true,false),Is.True);
        }
        [Test]
        public void AStillReleaseRemainsATap()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,new Vector2(100,200));
            Assert.That(gesture.End(1,new Vector2(102,201)),Is.True);
        }
        [Test]
        public void PanningFromCashNeverCollectsEvenAfterReturningToStart()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,new Vector2(100,200));
            Assert.That(gesture.Move(1,new Vector2(120,200),out _,out _,out _),Is.True);
            gesture.Move(1,new Vector2(100,200),out _,out _,out _);
            Assert.That(gesture.End(1,new Vector2(100,200)),Is.False);
        }
        [Test]
        public void PinchKeepsBothFingerReleasesFromBecomingPurchases()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,new Vector2(100,200));gesture.Begin(2,new Vector2(200,200));
            Assert.That(gesture.Move(2,new Vector2(250,200),out var from,out var to,out var factor),Is.True);
            Assert.That(from,Is.EqualTo(new Vector2(150,200)));Assert.That(to,Is.EqualTo(new Vector2(175,200)));
            Assert.That(factor,Is.EqualTo(2f/3).Within(.001));
            Assert.That(gesture.End(2,new Vector2(250,200)),Is.False);
            Assert.That(gesture.End(1,new Vector2(100,200)),Is.False);
        }
        [Test]
        public void FocusLossCancelsPendingTapAndNextGestureWorks()
        {
            var gesture=new ClinicGesture();gesture.Begin(1,Vector2.one);gesture.Cancel();
            Assert.That(gesture.End(1,Vector2.one),Is.False);
            gesture.Begin(3,Vector2.one);Assert.That(gesture.End(3,Vector2.one),Is.True);
        }
        [Test]
        public void MovingBeforePointerDownCannotMoveTheCamera()
        {
            var gesture=new ClinicGesture();
            Assert.That(gesture.Move(9,Vector2.one,out _,out _,out _),Is.False);
            Assert.That(gesture.End(9,Vector2.one),Is.False);
        }
    }
}
