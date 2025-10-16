using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmMount : MonoBehaviour
{
    [Header("Slots / Ring")]
    [Tooltip("If empty, slots will be auto-generated across the specified arc")]
    public Transform[] slotPoints;
    [Tooltip("Parent transform under which weapon instances will be created/parented. Usually a child so rotation/tweens affect visuals.")]
    public Transform weaponsParent;
    [Tooltip("How many slots to auto-create when none are provided (default 7)")]
    public int slotCount = 7;
    [Tooltip("Total arc in degrees (e.g. 180)")] public float arcDegrees = 180f;
    [Tooltip("Radius for auto-generated slots")] public float radius = 0.9f;

    [Header("Battery")]
    public ArmBattery battery;
    [Tooltip("Center gun consumes this multiplier of energyCostPerShot")] public float centerMultiplier = 1.5f;

    [Header("Input")]
    public bool handleInput = true;
    public KeyCode rotateRightKey = KeyCode.E;  
    public KeyCode rotateLeftKey = KeyCode.Q;   
    public KeyCode tossCenterKey = KeyCode.G;

    private List<ModularWeapon> attached; 
    private int centerIndex;              

    void Awake()
    {
        if (weaponsParent == null) weaponsParent = transform;

        if (slotPoints == null || slotPoints.Length == 0)
            CreateSlotsAuto(slotCount);

        attached = new List<ModularWeapon>(new ModularWeapon[slotPoints.Length]);
        centerIndex = slotPoints.Length / 2;

        UpdateCenterStates();
    }

    void Update()
    {
        if (!handleInput) return;

        if (Input.GetKeyDown(rotateRightKey))
        {
            ShiftAttached(+1);
        }
        if (Input.GetKeyDown(rotateLeftKey))
        {
            ShiftAttached(-1);
        }
        if (Input.GetKeyDown(tossCenterKey))
        {
            TossCenterGun();
        }

        if (Input.GetButton("Fire1"))
        {
            FireAll();
        }
    }

    void CreateSlotsAuto(int count)
    {
        slotPoints = new Transform[count];
        float startAngle = -arcDegrees * 0.5f;
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Slot_{i}");
            go.transform.SetParent(transform, false);
            float t = (count == 1) ? 0f : (i / (float)(count - 1));
            float angle = startAngle + t * arcDegrees;
            float rad = angle * Mathf.Deg2Rad;
            go.transform.localPosition = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            go.transform.localRotation = Quaternion.LookRotation((transform.position - go.transform.position).normalized);
            slotPoints[i] = go.transform;
        }
    }

    public int AttachWeapon(ModularWeapon prefab)
    {
        if (prefab == null) return -1;
        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;

        GameObject instGO = Instantiate(prefab.gameObject, weaponsParent);
        ModularWeapon inst = instGO.GetComponent<ModularWeapon>();
        inst.transform.SetParent(slotPoints[slot], false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;

        attached[slot] = inst;
        inst.SetParentMount(this, slot);

        UpdateCenterStates();
        return slot;
    }

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < attached.Count; i++)
            if (attached[i] == null) return i;
        return -1;
    }

    public void ShiftAttached(int shiftSlots)
    {
        if (attached == null || attached.Count == 0) return;
        int n = attached.Count;
        shiftSlots = ((shiftSlots % n) + n) % n;
        if (shiftSlots == 0) return;

        ModularWeapon[] newArr = new ModularWeapon[n];
        for (int i = 0; i < n; i++)
        {
            int fromIndex = (i - shiftSlots + n) % n;
            newArr[i] = attached[fromIndex];
        }

        attached = new List<ModularWeapon>(newArr);
        ReparentAllToSlots();
        UpdateCenterStates();
    }

    void ReparentAllToSlots()
    {
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w == null) continue;
            w.transform.SetParent(slotPoints[i], false);
            w.transform.localPosition = Vector3.zero;
            w.transform.localRotation = Quaternion.identity;
            w.SetParentMount(this, i);
        }
    }

    public void TossCenterGun(float forwardForce = 5f, float upForce = 1.2f)
    {
        if (AllSlotsEmpty()) return;

        int target = centerIndex;
        if (attached[target] == null)
        {
            target = FindNearestOccupiedSlot(centerIndex);
            if (attached[target] == null) return;
        }

        ModularWeapon w = attached[target];
        attached[target] = null;

        Vector3 dir = Vector3.forward;
        if (w.firePoint != null) dir = w.firePoint.forward;
        else if (slotPoints != null && target < slotPoints.Length && slotPoints[target] != null)
            dir = slotPoints[target].forward;

        w.TossOut(dir, forwardForce, upForce);

        UpdateCenterStates();
    }

    bool AllSlotsEmpty()
    {
        for (int i = 0; i < attached.Count; i++)
            if (attached[i] != null) return false;
        return true;
    }

    int FindNearestOccupiedSlot(int startIndex)
    {
        if (attached[startIndex] != null) return startIndex;
        int n = attached.Count;
        for (int dist = 1; dist < n; dist++)
        {
            int r = (startIndex + dist) % n;
            int l = (startIndex - dist + n) % n;
            if (attached[r] != null) return r;
            if (attached[l] != null) return l;
        }
        return startIndex;
    }

    public bool FireSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= attached.Count) return false;
        var w = attached[slotIndex];
        if (w == null) return false;
        Camera cam = Camera.main;
        float mult = (slotIndex == centerIndex) ? centerMultiplier : 1f;
        return w.TryFire(battery, cam, mult);
    }

    public void FireAll()
    {
        Camera cam = Camera.main;
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w == null) continue;
            float mult = (i == centerIndex) ? centerMultiplier : 1f;
            w.TryFire(battery, cam, mult);
        }
    }

    void UpdateCenterStates()
    {
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w != null) w.SetCenterState(i == centerIndex);
        }
    }

    public ModularWeapon[] GetAttachedWeaponsSnapshot() => attached.ToArray();
}