using System.Collections.Generic;
using UnityEngine;

public class BuffRandomizer
{
    private readonly BuffLibrary library;
    
    public BuffRandomizer(BuffLibrary buffLibrary)
    {
        library = buffLibrary;
    }
    
    public List<BuffData> GetRandomBuffs(int count)
    {
        if (library == null || library.IsEmpty)
        {
            Debug.LogWarning("BuffRandomizer: Library is empty or null!");
            return new List<BuffData>();
        }
        
        library.ValidateBuffs();
        
        List<BuffData> availableBuffs = new List<BuffData>(library.allBuffs);
        List<BuffData> selectedBuffs = new List<BuffData>();
        
        count = Mathf.Min(count, availableBuffs.Count);
        
        for (int i = 0; i < count; i++)
        {
            if (availableBuffs.Count == 0) break;
            
            BuffData selected = SelectWeightedRandom(availableBuffs);
            selectedBuffs.Add(selected);
            availableBuffs.Remove(selected);
        }
        
        return selectedBuffs;
    }
    
    private BuffData SelectWeightedRandom(List<BuffData> buffs)
    {
        int totalWeight = 0;
        foreach (BuffData buff in buffs)
        {
            totalWeight += buff.weight;
        }
        
        int randomValue = Random.Range(0, totalWeight);
        int currentWeight = 0;
        
        foreach (BuffData buff in buffs)
        {
            currentWeight += buff.weight;
            if (randomValue < currentWeight)
            {
                return buff;
            }
        }
        
        return buffs[buffs.Count - 1];
    }
}
