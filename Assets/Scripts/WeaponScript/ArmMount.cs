using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmMount : MonoBehaviour
{
    [Header("Slots / Ring")]
    public Transform[] slotPoints;
    public Transform weaponsParent;
    public int slotCount = 7;
    public float arcDegrees = 180f;
    public float radius = 0.9f;

    [Header("Battery")]
    public ArmBattery battery;
    public float centerMultiplier = 1.5f;

    [Header("Input")]
    public bool handleInput = true;
    public KeyCode rotateClockwiseKey = KeyCode.Q;
    public KeyCode rotateCounterClockwiseKey = KeyCode.E;
    public KeyCode tossCenterKey = KeyCode.G;

    private List<ModularWeapon> attached = new List<ModularWeapon>();
    private int centerIndex = 0;

    private int SlotCount => (slotPoints != null && slotPoints.Length > 0) ? slotPoints.Length : slotCount;

    void Awake()
    {
        if (weaponsParent == null) weaponsParent = transform;

        if (slotPoints == null || slotPoints.Length == 0)
            CreateSlotsAuto(slotCount);

        attached = new List<ModularWeapon>(new ModularWeapon[slotPoints.Length]);

        centerIndex = Mathf.FloorToInt(slotPoints.Length / 2f);

        EnsureCenterIsValid();
        UpdateCenterStates();
    }

    void Update()
    {
        if (!handleInput) return;

        if (Input.GetKeyDown(rotateClockwiseKey)) RotateCenterBy(1);
        if (Input.GetKeyDown(rotateCounterClockwiseKey)) RotateCenterBy(-1);
        if (Input.GetKeyDown(tossCenterKey)) TossCenterGun();

        if (Input.GetButton("Fire1")) FireAll();
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

        EnsureCenterIsValid();
        UpdateCenterStates();
        return slot;
    }

    public ModularWeapon DetachWeapon(int slotIndex, bool drop = true)
    {
        if (slotIndex < 0 || slotIndex >= attached.Count) return null;
        var w = attached[slotIndex];
        if (w == null) return null;

        attached[slotIndex] = null;

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

        EnsureCenterIsValid();
        UpdateCenterStates();

        return w;
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

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < attached.Count; i++)
            if (attached[i] == null) return i;
        return -1;
    }

    public void RotateCenterBy(int steps)
    {
        if (slotPoints == null || slotPoints.Length == 0) return;
        if (AllSlotsEmpty()) return;

        int len = attached.Count;

        int sign = (steps >= 0) ? 1 : -1;
        steps = Mathf.Abs(steps);

        for (int s = 0; s < steps; s++)
        {
            int next = FindNextOccupiedSlot(centerIndex, sign);
            if (next == centerIndex) break;
            float anglePerSlot = arcDegrees / Mathf.Max(1, slotPoints.Length);
            weaponsParent.Rotate(Vector3.up, -anglePerSlot * sign, Space.Self);

            centerIndex = next;
        }

        UpdateCenterStates();
    }

    public void TossCenterGun(float forwardForce = 5f, float upForce = 1.2f)
    {
        if (AllSlotsEmpty()) return;

        EnsureCenterIsValid();
        if (attached[centerIndex] == null)
        {
            return;
        }

        var w = attached[centerIndex];

        Vector3 dir = Vector3.forward;
        if (w.firePoint != null) dir = w.firePoint.forward;
        else if (slotPoints != null && centerIndex < slotPoints.Length && slotPoints[centerIndex] != null)
            dir = slotPoints[centerIndex].forward;

        attached[centerIndex] = null;

        w.TossOut(dir, forwardForce, upForce);

        EnsureCenterIsValid();
        UpdateCenterStates();
    }

    bool AllSlotsEmpty()
    {
        for (int i = 0; i < attached.Count; i++) if (attached[i] != null) return false;
        return true;
    }

    int FindNextOccupiedSlot(int startIndex, int step)
    {
        int len = attached.Count;
        int idx = startIndex;
        for (int i = 0; i < len; i++)
        {
            idx = (idx + step + len) % len;
            if (attached[idx] != null) return idx;
        }
        return startIndex;
    }

    int FindNearestOccupiedSlot(int startIndex)
    {
        if (attached[startIndex] != null) return startIndex;
        int len = attached.Count;
        for (int dist = 1; dist < len; dist++)
        {
            int right = (startIndex + dist) % len;
            int left = (startIndex - dist + len) % len;
            if (attached[right] != null) return right;
            if (attached[left] != null) return left;
        }
        return startIndex;
    }

    void EnsureCenterIsValid()
    {
        if (attached == null || attached.Count == 0) return;
        if (!AllSlotsEmpty())
        {
            if (attached[centerIndex] == null)
            {
                int newCenter = FindNearestOccupiedSlot(centerIndex);
                centerIndex = newCenter;
            }
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
