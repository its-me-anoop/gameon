using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Joined wall runs with explicit door apertures and low camera-facing cutaways.</summary>
    internal static class ClinicArchitecture
    {
        internal static void Build(ClinicArt art,Transform scene)
        {
            var root=art.Group("Joined clinic walls",scene);
            Wall(art,root,"West wall",new Vector2(-5.65f,-5.17f),new Vector2(-5.65f,5.05f),2.05f,.16f);
            Wall(art,root,"North wall",new Vector2(-5.73f,4.97f),new Vector2(2.955f,4.97f),2.05f,.16f);
            Wall(art,root,"North toilet return wall",new Vector2(3.92f,4.97f),new Vector2(5.765f,4.97f),2.05f,.16f);
            Wall(art,root,"East cutaway wall",new Vector2(5.69f,-5.17f),new Vector2(5.69f,5.05f),.68f,.15f);
            // Slim front runs leave the authored queue rows a full visitor radius from the wall.
            Wall(art,root,"Reception front wall",new Vector2(-5.73f,-5.13f),new Vector2(-.235f,-5.13f),.60f,.08f);
            Wall(art,root,"Lobby front wall",new Vector2(1.585f,-5.13f),new Vector2(5.765f,-5.13f),.60f,.08f);
            Wall(art,root,"Reception back partition",new Vector2(-5.73f,-.15f),new Vector2(-.235f,-.15f),.84f,.15f);
            Wall(art,root,"Waiting south return",new Vector2(1.67f,-1.70f),new Vector2(1.67f,-.235f),.84f,.14f);
            Wall(art,root,"Waiting corridor partition",new Vector2(1.67f,1.235f),new Vector2(1.67f,5.05f),.84f,.14f);
            Wall(art,root,"Waiting front west wall",new Vector2(1.60f,-1.66f),new Vector2(2.665f,-1.66f),.68f,.14f);
            Wall(art,root,"Waiting front east wall",new Vector2(4.135f,-1.66f),new Vector2(5.765f,-1.66f),.68f,.14f);
            for(int side=0;side<2;side++)
                art.Box("Toilet doorway jamb",root,new Vector3(side==0?2.955f:3.92f,1.11f,4.97f),new Vector3(.10f,1.94f,.18f),"SageDark");
            art.Box("Toilet doorway lintel",root,new Vector3(3.4375f,2.15f,4.97f),new Vector3(1.065f,.14f,.18f),"Ivory");
            art.Box("Toilet doorway transom",root,new Vector3(3.4375f,1.91f,4.97f),new Vector3(.87f,.34f,.07f),"Sage");
        }
        private static void Wall(ClinicArt art,Transform root,string name,Vector2 start,Vector2 end,float height,float thickness)
        {
            bool horizontal=Mathf.Abs(end.x-start.x)>Mathf.Abs(end.y-start.y);
            var midpoint=(start+end)*.5f;
            var scale=horizontal?new Vector3(Mathf.Abs(end.x-start.x),height,thickness):new Vector3(thickness,height,Mathf.Abs(end.y-start.y));
            art.Box(name,root,new Vector3(midpoint.x,.14f+height*.5f,midpoint.y),scale,"Ivory");
            scale.y=.055f;
            art.Box(name+" cap",root,new Vector3(midpoint.x,.14f+height,midpoint.y),scale,"Sage");
            scale.y=.10f;
            art.Box(name+" skirting",root,new Vector3(midpoint.x,.19f,midpoint.y),scale,"SageDark");
        }
    }
}
