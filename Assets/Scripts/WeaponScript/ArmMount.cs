using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class ArmMount : MonoBehaviour
{
    [Tooltip("Transforms that represent the N slots on the ring. If empty the script auto-creates slots on Awake.")]
    [SerializeField] private Transform[] slotPoints;


    [Tooltip("Parent transform under which weapon instances will be created/parented. Typically a child of the player.")]
    [SerializeField] private Transform weaponsParent;


    [Tooltip("Reference to an optional battery. Passes to TryFire to allow battery-driven weapons.")]
    [SerializeField] private ArmBattery battery;


    [Tooltip("Number of slots to auto-create when slotPoints is empty")]
    [SerializeField] private int defaultSlotCount = 6;


    [Tooltip("Angle step when rotating the ring (deg). If zero it'll compute 360/slotCount")]
    [SerializeField] private float manualAngleStep = 0f;


    [Tooltip("Optional: Let the mount handle rotation input (Q to rotate clockwise). Disable if you want your own input handling.")]
    [SerializeField] private bool handleInput = true;


    private List<ModularWeapon> attachedWeapons = new List<ModularWeapon>();


    // Which slot index is currently considered the center (aimed by crosshair)
    private int centerIndex = 0;


    // computed angle step
    private float angleStep;


    private void Awake()
    {
        if (weaponsParent == null) weaponsParent = this.transform;


        if (slotPoints == null || slotPoints.Length == 0)
            CreateAutoSlots(defaultSlotCount);


        angleStep = (manualAngleStep > 0f) ? manualAngleStep : (360f / slotPoints.Length);


        attachedWeapons = new List<ModularWeapon>(new ModularWeapon[slotPoints.Length]);


        // Initialize center state (if slot has a weapon later we will update it)
        UpdateCenterStates();
    }


    private void Update()
    {
        if (!handleInput) return;


        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotateCenterClockwise();
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            RotateCenterCounterClockwise();
        }


        // FireAll while holding Fire1 (player requested behavior)
        if (Input.GetButton("Fire1"))
        {
            FireAll();
        }
    }


    private void CreateAutoSlots(int count)
    {
        slotPoints = new Transform[count];
        float radius = 0.8f;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Slot_{i}");
            go.transform.SetParent(transform, false);
            float angle = (i / (float)count) * Mathf.PI * 2f;
            go.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0.0f, Mathf.Sin(angle) * radius);
            go.transform.localRotation = Quaternion.identity;
            slotPoints[i] = go.transform;
        }
    }

    public int AttachWeapon(ModularWeapon weaponPrefab)
    {
        if (weaponPrefab == null) return -1;
        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;


        var instanceGO = Instantiate(weaponPrefab.gameObject, weaponsParent);
        var instance = instanceGO.GetComponent<ModularWeapon>();
        instance.transform.SetParent(slotPoints[slot], false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;


        attachedWeapons[slot] = instance;
        instance.SetParentMount(this, slot);


        UpdateCenterStates();
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
            w.ClearParentMount();
        }
        else
        {
            w.ClearParentMount();
            Destroy(w.gameObject);
        }


        // If the detached slot was the center, advance center so the player always has a center slot (optional)
        if (slotIndex == centerIndex)
        {
            centerIndex = Mathf.Clamp(centerIndex, 0, attachedWeapons.Count - 1);
            UpdateCenterStates();
        }


        return w;
    }

    public bool FireSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= attachedWeapons.Count) return false;
        var w = attachedWeapons[slotIndex];
        if (w == null) return false;


        Camera cam = Camera.main; // center uses crosshair if available
        return w.TryFire(battery, cam);
    }


    public void FireAll()
    {
        Camera cam = Camera.main;
        for (int i = 0; i < attachedWeapons.Count; i++)
        {
            var w = attachedWeapons[i];
            if (w == null) continue;
            // Pass cam so center gun can use crosshair. Others will ignore it.
            w.TryFire(battery, cam);
        }
    }


    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < attachedWeapons.Count; i++) if (attachedWeapons[i] == null) return i;
        return -1;
    }


    public ModularWeapon[] GetAttachedWeaponsSnapshot() => attachedWeapons.ToArray();


    // Rotation API: rotates the weaponsParent visually so the next slot moves to 'center'


    public void RotateCenterClockwise()
    {
        RotateCenterBy(1);
    }


    public void RotateCenterCounterClockwise()
    {
        RotateCenterBy(-1);
    }

    public void RotateCenterBy(int steps)
    {
        if (slotPoints == null || slotPoints.Length == 0) return;
        int slotCount = slotPoints.Length;
        centerIndex = (centerIndex + steps) % slotCount;
        if (centerIndex < 0) centerIndex += slotCount;


        // rotate visual parent around Y by angleStep * steps (negative so rotating ring moves selected slot to front)
        float rotationAngle = -angleStep * steps;
        weaponsParent.Rotate(Vector3.up, rotationAngle, Space.Self);


        UpdateCenterStates();
    }


    private void UpdateCenterStates()
    {
        for (int i = 0; i < attachedWeapons.Count; i++)
        {
            var w = attachedWeapons[i];
            if (w != null)
            {
                w.SetCenterState(i == centerIndex);
            }
        }
    }
}