using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Terresquall; // VirtualJoystick

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    public float moveSpeed = 4f;

    Rigidbody2D rb;
    Animator anim;
    SpriteRenderer sr;
    Camera cam;

    Vector2 input;
    Vector2 lastMove;

    const float DZ = 0.10f;
    const float EPS = 0.10f;

    // ================== MOBILE (Terresquall) ==================
    [Header("Virtual Joystick Pack (Terresquall)")]
    [SerializeField] private bool useVirtualJoystickWhenPresent = true;

    [Tooltip("ID del joystick de movimiento (izquierdo).")]
    [SerializeField] private int moveJoystickId = 0;

    [Tooltip("ID del joystick de apuntado (derecho). Usa -1 si no tienes aim joystick.")]
    [SerializeField] private int aimJoystickId = 1;

    [SerializeField] private float aimDeadzone = 0.15f;

    // Botón UI para acción (Dash + Jump) - se conserva por si lo usas
    private bool mobileActionPressed;
    public void MobileAction() => mobileActionPressed = true;
    // ===========================================================

    // ================== Weapon ref ==================
    [Header("Weapon")]
    [SerializeField] private PlayerWeapon weapon;
    // =================================================

    // ================== Touch Gestures ==================
    [Header("Mobile Gestures")]
    [SerializeField] private bool enableTapShoot = true;
    [SerializeField] private bool enableSwipeJump = true;

    [Tooltip("Distancia mínima en pixeles para considerar swipe.")]
    [SerializeField] private float minSwipeDistance = 80f;

    [Tooltip("Tiempo máximo para considerar tap (segundos).")]
    [SerializeField] private float maxTapTime = 0.25f;

    private readonly Dictionary<int, Vector2> touchStartPos = new();
    private readonly Dictionary<int, float> touchStartTime = new();

    private bool tapShootThisFrame = false;
    private bool swipeJumpThisFrame = false;
    // ===========================================================

    // === Jump config ===
    [Header("Jump")]
    public float jumpLock = 0.30f;
    public float preJumpIdle = 0.10f;

    float jumpUnlockTime = -1f;
    float useInputFacingUntil = -1f;

    // === Dash config ===
    [Header("Dash")]
    public float dashSpeedMultiplier = 3.0f;
    public float dashDuration = 0.18f;
    public float dashCooldown = 0.40f;
    bool isDashing = false;
    float dashEndTime = -1f;
    float nextDashTime = 0f;
    Vector2 dashDir = Vector2.zero;

    // === KNOCKBACK ===
    [Header("Knockback")]
    public float knockbackSpeed = 5f;
    public float knockbackDuration = 0.15f;
    bool isKnockback = false;
    Vector2 knockDir;
    float knockEndTime = -1f;

    // === INVULNERABILIDAD POR LAYER ===
    [Header("Esquive: cambiar Layer (en vez de desactivar colliders)")]
    [SerializeField] private string dodgeLayerName = "PlayerDodge";
    [SerializeField] private bool applyLayerToChildren = true;

    private int dodgeLayer = -1;
    private bool isInDodgeLayer = false;
    private float dodgeLayerEndTime = -1f;

    private Transform[] cachedTransforms;
    private int[] cachedOriginalLayers;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        sr = GetComponentInChildren<SpriteRenderer>();
        cam = Camera.main;

        if (weapon == null)
            weapon = GetComponentInChildren<PlayerWeapon>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        dodgeLayer = LayerMask.NameToLayer(dodgeLayerName);
        if (dodgeLayer == -1)
        {
            Debug.LogWarning(
                $"[PlayerController2D] El layer '{dodgeLayerName}' no existe. " +
                $"Crea el layer o cambia el nombre en el Inspector."
            );
        }

        if (applyLayerToChildren)
        {
            cachedTransforms = GetComponentsInChildren<Transform>(true);
            cachedOriginalLayers = new int[cachedTransforms.Length];
            for (int i = 0; i < cachedTransforms.Length; i++)
                cachedOriginalLayers[i] = cachedTransforms[i].gameObject.layer;
        }
        else
        {
            cachedTransforms = new Transform[] { transform };
            cachedOriginalLayers = new int[] { gameObject.layer };
        }
    }

    void Update()
    {
        if (isKnockback && Time.time >= knockEndTime)
            isKnockback = false;

        // ===== Gestos móviles =====
        HandleTouchGestures();

        // Tap -> Disparo
        if (tapShootThisFrame && enableTapShoot && weapon != null)
        {
            weapon.ExternalShoot();
        }

        // ===== Movimiento (Teclado o VirtualJoystick) =====
        float h, v;

        bool joystickActive = useVirtualJoystickWhenPresent &&
                              VirtualJoystick.CountActiveInstances() > 0;

        if (joystickActive)
        {
            h = VirtualJoystick.GetAxisRaw("Horizontal", moveJoystickId);
            v = VirtualJoystick.GetAxisRaw("Vertical", moveJoystickId);
        }
        else
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
        }

        if (Mathf.Abs(h) < DZ) h = 0f;
        if (Mathf.Abs(v) < DZ) v = 0f;

        input = new Vector2(h, v);
        if (input.sqrMagnitude > 1f) input.Normalize();

        bool moving = (h != 0f || v != 0f);

        // Guardamos lastMove para fallback de dirección
        if (moving) lastMove = input;

        // ===== Aiming (Joystick derecho opcional o mouse) =====
        Vector2 aimDir = GetAimDir(joystickActive);

        // ===== Dirección de vista =====
        // REGLA NUEVA:
        // En móvil, si estás usando joystick y te estás moviendo,
        // mira hacia donde caminas.
        bool isMobile = Application.isMobilePlatform;
        bool mobileJoystickMoving = isMobile && joystickActive && moving;

        int viewDir;

        if (Time.time < useInputFacingUntil)
        {
            // Mantiene tu lógica original durante preJumpIdle
            viewDir = moving ? ComputeDir(h, v) : anim.GetInteger("LastDir");

            if (sr != null)
            {
                if (h < -EPS) sr.flipX = true;
                else if (h > EPS) sr.flipX = false;
            }
        }
        else
        {
            if (mobileJoystickMoving)
            {
                // <-- CAMBIO PRINCIPAL
                viewDir = ComputeDir(h, v);

                if (sr != null)
                {
                    if (h < -EPS) sr.flipX = true;
                    else if (h > EPS) sr.flipX = false;
                }
            }
            else
            {
                viewDir = ComputeDirFromVector(aimDir);
                if (sr != null) sr.flipX = (aimDir.x < -EPS);
            }
        }

        anim.SetFloat("Speed", (isDashing || moving) ? 1f : 0f);
        anim.SetInteger("Dir", viewDir);
        anim.SetInteger("LastDir", viewDir);

        // ===== Acción (clic derecho o botón móvil) =====
        // Se conserva tal cual para PC / botón UI si lo usas.
        bool actionThisFrame = !isKnockback &&
                               (Input.GetMouseButtonDown(1) || mobileActionPressed);

        mobileActionPressed = false;

        // ===== NUEVO: Swipe -> SOLO JUMP =====
        bool swipeJump = !isKnockback && swipeJumpThisFrame && enableSwipeJump;

        // --- DASH + JUMP (input original) ---
        if (actionThisFrame)
        {
            DoDash(moving);
            DoJump(moving, h, v);
        }

        // --- SOLO JUMP (swipe) ---
        if (swipeJump)
        {
            DoJump(moving, h, v);
        }

        // Fin del salto por tiempo
        if (anim.GetBool("IsJumping") && Time.time >= jumpUnlockTime)
        {
            anim.SetBool("IsJumping", false);
            TryExitDodgeLayer();
        }

        // Fin del dash por tiempo
        if (isDashing && Time.time >= dashEndTime)
        {
            isDashing = false;
            TryExitDodgeLayer();
        }

        TryExitDodgeLayer();
    }

    void FixedUpdate()
    {
        Vector2 vel = input * moveSpeed;

        if (isDashing)
            vel = dashDir * moveSpeed * dashSpeedMultiplier;

        if (isKnockback)
            vel += knockDir * knockbackSpeed;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = vel;
#else
        rb.velocity = vel;
#endif
    }

    // Evento opcional desde animación
    public void EndJump()
    {
        anim.SetBool("IsJumping", false);
        TryExitDodgeLayer();
    }

    // === KNOCKBACK API para PlayerLife ===
    public void ApplyKnockback(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            direction = -LastFacingVector(true);

        knockDir = direction.normalized;
        isKnockback = true;
        knockEndTime = Time.time + knockbackDuration;
    }

    // ================== DASH / JUMP helpers ==================
    private void DoDash(bool moving)
    {
        if (isDashing || Time.time < nextDashTime) return;

        dashDir = moving ? input : LastFacingVector(true);
        if (dashDir.sqrMagnitude < 0.0001f) dashDir = Vector2.down;
        dashDir.Normalize();

        isDashing = true;
        dashEndTime = Time.time + dashDuration;
        nextDashTime = Time.time + dashCooldown;

        RefreshDodgeLayer(dashDuration);

        if (sr != null)
        {
            if (dashDir.x < -EPS) sr.flipX = true;
            else if (dashDir.x > EPS) sr.flipX = false;
        }
    }

    private void DoJump(bool moving, float h, float v)
    {
        if (anim.GetBool("IsJumping")) return;

        int jdir = moving ? ComputeDir(h, v) : anim.GetInteger("LastDir");
        anim.SetInteger("JDir", jdir);

        useInputFacingUntil = Time.time + preJumpIdle;

        anim.SetBool("IsJumping", true);
        anim.ResetTrigger("Jump");
        anim.SetTrigger("Jump");

        jumpUnlockTime = Time.time + jumpLock;

        RefreshDodgeLayer(jumpLock);
    }
    // =========================================================

    // ================== LAYER DODGE ==================
    void RefreshDodgeLayer(float duration)
    {
        if (dodgeLayer == -1) return;

        dodgeLayerEndTime = Mathf.Max(dodgeLayerEndTime, Time.time + duration);

        if (!isInDodgeLayer)
            EnterDodgeLayer();
    }

    void EnterDodgeLayer()
    {
        if (dodgeLayer == -1 || cachedTransforms == null) return;

        isInDodgeLayer = true;

        for (int i = 0; i < cachedTransforms.Length; i++)
            if (cachedTransforms[i])
                cachedTransforms[i].gameObject.layer = dodgeLayer;
    }

    void ExitDodgeLayer()
    {
        if (cachedTransforms == null || cachedOriginalLayers == null) return;

        for (int i = 0; i < cachedTransforms.Length; i++)
            if (cachedTransforms[i])
                cachedTransforms[i].gameObject.layer = cachedOriginalLayers[i];

        isInDodgeLayer = false;
    }

    void TryExitDodgeLayer()
    {
        if (!isInDodgeLayer) return;

        bool stillInAction = isDashing || anim.GetBool("IsJumping");
        bool timeNotDone = Time.time < dodgeLayerEndTime;

        if (!stillInAction && !timeNotDone)
            ExitDodgeLayer();
    }
    // =================================================

    int ComputeDir(float h, float v)
    {
        if (h != 0f && v != 0f) return (v > 0f) ? 4 : 3;
        if (h != 0f) return 2;
        return (v > 0f) ? 1 : 0;
    }

    int ComputeDirFromVector(Vector2 v)
    {
        if (Mathf.Abs(v.x) > EPS && Mathf.Abs(v.y) > EPS)
            return v.y > 0f ? 4 : 3;
        if (Mathf.Abs(v.x) > Mathf.Abs(v.y))
            return 2;
        return v.y > 0f ? 1 : 0;
    }

    Vector2 GetAimDir(bool joystickActive)
    {
        // Si tienes joystick derecho configurado
        if (joystickActive && aimJoystickId >= 0 &&
            VirtualJoystick.CountActiveInstances() > 1)
        {
            Vector2 a = VirtualJoystick.GetAxis(aimJoystickId);
            if (a.sqrMagnitude > aimDeadzone * aimDeadzone)
                return a.normalized;

            if (lastMove.sqrMagnitude > 0.0001f)
                return lastMove.normalized;

            return Vector2.down;
        }

        // Aim de PC
        return GetAimDirMouse();
    }

    Vector2 GetAimDirMouse()
    {
        if (!cam) cam = Camera.main;
        float z = -cam.transform.position.z;
        Vector3 mw = cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, z));
        Vector2 dir = (Vector2)(mw - transform.position);
        if (dir.sqrMagnitude < 0.000001f) dir = Vector2.right;
        return dir.normalized;
    }

    Vector2 LastFacingVector(bool joystickActive)
    {
        if (lastMove.sqrMagnitude > 0.0001f) return lastMove.normalized;

        if (joystickActive && aimJoystickId >= 0 &&
            VirtualJoystick.CountActiveInstances() > 1)
        {
            Vector2 a = VirtualJoystick.GetAxis(aimJoystickId);
            if (a.sqrMagnitude > aimDeadzone * aimDeadzone)
                return a.normalized;
        }

        return Vector2.down;
    }

    // ================== TOUCH GESTURES ==================
    private void HandleTouchGestures()
    {
        tapShootThisFrame = false;
        swipeJumpThisFrame = false;

        if (!Application.isMobilePlatform) return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            // Ignorar toques sobre UI (joysticks, botones, etc.)
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject(t.fingerId))
            {
                continue;
            }

            if (t.phase == TouchPhase.Began)
            {
                touchStartPos[t.fingerId] = t.position;
                touchStartTime[t.fingerId] = Time.time;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                if (!touchStartPos.ContainsKey(t.fingerId)) continue;

                Vector2 startPos = touchStartPos[t.fingerId];
                float startTime = touchStartTime[t.fingerId];

                float dist = Vector2.Distance(startPos, t.position);
                float dt = Time.time - startTime;

                // Swipe -> Jump
                if (enableSwipeJump && dist >= minSwipeDistance)
                {
                    swipeJumpThisFrame = true;
                }
                // Tap -> Shoot
                else if (enableTapShoot && dt <= maxTapTime)
                {
                    tapShootThisFrame = true;
                }

                touchStartPos.Remove(t.fingerId);
                touchStartTime.Remove(t.fingerId);
            }
        }
    }
    // ======================================================
}
