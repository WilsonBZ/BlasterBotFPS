using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Place on a vendor/interactable object which has a BoxCollider (isTrigger = true).
/// Shows "Press E" when player is inside trigger. Press E to open the GunShop UI.
/// NOTE: this version passes the vendor reference to the shop UI so the shop can trigger waves on purchase.
/// </summary>
[RequireComponent(typeof(Collider))]
public class GunVendor : MonoBehaviour
{
    [Header("UI & Prompt")]
    [Tooltip("Root UI panel (disable by default) that contains the GunShopUI component.")]
    public GameObject shopPanel;

    [Tooltip("Optional small prompt GameObject to show 'Press E' when in range (UI Text or TMP Text).")]
    public GameObject promptObject;

    [Tooltip("Key to open shop")]
    public KeyCode interactKey = KeyCode.E;

    [Header("Player detection")]
    [Tooltip("Optional tag to recognize player (default 'Player')")]
    public string playerTag = "Player";

    [HideInInspector] public bool playerNearby = false;
    private GameObject playerGO = null;

    void Reset()
    {
        // ensure collider is trigger
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void Start()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (promptObject != null) promptObject.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerNearby = true;
        playerGO = other.gameObject;
        if (promptObject != null) promptObject.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        playerNearby = false;
        playerGO = null;
        if (promptObject != null) promptObject.SetActive(false);
        // close shop if player walks away while open
        if (shopPanel != null && shopPanel.activeSelf)
            CloseShop();
    }

    void Update()
    {
        if (!playerNearby) return;
        if (Input.GetKeyDown(interactKey))
        {
            if (shopPanel != null)
                OpenShop();
        }
    }

    /// <summary>
    /// Opens the shop UI and passes this vendor reference to it.
    /// Also unlocks cursor and hides the in-world prompt.
    /// </summary>
    public void OpenShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(true);

        // unlock & show cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // hide prompt while UI open
        if (promptObject != null) promptObject.SetActive(false);

        // tell the shop UI which player is opening it (so it can attach weapons to player's mount)
        var shopUI = shopPanel.GetComponent<GunShopUI>();
        if (shopUI != null)
        {
            shopUI.OpenForPlayer(playerGO, this); // <-- pass vendor reference
        }
    }

    /// <summary>
    /// Closes the shop and restores cursor lock. Called by shop UI or externally.
    /// </summary>
    public void CloseShop()
    {
        if (shopPanel == null) return;
        shopPanel.SetActive(false);

        // relock cursor to locked state (assume game uses Locked)
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        // show prompt again if player still nearby
        if (playerNearby && promptObject != null) promptObject.SetActive(true);
    }
}
