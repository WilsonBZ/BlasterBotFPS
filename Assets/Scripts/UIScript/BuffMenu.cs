using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BuffMenu : MonoBehaviour
{
    public GameObject panel;
    public Button healButton;
    public Button pelletsButton;
    public Button spreadButton;
    public KeyCode toggleKey = KeyCode.H;

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
            rt.anchorMin = new Vector2(0.5f, 0.1f);
            rt.anchorMax = new Vector2(0.5f, 0.1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500f, 80f);
            rt.anchoredPosition = new Vector2(0f, 60f);

            var img = panel.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);
        }

        var layout = panel.GetComponent<HorizontalLayoutGroup>() ?? panel.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        Button CreateButton(string label)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(panel.transform, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.95f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(150f, 50f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;

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
        if (txt != null) txt.text = $"{label} ({count})";
    }

    private void OnHealPressed()
    {
        Debug.Log("Heal button pressed");
        if (BuffManager.Instance == null) return;
        if (!BuffManager.Instance.UseBuff(BuffType.Heal)) return;

        var player = FindFirstObjectByType<PlayerManager>();
        if (player == null) { Debug.LogWarning("PlayerManager not found"); return; }

        player.Heal(player.MaxHealth * 0.2f);
        Debug.Log("Heal applied to player");
    }

    private void OnPelletsPressed()
    {
        Debug.Log("Pellets button pressed");
        if (BuffManager.Instance == null) return;
        if (!BuffManager.Instance.UseBuff(BuffType.Pellets)) return;

        var player = FindFirstObjectByType<PlayerManager>();
        if (player == null) { Debug.LogWarning("PlayerManager not found"); return; }

        var modulars = player.GetComponentsInChildren<ModularWeapon>(true);
        Debug.Log($"Found {modulars.Length} ModularWeapon(s) for pellets buff");

        foreach (var w in modulars)
        {
            w.AddPellets(1);
        }
    }

    private void OnSpreadPressed()
    {
        Debug.Log("Spread button pressed");
        if (BuffManager.Instance == null) return;
        if (!BuffManager.Instance.UseBuff(BuffType.Spread)) return;

        var player = FindFirstObjectByType<PlayerManager>();
        if (player == null) { Debug.LogWarning("PlayerManager not found"); return; }

        var modulars = player.GetComponentsInChildren<ModularWeapon>(true);
        Debug.Log($"Found {modulars.Length} ModularWeapon(s) for spread buff");

        foreach (var w in modulars)
        {
            w.ReduceSpreadPercent(0.3f);
        }
    }
}
