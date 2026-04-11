using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core")]
    public WaveSpawner waveSpawner;
    public PlayerManager player;
    public BuffMenu buffMenu;

    [Header("UI Panels (optional)")]
    public GameObject hudPanel;
    public GameObject pausePanel;
    public GameObject gameOverPanel;

    [Header("Settings")]
    public bool autoStartWaves = false;
    public bool startPaused = false;

    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }

    private PlayerMovement playerMovement;
    private MouseMovement playerMouse;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (waveSpawner == null)
        {
            waveSpawner = FindFirstObjectByType<WaveSpawner>();
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerManager>();
        }

        if (buffMenu == null)
        {
            buffMenu = FindFirstObjectByType<BuffMenu>();
        }

        CachePlayerComponents();

        Application.targetFrameRate = 60;
    }

    private void CachePlayerComponents()
    {
        if (player != null)
        {
            playerMovement = player.GetComponent<PlayerMovement>();
            playerMouse = player.GetComponentInChildren<MouseMovement>();
        }
    }

    private void OnEnable()
    {
        if (player != null)
        {
            player.OnDeath += HandlePlayerDeath;
        }

        if (waveSpawner != null)
        {
            waveSpawner.OnWaveStarted += OnWaveStarted;
            waveSpawner.OnWaveCompleted += OnWaveCompleted;
            waveSpawner.OnEnemyDied += OnEnemyDied;
        }
    }

    private void OnDisable()
    {
        if (player != null)
        {
            player.OnDeath -= HandlePlayerDeath;
        }

        if (waveSpawner != null)
        {
            waveSpawner.OnWaveStarted -= OnWaveStarted;
            waveSpawner.OnWaveCompleted -= OnWaveCompleted;
            waveSpawner.OnEnemyDied -= OnEnemyDied;
        }
    }

    private void Start()
    {
        SetPanelsVisibility(hud: hudPanel != null, pause: false, gameOver: false);

        if (startPaused)
        {
            PauseGame();
        }
        else
        {
            ResumeGame();
        }

        if (autoStartWaves && waveSpawner != null)
        {
            waveSpawner.StartWaves();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsGameOver)
            {
                return;
            }

            if (IsPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        if (IsGameOver && Input.GetKeyDown(KeyCode.R))
        {
            RestartLevel();
        }
    }

    public void StartWaves()
    {
        waveSpawner?.StartWaves();
    }

    public void StopWaves()
    {
        waveSpawner?.StopWaves();
    }

    public void PauseGame()
    {
        if (IsPaused || IsGameOver)
        {
            return;
        }

        IsPaused = true;
        Time.timeScale = 0f;
        SetPanelsVisibility(hud: false, pause: true, gameOver: false);
        UnlockCursor();
        TogglePlayerInput(false);
    }

    public void ResumeGame()
    {
        if (!IsPaused || IsGameOver)
        {
            return;
        }

        IsPaused = false;
        Time.timeScale = 1f;
        SetPanelsVisibility(hud: hudPanel != null, pause: false, gameOver: false);
        LockCursor();
        TogglePlayerInput(true);
    }

    private void HandlePlayerDeath()
    {
        IsGameOver = true;
        Time.timeScale = 0f;
        SetPanelsVisibility(hud: false, pause: false, gameOver: true);
        UnlockCursor();
        TogglePlayerInput(false);
        StopWaves();
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitToDesktop()
    {
    #if UNITY_EDITOR
        EditorApplication.isPlaying = false;
    #else      
        Application.Quit();
    #endif
    }

    private void SetPanelsVisibility(bool hud, bool pause, bool gameOver)
    {
        if (hudPanel != null)
        {
            hudPanel.SetActive(hud);
        }

        if (pausePanel != null)
        {
            pausePanel.SetActive(pause);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(gameOver);
        }
    }

    private void TogglePlayerInput(bool enabled)
    {
        if (player == null)
        {
            return;
        }

        if (playerMovement != null)
        {
            playerMovement.enabled = enabled;
        }

        if (playerMouse != null)
        {
            playerMouse.enabled = enabled;
        }
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnWaveStarted(int index)
    {
        Debug.Log($"Wave started: {index}");
    }

    private void OnWaveCompleted(int index)
    {
        Debug.Log($"Wave completed: {index}");
    }

    private void OnEnemyDied(int alive)
    {
     
    }
}
