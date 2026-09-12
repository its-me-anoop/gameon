using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Permanent interior dressing stays outside the graph's reserved actor lanes.</summary>
    internal static class ClinicFurnishings
    {
        internal static void Build(ClinicArt art,Transform parent)
        {
            var root=art.Group("Clinic interior details",parent);
            Rug(art,root,"Reception welcome rug",new Vector3(-2.9f,.15f,-3.92f),new Vector3(4.5f,.009f,.56f),"TilePeach");
            Rug(art,root,"Care floor inlay",new Vector3(-2.9f,.15f,2.4f),new Vector3(4.55f,.009f,3.9f),"TileBlue");
            Rug(art,root,"Waiting woven rug",new Vector3(3.4f,.15f,1.7f),new Vector3(1.65f,.009f,5.8f),"TilePeach");
            // The circulation line is flush with the floor; nothing occupies a walking socket.
            art.Box("Corridor sage runner",root,new Vector3(.68f,.15f,-.15f),new Vector3(.40f,.008f,8.3f),"TileSage");
            for(int i=0;i<5;i++)
            {
                art.Cylinder("Wayfinding care dot",root,new Vector3(.68f,.16f,-3.3f+i*1.55f),new Vector3(.14f,.008f,.14f),"Gold");
            }
            NoticeBoard(art,root,"Reception notice board",new Vector3(-5.53f,1.40f,-1.45f),90,1.1f);
            NoticeBoard(art,root,"Waiting notice board",new Vector3(2.45f,1.75f,4.75f),0,1.05f);
            var storage=art.Group("Reception record storage",root,new Vector3(-5.23f,.14f,-2.30f));
            Cabinet(art,storage,Vector3.zero,new Vector3(.52f,.90f,.60f),"Sage");
            for(int i=0;i<4;i++)art.Box("Appointment folders",storage,new Vector3(-.18f+i*.11f,1.00f,0),new Vector3(.075f,.21f,.28f),i%2==0?"Apricot":"Blue");
            var care=art.Group("Care supplies cabinet",root,new Vector3(-5.18f,.14f,2.35f));
            Cabinet(art,care,Vector3.zero,new Vector3(.58f,.80f,.80f),"Sage");
            for(int i=0;i<3;i++)art.Box("Folded care towels",care,new Vector3(0,.83f+i*.045f,0),new Vector3(.42f,.04f,.38f),"Linen");
            var sink=art.Group("Treatment handwash sink",root,new Vector3(-5.17f,.14f,1.05f));
            Cabinet(art,sink,Vector3.zero,new Vector3(.55f,.73f,.62f),"TileBlue");
            art.Box("Handwash basin",sink,new Vector3(0,.77f,0),new Vector3(.61f,.12f,.68f),"Linen");
            art.Box("Basin hollow",sink,new Vector3(0,.837f,0),new Vector3(.42f,.007f,.42f),"Blue");
            art.Cylinder("Wash tap",sink,new Vector3(-.20f,.94f,0),new Vector3(.06f,.25f,.06f),"Gold");
            art.Box("Wash tap spout",sink,new Vector3(-.12f,1.045f,0),new Vector3(.20f,.06f,.06f),"Gold");
            art.Model("Plant",root,new Vector3(1.57f,.14f,-3.34f));
            var display=art.Group("Welcome community display",root,new Vector3(3.2f,.14f,-4.15f));
            art.Box("Welcome display planter",display,new Vector3(0,.32f,0),new Vector3(1.9f,.64f,.6f),"Sage");
            for(int i=0;i<5;i++)
            {
                art.Orb("Welcome display foliage",display,new Vector3(-.7f+i*.35f,.73f,0),new Vector3(.42f,.38f,.42f),i%2==0?"Leaf":"Sage");
                art.Orb("Welcome display flower",display,new Vector3(-.7f+i*.35f,.94f,0),new Vector3(.13f,.12f,.13f),"Apricot");
            }
            art.Box("Welcome display warm wood cap",display,new Vector3(0,.57f,0),new Vector3(1.98f,.06f,.68f),"Wood");
            // Floor terrazzo chips give the pale surfaces texture without filling the circulation with props.
            for(int room=0;room<3;room++)for(int i=0;i<24;i++)
            {
                float x=room==2?1.55f+(i%4)*1.01f:-5.35f+(i%6)*.86f;
                float z=room==0?-4.38f+(i/6)*1.04f:room==1?.38f+(i/6)*1.17f:-1.3f+(i/4)*1.05f;
                var chip=art.Box("Terrazzo aggregate",root,new Vector3(x+(i%3)*.043f,.145f,z),new Vector3(.045f,.005f,.08f),i%3==0?"Clay":i%3==1?"TileSage":"TileBlue");
                chip.transform.localRotation=Quaternion.Euler(0,i*37,0);
            }
        }
        private static void Rug(ClinicArt art,Transform parent,string name,Vector3 position,Vector3 size,string role)
        {
            var rug=art.Group(name,parent,position);art.Box("Woven border",rug,Vector3.zero,size,"Wood");
            art.Box("Woven inset",rug,new Vector3(0,.005f,0),new Vector3(size.x-.12f,size.y,size.z-.12f),role);
        }
        internal static void Cabinet(ClinicArt art,Transform parent,Vector3 position,Vector3 size,string role)
        {
            art.Box("Cabinet body",parent,position+Vector3.up*size.y*.5f,size,role);
            art.Box("Cabinet split",parent,position+new Vector3(0,size.y*.5f,-size.z*.505f),new Vector3(.012f,size.y*.83f,.015f),"SageDark");
            for(int side=-1;side<=1;side+=2)art.Box("Cabinet brass handle",parent,position+new Vector3(side*.055f,size.y*.62f,-size.z*.53f),new Vector3(.025f,.12f,.035f),"Gold");
        }
        internal static void NoticeBoard(ClinicArt art,Transform parent,string name,Vector3 position,float rotation,float scale=1)
        {
            var board=art.Group(name,parent,position);board.localRotation=Quaternion.Euler(0,rotation,0);board.localScale=Vector3.one*scale;
            art.Box("Notice board oak frame",board,Vector3.zero,new Vector3(1.05f,.76f,.075f),"Wood");
            art.Box("Notice cork",board,new Vector3(0,0,-.044f),new Vector3(.94f,.65f,.02f),"Clay");
            for(int i=0;i<3;i++)
            {
                var poster=art.Group("Community pictogram poster",board,new Vector3(-.31f+i*.31f,i==1?-.05f:.04f,-.062f));
                poster.localRotation=Quaternion.Euler(0,0,i==1?-6:4);
                art.Box("Poster paper",poster,Vector3.zero,new Vector3(.25f,.40f,.014f),i==1?"TileBlue":"Linen");
                if(i==0)
                { art.Box("Poster care cross",poster,new Vector3(0,.06f,-.015f),new Vector3(.14f,.04f,.012f),"Rose");art.Box("Poster care cross",poster,new Vector3(0,.06f,-.02f),new Vector3(.04f,.14f,.012f),"Rose"); }
                else if(i==1)
                { art.Orb("Poster wellbeing circle",poster,new Vector3(0,.07f,-.015f),new Vector3(.15f,.15f,.015f),"Gold");art.Box("Poster wellbeing stem",poster,new Vector3(0,-.02f,-.02f),new Vector3(.03f,.11f,.012f),"SageDark"); }
                else
                { art.Orb("Poster person head",poster,new Vector3(0,.11f,-.02f),new Vector3(.07f,.07f,.014f),"Sage");art.Box("Poster person shoulders",poster,new Vector3(0,.02f,-.02f),new Vector3(.13f,.06f,.014f),"Sage"); }
                art.Box("Poster short caption",poster,new Vector3(0,-.11f,-.02f),new Vector3(.14f,.019f,.012f),"Wood");
                art.Orb("Brass notice pin",poster,new Vector3(0,.16f,-.027f),new Vector3(.032f,.032f,.015f),"Gold");
            }
        }
    }
}
