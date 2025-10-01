using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class DamageNumber : MonoBehaviour
{
    public void Initialize(float damageAmount, bool isCritical = false)
    {
        TextMeshProUGUI text = GetComponent<TextMeshProUGUI>();
        text.text = Mathf.RoundToInt(damageAmount).ToString();
        Destroy(gameObject, 1f);
    }
}