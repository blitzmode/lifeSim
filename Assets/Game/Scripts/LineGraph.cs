using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Drop this on a UI GameObject (must live under a Canvas, needs a RectTransform)
/// and it turns into a multi-line line graph with a title and Y-axis number labels.
///
/// Requires TextMeshPro (Window > TextMeshPro > Import TMP Essential Resources
/// if you haven't already used TMP in this project).
///
/// Usage:
///   LineGraph graph = someGameObject.AddComponent<LineGraph>();
///   graph.title = "Frame Time (ms)";
///   graph.series.Add(new LineGraph.LineSeries {
///       name = "Player",
///       color = Color.cyan,
///       values = new List<float> { 12, 14, 11, 20, 18 }
///   });
///   graph.Redraw();
/// </summary>
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
[ExecuteAlways]
public class LineGraph : MonoBehaviour
{
    [Serializable]
    public class LineSeries
    {
        public string name = "Series";
        public Color color = Color.cyan;
        [Min(0.1f)] public float thickness = 3f;
        public List<float> values = new List<float>();
    }

    [Header("Title")]
    public string title = "Graph Title";
    public int titleFontSize = 28;
    public Color titleColor = Color.white;
    [Tooltip("Height in pixels reserved at the top for the title.")]
    public float titleHeight = 50f;

    [Header("Y Axis")]
    [Tooltip("How many number labels to show on the Y axis (min 2).")]
    public int yAxisLabelCount = 5;
    public int yAxisFontSize = 18;
    public Color yAxisColor = Color.white;
    public string yAxisNumberFormat = "0.##";
    [Tooltip("Width in pixels reserved on the left for Y axis labels.")]
    public float yAxisWidth = 70f;

    [Header("Layout Padding")]
    public float rightPadding = 20f;
    public float bottomPadding = 20f;

    [Header("Default Line Style")]
    [Tooltip("Used for any series that has thickness <= 0.")]
    public float defaultLineThickness = 3f;

    [Header("Data")]
    public List<LineSeries> series = new List<LineSeries>();

    RectTransform _rect;
    RectTransform _plotArea;
    LineMeshGraphic _lineGraphic;
    TextMeshProUGUI _titleText;
    RectTransform _yAxisLabelsParent;
    readonly List<TextMeshProUGUI> _yAxisLabels = new List<TextMeshProUGUI>();

    bool _built;

    void Awake()
    {
        EnsureBuilt();
    }

    void OnEnable()
    {
        EnsureBuilt();
        Redraw();
    }

    void OnRectTransformDimensionsChange()
    {
        // Guards against firing before Awake has run (can happen in the editor).
        if (!_built) return;
        Redraw();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        EnsureBuilt();
        Redraw();
    }
#endif

    void EnsureBuilt()
    {
        if (_built) return;
        BuildHierarchy();
        _built = true;
    }

    void BuildHierarchy()
    {
        _rect = GetComponent<RectTransform>();

        // ---- Title ----
        GameObject titleGO = FindOrCreate("Title", transform);
        _titleText = titleGO.GetComponent<TextMeshProUGUI>();
        if (_titleText == null) _titleText = titleGO.AddComponent<TextMeshProUGUI>();
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(0, titleHeight);
        _titleText.alignment = TextAlignmentOptions.Center;

        // ---- Plot area (holds the actual drawn lines) ----
        GameObject plotGO = FindOrCreate("PlotArea", transform);
        _plotArea = plotGO.GetComponent<RectTransform>();
        _plotArea.anchorMin = Vector2.zero;
        _plotArea.anchorMax = Vector2.one;

        GameObject lineGO = FindOrCreate("Lines", _plotArea);
        _lineGraphic = lineGO.GetComponent<LineMeshGraphic>();
        if (_lineGraphic == null) _lineGraphic = lineGO.AddComponent<LineMeshGraphic>();
        RectTransform lineRect = lineGO.GetComponent<RectTransform>();
        lineRect.anchorMin = Vector2.zero;
        lineRect.anchorMax = Vector2.one;
        lineRect.pivot = Vector2.zero;
        lineRect.offsetMin = Vector2.zero;
        lineRect.offsetMax = Vector2.zero;
        _lineGraphic.color = Color.white;
        _lineGraphic.raycastTarget = false;

        // ---- Y axis label container ----
        GameObject yAxisGO = FindOrCreate("YAxisLabels", transform);
        _yAxisLabelsParent = yAxisGO.GetComponent<RectTransform>();
        _yAxisLabelsParent.anchorMin = new Vector2(0, 0);
        _yAxisLabelsParent.anchorMax = new Vector2(0, 1);
        _yAxisLabelsParent.pivot = new Vector2(0, 0.5f);

        ApplyLayout();
    }

