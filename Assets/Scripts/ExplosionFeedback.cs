using System.Collections;
using UnityEngine;

public class ExplosionFeedback : MonoBehaviour
{
    [Header("Camera Shake")]
    [SerializeField] private float shakeIntensity = 0.5f;
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private float shakeFrequency = 30f;
    [SerializeField] private float maxShakeDistance = 15f;
    [SerializeField] private AnimationCurve shakeDistanceFalloff = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Header("Screen Flash")]
    [SerializeField] private bool useScreenFlash = true;
    [SerializeField] private Color flashColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private float flashDuration = 0.3f;
    [SerializeField] private float flashMaxAlpha = 0.5f;

    [Header("Time Effects")]
    [SerializeField] private bool useHitPause = true;
    [SerializeField] private float hitPauseDuration = 0.1f;
    [SerializeField] private float timeSlowScale = 0.3f;
    [SerializeField] private float timeSlowDuration = 0.15f;

    [Header("Audio")]
    [SerializeField] private AudioSource explosionAudioSource;
    [SerializeField] private AudioClip explosionSound;
    [Range(0f, 1f)]
    [SerializeField] private float explosionVolume = 1f;

    public void TriggerExplosion(Vector3 explosionPosition)
    {
        Debug.Log($"ExplosionFeedback.TriggerExplosion called at {explosionPosition}");
        StartCoroutine(ExplosionSequence(explosionPosition));
    }

    private IEnumerator ExplosionSequence(Vector3 explosionPosition)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            float distance = Vector3.Distance(mainCam.transform.position, explosionPosition);
            float distanceRatio = Mathf.Clamp01(distance / maxShakeDistance);
            float shakeMagnitude = shakeIntensity * shakeDistanceFalloff.Evaluate(1f - distanceRatio);

            Debug.Log($"Camera shake: distance={distance:F2}, ratio={distanceRatio:F2}, magnitude={shakeMagnitude:F2}, instance={CameraShake.Instance != null}");

            if (CameraShake.Instance != null && shakeMagnitude > 0.05f)
            {
                CameraShake.Instance.Shake(shakeDuration, shakeMagnitude, shakeFrequency);
            }
            else if (CameraShake.Instance == null)
            {
                Debug.LogWarning("CameraShake.Instance is null!");
            }
        }
        else
        {
            Debug.LogWarning("Camera.main is null!");
        }

        if (useScreenFlash && ScreenFlash.Instance != null)
        {
            ScreenFlash.Instance.Flash(flashColor, flashDuration, flashMaxAlpha);
        }

        if (explosionAudioSource != null && explosionSound != null)
        {
            explosionAudioSource.PlayOneShot(explosionSound, explosionVolume);
        }

        if (useHitPause)
        {
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(hitPauseDuration);
            
            if (timeSlowDuration > 0f)
            {
                Time.timeScale = timeSlowScale;
                yield return new WaitForSecondsRealtime(timeSlowDuration);
            }
            
            Time.timeScale = 1f;
        }
    }
}
