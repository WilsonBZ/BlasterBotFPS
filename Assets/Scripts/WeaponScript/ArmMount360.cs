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

    [Header("Startup")]
    public bool autoApplyInitialCenter = false;

    private Transform[] slotTransforms;
    private List<ModularWeapon> attached;
    private int centerIndex;
    private float lastShotTime = -999f;
    private float lastRotateTime = -999f;
    private float currentParentRotationY = 0f;
    private Coroutine rotateCoroutine;

    private bool abilityActive;
    private bool abilityOnCooldown;
    private bool isSwitching; // prevents firing while switching

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

        if (autoApplyInitialCenter) UpdateCenterIndex(0);

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

        if (!isSwitching && Input.GetButton("Fire1"))
            StartCoroutine(FireCenterOrRotateToNearestThenFire());

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

        bool hadAny = false;
        for (int i = 0; i < attached.Count; i++) if (attached[i] != null) { hadAny = true; break; }

        var instGO = Instantiate(prefab.gameObject, weaponsParent);
        var inst = instGO.GetComponent<ModularWeapon>();
        if (slotTransforms[slot] != null)
        {
            inst.transform.SetParent(slotTransforms[slot], false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
        }
        else inst.transform.SetParent(weaponsParent, false);

        originalSlotLocalRot[slot] = inst.transform.localRotation;
        originalSlotLocalPos[slot] = inst.transform.localPosition;
        attached[slot] = inst;
        inst.SetParentMount(this, slot);

        if (!hadAny)
        {
            centerIndex = slot;
            float angleStep = 360f / slotCount;
            if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
            weaponsParent.localEulerAngles = new Vector3(0f, -centerIndex * angleStep, 0f);
            currentParentRotationY = weaponsParent.localEulerAngles.y;
            UpdateCenterIndex(centerIndex);
        }
        else
        {
            inst.SetCenterState(false);
            ApplySidePosition(slot);
            ApplySideRotation(slot);
        }

        return slot;
    }

    private IEnumerator FireCenterOrRotateToNearestThenFire()
    {
        if (isSwitching || abilityActive) yield break;

        float minInterval = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastShotTime < minInterval) yield break;
        var centerWeapon = attached[centerIndex];
        if (centerWeapon != null) { centerWeapon.TryFire(battery, Camera.main, centerMultiplier); lastShotTime = Time.time; yield break; }

        int nearest = FindNearestOccupiedSlot(centerIndex);
        if (nearest == centerIndex || attached[nearest] == null) yield break;

        float angleStep = 360f / slotCount;
        float targetParentY = -nearest * angleStep;
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);

        isSwitching = true;
        yield return StartCoroutine(AnimateParentRotationBlocking(currentParentRotationY, targetParentY, rotateDuration, rotateEase));
        isSwitching = false;

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

        isSwitching = true;

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
            oldTargetRot = Quaternion.Euler(sideLocalEulerOffset);
        }

        if (newW != null)
        {
            newStartPos = (originalSlotLocalPos.Length > newIndex ? originalSlotLocalPos[newIndex] : Vector3.zero) + sideLocalPositionOffset;
            newStartRot = Quaternion.Euler(sideLocalEulerOffset);
            newTargetPos = (originalSlotLocalPos.Length > newIndex ? originalSlotLocalPos[newIndex] : Vector3.zero) + centerLocalPositionOffset;
            newTargetRot = Quaternion.Euler(centerLocalEulerOffset);
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

        isSwitching = false;
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
        isSwitching = true;
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
        isSwitching = false;
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
            if (!isSwitching) w.TryFire(battery, cam, mult);
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
            slideStates[i].originalParent = (w != null) ? w.transform.parent : null;
            if (w != null)
            {
                slideStates[i].originalLocalPos = w.transform.localPosition;
                slideStates[i].originalLocalRot = w.transform.localRotation;
            }
            else
            {
                slideStates[i].originalLocalPos = Vector3.zero;
                slideStates[i].originalLocalRot = Quaternion.identity;
            }
        }

        int[] orderIndices = new int[n];
        for (int j = 0; j < n; j++)
            orderIndices[j] = (centerIndex - half + j + n) % n;

        int[] targetLineIndexForSlot = new int[n];
        for (int j = 0; j < n; j++)
        {
            int slotIdx = orderIndices[j];
            targetLineIndexForSlot[slotIdx] = j;
        }

        Camera cam = Camera.main;
        Vector3 aimPoint;
        if (cam != null)
        {
            Ray aimRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit[] hits = Physics.RaycastAll(aimRay, 1000f);
            aimPoint = aimRay.GetPoint(1000f);
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                var root = h.collider.transform.root;
                bool isWeapon = false;
                for (int k = 0; k < n; k++)
                {
                    if (attached[k] == null) continue;
                    if (attached[k].transform == root || attached[k].transform.root == root) { isWeapon = true; break; }
                }
                if (isWeapon) continue;
                if (root == transform.root) continue;
                aimPoint = h.point;
                break;
            }
        }
        else
        {
            aimPoint = transform.position + transform.forward * 50f;
        }

        // reparent to weaponsParent (preserve world) to simplify world-space lerps
        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            w.transform.SetParent(weaponsParent, true);
        }

        // forward direction (we want guns facing camera forward)
        Vector3 uniformForward = (cam != null) ? cam.transform.forward : weaponsParent.forward;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float u = Mathf.Clamp01(elapsed / slideDuration);
            float easeU = Mathf.SmoothStep(0f, 1f, u);

            for (int slotIdx = 0; slotIdx < n; slotIdx++)
            {
                var w = attached[slotIdx];
                if (w == null) continue;

                Vector3 startWorld = w.transform.position;
                Quaternion startRot = w.transform.rotation;

                Vector3 targetWorld;
                if (lineSlots != null && lineSlots.Length == n)
                    targetWorld = lineSlots[targetLineIndexForSlot[slotIdx]].position;
                else
                {
                    float spacing = 0.6f;
                    int mid = n / 2;
                    if (cam != null)
                    {
                        Vector3 right = cam.transform.right;
                        Vector3 basePos = cam.transform.position + cam.transform.forward * 5f + Vector3.up * centerLocalPositionOffset.y;
                        targetWorld = basePos + right * ((slotIdx - mid) * spacing);
                    }
                    else
                    {
                        Vector3 right = weaponsParent.right;
                        Vector3 basePos = weaponsParent.position + weaponsParent.forward * 5f + Vector3.up * centerLocalPositionOffset.y;
                        targetWorld = basePos + right * ((slotIdx - mid) * spacing);
                    }
                }

                Quaternion targetRot = Quaternion.LookRotation(uniformForward, Vector3.up) * Quaternion.Euler(centerLocalEulerOffset);

                w.transform.position = Vector3.Lerp(startWorld, targetWorld, easeU);
                w.transform.rotation = Quaternion.Slerp(startRot, targetRot, easeU);
            }
            yield return null;
        }

        // snap to final lined positions with uniform forward-facing rotation
        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;

            Vector3 finalWorld;
            if (lineSlots != null && lineSlots.Length == n)
                finalWorld = lineSlots[targetLineIndexForSlot[slotIdx]].position;
            else
            {
                float spacing = 0.6f;
                int mid = n / 2;
                if (cam != null)
                {
                    Vector3 right = cam.transform.right;
                    Vector3 basePos = cam.transform.position + cam.transform.forward * 5f + Vector3.up * centerLocalPositionOffset.y;
                    finalWorld = basePos + right * ((slotIdx - mid) * spacing);
                }
                else
                {
                    Vector3 right = weaponsParent.right;
                    Vector3 basePos = weaponsParent.position + weaponsParent.forward * 5f + Vector3.up * centerLocalPositionOffset.y;
                    finalWorld = basePos + right * ((slotIdx - mid) * spacing);
                }
            }

            Quaternion finalRot = Quaternion.LookRotation(uniformForward, Vector3.up) * Quaternion.Euler(centerLocalEulerOffset);

            w.transform.position = finalWorld;
            w.transform.rotation = finalRot;
        }

        // auto-fire toward aimPoint but keep weapon models facing uniformForward
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

                if (now - last >= interval)
                {
                    // spawn projectiles toward aim point but do not rotate the visual beyond the uniform facing
                    w.FireAtPoint(aimPoint);
                    fi_lastShotTime.SetValue(w, now);
                }
            }
            yield return null;
        }

        // restore original parents and local transforms exactly
        for (int i = 0; i < n; i++)
        {
            var state = slideStates[i];
            var w = state.weapon;
            if (w == null) continue;

            if (state.originalParent != null)
                w.transform.SetParent(state.originalParent, false);
            else if (slotTransforms != null && slotTransforms.Length > i && slotTransforms[i] != null)
                w.transform.SetParent(slotTransforms[i], false);
            else
                w.transform.SetParent(weaponsParent, true);

            w.transform.localPosition = state.originalLocalPos;
            w.transform.localRotation = state.originalLocalRot;
        }

        // do NOT rotate weaponsParent during ability
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

        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w == null) continue;
            if (i == centerIndex)
            {
                w.SetCenterState(true);
                ApplyCenterRotation(i);
                ApplyCenterPosition(i);
            }
            else
            {
                w.SetCenterState(false);
                ApplySideRotation(i);
                ApplySidePosition(i);
            }
        }
    }

    private void ApplyCenterRotation(int slot)
    {
        if (slot < 0 || slot >= attached.Count) return;
        var w = attached[slot];
        if (w == null) return;
        w.transform.localRotation = Quaternion.Euler(centerLocalEulerOffset);
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
        w.transform.localRotation = Quaternion.Euler(sideLocalEulerOffset);
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