    void ApplyLayout()
    {
        // Plot area sits to the right of the Y axis labels, below the title.
        _plotArea.offsetMin = new Vector2(yAxisWidth, bottomPadding);
        _plotArea.offsetMax = new Vector2(-rightPadding, -titleHeight);

        // Y axis label column occupies the same vertical band as the plot area.
        _yAxisLabelsParent.offsetMin = new Vector2(0, bottomPadding);
        _yAxisLabelsParent.offsetMax = new Vector2(yAxisWidth, -titleHeight);

        RectTransform titleRect = _titleText.rectTransform;
        titleRect.sizeDelta = new Vector2(0, titleHeight);
    }

    GameObject FindOrCreate(string childName, Transform parent)
    {
        Transform t = parent.Find(childName);
        if (t != null) return t.gameObject;
        GameObject go = new GameObject(childName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void SafeDestroy(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    /// <summary>Replace all series data and redraw.</summary>
    public void SetSeries(List<LineSeries> newSeries)
    {
        series = newSeries;
        Redraw();
    }

    /// <summary>Rebuilds labels and re-draws all line meshes from current data.</summary>
    public void Redraw()
    {
        EnsureBuilt();
        ApplyLayout();

        _titleText.text = title;
        _titleText.fontSize = titleFontSize;
        _titleText.color = titleColor;

        ComputeMinMax(out float min, out float max);
        DrawYAxisLabels(min, max);
        DrawLines(min, max);
    }

    void ComputeMinMax(out float min, out float max)
    {
        min = float.MaxValue;
        max = float.MinValue;
        foreach (LineSeries s in series)
        {
            if (s.values == null) continue;
            foreach (float v in s.values)
            {
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
        if (min == float.MaxValue)
        {
            min = 0f;
            max = 1f;
        }
        if (Mathf.Approximately(min, max))
        {
            min -= 1f;
            max += 1f;
        }
    }

    void DrawYAxisLabels(float min, float max)
    {
        int needed = Mathf.Max(2, yAxisLabelCount);

        // Compute a "nice" step size so labels land on round numbers.
        float rawStep = (max - min) / (needed - 1);
        float step = NiceStep(rawStep);
        float niceMin = Mathf.Floor(min / step) * step;
        float niceMax = Mathf.Ceil(max / step) * step;
        int actualCount = Mathf.RoundToInt((niceMax - niceMin) / step) + 1;

        // Resync with existing children in case of domain reload (from before)
        if (_yAxisLabels.Count == 0 && _yAxisLabelsParent.childCount > 0)
        {
            foreach (Transform child in _yAxisLabelsParent)
            {
                var t = child.GetComponent<TextMeshProUGUI>();
                if (t != null) _yAxisLabels.Add(t);
            }
        }

        while (_yAxisLabels.Count < actualCount)
        {
            GameObject go = new GameObject("YLabel" + _yAxisLabels.Count, typeof(RectTransform));
            go.transform.SetParent(_yAxisLabelsParent, false);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            _yAxisLabels.Add(tmp);
        }
        while (_yAxisLabels.Count > actualCount)
        {
            int last = _yAxisLabels.Count - 1;
            SafeDestroy(_yAxisLabels[last].gameObject);
            _yAxisLabels.RemoveAt(last);
        }

        float height = _yAxisLabelsParent.rect.height;
        for (int i = 0; i < actualCount; i++)
        {
            float value = niceMin + i * step;
            // Re-derive normalized position against the ORIGINAL min/max
            // so the label still lines up with where that value actually falls.
            float t = Mathf.InverseLerp(min, max, value);

            TextMeshProUGUI label = _yAxisLabels[i];
            label.text = value.ToString(yAxisNumberFormat);
            label.fontSize = yAxisFontSize;
            label.color = yAxisColor;

            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(1f, 0.5f);
            lr.sizeDelta = new Vector2(-6f, yAxisFontSize + 4);
            lr.anchoredPosition = new Vector2(-6f, t * height);
        }
    }

    float NiceStep(float rawStep)
    {
        if (rawStep <= 0f) return 1f;
        float magnitude = Mathf.Pow(10f, Mathf.Floor(Mathf.Log10(rawStep)));
        float residual = rawStep / magnitude;
        float niceResidual = residual < 1.5f ? 1f
                            : residual < 3f ? 2f
                            : residual < 7f ? 5f
                            : 10f;
        return niceResidual * magnitude;
    }

    void DrawLines(float min, float max)
    {
        _lineGraphic.ClearLines();

        float w = _plotArea.rect.width;
        float h = _plotArea.rect.height;

        foreach (LineSeries s in series)
        {
            if (s.values == null || s.values.Count < 2) continue;

            List<Vector2> points = new List<Vector2>(s.values.Count);
            int count = s.values.Count;
            for (int i = 0; i < count; i++)
            {
                float x = (count == 1) ? 0f : (float)i / (count - 1) * w;
                float norm = Mathf.InverseLerp(min, max, s.values[i]);
                float y = norm * h;
                points.Add(new Vector2(x, y));
            }

            float thickness = s.thickness > 0f ? s.thickness : defaultLineThickness;
            _lineGraphic.AddLine(points, s.color, thickness);
        }

        _lineGraphic.SetVerticesDirty();
    }
}

/// <summary>
/// Low-level renderer used by LineGraph. Draws an arbitrary number of
/// polylines (each with its own color and thickness) as a single UI mesh.
/// Created and driven automatically by LineGraph — no need to add this by hand.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class LineMeshGraphic : Graphic
{
    class LineData
    {
        public List<Vector2> points;
        public Color color;
        public float thickness;
    }

    readonly List<LineData> _lines = new List<LineData>();

    public void ClearLines()
    {
        _lines.Clear();
    }

    public void AddLine(List<Vector2> points, Color color, float thickness)
    {
        _lines.Add(new LineData { points = points, color = color, thickness = thickness });
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (LineData line in _lines)
        {
            DrawLine(vh, line.points, line.color, line.thickness);
        }
    }

    void DrawLine(VertexHelper vh, List<Vector2> points, Color color, float thickness)
    {
        if (points == null || points.Count < 2) return;
        float half = thickness * 0.5f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p0 = points[i];
            Vector2 p1 = points[i + 1];
            Vector2 dir = (p1 - p0).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x) * half;

            int idx = vh.currentVertCount;

            AddVert(vh, p0 - normal, color);
            AddVert(vh, p0 + normal, color);
            AddVert(vh, p1 + normal, color);
            AddVert(vh, p1 - normal, color);

            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);

            // Round joint at each interior point so segments connect cleanly.
            if (i < points.Count - 2)
            {
                AddJoint(vh, p1, half, color);
            }
        }
    }

    void AddJoint(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        const int segments = 8;
        int centerIdx = vh.currentVertCount;
        AddVert(vh, center, color);

        int firstOuterIdx = vh.currentVertCount;
        for (int s = 0; s <= segments; s++)
        {
            float angle = (s / (float)segments) * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            AddVert(vh, center + offset, color);
        }

        for (int s = 0; s < segments; s++)
        {
            vh.AddTriangle(centerIdx, firstOuterIdx + s, firstOuterIdx + s + 1);
        }
    }

    static void AddVert(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = position;
        v.color = color;
        vh.AddVert(v);
    }
}
