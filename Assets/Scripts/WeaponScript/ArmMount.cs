using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmMount : MonoBehaviour
{
    [Header("Slots / Ring")]
    [Tooltip("If empty, slots will be auto-generated across the specified arc")]
    public Transform[] slotPoints;
    public Transform weaponsParent; 
    public int slotCount = 7;       
    [Tooltip("Total arc in degrees across which the slots live (e.g., 180 => forward hemisphere)")]
    public float arcDegrees = 180f;
    [Tooltip("Radius for auto-generated slots")]
    public float radius = 0.9f;

    [Header("Battery")]
    public ArmBattery battery;
    [Tooltip("How much more battery the center gun consumes (multiplier)")]
    public float centerMultiplier = 1.5f;

    [Header("Input")]
    public bool handleInput = true;
    public KeyCode rotateClockwiseKey = KeyCode.Q;
    public KeyCode rotateCounterClockwiseKey = KeyCode.E;
    public KeyCode tossCenterKey = KeyCode.G;

    List<ModularWeapon> attached = new List<ModularWeapon>();
    int centerIndex = 0;
    float slotAngleStep => (slotPoints != null && slotPoints.Length > 0) ? (arcDegrees / slotPoints.Length) : (arcDegrees / Mathf.Max(1, slotCount));

    void Awake()
    {
        if (weaponsParent == null) weaponsParent = transform;

        if (slotPoints == null || slotPoints.Length == 0)
            CreateSlotsAuto(slotCount);

        attached = new List<ModularWeapon>(new ModularWeapon[slotPoints.Length]);
        centerIndex = Mathf.FloorToInt(slotPoints.Length / 2f); 
        UpdateCenterStates();
    }

    void Update()
    {
        if (handleInput)
        {
            if (Input.GetKeyDown(rotateClockwiseKey)) RotateCenterBy(1);
            if (Input.GetKeyDown(rotateCounterClockwiseKey)) RotateCenterBy(-1);
            if (Input.GetKeyDown(tossCenterKey)) TossCenterGun();

            if (Input.GetButton("Fire1"))
            {
                FireAll();
            }
        }
    }

    void CreateSlotsAuto(int count)
    {
        slotPoints = new Transform[count];
        float startAngle = -arcDegrees * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"Slot_{i}");
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
        for (int i = 0; i < attached.Count; i++) if (attached[i] == null) return i;
        return -1;
    }

    public void RotateCenterBy(int steps)
    {
        if (slotPoints == null || slotPoints.Length == 0) return;
        int len = slotPoints.Length;
        centerIndex = (centerIndex + steps) % len;
        if (centerIndex < 0) centerIndex += len;

        float anglePerSlot = arcDegrees / Mathf.Max(1, slotPoints.Length);
        weaponsParent.Rotate(Vector3.up, -anglePerSlot * steps, Space.Self);

        UpdateCenterStates();
    }

    void UpdateCenterStates()
    {
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w != null) w.SetCenterState(i == centerIndex);
        }
    }

    public bool FireSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= attached.Count) return false;
        var w = attached[slotIndex];
        if (w == null) return false;
        Camera cam = Camera.main;
        return w.TryFire(battery, cam, centerMultiplier);
    }

    public void FireAll()
    {
        Camera cam = Camera.main;
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w == null) continue;
            w.TryFire(battery, cam, centerMultiplier);
        }
    }

    public void TossCenterGun(float forwardForce = 5f, float upForce = 1.2f)
    {
        if (centerIndex < 0 || centerIndex >= attached.Count) return;
        var w = attached[centerIndex];
        if (w == null) return;

        Vector3 dir = Vector3.forward;
        if (w.firePoint != null) dir = w.firePoint.forward;
        else if (slotPoints != null && centerIndex < slotPoints.Length && slotPoints[centerIndex] != null)
            dir = (slotPoints[centerIndex].forward);

        attached[centerIndex] = null;

        w.TossOut(dir, forwardForce, upForce);

        if (attached.Count > 0)
        {
            centerIndex = Mathf.Clamp(centerIndex, 0, attached.Count - 1);
        }
        UpdateCenterStates();
    }
}
