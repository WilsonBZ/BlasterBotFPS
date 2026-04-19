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

    [Header("Cyberpunk Appearance")]
    [Tooltip("Active fill color — Cyberpunk yellow-orange.")]
    public Color healthColor = new Color(1f, 0.78f, 0.05f, 1f);
    [Tooltip("Active fill color for battery — Cyberpunk cyan.")]
    public Color batteryColor = new Color(0.08f, 0.9f, 0.95f, 1f);
    [Tooltip("Dim background track color.")]
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.14f, 0.85f);
    [Tooltip("Color used when stat is critically low (below criticalThreshold).")]
    public Color criticalColor = new Color(0.95f, 0.15f, 0.05f, 1f);
    [Tooltip("Below this fraction the arc pulses in criticalColor.")]
    [Range(0f, 0.4f)] public float criticalThreshold = 0.25f;

    [Tooltip("Thickness of the active fill arc.")]
    public float lineThickness = 0.025f;
    [Tooltip("Thickness of the background track.")]
    public float trackThickness = 0.018f;
    [Tooltip("Outward offset of the background track from the fill arc radius.")]
    public float trackRadiusOffset = 0.004f;

    [Header("Tick Marks")]
    [Tooltip("Number of tick marks along each arc.")]
    public int tickCount = 5;
    [Tooltip("Radial length of each tick.")]
    public float tickLength = 0.04f;
    [Tooltip("Thickness of tick marks.")]
    public float tickThickness = 0.006f;
    [Tooltip("Dim color of unlit ticks.")]
    public Color tickColor = new Color(1f, 0.78f, 0.05f, 0.45f);

    [Header("End Cap")]
    [Tooltip("Show a sharp angular bracket at the live tip of each arc.")]
    public bool showEndCap = true;
    [Tooltip("Size of the bracket.")]
    public float endCapSize = 0.05f;
    [Tooltip("Thickness of the bracket line.")]
    public float endCapThickness = 0.012f;

    [Header("Critical Pulse")]
    [Tooltip("Pulse frequency in Hz when below criticalThreshold.")]
    public float pulseFrequency = 3f;
    [Tooltip("Minimum alpha during a pulse cycle.")]
    [Range(0f, 1f)] public float pulseAlphaMin = 0.2f;

    [Header("Layer Settings")]
    public string uiLayerName = "Ring";
    public int uiLayerIndexFallback = 0;

    [Header("Numeric Text (Optional)")]
    public bool showNumericText = true;
    public Vector3 textLocalOffset = new Vector3(0, 0.1f, 0);
    public float numericFontSize = 0.1f;

    [Header("Behaviour")]
    public float updateRate = 0.05f;

    // ── internals ─────────────────────────────────────────────────────────────

    private int uiLayer;
    private float timer;

    private LineRenderer bgHealthRenderer;
    private LineRenderer bgBatteryRenderer;
    private LineRenderer healthRenderer;
    private LineRenderer batteryRenderer;
    private LineRenderer[] healthTicks;
    private LineRenderer[] batteryTicks;
    private LineRenderer healthCap;
    private LineRenderer batteryCap;
    private TextMesh numericText;
    private Material lineMaterial;

    private float healthPercent;
    private float batteryPercent;

    // ── lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        uiLayer = (!string.IsNullOrWhiteSpace(uiLayerName) && LayerMask.NameToLayer(uiLayerName) != -1)
            ? LayerMask.NameToLayer(uiLayerName) : uiLayerIndexFallback;

        if (mount  == null) mount  = FindFirstObjectByType<ArmMount360>();
        if (player == null) player = FindFirstObjectByType<PlayerManager>();
        if (uiParent == null) uiParent = (mount != null) ? mount.transform : transform;

        Shader s = Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/Internal-Colored");
        lineMaterial = new Material(s);
        lineMaterial.renderQueue = 4000;

        Build();
        if (showNumericText) CreateNumericText();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < updateRate) return;
        timer = 0f;

        healthPercent  = (player != null) ? Mathf.Clamp01(player.GetHealthPercent()) : 0f;
        batteryPercent = (mount != null && mount.battery != null)
            ? Mathf.Clamp01(mount.battery.GetPercent()) : 0f;

        float batteryStart = uiStartAngle;
        float healthStart  = uiStartAngle + batteryArcDegrees + gapDegreesBetweenArcs;
        float batteryEndPosition = batteryStart + batteryArcDegrees;

        Color hColor = GetArcColor(healthPercent,  healthColor)  * new Color(1, 1, 1, CriticalAlpha(healthPercent));
        Color bColor = GetArcColor(batteryPercent, batteryColor) * new Color(1, 1, 1, CriticalAlpha(batteryPercent));

        DrawArc(healthRenderer,  healthPercent,  uiRadius, segments, healthArcDegrees,  healthStart,  hColor);
        DrawArc(batteryRenderer, batteryPercent, uiRadius, segments, -batteryArcDegrees, batteryEndPosition, bColor);

        UpdateTicks(healthTicks,  healthPercent,  hColor);
        UpdateTicks(batteryTicks, batteryPercent, bColor);
        
        if (showEndCap)
        {
            UpdateEndCap(healthCap,  healthPercent,  healthArcDegrees,  healthStart,  hColor);
            UpdateEndCap(batteryCap, batteryPercent, -batteryArcDegrees, batteryEndPosition, bColor);
        }

        if (numericText != null)
        {
            int hp  = Mathf.RoundToInt(healthPercent  * 100f);
            int bat = Mathf.RoundToInt(batteryPercent * 100f);
            numericText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(batteryColor)}>BAT</color> {bat:D3}\n"
                             + $"<color=#{ColorUtility.ToHtmlStringRGB(healthColor)}>HP</color>  {hp:D3}";
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    private void OnValidate()
    {
        segments      = Mathf.Clamp(segments, 8, 512);
        uiRadius      = Mathf.Max(0.05f, uiRadius);
        lineThickness = Mathf.Max(0.001f, lineThickness);
        updateRate    = Mathf.Max(0.01f, updateRate);
        tickCount     = Mathf.Max(0, tickCount);
    }

    // ── build ─────────────────────────────────────────────────────────────────

    /// <summary>Destroys and rebuilds all renderers. Safe to call after changing layout parameters at runtime.</summary>
    public void Build()
    {
        DestroyChildren();

        float batteryStart = uiStartAngle;
        float healthStart  = uiStartAngle + batteryArcDegrees + gapDegreesBetweenArcs;
        float trackR       = uiRadius + trackRadiusOffset;

        // background tracks
        bgHealthRenderer  = MakeLine("Track_Health",  backgroundColor, trackThickness, uiParent);
        bgBatteryRenderer = MakeLine("Track_Battery", backgroundColor, trackThickness, uiParent);
        DrawFullArc(bgHealthRenderer,  trackR, segments, healthArcDegrees,  healthStart);
        DrawFullArc(bgBatteryRenderer, trackR, segments, batteryArcDegrees, batteryStart);

        // fill arcs
        healthRenderer  = MakeLine("Fill_Health",  healthColor,  lineThickness, uiParent);
        batteryRenderer = MakeLine("Fill_Battery", batteryColor, lineThickness, uiParent);

        // tick marks
        healthTicks  = BuildTicks("Tick_Health",  healthArcDegrees,  healthStart);
        batteryTicks = BuildTicks("Tick_Battery", batteryArcDegrees, batteryStart);

        // end caps
        if (showEndCap)
        {
            healthCap  = MakeLine("Cap_Health",  healthColor,  endCapThickness, uiParent);
            batteryCap = MakeLine("Cap_Battery", batteryColor, endCapThickness, uiParent);
            healthCap.positionCount  = 3;
            batteryCap.positionCount = 3;
        }
    }

    // ── drawing ───────────────────────────────────────────────────────────────

    private void DrawArc(LineRenderer lr, float percent, float radius, int segs,
                         float arcDeg, float startDeg, Color color)
    {
        percent = Mathf.Clamp01(percent);
        ApplyColor(lr, color);

        int points = Mathf.Max(2, Mathf.CeilToInt(segs * percent) + 1);
        lr.positionCount = points;
        float endDeg = startDeg + arcDeg * percent;

        for (int i = 0; i < points; i++)
        {
            float t = (points > 1) ? (float)i / (points - 1) : 0f;
            lr.SetPosition(i, ArcPoint(Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad, radius));
        }
    }

    private void DrawFullArc(LineRenderer lr, float radius, int segs, float arcDeg, float startDeg)
    {
        lr.positionCount = segs + 1;
        for (int i = 0; i <= segs; i++)
        {
            float t = (float)i / segs;
            lr.SetPosition(i, ArcPoint(Mathf.Lerp(startDeg, startDeg + arcDeg, t) * Mathf.Deg2Rad, radius));
        }
    }

    private LineRenderer[] BuildTicks(string prefix, float arcDeg, float startDeg)
    {
        var ticks = new LineRenderer[tickCount];
        for (int i = 0; i < tickCount; i++)
        {
            float t     = (tickCount > 1) ? (float)i / (tickCount - 1) : 0f;
            float angle = Mathf.Lerp(startDeg, startDeg + arcDeg, t) * Mathf.Deg2Rad;

            LineRenderer lr = MakeLine($"{prefix}_{i}", tickColor, tickThickness, uiParent);
            lr.positionCount = 2;
            lr.SetPosition(0, ArcPoint(angle, uiRadius - tickLength * 0.5f));
            lr.SetPosition(1, ArcPoint(angle, uiRadius + tickLength * 0.5f));
            ticks[i] = lr;
        }
        return ticks;
    }

    private void UpdateTicks(LineRenderer[] ticks, float percent, Color litColor)
    {
        if (ticks == null) return;
        for (int i = 0; i < ticks.Length; i++)
        {
            float fraction = (ticks.Length > 1) ? (float)i / (ticks.Length - 1) : 0f;
            ApplyColor(ticks[i], fraction <= percent ? litColor : tickColor);
        }
    }

    /// <summary>Draws a sharp Cyberpunk bracket at the live tip of the arc.</summary>
    private void UpdateEndCap(LineRenderer cap, float percent, float arcDeg, float startDeg, Color color)
    {
        if (cap == null) return;
        ApplyColor(cap, color);

        float   angleRad = (startDeg + arcDeg * Mathf.Clamp01(percent)) * Mathf.Deg2Rad;
        Vector3 radial   = new Vector3(Mathf.Cos(angleRad), 0f,  Mathf.Sin(angleRad));
        Vector3 tangent  = new Vector3(-Mathf.Sin(angleRad), 0f, Mathf.Cos(angleRad));

        cap.positionCount = 3;
        cap.SetPosition(0, radial * (uiRadius - endCapSize) + tangent * (endCapSize * 0.5f));
        cap.SetPosition(1, radial * uiRadius);
        cap.SetPosition(2, radial * (uiRadius + endCapSize) + tangent * (endCapSize * 0.5f));
    }

    // ── utilities ─────────────────────────────────────────────────────────────

    private LineRenderer MakeLine(string goName, Color color, float width, Transform parent)
    {
        var go = new GameObject(goName);
        go.layer = uiLayer;
        go.transform.SetParent(parent, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.material          = lineMaterial;
        lr.useWorldSpace     = false;
        lr.loop              = false;
        lr.positionCount     = 0;
        lr.numCapVertices    = 0;   // flat caps = sharp angular Cyberpunk look
        lr.numCornerVertices = 0;
        lr.startWidth        = width;
        lr.endWidth          = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows    = false;
        lr.sortingOrder      = 1000;
        ApplyColor(lr, color);
        return lr;
    }

    private static void ApplyColor(LineRenderer lr, Color c) { lr.startColor = c; lr.endColor = c; }

    private static Vector3 ArcPoint(float rad, float radius)
        => new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);

    private Color GetArcColor(float percent, Color baseColor)
        => percent < criticalThreshold ? criticalColor : baseColor;

    /// <summary>Returns a pulsing alpha when in critical state, 1 otherwise.</summary>
    private float CriticalAlpha(float percent)
    {
        if (percent >= criticalThreshold) return 1f;
        float pulse = (Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
        return Mathf.Lerp(pulseAlphaMin, 1f, pulse);
    }

    private void CreateNumericText()
    {
        var go = new GameObject("UI_Text");
        go.layer = uiLayer;
        go.transform.SetParent(uiParent, false);
        go.transform.localPosition = textLocalOffset;

        numericText               = go.AddComponent<TextMesh>();
        numericText.alignment     = TextAlignment.Center;
        numericText.anchor        = TextAnchor.MiddleCenter;
        numericText.characterSize = numericFontSize;
        numericText.fontSize      = 40;
        numericText.richText      = true;
        numericText.color         = Color.white;
        numericText.text          = "";
    }

    private void DestroyChildren()
    {
        foreach (string n in new[] { "Track_Health","Track_Battery","Fill_Health","Fill_Battery","Cap_Health","Cap_Battery","UI_Text" })
        {
            Transform t = uiParent != null ? uiParent.Find(n) : null;
            if (t != null) Destroy(t.gameObject);
        }

        if (healthTicks  != null) foreach (var lr in healthTicks)  if (lr) Destroy(lr.gameObject);
        if (batteryTicks != null) foreach (var lr in batteryTicks) if (lr) Destroy(lr.gameObject);

        healthTicks  = null;
        batteryTicks = null;
    }
}
