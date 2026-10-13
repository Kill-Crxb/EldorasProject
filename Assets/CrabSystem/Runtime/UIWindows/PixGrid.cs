#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class PixGrid : MonoBehaviour
{
    [Header("Grid Resolution")]
    [SerializeField] private int referenceWidth = 1920;
    [SerializeField] private int referenceHeight = 1080;

    [Header("Grid")]
    [SerializeField] private int minorSize = 10;
    [SerializeField] private int mediumSize = 50;
    [SerializeField] private int majorSize = 100;

    [Header("Colours")]
    [SerializeField]
    private Color minorColor =
        new Color(1f, 1f, 1f, 0.18f);

    [SerializeField]
    private Color mediumColor =
        new Color(1f, 1f, 1f, 0.35f);

    [SerializeField]
    private Color majorColor =
        new Color(1f, 1f, 1f, 0.60f);

    [SerializeField]
    private Color centerXColor =
        new Color(1f, 0f, 0f, 0.95f);

    [SerializeField]
    private Color centerYColor =
        new Color(0f, 1f, 0f, 0.95f);

    [Header("Display")]
    [SerializeField] private bool showMinor = true;
    [SerializeField] private bool showMedium = true;
    [SerializeField] private bool showMajor = true;
    [SerializeField] private bool showCenter = true;

    [Header("Runtime")]
    [Tooltip("If enabled, the grid also stays visible while playing in the Editor (not in a built player).")]
    [SerializeField] private bool showInPlayMode = false;

    [Header("Line Width")]
    [SerializeField] private float minorWidth = 1f;
    [SerializeField] private float mediumWidth = 1f;
    [SerializeField] private float majorWidth = 2f;
    [SerializeField] private float centerWidth = 3f;

    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform canvasRect;
    private PixGridGraphic graphic;

    private void OnEnable()
    {
        Setup();
    }

    private void OnValidate()
    {
        referenceWidth = Mathf.Max(1, referenceWidth);
        referenceHeight = Mathf.Max(1, referenceHeight);

        minorSize = Mathf.Max(1, minorSize);
        mediumSize = Mathf.Max(minorSize, mediumSize);
        majorSize = Mathf.Max(mediumSize, majorSize);

        // NOTE: Do NOT call Setup() here.
        // AddComponent()/new GameObject() are illegal inside OnValidate
        // and will silently fail (or throw) if this fires before OnEnable,
        // which happens on the very first Inspector "Add Component".
        // Update() already detects missing refs and calls Setup() safely
        // on the next editor tick, and just marks the mesh dirty here so
        // slider/toggle tweaks still redraw immediately once it exists.

        if (graphic != null)
            graphic.MarkDirty();
    }

    private void Update()
    {
        bool shouldBeActive =
            !Application.isPlaying || showInPlayMode;

        if (shouldBeActive)
        {
            if (canvas == null ||
                scaler == null ||
                graphic == null)
            {
                Setup();
            }

            if (canvas != null &&
                canvas.enabled != true)
            {
                canvas.enabled = true;
            }

            if (graphic != null)
                graphic.MarkDirty();
        }
        else if (canvas != null &&
                 canvas.enabled != false)
        {
            canvas.enabled = false;
        }
    }

    private void Setup()
    {
        if (this == null)
            return;

        // --------------------------------------------------------
        // CANVAS
        // --------------------------------------------------------

        canvas = GetComponent<Canvas>();

        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 32767;

        canvas.pixelPerfect = true;


        // --------------------------------------------------------
        // CANVAS SCALER
        // --------------------------------------------------------

        scaler =
            GetComponent<CanvasScaler>();

        if (scaler == null)
            scaler =
                gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode =
            CanvasScaler.ScaleMode.ConstantPixelSize;

        scaler.scaleFactor = 1f;


        // --------------------------------------------------------
        // RECT TRANSFORM
        // --------------------------------------------------------

        canvasRect =
            GetComponent<RectTransform>();

        if (canvasRect != null)
        {
            canvasRect.anchorMin =
                Vector2.zero;

            canvasRect.anchorMax =
                Vector2.one;

            canvasRect.offsetMin =
                Vector2.zero;

            canvasRect.offsetMax =
                Vector2.zero;

            canvasRect.localScale =
                Vector3.one;
        }


        // --------------------------------------------------------
        // GRID GRAPHIC
        // --------------------------------------------------------

        Transform gridTransform =
            transform.Find("Grid");

        if (gridTransform == null)
        {
            GameObject gridObject =
                new GameObject(
                    "Grid",
                    typeof(RectTransform)
                );

            gridTransform =
                gridObject.transform;

            gridTransform.SetParent(
                transform,
                false
            );
        }

        // RequireComponent on Graphic (base class of PixGridGraphic)
        // does not reliably cascade through the multi-type GameObject
        // constructor. Add CanvasRenderer explicitly, before the
        // Graphic component, so it's guaranteed to exist.
        if (gridTransform.GetComponent<CanvasRenderer>() == null)
            gridTransform.gameObject.AddComponent<CanvasRenderer>();


        // --------------------------------------------------------
        // GRID RECT
        // --------------------------------------------------------

        RectTransform gridRect =
            gridTransform as RectTransform;

        if (gridRect != null)
        {
            gridRect.anchorMin =
                Vector2.zero;

            gridRect.anchorMax =
                Vector2.one;

            gridRect.offsetMin =
                Vector2.zero;

            gridRect.offsetMax =
                Vector2.zero;

            gridRect.localScale =
                Vector3.one;
        }


        // --------------------------------------------------------
        // GRID GRAPHIC
        // --------------------------------------------------------

        graphic =
            gridTransform.GetComponent<PixGridGraphic>();

        if (graphic == null)
            graphic =
                gridTransform.gameObject
                    .AddComponent<PixGridGraphic>();


        // IMPORTANT:
        // Always reconnect the owner.
        //
        // This fixes the problem after Unity recompiles,
        // reloads the domain, or opens the scene.
        // --------------------------------------------------------

        graphic.Configure(this);

        graphic.transform.SetAsLastSibling();

        canvas.enabled =
            !Application.isPlaying || showInPlayMode;
    }


    // ============================================================
    // PUBLIC SETTINGS
    // ============================================================

    public Vector2 ReferenceResolution =>
        new Vector2(
            referenceWidth,
            referenceHeight
        );

    public int MinorSize =>
        minorSize;

    public int MediumSize =>
        mediumSize;

    public int MajorSize =>
        majorSize;

    public Color MinorColor =>
        minorColor;

    public Color MediumColor =>
        mediumColor;

    public Color MajorColor =>
        majorColor;

    public Color CenterXColor =>
        centerXColor;

    public Color CenterYColor =>
        centerYColor;

    public bool ShowMinor =>
        showMinor;

    public bool ShowMedium =>
        showMedium;

    public bool ShowMajor =>
        showMajor;

    public bool ShowCenter =>
        showCenter;

    public float MinorWidth =>
        minorWidth;

    public float MediumWidth =>
        mediumWidth;

    public float MajorWidth =>
        majorWidth;

    public float CenterWidth =>
        centerWidth;
}


