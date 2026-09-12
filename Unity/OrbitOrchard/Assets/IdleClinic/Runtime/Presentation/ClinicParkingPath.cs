using System.Collections.Generic;
using UnityEngine;

namespace IdleClinic.Presentation
{
    internal static class ClinicParkingLayout
    {
        internal const float AisleX=-10.6f;
        internal static readonly Vector3 SignPoint=new Vector3(-13.75f,.90f,-5.0f);
        internal static Vector3 BayCenter(int bay)=>new Vector3(bay%2==0?-13f:-8.2f,.055f,-3f+(bay/2)*2.9f);
        internal static Vector3 BayDoor(int bay)=>BayCenter(bay)+new Vector3(0,.085f,-.84f);
        internal static Vector3 BayFacing(int bay)=>bay%2==0?Vector3.left:Vector3.right;
        internal static Vector3 Point(float x,float z)=>new Vector3(x,Mathf.Lerp(-.11f,.055f,Mathf.InverseLerp(-7.8f,-6.2f,z)),z);
    }

    /// <summary>Cached rounded polyline sampled by distance; fixed work and no allocation per frame.</summary>
    internal sealed class ClinicParkingPath
    {
        private readonly Vector3[] points;private readonly float[] distances;
        internal float Length=>distances[distances.Length-1];
        internal ClinicParkingPath(params Vector3[] corners)
        {
            var samples=new List<Vector3>{corners[0]};
            for(int i=1;i<corners.Length-1;i++)
            {
                Vector3 incoming=corners[i]-corners[i-1],outgoing=corners[i+1]-corners[i];
                float radius=Mathf.Min(1.0f,Mathf.Min(incoming.magnitude,outgoing.magnitude)*.42f);
                Vector3 start=corners[i]-incoming.normalized*radius,end=corners[i]+outgoing.normalized*radius;
                samples.Add(start);
                for(int part=1;part<=8;part++)
                {
                    float t=part/8f; samples.Add((1-t)*(1-t)*start+2*(1-t)*t*corners[i]+t*t*end);
                }
            }
            samples.Add(corners[corners.Length-1]);points=samples.ToArray();distances=new float[points.Length];
            for(int i=1;i<points.Length;i++)distances[i]=distances[i-1]+Vector3.Distance(points[i-1],points[i]);
        }
        internal Vector3 Sample(float progress,out Vector3 direction,out float distance)
        {
            distance=Mathf.Clamp01(progress)*Length;int segment=1;
            while(segment<distances.Length-1&&distances[segment]<distance)segment++;
            direction=(points[segment]-points[segment-1]).normalized;
            return Vector3.Lerp(points[segment-1],points[segment],Mathf.InverseLerp(distances[segment-1],distances[segment],distance));
        }
        internal static ClinicParkingPath Enter(int bay)
        {
            Vector3 end=ClinicParkingLayout.BayCenter(bay);
            return new ClinicParkingPath(P(-29,-8.2f),P(-11.35f,-8.2f),P(-11.35f,-6.05f),P(-10.6f,-4.8f),P(-10.6f,end.z),end);
        }
        internal static ClinicParkingPath Reverse(int bay)
        {
            Vector3 start=ClinicParkingLayout.BayCenter(bay);
            return new ClinicParkingPath(start,P(-10.6f,start.z),P(-10.6f,start.z+1.15f));
        }
        internal static ClinicParkingPath Exit(int bay)
        {
            float z=ClinicParkingLayout.BayCenter(bay).z;
            return new ClinicParkingPath(P(-10.6f,z+1.15f),P(-10.6f,-4.8f),P(-8.7f,-5.55f),P(-8.7f,-8.2f),P(29,-8.2f));
        }
        private static Vector3 P(float x,float z)=>ClinicParkingLayout.Point(x,z);
    }
}
