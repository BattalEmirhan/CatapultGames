using System.Collections;
using UnityEngine;

namespace CatapultGames
{
    // Unified ball visual for the tray and for flight.
    //
    // The body IS the stamp: a 3x3 ball is nine little cubes, a 5-long line is
    // five cubes in a row, a plus is a plus. No numbers to decode — the player
    // reads the shape the way they read a piece in a block puzzle. The one
    // scaling rule keeps every body about the same overall size, so a 5x5 is a
    // finer grid rather than a bigger blob.
    public class BallVisual : MonoBehaviour
    {
        private Material _bodyMat;
        private Material _trailMat;
        private Color    _color;
        private int      _power;
        private BallShape _shape;

        // The ball's logical color — lets owners (e.g. the tray) match a visual
        // to a CellColor without re-querying the queue.
        public CellColor BallColor { get; private set; }

        // Does this visual still show that ball? A booster can recolour or reshape
        // a ball in place; the tray uses this to know it must rebuild the body.
        public bool Matches(BallData d) =>
            d != null && d.color == BallColor && Mathf.Clamp(d.powerLevel, 1, 3) == _power && d.shape == _shape;

        // Rainbow (CellColor.Any) balls tint each block with a different palette
        // hue. Seven shared materials, built once and kept for the app's life —
        // small, and never per-ball.
        private static Material[] _rainbowMats;
        private int _rainbowIndex;

        // ── Factory ───────────────────────────────────────────────────────
        public static BallVisual Create(Transform parent, CellColor color,
                                        int powerLevel, BallShape shape = BallShape.Square,
                                        float baseScale = 1f)
        {
            var go = new GameObject("BallVisual");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var bv = go.AddComponent<BallVisual>();
            bv.Build(color, powerLevel, shape, baseScale);
            return bv;
        }

        // ── Build ─────────────────────────────────────────────────────────
        private void Build(CellColor color, int powerLevel, BallShape shape, float baseScale)
        {
            _color    = GameConstants.GetColorF(color);
            BallColor = color;
            _power    = Mathf.Clamp(powerLevel, 1, 3);
            _shape    = shape;

            var litShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            _bodyMat = new Material(litShader) { color = _color };
            if (_bodyMat.HasProperty("_Smoothness"))
                _bodyMat.SetFloat("_Smoothness", 0.25f);   // low gloss: keep the colour readable
            if (color == CellColor.Any) EnsureRainbowMaterials(litShader);

            // Overall footprint of the body, whatever the shape. Runs and crosses
            // get a little more room because they are long and thin.
            float span = baseScale * 0.95f;
            int   power = Mathf.Clamp(powerLevel, 1, 3);

            switch (shape)
            {
                case BallShape.L:        BuildLBody(span);                                 break;
                case BallShape.Line:     BuildRun(span * 1.5f, RunLength(power), horizontal: true);  break;
                case BallShape.Column:   BuildRun(span * 1.5f, RunLength(power), horizontal: false); break;
                case BallShape.Plus:     BuildCross(span * 1.5f, power, diagonal: false);   break;
                case BallShape.Diagonal: BuildCross(span * 1.5f, power, diagonal: true);    break;
                default:                 BuildSquare(span, GameConstants.GetPaintSize(power)); break;
            }
        }

        private static int RunLength(int power) =>
            GameConstants.GetPaintCellCount(new BallData(CellColor.None, power, BallShape.Line), 0, 0);

        private static void EnsureRainbowMaterials(Shader shader)
        {
            if (_rainbowMats != null && _rainbowMats.Length > 0 && _rainbowMats[0] != null) return;
            _rainbowMats = new Material[7];
            for (int i = 0; i < 7; i++)
            {
                _rainbowMats[i] = new Material(shader) { color = GameConstants.GetColorF((CellColor)(i + 1)) };
                if (_rainbowMats[i].HasProperty("_Smoothness")) _rainbowMats[i].SetFloat("_Smoothness", 0.25f);
            }
        }

        // One lit rounded block at a local position, edge length `size`.
        private void AddBlock(Vector3 localPos, float size)
        {
            var cube = new GameObject("Block");
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale    = Vector3.one * size;
            cube.AddComponent<MeshFilter>().sharedMesh = RoundedCubeMesh.Get(0.16f, 3);

            var mr = cube.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows    = true;
            mr.sharedMaterial    = BallColor == CellColor.Any && _rainbowMats != null
                ? _rainbowMats[_rainbowIndex++ % _rainbowMats.Length]
                : _bodyMat;
        }

        // N×N mini cubes filling `span` — the real stamp at tray scale. Cell edge
        // leaves a hair of gap so the grid inside the body stays readable.
        private void BuildSquare(float span, int n)
        {
            n = Mathf.Max(1, n);
            float cell = span / n;
            float edge = cell * (n == 1 ? 1f : 0.86f);
            float start = -(n - 1) * 0.5f * cell;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                AddBlock(new Vector3(start + x * cell, 0f, start + y * cell), edge);
        }