// =================================================================
// GRID GRAPHIC
// =================================================================

[ExecuteAlways]
public class PixGridGraphic : Graphic
{
    private PixGrid owner;

    public void Configure(PixGrid grid)
    {
        owner = grid;

        raycastTarget = false;

        // Make absolutely sure the Graphic has a valid material.
        if (material == null)
            material = Graphic.defaultGraphicMaterial;

        SetVerticesDirty();
        SetMaterialDirty();
    }

    public void MarkDirty()
    {
        if (owner != null)
            SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        raycastTarget = false;

        if (material == null)
            material = Graphic.defaultGraphicMaterial;
    }

    protected override void OnPopulateMesh(
        VertexHelper vh)
    {
        vh.Clear();

        if (owner == null)
            return;

        Rect rect =
            rectTransform.rect;

        float width =
            rect.width;

        float height =
            rect.height;

        if (width <= 0f ||
            height <= 0f)
            return;


        Vector2 reference =
            owner.ReferenceResolution;


        // --------------------------------------------------------
        // SCALE REFERENCE RESOLUTION INTO GAME VIEW
        // --------------------------------------------------------

        float scaleX =
            width /
            reference.x;

        float scaleY =
            height /
            reference.y;

        float scale =
            Mathf.Min(
                scaleX,
                scaleY
            );


        float gridWidth =
            reference.x *
            scale;

        float gridHeight =
            reference.y *
            scale;


        float offsetX =
            rect.xMin +
            (width - gridWidth) *
            0.5f;

        float offsetY =
            rect.yMin +
            (height - gridHeight) *
            0.5f;


        // --------------------------------------------------------
        // MINOR 10 PX GRID
        // --------------------------------------------------------

        if (owner.ShowMinor)
        {
            for (
                int x = 0;
                x <= reference.x;
                x += owner.MinorSize)
            {
                if (x % owner.MediumSize == 0)
                    continue;

                float px =
                    offsetX +
                    x * scale;

                AddVertical(
                    vh,
                    px,
                    offsetY,
                    gridHeight,
                    owner.MinorWidth,
                    owner.MinorColor
                );
            }


            for (
                int y = 0;
                y <= reference.y;
                y += owner.MinorSize)
            {
                if (y % owner.MediumSize == 0)
                    continue;

                float py =
                    offsetY +
                    y * scale;

                AddHorizontal(
                    vh,
                    py,
                    offsetX,
                    gridWidth,
                    owner.MinorWidth,
                    owner.MinorColor
                );
            }
        }


        // --------------------------------------------------------
        // MEDIUM 50 PX GRID
        // --------------------------------------------------------

        if (owner.ShowMedium)
        {
            for (
                int x = 0;
                x <= reference.x;
                x += owner.MediumSize)
            {
                if (x % owner.MajorSize == 0)
                    continue;

                float px =
                    offsetX +
                    x * scale;

                AddVertical(
                    vh,
                    px,
                    offsetY,
                    gridHeight,
                    owner.MediumWidth,
                    owner.MediumColor
                );
            }


            for (
                int y = 0;
                y <= reference.y;
                y += owner.MediumSize)
            {
                if (y % owner.MajorSize == 0)
                    continue;

                float py =
                    offsetY +
                    y * scale;

                AddHorizontal(
                    vh,
                    py,
                    offsetX,
                    gridWidth,
                    owner.MediumWidth,
                    owner.MediumColor
                );
            }
        }


        // --------------------------------------------------------
        // MAJOR 100 PX GRID
        // --------------------------------------------------------

        if (owner.ShowMajor)
        {
            for (
                int x = 0;
                x <= reference.x;
                x += owner.MajorSize)
            {
                float px =
                    offsetX +
                    x * scale;

                AddVertical(
                    vh,
                    px,
                    offsetY,
                    gridHeight,
                    owner.MajorWidth,
                    owner.MajorColor
                );
            }


            for (
                int y = 0;
                y <= reference.y;
                y += owner.MajorSize)
            {
                float py =
                    offsetY +
                    y * scale;

                AddHorizontal(
                    vh,
                    py,
                    offsetX,
                    gridWidth,
                    owner.MajorWidth,
                    owner.MajorColor
                );
            }
        }


        // --------------------------------------------------------
        // EXACT CENTER
        // --------------------------------------------------------

        if (owner.ShowCenter)
        {
            float centerX =
                offsetX +
                reference.x *
                0.5f *
                scale;

            float centerY =
                offsetY +
                reference.y *
                0.5f *
                scale;


            AddVertical(
                vh,
                centerX,
                offsetY,
                gridHeight,
                owner.CenterWidth,
                owner.CenterXColor
            );


            AddHorizontal(
                vh,
                centerY,
                offsetX,
                gridWidth,
                owner.CenterWidth,
                owner.CenterYColor
            );


            // Center marker

            const float markerSize = 8f;

            AddQuad(
                vh,
                new Vector2(
                    centerX - markerSize,
                    centerY - 1f
                ),
                new Vector2(
                    centerX + markerSize,
                    centerY + 1f
                ),
                Color.yellow
            );

            AddQuad(
                vh,
                new Vector2(
                    centerX - 1f,
                    centerY - markerSize
                ),
                new Vector2(
                    centerX + 1f,
                    centerY + markerSize
                ),
                Color.yellow
            );
        }
    }


