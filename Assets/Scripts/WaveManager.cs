//using UnityEngine;
//using UnityEngine.UI;
//using TMPro;

//public class WaveManager : MonoBehaviour
//{
//    [Header("References")]
//    [SerializeField] private WaveSpawner spawner;
//    [SerializeField] private GunVendor vendor;
//    [SerializeField] private TextMeshProUGUI waveStatusText;

//    [Header("UI Settings")]
//    [SerializeField] private float messageDisplayTime = 2f;

//    private int currentWave = 0;
//    private bool allWavesComplete = false;

//    private void Start()
//    {
//        vendor.OnGunPurchased += HandleGunPurchased;
//        waveStatusText.gameObject.SetActive(false);
//    }

//    private void HandleGunPurchased()
//    {
//        vendor.LockVendor();
//        waveStatusText.gameObject.SetActive(false);

//        spawner.StartNextWave();
//    }

//    public void OnWaveComplete()
//    {
//        currentWave++;
//        ShowMessage($"Wave {currentWave} Complete!");

//        vendor.UnlockVendor();
//    }

//    public void OnAllWavesComplete()
//    {
//        allWavesComplete = true;
//        ShowMessage("All Waves Complete!");
//        vendor.UnlockVendor();
//    }

//    private void ShowMessage(string message)
//    {
//        StopAllCoroutines();
//        StartCoroutine(ShowMessageCoroutine(message));
//    }

//    private System.Collections.IEnumerator ShowMessageCoroutine(string message)
//    {
//        waveStatusText.text = message;
//        waveStatusText.gameObject.SetActive(true);

//        yield return new WaitForSeconds(messageDisplayTime);

//        waveStatusText.gameObject.SetActive(false);
//    }
//}
