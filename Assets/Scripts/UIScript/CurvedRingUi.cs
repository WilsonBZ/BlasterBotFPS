using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CurvedRingUI : MonoBehaviour
{
    [Header("References")]
    public ArmMount360 mount; // optional, will try to find in parents
    public PlayerManager player; // optional, will try to find in scene

    [Header("Arc Settings")]
    public int segments = 64;
    public float innerRadius = 0.6f; // relative to mount radius; adjust in inspector
    public float thickness = 0.03f;
    public float gapDegrees = 0f; // optional gap in degrees (0 = full circle)
    public Color backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
    public Color healthColor = Color.green;
    public Color batteryColor = Color.cyan;

    [Header("Behaviour")]
    [Tooltip("How often UI updates (seconds).")]
    public float updateRate = 0.05f;
    public bool showNumericText = true;
    public Vector3 textLocalOffset = new Vector3(0f, 0.1f, 0f);
    public float numericFontSize = 0.12f;

    private LineRenderer backgroundRenderer;
    private LineRenderer healthRenderer;
    private LineRenderer batteryRenderer;
    private TextMesh numericText;

    private float timer;
    private Material lineMaterial;
    private Transform targetParent;

    private void Awake()
    {
        if (mount == null)
        {
            mount = FindFirstObjectByType<ArmMount360>();
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerManager>();
        }

        // choose parent transform for local-space drawing
        targetParent = (mount != null && mount.weaponsParent != null) ? mount.weaponsParent : transform;

        // create runtime material using sprite shader (suitable for colored lines)
        Shader s = Shader.Find("Sprites/Default");
        lineMaterial = (s != null) ? new Material(s) : new Material(Shader.Find("Hidden/Internal-Colored"));

        CreateRenderers();
        if (showNumericText) CreateNumericText();
    }

    private void CreateRenderers()
    {
        backgroundRenderer = CreateLineRenderer("RingBG", backgroundColor, thickness * 1.0f);
        healthRenderer = CreateLineRenderer("RingHealth", healthColor, thickness * 1.2f);
        batteryRenderer = CreateLineRenderer("RingBattery", batteryColor, thickness * 1.2f);

        // parent them to targetParent so they follow mount transforms
        backgroundRenderer.transform.SetParent(targetParent, false);
        healthRenderer.transform.SetParent(targetParent, false);
        batteryRenderer.transform.SetParent(targetParent, false);

        // draw initial full-circle background
        DrawArc(backgroundRenderer, 1f, innerRadius, segments, gapDegrees);
    }

    private LineRenderer CreateLineRenderer(string name, Color color, float width)
    {
        GameObject go = new GameObject(name);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = 0;
        lr.numCapVertices = 8;
        lr.numCornerVertices = 8;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sortingOrder = 1000;
        return lr;
    }

    private void CreateNumericText()
    {
        GameObject go = new GameObject("RingNumericText");
        go.transform.SetParent(targetParent, false);
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
        timer = 0f;

        float healthPercent = 0f;
        if (player != null) healthPercent = Mathf.Clamp01(player.GetHealthPercent());

        float batteryPercent = 0f;
        if (mount != null && mount.battery != null) batteryPercent = Mathf.Clamp01(mount.battery.GetPercent());

        DrawArc(healthRenderer, healthPercent, innerRadius * 0.86f, segments, gapDegrees);
        DrawArc(batteryRenderer, batteryPercent, innerRadius * 0.72f, segments, gapDegrees);

        if (numericText != null)
        {
            int hp = Mathf.RoundToInt(healthPercent * 100f);
            int bat = Mathf.RoundToInt(batteryPercent * 100f);
            numericText.text = $"HP {hp}%\nBAT {bat}%";
            numericText.transform.localPosition = textLocalOffset;
            // keep readable color blending
            numericText.color = Color.white;
        }
    }

    /// <summary>
    /// Draw arc in local XZ plane (y up). percent in [0,1].
    /// </summary>
    private void DrawArc(LineRenderer lr, float percent, float radius, int segs, float gapDeg)
    {
        if (lr == null) return;
        percent = Mathf.Clamp01(percent);

        int usedSegs = Mathf.Max(4, segs);
        float totalAngle = 360f - Mathf.Max(0f, gapDeg);
        float angleStep = totalAngle / usedSegs;
        int points = Mathf.CeilToInt(usedSegs * percent) + 1;
        if (points < 2) points = 2;

        lr.positionCount = points;
        float startAngle = gapDeg * 0.5f; // center gap around 0
        float endAngle = startAngle + totalAngle * percent;

        for (int i = 0; i < points; i++)
        {
            float t = (points == 1) ? 0f : (float)i / (points - 1);
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            // local XZ plane point (y = 0)
            Vector3 p = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            lr.SetPosition(i, p);
        }
    }

    private void OnValidate()
    {
        // Keep sane values in editor
        segments = Mathf.Clamp(segments, 8, 512);
        innerRadius = Mathf.Max(0.05f, innerRadius);
        thickness = Mathf.Max(0.001f, thickness);
        updateRate = Mathf.Max(0.01f, updateRate);
    }

    private void OnDestroy()
    {
        // cleanup created materials to avoid leaks
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
}
