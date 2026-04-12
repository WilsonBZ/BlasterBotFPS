using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Soul Knight-style buff selection screen.
/// Shows 3 cards that slide in from below after a room is cleared.
/// Press 1 / 2 / 3 or click a card to pick. Time is frozen while the panel is open.
/// </summary>
public class BuffSelectionUI : MonoBehaviour
{
    // ─── Inspector ─────────────────────────────────────────────────────────────

    [Header("Panel / Container")]
    [Tooltip("Root overlay panel.")]
    [SerializeField] private GameObject panel;
    [Tooltip("Horizontal layout container for the card GameObjects.")]
    [SerializeField] private Transform cardContainer;
    [Tooltip("Optional card prefab. Needs Image (root), Image child 'Icon', Text child 'Name', Text child 'Description'. Leave null to build cards at runtime.")]
    [SerializeField] private GameObject cardPrefab;

    [Header("Card Display Mode")]
    [Tooltip("When enabled: BuffData.cardSprite fills the entire card face and all text/icon children are hidden. " +
             "Falls back to classic icon+text mode per-card if cardSprite is null on that buff.")]
    [SerializeField] private bool useCardSpriteMode = false;

    [Header("Header")]
    [SerializeField] private TextMeshProUGUI headerText;

    [Header("Animation")]
    [Tooltip("Cards start this many canvas units below their final position.")]
    [SerializeField] private float slideFromY = -320f;
    [Tooltip("Seconds each card takes to slide in.")]
    [SerializeField] private float slideInDuration = 0.22f;
    [Tooltip("Delay between successive card animations.")]
    [SerializeField] private float cardStagger = 0.07f;

    [Header("Keyboard Shortcuts")]
    [SerializeField] private KeyCode card1Key = KeyCode.Alpha1;
    [SerializeField] private KeyCode card2Key = KeyCode.Alpha2;
    [SerializeField] private KeyCode card3Key = KeyCode.Alpha3;

    // ─── Private ───────────────────────────────────────────────────────────────

    private Action<BuffData> onBuffSelected;
    private readonly List<GameObject> spawnedCards = new List<GameObject>();
    private bool isShowing;

    // ─── Unity ────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (!isShowing) return;
        if      (Input.GetKeyDown(card1Key) && spawnedCards.Count >= 1) PickCardByIndex(0);
        else if (Input.GetKeyDown(card2Key) && spawnedCards.Count >= 2) PickCardByIndex(1);
        else if (Input.GetKeyDown(card3Key) && spawnedCards.Count >= 3) PickCardByIndex(2);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>Displays the selection overlay with the provided buff choices.</summary>
    public void ShowBuffChoices(List<BuffData> buffs, Action<BuffData> onSelected)
    {
        if (buffs == null || buffs.Count == 0)
        {
            Debug.LogWarning("[BuffSelectionUI] No buffs to display.");
            return;
        }

        onBuffSelected = onSelected;
        ClearCards();

        if (panel != null) panel.SetActive(true);
        if (headerText != null) headerText.text = "Choose a Buff";

        for (int i = 0; i < buffs.Count; i++)
            SpawnCard(buffs[i], i);

        FreezeGame();
        isShowing = true;
    }

    // ─── Card spawning ────────────────────────────────────────────────────────

    private void SpawnCard(BuffData buff, int index)
    {
        GameObject card = cardPrefab != null
            ? Instantiate(cardPrefab, cardContainer)
            : BuildDefaultCard(index);

        // Always populate regardless of source
        PopulateCard(card, buff, index);

        spawnedCards.Add(card);
        StartCoroutine(SlideIn(card, index));
    }

    private void PopulateCard(GameObject card, BuffData buff, int index)
    {
        // Always store reference so PickCardByIndex can retrieve the buff.
        BuffCardData meta = card.GetComponent<BuffCardData>() ?? card.AddComponent<BuffCardData>();
        meta.Buff = buff;

        // Click handler — common to both modes.
        Button btn = card.GetComponent<Button>() ?? card.AddComponent<Button>();
        btn.onClick.RemoveAllListeners();
        int captured = index;
        btn.onClick.AddListener(() => PickCardByIndex(captured));

        // ── Card-sprite mode ──────────────────────────────────────────────────
        // Use buff.cardSprite as the full card face if the mode is active and
        // a sprite is actually assigned on this buff. Falls back to classic mode.
        bool showAsCardSprite = useCardSpriteMode && buff.cardSprite != null;

        if (showAsCardSprite)
        {
            // Root image becomes the card artwork; no rarity tint is applied
            // because the sprite itself carries the visual identity.
            Image bg = card.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite              = buff.cardSprite;
                bg.color               = Color.white;
                bg.type                = Image.Type.Simple;
                bg.preserveAspect      = false;
            }

            // Hide all text and icon children — the artwork is the card.
            SetCardChildrenVisible(card, false);
            return;
        }

        // ── Classic icon + text mode ──────────────────────────────────────────
        // Rarity tint on root Image.
        Image bgClassic = card.GetComponent<Image>();
        if (bgClassic != null)
        {
            bgClassic.sprite = null;
            bgClassic.color  = RarityColor(buff.weight);
        }

        // Make sure children are visible when switching back from card-sprite mode.
        SetCardChildrenVisible(card, true);

