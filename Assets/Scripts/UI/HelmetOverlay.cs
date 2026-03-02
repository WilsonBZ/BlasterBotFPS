using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Procedurally builds a robotic helmet HUD overlay.
/// Attach to any persistent GameObject in the scene.
/// </summary>
public class HelmetOverlay : MonoBehaviour
{
    [Header("Color Scheme")]
    [SerializeField] private Color bracketColor = new Color(0.25f, 0.85f, 1f, 0.9f);
    [SerializeField] private Color visorTintColor = new Color(0.05f, 0.3f, 0.65f, 0.04f);
    [SerializeField] private Color edgeColor = new Color(0f, 0f, 0f, 0.6f);

    [Header("Corner Brackets")]
    [SerializeField] private float bracketLength = 55f;
    [SerializeField] private float bracketThickness = 3f;
    [SerializeField] private float bracketPadding = 20f;
    [SerializeField] private bool animateBrackets = true;
    [SerializeField] private float pulseSpeed = 1.2f;
    [SerializeField] private float pulseMinAlpha = 0.5f;

    [Header("Edge Vignette")]
    [SerializeField] private float vignetteSize = 110f;

    [Header("Post Processing")]
    [SerializeField] private bool applyPostProcessing = true;
    [SerializeField] private Color visorColorFilter = new Color(0.88f, 0.97f, 1f);
    [SerializeField] private float contrastBoost = 18f;
    [SerializeField] private float saturationShift = -8f;
    [SerializeField] private float ppVignetteIntensity = 0.3f;

    private Canvas overlayCanvas;
    private readonly System.Collections.Generic.List<Image> bracketImages = new();

    private void Awake()
    {
        BuildCanvas();
        CreateVisorTint();
        CreateEdgeVignette();
        CreateCornerBrackets();

        if (applyPostProcessing)
            ApplyPostProcessing();

        if (animateBrackets)
            StartCoroutine(PulseBrackets());
    }

    // ── Canvas Setup ──────────────────────────────────────────────────────────

    private void BuildCanvas()
    {
        GameObject root = new GameObject("HelmetCanvas");
        root.transform.SetParent(transform, false);

        overlayCanvas = root.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 50;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Disable raycasting — overlay is visual only
        GraphicRaycaster raycaster = root.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;
    }

    // ── Layers ────────────────────────────────────────────────────────────────

    private void CreateVisorTint()
    {
        Image img = CreateFullscreenImage("VisorTint");
        img.color = visorTintColor;
    }

    private void CreateEdgeVignette()
    {
        // Left
        CreateEdgePanel("VigLeft",
            new Vector2(0f, 0f), new Vector2(0f, 1f),
            new Vector2(0f, 0f), new Vector2(vignetteSize, 0f));
        // Right
        CreateEdgePanel("VigRight",
            new Vector2(1f, 0f), new Vector2(1f, 1f),
            new Vector2(-vignetteSize, 0f), new Vector2(0f, 0f));
        // Top
        CreateEdgePanel("VigTop",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, -vignetteSize), new Vector2(0f, 0f));
        // Bottom
        CreateEdgePanel("VigBottom",
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 0f), new Vector2(0f, vignetteSize));
    }

    private void CreateCornerBrackets()
    {
        // (corner anchor, inward direction)
        SpawnBracket(new Vector2(0f, 1f), new Vector2( 1f, -1f)); // top-left
        SpawnBracket(new Vector2(1f, 1f), new Vector2(-1f, -1f)); // top-right
        SpawnBracket(new Vector2(0f, 0f), new Vector2( 1f,  1f)); // bottom-left
        SpawnBracket(new Vector2(1f, 0f), new Vector2(-1f,  1f)); // bottom-right
    }

    /// <summary>Spawns an L-shaped bracket anchored to a screen corner.</summary>
    private void SpawnBracket(Vector2 corner, Vector2 inward)
    {
        float px = bracketPadding * inward.x;
        float py = bracketPadding * inward.y;

        // Horizontal arm
        RectTransform h = CreateLineRect("BracketH");
        h.anchorMin = corner;
        h.anchorMax = corner;
        h.pivot = new Vector2(corner.x == 0f ? 0f : 1f, 0.5f);
        h.sizeDelta = new Vector2(bracketLength, bracketThickness);
        h.anchoredPosition = new Vector2(px, py);
        bracketImages.Add(h.GetComponent<Image>());

        // Vertical arm
        RectTransform v = CreateLineRect("BracketV");
        v.anchorMin = corner;
        v.anchorMax = corner;
        v.pivot = new Vector2(0.5f, corner.y == 0f ? 0f : 1f);
        v.sizeDelta = new Vector2(bracketThickness, bracketLength);
        v.anchoredPosition = new Vector2(px, py);
        bracketImages.Add(v.GetComponent<Image>());
    }

    // ── Post Processing ───────────────────────────────────────────────────────

    private void ApplyPostProcessing()
    {
        GameObject volumeGo = new GameObject("HelmetPPVolume");
        volumeGo.transform.SetParent(transform, false);

        Volume volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.colorFilter.Override(visorColorFilter);
        colorAdj.contrast.Override(contrastBoost);
        colorAdj.saturation.Override(saturationShift);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.color.Override(new Color(0f, 0.02f, 0.06f));
        vignette.intensity.Override(ppVignetteIntensity);
        vignette.rounded.Override(true);
        vignette.smoothness.Override(0.65f);

        volume.profile = profile;
    }

    // ── Bracket Pulse Animation ───────────────────────────────────────────────

    private IEnumerator PulseBrackets()
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(pulseMinAlpha, bracketColor.a, t);

            foreach (Image img in bracketImages)
            {
                if (img == null) continue;
                Color c = img.color;
                c.a = alpha;
                img.color = c;
            }

            yield return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private RectTransform CreateLineRect(string objName)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(overlayCanvas.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = bracketColor;
        img.raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private Image CreateFullscreenImage(string objName)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(overlayCanvas.transform, false);
        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    private void CreateEdgePanel(string objName,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(overlayCanvas.transform, false);
        Image img = go.AddComponent<Image>();
        img.color = edgeColor;
        img.raycastTarget = false;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
