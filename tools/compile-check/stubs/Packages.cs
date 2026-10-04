// Compile-check stubs for package assemblies (uGUI, TMP, Input System). Shape only:
// just the members this project touches. Extend when a new member is used.
using System;
using UnityEngine;
using UnityEngine.Events;

namespace UnityEngine.EventSystems
{
    public class UIBehaviour : MonoBehaviour { }
    public class EventSystem : UIBehaviour
    {
        public static EventSystem current;
        public bool IsPointerOverGameObject() => false;
        public bool IsPointerOverGameObject(int id) => false;
        public void RaycastAll(PointerEventData e, System.Collections.Generic.List<RaycastResult> r) { }
    }
    public class BaseEventData { public BaseEventData(EventSystem es) { } }
    public class PointerEventData : BaseEventData { public PointerEventData(EventSystem es) : base(es) { } public Vector2 position; }
    public struct RaycastResult { public GameObject gameObject; }
    public class BaseInputModule : UIBehaviour { }
    public class StandaloneInputModule : BaseInputModule { }
}

namespace UnityEngine.UI
{
    using UnityEngine.EventSystems;
    public class Graphic : UIBehaviour
    {
        public virtual Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => null;
        public Canvas canvas => null;
        public virtual Material material { get; set; }
        public void CrossFadeAlpha(float a, float d, bool i) { }
        public virtual void SetAllDirty() { }
        public virtual void SetVerticesDirty() { }
    }
    public class MaskableGraphic : Graphic { public bool maskable { get; set; } }
    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
        public Sprite sprite { get; set; }
        public Type type { get; set; }
        public FillMethod fillMethod { get; set; }
        public float fillAmount { get; set; }
        public int fillOrigin { get; set; }
        public bool preserveAspect { get; set; }
    }
    public class RawImage : MaskableGraphic { public Texture texture { get; set; } public Rect uvRect { get; set; } }
    public class Text : MaskableGraphic { public string text { get; set; } public int fontSize { get; set; } public Font font { get; set; } public TextAnchor alignment { get; set; } }
    public class Selectable : UIBehaviour
    {
        public bool interactable { get; set; }
        public Graphic targetGraphic { get; set; }
        public ColorBlock colors { get; set; }
    }
    public struct ColorBlock { public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor; public float colorMultiplier, fadeDuration; public static ColorBlock defaultColorBlock => default; }
    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; } = new ButtonClickedEvent();
    }
    public class Toggle : Selectable { public bool isOn { get; set; } public Graphic graphic; public class ToggleEvent : UnityEvent<bool> { } public ToggleEvent onValueChanged { get; set; } = new ToggleEvent(); public void SetIsOnWithoutNotify(bool v) { } }
    public class Slider : Selectable { public float value { get; set; } public float minValue { get; set; } public float maxValue { get; set; } public class SliderEvent : UnityEvent<float> { } public SliderEvent onValueChanged { get; set; } = new SliderEvent(); }
    public class ScrollRect : UIBehaviour { public RectTransform content { get; set; } public RectTransform viewport { get; set; } public bool horizontal { get; set; } public bool vertical { get; set; } public float scrollSensitivity { get; set; } public enum MovementType { Unrestricted, Elastic, Clamped } public MovementType movementType { get; set; } public Vector2 normalizedPosition { get; set; } public float verticalNormalizedPosition { get; set; } }
    public class Mask : UIBehaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : UIBehaviour { }
    public class LayoutGroup : UIBehaviour { public RectOffset padding { get; set; } public TextAnchor childAlignment { get; set; } }
    public class HorizontalOrVerticalLayoutGroup : LayoutGroup { public float spacing { get; set; } public bool childControlWidth { get; set; } public bool childControlHeight { get; set; } public bool childForceExpandWidth { get; set; } public bool childForceExpandHeight { get; set; } public bool childScaleWidth { get; set; } public bool childScaleHeight { get; set; } }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class GridLayoutGroup : LayoutGroup { public Vector2 cellSize { get; set; } public Vector2 spacing { get; set; } }
    public class LayoutElement : UIBehaviour { public float minHeight { get; set; } public float preferredHeight { get; set; } public float minWidth { get; set; } public float preferredWidth { get; set; } public float flexibleWidth { get; set; } public float flexibleHeight { get; set; } }
    public class ContentSizeFitter : UIBehaviour { public enum FitMode { Unconstrained, MinSize, PreferredSize } public FitMode horizontalFit { get; set; } public FitMode verticalFit { get; set; } }
    public class CanvasScaler : UIBehaviour { public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize } public ScaleMode uiScaleMode { get; set; } public Vector2 referenceResolution { get; set; } public float matchWidthOrHeight { get; set; } }
    public class GraphicRaycaster : UIBehaviour { }
    public class Dropdown : Selectable { }
    public static class LayoutRebuilder { public static void ForceRebuildLayoutImmediate(RectTransform r) { } }
}

