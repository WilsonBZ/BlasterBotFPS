using UnityEngine;
using System.Collections;

/// <summary>
/// Attach to vendor object (same GameObject as GunVendor). Coordinates shop -> wave spawning lifecycle:
/// - When a purchase happens, vendor closes shop and spawner.StartWaves() is called.
/// - While waves run, vendor is disabled (no interaction). When waves complete, vendor re-enables.
/// </summary>
[RequireComponent(typeof(GunVendor))]
public class VendorWaveController : MonoBehaviour
{
    [Tooltip("Wave spawner to start when purchase is made.")]
    public WaveSpawner spawner;

    [Tooltip("Optional: UI panel to show 'Wave Complete' or 'Wave X/Y' information.")]
    public GameObject waveCompletePanel;

    [Tooltip("If true, vendor will be disabled while waves run (no open shop).")]
    public bool disableVendorDuringWaves = true;

    private GunVendor vendor;
    private bool isRunning = false;

    void Awake()
    {
        vendor = GetComponent<GunVendor>();
        if (vendor == null) Debug.LogError("VendorWaveController requires GunVendor on same GameObject.");
        if (spawner == null) Debug.LogWarning("VendorWaveController: No WaveSpawner assigned.");
    }

    /// <summary>
    /// Called by the shop UI (or GunShopUI) when a purchase is made and the vendor should begin waves.
    /// </summary>
    public void OnPurchase_StartWaves()
    {
        Debug.Log("Gun is bought");
        if (spawner == null) return;
        if (isRunning) return;

        // close shop forcibly (in case shop UI didn't already)
        vendor.CloseShop();

        // disable vendor interaction
        if (disableVendorDuringWaves)
        {
            vendor.enabled = false;
            if (vendor.promptObject != null) vendor.promptObject.SetActive(false);
        }

        // subscribe to spawner events to know when waves finish
        spawner.OnWaveStarted += OnWaveStarted;
        spawner.OnWaveCompleted += OnWaveCompleted;
        spawner.OnAllWavesCompleted += OnAllWavesCompleted;

        isRunning = true;
        spawner.StartWaves();
    }

    private void OnWaveStarted(int waveIndex)
    {
        // hide waveComplete panel when wave starts
        if (waveCompletePanel != null) waveCompletePanel.SetActive(false);
        Debug.Log($"Wave {waveIndex} started.");
    }

    private void OnWaveCompleted(int waveIndex)
    {
        Debug.Log($"Wave {waveIndex} completed.");
        // show brief notification
        if (waveCompletePanel != null)
        {
            StartCoroutine(ShowWaveCompleteBrief());
        }
    }

    private IEnumerator ShowWaveCompleteBrief()
    {
        waveCompletePanel.SetActive(true);
        yield return new WaitForSeconds(1.2f);
        waveCompletePanel.SetActive(false);
    }

    private void OnAllWavesCompleted()
    {
        Debug.Log("All waves finished.");
        // cleanup subscriptions
        spawner.OnWaveStarted -= OnWaveStarted;
        spawner.OnWaveCompleted -= OnWaveCompleted;
        spawner.OnAllWavesCompleted -= OnAllWavesCompleted;

        isRunning = false;

        // re-enable vendor
        if (disableVendorDuringWaves)
        {
            vendor.enabled = true;
            if (vendor.promptObject != null) vendor.promptObject.SetActive(true);
        }

        // optionally open vendor or show "next round ready" UI — we'll just log
    }
}
