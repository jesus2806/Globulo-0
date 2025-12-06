using UnityEngine;
using UnityEngine.UI;

public class AmmoUI : MonoBehaviour
{
    [Header("Referencia al arma")]
    [SerializeField] PlayerWeapon weapon;

    [Header("Iconos de balas (en orden)")]
    [SerializeField] Image[] bulletIcons;

    int lastAmmo = -1;

    void Awake()
    {
        // Si no los llenas a mano, los toma de los hijos
        if (bulletIcons == null || bulletIcons.Length == 0)
        {
            bulletIcons = GetComponentsInChildren<Image>(true);
        }
    }

    void Update()
    {
        if (!weapon) return;

        int ammo = Mathf.Clamp(weapon.CurrentAmmo, 0, bulletIcons.Length);
        if (ammo == lastAmmo) return;   // no ha cambiado, no hacemos nada

        lastAmmo = ammo;
        RefreshIcons(ammo);
    }

    void RefreshIcons(int ammo)
    {
        int total = bulletIcons.Length;

        for (int i = 0; i < total; i++)
        {
            if (!bulletIcons[i]) continue;

            // Queremos que se apaguen desde el primer icono (izquierda)
            // o desde el que tengas al inicio del arreglo.
            // Mostramos solo los "ammo" últimos iconos del array.
            bool shouldShow = i >= total - ammo;

            bulletIcons[i].gameObject.SetActive(shouldShow);
        }
    }

}
