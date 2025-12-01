using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EnemyIndicatorManager : MonoBehaviour
{
    [Header("Indicator Settings")]
    public RectTransform crosshair;
    public GameObject indicatorPrefab;
    public float orbitRadius = 80f;
    public float pulseSpeed = 2f;

    private Dictionary<Transform, GameObject> activeIndicators = new();



    void Update()
    {
        List<Transform> toRemove = new();
        foreach (var kvp in activeIndicators)
        {
            if (kvp.Key == null)
            {
                Destroy(kvp.Value); // destroy the indicator UI
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            activeIndicators.Remove(key);
        }

        foreach (Transform enemy in FindEnemies())
        {
            Vector3 viewportPos = Camera.main.WorldToViewportPoint(enemy.position);
            bool isOffscreen = viewportPos.z < 0 || viewportPos.x < 0 || viewportPos.x > 1 || viewportPos.y < 0 || viewportPos.y > 1;

            if (isOffscreen)
            {
                if (!activeIndicators.ContainsKey(enemy))
                {
                    GameObject newIndicator = Instantiate(indicatorPrefab, crosshair.position, Quaternion.identity, crosshair.parent);
                    activeIndicators.Add(enemy, newIndicator);
                }

                UpdateIndicator(enemy, activeIndicators[enemy]);
            }
            else
            {
                if (activeIndicators.ContainsKey(enemy))
                {
                    Destroy(activeIndicators[enemy]);
                    activeIndicators.Remove(enemy);
                }
            }
        }
    }

    void UpdateIndicator(Transform enemy, GameObject indicator)
    {
        Vector3 direction = (enemy.position - Camera.main.transform.position).normalized;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        float angleRad = angle * Mathf.Deg2Rad;

        Vector2 offset = new Vector2(Mathf.Sin(angleRad), Mathf.Cos(angleRad)) * orbitRadius;
        indicator.transform.position = crosshair.position + (Vector3)offset;
        indicator.transform.rotation = Quaternion.Euler(0, 0, -angle);

        // Pulsing effect
        float scale = 1f + Mathf.Sin(Time.time * pulseSpeed) * 0.1f;
        indicator.transform.localScale = new Vector3(scale, scale, 1f);

        // Optional: Fade in (if using CanvasGroup)
        CanvasGroup cg = indicator.GetComponent<CanvasGroup>();
        if (cg) cg.alpha = Mathf.Lerp(cg.alpha, 0.5f, Time.deltaTime * 10f);
    }

    List<Transform> FindEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        List<Transform> list = new();
        foreach (var e in enemies)
        {
            list.Add(e.transform);
        }
        return list;
    }
}
