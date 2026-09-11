using System;
using UnityEngine;
namespace OrbitOrchard.App
{
    public static class OrchardColors
    {
        /// <summary>OKLCH authored palette converted into sRGB for Unity UI.</summary>
        public static Color FromOKLCH(double lightness, double chroma, double hue)
        {
            var a = chroma * Math.Cos(hue * Math.PI / 180);
            var b = chroma * Math.Sin(hue * Math.PI / 180);
            var l = Math.Pow(lightness + .3963377774 * a + .2158037573 * b, 3);
            var m = Math.Pow(lightness - .1055613458 * a - .0638541728 * b, 3);
            var s = Math.Pow(lightness - .0894841775 * a - 1.2914855480 * b, 3);
            return new Color(Gamma(4.0767416621*l - 3.3077115913*m + .2309699292*s), Gamma(-1.2684380046*l + 2.6097574011*m - .3413193965*s), Gamma(-.0041960863*l - .7034186147*m + 1.7076147010*s));
        }
        private static float Gamma(double v) { v = Math.Min(1, Math.Max(0, v)); return (float)(v <= .0031308 ? 12.92*v : 1.055*Math.Pow(v, 1/2.4)-.055); }
    }
}
