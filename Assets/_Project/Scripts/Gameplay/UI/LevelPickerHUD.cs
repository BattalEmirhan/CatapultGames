using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CatapultGames
{
    // Canvas-based level picker — works on mobile / Device Simulator via EventSystem.
    // Creates its own Screen Space Overlay canvas at Start() so it is self-contained.
    // Wire _loader in Inspector (done by GameplaySceneBuilder).
    public class LevelPickerHUD : MonoBehaviour
    {
        [SerializeField] private LevelLoader _loader;

        // ── Runtime state ─────────────────────────────────────────────────
        private string[]    _names   = System.Array.Empty<string>();
        private string      _current = "";

        // ── Built UI refs ─────────────────────────────────────────────────
        private GameObject        _listPanel;
        private Transform         _content;
        private TextMeshProUGUI   _toggleLabel;

        // Reference resolution for CanvasScaler (portrait)
        private const float RefW = 1080f;
        private const float RefH = 1920f;

        // ── Lifecycle ─────────────────────────────────────────────────────
        private void Start()
        {
            EnsureEventSystem();
            RefreshList();
            BuildUI();
            UpdateToggleLabel();
        }

        // Canvas buttons require an EventSystem. Create one if none exists so
        // the picker works even in scenes built before EventSystem was added.
        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var inputModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
                esGo.AddComponent(inputModuleType);
            else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            Debug.Log("[LevelPickerHUD] Created EventSystem automatically.");
        }

        // Called by LevelLoader after each successful load
        public void SetCurrent(string name)
        {
            _current = name;
            UpdateToggleLabel();
        }

        // ── UI Construction ───────────────────────────────────────────────
        private void BuildUI()
        {
            // ── Canvas ────────────────────────────────────────────────────
            var cgo     = new GameObject("LevelPickerCanvas");
            cgo.transform.SetParent(transform);
            var canvas  = cgo.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;  // above everything else

            var scaler             = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode     = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight  = 0.5f;

            cgo.AddComponent<GraphicRaycaster>();

            // ── Safe area panel ───────────────────────────────────────────
            var safeGo  = new GameObject("SafeArea");
            safeGo.transform.SetParent(cgo.transform, false);
            safeGo.AddComponent<SafeAreaFitter>();
            // SafeAreaFitter will set correct anchors; keep offsets at zero

            // ── Toggle button — top-right of safe area ────────────────────
            var btnGo    = new GameObject("LevelsToggle");
            btnGo.transform.SetParent(safeGo.transform, false);

            var btnImg   = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.10f, 0.10f, 0.16f, 0.92f);

            var btn      = btnGo.AddComponent<Button>();
            var btnCols  = btn.colors;
            btnCols.normalColor      = new Color(0.10f, 0.10f, 0.16f, 0.92f);
            btnCols.highlightedColor = new Color(0.20f, 0.30f, 0.55f, 0.97f);
            btnCols.pressedColor     = new Color(0.25f, 0.40f, 0.70f, 1.00f);
            btn.colors               = btnCols;
            btn.onClick.AddListener(TogglePanel);

            var btnRT           = btnGo.GetComponent<RectTransform>();
            btnRT.anchorMin     = new Vector2(1f, 1f);
            btnRT.anchorMax     = new Vector2(1f, 1f);
            btnRT.pivot         = new Vector2(1f, 1f);
            btnRT.sizeDelta     = new Vector2(300f, 90f);   // large enough for mobile tap
            btnRT.anchoredPosition = new Vector2(-24f, -24f);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(btnGo.transform, false);
            _toggleLabel            = lblGo.AddComponent<TextMeshProUGUI>();
            _toggleLabel.fontSize   = 42f;
            _toggleLabel.fontStyle  = FontStyles.Bold;
            _toggleLabel.color      = Color.white;
            _toggleLabel.alignment  = TextAlignmentOptions.Center;
            var lblRT           = lblGo.GetComponent<RectTransform>();
            lblRT.anchorMin     = Vector2.zero;
            lblRT.anchorMax     = Vector2.one;
            lblRT.offsetMin     = new Vector2(8f, 0f);
            lblRT.offsetMax     = new Vector2(-8f, 0f);

            // ── List panel — below toggle button ──────────────────────────
            _listPanel = new GameObject("LevelList");
            _listPanel.transform.SetParent(safeGo.transform, false);

            var panelImg         = _listPanel.AddComponent<Image>();
            panelImg.color       = new Color(0.07f, 0.07f, 0.12f, 0.96f);

            var panelRT          = _listPanel.GetComponent<RectTransform>();
            panelRT.anchorMin    = new Vector2(1f, 1f);
            panelRT.anchorMax    = new Vector2(1f, 1f);
            panelRT.pivot        = new Vector2(1f, 1f);
            panelRT.sizeDelta    = new Vector2(340f, 800f);
            panelRT.anchoredPosition = new Vector2(-24f, -124f);  // 24 margin + 90 btn + 10 gap

            // ScrollRect
            var scroll             = _listPanel.AddComponent<ScrollRect>();
            scroll.horizontal      = false;
            scroll.vertical        = true;
            scroll.scrollSensitivity = 40f;
            scroll.movementType    = ScrollRect.MovementType.Clamped;

            // Viewport — clip with RectMask2D, NOT Mask. A Mask whose graphic is
            // fully transparent (Color.clear) fails to write the stencil, which
            // clips every child out — the rows render invisibly. RectMask2D clips
            // by rectangle with no graphic/stencil, so the rows stay visible.
            var vpGo = new GameObject("Viewport");
            vpGo.transform.SetParent(_listPanel.transform, false);
            var vpImg = vpGo.AddComponent<Image>();   // also gives us the RectTransform
            vpImg.color        = Color.clear;
            vpImg.raycastTarget = false;
            vpGo.AddComponent<RectMask2D>();
            var vpRT         = vpGo.GetComponent<RectTransform>();
            vpRT.anchorMin   = Vector2.zero;
            vpRT.anchorMax   = Vector2.one;
            vpRT.offsetMin   = new Vector2(4f,  4f);
            vpRT.offsetMax   = new Vector2(-4f, -4f);
            scroll.viewport  = vpRT;

            // Content with VerticalLayoutGroup
            var ctGo = new GameObject("Content");
            ctGo.transform.SetParent(vpGo.transform, false);
            var vlg                  = ctGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing              = 6f;
            vlg.padding              = new RectOffset(6, 6, 6, 6);
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = true;   // size rows from their LayoutElement
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            var csf                  = ctGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit          = ContentSizeFitter.FitMode.PreferredSize;
            var ctRT         = ctGo.GetComponent<RectTransform>();
            ctRT.anchorMin   = new Vector2(0f, 1f);
            ctRT.anchorMax   = new Vector2(1f, 1f);
            ctRT.pivot       = new Vector2(0f, 1f);
            ctRT.offsetMin   = ctRT.offsetMax = Vector2.zero;
            scroll.content   = ctRT;
            _content         = ctGo.transform;

            _listPanel.SetActive(false);

            // Populate once
            PopulateList();
        }

        // ── Populate list ─────────────────────────────────────────────────
        private void PopulateList()
        {
            if (_content == null) return;

            // Destroy existing rows
            foreach (Transform child in _content)
                Destroy(child.gameObject);

            if (_names.Length == 0)
            {
                // "No levels" placeholder
                var ph = new GameObject("NoLevels");
                ph.transform.SetParent(_content, false);
                var phText        = ph.AddComponent<TextMeshProUGUI>();  // adds RectTransform
                phText.text       = "No levels found in\nResources/Levels/";
                phText.fontSize   = 32f;
                phText.color      = new Color(0.6f, 0.6f, 0.6f);
                phText.alignment  = TextAlignmentOptions.Center;
                var phLE           = ph.AddComponent<LayoutElement>();
                phLE.minHeight     = 80f;
                phLE.preferredHeight = 80f;
                return;
            }

            foreach (var name in _names)
            {
                var n = name;  // capture

                var rowGo           = new GameObject(n);
                rowGo.transform.SetParent(_content, false);

                bool active         = n == _current;
                var img             = rowGo.AddComponent<Image>();
                img.color           = active
                    ? new Color(0.22f, 0.48f, 0.90f)
                    : new Color(0.18f, 0.18f, 0.26f);

                var rowBtn          = rowGo.AddComponent<Button>();
                var cols            = rowBtn.colors;
                cols.normalColor    = img.color;
                cols.highlightedColor = active
                    ? new Color(0.30f, 0.58f, 1.00f)
                    : new Color(0.25f, 0.25f, 0.36f);
                cols.pressedColor   = new Color(0.15f, 0.40f, 0.80f);
                rowBtn.colors       = cols;
                rowBtn.targetGraphic = img;
                rowBtn.onClick.AddListener(() => OnLevelSelected(n));

                // Fixed row height so the VerticalLayoutGroup + ContentSizeFitter
                // size the content correctly (without this rows collapse to 0).
                var rowLE            = rowGo.AddComponent<LayoutElement>();
                rowLE.minHeight      = 76f;
                rowLE.preferredHeight = 76f;
                rowLE.flexibleHeight = 0f;

                var rowRT           = rowGo.GetComponent<RectTransform>();
                rowRT.sizeDelta     = new Vector2(0f, 76f);

                var lblGo           = new GameObject("Label");
                lblGo.transform.SetParent(rowGo.transform, false);
                var lbl             = lblGo.AddComponent<TextMeshProUGUI>();
                lbl.text            = (active ? ">  " : "    ") + n;
                lbl.fontSize        = 36f;
                lbl.color           = Color.white;
                lbl.alignment       = TextAlignmentOptions.MidlineLeft;
                lbl.margin          = new Vector4(16f, 0f, 8f, 0f);
                var lRT             = lblGo.GetComponent<RectTransform>();
                lRT.anchorMin       = Vector2.zero;
                lRT.anchorMax       = Vector2.one;
                lRT.offsetMin       = lRT.offsetMax = Vector2.zero;
            }
        }

        // ── Event handlers ────────────────────────────────────────────────
        private void TogglePanel()
        {
            bool open = !_listPanel.activeSelf;
            if (open)
            {
                RefreshList();
                PopulateList();
            }
            _listPanel.SetActive(open);
            UpdateToggleLabel();
        }

        private void OnLevelSelected(string name)
        {
            if (_loader == null)
            {
                Debug.LogWarning("[LevelPickerHUD] _loader not assigned!");
                return;
            }
            _loader.LoadByName(name);
            _current = name;
            _listPanel.SetActive(false);
            UpdateToggleLabel();
            PopulateList();
        }

        // ── Helpers ───────────────────────────────────────────────────────
        private void UpdateToggleLabel()
        {
            if (_toggleLabel == null) return;
            bool open  = _listPanel != null && _listPanel.activeSelf;
            string nm  = string.IsNullOrEmpty(_current) ? "Levels" : _current;
            _toggleLabel.text = open ? "[ Close ]" : $"v  {nm}";
        }

        private void RefreshList()
        {
            // Resources.LoadAll works on every platform (incl. Android) and reads
            // the same folder the Level Editor saves into.
            var assets = Resources.LoadAll<TextAsset>("Levels");
            _names = new string[assets.Length];
            for (int i = 0; i < assets.Length; i++)
                _names[i] = assets[i].name;
            System.Array.Sort(_names);
            Debug.Log($"[LevelPickerHUD] {_names.Length} level(s) found");
        }
    }
}
