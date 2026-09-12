using System;
using System.Collections.Generic;
using IdleClinic.Core;
using IdleClinic.Presentation;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UIElements;

namespace IdleClinic.App
{
    public sealed partial class ClinicApp
    {
        private readonly List<AccessibleBinding> accessible=new List<AccessibleBinding>();
        private AccessibilityHierarchy accessibility;
        private AccessibilityNode walletNode,hintNode;
        private readonly Dictionary<AccessibilityNode,Rect> accessibilityFrames=new Dictionary<AccessibilityNode,Rect>();
        private bool accessibilityLayoutDirty;
        private double nextAccessibilityNotification;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool accessibilityQaOverride;
        private bool accessibilityQaDiagnosticLogged;
        private double accessibilityQaDiagnosticAt;
        private AssistiveSupport.ScreenReaderStatusOverride previousScreenReaderOverride;
#endif
        private sealed class AccessibleBinding { public VisualElement Element;public string Label;public Action Action;public Func<string> Value;public AccessibilityNode Node; }
        private void InitializeAccessibility()
        {
            accessibility=new AccessibilityHierarchy();
            walletNode=accessibility.AddNode("Clinic money");walletNode.role=AccessibilityRole.StaticText;
            walletNode.frameGetter=()=>AccessibleScreenFrame(wallet);
            hintNode=accessibility.AddNode("Clinic guidance");hintNode.role=AccessibilityRole.StaticText;
            hintNode.frameGetter=()=>AccessibleScreenFrame(hint);
            foreach(var binding in accessible)AddAccessibilityNode(binding);
            AssistiveSupport.screenReaderStatusChanged+=ScreenReaderStatusChanged;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Export the same native hierarchy to XCTest in development players without
            // changing the user's actual VoiceOver setting. Absent from release players.
            accessibilityQaDiagnosticAt=Time.unscaledTimeAsDouble+1;
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-clinic-accessibility-qa")>=0
                ||Environment.GetEnvironmentVariable("CLINIC_ACCESSIBILITY_QA")=="1")
            {
                previousScreenReaderOverride=AssistiveSupport.screenReaderStatusOverride;
                accessibilityQaOverride=true;
                AssistiveSupport.screenReaderStatusOverride=AssistiveSupport.ScreenReaderStatusOverride.ForceEnabled;
                Debug.Log("Clinic accessibility QA export enabled");
            }
#endif
            UpdateAccessibilityValues();
            UpdateAccessibilityFrames();
            ScreenReaderStatusChanged(AssistiveSupport.isScreenReaderEnabled);
        }
        private void ScreenReaderStatusChanged(bool enabled)
        {
            // Unity drops activeHierarchy when the screen reader is off. Assigning it
            // once at launch cannot support users who enable VoiceOver later.
            if(!enabled||accessibility==null)return;
            UpdateAccessibilityValues();
            UpdateAccessibilityFrames();
            if(AssistiveSupport.activeHierarchy!=accessibility)
                AssistiveSupport.activeHierarchy=accessibility;
            accessibilityLayoutDirty=true;
        }
        private void RegisterAccessibleButton(VisualElement element,string label,Action action,Func<string> value=null)
        {
            if(element is Button)BindTouchCaptureLifecycle(element);
            var binding=new AccessibleBinding{Element=element,Label=label,Action=action,Value=value};accessible.Add(binding);
            if(accessibility!=null)AddAccessibilityNode(binding);
        }
        private void RegisterCashAccessibility(VisualElement marker,int deskId)
        {
            RegisterAccessibleButton(marker,"Collect reception "+(deskId+1)+" cash",()=>Collect(deskId),
                ()=>(State.ReceptionDesks.Find(d=>d.Id==deskId)?.Till??0)
                    .ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" coins");
        }
        private void AddAccessibilityNode(AccessibleBinding binding)
        {
            binding.Node=accessibility.AddNode(binding.Label);binding.Node.role=AccessibilityRole.Button;
            binding.Node.frameGetter=()=>AccessibleScreenFrame(binding.Element);
            if(binding.Value!=null)binding.Node.value=binding.Value();
            accessibilityLayoutDirty=true;
#if UNITY_EDITOR || (!UNITY_IOS && !UNITY_ANDROID)
            // Mobile activation already synthesizes a center tap. Calling Action here
            // as well would purchase twice or toggle a setting straight back off.
            // Desktop screen readers require this direct action instead.
            binding.Node.invoked+=()=>
            {
                if(!IsAccessible(binding.Element)||!binding.Element.enabledInHierarchy)return false;
                binding.Action();return true;
            };
#endif
        }
        private bool IsAccessible(VisualElement element)
        {
            if(element==null||element.panel==null||element.resolvedStyle.display==DisplayStyle.None
                ||element.resolvedStyle.visibility==Visibility.Hidden)return false;
            for(var p=element.parent;p!=null;p=p.parent)
                if(p.resolvedStyle.display==DisplayStyle.None||p.resolvedStyle.visibility==Visibility.Hidden)return false;
            var bounds=element.worldBound;
            if(!FiniteRect(bounds)||bounds.width<=0||bounds.height<=0||!bounds.Overlaps(root.worldBound))return false;
            // These nodes represent world points, not floating UI controls. They
            // cannot be hit when an opaque control covers their actual tap center.
            if(overlay!=null&&overlay.Contains(element)&&WorldPointIsCovered(bounds.center))return false;
            // Mobile accessibility activates by tapping the node center. Match the
            // cash/plot priority in WorldTap so a room label cannot invoke another action.
            if((roomTargets.ContainsValue(element)||objectHits.ContainsKey(element))&&RoomPointHasHigherPriorityAction(bounds.center))return false;
            if(roomTargets.ContainsValue(element)&&TryPickManagementTarget(bounds.center,out _))return false;
            if(world!=null&&board!=null&&objectHits.TryGetValue(element,out var expected))
            {
                var actual=world.Pick(ToViewport(bounds.center));
                if(actual.Kind!=expected.Kind||actual.Id!=expected.Id)return false;
            }
            return true;
        }
        private bool RoomPointHasHigherPriorityAction(Vector2 point)
        {
            foreach(var marker in cashMarkers.Values)if(CoversWorldPoint(marker,point))return true;
            return CoversWorldPoint(vendingCashMarker,point)||CoversWorldPoint(waitingMarker,point);
        }
        private bool WorldPointIsCovered(Vector2 point)
        {
            if(CoversWorldPoint(dock,point)||CoversWorldPoint(hintLabel,point)||CoversWorldPoint(toast,point))return true;
            // The header and camera row themselves are transparent. Only their
            // visible children block the world, leaving gaps available for taps.
            if(header!=null)foreach(var control in header.Children())if(CoversWorldPoint(control,point))return true;
            if(cameraTools!=null)foreach(var control in cameraTools.Children())if(CoversWorldPoint(control,point))return true;
            return false;
        }
        private static bool CoversWorldPoint(VisualElement cover,Vector2 point)
        {
            if(cover==null||cover.panel==null)return false;
            for(var current=cover;current!=null;current=current.parent)
                if(current.resolvedStyle.display==DisplayStyle.None||current.resolvedStyle.visibility==Visibility.Hidden)return false;
            var bounds=cover.worldBound;
            return FiniteRect(bounds)&&bounds.width>0&&bounds.height>0&&bounds.Contains(point);
        }
        private Rect ScreenFrame(VisualElement element)
        {
            if(element==null||root==null)return Rect.zero;
            var b=element.worldBound;var area=root.worldBound;
            if(!FiniteRect(b)||!FiniteRect(area)||area.width<=0||area.height<=0)return Rect.zero;
            // Accessibility uses a top-left screen origin, like UI Toolkit. Convert
            // panel units to actual screen pixels; Unity converts those to native points.
            return new Rect((b.x-area.x)/area.width*Screen.width,(b.y-area.y)/area.height*Screen.height,
                b.width/area.width*Screen.width,b.height/area.height*Screen.height);
        }
        private Rect AccessibleScreenFrame(VisualElement element)
            =>IsAccessible(element)?ScreenFrame(element):Rect.zero;
        private static bool FiniteRect(Rect rect)
        {
            return !float.IsNaN(rect.x)&&!float.IsInfinity(rect.x)&&!float.IsNaN(rect.y)&&!float.IsInfinity(rect.y)
                &&!float.IsNaN(rect.width)&&!float.IsInfinity(rect.width)&&!float.IsNaN(rect.height)&&!float.IsInfinity(rect.height);
        }
        private void UpdateAccessibilityNode(AccessibilityNode node,VisualElement element)
        {
            var active=IsAccessible(element);
            if(node.isActive!=active){node.isActive=active;accessibilityLayoutDirty=true;}
            node.state=active&&element.enabledInHierarchy?AccessibilityState.None:AccessibilityState.Disabled;
            // Inactive virtual nodes can remain queryable by native automation. Do
            // not leave their old physical hit point over an unrelated room/control.
            var frame=active?ScreenFrame(element):Rect.zero;
            if(!accessibilityFrames.TryGetValue(node,out var previous)||previous!=frame)
            {
                node.frame=frame;accessibilityFrames[node]=frame;accessibilityLayoutDirty=true;
            }
        }
        private void UpdateAccessibilityFrames()
        {
            if(accessibility==null)return;
            UpdateAccessibilityNode(walletNode,wallet);
            UpdateAccessibilityNode(hintNode,hint);
            for(var i=accessible.Count-1;i>=0;i--)
            {
                var binding=accessible[i];
                if(binding.Element.panel==null)
                {
                    if(binding.Node!=null){accessibility.RemoveNode(binding.Node);accessibilityFrames.Remove(binding.Node);}
                    accessible.RemoveAt(i);accessibilityLayoutDirty=true;continue;
                }
                UpdateAccessibilityNode(binding.Node,binding.Element);
            }
            // Menus, markers and camera motion mutate the live hierarchy. Coalesce
            // invalidations so screen readers see them without a notification every frame.
            if(accessibilityLayoutDirty&&AssistiveSupport.activeHierarchy==accessibility
                &&Time.unscaledTimeAsDouble>=nextAccessibilityNotification)
            {
                AssistiveSupport.notificationDispatcher.SendLayoutChanged(null);
                accessibilityLayoutDirty=false;
                nextAccessibilityNotification=Time.unscaledTimeAsDouble+.2;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!accessibilityQaDiagnosticLogged&&Time.unscaledTimeAsDouble>=accessibilityQaDiagnosticAt)
            {
                accessibilityQaDiagnosticLogged=true;
                var activeNodes=0;
                foreach(var node in accessibility.rootNodes)if(node.isActive)activeNodes++;
                Debug.Log("Clinic accessibility QA state: requested="+accessibilityQaOverride
                    +", override="+AssistiveSupport.screenReaderStatusOverride
                    +", reader="+AssistiveSupport.isScreenReaderEnabled
                    +", activeHierarchy="+(AssistiveSupport.activeHierarchy==accessibility)
                    +", nodes="+accessibility.rootNodes.Count+", activeNodes="+activeNodes);
            }
#endif
        }
        private void UpdateAccessibilityValues()
        {
            if(accessibility==null)return;
            walletNode.value=State.Wallet.ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" coins";
            hintNode.value=hintLabel.text;
            foreach(var binding in accessible)
            {
                binding.Node.state=IsAccessible(binding.Element)&&binding.Element.enabledInHierarchy?AccessibilityState.None:AccessibilityState.Disabled;
                if(binding.Value!=null){binding.Node.value=binding.Value();continue;}
                var preferenceValue=binding.Element.Q<Label>(className:"preference-value");
                if(preferenceValue!=null)binding.Node.value=preferenceValue.text;
            }
        }
        private void DisposeAccessibility()
        {
            AssistiveSupport.screenReaderStatusChanged-=ScreenReaderStatusChanged;
            if(accessibility!=null && AssistiveSupport.activeHierarchy==accessibility)AssistiveSupport.activeHierarchy=null;
            accessibility?.Clear();accessible.Clear();accessibilityFrames.Clear();
            accessibility=null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(accessibilityQaOverride)
            {
                AssistiveSupport.screenReaderStatusOverride=previousScreenReaderOverride;
                accessibilityQaOverride=false;
            }
#endif
        }
    }
}
