using UnityEngine;

[CreateAssetMenu(fileName = "New Buff", menuName = "Buff System/Buff Data")]
public class BuffData : ScriptableObject
{
    [Header("Basic Info")]
    public string buffName = "New Buff";
    [TextArea(2, 4)]
    public string description = "Buff description";

    [Tooltip("Small icon shown in the centre of the card (classic mode).")]
    public Sprite icon;

    [Tooltip("Full card artwork. When assigned and the UI is set to Card Sprite mode, " +
             "this image fills the entire card and text elements are hidden.")]
    public Sprite cardSprite;

    [Header("Rarity")]
    [Range(1, 100)]
    public int weight = 50;

    [Header("Effect")]
    public BuffEffect effect;

    /// <summary>Applies the buff effect to the given player.</summary>
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
