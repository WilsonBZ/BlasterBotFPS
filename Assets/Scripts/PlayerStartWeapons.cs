using UnityEngine;

public class PlayerStartWeapons : MonoBehaviour
{
    [Header("Starting Weapons")]
    [SerializeField] private ModularWeapon[] startingWeapons;
    
    private ArmMount360 armMount;

    private void Awake()
    {
        armMount = GetComponentInChildren<ArmMount360>();
        
        if (armMount == null)
        {
            Debug.LogError("PlayerStartWeapons: No ArmMount360 found on player!");
            return;
        }
    }

    private void Start()
    {
        EquipStartingWeapons();
    }

    private void EquipStartingWeapons()
    {
        if (startingWeapons == null || startingWeapons.Length == 0)
        {
            Debug.LogWarning("PlayerStartWeapons: No starting weapons assigned!");
            return;
        }

        foreach (ModularWeapon weaponPrefab in startingWeapons)
        {
            if (weaponPrefab == null)
            {
                continue;
            }

            int slot = armMount.AttachWeapon(weaponPrefab);
            
            if (slot < 0)
            {
                Debug.LogWarning($"PlayerStartWeapons: Could not attach weapon {weaponPrefab.name} - no free slots.");
            }
        }
    }
}
