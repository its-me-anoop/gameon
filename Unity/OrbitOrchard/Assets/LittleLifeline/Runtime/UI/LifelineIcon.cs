using UnityEngine;
using UnityEngine.UIElements;

namespace LittleLifeline.App
{
    public enum GameGlyph
    {
        Train, Map, Crew, Depot, Coin, Star, Clinic, Diagnostics, Recovery, Plus,
        Upgrade, Arrange, Refit, Close, Play, Pause, Trophy, Palette, Sound, Haptic,
        Motion, Back, Check, Help, Clock, Person, ChevronLeft, ChevronRight, Restore,
        Garden, School, Workshop, Lighthouse, Flag
    }

    /// <summary>Small railway-inspired vector marks. The owning button supplies its accessible label.</summary>
    public sealed class LifelineIcon : VisualElement
    {
        private GameGlyph glyph;
        private Color tint;

        public GameGlyph Glyph
        {
            get => glyph;
            set { if (glyph == value) return; glyph = value; MarkDirtyRepaint(); }
        }

        public Color Tint
        {
            get => tint;
            set { if (tint == value) return; tint = value; MarkDirtyRepaint(); }
        }

        public LifelineIcon(GameGlyph glyph, Color? color = null, float size = 24)
        {
            this.glyph = glyph;
            tint = color ?? new Color(.16f, .23f, .21f, 1);
            if (float.IsNaN(size) || float.IsInfinity(size) || size <= 0) size = 24;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            focusable = false;
            AddToClassList("lifeline-icon");
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            var side = Mathf.Min(rect.width, rect.height);
            if (side <= 0 || float.IsNaN(side) || float.IsInfinity(side)) return;
            var c = new Canvas(context.painter2D, rect.center - Vector2.one * side / 2, side / 24, tint);
            switch (glyph)
            {
                case GameGlyph.Train:
                    c.RoundBox(5, 3, 14, 15, 3);
                    c.RoundBox(7.5f, 6, 9, 5, 1);
                    c.Disc(8, 14.5f, 1); c.Disc(16, 14.5f, 1);
                    c.Line(8, 18, 5, 21); c.Line(16, 18, 19, 21); c.Line(7, 20, 17, 20);
                    break;
                case GameGlyph.Map:
                    c.Shape(false, 3, 6, 8.5f, 3.5f, 15.5f, 6, 21, 3.5f, 21, 18, 15.5f, 20.5f, 8.5f, 18, 3, 20.5f);
                    c.Line(8.5f, 4.5f, 8.5f, 16.5f); c.Line(15.5f, 8, 15.5f, 19.5f);
                    break;
                case GameGlyph.Crew:
                    c.Circle(12, 7.5f, 3);
                    c.Begin(6.5f, 20); c.Curve(6.5f, 11.5f, 17.5f, 11.5f, 17.5f, 20); c.Stroke();
                    c.Begin(5.5f, 5.5f); c.Curve(1.5f, 5.5f, 1.5f, 11.5f, 5.5f, 11.5f); c.Stroke();
                    c.Begin(18.5f, 5.5f); c.Curve(22.5f, 5.5f, 22.5f, 11.5f, 18.5f, 11.5f); c.Stroke();
                    c.Begin(4.5f, 14.5f); c.Curve(2.5f, 15, 2, 17, 2, 19); c.Stroke();
                    c.Begin(19.5f, 14.5f); c.Curve(21.5f, 15, 22, 17, 22, 19); c.Stroke();
                    break;
                case GameGlyph.Depot:
                    c.Shape(false, 9.5f, 2.5f, 14.5f, 2.5f, 15, 5.3f, 17, 6.5f,
                        19.7f, 5.6f, 22, 9.6f, 19.8f, 11.5f, 19.8f, 13.5f,
                        22, 15.4f, 19.7f, 19.4f, 17, 18.5f, 15, 19.7f,
                        14.5f, 22, 9.5f, 22, 9, 19.7f, 7, 18.5f,
                        4.3f, 19.4f, 2, 15.4f, 4.2f, 13.5f, 4.2f, 11.5f,
                        2, 9.6f, 4.3f, 5.6f, 7, 6.5f, 9, 5.3f);
                    c.Circle(12, 12.2f, 3.5f);
                    break;
                case GameGlyph.Coin:
                    c.Circle(12, 12, 9); c.Circle(12, 12, 6.2f);
                    c.Shape(true, 12, 8, 15, 12, 12, 16, 9, 12);
                    break;
                case GameGlyph.Star:
                    c.Begin(12, 2.8f);
                    for (var i = 1; i < 10; i++)
                    {
                        var angle = (-90 + i * 36) * Mathf.Deg2Rad;
                        var radius = i % 2 == 0 ? 9.2f : 4.3f;
                        c.To(12 + Mathf.Cos(angle) * radius, 12 + Mathf.Sin(angle) * radius);
                    }
                    c.Close(); c.Fill();
                    break;
                case GameGlyph.Clinic:
                    c.RoundBox(3, 7, 18, 14, 2.5f); c.Path(8, 7, 8, 3.5f, 16, 3.5f, 16, 7);
                    c.Line(8, 14, 16, 14); c.Line(12, 10, 12, 18);
                    break;
                case GameGlyph.Diagnostics:
                    c.Shape(false, 9, 3, 13, 3, 13, 11, 9, 11);
                    c.Line(8, 2.5f, 14, 2.5f); c.Line(10, 13.5f, 14, 13.5f);
                    c.Begin(15, 8); c.Curve(23, 9, 20, 19, 12, 19); c.Stroke();
                    c.Line(8, 16, 13, 16); c.Line(12, 19, 12, 21); c.Line(6, 21, 20, 21);
                    break;
                case GameGlyph.Recovery:
                    c.Line(3, 7, 3, 21); c.Line(21, 11, 21, 21);
                    c.Path(3, 17, 21, 17, 21, 12, 3, 12); c.Disc(7, 9, 2);
                    c.Line(11, 9.5f, 17.5f, 9.5f);
                    break;
                case GameGlyph.Plus:
                    c.Line(12, 5, 12, 19); c.Line(5, 12, 19, 12);
                    break;
                case GameGlyph.Upgrade:
                    c.Path(6, 10, 12, 4, 18, 10); c.Line(12, 4, 12, 17); c.Line(5, 21, 19, 21);
                    break;
                case GameGlyph.Arrange:
                    c.Path(3, 7, 20, 7, 16, 3); c.Line(20, 7, 16, 11);
                    c.Path(21, 17, 4, 17, 8, 21); c.Line(4, 17, 8, 13);
                    break;
                case GameGlyph.Refit:
                    c.Begin(14.5f, 3.5f); c.Curve(17, 2, 20, 3, 21, 5);
                    c.To(17, 5.5f); c.To(16, 8.5f); c.To(18.5f, 11); c.To(22, 9.5f);
                    c.Curve(21.8f, 13.8f, 17.5f, 16, 14, 13.8f);
                    c.To(6.2f, 21); c.Curve(4, 23, 1, 19.8f, 3.2f, 17.8f);
                    c.To(10.8f, 10.5f); c.Curve(8.5f, 7.2f, 10.5f, 3.5f, 14.5f, 3.5f);
                    c.Close(); c.Stroke(); c.Disc(5.3f, 19.1f, .7f);
                    break;
                case GameGlyph.Close:
                    c.Line(6, 6, 18, 18); c.Line(18, 6, 6, 18);
                    break;
                case GameGlyph.Play:
                    c.Shape(true, 7, 3.8f, 21, 12, 7, 20.2f);
                    break;
                case GameGlyph.Pause:
                    c.RoundBox(5, 4, 4, 16, 1, true); c.RoundBox(15, 4, 4, 16, 1, true);
                    break;
                case GameGlyph.Trophy:
                    c.Begin(7, 3.5f); c.To(17, 3.5f); c.To(17, 9);
                    c.Curve(17, 16, 7, 16, 7, 9); c.Close(); c.Stroke();
                    c.Begin(7, 5); c.To(3, 5); c.Curve(2, 10, 5, 12, 8, 12); c.Stroke();
                    c.Begin(17, 5); c.To(21, 5); c.Curve(22, 10, 19, 12, 16, 12); c.Stroke();
                    c.Line(12, 14.5f, 12, 20); c.Line(7.5f, 21, 16.5f, 21);
                    break;
                case GameGlyph.Palette:
                    c.Begin(21, 11); c.Curve(21, 3, 12, 1, 6, 5); c.Curve(0, 9, 2, 19, 9, 21);
                    c.Curve(13, 22, 16, 19, 13.5f, 16.5f); c.Curve(11, 14, 13, 12, 16, 13);
                    c.Curve(19, 14, 21, 13, 21, 11); c.Close(); c.Stroke();
                    c.Disc(7, 11, 1.25f); c.Disc(10, 6.5f, 1.25f); c.Disc(16, 7, 1.25f);
                    break;
                case GameGlyph.Sound:
                    c.Shape(false, 3, 9, 7, 9, 12, 5, 12, 19, 7, 15, 3, 15);
                    c.Begin(16, 8); c.Curve(19, 10, 19, 14, 16, 16); c.Stroke();
                    c.Begin(19, 4.5f); c.Curve(24, 8.5f, 24, 15.5f, 19, 19.5f); c.Stroke();
                    break;
                case GameGlyph.Haptic:
                    c.RoundBox(7, 3, 10, 18, 2); c.Disc(12, 17.5f, .8f);
                    c.Path(3.5f, 7, 2, 10, 4, 13, 2.5f, 16);
                    c.Path(20.5f, 7, 22, 10, 20, 13, 21.5f, 16);
                    break;
                case GameGlyph.Motion:
                    c.Line(3, 5.5f, 10, 5.5f); c.Line(2, 11.5f, 7, 11.5f); c.Line(4, 17.5f, 10, 17.5f);
                    c.Begin(11, 6); c.Curve(16, 0, 23, 5, 21, 12); c.Curve(19, 21, 10, 22, 9, 16); c.Stroke();
                    c.Path(17, 11, 21, 12, 22, 8);
                    break;
                case GameGlyph.Back:
                    c.Path(10, 5, 3, 12, 10, 19); c.Line(3, 12, 21, 12);
                    break;
                case GameGlyph.Check:
                    c.Path(4, 12, 9.5f, 17.5f, 20, 6.5f);
                    break;
                case GameGlyph.Clock:
                    c.Circle(12, 12, 9); c.Path(12, 6.5f, 12, 12, 16, 14.5f);
                    break;
                case GameGlyph.Person:
                    c.Circle(12, 6.5f, 3.5f);
                    c.Begin(4.5f, 21); c.Curve(4.5f, 10.5f, 19.5f, 10.5f, 19.5f, 21); c.Stroke();
                    break;
                case GameGlyph.ChevronLeft:
                    c.Path(15.5f, 5, 8.5f, 12, 15.5f, 19);
                    break;
                case GameGlyph.ChevronRight:
                    c.Path(8.5f, 5, 15.5f, 12, 8.5f, 19);
                    break;
                case GameGlyph.Restore:
                    c.Begin(4, 9); c.Curve(7, 1, 21, 2, 21, 12);
                    c.Curve(21, 21, 9, 24, 4, 17); c.Stroke();
                    c.Path(3, 3.5f, 3, 9.5f, 9, 9.5f); c.Path(12, 7.5f, 12, 12, 16, 14);
                    break;
                case GameGlyph.Garden:
                    c.Path(6, 16, 8, 22, 16, 22, 18, 16, 6, 16);
                    c.Line(12, 16, 12, 8);
                    c.Begin(12, 11); c.Curve(5, 12, 4, 7, 4, 5);
                    c.Curve(9, 4, 12, 6, 12, 11); c.Close(); c.Stroke();
                    c.Begin(12, 8); c.Curve(12, 3, 15, 1.5f, 20, 2.5f);
                    c.Curve(20, 7, 17, 10, 12, 8); c.Close(); c.Stroke();
                    break;
                case GameGlyph.School:
                    c.Path(3, 11, 12, 5.5f, 21, 11); c.Path(5, 10, 5, 21, 19, 21, 19, 10);
                    c.RoundBox(10, 15, 4, 6, .5f); c.Disc(12, 11.5f, 1);
                    c.Path(12, 5, 12, 1.5f, 17, 1.5f, 17, 4, 12, 4);
                    break;
                case GameGlyph.Workshop:
                    c.Shape(false, 11, 3, 15, 3, 21, 9, 18, 12, 14, 8,
                        6, 20, 3, 18, 11, 6, 9, 4);
                    c.Line(3, 22, 20, 22);
                    break;
                case GameGlyph.Lighthouse:
                    c.Shape(false, 9, 10, 15, 10, 18, 22, 6, 22);
                    c.RoundBox(8, 5, 8, 5, .6f); c.Path(7, 5, 12, 1.5f, 17, 5);
                    c.Line(8, 16, 16, 16); c.Line(3, 6, 1, 5); c.Line(3, 10, 1, 11);
                    c.Line(21, 6, 23, 5); c.Line(21, 10, 23, 11);
                    break;
                case GameGlyph.Flag:
                    c.Line(5, 2, 5, 22);
                    c.Begin(5, 4); c.Curve(10, 0, 14, 8, 21, 3);
                    c.To(21, 13); c.Curve(14, 18, 10, 10, 5, 14); c.Stroke();
                    break;
                case GameGlyph.Help:
                default:
                    c.Circle(12, 12, 9);
                    c.Begin(8.5f, 8); c.Curve(9, 4, 16.5f, 5, 15, 9);
                    c.Curve(14.5f, 11, 12, 11, 12, 13.5f); c.Stroke(); c.Disc(12, 17, .9f);
                    break;
            }
        }

