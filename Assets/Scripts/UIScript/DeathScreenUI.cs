using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class DeathScreenUi : MonoBehaviour
{
    [Header("UI (optional - will be created at runtime if left empty)")]
    public GameObject Panel;
    public Button MainMenuButton;
    public Button QuitButton;
    public string MainMenuSceneName = "MainMenu";
    public string TitleText = "You Died";

    private void Start()
    {
        EnsureUiExists();
        HookButtons();
        Hide();

        // If GameManager doesn't have a gameOverPanel assigned, give it ours so existing logic shows it
        if (GameManager.Instance != null && GameManager.Instance.gameOverPanel == null)
        {
            GameManager.Instance.gameOverPanel = Panel;
        }
    }

    private void EnsureUiExists()
    {
        if (Panel != null && MainMenuButton != null && QuitButton != null)
            return;

        // ensure EventSystem
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.hideFlags = HideFlags.DontSaveInBuild;
        }

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("DeathScreenCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        if (Panel == null)
        {
            Panel = new GameObject("GameOverPanel");
            Panel.transform.SetParent(canvas.transform, false);
            var rt = Panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(600f, 300f);
            rt.anchoredPosition = Vector2.zero;

            var img = Panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.8f);
        }

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(Panel.transform, false);
        var title = titleGO.AddComponent<Text>();
        title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title.text = TitleText;
        title.alignment = TextAnchor.MiddleCenter;
        title.color = Color.white;
        var tr = titleGO.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0.1f, 0.65f);
        tr.anchorMax = new Vector2(0.9f, 0.9f);
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        // Buttons container
        var buttonsGO = new GameObject("Buttons");
        buttonsGO.transform.SetParent(Panel.transform, false);
        var brt = buttonsGO.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.2f, 0.15f);
        brt.anchorMax = new Vector2(0.8f, 0.45f);
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        var layout = buttonsGO.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;

        // factory for buttons
        System.Func<string, Button> createButton = (label) =>
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(buttonsGO.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.95f);
            var btn = go.AddComponent<Button>();
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(200f, 60f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.color = Color.black;
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        };

        if (MainMenuButton == null) MainMenuButton = createButton("Main Menu");
        if (QuitButton == null) QuitButton = createButton("Quit");
    }

    private void HookButtons()
    {
        if (MainMenuButton != null)
        {
            MainMenuButton.onClick.RemoveAllListeners();
            MainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (QuitButton != null)
        {
            QuitButton.onClick.RemoveAllListeners();
            QuitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    public void Show()
    {
        if (Panel == null) return;
        Panel.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (EventSystem.current != null && MainMenuButton != null)
        {
            EventSystem.current.SetSelectedGameObject(MainMenuButton.gameObject);
        }
    }

    public void Hide()
    {
        if (Panel == null) return;
        Panel.SetActive(false);
    }

    private void OnMainMenuClicked()
    {
        // ensure time scale restored before scene change
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(MainMenuSceneName))
        {
            SceneManager.LoadScene(MainMenuSceneName);
            return;
        }

        // fallback: restart current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OnQuitClicked()
    {
        // delegate to GameManager if present (handles editor quit)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.QuitToDesktop();
            return;
        }

        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
