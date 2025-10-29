using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class VendorIndicator : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Transform to point to (vendor).")]
    public Transform target;

    [Tooltip("If null, will attempt to find first GunVendor in scene and use its transform.")]
    public GunVendor vendorReference;

    [Header("UI References")]
    public Image arrowImage;        // arrow icon placed somewhere in canvas (centered recommended)
    public Text distanceText;       // optional; shows distance
    public RectTransform worldMarkerPrefab; // small UI element that will be instantiated as a world-space marker
    [Tooltip("Parent for instantiated world marker (should be a Screen Space - Camera or World space canvas)")]
    public RectTransform worldMarkerParent;

    [Header("Behavior")]
    public float interactRange = 2.5f;    // within this range we consider 'close'
    public float edgeMargin = 40f;        // keep arrow inset from screen edge
    public float arrowScaleAtClose = 0.8f;
    public float arrowScaleAtFar = 1.15f;
    public float maxDisplayDistance = 200f; // clamp distance display

    [Header("Smoothing")]
    public float rotationSmoothTime = 0.075f;
    public float positionLerp = 0.2f;

    // runtime
    private Camera mainCam;
    private RectTransform canvasRect;
    private Canvas parentCanvas;
    private RectTransform arrowRect;
    private RectTransform worldMarkerInstance;
    private CanvasGroup canvasGroup;
    private float rotationVelocity;

    void Awake()
    {
        mainCam = Camera.main;
        canvasGroup = GetComponent<CanvasGroup>();

        // try to get canvas
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
            Debug.LogError("VendorIndicator must be under a Canvas.");

        canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (arrowImage != null)
            arrowRect = arrowImage.GetComponent<RectTransform>();

        // find vendor if not assigned
        if (vendorReference == null)
        {
            var vendor = FindFirstObjectByType<GunVendor>();
            if (vendor != null)
                vendorReference = vendor;
        }

        if (target == null && vendorReference != null)
            target = vendorReference.transform;

        // instantiate world marker (if prefab assigned)
        if (worldMarkerPrefab != null && worldMarkerParent != null)
        {
            var go = Instantiate(worldMarkerPrefab.gameObject, worldMarkerParent);
            worldMarkerInstance = go.GetComponent<RectTransform>();
            worldMarkerInstance.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (target == null)
        {
            // nothing to point to — hide everything
            SetVisible(false);
            return;
        }

        SetVisible(true);

        Vector3 worldPos = target.position;
        Vector3 camPos = mainCam.transform.position;

        float dist = Vector3.Distance(camPos, worldPos);
        dist = Mathf.Min(dist, maxDisplayDistance);

        if (distanceText != null)
            distanceText.text = $"{Mathf.RoundToInt(dist)}m";

        // determine if target is within camera viewport and in front
        Vector3 viewport = mainCam.WorldToViewportPoint(worldPos);

        bool isInFront = viewport.z > 0f;
        bool isOnScreen = isInFront && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;

        if (isOnScreen)
        {
            // hide arrow, show world marker above target
            if (arrowRect != null) arrowRect.gameObject.SetActive(false);

            if (worldMarkerInstance != null)
            {
                worldMarkerInstance.gameObject.SetActive(true);
                // convert world position to screen position for canvas
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(mainCam, worldPos + Vector3.up * 1.6f); // offset above vendor
                Vector2 localPoint;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out localPoint);

                // lerp marker position for smoothing
                worldMarkerInstance.anchoredPosition = Vector2.Lerp(worldMarkerInstance.anchoredPosition, localPoint, positionLerp);
            }
        }
        else
        {
            // off-screen: show arrow at edge pointing toward vendor
            if (arrowRect != null) arrowRect.gameObject.SetActive(true);
            if (worldMarkerInstance != null) worldMarkerInstance.gameObject.SetActive(false);

            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector3 dir = (mainCam.WorldToScreenPoint(worldPos) - new Vector3(screenCenter.x, screenCenter.y, 0f)).normalized;

            // compute position at screen edge with margin
            float halfW = Screen.width / 2f - edgeMargin;
            float halfH = Screen.height / 2f - edgeMargin;

            // project direction onto screen rectangle
            float ratio = Mathf.Max(Mathf.Abs(dir.x / (halfW / screenCenter.x)), Mathf.Abs(dir.y / (halfH / screenCenter.y)));
            Vector3 edgePoint = new Vector3(screenCenter.x + dir.x * halfW, screenCenter.y + dir.y * halfH, 0f);

            // convert to local point in canvas
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(edgePoint.x, edgePoint.y), parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : mainCam, out localPoint);

            if (arrowRect != null)
            {
                // set position
                arrowRect.anchoredPosition = Vector2.Lerp(arrowRect.anchoredPosition, localPoint, positionLerp);

                // rotate arrow to face direction
                float targetAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                // arrow art usually points up; adjust if your arrow points right/forward
                float arrowOffset = -90f; // assume arrow points up; change if your sprite faces right
                float desired = targetAngle + arrowOffset;
                float smooth = Mathf.SmoothDampAngle(arrowRect.localEulerAngles.z, desired, ref rotationVelocity, rotationSmoothTime);
                arrowRect.localEulerAngles = new Vector3(0f, 0f, smooth);

                // scale based on distance
                float t = Mathf.Clamp01(dist / maxDisplayDistance);
                float scale = Mathf.Lerp(arrowScaleAtClose, arrowScaleAtFar, t);
                arrowRect.localScale = Vector3.one * scale;
            }
        }

        // optional: when within interact range, show hint
        if (vendorReference != null && vendorReference.playerNearby)
        {
            // vendor script already shows its prompt; optionally we can do more here
        }
    }

    void SetVisible(bool v)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
        }
    }
}