        /// <summary>24-unit artboard keeps stroke weights and optical padding consistent at every size.</summary>
        private readonly struct Canvas
        {
            private readonly Painter2D painter;
            private readonly Vector2 origin;
            private readonly float scale;
            public Canvas(Painter2D painter, Vector2 origin, float scale, Color color)
            {
                this.painter = painter; this.origin = origin; this.scale = scale;
                painter.strokeColor = color; painter.fillColor = color;
                painter.lineWidth = 1.7f * scale;
                painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
            }
            private Vector2 Point(float x, float y) => origin + new Vector2(x, y) * scale;
            public void Begin(float x, float y) { painter.BeginPath(); painter.MoveTo(Point(x, y)); }
            public void To(float x, float y) => painter.LineTo(Point(x, y));
            public void Curve(float a, float b, float c, float d, float x, float y)
                => painter.BezierCurveTo(Point(a, b), Point(c, d), Point(x, y));
            public void Close() => painter.ClosePath();
            public void Stroke() => painter.Stroke();
            public void Fill() => painter.Fill();
            public void Line(float x1, float y1, float x2, float y2) { Begin(x1, y1); To(x2, y2); Stroke(); }
            public void Path(params float[] points)
            {
                Begin(points[0], points[1]);
                for (var i = 2; i < points.Length; i += 2) To(points[i], points[i + 1]);
                Stroke();
            }
            public void Shape(bool fill, params float[] points)
            {
                Begin(points[0], points[1]);
                for (var i = 2; i < points.Length; i += 2) To(points[i], points[i + 1]);
                Close(); if (fill) Fill(); else Stroke();
            }
            public void Circle(float x, float y, float radius, bool filled = false)
            {
                var k = radius * .55228475f;
                Begin(x + radius, y);
                Curve(x + radius, y + k, x + k, y + radius, x, y + radius);
                Curve(x - k, y + radius, x - radius, y + k, x - radius, y);
                Curve(x - radius, y - k, x - k, y - radius, x, y - radius);
                Curve(x + k, y - radius, x + radius, y - k, x + radius, y);
                Close(); if (filled) Fill(); else Stroke();
            }
            public void Disc(float x, float y, float radius) => Circle(x, y, radius, true);
            public void RoundBox(float x, float y, float width, float height, float radius, bool fill = false)
            {
                var k = radius * .55228475f;
                Begin(x + radius, y); To(x + width - radius, y);
                Curve(x + width - radius + k, y, x + width, y + radius - k, x + width, y + radius);
                To(x + width, y + height - radius);
                Curve(x + width, y + height - radius + k, x + width - radius + k, y + height, x + width - radius, y + height);
                To(x + radius, y + height);
                Curve(x + radius - k, y + height, x, y + height - radius + k, x, y + height - radius);
                To(x, y + radius); Curve(x, y + radius - k, x + radius - k, y, x + radius, y);
                Close(); if (fill) Fill(); else Stroke();
            }
        }
    }
}
