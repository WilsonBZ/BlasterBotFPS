using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CurvedRingUI : MonoBehaviour
{
    [Header("References")]
    public ArmMount360 mount;
    public PlayerManager player;

    [Header("UI Parent (Static)")]
    [Tooltip("UI will not rotate with gun ring. If null, defaults to mount transform.")]
    public Transform uiParent;

    [Header("Arc Layout")]
    public float uiRadius = 0.6f;
    public int segments = 64;
    [Tooltip("Where the health arc starts (0° = forward).")]
    public float uiStartAngle = 0f;
    [Tooltip("Degrees covered by health arc.")]
    public float healthArcDegrees = 90f;
    [Tooltip("Degrees covered by battery arc.")]
    public float batteryArcDegrees = 90f;
    [Tooltip("Degrees gap between health and battery arcs.")]
    public float gapDegreesBetweenArcs = 2f;

    [Header("Appearance")]
    public float lineThickness = 0.03f;
    public Color healthColor = Color.green;
    public Color batteryColor = Color.cyan;
    public Color backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);

    [Header("Layer Settings")]
    public string uiLayerName = "Ring";
    public int uiLayerIndexFallback = 0;

    [Header("Numeric Text (Optional)")]
    public bool showNumericText = true;
    public Vector3 textLocalOffset = new Vector3(0, 0.1f, 0);
    public float numericFontSize = 0.12f;

    [Header("Behaviour")]
    public float updateRate = 0.05f;

    private int uiLayer;
    private float timer;

    private LineRenderer bgRenderer;
    private LineRenderer healthRenderer;
    private LineRenderer batteryRenderer;
    private TextMesh numericText;

    private Material lineMaterial;

    private void Awake()
    {
        // Resolve layer
        if (!string.IsNullOrWhiteSpace(uiLayerName) && LayerMask.NameToLayer(uiLayerName) != -1)
            uiLayer = LayerMask.NameToLayer(uiLayerName);
        else
            uiLayer = uiLayerIndexFallback;

        if (mount == null) mount = FindFirstObjectByType<ArmMount360>();
        if (player == null) player = FindFirstObjectByType<PlayerManager>();

        // static UI parent fallback
        if (uiParent == null) uiParent = (mount != null) ? mount.transform : transform;

        Shader s = Shader.Find("Sprites/Default");
        lineMaterial = (s != null) ? new Material(s) : new Material(Shader.Find("Hidden/Internal-Colored"));

        CreateRenderers();
        if (showNumericText) CreateNumericText();
    }

    private void CreateRenderers()
    {
        bgRenderer = CreateLineRenderer("UI_Background", backgroundColor);
        healthRenderer = CreateLineRenderer("UI_Health", healthColor);
        batteryRenderer = CreateLineRenderer("UI_Battery", batteryColor);

        bgRenderer.transform.SetParent(uiParent, false);
        healthRenderer.transform.SetParent(uiParent, false);
        batteryRenderer.transform.SetParent(uiParent, false);

        // full circle background
        DrawFullCircle(bgRenderer, uiRadius, segments);
    }

    private LineRenderer CreateLineRenderer(string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.layer = uiLayer;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = 0;

        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;

        lr.startWidth = lineThickness;
        lr.endWidth = lineThickness;

        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = 1000;

        return lr;
    }

    private void CreateNumericText()
    {
        GameObject go = new GameObject("UI_Text");
        go.layer = uiLayer;
        go.transform.SetParent(uiParent, false);

        go.transform.localPosition = textLocalOffset;
        numericText = go.AddComponent<TextMesh>();

        numericText.alignment = TextAlignment.Center;
        numericText.anchor = TextAnchor.MiddleCenter;
        numericText.characterSize = numericFontSize;
        numericText.fontSize = 32;
        numericText.color = Color.white;
        numericText.text = "";
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < updateRate) return;
        timer = 0;

        float healthPercent = (player != null) ? Mathf.Clamp01(player.GetHealthPercent()) : 0f;
        float batteryPercent = (mount != null && mount.battery != null)
            ? Mathf.Clamp01(mount.battery.GetPercent())
            : 0f;

        float radius = uiRadius;

        float healthStart = uiStartAngle;
        float batteryStart = uiStartAngle + healthArcDegrees + gapDegreesBetweenArcs;

        DrawArc(healthRenderer, healthPercent, radius, segments, healthArcDegrees, healthStart);
        DrawArc(batteryRenderer, batteryPercent, radius, segments, batteryArcDegrees, batteryStart);

        if (numericText != null)
        {
            int hp = Mathf.RoundToInt(healthPercent * 100f);
            int bat = Mathf.RoundToInt(batteryPercent * 100f);
            numericText.text = $"HP {hp}%\nBAT {bat}%";
        }
    }

    private void DrawFullCircle(LineRenderer lr, float radius, int segs)
    {
        lr.positionCount = segs + 1;
        float step = 360f / segs;

        for (int i = 0; i <= segs; i++)
        {
            float angle = Mathf.Deg2Rad * (i * step);
            lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
        }
    }

    private void DrawArc(LineRenderer lr, float percent, float radius, int segs, float arcDegrees, float startDeg)
    {
        int usedSegs = Mathf.Max(4, segs);
        percent = Mathf.Clamp01(percent);

        float endDeg = startDeg + arcDegrees * percent;
        int points = Mathf.CeilToInt(usedSegs * percent) + 1;
        lr.positionCount = points;

        for (int i = 0; i < points; i++)
        {
            float t = (float)i / (points - 1);
            float angle = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;

            Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            lr.SetPosition(i, p);
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    private void OnValidate()
    {
        segments = Mathf.Clamp(segments, 8, 512);
        uiRadius = Mathf.Max(0.05f, uiRadius);
        lineThickness = Mathf.Max(0.001f, lineThickness);
        updateRate = Mathf.Max(0.01f, updateRate);
    }
}
