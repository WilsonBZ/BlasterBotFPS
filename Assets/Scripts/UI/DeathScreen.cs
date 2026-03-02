using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeathScreen : MonoBehaviour
{
    public static DeathScreen Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image fadeImage;
    [SerializeField] private TMPro.TextMeshProUGUI deathText;
    [SerializeField] private TMPro.TextMeshProUGUI subtitleText;
    [SerializeField] private Button respawnButton;

    [Header("Timing")]
    [SerializeField] private float fadeInDuration = 1f;
    [SerializeField] private float textDelayAfterFade = 0.5f;
    [SerializeField] private float autoRespawnDelay = 3f;

    [Header("Audio")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private AudioSource audioSource;

    private bool isDead = false;

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

        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(RespawnPlayer);
        }
    }

    private void Start()
    {
        PlayerManager player = FindFirstObjectByType<PlayerManager>();
        if (player != null)
        {
            player.OnDeath += ShowDeathScreen;
            Debug.Log("DeathScreen: Subscribed to PlayerManager.OnDeath");
        }
        else
        {
            Debug.LogError("DeathScreen: PlayerManager not found!");
        }
    }

    private void OnEnable()
    {
        Debug.Log("DeathScreen: OnEnable called");
    }

    public void ShowDeathScreen()
    {
        Debug.Log("DeathScreen: ShowDeathScreen called!");
        
        if (isDead) 
        {
            Debug.Log("DeathScreen: Already dead, ignoring");
            return;
        }

        isDead = true;

        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
            Debug.Log("DeathScreen: Panel activated");
        }
        else
        {
            Debug.LogError("DeathScreen: deathPanel is null!");
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        Debug.Log("DeathScreen: Starting death sequence");
        
        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }

            if (fadeImage != null)
            {
                Color color = fadeImage.color;
                color.a = alpha * 0.8f;
                fadeImage.color = color;
            }

            yield return null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        Debug.Log("DeathScreen: Fade complete, showing text");

        yield return new WaitForSeconds(textDelayAfterFade);

        if (deathText != null)
        {
            deathText.gameObject.SetActive(true);
            Debug.Log("DeathScreen: Death text shown");
        }

        yield return new WaitForSeconds(0.3f);

        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(true);
            Debug.Log("DeathScreen: Subtitle shown");
        }

        if (respawnButton != null)
        {
            respawnButton.gameObject.SetActive(true);
            Debug.Log("DeathScreen: Button shown");
        }

        Debug.Log($"DeathScreen: Waiting {autoRespawnDelay} seconds for auto-respawn");
        yield return new WaitForSeconds(autoRespawnDelay);

        Debug.Log("DeathScreen: Auto-respawning");
        RespawnPlayer();
    }

    public void RespawnPlayer()
    {
        Debug.Log("DeathScreen: RespawnPlayer called");
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (respawnButton != null)
        {
            respawnButton.onClick.RemoveListener(RespawnPlayer);
        }
    }
}