    private void AddVertical(
        VertexHelper vh,
        float x,
        float y,
        float height,
        float thickness,
        Color color)
    {
        float half =
            thickness * 0.5f;

        AddQuad(
            vh,
            new Vector2(
                x - half,
                y
            ),
            new Vector2(
                x + half,
                y + height
            ),
            color
        );
    }


    private void AddHorizontal(
        VertexHelper vh,
        float y,
        float x,
        float width,
        float thickness,
        Color color)
    {
        float half =
            thickness * 0.5f;

        AddQuad(
            vh,
            new Vector2(
                x,
                y - half
            ),
            new Vector2(
                x + width,
                y + half
            ),
            color
        );
    }


    private void AddQuad(
        VertexHelper vh,
        Vector2 min,
        Vector2 max,
        Color color)
    {
        int index =
            vh.currentVertCount;


        UIVertex vertex =
            UIVertex.simpleVert;

        vertex.color =
            color;


        vertex.position =
            new Vector3(
                min.x,
                min.y,
                0f
            );

        vh.AddVert(vertex);


        vertex.position =
            new Vector3(
                min.x,
                max.y,
                0f
            );

        vh.AddVert(vertex);


        vertex.position =
            new Vector3(
                max.x,
                max.y,
                0f
            );

        vh.AddVert(vertex);


        vertex.position =
            new Vector3(
                max.x,
                min.y,
                0f
            );

        vh.AddVert(vertex);


        vh.AddTriangle(
            index,
            index + 1,
            index + 2
        );

        vh.AddTriangle(
            index,
            index + 2,
            index + 3
        );
    }
}

#endif