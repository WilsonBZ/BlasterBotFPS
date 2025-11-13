using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmMount360 : MonoBehaviour
{
    [Header("Ring configuration")]
    [Tooltip("Number of slots (should be odd, e.g. 7).")]
    public int slotCount = 7;
    [Tooltip("Radius of ring in local space.")]
    public float radius = 1.0f;
    [Tooltip("If true, auto-generate the slot transforms as children.")]
    public bool autoGenerateSlots = true;

    [Header("Visual parent (rotates to show active center)")]
    [Tooltip("Parent transform that holds the weapon instances. Rotating this will rotate the ring visually.")]
    public Transform weaponsParent;

    [Header("Gameplay")]
    public ArmBattery battery;
    [Tooltip("How many shots per second the mount can perform (global fire rate).")]
    public float fireRate = 3f;
    public float turnRate = 3f;
    [Tooltip("Energy multiplier applied when firing (if you want center to cost more).")]
    public float centerMultiplier = 1f;

    [Header("Rotation tween")]
    [Tooltip("Seconds for ring to rotate visually when advancing to next gun.")]
    public float rotateDuration = 0.12f;
    [Tooltip("Easing exponent for Lerp (1 = linear).")]
    public float rotateEase = 1.0f;

    [Header("Input (keys)")]
    public KeyCode tossKey = KeyCode.G; 
    public bool handleInput = true;
    public float modelYawOffset = -90f;

    
    private Transform[] slotTransforms;           
    private List<ModularWeapon> attached;         
    private int centerIndex;                      
    private float lastShotTime = -999f;
    private float nextFireTime;
    private float currentParentRotationY = 0f;    
    private Coroutine rotateCoroutine = null;

    private void Awake()
    {
        if (weaponsParent == null) weaponsParent = this.transform;

        if (slotCount <= 1) slotCount = 7; 

        slotTransforms = new Transform[slotCount];
        attached = new List<ModularWeapon>(new ModularWeapon[slotCount]);

        if (autoGenerateSlots || slotTransforms == null)
            CreateSlotTransforms();

        centerIndex = 0;

        currentParentRotationY = weaponsParent.localEulerAngles.y;

        if (battery == null)
            battery = FindFirstObjectByType<ArmBattery>();
    }

    private void Update()
    {
        if (!handleInput) return;

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            AttemptFireAndAdvance(-1);
        }
        else if (Input.GetButton("Fire2") && Time.time >= nextFireTime)
        {
            AttemptFireAndAdvance(+1);
        }

        if (Input.GetKeyDown(tossKey))
        {
            TossCenterGun();
        }
    }

    private void CreateSlotTransforms()
    {
        float angleStep = 360f / slotCount;
        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotGO = new GameObject($"RingSlot_{i}");
            slotGO.transform.SetParent(weaponsParent, false);

            float angle = i * angleStep; 
            float rad = Mathf.Deg2Rad * angle;
            Vector3 localPos = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            slotGO.transform.localPosition = localPos;

            slotGO.transform.localRotation = Quaternion.Euler(0f, angle + modelYawOffset, 0f);
            Vector3 lookDir = (slotGO.transform.position - weaponsParent.position).normalized;
            if (lookDir.sqrMagnitude < 0.0001f) lookDir = Vector3.forward;
            slotGO.transform.localRotation = Quaternion.LookRotation(lookDir, Vector3.up);

            slotTransforms[i] = slotGO.transform;
        }
    }

    public int AttachWeapon(ModularWeapon prefab)
    {
        if (prefab == null) return -1;
        int slot = FindFirstEmptySlot();
        if (slot < 0) return -1;

        GameObject instGO = Instantiate(prefab.gameObject, weaponsParent);
        ModularWeapon inst = instGO.GetComponent<ModularWeapon>();
        inst.transform.SetParent(slotTransforms[slot], false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;

        attached[slot] = inst;
        inst.SetParentMount(this, slot);

        return slot;
    }

    int FindFirstEmptySlot()
    {
        for (int i = 0; i < attached.Count; i++)
            if (attached[i] == null) return i;
        return -1;
    }

    private void AttemptFireAndAdvance(int direction)
    {
        nextFireTime = Time.time + fireRate;
        float minInterval = 1f / Mathf.Max(0.0001f, turnRate);
        if (Time.time - lastShotTime < minInterval) return;

        int idx = centerIndex;
        ModularWeapon w = attached[idx];

        bool shotFired = false;

        if (w != null)
        {
            Camera cam = Camera.main;
            float mult = centerMultiplier;
            shotFired = w.TryFire(battery, cam, mult);
        }
        else
        {
            shotFired = false;
        }

        lastShotTime = Time.time;

        AdvanceBy(direction);
    }

    private void AdvanceBy(int delta)
    {
        if (slotCount <= 0) return;

        int newIndex = (centerIndex + delta) % slotCount;
        if (newIndex < 0) newIndex += slotCount;

        float angleStep = 360f / slotCount;
        float targetSlotAngle = newIndex * angleStep; 
        float targetParentY = -targetSlotAngle;

        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = StartCoroutine(AnimateParentRotation(currentParentRotationY, targetParentY, rotateDuration, rotateEase));

        centerIndex = newIndex;
    }

    private IEnumerator AnimateParentRotation(float startY, float endY, float duration, float ease)
    {
        float t = 0f;
        float from = NormalizeAngle(startY);
        float to = NormalizeAngle(endY);
        float delta = ShortestAngleDelta(from, to);

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(duration <= 0f ? 1f : t / duration);
            if (Mathf.Abs(ease - 1f) > 0.001f)
                u = Mathf.Pow(u, ease);

            float current = from + delta * u;
            weaponsParent.localEulerAngles = new Vector3(0f, current, 0f);
            currentParentRotationY = current;
            yield return null;
        }

        weaponsParent.localEulerAngles = new Vector3(0f, to, 0f);
        currentParentRotationY = to;
        rotateCoroutine = null;
    }

    private float NormalizeAngle(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        return a;
    }

    private float ShortestAngleDelta(float a, float b)
    {
        float diff = b - a;
        diff = Mathf.Repeat(diff + 180f, 360f) - 180f;
        return diff;
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
        else dir = (slotTransforms[target] != null) ? slotTransforms[target].forward : transform.forward;

        w.TossOut(dir, forwardForce, upForce);

    }

    bool AllSlotsEmpty()
    {
        for (int i = 0; i < attached.Count; i++) if (attached[i] != null) return false;
        return true;
    }

    int FindNearestOccupiedSlot(int start)
    {
        if (attached[start] != null) return start;
        int n = attached.Count;
        for (int d = 1; d < n; d++)
        {
            int r = (start + d) % n;
            int l = (start - d + n) % n;
            if (attached[r] != null) return r;
            if (attached[l] != null) return l;
        }
        return start;
    }

    public void FireCenterAndAdvance(int direction)
    {
        AttemptFireAndAdvance(direction);
    }

    public ModularWeapon[] GetAttachedSnapshot()
    {
        return attached.ToArray();
    }
}
