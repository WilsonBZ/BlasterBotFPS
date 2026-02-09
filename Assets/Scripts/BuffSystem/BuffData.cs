using UnityEngine;

[CreateAssetMenu(fileName = "New Buff", menuName = "Buff System/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("Basic Info")]
    public string buffName = "New Buff";
    [TextArea(2, 4)]
    public string description = "Buff description";
    public Sprite icon;
    
    [Header("Rarity")]
    [Range(1, 100)]
    public int weight = 50;
    
    [Header("Effect")]
    public BuffEffect effect;
    
    public void ApplyBuff(PlayerManager player)
    {
        if (effect != null)
        {
            effect.Apply(player);
            Debug.Log($"Applied buff: {buffName}");
        }
        else
        {
            Debug.LogWarning($"Buff {buffName} has no effect assigned!");
        }
    }
}
