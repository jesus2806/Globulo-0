using UnityEngine;

[DisallowMultipleComponent]
public class WeaponAim2D : MonoBehaviour
{
    [Header("Referencias")]
    public Transform player;              // si este objeto es hijo del player puedes dejarlo vacío
    public SpriteRenderer playerSR;       // opcional
    public SpriteRenderer weaponSR;       // SpriteRenderer del arma

    [Header("Bullet Spawner")]
    public Transform bulletSpawner;       // ← arrastra aquí tu objeto "BulletSpawner"
    public bool mirrorSpawnerYOnLeft = true; // ← activar inversión de Y al apuntar a la izquierda
    float spawnerYAbs = 0f;               // guardamos el |Y| inicial

    [Header("Modo de anclaje")]
    public bool childOfPlayer = true;
    public Vector2 localGripOffset = new Vector2(0.25f, 0f);

    [Header("Comportamiento de apuntado")]
    [Tooltip("Activa espejo vertical (flipY del sprite) al apuntar a la izquierda.")]
    public bool flipYOnLeft = true;

    [Header("Orden de dibujo opcional")]
    public bool sortInFrontWhenDown = true;
    public int sortingOrderFront = 10;
    public int sortingOrderBack = 0;

    Camera cam;

    void Awake()
    {
        cam = Camera.main;

        if (!player && transform.parent != null) player = transform.parent;
        if (!weaponSR) weaponSR = GetComponentInChildren<SpriteRenderer>();
        if (!playerSR && player) playerSR = player.GetComponentInChildren<SpriteRenderer>();

        // Cachea el |Y| inicial del spawner
        if (bulletSpawner)
            spawnerYAbs = Mathf.Abs(bulletSpawner.localPosition.y);
    }

    void LateUpdate()
    {
        if (!player) return;

        // 1) Posicionar si no somos hijo del player
        if (!childOfPlayer)
        {
            Vector3 basePos = player.position;
            transform.position = basePos + (transform.right * localGripOffset.x) + (transform.up * localGripOffset.y);
        }

        // 2) Dirección hacia el mouse (mundo)
        Vector3 mw = GetMouseWorld();
        Vector2 toMouse = (Vector2)(mw - transform.position);
        if (toMouse.sqrMagnitude < 0.0001f) return;

        // 3) Rotar arma/pivote
        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // 4) ¿Apunta a la izquierda? (los 180° izquierdos)
        bool aimingLeft = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) > 90f;

        // 4.1) Espejo del sprite (no afecta hijos, por eso ajustamos spawner abajo)
        if (weaponSR && flipYOnLeft)
        {
            weaponSR.flipY = aimingLeft;
            var s = weaponSR.transform.localScale;
            if (s.y < 0f) { s.y = Mathf.Abs(s.y); weaponSR.transform.localScale = s; }
        }

        // 4.2) INVERTIR Y DEL SPAWNER cuando apunta a la izquierda
        UpdateSpawnerY(aimingLeft);

        // 5) Orden de dibujo
        if (weaponSR)
        {
            if (aimingLeft)
            {
                weaponSR.sortingOrder = sortingOrderFront; // izquierda nunca detrás
            }
            else if (sortInFrontWhenDown)
            {
                bool aimingDown = Mathf.Abs(Mathf.DeltaAngle(angle, 0f)) <= 90f;
                weaponSR.sortingOrder = aimingDown ? sortingOrderFront : sortingOrderBack;
            }
            else
            {
                weaponSR.sortingOrder = sortingOrderFront;
            }
        }
    }

    void UpdateSpawnerY(bool aimingLeft)
    {
        if (!bulletSpawner || !mirrorSpawnerYOnLeft) return;

        // Si no hemos cacheado aún (por si se asigna en runtime)
        if (spawnerYAbs == 0f) spawnerYAbs = Mathf.Abs(bulletSpawner.localPosition.y);

        Vector3 lp = bulletSpawner.localPosition;
        // Forzamos su Y a ±|Y| según la mitad del círculo
        lp.y = aimingLeft ? -spawnerYAbs : spawnerYAbs;
        bulletSpawner.localPosition = lp;
    }

    Vector3 GetMouseWorld()
    {
#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        Vector2 screen = mouse != null ? mouse.position.ReadValue() : (Vector2)Input.mousePosition;
        float z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
#else
        float z = -cam.transform.position.z;
        return cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, z));
#endif
    }
}
