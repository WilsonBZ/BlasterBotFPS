using System;
using System.Linq;
using System.Reflection;
    using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class BuffMenu : MonoBehaviour
{
    [Header("UI (optional - will be created at runtime if left empty)")]
    public GameObject panel;                // root panel to toggle
    public Button healButton;
    public Button pelletsButton;
    public Button spreadButton;

    [Header("Behaviour")]
    [Tooltip("Key to toggle buff menu")]
    public KeyCode toggleKey = KeyCode.H;

    void Start()
    {
        EnsureUIExists();
        HookButtons();
        SetPanelActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetPanelActive(!(panel != null && panel.activeSelf));
        }
    }

    private void EnsureUIExists()
    {
        if (panel != null && healButton != null && pelletsButton != null && spreadButton != null)
            return;

        // Ensure EventSystem exists
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            es.hideFlags = HideFlags.DontSaveInBuild;
        }

        // Create Canvas if necessary
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

        // create horizontal layout container
        var layout = panel.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 10f;
        layout.padding = new RectOffset(10, 10, 10, 10);

        Func<string, Button> createButton = (label) =>
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(panel.transform, false);
            var btnImage = go.AddComponent<Image>();
            btnImage.color = new Color(1f, 1f, 1f, 0.95f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(150f, 50f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            //text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.black;
            var tr = textGO.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            return btn;
        };

        if (healButton == null) healButton = createButton("Heal 20%");
        if (pelletsButton == null) pelletsButton = createButton(" +1 Pellets");
        if (spreadButton == null) spreadButton = createButton(" -30% Spread");
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

    private void OnHealPressed()
    {
        var player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
        {
            Debug.LogWarning("BuffMenu: PlayerManager not found.");
            return;
        }

        float healAmount = player.MaxHealth * 0.2f;
        player.Heal(healAmount);
    }

    private void OnPelletsPressed()
    {
        var player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
        {
            Debug.LogWarning("BuffMenu: PlayerManager not found.");
            return;
        }

        // Try to find weapons parent (player children)
        var modulars = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (var w in modulars)
        {
            w.pellets += 1;
        }

        // Handle GeneralWeapon (private fields / methods)
        var generals = player.GetComponentsInChildren<GeneralWeapon>(true);
        foreach (var gw in generals)
        {
            // use public method if available
            gw.BuffPellets(1);
        }
    }

    private void OnSpreadPressed()
    {
        var player = FindFirstObjectByType<PlayerManager>();
        if (player == null)
        {
            Debug.LogWarning("BuffMenu: PlayerManager not found.");
            return;
        }

        var modulars = player.GetComponentsInChildren<ModularWeapon>(true);
        foreach (var w in modulars)
        {
            w.spreadAngle = w.spreadAngle * 0.7f; // reduce by 30%
        }

        var generals = player.GetComponentsInChildren<GeneralWeapon>(true);
        foreach (var gw in generals)
        {
            // GeneralWeapon exposes BuffSpread(float reduction) which subtracts degrees.
            // Use reflection to read private spreadAngle if necessary, otherwise attempt a best-effort call.
            FieldInfo fi = typeof(GeneralWeapon).GetField("spreadAngle", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi != null)
            {
                float cur = (float)fi.GetValue(gw);
                float reduction = cur * 0.3f;
                gw.BuffSpread(reduction);
            }
            else
            {
                // fallback: attempt to reduce by an assumed amount (not ideal)
                gw.BuffSpread(3f);
            }
        }
    }
}
