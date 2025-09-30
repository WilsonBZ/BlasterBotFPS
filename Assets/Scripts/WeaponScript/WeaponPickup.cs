using UnityEngine;


[RequireComponent(typeof(Collider))]
public class WeaponPickup : MonoBehaviour
{
    [Tooltip("Reference to a ModularWeapon prefab (not an instantiated scene object)")]
    public ModularWeapon weaponPrefab;


    [Tooltip("Optional: if set, the pickup will attempt to auto-attach to the player's ArmMount when entering trigger and pressing InteractKey.")]
    public KeyCode interactKey = KeyCode.E;


    private bool playerNearby = false;
    private ArmMount nearbyArm = null;


    private void Reset()
    {
        Collider c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            nearbyArm = other.GetComponentInChildren<ArmMount>();
            playerNearby = nearbyArm != null;
        }
    }


    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            nearbyArm = null;
        }
    }


    private void Update()
    {
        if (!playerNearby || nearbyArm == null || weaponPrefab == null) return;


        if (Input.GetKeyDown(interactKey))
        {
            Debug.Log("Interacted");
            int slot = nearbyArm.AttachWeapon(weaponPrefab);
            if (slot >= 0)
            {
                Destroy(gameObject); 
            }
            else
            {
                Debug.Log("ArmMount has no free slots.");
            }
        }
    }
}

