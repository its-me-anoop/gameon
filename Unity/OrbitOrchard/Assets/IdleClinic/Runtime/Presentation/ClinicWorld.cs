using System;
using System.Collections.Generic;
using IdleClinic.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace IdleClinic.Presentation
{
    public enum ClinicHitKind { None,Cash,Reception,Treatment,Waiting,Expansion,Desk,Station,Parking,Toilet,Vending,VendingCash }
    public readonly struct ClinicHit
    {
        public ClinicHitKind Kind { get; }
        public int Id { get; }
        public ClinicHit(ClinicHitKind kind,int id=0) { Kind=kind;Id=id; }
    }

    /// <summary>Persistent miniature hospital scene. Gesture ownership and financial effects live in the UI/core.</summary>
    public sealed class ClinicWorld : MonoBehaviour
    {
        private const float Floor=.14f;
        private static readonly Vector3 CameraOffset=new Vector3(1.2f,15,-12);
        private readonly Plane floor=new Plane(Vector3.up,new Vector3(0,Floor,0));
        private readonly Dictionary<string,Transform> anchors=new Dictionary<string,Transform>();
        private readonly GameObject[] desks=new GameObject[2],stations=new GameObject[2],seats=new GameObject[14];
        private readonly GameObject[,] cash=new GameObject[2,6];
        private readonly Transform[] roomRoots=new Transform[3];
        private readonly GameObject[,] tierDetails=new GameObject[3,2];
        private readonly GameObject[] renovations=new GameObject[3];
        private readonly long[] tills=new long[2];
        private static readonly string[] CashAnchors={"reception.desk.0.cash","reception.desk.1.cash"};
        private readonly ClinicRoomState[] roomState=new ClinicRoomState[3];
        private ClinicArt art;
        private ClinicActors actors;
        private ClinicUpgrades upgrades;
        private ClinicAmenities amenities;
        private ClinicStreetLife streetLife;
        private ClinicConstruction construction;
        private ClinicDoor careDoor,entranceDoor,waitingDoor,refreshmentDoor;
        private Transform scene,selection;
        private GameObject waitingClosed,receptionDivider,treatmentDivider;
        private Vector3 center=new Vector3(-2,0,-.65f),homeTarget,velocity;
        private float size=6.9f,homeSize,sizeVelocity;
        private bool initialized,homing,waitingBuilt,userCamera;

        public Camera SceneCamera { get; private set; }
        public RenderTexture Texture { get; private set; }
        public RenderTexture SceneTexture=>Texture;
        public Vector3 CashPoint=>GetCashPoint(0);

        public void Initialize()
        {
            if(initialized)return;initialized=true;art=new ClinicArt();scene=art.Group("Fixed hospital",transform);
            var camera=art.Group("Clinic camera",transform);SceneCamera=camera.gameObject.AddComponent<Camera>();
            SceneCamera.orthographic=true;SceneCamera.transform.rotation=Quaternion.LookRotation(-CameraOffset,Vector3.up);
            SceneCamera.clearFlags=CameraClearFlags.SolidColor;SceneCamera.backgroundColor=ClinicArt.Color("Meadow");
            SceneCamera.cullingMask=1<<ClinicArt.Layer;SceneCamera.nearClipPlane=.1f;SceneCamera.farClipPlane=100;
            SceneCamera.allowHDR=false;SceneCamera.allowMSAA=true;
            BuildArchitecture();BuildFurniture();BuildAnchors();BuildLighting();BuildPrivacy();ClinicSurroundings.Build(art,scene);ClinicFurnishings.Build(art,scene);
            actors=new ClinicActors(art,scene,this);upgrades=new ClinicUpgrades(art,scene);
            amenities=new ClinicAmenities(art,scene);streetLife=new ClinicStreetLife(art,scene);construction=new ClinicConstruction(art,scene);careDoor=new ClinicDoor(art,scene);entranceDoor=new ClinicDoor(art,scene,true);
            waitingDoor=new ClinicDoor(art,scene,position:new Vector3(1.67f,Floor,.50f),yaw:90,openingWidth:1.30f,name:"Waiting corridor doorway");
            refreshmentDoor=new ClinicDoor(art,scene,position:new Vector3(3.40f,Floor,-1.66f),openingWidth:1.30f,name:"Waiting refreshment doorway");
            SetRenderSize(393,852);Home(true);
        }

        public RenderTexture SetRenderSize(int width,int height)
        {
            Initialize();width=Mathf.Max(1,width);height=Mathf.Max(1,height);
            float factor=Mathf.Min(1,1536f/Mathf.Max(width,height));width=Mathf.Max(1,Mathf.RoundToInt(width*factor));height=Mathf.Max(1,Mathf.RoundToInt(height*factor));
            if(Texture!=null && Texture.width==width && Texture.height==height) { if(!Texture.IsCreated())Texture.Create();return Texture; }
            ReleaseTexture();var descriptor=new RenderTextureDescriptor(width,height,RenderTextureFormat.ARGB32,16){msaaSamples=2,sRGB=QualitySettings.activeColorSpace==ColorSpace.Linear};
            descriptor.msaaSamples=Mathf.Max(1,SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
            Texture=new RenderTexture(descriptor){name="Idle Clinic world",filterMode=FilterMode.Bilinear};Texture.Create();
            SceneCamera.targetTexture=Texture;SceneCamera.aspect=width/(float)height;
            if(!userCamera)Home(true);else {size=BoundedSize(size);ClampCenter();ApplyCamera();}return Texture;
        }

        public void Render(ClinicState state,float deltaTime,bool reducedMotion=false)
        {
            Initialize();if(state==null)return;
            for(int i=0;i<3;i++)roomState[i]=null;
            for(int i=0;i<state.Rooms.Count;i++)roomState[(int)state.Rooms[i].Kind]=state.Rooms[i];
            for(int i=0;i<2;i++)
            {
                ReceptionDeskState desk=null;for(int j=0;j<state.ReceptionDesks.Count;j++)if(state.ReceptionDesks[j].Id==i)desk=state.ReceptionDesks[j];
                desks[i].SetActive(desk!=null);tills[i]=desk==null?0:desk.Till;
                int stacks=tills[i]==0?0:Mathf.Clamp(1+(int)Math.Log10(Math.Max(1,tills[i])),1,6);
                for(int j=0;j<6;j++)cash[i,j].SetActive(j<stacks && desk!=null);
            }
            var treatment=roomState[1];int stationCount=treatment==null?1:treatment.StationCount;
            for(int i=0;i<2;i++)stations[i].SetActive(i<stationCount);
            receptionDivider.SetActive(desks[0].activeSelf&&desks[1].activeSelf);treatmentDivider.SetActive(stationCount>=2);
            var waiting=roomState[2];waitingBuilt=waiting!=null&&waiting.Built;
            waitingClosed.SetActive(!waitingBuilt);
            int capacity=waitingBuilt?Mathf.Clamp(4+2*(waiting.FacilitiesLevel-1),0,14):0;
            for(int i=0;i<14;i++)seats[i].SetActive(i<capacity);
            for(int i=0;i<3;i++)
            {
                var room=roomState[i];int tier=room==null?1:room.Tier;
                for(int j=0;j<2;j++)tierDetails[i,j].SetActive((i!=2||waitingBuilt)&&tier>=j+2);
                bool building=false;for(int j=0;j<state.Construction.Count;j++)if((int)state.Construction[j].Room==i)building=true;
                renovations[i].SetActive(building);
            }
            actors.Render(state,reducedMotion);upgrades.Render(state);amenities.Render(state,reducedMotion);streetLife.Render(state,reducedMotion);construction.Render(state,reducedMotion);careDoor.Render(actors,deltaTime,reducedMotion);entranceDoor.Render(actors,deltaTime,reducedMotion);waitingDoor.Render(actors,deltaTime,reducedMotion);refreshmentDoor.Render(actors,deltaTime,reducedMotion);
            if(homing)
            {
                if(reducedMotion) { center=homeTarget;size=homeSize; }
                else { center=Vector3.SmoothDamp(center,homeTarget,ref velocity,.22f,100,deltaTime);size=Mathf.SmoothDamp(size,homeSize,ref sizeVelocity,.22f,100,deltaTime); }
                if((center-homeTarget).sqrMagnitude<.00001f&&Mathf.Abs(size-homeSize)<.001f)homing=false;
                ApplyCamera();
            }
        }

        public void Home(bool immediate=false)
        {
            Initialize();FitHome(out homeTarget,out homeSize);userCamera=false;homing=!immediate;velocity=Vector3.zero;sizeVelocity=0;
            var previousCenter=center;float previousSize=size;
            center=homeTarget;size=BoundedSize(homeSize);ClampCenter();homeTarget=center;homeSize=size;
            if(!immediate){center=previousCenter;size=previousSize;}ApplyCamera();
        }
        public void Pan(Vector2 fromUV,Vector2 toUV)
        {
            if(!TryViewportToGround(fromUV,out var before)||!TryViewportToGround(toUV,out var after))return;
            homing=false;userCamera=true;center+=before-after;ClampCenter();ApplyCamera();
        }
        /// <param name="factor">Values below one zoom in. The anchor remains under the finger/cursor.</param>
        public void Zoom(float factor,Vector2 anchorUV)
        {
            if(float.IsNaN(factor)||float.IsInfinity(factor)||factor<=0||!TryViewportToGround(anchorUV,out var before))return;
            homing=false;userCamera=true;size=BoundedSize(size*factor);ApplyCamera();
            if(TryViewportToGround(anchorUV,out var after))center+=before-after;
            ClampCenter();ApplyCamera();
        }
        public bool TryViewportToGround(Vector2 uv,out Vector3 point)
        {
            Initialize();var ray=SceneCamera.ViewportPointToRay(uv);
            if(floor.Raycast(ray,out float distance)) { point=ray.GetPoint(distance);return true; }
            point=default;return false;
        }
        public Vector2 WorldToViewport(Vector3 point)
        { var p=SceneCamera.WorldToViewportPoint(point);return p.z>0?new Vector2(p.x,p.y):new Vector2(-10,-10); }
        public bool TryGetAnchorViewport(string name,out Vector2 uv)
        { if(!anchors.TryGetValue(name,out var anchor)){uv=default;return false;}uv=WorldToViewport(anchor.position);return uv.x>=0&&uv.x<=1&&uv.y>=0&&uv.y<=1; }
        public Vector3 GetCashPoint(int deskId)=>GetAnchorPoint(CashAnchors[Mathf.Clamp(deskId,0,1)]);
        public Vector3 GetDeskPoint(int id)=>desks[Mathf.Clamp(id,0,1)].transform.position+new Vector3(-.40f,1.26f,.02f);
        public Vector3 GetStationPoint(int id)=>stations[Mathf.Clamp(id,0,1)].transform.position+new Vector3(-.20f,.80f,.27f);
        public Vector3 GetAmenityPoint(ClinicAmenity kind)=>kind==ClinicAmenity.Parking?ClinicAmenities.ParkingPoint:
            kind==ClinicAmenity.Toilet?ClinicAmenities.ToiletPoint:ClinicAmenities.VendingPoint;
        public Vector3 GetVendingCashPoint()=>ClinicAmenities.VendingCashPoint;
        public Vector3 GetAnchorPoint(string name)=>anchors.TryGetValue(name??"",out var anchor)?anchor.position:new Vector3(.65f,Floor,-5.7f);
        internal Vector3 Facing(string name)=>anchors.TryGetValue(name??"",out var anchor)?anchor.forward:Vector3.forward;
        internal bool HasSeatAt(string name)
        {
            if(string.IsNullOrEmpty(name))return false;
            bool chair=name.StartsWith("waiting.seat.",StringComparison.Ordinal)||
                name.StartsWith("firstaid.station.",StringComparison.Ordinal)&&name.EndsWith(".patient",StringComparison.Ordinal);
            return chair&&anchors.TryGetValue(name,out var anchor)&&anchor.gameObject.activeInHierarchy;
        }
        public ClinicHit Pick(Vector2 uv)
        {
            if(uv.x<0||uv.x>1||uv.y<0||uv.y>1)return default;
            var ray=SceneCamera.ViewportPointToRay(uv);
            for(int i=0;i<2;i++)if(tills[i]>0&&new Bounds(GetCashPoint(i),new Vector3(.65f,.65f,.55f)).IntersectRay(ray))return new ClinicHit(ClinicHitKind.Cash,i);
            if(amenities.VendingTill>0&&new Bounds(GetVendingCashPoint(),new Vector3(.40f,.42f,.36f)).IntersectRay(ray))return new ClinicHit(ClinicHitKind.VendingCash);
            for(int i=0;i<2;i++)
            {
                if(desks[i].activeSelf&&new Bounds(GetDeskPoint(i),new Vector3(.56f,.50f,.48f)).IntersectRay(ray))return new ClinicHit(ClinicHitKind.Desk,i);
                if(stations[i].activeSelf&&new Bounds(GetStationPoint(i),new Vector3(.66f,.64f,.67f)).IntersectRay(ray))return new ClinicHit(ClinicHitKind.Station,i);
            }
            for(int i=0;i<3;i++)if(new Bounds(GetAmenityPoint((ClinicAmenity)i),i==0?new Vector3(1.2f,1.3f,.8f):new Vector3(.85f,.82f,.7f)).IntersectRay(ray))
                return new ClinicHit(i==0?ClinicHitKind.Parking:i==1?ClinicHitKind.Toilet:ClinicHitKind.Vending,i);
            if(!TryViewportToGround(uv,out var p))return default;
            if(p.x>=-14.5f&&p.x<=-6.15f&&p.z>=-5.1f&&p.z<=5.1f)return new ClinicHit(ClinicHitKind.Parking,(int)ClinicAmenity.Parking);
            if(p.x>=2.6f&&p.x<=5.8f&&p.z>4.9f&&p.z<=7.2f)return new ClinicHit(ClinicHitKind.Toilet,(int)ClinicAmenity.Toilet);
            if(p.x>=4.5f&&p.x<=5.8f&&p.z>=-3.0f&&p.z<=-2.0f)return new ClinicHit(ClinicHitKind.Vending,(int)ClinicAmenity.Vending);
            if(p.x>=-5.6f&&p.x<=-.25f&&p.z>=-4.6f&&p.z<=-.05f)return new ClinicHit(ClinicHitKind.Reception,(int)ClinicRoom.Reception);
            if(p.x>=-5.6f&&p.x<=-.25f&&p.z>-.05f&&p.z<=4.9f)return new ClinicHit(ClinicHitKind.Treatment,(int)ClinicRoom.FirstAid);
            if(p.x>=1.35f&&p.x<=5.6f&&p.z>=-1.7f&&p.z<=4.9f)return new ClinicHit(waitingBuilt?ClinicHitKind.Waiting:ClinicHitKind.Expansion,(int)ClinicRoom.Waiting);
            return default;
        }
        public void SelectRoom(ClinicRoom room)
        { selection.gameObject.SetActive(true);selection.position=roomRoots[(int)room].position+new Vector3(0,.17f,0);selection.localScale=room==ClinicRoom.Waiting?new Vector3(4.4f,1,6.5f):new Vector3(5.35f,1,4.8f); }
        public void SelectRoom(ClinicHit hit)
        {
            if(hit.Kind==ClinicHitKind.None||hit.Kind==ClinicHitKind.Cash||hit.Kind==ClinicHitKind.VendingCash){selection.gameObject.SetActive(false);return;}
            if(hit.Kind==ClinicHitKind.Desk||hit.Kind==ClinicHitKind.Station)
            {
                selection.gameObject.SetActive(true);selection.position=(hit.Kind==ClinicHitKind.Desk?desks[Mathf.Clamp(hit.Id,0,1)]:stations[Mathf.Clamp(hit.Id,0,1)]).transform.position+Vector3.up*.025f;
                selection.localScale=hit.Kind==ClinicHitKind.Desk?new Vector3(1.85f,1,1.1f):new Vector3(1.9f,1,1.9f);return;
            }
            if(hit.Kind==ClinicHitKind.Parking||hit.Kind==ClinicHitKind.Toilet||hit.Kind==ClinicHitKind.Vending)
            {
                selection.gameObject.SetActive(true);selection.position=hit.Kind==ClinicHitKind.Parking?new Vector3(-10.325f,.17f,0):hit.Kind==ClinicHitKind.Toilet?new Vector3(4.2f,.19f,6.05f):new Vector3(5.05f,.17f,-2.58f);
                selection.localScale=hit.Kind==ClinicHitKind.Parking?new Vector3(8.35f,1,10.2f):hit.Kind==ClinicHitKind.Toilet?new Vector3(3.15f,1,2.3f):new Vector3(1.05f,1,.86f);return;
            }
            SelectRoom((ClinicRoom)hit.Id);
        }
        private float BoundedSize(float requested)
        {
            var coverage=GroundCoverage();float reference=Mathf.Max(.001f,SceneCamera.orthographicSize);
            float limit=Mathf.Min(16,50f*reference/Mathf.Max(.001f,coverage.size.x),42f*reference/Mathf.Max(.001f,coverage.size.z));
            return Mathf.Clamp(requested,2.4f,Mathf.Max(2.4f,limit));
        }
        private Bounds GroundCoverage()
        {
            TryViewportToGround(Vector2.zero,out var first);var coverage=new Bounds(first,Vector3.zero);
            for(int corner=1;corner<4;corner++)if(TryViewportToGround(new Vector2(corner&1,(corner>>1)&1),out var point))coverage.Encapsulate(point);
            return coverage;
        }
        private void ClampCenter()
        {
            ApplyCamera();var coverage=GroundCoverage();
            float minimum=-25-coverage.min.x,maximum=25-coverage.max.x;
            center.x+=minimum>maximum?(minimum+maximum)*.5f:Mathf.Clamp(0,minimum,maximum);
            minimum=-22-coverage.min.z;maximum=20-coverage.max.z;
            center.z+=minimum>maximum?(minimum+maximum)*.5f:Mathf.Clamp(0,minimum,maximum);center.y=0;
        }
        private void ApplyCamera() { SceneCamera.transform.position=center+CameraOffset;SceneCamera.orthographicSize=size; }
        private void FitHome(out Vector3 target,out float fittedSize)
        {
            // The starter rooms, circulation strip and full queue forecourt fit below the top HUD.
            // Projection-space fitting works for every render-target aspect without resetting a user camera.
            var right=SceneCamera.transform.right;var up=SceneCamera.transform.up;
            var minimum=new Vector2(float.MaxValue,float.MaxValue);var maximum=new Vector2(float.MinValue,float.MinValue);
            for(int corner=0;corner<8;corner++)
            {
                var point=new Vector3((corner&1)==0?-5.75f:1.50f,(corner&2)==0?0:2.20f,(corner&4)==0?-6.75f:5.05f);
                var projected=new Vector2(Vector3.Dot(right,point),Vector3.Dot(up,point));minimum=Vector2.Min(minimum,projected);maximum=Vector2.Max(maximum,projected);
            }
            const float visibleWidth=.88f,visibleHeight=.64f,verticalCenter=.46f;
            fittedSize=Mathf.Max((maximum.x-minimum.x)/(2*SceneCamera.aspect*visibleWidth),(maximum.y-minimum.y)/(2*visibleHeight));
            var middle=(minimum+maximum)*.5f;
            float desiredRight=middle.x,desiredUp=middle.y-(verticalCenter-.5f)*2*fittedSize;
            float determinant=right.x*up.z-right.z*up.x;
            target=new Vector3((desiredRight*up.z-right.z*desiredUp)/determinant,0,(right.x*desiredUp-desiredRight*up.x)/determinant);
        }

        private void BuildArchitecture()
        {
            art.Box("Meadow ground",scene,new Vector3(0,-.30f,0),new Vector3(100,.15f,100),"Meadow");
            art.Box("Clinic foundation",scene,new Vector3(0,-.01f,-.35f),new Vector3(11.7f,.27f,11.1f),"Clay");
            roomRoots[0]=art.Group("Reception",scene,new Vector3(-2.9f,0,-2.35f));
            roomRoots[1]=art.Group("First aid",scene,new Vector3(-2.9f,0,2.45f));
            roomRoots[2]=art.Group("Waiting room",scene,new Vector3(3.45f,0,1.6f));
            for(int room=0;room<3;room++)
            {
                float width=room==2?4.35f:5.35f,depth=room==2?6.55f:4.70f;
                var root=roomRoots[room];art.Box("Terrazzo floor",root,new Vector3(0,.11f,0),new Vector3(width,.05f,depth),room==0?"TilePeach":room==1?"TileBlue":"TileSage");
                for(float z=-depth*.5f+.48f;z<depth*.5f;z+=.65f)
                    art.Box("Tile seam",root,new Vector3(0,.142f,z),new Vector3(width,.004f,.008f),"Ivory");
                // Supplies stay in the forecourt while rooms continue to treat and seat visitors.
                var workArea=room==2?new Vector3(4.9f,0,-2.8f):new Vector3(2.2f+room*1.4f,0,-3.9f);
                renovations[room]=art.Group("Renovation at work",scene,workArea).gameObject;
                art.Box("Renovation toolbox",renovations[room].transform,new Vector3(0,.31f,0),new Vector3(.55f,.34f,.37f),"Apricot");
                for(int i=0;i<3;i++)art.Box("Stacked fresh panels",renovations[room].transform,new Vector3(.38f,.19f+i*.09f,.18f),new Vector3(.5f,.08f,.6f),"Wood");
                renovations[room].SetActive(false);
                for(int level=0;level<2;level++)
                {
                    var details=art.Group("Tier "+(level+2)+" fittings",root);tierDetails[room,level]=details.gameObject;
                    if(level==0)
                    {
                        if(room==2)
                        {
                            art.Box("Lounge oak window pelmet",details,new Vector3(0,2.05f,3.20f),new Vector3(4.05f,.14f,.18f),"Wood");
                            for(int n=0;n<3;n++)art.Box("Lounge shade valance",details,new Vector3(-1.34f+n*1.34f,1.90f,3.20f),new Vector3(1.17f,.20f,.08f),"Sage");
                        }
                        else { art.Model("Plant",details,new Vector3(-width*.39f,Floor,depth*.31f));art.Model("Cupboard",details,new Vector3(-width*.20f,Floor,depth*.39f)); }
                    }
                    else
                    {
                        for(int i=0;i<4;i++)art.Box("Decorative wall slat",details,new Vector3(-width*.46f,1.14f,-.7f+i*.45f),new Vector3(.035f,1.30f,.08f),"Gold");
                        if(room!=2)art.Model("Plant",details,new Vector3(width*.35f,Floor,depth*.38f));
                        else art.Box("Gold lounge cornice",details,new Vector3(0,2.17f,3.24f),new Vector3(4.20f,.06f,.10f),"Gold");
                    }
                    details.gameObject.SetActive(false);
                }
            }
            art.Box("Main circulation floor",scene,new Vector3(.58f,.11f,-.1f),new Vector3(1.5f,.05f,9.9f),"Ivory");
            art.Box("Reception forecourt paving",scene,new Vector3(-2.98f,.11f,-5.72f),new Vector3(5.4f,.06f,2.0f),"Ivory");
            ClinicArchitecture.Build(art,scene);
            for(int i=0;i<5;i++)
            {
                var x=i==4?4.83f:-4.25f+i*2.0f;
                art.Box("High window frame",scene,new Vector3(x,1.42f,4.86f),new Vector3(1.46f,1.02f,.07f),"Sage");
                art.Box("Daylight window",scene,new Vector3(x,1.43f,4.81f),new Vector3(1.29f,.86f,.03f),"Blue");
                art.Box("Window mullion",scene,new Vector3(x,1.44f,4.77f),new Vector3(.055f,.87f,.04f),"Ivory");
            }
            art.Box("Entry mat",scene,new Vector3(.68f,.145f,-4.55f),new Vector3(1.29f,.012f,1.0f),"Sage");
            for(float x=-5.4f;x<5.5f;x+=.72f)art.Box("Front path paver",scene,new Vector3(x,.12f,-5.15f),new Vector3(.68f,.07f,.55f),"Ivory");
            art.Model("Plant",scene,new Vector3(5.15f,Floor,-4.22f));art.Model("Plant",scene,new Vector3(-5.09f,Floor,-3.98f));
            var entrance=art.Group("Entrance care emblem",scene,new Vector3(1.60f,0,-4.68f));
            art.Box("Emblem post",entrance,new Vector3(0,.72f,0),new Vector3(.075f,1.45f,.075f),"Gold");
            art.Cylinder("Sage sign",entrance,new Vector3(0,1.49f,0),new Vector3(.56f,.10f,.56f),"SageDark").transform.localRotation=Quaternion.Euler(90,0,0);
            art.Box("Care mark horizontal",entrance,new Vector3(0,1.49f,-.066f),new Vector3(.32f,.085f,.027f),"Linen");
            art.Box("Care mark vertical",entrance,new Vector3(0,1.49f,-.066f),new Vector3(.085f,.32f,.027f),"Linen");
            selection=art.Group("Room selection",scene);
            for(int side=-1;side<=1;side+=2)
            {
                art.Box("Selection edge",selection,new Vector3(side*.49f,0,0),new Vector3(.007f,.014f,.98f),"Gold");
                art.Box("Selection edge",selection,new Vector3(0,0,side*.49f),new Vector3(.98f,.014f,.007f),"Gold");
            }
            selection.gameObject.SetActive(false);
        }
        private void BuildPrivacy()
        {
            receptionDivider=PrivacyScreen("Reception privacy divider",new Vector3(-2.79f,Floor,-2.50f),1.10f,1.46f,"Sage");
            // Offset toward station two: the first nurse's authored work socket remains 0.55m away.
            treatmentDivider=PrivacyScreen("Treatment privacy partition",new Vector3(-2.50f,Floor,2.20f),.98f,1.46f,"Linen");
        }
        private GameObject PrivacyScreen(string name,Vector3 position,float depth,float height,string role)
        {
            var screen=art.Group(name,scene,position);
            art.Box("Privacy panel",screen,new Vector3(0,height*.5f,0),new Vector3(.055f,height,depth),role);
            for(int side=-1;side<=1;side+=2)
                art.Box("Privacy brass upright",screen,new Vector3(0,height*.5f,side*depth*.5f),new Vector3(.065f,height+.04f,.045f),"Gold");
            art.Box("Privacy top rail",screen,new Vector3(0,height,0),new Vector3(.065f,.045f,depth),"Wood");
            screen.gameObject.SetActive(false);return screen.gameObject;
        }
        private void BuildFurniture()
        {
            for(int i=0;i<2;i++)
            {
                desks[i]=art.Model("ReceptionDesk",scene,new Vector3(-3.85f+i*2.12f,Floor,-2.52f));
                RegisterSockets(desks[i].transform,"reception.desk."+i+".",Vector3.forward,Vector3.back);
                var point=GetCashPoint(i);
                for(int j=0;j<6;j++)cash[i,j]=art.Box("Collectable cash",scene,point+new Vector3((j%2)*.13f-.06f,(j/2)*.055f,0),new Vector3(.20f,.043f,.16f),j%2==0?"Gold":"Apricot");
                desks[i].SetActive(i==0);for(int j=0;j<6;j++)cash[i,j].SetActive(false);
                stations[i]=art.Model("TreatmentBay",scene,new Vector3(-3.88f+i*2.15f,Floor,2.31f));
                RegisterSockets(stations[i].transform,"firstaid.station."+i+".",Vector3.back,Vector3.left);stations[i].SetActive(i==0);
            }
            for(int i=0;i<14;i++)
            {
                bool left=i%2==0;var rotation=Quaternion.Euler(0,left?-90:90,0);
                seats[i]=art.Model("Seat",scene,new Vector3(left?2.12f:4.73f,Floor,-.72f+(i/2)*.81f),rotation);
                foreach(var node in seats[i].GetComponentsInChildren<Transform>(true))if(node.name.Contains("__patient"))
                { anchors["waiting.seat."+i]=node;node.rotation=Quaternion.LookRotation(left?Vector3.right:Vector3.left);break; }
                seats[i].SetActive(false);
            }
            waitingClosed=art.Group("Future waiting lounge",scene,new Vector3(3.40f,0,1.36f)).gameObject;
            art.Box("Covered seating crate",waitingClosed.transform,new Vector3(0,.38f,0),new Vector3(1.70f,.48f,1.05f),"Wood");
            for(int i=-1;i<=1;i++)art.Box("Crate strap",waitingClosed.transform,new Vector3(i*.54f,.39f,0),new Vector3(.075f,.52f,1.09f),"Gold");
            art.Box("Future lounge inset",waitingClosed.transform,new Vector3(0,.15f,0),new Vector3(3.8f,.015f,5.8f),"Sage");
        }
        private void RegisterSockets(Transform root,string prefix,Vector3 patientFacing,Vector3 staffFacing)
        {
            foreach(var node in root.GetComponentsInChildren<Transform>(true))
            {
                int split=node.name.IndexOf("__",StringComparison.Ordinal);if(split<0)continue;
                var part=node.name.Substring(split+2).Split('.')[0];anchors[prefix+part]=node;
                if(part=="patient"||part=="staff")node.rotation=Quaternion.LookRotation(part=="patient"?patientFacing:staffFacing);
            }
        }
        private void Anchor(string name,Vector3 position,Vector3 forward=default)
        { var item=art.Group(name,scene,position);item.rotation=Quaternion.LookRotation(forward==default?Vector3.forward:forward);anchors[name]=item; }
        private void BuildAnchors()
        {
            for(int i=0;i<6;i++)Anchor("parking.bay."+i+".patient",ClinicAmenities.BayPatient(i),Vector3.right);
            Anchor("waiting.toilet.patient",new Vector3(4.80f,Floor,6.25f),Vector3.back);
            Anchor("waiting.vending.patient",new Vector3(5.05f,Floor,-3.42f),Vector3.forward);
            Anchor("waiting.vending.cash",ClinicAmenities.VendingCashPoint);
            Anchor("entrance",new Vector3(.67f,Floor,-5.80f));Anchor("exit",new Vector3(1.12f,Floor,-6.15f),Vector3.back);
            for(int i=0;i<11;i++)Anchor("reception.queue."+i,new Vector3(-3.85f+(i%4)*.73f,Floor,-4.78f-(i/4)*.70f));
            for(int i=0;i<2;i++)Anchor("firstaid.standing."+i,new Vector3(-.62f,Floor,.70f+i*.65f),Vector3.left);
            Anchor("reception.progress",new Vector3(-2.8f,1.65f,-1.40f));Anchor("firstaid.progress",new Vector3(-2.8f,1.90f,3.35f));Anchor("waiting.progress",new Vector3(3.4f,1.5f,1.1f));
        }
        private void BuildLighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.75f,.78f,.75f);
            RenderSettings.ambientEquatorColor=new Color(.48f,.53f,.49f);RenderSettings.ambientGroundColor=new Color(.31f,.28f,.24f);
            var light=art.Group("Soft clinic daylight",transform).gameObject.AddComponent<Light>();light.type=LightType.Directional;
            light.transform.rotation=Quaternion.Euler(48,-35,0);light.intensity=1.02f;light.color=new Color(.98f,.96f,.90f);
            light.shadows=LightShadows.Soft;light.shadowStrength=.28f;light.shadowBias=.025f;light.shadowNormalBias=.12f;light.cullingMask=1<<ClinicArt.Layer;
        }
        private void ReleaseTexture() { if(Texture==null)return;SceneCamera.targetTexture=null;Texture.Release();ClinicArt.Destroy(Texture);Texture=null; }
        private void OnDestroy() { ReleaseTexture();art?.Dispose(); }
    }
}
