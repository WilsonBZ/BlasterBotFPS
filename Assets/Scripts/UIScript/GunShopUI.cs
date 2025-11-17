using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// UI controller for the gun shop panel. Populate `weaponPrefabs` in inspector.
/// The script will create a button per weapon. Clicking a button attempts to attach it to the player's arm mount.
/// This modified version accepts a GunVendor when opened and will notify the vendor's VendorWaveController on purchase.
/// </summary>
public class GunShopUI : MonoBehaviour
{
    [Header("Prefabs & UI")]
    [Tooltip("Weapon prefabs of type ModularWeapon")]
    public List<ModularWeapon> weaponPrefabs;

    [Tooltip("Button prefab used to represent a weapon in the grid. Button must have a child Text for the label.")]
    public Button weaponButtonPrefab;

    [Tooltip("Parent transform that will receive instantiated buttons (use GridLayoutGroup for automatic layout).")]
    public Transform contentParent;

    [Tooltip("Optional: player-facing message text to show success/failure (assign a Text or TMPro).")]
    public Text messageText;

    [Header("Close behavior")]
    [Tooltip("When a weapon is purchased, close the shop automatically")]
    public bool closeOnPurchase = true;

    // runtime
    private GameObject currentPlayer = null;
    private GunVendor currentVendor = null;

    void Start()
    {
        if (weaponButtonPrefab == null)
        {
            Debug.LogError("GunShopUI: weaponButtonPrefab not assigned.");
            return;
        }
        Populate();
    }

    public void Populate()
    {
        if (contentParent == null || weaponButtonPrefab == null) return;

        // clear existing
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }

        for (int i = 0; i < weaponPrefabs.Count; i++)
        {
            var prefab = weaponPrefabs[i];
            var btnGO = Instantiate(weaponButtonPrefab.gameObject, contentParent);
            var btn = btnGO.GetComponent<Button>();
            var txt = btnGO.GetComponentInChildren<Text>();
            if (txt != null)
                txt.text = prefab != null ? prefab.gameObject.name : "Null";

            int idx = i; // capture
            btn.onClick.AddListener(() => OnWeaponButtonClicked(idx));
        }
    }


    public void OpenForPlayer(GameObject player, GunVendor vendor)
    {
        currentPlayer = player;
        currentVendor = vendor;

        if (player == null)
        {

        }

        if (messageText != null) messageText.text = "";
    }

    void OnWeaponButtonClicked(int index)
    {
        if (index < 0 || index >= weaponPrefabs.Count) return;
        var prefab = weaponPrefabs[index];
        AttemptBuyWeapon(prefab);
    }


    void AttemptBuyWeapon(ModularWeapon prefab)
    {
        if (prefab == null)
        {
            SetMessage("Invalid weapon.");
            return;
        }

        if (currentPlayer == null)
        {
            SetMessage("No player present.");
            return;
        }

        // try ArmMount first
        ArmMount360 mount = currentPlayer.GetComponentInChildren<ArmMount360>();
        if (mount != null)
        {
            int slot = mount.AttachWeapon(prefab);
            if (slot >= 0)
            {
                SetMessage($"Equipped: {prefab.gameObject.name} (slot {slot})");

                // Trigger vendor waves BEFORE we close the shop (so currentVendor still valid)
                TriggerVendorWavesIfPresent();

                // optionally close UI
                if (closeOnPurchase) CloseShop();
            }
            else
            {
                SetMessage("No free slot on arm. Toss a gun to free space.");
            }
            return;
        }

        // fallback: try ArmMount360 or other mount names (call their AttachWeapon if present)
        var m360 = currentPlayer.GetComponentInChildren<ArmMount360>();
        if (m360 != null)
        {
            int slot = m360.AttachWeapon(prefab);
            if (slot >= 0)
            {
                SetMessage($"Equipped: {prefab.gameObject.name} (slot {slot})");

                // Trigger vendor waves BEFORE we close the shop
                TriggerVendorWavesIfPresent();

                if (closeOnPurchase) CloseShop();
            }
            else
            {
                SetMessage("No free slot on arm. Toss a gun to free space.");
            }
            return;
        }

        SetMessage("No arm mount on player.");
    }

    private void TriggerVendorWavesIfPresent()
    {
        Debug.Log("[GunShopUI] TriggerVendorWavesIfPresent called. currentVendor = " + (currentVendor ? currentVendor.name : "NULL"));

        if (currentVendor == null)
        {
            Debug.LogWarning("[GunShopUI] currentVendor is null. Cannot trigger waves.");
            return;
        }

        // 1) Try same GameObject
        var vwc = currentVendor.GetComponent<VendorWaveController>();
        if (vwc != null)
        {
            Debug.Log("[GunShopUI] Found VendorWaveController on currentVendor GameObject.");
            vwc.OnPurchase_StartWaves();
            return;
        }

        // 2) Try children of vendor
        vwc = currentVendor.GetComponentInChildren<VendorWaveController>(includeInactive: true);
        if (vwc != null)
        {
            Debug.Log("[GunShopUI] Found VendorWaveController in children of currentVendor: " + vwc.gameObject.name);
            vwc.OnPurchase_StartWaves();
            return;
        }

        // 3) Try parent chain (in case vendor object is child of a parent that has controller)
        vwc = currentVendor.GetComponentInParent<VendorWaveController>(includeInactive: true);
        if (vwc != null)
        {
            Debug.Log("[GunShopUI] Found VendorWaveController in parent of currentVendor: " + vwc.gameObject.name);
            vwc.OnPurchase_StartWaves();
            return;
        }

        // 4) Last resort: find in scene. Only use this if there's a single VendorWaveController in scene.
        var all = FindObjectsOfType<VendorWaveController>(includeInactive: true);
        if (all != null && all.Length == 1)
        {
            vwc = all[0];
            Debug.Log("[GunShopUI] Found single VendorWaveController in scene: " + vwc.gameObject.name + " — using it as fallback.");
            vwc.OnPurchase_StartWaves();
            return;
        }

        Debug.LogWarning("[GunShopUI] Could not find a VendorWaveController for this vendor. " +
                         "Make sure VendorWaveController is attached to the vendor GameObject or its parent/child. " +
                         "Found in scene: " + (all != null ? all.Length.ToString() : "0"));
    }

    void SetMessage(string text)
    {
        if (messageText != null) messageText.text = text;
        else Debug.Log(text);
    }

    /// <summary>
    /// Closes the shop and restores cursor lock.
    /// </summary>
    public void CloseShop()
    {
        // hide this UI
        gameObject.SetActive(false);

        // re-lock cursor to gameplay defaults
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // clear player/vendor ref
        currentPlayer = null;
        currentVendor = null;
    }
}
