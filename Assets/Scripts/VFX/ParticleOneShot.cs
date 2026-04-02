using System.Collections;
using UnityEngine;

/// <summary>
/// Ensures every ParticleSystem on this GameObject and its children plays once
/// then returns the root to the pool (or destroys it if not pooled).
/// Attach to the root of any one-shot VFX prefab alongside Poolable.
/// </summary>
[RequireComponent(typeof(Poolable))]
public class ParticleOneShot : MonoBehaviour
{
    [Tooltip("Extra seconds after the longest particle duration before releasing. Gives tails time to fade.")]
    [SerializeField] private float extraDelay = 0.5f;

    private Poolable poolable;
    private float cachedDuration;

    private void Awake()
    {
        poolable = GetComponent<Poolable>();

        // Cache the max duration once (particle durations don't change at runtime).
        cachedDuration = 0f;
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            main.loop       = false;
            main.stopAction = ParticleSystemStopAction.None;

            float d = main.duration + main.startLifetime.constantMax;
            if (d > cachedDuration) cachedDuration = d;
        }
    }

    private void OnEnable()
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play();
        }

        StartCoroutine(ReleaseAfterDelay(cachedDuration + extraDelay));
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator ReleaseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        poolable.Release();
    }
}
