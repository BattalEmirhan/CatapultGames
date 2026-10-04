// Compile-check shims for the full Editor check (check-editor.sh rewrites the two
// Unity-6-only members to these): EditorWindow.rootVisualElement and
// MeshGenerationContext.painter2D are newer than the reference UnityEditor 2018.
using UnityEngine;
using UnityEngine.UIElements;

namespace CatapultGames.Editor
{
    public enum ArcDirectionShim { Clockwise, CounterClockwise }
    public sealed class Painter2DShim
    {
        public float lineWidth { get; set; }
        public Color strokeColor { get; set; }
        public Color fillColor { get; set; }
        public LineJoin lineJoin { get; set; }
        public LineCap lineCap { get; set; }
        public void BeginPath() { }
        public void ClosePath() { }
        public void MoveTo(Vector2 p) { }
        public void LineTo(Vector2 p) { }
        public void Arc(Vector2 c, float r, Angle a, Angle b, ArcDirectionShim d = ArcDirectionShim.Clockwise) { }
        public void Stroke() { }
        public void Fill(FillRule rule = FillRule.NonZero) { }
    }
    public enum LineJoin { Miter, Bevel, Round }
    public enum LineCap { Butt, Round }
    public enum FillRule { NonZero, OddEven }
    public struct Angle { public Angle(float v, AngleUnit u = AngleUnit.Degree) { } public static implicit operator Angle(float degrees) => default; public static Angle Degrees(float d) => default; public static Angle Radians(float r) => default; }
    public enum AngleUnit { Degree, Radian }
    public static class ShimExtensions
    {
        public static Painter2DShim Painter2DShim(this MeshGenerationContext ctx) => new Painter2DShim();
        public static VisualElement RootVisualElementShim(this UnityEditor.EditorWindow w) => null;
    }
}
