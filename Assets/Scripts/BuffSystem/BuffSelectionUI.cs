using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BuffSelectionUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panel;
    public Transform buffChoicesContainer;
    public GameObject buffChoiceButtonPrefab;
    
    [Header("Settings")]
    public KeyCode skipKey = KeyCode.Escape;
    
    private Action<BuffData> onBuffSelected;
    private List<GameObject> spawnedButtons = new List<GameObject>();
    
    private void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
    
    private void Update()
    {
        if (panel != null && panel.activeSelf && Input.GetKeyDown(skipKey))
        {
            HideUI();
        }
    }
    
    public void ShowBuffChoices(List<BuffData> buffs, Action<BuffData> onSelected)
    {
        if (buffs == null || buffs.Count == 0)
        {
            Debug.LogWarning("BuffSelectionUI: No buffs to display!");
            return;
        }
        
        onBuffSelected = onSelected;
        ClearPreviousChoices();
        
        foreach (BuffData buff in buffs)
        {
            CreateBuffChoiceButton(buff);
        }
        
        ShowUI();
    }
    
    private void CreateBuffChoiceButton(BuffData buff)
    {
        GameObject buttonObj;
        
        if (buffChoiceButtonPrefab != null)
        {
            buttonObj = Instantiate(buffChoiceButtonPrefab, buffChoicesContainer);
        }
        else
        {
            buttonObj = CreateDefaultButton();
        }
        
        Button button = buttonObj.GetComponent<Button>();
        if (button == null)
        {
            button = buttonObj.AddComponent<Button>();
        }
        
        button.onClick.AddListener(() => OnBuffChosen(buff));
        
        Text buttonText = buttonObj.GetComponentInChildren<Text>();
        if (buttonText != null)
        {
            string description = buff.effect != null ? buff.effect.GetDescription() : buff.description;
            buttonText.text = $"{buff.buffName}\n{description}";
        }
        
        Image iconImage = buttonObj.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null && buff.icon != null)
        {
            iconImage.sprite = buff.icon;
        }
        
        spawnedButtons.Add(buttonObj);
    }
    
    private GameObject CreateDefaultButton()
    {
        GameObject buttonObj = new GameObject("BuffChoiceButton");
        buttonObj.transform.SetParent(buffChoicesContainer, false);
        
        RectTransform rt = buttonObj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 100f);
        
        Image img = buttonObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        
        Text text = textObj.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 10);
        textRt.offsetMax = new Vector2(-10, -10);
        
        return buttonObj;
    }
    
    private void OnBuffChosen(BuffData buff)
    {
        onBuffSelected?.Invoke(buff);
        HideUI();
    }
    
    private void ClearPreviousChoices()
    {
        foreach (GameObject button in spawnedButtons)
        {
            if (button != null)
            {
                Destroy(button);
            }
        }
        spawnedButtons.Clear();
    }
    
    private void ShowUI()
    {
        if (panel != null)
        {
            panel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }
    
    private void HideUI()
    {
        if (panel != null)
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        
        ClearPreviousChoices();
    }
}
