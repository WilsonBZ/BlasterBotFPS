using System;
using System.Collections.Generic;

using UnityEngine;

[DisallowMultipleComponent]
public class BuffManager : MonoBehaviour
{
    public static BuffManager Instance
    {
        get; private set;
    }
    public const string SaveKey = "Buffs_v1";
    public event Action OnBuffsChanged;

    [Serializable]
    private struct BuffEntry
    {
        public BuffType Type; public int Count;
    }

    [Serializable]
    private struct BuffSave
    {
        public BuffEntry[] Entries;
    }

    private readonly Dictionary<BuffType, int> counts = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
        Debug.Log("BuffManager Awake: Loaded buffs.");
    }

    private void Start()
    {
        // Only for testing:
        AddBuff(BuffType.Heal, 3);
        AddBuff(BuffType.Pellets, 3);
        AddBuff(BuffType.Spread, 3);
    }

    public void AddBuff(BuffType type, int amount = 1)
    {
        if (amount <= 0) return;
        int cur = GetCount(type);
        counts[type] = cur + amount;
        Save();
        OnBuffsChanged?.Invoke();
        Debug.Log($"BuffManager: Added {amount} {type}, new count = {counts[type]}");
    }

    public bool UseBuff(BuffType type)
    {
        int cur = GetCount(type);
        if (cur <= 0)
        {
            Debug.Log($"BuffManager: Tried to use {type} but count = 0");
            return false;
        }
        counts[type] = cur - 1;
        Save();
        OnBuffsChanged?.Invoke();
        Debug.Log($"BuffManager: Used 1 {type}, remaining = {counts[type]}");
        return true;
    }

    public int GetCount(BuffType type) => counts.TryGetValue(type, out var v) ? v : 0;

    public void ResetToDefaults()
    {
        counts.Clear();
        Save();
        OnBuffsChanged?.Invoke();
        Debug.Log("BuffManager: Reset buffs to defaults");
    }

    public void Save()
    {
        var entries = new List<BuffEntry>();
        foreach (var kv in counts)
            if (kv.Value > 0)
                entries.Add(new BuffEntry { Type = kv.Key, Count = kv.Value });

        string json = JsonUtility.ToJson(new BuffSave { Entries = entries.ToArray() });
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save();
        Debug.Log("BuffManager: Saved buffs");
    }

    public void Load()
    {
        counts.Clear();
        if (!PlayerPrefs.HasKey(SaveKey)) return;

        try
        {
            string json = PlayerPrefs.GetString(SaveKey);
            var save = JsonUtility.FromJson<BuffSave>(json);
            if (save.Entries != null)
                foreach (var e in save.Entries)
                    counts[e.Type] = e.Count;
            Debug.Log("BuffManager: Buffs loaded from PlayerPrefs");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"BuffManager: Failed to load buffs: {ex.Message}");
        }

        OnBuffsChanged?.Invoke();
    }
}

public enum BuffType
{
    Heal, Pellets, Spread
}
