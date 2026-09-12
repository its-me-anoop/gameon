using UnityEngine;

namespace IdleClinic.App
{
    /// <summary>Panel-point layout shared by portrait, landscape and resizable windows.</summary>
    public static class ClinicViewportLayout
    {
        public static Rect SafePanelArea(Vector2 panelSize,Vector2 pixelSize,Rect safePixels)
        {
            var scale=new Vector2(panelSize.x/Mathf.Max(1,pixelSize.x),panelSize.y/Mathf.Max(1,pixelSize.y));
            return Rect.MinMaxRect(safePixels.xMin*scale.x,(pixelSize.y-safePixels.yMax)*scale.y,
                safePixels.xMax*scale.x,(pixelSize.y-safePixels.yMin)*scale.y);
        }

        public static Rect DockArea(Rect safeArea)
        {
            const float maximumWidth=520,toolbarClearance=132,bottomMargin=12;
            var margin=safeArea.width<370?8:12;
            var width=Mathf.Max(0,Mathf.Min(maximumWidth,safeArea.width-margin*2));
            return new Rect(safeArea.center.x-width/2,safeArea.yMin+toolbarClearance,width,
                Mathf.Max(0,safeArea.height-toolbarClearance-bottomMargin));
        }
    }
}
