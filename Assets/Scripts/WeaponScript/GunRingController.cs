//using UnityEngine;
//using System.Collections.Generic;

//public class GunRingController : MonoBehaviour
//{
//    [Header("Gun Ring Settings")]
//    public List<ModularWeapon> gunRing = new List<ModularWeapon>(7);
//    public int centerIndex = 3; // Always 3 for middle gun

//    [Header("Rotation Settings")]
//    public KeyCode rotateLeftKey = KeyCode.Q;
//    public KeyCode rotateRightKey = KeyCode.E;
//    public float rotateSpeed = 360f; // For smooth visual rotation if needed

//    [Header("Toss Settings")]
//    public KeyCode tossKey = KeyCode.G;
//    public Transform tossOrigin;
//    public float tossForce = 10f;

//    private ArmBattery battery;

//    private void Start()
//    {
//        battery = FindAnyObjectByType<ArmBattery>();

//        if (gunRing.Count != 7)
//            Debug.LogWarning("GunRingController: gunRing should have 7 weapons assigned.");
//    }

//    private void Update()
//    {
//        HandleRotationInput();
//        HandleTossInput();
//    }

//    private void HandleRotationInput()
//    {
//        if (Input.GetKeyDown(rotateRightKey))
//            RotateRing(1);

//        if (Input.GetKeyDown(rotateLeftKey))
//            RotateRing(-1);
//    }

//    private void HandleTossInput()
//    {
//        if (Input.GetKeyDown(tossKey))
//        {
//            var currentGun = gunRing[centerIndex];
//            if (currentGun == null)
//                return;

//            gunRing[centerIndex] = null;
//            TossGun(currentGun);
//        }
//    }

//    private void RotateRing(int direction)
//    {
//        if (gunRing.Count == 0)
//            return;

//        ModularWeapon[] newRing = new ModularWeapon[gunRing.Count];

//        for (int i = 0; i < gunRing.Count; i++)
//        {
//            int newIndex = (i + direction) % gunRing.Count;
//            if (newIndex < 0) newIndex += gunRing.Count;
//            newRing[newIndex] = gunRing[i];
//        }

//        gunRing = new List<ModularWeapon>(newRing);
//        UpdateGunCenter();
//    }

//    private void UpdateGunCenter()
//    {
//        for (int i = 0; i < gunRing.Count; i++)
//        {
//            if (gunRing[i] != null)
//                gunRing[i].SetAsMainGun(i == centerIndex);
//        }
//    }

//    private void TossGun(ModularWeapon gun)
//    {
//        gun.transform.parent = null;

//        Rigidbody rb = gun.GetComponent<Rigidbody>();
//        if (rb == null)
//            rb = gun.gameObject.AddComponent<Rigidbody>();

//        rb.isKinematic = false;
//        rb.AddForce(tossOrigin.forward * tossForce, ForceMode.Impulse);

//        gun.OnTossed();
//    }

//    public void FireAllGuns(Vector3 crosshairPoint)
//    {
//        foreach (var gun in gunRing)
//        {
//            if (gun == null) continue;

//            if (gun.isMainGun)
//                gun.TryFireTowards(crosshairPoint, battery);
//            else
//                gun.TryFireForward(battery);
//        }
//    }
//}
