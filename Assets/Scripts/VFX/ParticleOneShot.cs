using UnityEngine;

/// <summary>
/// Ensures every ParticleSystem on this GameObject and its children plays once
/// then destroys the root GameObject. Attach to the root of any one-shot VFX prefab.
/// </summary>
public class ParticleOneShot : MonoBehaviour
{
    [Tooltip("Extra seconds to wait after the longest particle duration before destroying. Gives tails time to fade.")]
    [SerializeField] private float extraDelay = 0.5f;

    private void Awake()
    {
        float maxDuration = 0f;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.None;

            float duration = main.duration + main.startLifetime.constantMax;
            if (duration > maxDuration)
                maxDuration = duration;
        }

        Destroy(gameObject, maxDuration + extraDelay);
    }
}