        // A straight run of `len` cubes, real length.
        private void BuildRun(float span, int len, bool horizontal)
        {
            len = Mathf.Max(1, len);
            float cell  = span / len;
            float edge  = Mathf.Min(cell * 0.86f, span * 0.28f);
            float start = -(len - 1) * 0.5f * cell;
            for (int i = 0; i < len; i++)
            {
                float d = start + i * cell;
                AddBlock(horizontal ? new Vector3(d, 0f, 0f) : new Vector3(0f, 0f, d), edge);
            }
        }

        // Centre plus four arms of real length (1/2/3 by power).
        private void BuildCross(float span, int power, bool diagonal)
        {
            int arm   = Mathf.Clamp(power, 1, 3);
            int len   = 2 * arm + 1;
            float cell = span / len;
            float edge = Mathf.Min(cell * 0.86f, span * 0.28f);
            AddBlock(Vector3.zero, edge);
            for (int i = 1; i <= arm; i++)
            {
                float d = i * cell;
                if (diagonal)
                {
                    AddBlock(new Vector3( d, 0f,  d), edge);
                    AddBlock(new Vector3(-d, 0f, -d), edge);
                    AddBlock(new Vector3( d, 0f, -d), edge);
                    AddBlock(new Vector3(-d, 0f,  d), edge);
                }
                else
                {
                    AddBlock(new Vector3( d, 0f, 0f), edge);
                    AddBlock(new Vector3(-d, 0f, 0f), edge);
                    AddBlock(new Vector3(0f, 0f,  d), edge);
                    AddBlock(new Vector3(0f, 0f, -d), edge);
                }
            }
        }

        // Legacy L: its arms run to the grid edges, so no tray-sized body can be
        // literal. Three blocks in an L is the symbol.
        private void BuildLBody(float span)
        {
            float u = span * 0.5f;
            AddBlock(new Vector3(-0.5f * u, 0f, -0.5f * u), u);
            AddBlock(new Vector3(-0.5f * u, 0f,  0.5f * u), u);
            AddBlock(new Vector3( 0.5f * u, 0f, -0.5f * u), u);
        }

        // ── Flight trail ──────────────────────────────────────────────────
        // Adds a fading colored streak behind the ball. Call right after Create()
        // on the flying ball (not on tray balls).
        public void EnableTrail(float width)
        {
            var go = new GameObject("Trail");
            go.transform.SetParent(transform, false);

            var tr = go.AddComponent<TrailRenderer>();
            tr.time              = 0.22f;
            tr.startWidth        = width;
            tr.endWidth          = 0f;
            tr.minVertexDistance = 0.04f;
            tr.numCapVertices    = 4;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows    = false;

            var sh = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");
            _trailMat        = new Material(sh);
            tr.sharedMaterial = _trailMat;

            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(_color, 0f), new GradientColorKey(_color, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = grad;
        }

        // ── Firework (color-cleared celebration) ─────────────────────────
        // Detaches the ball, rockets it up toward the top of the screen, then pops
        // it with a colored firework burst. Used when a colour is fully painted and
        // its leftover balls are no longer needed. `delay` staggers a volley.
        public void PlayFireworkAndDestroy(float delay = 0f)
        {
            transform.SetParent(null, worldPositionStays: true);
            StartCoroutine(FireworkRoutine(delay));
        }

        private IEnumerator FireworkRoutine(float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);

            EnableTrail(0.22f);   // rocket streak

            var     cam   = Camera.main;
            Vector3 start = transform.position;
            Vector3 target;
            if (cam != null)
            {
                // Converge toward the centre as they rise, fanning into a central
                // burst. Same camera depth so size stays consistent on screen.
                Vector3 sp      = cam.WorldToScreenPoint(start);
                float   centerX = Screen.width * 0.5f;
                float   tx      = Mathf.Lerp(sp.x, centerX, 0.8f) + Random.Range(-0.05f, 0.05f) * Screen.width;
                float   ty      = Screen.height * Random.Range(0.66f, 0.86f);
                target          = cam.ScreenToWorldPoint(new Vector3(tx, ty, sp.z));
            }
            else
            {
                target = start + Vector3.up * 6f;
            }

            float dur = Random.Range(0.45f, 0.65f);
            float t   = 0f;
            while (t < dur)
            {
                float k    = t / dur;
                float ease = 1f - (1f - k) * (1f - k);   // shoot up fast, decelerate at apex
                transform.position = Vector3.Lerp(start, target, ease);
                t += Time.deltaTime;
                yield return null;
            }
            transform.position = target;

            GameFX.Instance.Firework(target, _color);
            GameAudio.Play(GameAudio.Sfx.Pop, Random.Range(0.85f, 1.2f));
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_bodyMat)  Destroy(_bodyMat);
            if (_trailMat) Destroy(_trailMat);
        }
    }
}
