using UnityEngine;  
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class PauseMenuUi : MonoBehaviour
{
    [Header("Button Prefabs")]
    [SerializeField] private Button resumeButtonPrefab;
    [SerializeField] private Button settingsButtonPrefab;
    [SerializeField] private Button quitButtonPrefab;

    [Header("Optional UI")]
    [Tooltip("Optional settings panel that will be shown when Settings is clicked")]
    [SerializeField] private GameObject settingsPanel;
    [Tooltip("If set, Quit will load this scene instead of closing the application")]
    [SerializeField] private string quitToSceneName = "";

    [Header("Behavior")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private bool lockCursorOnResume = true;

    private Button resumeButton;
    private Button settingsButton;
    private Button quitButton;
    private GameObject menuRoot;
    private bool is_paused;
    private float previous_timeScale;
    private float previous_fixedDeltaTime;

    private void Awake()
    {
        menuRoot = gameObject;

        if (resumeButtonPrefab != null)
        {
            resumeButton = Instantiate(resumeButtonPrefab, menuRoot.transform);
            resumeButton.onClick.AddListener(OnResumeButtonClicked);
        }

        if (settingsButtonPrefab != null)
        {
            settingsButton = Instantiate(settingsButtonPrefab, menuRoot.transform);
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        if (quitButtonPrefab != null)
        {
            quitButton = Instantiate(quitButtonPrefab, menuRoot.transform);
            quitButton.onClick.AddListener(OnQuitButtonClicked);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        HideMenuInstant();
    }

    private void Update()
    {
        if (Input.GetKeyDown(pauseKey))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (is_paused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    public void Pause()
    {
        if (is_paused) return;

        previous_timeScale = Time.timeScale;
        previous_fixedDeltaTime = Time.fixedDeltaTime;

        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;

        AudioListener.pause = true;


        menuRoot.SetActive(true);
        is_paused = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            if (resumeButton != null)
            {
                eventSystem.SetSelectedGameObject(resumeButton.gameObject);
            }
            else if (settingsButton != null)
            {
                eventSystem.SetSelectedGameObject(settingsButton.gameObject);
            }
        }
    }

    public void Resume()
    {
        if (!is_paused) return;

        // hide possible settings panel first
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        Time.timeScale = previous_timeScale > 0f ? previous_timeScale : 1f;
        Time.fixedDeltaTime = previous_fixedDeltaTime > 0f ? previous_fixedDeltaTime : 0.02f;

        AudioListener.pause = false;

        menuRoot.SetActive(false);
        is_paused = false;

        if (lockCursorOnResume)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void HideMenuInstant()
    {
        menuRoot.SetActive(false);
        is_paused = false;
    }

    private void OnResumeButtonClicked()
    {
        Resume();
    }

    private void OnSettingsButtonClicked()
    {
        if (settingsPanel == null)
        {
            Debug.Log("PauseMenuUi: Settings panel not assigned.");
            return;
        }

        bool active = settingsPanel.activeSelf;
        settingsPanel.SetActive(!active);

        if (EventSystem.current != null && !active)
        {
            Selectable sel = settingsPanel.GetComponentInChildren<Selectable>();
            if (sel != null)
            {
                EventSystem.current.SetSelectedGameObject(sel.gameObject);
            }
        }
        else if (EventSystem.current != null)
        {
            if (resumeButton != null)
            {
                EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
            }
        }
    }

    private void OnQuitButtonClicked()
    {
        if (!string.IsNullOrEmpty(quitToSceneName))
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            AudioListener.pause = false;
            SceneManager.LoadScene(quitToSceneName);
            return;
        }

        //UnityEditor.EditorApplication.isPlaying = false;
    }
}
