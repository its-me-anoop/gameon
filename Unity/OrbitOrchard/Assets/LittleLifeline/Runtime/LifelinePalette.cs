using OrbitOrchard.App;
using UnityEngine;

namespace LittleLifeline
{
    /// <summary>Railway colours authored in OKLCH, converted to Unity's sRGB UI space.</summary>
    public static class LifelinePalette
    {
        public static readonly Color Paper = OrchardColors.FromOKLCH(.958, .021, 90);
        public static readonly Color Ink = OrchardColors.FromOKLCH(.300, .024, 184);
        public static readonly Color Green = OrchardColors.FromOKLCH(.403, .054, 149);
        public static readonly Color Ochre = OrchardColors.FromOKLCH(.679, .089, 84);
        public static readonly Color Clay = OrchardColors.FromOKLCH(.651, .082, 44);
    }
}