        // Icon.
        Image icon = card.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null)
        {
            icon.enabled = buff.icon != null;
            if (buff.icon != null) icon.sprite = buff.icon;
        }

        // Name.
        TextMeshProUGUI nameText = card.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
        if (nameText != null) nameText.text = buff.buffName;

        // Description.
        TextMeshProUGUI descText = card.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
            descText.text = buff.effect != null ? buff.effect.GetDescription() : buff.description;

        // Keyboard shortcut badge.
        TextMeshProUGUI shortcutText = card.transform.Find("Shortcut")?.GetComponent<TextMeshProUGUI>();
        if (shortcutText != null) shortcutText.text = $"[{index + 1}]";
    }

    /// <summary>Shows or hides all direct children of the card root.</summary>
    private static void SetCardChildrenVisible(GameObject card, bool visible)
    {
        foreach (Transform child in card.transform)
            child.gameObject.SetActive(visible);
    }

    /// <summary>Builds a complete card GameObject purely from code (no prefab needed).</summary>
    private GameObject BuildDefaultCard(int index)
    {
        // Root
        GameObject card = new GameObject($"BuffCard_{index}");
        card.transform.SetParent(cardContainer, false);

        RectTransform cardRt = card.AddComponent<RectTransform>();
        cardRt.sizeDelta = new Vector2(220f, 310f);

        // Root image: used as rarity tint (classic) or full card art (card-sprite mode).
        Image bg          = card.AddComponent<Image>();
        bg.raycastTarget  = true;

        Button btn = card.AddComponent<Button>();
        ColorBlock cb = ColorBlock.defaultColorBlock;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.80f);
        cb.pressedColor     = new Color(0.6f, 0.6f, 0.6f, 1f);
        btn.colors = cb;

        // Icon — only used in classic mode; hidden in card-sprite mode.
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(card.transform, false);
        RectTransform iconRt = iconGO.AddComponent<RectTransform>();
        iconRt.anchorMin        = new Vector2(0.5f, 1f);
        iconRt.anchorMax        = new Vector2(0.5f, 1f);
        iconRt.pivot            = new Vector2(0.5f, 1f);
        iconRt.sizeDelta        = new Vector2(90f, 90f);
        iconRt.anchoredPosition = new Vector2(0f, -18f);
        iconGO.AddComponent<Image>();

        // Name
        MakeText(card, "Name", 18, FontStyles.Bold,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f), anchoredPos: new Vector2(0f, -120f),
            size: new Vector2(0f, 34f));

        // Description
        MakeText(card, "Description", 12, FontStyles.Normal,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 50f),
            size: new Vector2(-20f, 100f));

        // Shortcut
        MakeText(card, "Shortcut", 13, FontStyles.Italic,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f), anchoredPos: new Vector2(0f, 10f),
            size: new Vector2(0f, 24f));

        return card;
    }

    private static void MakeText(GameObject parent, string goName, int fontSize, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);

        RectTransform rt    = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        TextMeshProUGUI t  = go.AddComponent<TextMeshProUGUI>();
        t.fontSize         = fontSize;
        t.fontStyle        = style;
        t.color            = Color.white;
        t.alignment        = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode     = TextOverflowModes.Overflow;
    }

    // ─── Selection ────────────────────────────────────────────────────────────

    private void PickCardByIndex(int index)
    {
        if (!isShowing || index >= spawnedCards.Count) return;

        BuffCardData meta = spawnedCards[index].GetComponent<BuffCardData>();
        if (meta == null)
        {
            Debug.LogError($"[BuffSelectionUI] Card {index} missing BuffCardData.");
            return;
        }

        BuffData chosen = meta.Buff;
        Hide();
        onBuffSelected?.Invoke(chosen);
    }

    // ─── Animation ────────────────────────────────────────────────────────────

    private IEnumerator SlideIn(GameObject card, int index)
    {
        yield return new WaitForSecondsRealtime(index * cardStagger);

        RectTransform rt = card.GetComponent<RectTransform>();
        if (rt == null) yield break;

        Vector2 destination = rt.anchoredPosition;
        Vector2 origin      = destination + new Vector2(0f, slideFromY);
        float elapsed       = 0f;

        while (elapsed < slideInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideInDuration));
            rt.anchoredPosition = Vector2.Lerp(origin, destination, t);
            yield return null;
        }

        rt.anchoredPosition = destination;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private void Hide()
    {
        isShowing = false;
        StopAllCoroutines();
        ClearCards();
        if (panel != null) panel.SetActive(false);
        UnfreezeGame();
    }

    private void ClearCards()
    {
        foreach (GameObject card in spawnedCards)
            if (card != null) Destroy(card);
        spawnedCards.Clear();
    }

    private void FreezeGame()
    {
        Time.timeScale   = 0f;
        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void UnfreezeGame()
    {
        Time.timeScale   = 1f;
        Cursor.visible   = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    /// <summary>Maps buff weight to a dark rarity background tint.</summary>
    private static Color RarityColor(int weight)
    {
        if (weight <= 15) return new Color(0.22f, 0.10f, 0.32f, 0.97f); // Epic   – purple
        if (weight <= 30) return new Color(0.08f, 0.14f, 0.38f, 0.97f); // Rare   – blue
        if (weight <= 50) return new Color(0.08f, 0.26f, 0.12f, 0.97f); // Uncommon – green
        return                   new Color(0.16f, 0.16f, 0.20f, 0.97f); // Common – dark grey
    }
}
