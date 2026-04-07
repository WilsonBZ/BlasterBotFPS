using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            randomizer = new BuffRandomizer(buffLibrary);
        else
            Debug.LogWarning("[NewBuffManager] No BuffLibrary assigned!");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only re-cache on additive loads (room scenes). Avoids interfering with
        // non-additive loads if any remain elsewhere.
        if (mode == LoadSceneMode.Additive)
            RecacheReferences();
    }

    private void Start()
    {
        RecacheReferences();
    }

    /// <summary>
    /// Re-acquires PlayerManager and BuffSelectionUI from the current loaded scenes.
    /// Call this after any room reload or death restart.
    /// </summary>
    public void RecacheReferences()
    {
        cachedPlayer = FindFirstObjectByType<PlayerManager>();
        if (selectionUI == null)
            selectionUI = FindFirstObjectByType<BuffSelectionUI>();
    }
    
    /// <summary>Rolls 3 random buffs and shows the selection overlay.</summary>
    public void ShowBuffSelection()
    {
        if (randomizer == null)
        {
            Debug.LogError("[NewBuffManager] BuffRandomizer not initialized!");
            return;
        }
        
        if (selectionUI == null)
        {
            selectionUI = FindFirstObjectByType<BuffSelectionUI>();
            if (selectionUI == null)
            {
                Debug.LogError("[NewBuffManager] BuffSelectionUI not found in scene!");
                return;
            }
        }
        
        var choices = randomizer.GetRandomBuffs(buffChoicesPerReward);
        selectionUI.ShowBuffChoices(choices, ApplyBuff);
    }
    
    /// <summary>Applies a chosen buff to the player.</summary>
    public void ApplyBuff(BuffData buff)
    {
        if (buff == null) return;
        
        if (cachedPlayer == null)
            cachedPlayer = FindFirstObjectByType<PlayerManager>();
        
        if (cachedPlayer == null)
        {
            Debug.LogWarning("[NewBuffManager] PlayerManager not found!");
            return;
        }
        
        buff.ApplyBuff(cachedPlayer);
        OnBuffApplied?.Invoke(buff);
    }
}
