using System;
using LittleLifeline.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LittleLifeline.Presentation
{
    /// <summary>The playable miniature railway. UI placement and simulation stay outside this class.</summary>
    public sealed class LifelineWorld : MonoBehaviour
    {
        private const float FloorHeight = .78f;
        private const int MaximumTextureDimension = 1536;
        private static readonly Vector3 CameraOffset = new Vector3(-3,23,-17);
        private static readonly Quaternion CameraRotation = Quaternion.LookRotation(-CameraOffset, Vector3.up);
        private static readonly Bounds[] OverviewFootprints =
        {
            new Bounds(new Vector3(0,1,-.7f),new Vector3(3,3,20)),
            new Bounds(new Vector3(-2.8f,.4f,-.7f),new Vector3(3.3f,.8f,20)),
            new Bounds(new Vector3(3,.9f,-3.8f),new Vector3(3.8f,3.8f,4.4f)),
            new Bounds(new Vector3(3,1.7f,3.1f),new Vector3(3.8f,4.1f,5.2f))
        };
        private readonly Plane floor = new Plane(Vector3.up, new Vector3(0,FloorHeight,0));
        private readonly CarriageView[] carriages = new CarriageView[4];
        private LifelineArt art;
        private LifelineActors actors;
        private LifelineTownView townView;
        private Transform selection;
        private Transform scenery;
        private Transform overviewTrees;
        private bool initialized;
        private bool reducedMotion;
        private bool cameraSettled;
        private Vector3 cameraTarget, cameraCurrent, targetVelocity;
        private float cameraSize, desiredSize, sizeVelocity;
        private float lastAspect;

        public Camera SceneCamera { get; private set; }
        public RenderTexture SceneTexture { get; private set; }
        public int SelectedSlot { get; private set; } = -1;

        public static float SlotZ(double slot) => -4.65f + (float)slot * 3.80f;
        public Vector3 CarriageCenter(int slot) => new Vector3(0,FloorHeight,SlotZ(Mathf.Clamp(slot,0,3)));

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            art = new LifelineArt();
            scenery = new GameObject("Railway diorama").transform;
            scenery.SetParent(transform,false);
            var cameraObject = new GameObject("Lifeline camera");
            cameraObject.transform.SetParent(transform,false);
            SceneCamera = cameraObject.AddComponent<Camera>();
            SceneCamera.orthographic = true;
            SceneCamera.transform.rotation = CameraRotation;
            SceneCamera.clearFlags = CameraClearFlags.SolidColor;
            SceneCamera.backgroundColor = LifelinePalette.Color("Paper","base");
            SceneCamera.cullingMask = 1 << LifelineArt.Layer;
            SceneCamera.nearClipPlane = .1f;
            SceneCamera.farClipPlane = 100;
            SceneCamera.allowHDR = false;
            SceneCamera.allowMSAA = true;
            ConfigureLight();
            BuildStation();
            townView = new LifelineTownView(art,scenery);
            for (int slot = 0; slot < 4; slot++) carriages[slot] = new CarriageView(art,scenery,slot);
            var engine = art.Model("Locomotive",scenery,new Vector3(0,0,-8.35f));
            engine.transform.localRotation = Quaternion.Euler(0,180,0);
            selection = new GameObject("Selected carriage brass outline").transform;
            selection.SetParent(scenery,false);
            foreach (float x in new[] {-1.31f,1.31f}) art.Box("Selected rail",selection,new Vector3(x,.82f,0),new Vector3(.045f,.04f,3.47f),"Brass");
            foreach (float z in new[] {-1.735f,1.735f}) art.Box("Selected end",selection,new Vector3(0,.82f,z),new Vector3(2.66f,.04f,.045f),"Brass");
            selection.gameObject.SetActive(false);
            actors = new LifelineActors(art,scenery);
            SetRenderSize(512,768);
            Reframe(true);
        }

        public RenderTexture SetRenderSize(int width, int height)
        {
            Initialize();
            width = Mathf.Max(1,width); height = Mathf.Max(1,height);
            float scale = Mathf.Min(1,MaximumTextureDimension/(float)Mathf.Max(width,height));
            width = Mathf.Max(1,Mathf.RoundToInt(width*scale)); height = Mathf.Max(1,Mathf.RoundToInt(height*scale));
            if (SceneTexture != null && SceneTexture.width == width && SceneTexture.height == height)
            {
                if (!SceneTexture.IsCreated()) SceneTexture.Create();
                return SceneTexture;
            }
            ReleaseTexture();
            var descriptor = new RenderTextureDescriptor(width,height,RenderTextureFormat.ARGB32,16)
                { msaaSamples = 2, sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear };
            descriptor.msaaSamples = Mathf.Max(1,SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
            SceneTexture = new RenderTexture(descriptor) { name = "Little Lifeline board", filterMode = FilterMode.Bilinear };
            SceneTexture.Create();
            SceneCamera.targetTexture = SceneTexture;
            Reframe(true);
            return SceneTexture;
        }

        public void Render(SimulationState state, bool reducedMotion = false)
        {
            Initialize();
            this.reducedMotion = reducedMotion;
            if (state == null) return;
            if (Mathf.Abs(lastAspect - SceneTexture.width/(float)SceneTexture.height) > .001f) Reframe(true);
            for (int slot = 0; slot < 4; slot++)
            {
                CarriageState room = null;
                for (int i = 0; i < state.Carriages.Count; i++)
                    if (state.Carriages[i].Slot == slot) { room = state.Carriages[i]; break; }
                carriages[slot].Render(room,state);
            }
            actors.Render(state,reducedMotion);
            townView.Render(state);
            MoveCamera();
        }

        public void SetLivery(string id)
        {
            Initialize();
            art.SetLivery(id == "sunrise" || id == "coastal" || id == "heritage" ? id : "base");
        }

        public int PickCarriage(Vector2 viewportUV)
        {
            if (!initialized || viewportUV.x < 0 || viewportUV.x > 1 || viewportUV.y < 0 || viewportUV.y > 1) return -1;
            var ray = SceneCamera.ViewportPointToRay(viewportUV);
            if (!floor.Raycast(ray,out float distance)) return -1;
            var hit = ray.GetPoint(distance);
            if (Mathf.Abs(hit.x) > 1.40f) return -1;
            for (int slot = 0; slot < 4; slot++)
                if (Mathf.Abs(hit.z - SlotZ(slot)) <= 1.76f) return slot;
            return -1;
        }

        public void Focus(int slot, bool reducedMotion = false)
        {
            Initialize();
            SelectedSlot = slot >= 0 && slot < 4 ? slot : -1;
            this.reducedMotion = reducedMotion;
            selection.gameObject.SetActive(SelectedSlot >= 0);
            if (SelectedSlot >= 0) selection.localPosition = new Vector3(0,0,SlotZ(SelectedSlot));
            overviewTrees.gameObject.SetActive(SelectedSlot < 0);
            townView.SetFocused(SelectedSlot >= 0);
            Reframe(reducedMotion);
        }

        private void Reframe(bool immediate)
        {
            if (SceneTexture == null) return;
            lastAspect = SceneTexture.width/(float)SceneTexture.height;
            SceneCamera.aspect = lastAspect;
            var inverse = Quaternion.Inverse(CameraRotation);
            if (SelectedSlot < 0)
            {
                // Fit occupied footprints: a single large box includes empty diagonal corners and makes the train too small.
                var projected = new Bounds(inverse * OverviewFootprints[0].center,Vector3.zero);
                for (int i=0;i<OverviewFootprints.Length;i++) EncapsulateProjected(OverviewFootprints[i],inverse,ref projected);
                cameraTarget = CameraRotation * projected.center;
                desiredSize = Mathf.Max(projected.extents.y/.84f,projected.extents.x/(lastAspect*.94f));
            }
            else
            {
                var bounds = new Bounds(new Vector3(-.1f,1.1f,SlotZ(SelectedSlot)),new Vector3(3.8f,2.1f,4.9f));
                cameraTarget = bounds.center;
                var projected = new Bounds(Vector3.zero,Vector3.zero);
                EncapsulateProjected(new Bounds(Vector3.zero,bounds.size),inverse,ref projected);
                desiredSize = Mathf.Max(projected.extents.y,projected.extents.x/lastAspect)*1.07f;
            }
            cameraSettled = false;
            if (immediate)
            {
                cameraCurrent = cameraTarget; cameraSize = desiredSize;
                targetVelocity = Vector3.zero; sizeVelocity = 0;
                ApplyCamera(); cameraSettled = true;
            }
        }

        private static void EncapsulateProjected(Bounds bounds,Quaternion inverse,ref Bounds projected)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                        projected.Encapsulate(inverse*(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
        }

        private void MoveCamera()
        {
            if (cameraSettled) return;
            if (reducedMotion) { cameraCurrent = cameraTarget; cameraSize = desiredSize; }
            else
            {
                cameraCurrent = Vector3.SmoothDamp(cameraCurrent,cameraTarget,ref targetVelocity,.22f,100,Time.unscaledDeltaTime);
                cameraSize = Mathf.SmoothDamp(cameraSize,desiredSize,ref sizeVelocity,.22f,100,Time.unscaledDeltaTime);
            }
            ApplyCamera();
            if ((cameraCurrent-cameraTarget).sqrMagnitude < .00001f && Mathf.Abs(cameraSize-desiredSize) < .001f) cameraSettled = true;
        }

        private void ApplyCamera()
        {
            // Raise the selected treatment room in the image, clear of the lower action dock.
            var dockOffset = SelectedSlot >= 0 ? SceneCamera.transform.up * (cameraSize * .16f) : Vector3.zero;
            SceneCamera.transform.position = cameraCurrent + CameraOffset - dockOffset;
            SceneCamera.orthographicSize = cameraSize;
        }

        private void BuildStation()
        {
            art.Box("Meadow",scenery,new Vector3(-1.5f,-.21f,0),new Vector3(14,.35f,27),"Ground");
            for (int i = -1; i <= 4; i++)
            {
                art.Model("Rail",scenery,new Vector3(0,0,SlotZ(i)));
                art.Model("Platform",scenery,new Vector3(-2.93f,0,SlotZ(i)));
            }
            var station = art.Model("Station",scenery,new Vector3(-6.15f,0,6.7f));
            station.transform.localRotation = Quaternion.Euler(0,90,0);
            for (int i = 0; i < 5; i++)
            {
                var bench = art.Model("Bench",scenery,new Vector3(-3.85f,.45f,-5.8f+i*3.7f),.82f);
                bench.transform.localRotation = Quaternion.Euler(0,90,0);
                art.Model("Lamp",scenery,new Vector3(-4.13f,.46f,-7.1f+i*3.7f),.82f);
            }
            overviewTrees = new GameObject("Overview trees").transform;
            overviewTrees.SetParent(scenery,false);
            for (int i = 0; i < 4; i++)
                art.Model("Tree",overviewTrees,new Vector3(-6.6f,0,-8.4f+i*5.2f),.80f+(i%3)*.14f);
        }

        private void ConfigureLight()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.86f,.84f,.76f);
            RenderSettings.ambientEquatorColor = new Color(.59f,.61f,.54f);
            RenderSettings.ambientGroundColor = new Color(.36f,.31f,.23f);
            var item = new GameObject("Afternoon key light");
            item.transform.SetParent(transform,false);
            item.transform.rotation = Quaternion.Euler(48,-38,0);
            var light = item.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.15f;
            light.color = new Color(1,.94f,.81f);
            light.cullingMask = 1 << LifelineArt.Layer;
            light.shadows = LightShadows.Soft; light.shadowStrength = .40f;
            light.shadowBias = .03f; light.shadowNormalBias = .18f;
        }

        private void ReleaseTexture()
        {
            if (SceneTexture == null) return;
            if (SceneCamera != null) SceneCamera.targetTexture = null;
            SceneTexture.Release(); LifelineArt.Destroy(SceneTexture); SceneTexture = null;
        }
        private void OnDestroy() { ReleaseTexture(); art?.Dispose(); }
    }

    internal sealed class CarriageView
    {
        private readonly LifelineArt art;
        private readonly Transform root;
        private readonly GameObject shell, empty, signal;
        private readonly GameObject[] interiors = new GameObject[3];
        private RoomKind? shownKind;

        internal CarriageView(LifelineArt art, Transform parent, int slot)
        {
            this.art = art;
            root = new GameObject("Carriage slot " + slot).transform;
            root.SetParent(parent,false); root.localPosition = new Vector3(0,0,LifelineWorld.SlotZ(slot));
            shell = art.Model("Carriage",root,Vector3.zero);
            empty = new GameObject("Future carriage"); empty.transform.SetParent(root,false);
            art.Box("Flatbed",empty.transform,new Vector3(0,.57f,0),new Vector3(2.30f,.25f,3.28f),"Wood");
            art.Box("Folded equipment crate",empty.transform,new Vector3(.25f,.94f,.20f),new Vector3(.85f,.48f,.85f),"Oat");
            for (int i = -1; i <= 1; i++) art.Box("Crate strap",empty.transform,new Vector3(.25f,.94f,.20f+i*.3f),new Vector3(.88f,.50f,.04f),"Brass");
            signal = art.Orb("Work status lamp",root,new Vector3(-1.24f,1.03f,-1.28f),new Vector3(.13f,.13f,.13f),"LeafLight");
            empty.SetActive(false);
        }

        internal void Render(CarriageState room, SimulationState state)
        {
            bool occupied = room != null;
            shell.SetActive(occupied); empty.SetActive(!occupied); signal.SetActive(occupied);
            if (shownKind != room?.Kind)
            {
                for (int i = 0; i < interiors.Length; i++) if (interiors[i] != null) interiors[i].SetActive(false);
                shownKind = room?.Kind;
                if (occupied)
                {
                    int index = (int)room.Kind;
                    if (interiors[index] == null) interiors[index] = art.Model(room.Kind.ToString(),root,Vector3.zero);
                    interiors[index].SetActive(true);
                }
            }
            if (!occupied) return;
            bool treating = false;
            for (int i = 0; i < state.Patients.Count; i++)
                if (state.Patients[i].RoomId == room.Id && state.Patients[i].Phase == PatientPhase.Treating) { treating = true; break; }
            signal.GetComponent<MeshRenderer>().sharedMaterial = art.Material(treating ? "Flower" : "LeafLight");
        }
    }
}
