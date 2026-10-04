using UnityEngine;

namespace CatapultGames
{
    // Soft vertical gradient behind everything — a dark night-blue, so the board
    // and its coloured cubes are the brightest thing on screen. A single unlit quad parented to the camera, far
    // down the frustum and sized to fill it, with a 1×64 gradient texture
    // generated at runtime (no asset to keep in sync with the palette).
    //
    // Attach to the camera. Re-fits when the aspect changes (rotation).
    [RequireComponent(typeof(Camera))]
    public class BackgroundGradient : MonoBehaviour
    {
        // Dark (2026-10-04, as before the light-board pass): sky/cream washed the board out.
        [SerializeField] private Color _top    = new Color(0.10f, 0.12f, 0.20f);
        [SerializeField] private Color _bottom = new Color(0.04f, 0.05f, 0.09f);
        [SerializeField] private float _distance = 60f;

        private Camera    _cam;
        private Transform _quad;
        private Material  _mat;
        private Texture2D _tex;
        private float     _lastAspect = -1f;

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            Build();
        }

        private void Build()
        {
            _tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) _tex.SetPixel(0, y, Color.Lerp(_bottom, _top, y / 63f));
            _tex.Apply();

            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            _mat = new Material(sh);
            if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", _tex);
            else                              _mat.mainTexture = _tex;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "BackgroundGradient";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, _distance);
            go.transform.localRotation = Quaternion.identity;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial    = _mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            _quad = go.transform;
            Fit();
        }

        private void LateUpdate()
        {
            if (_cam != null && !Mathf.Approximately(_cam.aspect, _lastAspect)) Fit();
        }

        // Size the quad to the frustum cross-section at _distance, with a margin
        // so camera shake never shows an edge.
        private void Fit()
        {
            if (_cam == null || _quad == null) return;
            _lastAspect = _cam.aspect;
            float h = 2f * _distance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f;
            _quad.localScale = new Vector3(h * _cam.aspect, h, 1f);
        }

        private void OnDestroy()
        {
            if (_mat) Destroy(_mat);
            if (_tex) Destroy(_tex);
        }
    }
}
