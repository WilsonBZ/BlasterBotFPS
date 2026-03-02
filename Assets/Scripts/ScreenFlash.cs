using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenFlash : MonoBehaviour
{
    public static ScreenFlash Instance { get; private set; }

    [SerializeField] private Image flashImage;
    [SerializeField] private Canvas flashCanvas;

    private Coroutine flashCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SetupFlashUI();
    }

    private void SetupFlashUI()
    {
        if (flashCanvas == null)
        {
            GameObject canvasObj = new GameObject("ScreenFlashCanvas");
            canvasObj.transform.SetParent(transform);
            flashCanvas = canvasObj.AddComponent<Canvas>();
            flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            flashCanvas.sortingOrder = 999;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            canvasObj.AddComponent<GraphicRaycaster>();
        }

        if (flashImage == null)
        {
            GameObject imageObj = new GameObject("FlashImage");
            imageObj.transform.SetParent(flashCanvas.transform, false);
            flashImage = imageObj.AddComponent<Image>();
            
            RectTransform rect = flashImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;

            flashImage.color = new Color(1f, 1f, 1f, 0f);
            flashImage.raycastTarget = false;
        }
    }

    public void Flash(Color color, float duration, float maxAlpha = 0.7f)
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(FlashCoroutine(color, duration, maxAlpha));
    }

    private IEnumerator FlashCoroutine(Color color, float duration, float maxAlpha)
    {
        color.a = maxAlpha;
        flashImage.color = color;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(maxAlpha, 0f, elapsed / duration);
            
            Color currentColor = color;
            currentColor.a = alpha;
            flashImage.color = currentColor;

            yield return null;
        }

        color.a = 0f;
        flashImage.color = color;
        flashCoroutine = null;
    }
}
