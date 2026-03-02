using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Transform cameraTransform;
    private Vector3 originalPosition;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            cameraTransform = transform;
            originalPosition = cameraTransform.localPosition;
        }
        else
        {
            Destroy(this);
        }
    }

    public void Shake(float duration, float magnitude, float frequency = 25f)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude, frequency));
    }

    private IEnumerator ShakeCoroutine(float duration, float magnitude, float frequency)
    {
        float elapsed = 0f;
        Vector3 startPosition = cameraTransform.localPosition;

        while (elapsed < duration)
        {
            float percentComplete = elapsed / duration;
            float damper = 1f - Mathf.Clamp01(percentComplete);

            float x = Mathf.PerlinNoise(Time.time * frequency, 0f) * 2f - 1f;
            float y = Mathf.PerlinNoise(0f, Time.time * frequency) * 2f - 1f;

            x *= magnitude * damper;
            y *= magnitude * damper;

            cameraTransform.localPosition = startPosition + new Vector3(x, y, 0f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        cameraTransform.localPosition = startPosition;
        shakeCoroutine = null;
    }

    public void StopShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (cameraTransform != null)
        {
            cameraTransform.localPosition = originalPosition;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
