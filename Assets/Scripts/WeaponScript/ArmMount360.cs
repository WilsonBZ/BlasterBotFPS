using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    [Tooltip("How many seconds must pass between manual rotations (E/Q).")]
    public float turnRate = 0.12f;
    [Tooltip("Energy multiplier applied when firing (if you want center to cost more).")]
    public float centerMultiplier = 1f;

    [Header("Rotation tween")]
    [Tooltip("Seconds for ring to rotate visually when advancing to next gun.")]
    public float rotateDuration = 0.12f;
    [Tooltip("Easing exponent for Lerp (1 = linear).")]
    public float rotateEase = 1.0f;
    [Tooltip("Yaw offset applied to auto-generated slot rotations.")]
    public float modelYawOffset = -90f;

    [Header("Input (keys)")]
    public KeyCode tossKey = KeyCode.G;
    public KeyCode rotateLeftKey = KeyCode.E;
    public KeyCode rotateRightKey = KeyCode.Q;
    public KeyCode abilityKey = KeyCode.T;
    public bool handleInput = true;

    [Header("Frontal Ability (LineSlots, Option B mapping)")]
    [Tooltip("Explicit transforms in front of the player to use for ability. Size must equal slotCount. Slot index slotCount/2 is the center.")]
    public Transform[] lineSlots;
    [Tooltip("Seconds that ability auto-fires and ignores battery.")]
    public float abilityDuration = 6f;
    [Tooltip("Cooldown after ability ends (seconds).")]
    public float abilityCooldown = 10f;
    [Tooltip("Slide animation duration to/from line slots (seconds).")]
    public float slideDuration = 0.18f;
    [Tooltip("Should ability rotate the weaponsParent visually to center the center slot?")]
    public bool rotateParentToCenterOnAbility = true;

    // --- internal state ---
    private Transform[] slotTransforms;
    private List<ModularWeapon> attached;
    private int centerIndex;
    private float lastShotTime = -999f;
    private float lastRotateTime = -999f;
    private float currentParentRotationY = 0f;
    private Coroutine rotateCoroutine = null;

    private bool abilityActive = false;
    private bool abilityOnCooldown = false;

    private struct WeaponSlideState
    {
        public ModularWeapon weapon;
        public Transform originalParent;
        public Vector3 originalLocalPos;
        public Quaternion originalLocalRot;
    }
    private WeaponSlideState[] slideStates;

    private MethodInfo mi_FireInternal = null;
    private FieldInfo fi_lastShotTime = null;

    public List<GameObject> WeaponSockets ;

    private void Awake()
    {
        if (weaponsParent == null) weaponsParent = this.transform;

        if (slotCount <= 1) slotCount = 7;

        slotTransforms = new Transform[slotCount];
        attached = new List<ModularWeapon>(new ModularWeapon[slotCount]);
        slideStates = new WeaponSlideState[slotCount];

        if (autoGenerateSlots)
            CreateSlotTransforms();

        centerIndex = 0;
        currentParentRotationY = weaponsParent.localEulerAngles.y;

        if (battery == null)
            battery = FindFirstObjectByType<ArmBattery>();

        var mwType = typeof(ModularWeapon);
        mi_FireInternal = mwType.GetMethod("FireInternal", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        fi_lastShotTime = mwType.GetField("lastShotTime", BindingFlags.Instance | BindingFlags.NonPublic);
        if (mi_FireInternal == null || fi_lastShotTime == null)
        {
            Debug.LogWarning("ArmMount360: Reflection failed to locate ModularWeapon internals (FireInternal/lastShotTime). Ability will be disabled.");
        }

        if (lineSlots != null && lineSlots.Length > 0 && lineSlots.Length != slotCount)
        {
            Debug.LogWarning($"ArmMount360: lineSlots length ({lineSlots.Length}) != slotCount ({slotCount}). Set to exactly {slotCount} or leave empty.");
        }
    }

    private void Update()
    {
        if (!handleInput) return;

        // While abilityActive block normal inputs (you asked that ability blocks rotation/firing)
        if (!abilityActive)
        {
            // Fire1: fire center only (if empty => rotate-to-nearest-occupied then fire)
            if (Input.GetButton("Fire1"))
                StartCoroutine(FireCenterOrRotateToNearestThenFire());

            // rotate keys
            if (Input.GetKey(rotateLeftKey))
                TryRotate(-1);
            else if (Input.GetKey(rotateRightKey))
                TryRotate(+1);

            // toss center
            if (Input.GetKeyDown(tossKey))
                TossCenterGun();
        }

        // Ability activation (T)
        if (Input.GetKeyDown(abilityKey))
        {
            if (!abilityActive && !abilityOnCooldown)
            {
                if (lineSlots == null || lineSlots.Length != slotCount)
                {
                    Debug.LogError("ArmMount360: lineSlots not set or wrong length. Ability aborted.");
                }
                else if (mi_FireInternal == null || fi_lastShotTime == null)
                {
                    Debug.LogError("ArmMount360: Ability aborted because ModularWeapon internals are not accessible via reflection.");
                }
                else if (AllSlotsEmpty())
                {
                    Debug.Log("ArmMount360: Ability aborted — no weapons attached.");
                }
                else
                {
                    StartCoroutine(RunFrontAbilityCoroutine());
                }
            }
        }
    }

    private void CreateSlotTransforms()
    {
        float angleStep = 360f / slotCount;
        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotGO = new GameObject($"RingSlot_{i}");
            slotGO.transform.SetParent(weaponsParent, false);
            WeaponSockets.Add(slotGO);

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

        if (slotTransforms[slot] != null)
        {
            inst.transform.SetParent(slotTransforms[slot], false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
        }
        else
        {
            inst.transform.SetParent(weaponsParent, false);
        }

        attached[slot] = inst;
        inst.SetParentMount(this, slot);
        return slot;
    }

    private int FindFirstEmptySlot()
    {
        for (int i = 0; i < attached.Count; i++)
            if (attached[i] == null) return i;
        return -1;
    }


    private IEnumerator FireCenterOrRotateToNearestThenFire()
    {
        float minInterval = 1f / Mathf.Max(0.0001f, fireRate);
        if (Time.time - lastShotTime < minInterval) yield break;

        ModularWeapon centerWeapon = attached[centerIndex];
        if (centerWeapon != null)
        {
            Camera cam = Camera.main;
            centerWeapon.TryFire(battery, cam, centerMultiplier);
            lastShotTime = Time.time;
            yield break;
        }

        int nearest = FindNearestOccupiedSlot(centerIndex);
        if (nearest == centerIndex || attached[nearest] == null)
        {
            yield break;
        }

        float angleStep = 360f / slotCount;
        float targetParentY = -nearest * angleStep;
        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        //use a blocking coroutine to rotate then fire
        yield return StartCoroutine(AnimateParentRotationBlocking(currentParentRotationY, targetParentY, rotateDuration, rotateEase));
        centerIndex = nearest;

        // now fire it
        var w = attached[centerIndex];
        if (w != null)
        {
            Camera cam = Camera.main;
            w.TryFire(battery, cam, centerMultiplier);
            lastShotTime = Time.time;
        }
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

        float angleStep = 360f / slotCount;
        float targetParentY = -newIndex * angleStep;

        if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
        rotateCoroutine = StartCoroutine(AnimateParentRotation(currentParentRotationY, targetParentY, rotateDuration, rotateEase));

        centerIndex = newIndex;
    }

    private IEnumerator AnimateParentRotationBlocking(float startY, float endY, float duration, float ease)
    {
        // use the same logic as AnimateParentRotation but blocking (no rotateCoroutine set)
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

    // Non-blocking rotation coroutine (used by AdvanceBy and ability)
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

    // Toss the weapon at center (or nearest occupied) — unchanged
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

    // FireAll used by ability (calls TryFireAllowNoBattery in earlier versions — here we use reflection)
    public void FireAll(bool ignoreBattery)
    {
        // Not used in this script (ability uses reflection per-weapon internally),
        // but kept for compatibility if you want to call it externally.
        Camera cam = Camera.main;
        for (int i = 0; i < attached.Count; i++)
        {
            var w = attached[i];
            if (w == null) continue;
            float mult = (i == centerIndex) ? centerMultiplier : 1f;
            w.TryFire(battery, cam, mult);
        }
    }

    // -------------------- Ability (Option B mapping) --------------------

    private IEnumerator RunFrontAbilityCoroutine()
    {
        abilityActive = true;
        abilityOnCooldown = true;

        int n = slotCount;
        int half = n / 2;

        // store original states
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

        // Build ordered indices left->center->right (Option B)
        int[] orderIndices = new int[n];
        for (int j = 0; j < n; j++)
            orderIndices[j] = (centerIndex - half + j + n) % n;

        // Inverse: slotIdx -> line index
        int[] targetLineIndexForSlot = new int[n];
        for (int j = 0; j < n; j++)
        {
            int slotIdx = orderIndices[j];
            targetLineIndexForSlot[slotIdx] = j;
        }

        // Reparent each weapon to its target lineSlot (preserve world)
        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            int targetLineIdx = targetLineIndexForSlot[slotIdx];
            Transform tParent = lineSlots[targetLineIdx];
            if (tParent == null)
            {
                Debug.LogWarning("ArmMount360: lineSlots contains null entry. Skipping.");
                continue;
            }
            w.transform.SetParent(tParent, true);
        }

        // Optionally rotate weaponsParent so center visually faces front
        float startParentY = currentParentRotationY;
        if (rotateParentToCenterOnAbility)
        {
            float angleStep = 360f / slotCount;
            float targetSlotAngle = centerIndex * angleStep;
            if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
            rotateCoroutine = StartCoroutine(AnimateParentRotation(currentParentRotationY, -targetSlotAngle, rotateDuration, rotateEase));
        }

        // Slide into lineSlots (local -> zero)
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
                Vector3 from = w.transform.localPosition;
                Quaternion fromR = w.transform.localRotation;
                w.transform.localPosition = Vector3.Lerp(from, Vector3.zero, easeU);
                w.transform.localRotation = Quaternion.Slerp(fromR, Quaternion.identity, easeU);
            }
            yield return null;
        }

        // snap to zero
        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            w.transform.localPosition = Vector3.zero;
            w.transform.localRotation = Quaternion.identity;
        }

        // AUTO-FIRE using reflection: call FireInternal and update lastShotTime
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
                    // center weapon: we could pass Camera.main for camera-aiming; for simplicity we pass null so all fire straight.
                    mi_FireInternal.Invoke(w, new object[] { null });
                    fi_lastShotTime.SetValue(w, now);
                }
            }
            yield return null;
        }

        // Slide back: compute targets in weaponsParent local space
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
            else
            {
                backTargetsLocal[i] = Vector3.zero;
                backTargetsLocalRot[i] = Quaternion.identity;
            }
        }

        // Reparent all weapons to weaponsParent (preserve world pos) to lerp back
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
            float u = Mathf.Clamp01(backElapsed / slideDuration);
            float easeU = Mathf.SmoothStep(0f, 1f, u);

            for (int slotIdx = 0; slotIdx < n; slotIdx++)
            {
                var w = attached[slotIdx];
                if (w == null) continue;
                Vector3 from = w.transform.localPosition;
                Quaternion fromR = w.transform.localRotation;
                Vector3 to = backTargetsLocal[slotIdx];
                Quaternion toR = backTargetsLocalRot[slotIdx];
                w.transform.localPosition = Vector3.Lerp(from, to, easeU);
                w.transform.localRotation = Quaternion.Slerp(fromR, toR, easeU);
            }
            yield return null;
        }

        // snap back & reparent to slotTransforms
        for (int slotIdx = 0; slotIdx < n; slotIdx++)
        {
            var w = attached[slotIdx];
            if (w == null) continue;
            if (slotTransforms[slotIdx] != null)
            {
                w.transform.SetParent(slotTransforms[slotIdx], false);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = Quaternion.identity;
            }
            else
            {
                w.transform.SetParent(weaponsParent, true);
            }
        }

        // restore parent rotation if changed
        if (rotateParentToCenterOnAbility)
        {
            if (rotateCoroutine != null) StopCoroutine(rotateCoroutine);
            rotateCoroutine = StartCoroutine(AnimateParentRotation(currentParentRotationY, startParentY, rotateDuration, rotateEase));
        }

        abilityActive = false;
        StartCoroutine(AbilityCooldownCoroutine());
    }

    private IEnumerator AbilityCooldownCoroutine()
    {
        float start = Time.time;
        while (Time.time < start + abilityCooldown)
            yield return null;
        abilityOnCooldown = false;
    }

    // -------------------- Helpers / Exposed --------------------

    public void FireCenterAndAdvance(int direction)
    {
        // compatibility helper: fire then rotate
        StartCoroutine(FireCenterThenAdvanceCoroutine(direction));
    }

    private IEnumerator FireCenterThenAdvanceCoroutine(int direction)
    {
        yield return StartCoroutine(FireCenterOrRotateToNearestThenFire());
        AdvanceBy(direction);
    }

    public ModularWeapon[] GetAttachedSnapshot() => attached.ToArray();
}
