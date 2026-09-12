using UnityEngine;
using UnityEngine.UIElements;

namespace IdleClinic.App
{
    public enum ClinicGlyph { Coin, Nurse, Reception, Bed, Chair, Equipment, Facility, Plant, Upgrade, Clock, Home, Plus, Minus, Close, Check, Settings, Sound, Haptic, Motion, Help, Arrow, Restore }

    public sealed class ClinicIcon : VisualElement
    {
        public static readonly Color Ink = new Color(.19f, .29f, .25f);
        private readonly ClinicGlyph glyph;
        public Color Tint { get; set; }
        public ClinicIcon(ClinicGlyph glyph, float size = 24, Color? tint = null)
        {
            this.glyph = glyph; Tint = tint ?? Ink;
            style.width = size; style.height = size; style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (r.width <= 0 || r.height <= 0) return;
            var p = context.painter2D;
            var s = Mathf.Min(r.width, r.height) / 24;
            var o = r.center - new Vector2(12, 12) * s;
            p.lineWidth = 1.9f * s; p.strokeColor = Tint; p.fillColor = Tint;
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            Vector2 V(float x, float y) => o + new Vector2(x, y) * s;
            void Line(params float[] a) { p.BeginPath(); p.MoveTo(V(a[0],a[1])); for(var i=2;i<a.Length;i+=2)p.LineTo(V(a[i],a[i+1])); p.Stroke(); }
            void Box(float x,float y,float w,float h) => Line(x,y,x+w,y,x+w,y+h,x,y+h,x,y);
            void Circle(float x,float y,float radius,bool fill=false) { p.BeginPath(); p.Arc(V(x,y),radius*s,0,360); if(fill)p.Fill();else p.Stroke(); }
            void Cross(float x,float y,float half) { Line(x-half,y,x+half,y);Line(x,y-half,x,y+half); }
            switch(glyph)
            {
                case ClinicGlyph.Coin: Circle(12,12,9); Circle(12,12,6); Line(12,8,15,12,12,16,9,12,12,8);break;
                case ClinicGlyph.Nurse: Circle(12,8,3.5f); Line(5,21,5,18,8,14,16,14,19,18,19,21);Box(7,2,10,4);Cross(12,4,1);break;
                case ClinicGlyph.Reception: Box(3,12,18,7);Line(5,19,5,22);Line(19,19,19,22);Circle(9,6,3);Line(6,12,6,10,12,10,12,12);Box(15,6,5,4);break;
                case ClinicGlyph.Bed: Line(3,6,3,21);Line(21,12,21,21);Box(3,12,18,5);Circle(7,9,2);Line(11,9,17,9);break;
                case ClinicGlyph.Chair: Box(5,3,14,10);Box(4,13,16,4);Line(6,17,6,22);Line(18,17,18,22);break;
                case ClinicGlyph.Equipment: Box(3,7,18,14);Line(8,7,8,3,16,3,16,7);Cross(12,14,4);break;
                case ClinicGlyph.Facility: Box(4,6,16,15);Line(4,13,20,13);Line(8,3,16,3);Circle(16,10,1,true);Circle(16,17,1,true);break;
                case ClinicGlyph.Plant: Line(6,16,8,22,16,22,18,16,6,16);Line(12,16,12,5);Line(12,12,5,11,3,5,9,6,12,10);Line(12,8,15,3,21,2,20,7,12,11);break;
                case ClinicGlyph.Upgrade: Line(5,10,12,3,19,10);Line(12,3,12,17);Line(5,21,19,21);break;
                case ClinicGlyph.Clock: Circle(12,12,9);Line(12,6,12,12,17,15);break;
                case ClinicGlyph.Home: Line(3,11,12,3,21,11);Line(6,10,6,21,18,21,18,10);Cross(12,14,2.5f);break;
                case ClinicGlyph.Plus: Cross(12,12,7);break;
                case ClinicGlyph.Minus:Line(5,12,19,12);break;
                case ClinicGlyph.Close:Line(6,6,18,18);Line(18,6,6,18);break;
                case ClinicGlyph.Check:Line(4,12,10,18,20,6);break;
                case ClinicGlyph.Settings:Circle(12,12,6);Circle(12,12,2.5f);for(var i=0;i<8;i++){float a=i*Mathf.PI/4;Line(12+Mathf.Cos(a)*7,12+Mathf.Sin(a)*7,12+Mathf.Cos(a)*10,12+Mathf.Sin(a)*10);}break;
                case ClinicGlyph.Sound:Line(3,9,7,9,12,5,12,19,7,15,3,15,3,9);Line(16,8,19,11,19,13,16,16);Line(20,5,23,10,23,14,20,19);break;
                case ClinicGlyph.Haptic:Box(7,3,10,18);Circle(12,18,1,true);Line(3,7,1,11,3,15);Line(21,7,23,11,21,15);break;
                case ClinicGlyph.Motion:Circle(15,12,7);Line(2,6,6,6);Line(1,12,5,12);Line(2,18,6,18);Line(15,8,15,12,18,14);break;
                case ClinicGlyph.Arrow:Line(4,12,20,12,14,6);Line(20,12,14,18);break;
                case ClinicGlyph.Restore:Circle(12,12,8);Line(3,3,3,10,9,10);Line(12,7,12,12,16,15);break;
                default:Circle(12,12,9);Line(9,8,10,6,14,6,16,9,12,12,12,14);Circle(12,18,.8f,true);break;
            }
        }
    }

    public sealed class ClinicProgress : VisualElement
    {
        private float progress;
        public Color Tint = new Color(.25f,.48f,.35f);
        public float Progress { get => progress; set { var next=Mathf.Clamp01(value); if(Mathf.Abs(next-progress)>.001f){progress=next;MarkDirtyRepaint();} } }
        public ClinicProgress(float size=30)
        {
            style.width=size;style.height=size;pickingMode=PickingMode.Ignore;
            generateVisualContent += ctx =>
            {
                var p=ctx.painter2D;var c=contentRect.center;var radius=Mathf.Min(contentRect.width,contentRect.height)/2-3;
                if(radius<=0)return;
                p.lineWidth=4;p.lineCap=LineCap.Round;p.strokeColor=new Color(.91f,.91f,.84f);
                p.BeginPath();p.Arc(c,radius,0,360);p.Stroke();
                if(progress>0){p.strokeColor=Tint;p.BeginPath();p.Arc(c,radius,-90,-90+progress*359.99f);p.Stroke();}
            };
        }
    }
}
