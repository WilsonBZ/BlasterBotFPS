using UnityEngine;

/// <summary>
/// Lightweight tag component placed on each buff card GameObject so
/// BuffSelectionUI can retrieve which BuffData the card represents on click.
/// </summary>
public class BuffCardData : MonoBehaviour
{
    public BuffData Buff { get; set; }
}
