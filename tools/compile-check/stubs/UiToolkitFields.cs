// Compile-check stubs: UI Toolkit value fields that live in UnityEngine.UIElements
// in Unity 6 but in UnityEditor.UIElements in the 2021-era reference assemblies.
namespace UnityEngine.UIElements
{
    public class IntegerField : BaseField<int> { public IntegerField() : this(null) { } public IntegerField(string label) : base(label, null) { } public bool isDelayed { get; set; } }
    public class FloatField : BaseField<float> { public FloatField() : this(null) { } public FloatField(string label) : base(label, null) { } public bool isDelayed { get; set; } }
    public class Vector3Field : BaseField<Vector3> { public Vector3Field() : this(null) { } public Vector3Field(string label) : base(label, null) { } }
}
