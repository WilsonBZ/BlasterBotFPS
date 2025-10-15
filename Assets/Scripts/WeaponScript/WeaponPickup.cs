using UnityEngine;


[RequireComponent(typeof(Collider))]
public class WeaponPickup : MonoBehaviour
{
    public ModularWeapon weaponPrefab;
    public KeyCode interactKey = KeyCode.E;


    private bool playerNearby = false;
    private ArmMount nearbyArm = null;


    private void Reset() { var c = GetComponent<Collider>(); if (c) c.isTrigger = true; }


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            nearbyArm = other.GetComponentInChildren<ArmMount>();
            playerNearby = nearbyArm != null;
        }
    }
    private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) { playerNearby = false; nearbyArm = null; } }


    private void Update()
    {
        if (!playerNearby || nearbyArm == null || weaponPrefab == null) return;
        if (Input.GetKeyDown(interactKey))
        {
            int slot = nearbyArm.AttachWeapon(weaponPrefab);
            if (slot >= 0) Destroy(gameObject);
            else Debug.Log("ArmMount has no free slots.");
        }
    }
}