using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Static neighbourhood scenery outside the working clinic. All parts share the clinic's cached meshes.</summary>
    internal static class ClinicSurroundings
    {
        internal static void Build(ClinicArt art,Transform parent)
        {
            var town=art.Group("Clinic neighbourhood",parent);
            art.Box("Neighbourhood street",town,new Vector3(0,-.11f,-9.0f),new Vector3(30,.15f,3.2f),"Asphalt");
            art.Box("Neighbourhood side street",town,new Vector3(8.85f,-.11f,1.8f),new Vector3(3.2f,.15f,24.8f),"Asphalt");
            art.Box("Front pavement",town,new Vector3(0,-.035f,-6.95f),new Vector3(30,.16f,.90f),"Linen");
            art.Box("Far pavement",town,new Vector3(0,-.035f,-11.05f),new Vector3(30,.16f,.90f),"Linen");
            art.Box("Side pavement",town,new Vector3(6.73f,-.035f,1.3f),new Vector3(.95f,.16f,16.5f),"Linen");
            art.Box("Front curb",town,new Vector3(0,.005f,-7.40f),new Vector3(30,.12f,.08f),"Ivory");
            art.Box("Side curb",town,new Vector3(7.19f,.005f,1.3f),new Vector3(.08f,.12f,16.5f),"Ivory");
            for(int i=0;i<7;i++)art.Box("Street centre stripe",town,new Vector3(-12+i*4,-.028f,-9.0f),new Vector3(1.5f,.005f,.08f),"Linen");
            for(int i=0;i<5;i++)art.Box("Clinic crossing",town,new Vector3(.72f,-.025f,-7.80f-i*.55f),new Vector3(1.85f,.01f,.25f),"Linen");
            for(int i=0;i<7;i++)art.Box("Pavement joint",town,new Vector3(-5.5f+i*1.8f,.05f,-6.94f),new Vector3(.018f,.004f,.85f),"Clay");

            House(art,town,0,new Vector3(-8.2f,.05f,1.4f),new Vector3(2.6f,2.5f,3.2f),"Apricot");
            House(art,town,1,new Vector3(-4.5f,.05f,8.5f),new Vector3(3.2f,2.9f,3.0f),"Sage");
            House(art,town,2,new Vector3(.3f,.05f,8.9f),new Vector3(3.4f,3.4f,3.5f),"Ivory");
            House(art,town,3,new Vector3(4.7f,.05f,8.4f),new Vector3(2.8f,2.6f,3.0f),"Blue");
            House(art,town,4,new Vector3(-5.3f,.05f,-13.3f),new Vector3(3.3f,2.1f,2.6f),"Ivory");
            House(art,town,5,new Vector3(11.8f,.05f,1.4f),new Vector3(2.1f,2.5f,3.2f),"Apricot");
            Tree(art,town,0,new Vector3(-6.95f,.05f,-4.2f),1.0f);
            Tree(art,town,1,new Vector3(-6.90f,.05f,5.9f),.95f);
            Tree(art,town,2,new Vector3(3.5f,.05f,6.65f),.86f);
            Tree(art,town,3,new Vector3(6.8f,.05f,-3.8f),.82f);
            Tree(art,town,4,new Vector3(-2.9f,.05f,-11.45f),.90f);
            Tree(art,town,5,new Vector3(3.9f,.05f,-11.5f),.92f);
            var bench=art.Group("Street bench",town,new Vector3(-7.05f,.05f,-.85f));
            for(int side=-1;side<=1;side+=2)art.Box("Bench leg",bench,new Vector3(side*.50f,.22f,0),new Vector3(.08f,.44f,.48f),"SageDark");
            for(int slat=0;slat<3;slat++)art.Box("Bench oak slat",bench,new Vector3(0,.46f,-.18f+slat*.18f),new Vector3(1.35f,.055f,.13f),"Wood");
        }

        private static void House(ClinicArt art,Transform town,int index,Vector3 position,Vector3 size,string role)
        {
            var house=art.Group("Town house "+index,town,position);
            art.Box("House walls",house,new Vector3(0,size.y*.5f,0),size,role);
            art.Box("House foundation",house,new Vector3(0,.13f,0),new Vector3(size.x+.14f,.26f,size.z+.14f),"Clay");
            art.Box("Roof cornice",house,new Vector3(0,size.y,0),new Vector3(size.x+.26f,.18f,size.z+.26f),"Linen");
            art.Box("Recessed roof",house,new Vector3(0,size.y+.15f,0),new Vector3(size.x-.12f,.16f,size.z-.12f),"SageDark");
            art.Box("House door",house,new Vector3(0,.67f,-size.z*.5f-.025f),new Vector3(.56f,1.28f,.05f),"Wood");
            art.Box("Door lintel",house,new Vector3(0,1.36f,-size.z*.5f-.04f),new Vector3(.72f,.12f,.10f),"Linen");
            for(int side=-1;side<=1;side+=2)
            {
                var window=new Vector3(side*size.x*.29f,size.y*.66f,-size.z*.5f-.03f);
                art.Box("House window frame",house,window,new Vector3(.60f,.76f,.06f),"Linen");
                art.Box("House window glass",house,window+new Vector3(0,0,-.04f),new Vector3(.47f,.62f,.025f),"Blue");
                art.Box("House window sill",house,window+new Vector3(0,-.40f,-.045f),new Vector3(.72f,.07f,.16f),"Gold");
            }
            art.Box("Chimney",house,new Vector3(size.x*.25f,size.y+.48f,size.z*.20f),new Vector3(.35f,.75f,.35f),"Clay");
        }

        private static void Tree(ClinicArt art,Transform town,int index,Vector3 position,float scale)
        {
            var tree=art.Group("Street tree "+index,town,position);tree.localScale=Vector3.one*scale;
            art.Cylinder("Tree planting bed",tree,new Vector3(0,.025f,0),new Vector3(1.30f,.05f,1.30f),"Sage");
            art.Cylinder("Tree trunk",tree,new Vector3(0,.70f,0),new Vector3(.16f,1.40f,.16f),"Wood");
            art.Orb("Tree crown",tree,new Vector3(0,1.70f,0),new Vector3(1.35f,1.70f,1.25f),"Leaf");
            art.Orb("Tree side leaves",tree,new Vector3(-.34f,1.40f,.12f),new Vector3(.88f,1.15f,.92f),"Sage");
            art.Orb("Tree upper leaves",tree,new Vector3(.23f,2.17f,-.04f),new Vector3(.77f,.81f,.77f),"Leaf");
        }
    }
}
