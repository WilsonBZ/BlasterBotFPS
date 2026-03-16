using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BuffMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public Button healButton;
    public Button pelletsButton;
    public Button spreadButton;

    [Header("Player Reference")]
    [SerializeField] private PlayerManager playerManager;

    [Header("Settings")]
    public KeyCode toggleKey = KeyCode.H;

    private const float PanelWidth = 900f;
    private const float PanelHeight = 120f;
    private const float ButtonWidth = 260f;
    private const float ButtonHeight = 80f;
    private const float ButtonSpacing = 30f;
    private const int ButtonFontSize = 24;

    private void Start()
    {
        EnsureUIExists();
        HookButtons();
        SetPanelActive(false);

        if (BuffManager.Instance != null)
            BuffManager.Instance.OnBuffsChanged += RefreshUI;

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (BuffManager.Instance != null)
            BuffManager.Instance.OnBuffsChanged -= RefreshUI;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetPanelActive(!(panel != null && panel.activeSelf));
    }

    private void EnsureUIExists()
    {
        if (panel != null && healButton != null && pelletsButton != null && spreadButton != null) return;

        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasGO = new GameObject("BuffMenuCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        if (panel == null)
        {
            panel = new GameObject("BuffPanel");
            panel.transform.SetParent(canvas.transform, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rt.anchoredPosition = new Vector2(0f, 80f);

            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.75f);
        }

        var layout = panel.GetComponent<HorizontalLayoutGroup>() ?? panel.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = ButtonSpacing;
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        Button CreateButton(string label)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(panel.transform, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.18f, 0.18f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            colors.pressedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = ButtonFontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;

            var tr = textGO.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            return btn;
        }

        if (healButton == null) healButton = CreateButton("Heal");
        if (pelletsButton == null) pelletsButton = CreateButton("Pellets");
        if (spreadButton == null) spreadButton = CreateButton("Spread");
    }

    private void HookButtons()
    {
        healButton.onClick.RemoveAllListeners();
        pelletsButton.onClick.RemoveAllListeners();
        spreadButton.onClick.RemoveAllListeners();

        healButton.onClick.AddListener(OnHealPressed);
        pelletsButton.onClick.AddListener(OnPelletsPressed);
        spreadButton.onClick.AddListener(OnSpreadPressed);
    }

    private void SetPanelActive(bool active)
    {
        if (panel == null) return;
        panel.SetActive(active);
        Cursor.visible = active;
        Cursor.lockState = active ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void RefreshUI()
    {
        UpdateButtonWithCount(healButton, "Heal", BuffType.Heal);
        UpdateButtonWithCount(pelletsButton, "Pellets", BuffType.Pellets);
        UpdateButtonWithCount(spreadButton, "Spread", BuffType.Spread);
    }

    private void UpdateButtonWithCount(Button btn, string label, BuffType type)
    {
        if (btn == null) return;
        var txt = btn.GetComponentInChildren<Text>();
        int count = BuffManager.Instance != null ? BuffManager.Instance.GetCount(type) : 0;
        if (txt != null) txt.text = $"{label}\n({count})";
    }

    /// <summary>Resolves the PlayerManager from the serialized field, with a scene fallback.</summary>
    private PlayerManager GetPlayer()
    {
        if (playerManager != null) return playerManager;
        playerManager = FindFirstObjectByType<PlayerManager>();
        if (playerManager == null) Debug.LogWarning("BuffMenu: PlayerManager reference is not set and could not be found in the scene.");
        return playerManager;
    }

    private void OnHealPressed()
    {
        if (BuffManager.Instance == null) return;
        if (!BuffManager.Instance.UseBuff(BuffType.Heal)) return;

        var player = GetPlayer();
        if (player == null) return;

        player.Heal(player.MaxHealth * 0.2f);
    }

    private void OnPelletsPressed()
    {
        if (BuffManager.Instance == null) return;
        if (!BuffManager.Instance.UseBuff(BuffType.Pellets)) return;

        var player = GetPlayer();
        if (player == null) return;

        foreach (var w in player.GetComponentsInChildren<ModularWeapon>(true))
            w.AddPellets(1);
    }

    private void OnSpreadPressed()
    {
        if (BuffManager.Instance == null) return;
        if (!BuffManager.Instance.UseBuff(BuffType.Spread)) return;

        var player = GetPlayer();
        if (player == null) return;

        foreach (var w in player.GetComponentsInChildren<ModularWeapon>(true))
            w.ReduceSpreadPercent(0.3f);
    }
}
