using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    [SerializeField] private GeneralWeapon generalWeapon;
    [SerializeField] private KeyCode switchWeaponKey = KeyCode.Alpha1;
    
    void Update()
    {
        if (Input.GetKeyDown(switchWeaponKey))
        {
            ToggleShotgun();
        }
    }

    private void ToggleShotgun()
    {
        if(generalWeapon.gameObject.activeSelf)
        {
            generalWeapon.gameObject.SetActive(false);
        }
        else
        {
            generalWeapon.gameObject.SetActive(true);
        }
    }
}
