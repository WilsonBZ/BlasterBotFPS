using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WeaponPickup : MonoBehaviour
{
    public ModularWeapon weaponPrefab;
    public KeyCode interactKey = KeyCode.E;

    bool playerNearby = false;
    ArmMount nearbyArm = null;

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c) c.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            nearbyArm = other.GetComponentInChildren<ArmMount>();
            playerNearby = nearbyArm != null;
        }
    }
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            nearbyArm = null;
        }
    }

    void Update()
    {
        if (!playerNearby || nearbyArm == null || weaponPrefab == null) return;
        if (Input.GetKeyDown(KeyCode.F))
        {
            int slot = nearbyArm.AttachWeapon(weaponPrefab);
            if (slot >= 0) Destroy(gameObject);
            else Debug.Log("No free slot on arm mount.");
        }
    }
}
