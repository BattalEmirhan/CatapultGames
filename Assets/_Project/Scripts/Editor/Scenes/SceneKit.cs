using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CatapultGames.Editor
{
    // Building blocks shared by every scene builder: serialized wiring by field
    // name, the canvas convention (UiLayout), and the small UI primitives the
    // project builds in code instead of prefabs.
    internal static class SceneKit
    {
        internal const float ButtonFont = 46f;

        internal static Scene NewScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        internal static void SetRef(Object target, string field, Object value) =>
            Apply(target, field, p => p.objectReferenceValue = value);

        internal static void SetFloat(Object target, string field, float value) =>
            Apply(target, field, p => p.floatValue = value);

        internal static void SetStr(Object target, string field, string value) =>
            Apply(target, field, p => p.stringValue = value);

        internal static void SetRefArray(Object target, string field, Object[] values) =>
            Apply(target, field, p =>
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            });

        // Screen Space - Camera, never Overlay: GameScene's canvas uses the gameplay
        // camera, UIScene's the stacked overlay camera.
        internal static GameObject MakeCanvas(string name, Camera camera, int layer, int sortingOrder)
        {
            var go = new GameObject(name) { layer = layer };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode    = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera   = camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder  = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UiLayout.ReferenceResolution;
            scaler.matchWidthOrHeight  = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        internal static RectTransform NewRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            SetAnchors(r, anchorMin, anchorMax);
            return r;
        }

        internal static void SetAnchors(RectTransform r, Vector2 min, Vector2 max)
        {
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        internal static TextMeshProUGUI MakeText(Transform parent, string name, string text, float fontSize,
                                                 Vector2 anchorMin, Vector2 anchorMax)
        {
            var tmp = NewRect(parent, name, anchorMin, anchorMax).gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = fontSize;
            tmp.color         = Color.white;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        internal static Button MakeButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var rect = NewRect(parent, label + "Btn", anchorMin, anchorMax);
            rect.gameObject.AddComponent<Image>().color = color;
            var button = rect.gameObject.AddComponent<Button>();
            MakeText(rect, "Label", label, ButtonFont, Vector2.zero, Vector2.one);
            return button;
        }

        // A full-screen image that also swallows taps meant for what lies beneath.
        internal static GameObject MakeBackdrop(Transform parent, string name, Color color)
        {
            var rect = NewRect(parent, name, Vector2.zero, Vector2.one);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect.gameObject;
        }

        internal static void AddEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        internal static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        // A collider-less primitive — scenery only (the game has no physics).
        internal static void AddDeco(Transform parent, string name, PrimitiveType type, Vector3 localPos,
                                     Vector3 localScale, Material template, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(template) { color = color };
        }

        internal static int EnsureLayer(string layerName)
        {
            var tags   = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            int free   = -1;
            for (int i = 0; i < layers.arraySize; i++)
            {
                var name = layers.GetArrayElementAtIndex(i).stringValue;
                if (name == layerName)
                    return i;
                if (i >= 8 && free < 0 && string.IsNullOrEmpty(name))
                    free = i;
            }
            if (free < 0)
                return 0;
            layers.GetArrayElementAtIndex(free).stringValue = layerName;
            tags.ApplyModifiedPropertiesWithoutUndo();
            return free;
        }

        private static void Apply(Object target, string field, System.Action<SerializedProperty> write)
        {
            var so   = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[SceneKit] Field not found: {field} on {target.GetType().Name}");
                return;
            }
            write(prop);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
