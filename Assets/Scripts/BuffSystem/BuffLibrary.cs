using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BuffLibrary", menuName = "Buff System/Buff Library")]
public class BuffLibrary : ScriptableObject
{
    [Header("All Available Buffs")]
    public List<BuffData> allBuffs = new List<BuffData>();
    
    public bool IsEmpty => allBuffs == null || allBuffs.Count == 0;
    
    public void ValidateBuffs()
    {
        if (allBuffs == null) return;
        
        allBuffs.RemoveAll(buff => buff == null);
    }
}
