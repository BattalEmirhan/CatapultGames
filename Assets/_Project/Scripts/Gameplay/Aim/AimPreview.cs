using UnityEngine;

namespace CatapultGames
{
    // Trajectory arc preview and landing cell highlight.
    //
    // Driven by TapLaunchController: it calls ShowArc() with the velocity that
    // reaches the aimed cell while the finger is down, and Hide() on release.
    //
    // Attach to a GameObject with a LineRenderer.
    // Wire up in Inspector:
    //   queue        — BallQueue (to know current ball color)
    //   grid         — GridRenderer (for highlight + PaintingSystem.Preview)
    //   launchOrigin — Transform at catapult ball position (arc start point)
    [RequireComponent(typeof(LineRenderer))]
    public sealed class AimPreview : MonoBehaviour
    {
        [SerializeField] private BallQueue    queue;
        [SerializeField] private GridRenderer grid;
        [SerializeField] private Transform    launchOrigin;
        [SerializeField] private MaterialSet  materials;

        [Header("Aim line look")]
        [Tooltip("Tint of the flowing aim dots (valid shot).")]
        [SerializeField] private Color lineColor = new Color(0.35f, 0.95f, 1f);

        [Tooltip("How fast the dots flow toward the target (world units/sec).")]
        [SerializeField] private float flowSpeed = 1.6f;

        [Header("Dotted aim (flowing dots)")]
        [Tooltip("ON: a stream of round dots travels along the arc toward the target " +
                 "(guaranteed flow). OFF: the breathing dashed LineRenderer instead.")]
        [SerializeField] private bool  useDots      = true;
        [SerializeField] private float dotSpacing    = 0.55f;  // world units between dots
        [SerializeField] private float dotStartScale = 0.22f;  // dot size near the catapult
        [SerializeField] private float dotEndScale    = 0.10f;  // dot size at the landing

        private LineRenderer _lr;

        // Arc colours: lively gradient when the shot is legal, red when it would land
        // off the grid (and so won't fire — BallLauncher).
        private Gradient  _validGradient;
        private Gradient  _invalidGradient;

        // Self-owned dashed-line material/texture (line mode).
        private Material  _lineMat;
        private Texture2D _lineTex;
        private float     _scroll;
        private const float BaseWidth = 0.30f;   // taper is in widthCurve; this is the base scale

        // Dotted mode: pooled sphere "dots" placed along the arc, marching to target.
        private readonly System.Collections.Generic.List<Transform>    _dots          = new();
        private readonly System.Collections.Generic.List<MeshRenderer> _dotRenderers  = new();
        private Material                 _dotMat;
        private MaterialPropertyBlock    _mpb;
        private Transform                _dotRoot;

        // Reused so aiming (every frame while the finger is down) doesn't allocate.
        private readonly System.Collections.Generic.HashSet<Vector2Int> _paintedLookup = new();
        private System.Collections.Generic.List<Vector3> _arcPoints;
        private bool                     _arcValid;
        private bool                     _dotsActive;
        private float                    _phase;
        private const int                MaxDots = 64;

        private void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.enabled       = false;
            StyleLine();
        }

        private void OnEnable()
        {
            if (queue != null)
                queue.OnChanged += RefreshActiveColor;
            RefreshActiveColor();
        }

        // Animate while a preview is visible.
        private void Update()
        {
            if (useDots)
            {
                if (!_dotsActive)
                    return;
                // March the dots toward the target; wrap by spacing for a seamless loop.
                _phase += flowSpeed * Time.deltaTime;
                if (dotSpacing > 0.001f)
                    _phase %= dotSpacing;
                DrawDots();
                return;
            }

            if (_lr == null || !_lr.enabled)
                return;

            // Flow the dashes (bonus: only if the shader honours texture offset).
            if (_lineMat != null)
            {
                _scroll -= flowSpeed * Time.deltaTime;
                _lineMat.mainTextureOffset = new Vector2(_scroll, 0f);
            }

            // Gentle breathing on the width — guaranteed liveliness.
            _lr.widthMultiplier = BaseWidth * (1f + 0.12f * Mathf.Sin(Time.time * 7f));
        }

        private void OnDisable()
        {
            if (queue != null)
                queue.OnChanged -= RefreshActiveColor;
            if (grid  != null)
                grid.SetActiveColor(CellColor.None);
            ClearPreview();
        }

