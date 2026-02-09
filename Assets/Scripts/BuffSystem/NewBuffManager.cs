using System;
using UnityEngine;

[DisallowMultipleComponent]
public class NewBuffManager : MonoBehaviour
{
    public static NewBuffManager Instance { get; private set; }
    
    [Header("Configuration")]
    public BuffLibrary buffLibrary;
    public int buffChoicesPerReward = 3;
    
    [Header("References")]
    public BuffSelectionUI selectionUI;
    
    public event Action<BuffData> OnBuffApplied;
    
    private BuffRandomizer randomizer;
    private PlayerManager cachedPlayer;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (buffLibrary != null)
        {
            randomizer = new BuffRandomizer(buffLibrary);
        }
        else
        {
            Debug.LogWarning("NewBuffManager: No BuffLibrary assigned!");
        }
    }
    
    private void Start()
    {
        cachedPlayer = FindFirstObjectByType<PlayerManager>();
        
        if (selectionUI == null)
        {
            selectionUI = FindFirstObjectByType<BuffSelectionUI>();
        }
    }
    
    public void ShowBuffSelection()
    {
        if (randomizer == null)
        {
            Debug.LogError("NewBuffManager: BuffRandomizer not initialized!");
            return;
        }
        
        if (selectionUI == null)
        {
            Debug.LogError("NewBuffManager: BuffSelectionUI not found!");
            return;
        }
        
        var buffChoices = randomizer.GetRandomBuffs(buffChoicesPerReward);
        selectionUI.ShowBuffChoices(buffChoices, ApplyBuff);
    }
    
    public void ApplyBuff(BuffData buff)
    {
        if (buff == null)
        {
            Debug.LogWarning("NewBuffManager: Tried to apply null buff!");
            return;
        }
        
        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<PlayerManager>();
        }
        
        if (cachedPlayer == null)
        {
            Debug.LogWarning("NewBuffManager: PlayerManager not found!");
            return;
        }
        
        buff.ApplyBuff(cachedPlayer);
        OnBuffApplied?.Invoke(buff);
    }
}
