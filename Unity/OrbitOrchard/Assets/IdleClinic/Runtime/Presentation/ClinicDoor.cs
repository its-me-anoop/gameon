using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Sliding panels park outside both authored circulation lanes.</summary>
    internal sealed class ClinicDoor
    {
        private readonly Vector3 center;
        private readonly Transform left,right;
        private readonly float halfPanel,slide;
        private float openness,hold;
        private bool rendered;
        internal ClinicDoor(ClinicArt art,Transform parent,bool entrance=false,Vector3? position=null,float yaw=0,float openingWidth=1.65f,string name=null)
        {
            center=position??new Vector3(.675f,.14f,entrance?-5.13f:-.15f);
            halfPanel=openingWidth*.25f;slide=openingWidth*.5f+.05f;
            var root=art.Group(name??(entrance?"Clinic entrance doorway":"Care wing doorway"),parent,center);root.localRotation=Quaternion.Euler(0,yaw,0);
            // Entrance rows pass 0.35m north/south of the frame; slim leaves preserve a 0.30m visitor radius.
            float depth=entrance?.05f:.13f;
            for(int side=-1;side<=1;side+=2)
                art.Box("Care doorway jamb",root,new Vector3(side*(openingWidth*.5f+.085f),.975f,0),new Vector3(.12f,1.95f,depth),"SageDark");
            art.Box("Care doorway header",root,new Vector3(0,2.025f,0),new Vector3(openingWidth+.30f,.15f,.17f),"Ivory");
            art.Box("Door head seal",root,new Vector3(0,1.865f,0),new Vector3(openingWidth+.03f,.17f,depth),"Sage");
            art.Box("Care doorway rail",root,new Vector3(0,.013f,0),new Vector3(openingWidth+.17f,.025f,entrance?.045f:.065f),"Gold");
            left=Panel(art,root,entrance?"Entrance door left panel":"Care door left panel",-halfPanel,entrance);
            right=Panel(art,root,entrance?"Entrance door right panel":"Care door right panel",halfPanel,entrance);
            left.localScale=right.localScale=new Vector3(openingWidth/1.65f,1,1);
            if(entrance)
            {
                art.Box("Clinic name plaque",root,new Vector3(0,2.37f,0),new Vector3(3.48f,.55f,.14f),"SageDark");
                art.Box("Name plaque brass trim",root,new Vector3(0,2.08f,0),new Vector3(3.48f,.035f,.15f),"Gold");
                art.Model("ClinicSign",root,new Vector3(0,2.37f,-.08f));
            }
        }
        private static Transform Panel(ClinicArt art,Transform parent,string name,float x,bool slim)
        {
            var panel=art.Group(name,parent,new Vector3(x,0,0));
            if(slim)
            {
                art.Box("Entrance panel frame",panel,new Vector3(0,.90f,0),new Vector3(.815f,1.76f,.030f),"Sage");
                art.Box("Entrance daylight pane",panel,new Vector3(0,1.04f,-.019f),new Vector3(.70f,1.24f,.010f),"Blue");
                art.Box("Entrance lower panel",panel,new Vector3(0,.20f,-.019f),new Vector3(.71f,.31f,.010f),"Ivory");
                art.Box("Entrance pull",panel,new Vector3(x<0?.27f:-.27f,.96f,-.024f),new Vector3(.025f,.25f,.010f),"Gold");
                return panel;
            }
            art.Box("Door panel frame",panel,new Vector3(0,.90f,0),new Vector3(.815f,1.76f,.07f),"Sage");
            art.Box("Door daylight pane",panel,new Vector3(0,1.03f,-.042f),new Vector3(.70f,1.31f,.021f),"Blue");
            art.Box("Door lower panel",panel,new Vector3(0,.19f,-.043f),new Vector3(.71f,.29f,.022f),"Ivory");
            art.Box("Door care mark",panel,new Vector3(0,1.08f,-.059f),new Vector3(.22f,.06f,.022f),"Linen");
            art.Box("Door care mark",panel,new Vector3(0,1.08f,-.073f),new Vector3(.06f,.22f,.022f),"Linen");
            return panel;
        }
        internal void Render(ClinicActors actors,float deltaTime,bool reducedMotion)
        {
            if(actors.ApproachesDoor(center,1.20f,1.40f))hold=.45f;else hold=Mathf.Max(0,hold-Mathf.Max(0,deltaTime));
            float target=hold>0?1:0;
            openness=reducedMotion||!rendered?target:Mathf.MoveTowards(openness,target,Mathf.Max(0,deltaTime)*(target>openness?8f:2.5f));rendered=true;
            left.localPosition=new Vector3(-halfPanel-slide*openness,0,0);right.localPosition=new Vector3(halfPanel+slide*openness,0,0);
        }
    }
}
