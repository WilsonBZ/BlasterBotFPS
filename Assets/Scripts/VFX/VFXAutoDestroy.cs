using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Plays a VisualEffect once then destroys the GameObject after the given duration.
/// Attach to any VFX Graph object that should act as a one-shot effect.
/// </summary>
[RequireComponent(typeof(VisualEffect))]
public class VFXAutoDestroy : MonoBehaviour
{
    [Tooltip("Seconds to wait before destroying the GameObject. Should match or exceed the VFX Graph duration.")]
    [SerializeField] private float destroyDelay = 2f;

    private VisualEffect vfx;

    private void Awake()
    {
        vfx = GetComponent<VisualEffect>();
    }

    private void OnEnable()
    {
        vfx.Stop();
        vfx.Play();
        Destroy(gameObject, destroyDelay);
    }
}
