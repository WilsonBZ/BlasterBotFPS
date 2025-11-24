using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class ArmMount360 : MonoBehaviour
{
    [Header("Ring configuration")]
    public int slotCount = 7;
    public float radius = 1.0f;
    public bool autoGenerateSlots = true;

    [Header("Visual parent")]
    public Transform weaponsParent;

    [Header("Gameplay")]
    public ArmBattery battery;
    public float fireRate = 3f;
    public float turnRate = 0.12f;
    public float centerMultiplier = 1f;

    [Header("Rotation tween")]
    public float rotateDuration = 0.12f;
    public float rotateEase = 1.0f;
    public float modelYawOffset = -90f;

    [Header("Input")]
    public KeyCode tossKey = KeyCode.G;
    public KeyCode abilityKey = KeyCode.T;
    public bool handleInput = true;
    public bool allowScrollWheel = true;

    [Header("Frontal Ability")]
    public Transform[] lineSlots;
    public float abilityDuration = 6f;
    public float abilityCooldown = 10f;
    public float slideDuration = 0.18f;
    public bool rotateParentToCenterOnAbility = true;

    [Header("Center visuals")]
    public Vector3 centerLocalEulerOffset = Vector3.zero;
    public Vector3 centerLocalPositionOffset = new Vector3(-0.35f, -0.6f, 0.6f);

    [Header("Side visuals")]
    public Vector3 sideLocalPositionOffset = new Vector3(0.45f, -0.45f, 0.6f);
    public Vector3 sideLocalEulerOffset = new Vector3(20f, 0f, 0f);

    private Transform[] slotTransforms;
    private List<ModularWeapon> attached;
    private int centerIndex;
    private int sideIndex = -1;
    private float lastShotTime = -999f;
    private float lastRotateTime = -999f;
    private float currentParentRotationY = 0f;
    private Coroutine rotateCoroutine;

    private bool abilityActive;
    private bool abilityOnCooldown;

    private struct WeaponSlideState { public ModularWeapon weapon; public Transform originalParent; public Vector3 originalLocalPos; public Quaternion originalLocalRot; }
    private WeaponSlideState[] slideStates;

    private Quaternion[] originalSlotLocalRot;
    private Vector3[] originalSlotLocalPos;

    private MethodInfo mi_FireInternal;
    private FieldInfo fi_lastShotTime;

    public List<GameObject> WeaponSockets;

    private void Awake()
    {
        if (weaponsParent == null) weaponsParent = transform;
        if (slotCount <= 1) slotCount = 7;

        slotTransforms = new Transform[slotCount];
        attached = new List<ModularWeapon>(new ModularWeapon[slotCount]);
        slideStates = new WeaponSlideState[slotCount];
        originalSlotLocalRot = new Quaternion[slotCount];
        originalSlotLocalPos = new Vector3[slotCount];
        for (int i = 0; i < slotCount; i++) { originalSlotLocalRot[i] = Quaternion.identity; originalSlotLocalPos[i] = Vector3.zero; }

        if (autoGenerateSlots) CreateSlotTransforms();

        centerIndex = 0;
        currentParentRotationY = weaponsParent.localEulerAngles.y;
        sideIndex = -1;
        UpdateCenterIndex(0);

        if (battery == null) battery = FindFirstObjectByType<ArmBattery>();

        var mwType = typeof(ModularWeapon);
        mi_FireInternal = mwType.GetMethod("FireInternal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fi_lastShotTime = mwType.GetField("lastShotTime", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mi_FireInternal == null || fi_lastShotTime == null) Debug.LogWarning("ArmMount360: Reflection failed; ability will be disabled.");

        if (lineSlots != null && lineSlots.Length > 0 && lineSlots.Length != slotCount)
            Debug.LogWarning($"ArmMount360: lineSlots length ({lineSlots.Length}) != slotCount ({slotCount}).");
    }

    private void Update()
    {
        if (!handleInput) return;
        if (allowScrollWheel)
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (wheel > 0.0001f) TryRotate(-1);
            else if (wheel < -0.0001f) TryRotate(+1);
        }

        if (abilityActive) return;
        if (Input.GetButton("Fire1")) StartCoroutine(FireCenterOrRotateToNearestThenFire());
        if (Input.GetKeyDown(tossKey)) TossCenterGun();
        if (Input.GetKeyDown(abilityKey) && !abilityOnCooldown) StartCoroutine(TryStartAbility());
    }

    private IEnumerator TryStartAbility()
    {
        if (lineSlots == null || lineSlots.Length != slotCount) { Debug.LogError("ArmMount360: lineSlots invalid."); yield break; }
        if (mi_FireInternal == null || fi_lastShotTime == null) { Debug.LogError("ArmMount360: Ability aborted (reflection unavailable)."); yield break; }
        if (AllSlotsEmpty()) { Debug.Log("ArmMount360: Ability aborted — no weapons attached."); yield break; }
        yield return StartCoroutine(RunFrontAbilityCoroutine());
    }

    private void CreateSlotTransforms()
    {
        float angleStep = 360f / slotCount;
        for (int i = 0; i < slotCount; i++)
        {
            var slotGO = new GameObject($"RingSlot_{i}");
            slotGO.transform.SetParent(weaponsParent, false);
            WeaponSockets?.Add(slotGO);
            float angle = i * angleStep;
            float rad = Mathf.Deg2Rad * angle;
            var localPos = new Vector3(Mathf.Sin(rad) * radius, 0f, Mathf.Cos(rad) * radius);
            slotGO.transform.localPosition = localPos;
            var lookDir = (slotGO.transform.position - weaponsParent.position).normalized;
            if (lookDir.sqrMagnitude < 0.0001f) lookDir = Vector3.forward;
            slotGO.transform.localRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            slotTransforms[i] = slotGO.transform;
            originalSlotLocalRot[i] = Quaternion.identity;
            originalSlotLocalPos[i] = slotGO.transform.localPosition;
        }
    }

    public int AttachWeapon(ModularWeapon prefab)
    {
        if (prefab == null) return -1;
        int slot = attached.FindIndex(w => w == null);
        if (slot < 0) return -1;

        var instGO = Instantiate(prefab.gameObject, weaponsParent);
        var inst = instGO.GetComponent<ModularWeapon>();
        if (slotTransforms[slot] != null) { inst.transform.SetParent(slotTransforms[slot], false); inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; }
        else inst.transform.SetParent(weaponsParent, false);

        originalSlotLocalRot[slot] = inst.transform.localRotation;
        originalSlotLocalPos[slot] = inst.transform.localPosition;
        attached[slot] = inst;
        inst.SetParentMount(this, slot);

        if (slot == centerIndex) { inst.SetCenterState(true); ApplyCenterRotation(slot); ApplyCenterPosition(slot); }
        else if (slot == sideIndex) { ApplySidePosition(slot); ApplySideRotation(slot); }
        return slot;
    }

    private IEnumerator FireCenterOrRotateToNearestThenFire()
    {
        float minInterval = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastShotTime < minInterval) yield break;
        var centerWeapon = attached[centerIndex];
        if (centerWeapon != null) { centerWeapon.TryFire(battery, Camera.main, centerMultiplier); lastShotTime = Time.time; yield break; }

        int nearest = FindNearestOccupiedSlot(centerIndex);
        if (nearest == centerIndex || attached[nearest] == null) yield break;

        float angleStep = 360f / slotCount;
        float targetParentY = -nearest * angleStep;
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        yield return StartCoroutine(AnimateParentRotationBlocking(currentParentRotationY, targetParentY, rotateDuration, rotateEase));
        UpdateCenterIndex(nearest);
        var w = attached[centerIndex];
        if (w != null) { w.TryFire(battery, Camera.main, centerMultiplier); lastShotTime = Time.time; }
    }

    private void TryRotate(int delta)
    {
        if (Time.time - lastRotateTime < Mathf.Max(0.0001f, turnRate)) return;
        lastRotateTime = Time.time;
        AdvanceBy(delta);
    }

    private void AdvanceBy(int delta)
    {
        if (slotCount <= 0) return;
        int newIndex = (centerIndex + delta) % slotCount;
        if (newIndex < 0) newIndex += slotCount;
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = StartCoroutine(RotateAndSwapCoroutine(centerIndex, newIndex, rotateDuration, rotateEase));
    }

    private IEnumerator RotateAndSwapCoroutine(int oldIndex, int newIndex, float duration, float ease)
    {
        if (oldIndex == newIndex) { rotateCoroutine = null; yield break; }
        float t = 0f;
        float from = NormalizeAngle(currentParentRotationY);
        float angleStep = 360f / slotCount;
        float to = NormalizeAngle(-newIndex * angleStep);
        float delta = ShortestAngleDelta(from, to);

        var oldW = oldIndex >= 0 && oldIndex < attached.Count ? attached[oldIndex] : null;
        var newW = newIndex >= 0 && newIndex < attached.Count ? attached[newIndex] : null;

        Vector3 oldStartPos = Vector3.zero; Quaternion oldStartRot = Quaternion.identity; Vector3 oldTargetPos = Vector3.zero; Quaternion oldTargetRot = Quaternion.identity;
        Vector3 newStartPos = Vector3.zero; Quaternion newStartRot = Quaternion.identity; Vector3 newTargetPos = Vector3.zero; Quaternion newTargetRot = Quaternion.identity;

        if (oldW != null)
        {
            oldStartPos = oldW.transform.localPosition;
            oldStartRot = oldW.transform.localRotation;
            oldTargetPos = (originalSlotLocalPos.Length > oldIndex ? originalSlotLocalPos[oldIndex] : Vector3.zero) + sideLocalPositionOffset;
            oldTargetRot = Quaternion.Euler(sideLocalEulerOffset) * (originalSlotLocalRot.Length > oldIndex ? originalSlotLocalRot[oldIndex] : Quaternion.identity);
        }

        if (newW != null)
        {
            newStartPos = (originalSlotLocalPos.Length > newIndex ? originalSlotLocalPos[newIndex] : Vector3.zero) + sideLocalPositionOffset;
            newStartRot = Quaternion.Euler(sideLocalEulerOffset) * (originalSlotLocalRot.Length > newIndex ? originalSlotLocalRot[newIndex] : Quaternion.identity);
            newTargetPos = (originalSlotLocalPos.Length > newIndex ? originalSlotLocalPos[newIndex] : Vector3.zero) + centerLocalPositionOffset;
            newTargetRot = Quaternion.Euler(centerLocalEulerOffset) * (originalSlotLocalRot.Length > newIndex ? originalSlotLocalRot[newIndex] : Quaternion.identity);
            newW.transform.localPosition = newStartPos;
            newW.transform.localRotation = newStartRot;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(duration <= 0f ? 1f : t / duration);
            if (Mathf.Abs(ease - 1f) > 0.001f) u = Mathf.Pow(u, ease);
            float current = from + delta * u;
            weaponsParent.localEulerAngles = new Vector3(0f, current, 0f);
            currentParentRotationY = current;
            float v = Mathf.SmoothStep(0f, 1f, u);
            if (oldW != null) { oldW.transform.localPosition = Vector3.Lerp(oldStartPos, oldTargetPos, v); oldW.transform.localRotation = Quaternion.Slerp(oldStartRot, oldTargetRot, v); }
            if (newW != null) { newW.transform.localPosition = Vector3.Lerp(newStartPos, newTargetPos, v); newW.transform.localRotation = Quaternion.Slerp(newStartRot, newTargetRot, v); }
            yield return null;
        }

        weaponsParent.localEulerAngles = new Vector3(0f, to, 0f);
        currentParentRotationY = to;
        UpdateCenterIndex(newIndex);
        rotateCoroutine = null;
    }

    private IEnumerator AnimateParentRotationBlocking(float startY, float endY, float duration, float ease)
    {
        float t = 0f;
        float from = NormalizeAngle(startY);
        float to = NormalizeAngle(endY);
        float delta = ShortestAngleDelta(from, to);
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(duration <= 0f ? 1f : t / duration);
            if (Mathf.Abs(ease - 1f) > 0.001f) u = Mathf.Pow(u, ease);
            float current = from + delta * u;
            weaponsParent.localEulerAngles = new Vector3(0f, current, 0f);
            currentParentRotationY = current;
            yield return null;
        }
        weaponsParent.localEulerAngles = new Vector3(0f, to, 0f);
        currentParentRotationY = to;
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
            if (Mathf.Abs(ease - 1f) > 0.001f) u = Mathf.Pow(u, ease);
            float current = from + delta * u;
            weaponsParent.localEulerAngles = new Vector3(0f, current, 0f);
            currentParentRotationY = current;
            yield return null;
        }
        weaponsParent.localEulerAngles = new Vector3(0f, to, 0f);
        currentParentRotationY = to;
        rotateCoroutine = null;
    }

    private float NormalizeAngle(float a) => Mathf.Repeat(a + 180f, 360f) - 180f;
    private float ShortestAngleDelta(float a, float b) => Mathf.Repeat(b - a + 180f, 360f) - 180f;

    public void TossCenterGun(float forwardForce = 5f, float upForce = 1.2f)
    {
        if (AllSlotsEmpty()) return;
        int target = centerIndex;
        if (attached[target] == null) { target = FindNearestOccupiedSlot(centerIndex); if (attached[target] == null) return; }
        var w = attached[target];
        attached[target] = null;
        Vector3 dir = w.firePoint != null ? w.firePoint.forward : (slotTransforms[target] != null ? slotTransforms[target].forward : transform.forward);
        originalSlotLocalRot[target] = Quaternion.identity;
        w.TossOut(dir, forwardForce, upForce);
    }

    private bool AllSlotsEmpty()
    {
        for (int i = 0; i < attached.Count; i++) if (attached[i] != null) return false;
        return true;
    }

    private int FindNearestOccupiedSlot(int start)
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

    public void FireAll(bool ignoreBattery)
    {
        var cam = Camera.main;
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w == null) continue;
            float mult = (i == centerIndex) ? centerMultiplier : 1f;
            w.TryFire(battery, cam, mult);
        }
    }

    private IEnumerator RunFrontAbilityCoroutine()
    {
        abilityActive = true;
        abilityOnCooldown = true;
        int n = slotCount;
        int half = n / 2;
        for (int i = 0; i < n; i++)
        {
            var w = attached[i];
            slideStates[i].weapon = w;
            slideStates[i].originalParent = w != null ? w.transform.parent : null;
            if (w != null) { slideStates[i].originalLocalPos = w.transform.localPosition; slideStates[i].originalLocalRot = w.transform.localRotation; }
            else { slideStates[i].originalLocalPos = Vector3.zero; slideStates[i].originalLocalRot = Quaternion.identity; }
        }

        int[] orderIndices = new int[n];
        for (int j = 0; j < n; j++) orderIndices[j] = (centerIndex - half + j + n) % n;
        int[] targetLineIndexForSlot = new int[n];
        for (int j = 0; j < n; j++) targetLineIndexForSlot[orderIndices[j]] = j;

        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            var tParent = lineSlots[targetLineIndexForSlot[slotIdx]];
            if (tParent == null) { Debug.LogWarning("ArmMount360: lineSlots contains null entry. Skipping."); continue; }
            w.transform.SetParent(tParent, true);
        }

        float startParentY = currentParentRotationY;
        if (rotateParentToCenterOnAbility)
        {
            float angleStep = 360f / slotCount;
            float targetSlotAngle = centerIndex * angleStep;
            if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
            rotateCoroutine = StartCoroutine(AnimateParentRotation(currentParentRotationY, -targetSlotAngle, rotateDuration, rotateEase));
        }

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float easeU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / slideDuration));
            for (int slotIdx = 0; slotIdx < n; slotIdx++)
            {
                var w = attached[slotIdx];
                if (w == null) continue;
                w.transform.localPosition = Vector3.Lerp(w.transform.localPosition, Vector3.zero, easeU);
                w.transform.localRotation = Quaternion.Slerp(w.transform.localRotation, Quaternion.identity, easeU);
            }
            yield return null;
        }

        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            w.transform.localPosition = Vector3.zero;
            w.transform.localRotation = Quaternion.identity;
        }

        float abilityStart = Time.time;
        while (Time.time < abilityStart + abilityDuration)
        {
            float now = Time.time;
            for (int slotIdx = 0; slotIdx < n; slotIdx++)
            {
                var w = attached[slotIdx];
                if (w == null) continue;
                float weaponFireRate = Mathf.Max(0.0001f, w.fireRate);
                float interval = 1f / weaponFireRate;
                float last = 0f;
                object lastObj = fi_lastShotTime.GetValue(w);
                if (lastObj is float f) last = f;
                if (now - last >= interval) { mi_FireInternal.Invoke(w, new object[] { null }); fi_lastShotTime.SetValue(w, now); }
            }
            yield return null;
        }

        Vector3[] backTargetsLocal = new Vector3[n];
        Quaternion[] backTargetsLocalRot = new Quaternion[n];
        for (int i = 0; i < n; i++)
        {
            if (slotTransforms[i] != null)
            {
                Vector3 worldTarget = slotTransforms[i].position;
                backTargetsLocal[i] = weaponsParent.InverseTransformPoint(worldTarget);
                backTargetsLocalRot[i] = slotTransforms[i].localRotation;
            }
            else { backTargetsLocal[i] = Vector3.zero; backTargetsLocalRot[i] = Quaternion.identity; }
        }

        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            w.transform.SetParent(weaponsParent, true);
        }

        float backElapsed = 0f;
        while (backElapsed < slideDuration)
        {
            backElapsed += Time.deltaTime;
            float easeU = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(backElapsed / slideDuration));
            for (int slotIdx = 0; slotIdx < n; slotIdx++)
            {
                var w = attached[slotIdx];
                if (w == null) continue;
                w.transform.localPosition = Vector3.Lerp(w.transform.localPosition, backTargetsLocal[slotIdx], easeU);
                w.transform.localRotation = Quaternion.Slerp(w.transform.localRotation, backTargetsLocalRot[slotIdx], easeU);
            }
            yield return null;
        }

        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            if (slotTransforms[slotIdx] != null)
            {
                w.transform.SetParent(slotTransforms[slotIdx], false);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = Quaternion.identity;
                originalSlotLocalRot[slotIdx] = w.transform.localRotation;
                originalSlotLocalPos[slotIdx] = w.transform.localPosition;
            }
            else w.transform.SetParent(weaponsParent, true);
        }

        if (rotateParentToCenterOnAbility)
        {
            if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
            rotateCoroutine = StartCoroutine(AnimateParentRotation(currentParentRotationY, startParentY, rotateDuration, rotateEase));
        }

        ApplyCenterRotation(centerIndex);
        ApplyCenterPosition(centerIndex);
        abilityActive = false;
        StartCoroutine(AbilityCooldownCoroutine());
    }

    private IEnumerator AbilityCooldownCoroutine()
    {
        float start = Time.time;
        while (Time.time < start + abilityCooldown) yield return null;
        abilityOnCooldown = false;
    }

    public void FireCenterAndAdvance(int direction) { StartCoroutine(FireCenterThenAdvanceCoroutine(direction)); }
    private IEnumerator FireCenterThenAdvanceCoroutine(int direction) { yield return StartCoroutine(FireCenterOrRotateToNearestThenFire()); AdvanceBy(direction); }

    public ModularWeapon[] GetAttachedSnapshot() => attached.ToArray();

    private void UpdateCenterIndex(int newIndex)
    {
        if (newIndex < 0 || newIndex >= slotCount) return;
        int old = centerIndex;
        if (old >= 0 && old < attached.Count)
        {
            var oldW = attached[old];
            if (oldW != null) { oldW.SetCenterState(false); RestoreOriginalLocalRotation(old); RestoreOriginalLocalPosition(old); }
        }

        centerIndex = newIndex;
        int newSide = (centerIndex + 1) % slotCount;
        if (sideIndex >= 0 && sideIndex != newSide && sideIndex < attached.Count) { RestoreOriginalLocalRotation(sideIndex); RestoreOriginalLocalPosition(sideIndex); }
        sideIndex = newSide;

        if (centerIndex >= 0 && centerIndex < attached.Count)
        {
            var newW = attached[centerIndex];
            if (newW != null) { newW.SetCenterState(true); ApplyCenterRotation(centerIndex); ApplyCenterPosition(centerIndex); }
        }

        if (sideIndex >= 0 && sideIndex < attached.Count)
        {
            var sideW = attached[sideIndex];
            if (sideW != null) { ApplySideRotation(sideIndex); ApplySidePosition(sideIndex); }
        }
    }

    private void ApplyCenterRotation(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        Quaternion orig = originalSlotLocalRot != null && slot < originalSlotLocalRot.Length ? originalSlotLocalRot[slot] : Quaternion.identity;
        w.transform.localRotation = Quaternion.Euler(centerLocalEulerOffset) * orig;
    }

    private void ApplyCenterPosition(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        Vector3 orig = originalSlotLocalPos != null && slot < originalSlotLocalPos.Length ? originalSlotLocalPos[slot] : Vector3.zero;
        w.transform.localPosition = orig + centerLocalPositionOffset;
    }

    private void ApplySideRotation(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        Quaternion orig = originalSlotLocalRot != null && slot < originalSlotLocalRot.Length ? originalSlotLocalRot[slot] : Quaternion.identity;
        w.transform.localRotation = Quaternion.Euler(sideLocalEulerOffset) * orig;
    }

    private void ApplySidePosition(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        Vector3 orig = originalSlotLocalPos != null && slot < originalSlotLocalPos.Length ? originalSlotLocalPos[slot] : Vector3.zero;
        w.transform.localPosition = orig + sideLocalPositionOffset;
    }

    private void RestoreOriginalLocalRotation(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        Quaternion orig = originalSlotLocalRot != null && slot < originalSlotLocalRot.Length ? originalSlotLocalRot[slot] : Quaternion.identity;
        w.transform.localRotation = orig;
    }

    private void RestoreOriginalLocalPosition(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        Vector3 orig = originalSlotLocalPos != null && slot < originalSlotLocalPos.Length ? originalSlotLocalPos[slot] : Vector3.zero;
        w.transform.localPosition = orig;
    }
}
