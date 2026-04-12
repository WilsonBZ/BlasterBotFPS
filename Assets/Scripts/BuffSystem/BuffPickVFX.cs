using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the player character. Keeps buff-pick VFX GameObjects deactivated
/// at all times and plays them once (one-shot) whenever a buff is selected.
/// Subscribes to <see cref="NewBuffManager.OnBuffApplied"/> so it stays
/// decoupled from the buff selection UI flow.
/// </summary>
public class BuffPickVFX : MonoBehaviour
{
    [Header("Buff Pick Effects")]
    [Tooltip("splash_blue VFX — played once when a buff is chosen.")]
    [SerializeField] private GameObject splashBlueVFX;
    [Tooltip("firecircle VFX — played once when a buff is chosen.")]
    [SerializeField] private GameObject fireCircleVFX;

    [Tooltip("How long to let each VFX run before deactivating it again. Uses real time so it works while Time.timeScale is 0.")]
    [SerializeField] private float vfxDuration = 3f;

    private Coroutine _splashCoroutine;
    private Coroutine _fireCoroutine;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        SetActive(splashBlueVFX, false);
        SetActive(fireCircleVFX, false);
    }

    private void OnEnable()
    {
        if (NewBuffManager.Instance != null)
        {
            NewBuffManager.Instance.OnBuffApplied += OnBuffApplied;
            Debug.Log("[BuffPickVFX] Subscribed to OnBuffApplied.");
        }
        else
        {
            Debug.LogWarning("[BuffPickVFX] OnEnable: NewBuffManager.Instance is null — subscription missed. Will retry in Start.");
        }
    }

    private void Start()
    {
        // Safety net: if OnEnable fired before NewBuffManager.Awake set Instance,
        // subscribe now. Unsubscribe first to prevent double-subscription.
        if (NewBuffManager.Instance != null)
        {
            NewBuffManager.Instance.OnBuffApplied -= OnBuffApplied;
            NewBuffManager.Instance.OnBuffApplied += OnBuffApplied;
            Debug.Log("[BuffPickVFX] Start: Ensured subscription to OnBuffApplied.");
        }
        else
        {
            Debug.LogError("[BuffPickVFX] Start: NewBuffManager.Instance still null. VFX will never fire.");
        }
    }

    private void OnDisable()
    {
        if (NewBuffManager.Instance != null)
            NewBuffManager.Instance.OnBuffApplied -= OnBuffApplied;
    }

    // ─── Event handler ────────────────────────────────────────────────────────

    private void OnBuffApplied(BuffData buff)
    {
        Debug.Log($"[BuffPickVFX] OnBuffApplied fired for '{buff?.buffName}'. Triggering VFX.");
        TriggerVFX(splashBlueVFX, ref _splashCoroutine);
        TriggerVFX(fireCircleVFX, ref _fireCoroutine);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Restarts the VFX: cancels any in-progress deactivation timer, activates
    /// the GameObject, then deactivates it after <see cref="vfxDuration"/> real seconds.
    /// Uses <see cref="WaitForSecondsRealtime"/> so it runs even when
    /// <see cref="Time.timeScale"/> is 0 (buff screen freeze).
    /// </summary>
    private void TriggerVFX(GameObject vfx, ref Coroutine handle)
    {
        if (vfx == null)
        {
            Debug.LogWarning("[BuffPickVFX] TriggerVFX: VFX reference is null.");
            return;
        }

        Debug.Log($"[BuffPickVFX] Activating '{vfx.name}'.");

        if (handle != null)
            StopCoroutine(handle);

        handle = StartCoroutine(PlayOnce(vfx));
    }

    private IEnumerator PlayOnce(GameObject vfx)
    {
        SetActive(vfx, true);
        yield return new WaitForSecondsRealtime(vfxDuration);
        SetActive(vfx, false);
    }

    private static void SetActive(GameObject vfx, bool active)
    {
        if (vfx != null && vfx.activeSelf != active)
            vfx.SetActive(active);
    }
}