namespace TMPro
{
    using UnityEngine.UI;
    public enum TextAlignmentOptions { TopLeft, Top, TopRight, Left, Center, Right, MidlineLeft, Midline, MidlineRight, BottomLeft, Bottom, BottomRight, CenterGeoAligned }
    [Flags] public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64 }
    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    public enum TextWrappingModes { NoWrap, Normal, PreserveWhitespace, PreserveWhitespaceNoWrap }
    public class TMP_FontAsset : ScriptableObject { }
    public class TMP_Text : MaskableGraphic
    {
        public string text { get; set; }
        public float fontSize { get; set; }
        public bool enableAutoSizing { get; set; }
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public FontStyles fontStyle { get; set; }
        public Vector4 margin { get; set; }
        public bool richText { get; set; }
        [Obsolete] public bool enableWordWrapping { get; set; }
        public TextWrappingModes textWrappingMode { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public TMP_FontAsset font { get; set; }
        public float alpha { get; set; }
        public float characterSpacing { get; set; }
        public float lineSpacing { get; set; }
        public Color32 faceColor { get; set; }
        public Color outlineColor { get; set; }
        public float outlineWidth { get; set; }
        public float preferredWidth => 0; public float preferredHeight => 0;
        public void SetText(string s) { }
        public void ForceMeshUpdate() { }
    }
    public class TextMeshProUGUI : TMP_Text { }
    public class TextMeshPro : TMP_Text { }
    public class TMP_Dropdown : Selectable
    {
        public class OptionData { public string text; public OptionData() { } public OptionData(string t) { text = t; } }
        public class DropdownEvent : UnityEvent<int> { }
        public System.Collections.Generic.List<OptionData> options { get; set; } = new();
        public int value { get; set; }
        public DropdownEvent onValueChanged { get; set; } = new DropdownEvent();
        public void ClearOptions() { }
        public void AddOptions(System.Collections.Generic.List<string> o) { }
        public void AddOptions(System.Collections.Generic.List<OptionData> o) { }
        public void SetValueWithoutNotify(int v) { }
        public void RefreshShownValue() { }
        public RectTransform template { get; set; }
        public TMP_Text captionText { get; set; }
        public TMP_Text itemText { get; set; }
    }
}

namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;
    public class InputDevice { }
    public class Pointer : InputDevice { public Vector2Control position { get; } = new Vector2Control(); }
    public class Mouse : Pointer
    {
        public static Mouse current;
        public ButtonControl leftButton { get; } = new ButtonControl();
        public ButtonControl rightButton { get; } = new ButtonControl();
        public Vector2Control scroll { get; } = new Vector2Control();
    }
    public class Touchscreen : Pointer
    {
        public static Touchscreen current;
        public TouchControl primaryTouch { get; } = new TouchControl();
    }
    public class Keyboard : InputDevice { public static Keyboard current; public KeyControl this[Key k] => new KeyControl(); public KeyControl escapeKey { get; } = new KeyControl(); }
    public enum Key { None, Space, Escape, Z, Y, Backspace }
}
namespace UnityEngine.InputSystem.Controls
{
    public class InputControl<T> where T : struct { public T ReadValue() => default; }
    public class ButtonControl : InputControl<float> { public bool isPressed => false; public bool wasPressedThisFrame => false; public bool wasReleasedThisFrame => false; }
    public class KeyControl : ButtonControl { }
    public class Vector2Control : InputControl<Vector2> { }
    public class IntegerControl : InputControl<int> { }
    public class TouchControl : InputControl<Vector2>
    {
        public ButtonControl press { get; } = new ButtonControl();
        public Vector2Control position { get; } = new Vector2Control();
        public IntegerControl touchId { get; } = new IntegerControl();
    }
}
namespace UnityEngine.InputSystem.UI { public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { } }
