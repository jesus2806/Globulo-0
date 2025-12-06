using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerWeapon : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;

    public new Camera camera;
    public Transform spawner;
    public GameObject bulletPrefab;

    [Header("Balas / Recarga")]
    [SerializeField] int magazineSize = 12;      // balas por cargador
    [SerializeField] float reloadDuration = 1f;  // tiempo de recarga en segundos
    int currentAmmo;
    bool isReloading = false;

    [Header("Audio de disparo")]
    public AudioClip shootClip;
    private AudioSource audioSource;

    [Header("Audio de recarga")]
    public AudioClip reloadClip;
    [Range(0f, 2f)] public float reloadVolume = 1f;

    [Header("Randomización de audio")]
    [Range(0f, 2f)] public float baseVolume = 1f;
    public float minVolumeMult = 0.9f;
    public float maxVolumeMult = 1.1f;

    [Range(0.1f, 3f)] public float basePitch = 1f;
    public float minPitchMult = 0.95f;
    public float maxPitchMult = 1.05f;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (camera == null)
            camera = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        currentAmmo = magazineSize;
    }

    void Update()
    {
        RotateTowardsMouse();
        HandleFireAndReload();
    }

    private void RotateTowardsMouse()
    {
        if (camera == null) return;

        float angle = GetAngleTowardsMouse();

        transform.rotation = Quaternion.Euler(0, 0, angle);
        if (spriteRenderer != null)
            spriteRenderer.flipY = angle >= 90 && angle <= 270;
    }

    private float GetAngleTowardsMouse()
    {
        Vector3 mouseWorldPosition = camera.ScreenToWorldPoint(Input.mousePosition);

        Vector3 mouseDirection = mouseWorldPosition - transform.position;
        mouseDirection.z = 0;

        float angle = (Vector3.SignedAngle(Vector3.right, mouseDirection, Vector3.forward) + 360) % 360;
        return angle;
    }

    /// <summary>
    /// Disparo externo (tap en móvil).
    /// Emula el comportamiento del clic izquierdo:
    /// si hay balas, dispara; si no, recarga.
    /// </summary>
    public void ExternalShoot()
    {
        if (isReloading) return;

        if (currentAmmo > 0)
            Shoot();
        else
            StartCoroutine(ReloadRoutine());
    }

    /// <summary>
    /// Maneja disparo + recarga manual / automática (PC).
    /// </summary>
    private void HandleFireAndReload()
    {
        if (isReloading) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            if (currentAmmo < magazineSize)
                StartCoroutine(ReloadRoutine());
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (currentAmmo > 0)
                Shoot();
            else
                StartCoroutine(ReloadRoutine());
        }
    }

    private void Shoot()
    {
        if (!bulletPrefab || !spawner) return;

        GameObject bullet = Instantiate(bulletPrefab, spawner.position, transform.rotation);
        Destroy(bullet, 2f);

        PlayShootSound();

        currentAmmo--;

        if (currentAmmo <= 0)
            StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        if (isReloading) yield break;

        isReloading = true;

        PlayReloadSound();

        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = magazineSize;
        isReloading = false;
    }

    private void PlayShootSound()
    {
        if (shootClip == null || audioSource == null) return;

        float volMult = Random.Range(minVolumeMult, maxVolumeMult);
        float volume = Mathf.Clamp01(baseVolume * volMult);

        float pitchMult = Random.Range(minPitchMult, maxPitchMult);
        audioSource.pitch = basePitch * pitchMult;

        audioSource.PlayOneShot(shootClip, volume);
    }

    private void PlayReloadSound()
    {
        if (reloadClip == null || audioSource == null) return;

        float volMult = Random.Range(minVolumeMult, maxVolumeMult);
        float volume = Mathf.Clamp01(reloadVolume * volMult);

        float pitchMult = Random.Range(minPitchMult, maxPitchMult);
        audioSource.pitch = basePitch * pitchMult;

        audioSource.PlayOneShot(reloadClip, volume);
    }
}
