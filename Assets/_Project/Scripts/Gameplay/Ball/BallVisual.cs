using System.Collections;
using TMPro;
using UnityEngine;

namespace CatapultGames
{
    // Unified ball visual for queue, catapult slot, and flight.
    //
    // Visual language (readable from BOTH cameras):
    //   • Sphere  — URP Lit, full 3-D shading, color = ball color
    //   • Rings   — equatorial halos around the sphere (1/2/3 for power level)
    //               Flat cylinders at sphere equator → visible from any angle above.
    //   • Number  — TMP world-space label on top face of sphere (primary indicator)
    public class BallVisual : MonoBehaviour
    {
        private Material   _bodyMat;
        private Material   _trailMat;
        private Color      _color;

        // The ball's logical color — lets owners (e.g. the queue view) match a
        // visual to a CellColor without re-querying the queue.
        public CellColor BallColor { get; private set; }

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
            Color32 c32 = GameConstants.GetColor(color);
            Color   col = new Color(c32.r / 255f, c32.g / 255f, c32.b / 255f);
            _color      = col;
            BallColor   = color;

            var litShader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            _bodyMat = new Material(litShader) { color = col };
            if (_bodyMat.HasProperty("_Smoothness"))
                _bodyMat.SetFloat("_Smoothness", 0.30f);

            // Body mirrors what the ball PAINTS, so the stamp is recognisable in the
            // queue before it is ever fired. Reads from both the top-down grid camera
            // and the angled world camera.
            float unit = baseScale * (0.80f + powerLevel * 0.10f);

            switch (shape)
            {
                case BallShape.L:        BuildLBody(unit);                   break;
                case BallShape.Line:     BuildRunBody(unit, horizontal: true);  break;
                case BallShape.Column:   BuildRunBody(unit, horizontal: false); break;
                case BallShape.Plus:     BuildCrossBody(unit, diagonal: false); break;
                case BallShape.Diagonal: BuildCrossBody(unit, diagonal: true);  break;
                default:                 AddBlock(Vector3.zero, unit);       break;
            }

            // Each label answers "how big is this stamp" in the shape's own unit:
            // a square shows its SIDE (2x2→"2"), a run shows its LENGTH, a cross
            // shows its total cells. The L scales to the grid, so no number can
            // describe it — and its body already reads as an L.
            if (shape != BallShape.L)
                AddTopLabel(LabelFor(shape, powerLevel), unit);
        }

        private static string LabelFor(BallShape shape, int powerLevel)
        {
            var probe = new BallData(CellColor.None, powerLevel, shape);
            return shape == BallShape.Square
                ? GameConstants.GetPaintSize(powerLevel).ToString()
                : GameConstants.GetPaintCellCount(probe, 0, 0).ToString();
        }

        // One lit cube block at a local position, edge length `size`.
        private void AddBlock(Vector3 localPos, float size)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Block";
            Destroy(cube.GetComponent<BoxCollider>());
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale    = Vector3.one * size;

            var mr = cube.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows    = true;
            mr.sharedMaterial    = _bodyMat;
        }

        // Three blocks forming an L (matches the L ball's corner paint), centred on
        // the transform so it sits where a single block would.
        private void BuildLBody(float size)
        {
            float u = size * 0.62f;   // per-block edge; blocks touch to read as one L
            // L-tromino footprint in a 2x2 box: (0,0),(0,1),(1,0) — then centre it.
            AddBlock(new Vector3(-0.5f * u, 0f, -0.5f * u), u);   // corner (bend)
            AddBlock(new Vector3(-0.5f * u, 0f,  0.5f * u), u);   // up
            AddBlock(new Vector3( 0.5f * u, 0f, -0.5f * u), u);   // right
        }

        // Three blocks in a row — a symbol of the run, not its true length (a
        // power-3 Line paints 7 cells and would be an unwieldy ball). The number
        // on top carries the actual length.
        private void BuildRunBody(float size, bool horizontal)
        {
            float u = size * 0.58f;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 p = horizontal ? new Vector3(i * u, 0f, 0f) : new Vector3(0f, 0f, i * u);
                AddBlock(p, u);
            }
        }

        // Centre block plus four arms — orthogonal for Plus, corner-to-corner for
        // Diagonal. Same symbolic single-step arms as the run body.
        private void BuildCrossBody(float size, bool diagonal)
        {
            float u = size * 0.52f;
            AddBlock(Vector3.zero, u);

            if (diagonal)
            {
                AddBlock(new Vector3( u, 0f,  u), u);
                AddBlock(new Vector3(-u, 0f, -u), u);
                AddBlock(new Vector3( u, 0f, -u), u);
                AddBlock(new Vector3(-u, 0f,  u), u);
            }
            else
            {
                AddBlock(new Vector3( u, 0f, 0f), u);
                AddBlock(new Vector3(-u, 0f, 0f), u);
                AddBlock(new Vector3(0f, 0f,  u), u);
                AddBlock(new Vector3(0f, 0f, -u), u);
            }
        }

        // White number laid flat-ish on top of the body, readable from both cameras.
        private void AddTopLabel(string text, float size)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, size * 0.62f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            labelGo.transform.localScale    = Vector3.one * (size * 0.90f);

            var tmp = labelGo.AddComponent<TextMeshPro>();
            tmp.text          = text;
            tmp.fontSize      = text.Length >= 2 ? 3.6f : 5f;
            tmp.fontStyle     = FontStyles.Bold;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.color         = Color.white;
            tmp.overflowMode  = TextOverflowModes.Overflow;
        }

        // ── Flight trail ──────────────────────────────────────────────────
        // Adds a fading colored streak behind the ball. Call right after Create()
        // on the flying ball (not on queue/catapult balls).
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
        // its leftover queue balls are no longer needed. `delay` staggers a volley.
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
                // Converge toward the centre as they rise (instead of shooting
                // straight up from the far-left queue), fanning into a central
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
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_bodyMat)  Destroy(_bodyMat);
            if (_trailMat) Destroy(_trailMat);
        }
    }
}