        private void OnDestroy()
        {
            if (_lineMat)
                Destroy(_lineMat);
            if (_lineTex)
                Destroy(_lineTex);
            if (_dotMat)
                Destroy(_dotMat);
        }

        // Draw the arc + landing highlight for a launch velocity, as called by
        // TapLaunchController while the player holds a target cell.
        public void ShowArc(Vector3 velocity) =>
            ShowArc(launchOrigin ? launchOrigin.position : transform.position, velocity);

        // Origin is the selected tray ball's position (BallQueueView.CurrentLaunchOrigin);
        // it must be the same point BallLauncher.Launch is given or the arc lies.
        public void ShowArc(Vector3 origin, Vector3 velocity)
        {
            if (velocity == Vector3.zero)
            {
                ClearPreview();
                return;
            }

            // The incoming velocity already targets an exact cell centre
            // (LaunchSolver.SolveToCell), so the arc is simulated as-is — same
            // resolution as BallLauncher, therefore preview == reality.
            var arc = TrajectorySimulator.Simulate(
                origin, velocity, GameConstants.TrajectorySteps,
                GameConstants.TrajectoryTimeStep, out Vector3 landPos);

            int gx = 0, gy = 0;
            bool inGrid = grid != null && grid.WorldToGrid(landPos, out gx, out gy);

            if (useDots)
            {
                // Store the path; Update() places + flows the dots each frame.
                _arcPoints  = arc;
                _arcValid   = inGrid;
                _dotsActive = true;
                if (_lr)
                    _lr.enabled = false;
            }
            else
                DrawArc(arc, inGrid);

            HighlightLanding(inGrid, gx, gy);
        }

        // Hide the arc + clear cell highlights.
        public void Hide() => ClearPreview();

