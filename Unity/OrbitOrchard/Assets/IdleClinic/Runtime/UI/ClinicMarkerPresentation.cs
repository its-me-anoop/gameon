using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleClinic.App
{
    /// <summary>World-scale marker detail and stateless spacing in panel points.</summary>
    public static class ClinicMarkerPresentation
    {
        public const float CashSize=44;
        public const float ConstructionSize=26;
        public static bool IsWide(float cameraSize,bool currentlyWide)=>cameraSize>=(currentlyWide?7.75f:8.25f);

        public static bool CrowdedReception(Vector2 first,Vector2 second,Rect viewport)
            =>viewport.Contains(first)&&viewport.Contains(second)&&Mathf.Abs(second.x-first.x)<80;

        public static void SeparateReception(Vector2 first,Vector2 second,out Vector2 firstResult,out Vector2 secondResult)
        {
            firstResult=first;secondResult=second;
            var distance=Mathf.Abs(second.x-first.x);
            if(distance>=48)return;
            var direction=second.x>=first.x?1:-1;
            var shift=(48-distance)/2;
            firstResult.x-=direction*shift;secondResult.x+=direction*shift;
        }
        /// <summary>Deterministic, bounded targets for every live till, including four overlapping projections.</summary>
        public static Vector2[] SeparateCashTargets(IReadOnlyList<Vector2> projections,Rect viewport)
        {
            var result=new Vector2[projections.Count];
            var half=CashSize/2;
            var minX=viewport.xMin+half;var maxX=Mathf.Max(minX,viewport.xMax-half);
            var minY=viewport.yMin+half;var maxY=Mathf.Max(minY,viewport.yMax-half);
            for(var i=0;i<projections.Count;i++)
            {
                var origin=projections[i];var best=new Vector2(Mathf.Clamp(origin.x,minX,maxX),Mathf.Clamp(origin.y,minY,maxY));
                var score=float.PositiveInfinity;
                for(var dy=-4;dy<=4;dy++)for(var dx=-4;dx<=4;dx++)
                {
                    var candidate=new Vector2(Mathf.Clamp(origin.x+dx*48,minX,maxX),Mathf.Clamp(origin.y+dy*48,minY,maxY));
                    var clear=true;
                    for(var j=0;j<i;j++)
                        if(Mathf.Abs(candidate.x-result[j].x)<48&&Mathf.Abs(candidate.y-result[j].y)<48){clear=false;break;}
                    if(!clear)continue;
                    var distance=(candidate-origin).sqrMagnitude;
                    if(distance>=score)continue;
                    best=candidate;score=distance;
                }
                result[i]=best;
            }
            return result;
        }
        public static void CashDetail(VisualElement marker,bool wide)
        {
            marker.EnableInClassList("compact-cash",wide);
            marker.Q<Label>(className:"cash-amount").style.display=wide?DisplayStyle.None:DisplayStyle.Flex;
            FixedSize(marker,wide,CashSize);
        }
        public static void ConstructionDetail(VisualElement marker,bool wide)
        {
            marker.EnableInClassList("compact-construction",wide);
            marker.Q<Label>(className:"construction-clock").style.display=wide?DisplayStyle.None:DisplayStyle.Flex;
            FixedSize(marker,wide,ConstructionSize);
        }
        public static void PatientDetail(ClinicProgress ring,bool wide)
            =>ring.style.scale=new Scale(Vector3.one*(wide?18f/28:1));
        private static void FixedSize(VisualElement marker,bool fixedSize,float size)
        {
            var length=fixedSize?new StyleLength(size):new StyleLength(StyleKeyword.Null);
            marker.style.width=length;marker.style.height=length;
            marker.style.minWidth=length;marker.style.maxWidth=length;
            marker.style.minHeight=length;marker.style.maxHeight=length;
        }
    }
}
