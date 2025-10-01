using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class ArmMount : MonoBehaviour
{
    [Tooltip("Slot Transforms where weapons will be parented. If left empty, 6 auto slots are created as children.")]
    [SerializeField] private Transform[] slotPoints;
    [SerializeField] private Transform weaponsParent;
    [SerializeField] private ArmBattery battery;


    [Tooltip("If true, attached weapons will set their playerCamera to Camera.main if none assigned.")]
    [SerializeField] private bool assignMainCameraIfMissing = true;


    private List<ModularWeapon> attachedWeapons = new List<ModularWeapon>();


    private void Awake()
    {
        if (weaponsParent == null) weaponsParent = this.transform;


        if (slotPoints == null || slotPoints.Length == 0)
        {
            int autoSlots = 6;
            slotPoints = new Transform[autoSlots];
            float radius = 0.6f;
            for (int i = 0; i < autoSlots; i++)
            {
                GameObject g = new GameObject($"Slot_{i}");
                g.transform.SetParent(transform, false);
                float angle = (i / (float)autoSlots) * Mathf.PI * 2f;
                g.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * 0.1f, Mathf.Sin(angle) * radius);
                g.transform.localRotation = Quaternion.identity;
                slotPoints[i] = g.transform;
            }
        }


        attachedWeapons = new List<ModularWeapon>(new ModularWeapon[slotPoints.Length]);
    }

    public int AttachWeapon(ModularWeapon weaponPrefab)
    {
        if (weaponPrefab == null) return -1;


        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;


        var instance = Instantiate(weaponPrefab.gameObject, weaponsParent).GetComponent<ModularWeapon>();
        instance.transform.SetParent(slotPoints[slot], false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;


        instance.isMainGun = false;


        if (assignMainCameraIfMissing && instance.playerCamera == null && Camera.main != null)
            instance.playerCamera = Camera.main;


        attachedWeapons[slot] = instance;
        instance.Equip();
        Debug.Log("Equip");


        return slot;
    }

    public int AttachExistingWeapon(ModularWeapon instance)
    {
        if (instance == null) return -1;
        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;


        instance.transform.SetParent(slotPoints[slot], false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.isMainGun = false;


        if (assignMainCameraIfMissing && instance.playerCamera == null && Camera.main != null)
            instance.playerCamera = Camera.main;


        attachedWeapons[slot] = instance;
        instance.Equip();
        return slot;
    }

    public ModularWeapon DetachWeapon(int slotIndex, bool drop = true)
    {
        if (slotIndex < 0 || slotIndex >= attachedWeapons.Count) return null;
        var w = attachedWeapons[slotIndex];
        if (w == null) return null;


        attachedWeapons[slotIndex] = null;


        if (drop)
        {
            w.transform.SetParent(null, true);
            w.isMainGun = false;
        }
        else
        {
            Destroy(w.gameObject);
        }


        return w;
    }

    public bool FireSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= attachedWeapons.Count) return false;
        var w = attachedWeapons[slotIndex];
        if (w == null) return false;
        return w.TryFire(battery);
    }

    public void FireAll()
    {
        for (int i = 0; i < attachedWeapons.Count; i++)
        {
            var w = attachedWeapons[i];
            if (w == null) continue;
            w.TryFire(battery);
        }
    }


    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < attachedWeapons.Count; i++)
        {
            if (attachedWeapons[i] == null) return i;
        }
        return -1;
    }

    public ModularWeapon[] GetAttachedWeaponsSnapshot()
    {
        return attachedWeapons.ToArray();
    }


    public ArmBattery GetBattery() => battery;


    
}
