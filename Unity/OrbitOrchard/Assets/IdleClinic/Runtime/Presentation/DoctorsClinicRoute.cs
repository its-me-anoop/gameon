using System;
using IdleClinic.Core;
using UnityEngine;

namespace IdleClinic.Presentation
{
    /// <summary>Rendering and authoritative travel duration share one directed corridor graph.</summary>
    internal static class DoctorsClinicRoute
    {
        internal static void Build(ClinicWorld world,string from,string to,bool staff,Vector3 start,Action<Vector3> add)
        {
            ClinicDoctorsNavigation.Build(from,to,staff,new ClinicDoctorsNavigation.Point(start.x,start.z),
                point=>add(new Vector3(point.x,start.y,point.z)));
        }
        internal static int Index(string anchor)
        {foreach(string part in anchor.Split('.'))if(int.TryParse(part,out int id))return id;return 0;}
    }
}