        // Builds the flowing-dots look: soft round dots tiled along the arc,
        // billboarded to the camera, tapering toward the landing, scrolling toward
        // it over time. Self-contained so it works regardless of the scene setup.
        private void StyleLine()
        {
            _lineTex = BuildDotTexture();
            _lineMat = new Material(materials.Sprite) { mainTexture = _lineTex };
            _lineMat.mainTextureScale = new Vector2(1.3f, 1f);   // dot density along the line
            _lr.sharedMaterial = _lineMat;

            _lr.textureMode       = LineTextureMode.Tile;
            _lr.alignment         = LineAlignment.View;          // ribbon always faces the camera
            _lr.numCapVertices    = 6;
            _lr.numCornerVertices = 4;
            _lr.widthCurve        = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.55f)); // taper
            _lr.widthMultiplier   = BaseWidth;
            _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            _validGradient = new Gradient();
            _validGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(lineColor, 0.45f),
                    new GradientColorKey(lineColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.90f, 0.7f),
                    new GradientAlphaKey(0.45f, 1f),
                });

            Color red = new Color(1f, 0.32f, 0.30f);
            _invalidGradient = new Gradient();
            _invalidGradient.SetKeys(
                new[] { new GradientColorKey(red, 0f), new GradientColorKey(red, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.4f, 1f) });
        }

        // Soft round dot on a transparent tile — repeats into a row of dots.
        private static Texture2D BuildDotTexture()
        {
            const int S = 32;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            Vector2 c      = new Vector2(S * 0.5f, S * 0.5f);
            float   radius = S * 0.34f;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - d / radius));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        // Tell the board which colour is loaded, so the cells this ball can paint
        // read brighter and sit a step higher. Driven off the queue rather than
        // polled — the queue already announces every change it makes.
        private void RefreshActiveColor()
        {
            if (grid == null)
                return;
            grid.SetActiveColor(queue?.Current?.color ?? CellColor.None);
        }

        private void DrawArc(System.Collections.Generic.List<Vector3> points, bool valid)
        {
            _lr.enabled       = true;
            _lr.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                _lr.SetPosition(i, points[i]);

            _lr.colorGradient = valid ? _validGradient : _invalidGradient;
        }

        // Walks the stored arc once, dropping a pooled dot every dotSpacing units
        // starting at _phase (which Update() scrolls), tapering size + colour toward
        // the landing. Spare dots are deactivated.
        private void DrawDots()
        {
            var pts = _arcPoints;
            if (pts == null || pts.Count < 2)
            {
                HideDots();
                return;
            }

            float total = 0f;
            for (int i = 1; i < pts.Count; i++)
                total += Vector3.Distance(pts[i - 1], pts[i]);
            if (total < 0.01f || dotSpacing < 0.01f)
            {
                HideDots();
                return;
            }

            Color red = new Color(1f, 0.32f, 0.30f);

            int   used     = 0;
            int   seg      = 1;
            float segStart = 0f;
            float segLen   = Vector3.Distance(pts[0], pts[1]);

            for (float dist = _phase; dist <= total && used < MaxDots; dist += dotSpacing)
            {
                while (seg < pts.Count - 1 && dist > segStart + segLen)
                {
                    segStart += segLen;
                    seg++;
                    segLen = Vector3.Distance(pts[seg - 1], pts[seg]);
                }

                float   f   = segLen > 0.0001f ? (dist - segStart) / segLen : 0f;
                Vector3 pos = Vector3.Lerp(pts[seg - 1], pts[seg], f);
                float   t   = dist / total;

                float scale = Mathf.Lerp(dotStartScale, dotEndScale, t);
                Color c = _arcValid
                    ? Color.Lerp(Color.white, lineColor, Mathf.SmoothStep(0f, 1f, t * 1.6f))
                    : red;

                PlaceDot(used++, pos, scale, c);
            }

            for (int i = used; i < _dots.Count; i++)
                if (_dots[i])
                    _dots[i].gameObject.SetActive(false);
        }

        private void PlaceDot(int i, Vector3 pos, float scale, Color color)
        {
            while (_dots.Count <= i)
                CreateDot();

            var tr = _dots[i];
            if (!tr.gameObject.activeSelf)
                tr.gameObject.SetActive(true);
            tr.position   = pos;
            tr.localScale = Vector3.one * scale;

            _mpb ??= new MaterialPropertyBlock();
            var mr = _dotRenderers[i];
            mr.GetPropertyBlock(_mpb);
            _mpb.SetColor("_BaseColor", color);   // URP Unlit
            _mpb.SetColor("_Color",     color);   // legacy Unlit/Color fallback
            mr.SetPropertyBlock(_mpb);
        }

        private void CreateDot()
        {
            if (_dotRoot == null)
            {
                _dotRoot = new GameObject("AimDots").transform;
                _dotRoot.SetParent(transform, false);
            }
            if (_dotMat == null)
                _dotMat = new Material(materials.Unlit);

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Dot_{_dots.Count}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_dotRoot, false);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial     = _dotMat;
            mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows     = false;

            _dots.Add(go.transform);
            _dotRenderers.Add(mr);
        }

        private void HideDots()
        {
            foreach (var d in _dots)
                if (d && d.gameObject.activeSelf)
                    d.gameObject.SetActive(false);
        }

        private void HighlightLanding(bool inGrid, int gx, int gy)
        {
            if (grid == null)
                return;
            grid.ClearHighlights();
            if (!inGrid)
                return;   // off-grid shot: no landing highlight (and won't fire)

            // Mark the landing cell itself.
            grid.SetHighlight(gx, gy, true);

            // Ghost-raise every cell this ball would actually paint, in its real
            // fill colour — so the player reads the painted footprint (and how many
            // cells it covers) directly, before firing.
            var current = queue?.Current;
            if (current == null)
                return;

            _paintedLookup.Clear();
            foreach (var c in PaintingSystem.Preview(grid, gx, gy, current))
            {
                grid.SetPreview(c.x, c.y, true);
                _paintedLookup.Add(c);
            }

            // ...and outline the REST of the stamp — the cells it covers but won't
            // paint. Showing only the paying cells hides where the stamp actually
            // sits; with the whole footprint drawn, the aimed cell always reads as
            // the centre of it (GameConstants.GetPaintOffset).
            foreach (var c in GameConstants.GetPaintedCells(gx, gy, current, grid.Width, grid.Height))
            {
                if (c.x == gx && c.y == gy)
                    continue;       // the landing cell owns the highlight
                if (_paintedLookup.Contains(c))
                    continue;   // already a paint ghost
                grid.SetFootprint(c.x, c.y, true);
            }
        }

        private void ClearPreview()
        {
            if (_lr)
                _lr.enabled = false;
            if (grid)
                grid.ClearHighlights();
            _dotsActive = false;
            HideDots();
        }
    }
}
