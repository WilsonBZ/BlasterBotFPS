using UnityEngine;

public abstract class BuffEffect : ScriptableObject
{
    public abstract void Apply(PlayerManager player);
    public abstract string GetDescription();
}
