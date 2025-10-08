using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmMount : MonoBehaviour
{
    [Tooltip("Slot Transforms where weapons will be parented. If left empty, 6 auto slots are created as children.")]
    [SerializeField] private Transform[] slotPoints;
    [SerializeField] private Transform weaponsParent;
    [SerializeField] private ArmBattery battery;

    [Header("Player input auto-fire")]
    [Tooltip("If true, the ArmMount will listen for player input and fire attached weapons accordingly.")]
    [SerializeField] private bool allowPlayerAutoFire = true;
    [Tooltip("Input button name to listen for (GetButton/GetButtonDown)")]
    [SerializeField] private string fireButton = "Fire1";

    private List<ModularWeapon> attachedWeapons = new List<ModularWeapon>();

    private void Awake()
    {
        if (weaponsParent == null) weaponsParent = this.transform;

        if (slotPoints == null || slotPoints.Length == 0)
        {
            // auto-generate 6 evenly spaced empty slots around the parent if none assigned
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

        // ensure attachedWeapons list length matches slots (contains nulls)
        attachedWeapons = new List<ModularWeapon>(new ModularWeapon[slotPoints.Length]);
    }

    private void Update()
    {
        if (!allowPlayerAutoFire) return;

        // Single press: attempt one shot for every attached weapon (semi-auto compatible)
        if (Input.GetButtonDown(fireButton))
        {
            FireAllOnce();
        }

        // Hold: attempt to fire only weapons that are automatic
        if (Input.GetButton(fireButton))
        {
            FireAllHold();
        }
    }

    /// <summary>
    /// Attach a prefab to the first empty slot. Returns slot index or -1 if failed.
    /// </summary>
    public int AttachWeapon(ModularWeapon weaponPrefab)
    {
        if (weaponPrefab == null) return -1;

        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;

        var instance = Instantiate(weaponPrefab.gameObject, weaponsParent).GetComponent<ModularWeapon>();
        instance.transform.SetParent(slotPoints[slot], false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        // Treat as attached weapon
        instance.isMainGun = false;

        attachedWeapons[slot] = instance;
        instance.SetParentMount(this, slot);

        // no Equip() call needed - SetParentMount is sufficient
        return slot;
    }

    /// <summary>
    /// Attach an already-instantiated ModularWeapon instance.
    /// </summary>
    public int AttachExistingWeapon(ModularWeapon instance)
    {
        if (instance == null) return -1;
        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;

        instance.transform.SetParent(slotPoints[slot], false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.isMainGun = false;

        attachedWeapons[slot] = instance;
        instance.SetParentMount(this, slot);
        return slot;
    }

    /// <summary>
    /// Removes weapon from slot and optionally drops it into the world. Returns the weapon instance (or null).
    /// </summary>
    public ModularWeapon DetachWeapon(int slotIndex, bool drop = true)
    {
        if (slotIndex < 0 || slotIndex >= attachedWeapons.Count) return null;
        var w = attachedWeapons[slotIndex];
        if (w == null) return null;

        attachedWeapons[slotIndex] = null;

        if (drop)
        {
            // unparent and leave it in world
            w.transform.SetParent(null, true);
            w.isMainGun = false;
            w.ClearParentMount();
        }
        else
        {
            // destroy immediately (slot freed)
            w.ClearParentMount();
            Destroy(w.gameObject);
        }

        return w;
    }

    /// <summary>
    /// Fire the weapon in a single slot (if present). Returns true if a shot was produced.
    /// </summary>
    public bool FireSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= attachedWeapons.Count) return false;
        var w = attachedWeapons[slotIndex];
        if (w == null) return false;
        return w.TryFire(battery);
    }

    /// <summary>
    /// Attempt one shot from every attached weapon (used on button down).
    /// Works for both automatic and semi-auto weapons (every weapon attempts one shot).
    /// </summary>
    public void FireAllOnce()
    {
        for (int i = 0; i < attachedWeapons.Count; i++)
        {
            var w = attachedWeapons[i];
            if (w == null) continue;
            w.TryFire(battery);
        }
    }

    /// <summary>
    /// Attempt to fire only automatic weapons. Should be called while the fire button is held.
    /// </summary>
    public void FireAllHold()
    {
        for (int i = 0; i < attachedWeapons.Count; i++)
        {
            var w = attachedWeapons[i];
            if (w == null) continue;
            if (w.IsAutomatic)
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
