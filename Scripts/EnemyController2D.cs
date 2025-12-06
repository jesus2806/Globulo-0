using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class EnemyController2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform target;           // Player (se autollenará por Tag)
    Rigidbody2D rb;
    Animator anim;
    Collider2D[] selfCols;                       // para ignorar colisiones con balas propias

    [Header("Vida")]
    [SerializeField] EnemyHealth health;

    [Header("Evitar obstáculos")]
    public LayerMask obstacleMask;
    public float obstacleCheckDistance = 0.6f;
    public float sideCheckDistance = 0.5f;

    // ---- estado de evasión de obstáculos ----
    [Header("Evasión avanzada")]
    public float avoidSideTime = 0.4f;   // cuánto tiempo insiste en ir de lado
    int currentAvoidSign = 0;            // -1 = izquierda, 1 = derecha, 0 = sin evasión
    float avoidSideUntil = 0f;

    [Header("Movimiento")]
    public float walkSpeed = 2f;
    public float sprintMultiplier = 2f;

    [Header("IA (decisiones)")]
    public float thinkEvery = 1f;
    [Range(0, 1)] public float idleChance = 0.20f;
    public Vector2 idleDuration = new Vector2(1f, 3f);
    [Range(0, 1)] public float sprintChance = 0.25f;
    public Vector2 sprintDuration = new Vector2(0.8f, 1.6f);

    [Header("Distancias (sin CircleCollider)")]
    [Tooltip("Se detiene (no avanza) cuando la distancia al jugador es <= a este valor.")]
    public float stopDistance = 1.0f;
    [Tooltip("Vuelve a avanzar cuando la distancia es > a este valor (histéresis).")]
    public float resumeDistance = 1.2f;

    [Header("Bloqueo de avance")]
    [Tooltip("Cada vez que empieza a avanzar, lo hará al menos este tiempo.")]
    public float minMoveDuration = 1.5f;

    [Header("Ataque (melee)")]
    public float attackCooldown = 0.8f;    // Ataca por cooldown (no depende de distancia)
    public float animAttackLock = 0.35f;   // Bloqueo de movimiento durante el ataque
    public string attackTrigger = "Attack";

    [Header("Daño / Hurt")]
    public float hurtStun = 0.25f;
    public float knockbackSpeed = 5f;
    public float knockbackDrag = 12f;
    public string bulletTag = "Bullet";
    public bool destroyBulletOnHit = true;

    [Header("Parámetros Animator Hurt")]
    [SerializeField] string hurtTrigger = "Hurt";     // trigger para entrar a Hurt_*
    [SerializeField] string hurtBool = "IsHurt";      // bool que mantienen las anims de Hurt
    [SerializeField] string hurtDirParam = "HDir";    // int para elegir Hurt_Abajo/Arriba/Izquierda

    [Header("Muerte (animación)")]
    public string deathTrigger = "Die";

    // ===== RANGED / PROJECTILES =====
    [Header("Ranged (abanico)")]
    public GameObject projectilePrefab;          // Prefab con EnemyProjectile o RB2D
    public Transform shootPoint;                 // hijo en la mano/boquilla
    public int fanCount = 5;                     // nº de balas
    [Range(0f, 360f)] public float fanArc = 180f;// medio círculo
    public float projectileSpeed = 6f;
    public float projectileLife = 2.5f;
    [Tooltip("Centra el abanico hacia el jugador si está activo; si no, hacia ARRIBA.")]
    public bool aimAtPlayer = false;
    [Tooltip("Si no apuntas al jugador, el abanico se centra hacia ARRIBA.")]
    public bool fanCenteredUp = true;

    [Header("Ranged aleatorio (3–5 s si NO hay choque)")]
    public Vector2 randomShootInterval = new Vector2(3f, 5f);  // [min, max] segundos
    public bool useShootAnimation = false;                     // si quieres lanzar trigger
    public string shootTrigger = "Shoot";                      // nombre del trigger de disparo

    // ---- estado interno ----
    bool isIdle, isRunning, isAttacking, inStopZone, wasMoving;
    float stateEndsAt, nextThinkAt, nextAttackAt, moveLockUntil;
    bool isHurt; float hurtEndsAt;
    int lastDir4 = 2; // derecha por defecto

    bool isDead = false;   // estado de muerte

    // contacto con Player para pausar disparo aleatorio
    int touchingPlayerCount = 0;
    bool wasTouching = false;
    float nextShootAt = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        selfCols = GetComponentsInChildren<Collider2D>(includeInactive: true);
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        if (!health) health = GetComponent<EnemyHealth>();
    }

    void Start()
    {
        if (!target)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }
        nextThinkAt = Time.time + Random.Range(0.2f, 0.6f);
        ScheduleNextRandomShot(); // primera cita de disparo aleatorio
    }

    void Update()
    {
        if (isDead) return;
        if (!target) return;

        // === Estado de Hurt: solo knockback + espera a que termine ===
        if (isHurt)
        {
            rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.deltaTime * knockbackDrag);

            // si el tiempo de stun terminó, soltamos el bool (por si no usas Animation Event)
            if (Time.time >= hurtEndsAt)
            {
                isHurt = false;
                if (!string.IsNullOrEmpty(hurtBool))
                    anim.SetBool(hurtBool, false);
            }
            return;
        }

        // Vector y distancia al player
        Vector2 delta = (Vector2)(target.position - transform.position);
        float dist = delta.magnitude;
        Vector2 dir = dist > 0.0001f ? delta / dist : Vector2.zero;

        // Stop-zone por distancia (histéresis clara)
        if (inStopZone)
        {
            if (dist >= resumeDistance) inStopZone = false;
        }
        else
        {
            if (dist <= stopDistance) inStopZone = true;
        }

        // -------- Disparo aleatorio (3–5 s) si NO hay contacto físico con el Player --------
        bool touching = (touchingPlayerCount > 0);
        if (wasTouching && !touching)
        {
            // al dejar de tocar al player, reprograma el siguiente disparo
            ScheduleNextRandomShot();
        }
        if (!touching && Time.time >= nextShootAt)
        {
            if (useShootAnimation)
            {
                anim.ResetTrigger(shootTrigger);
                anim.SetTrigger(shootTrigger); // Animation Event -> AE_ShootFan()
            }
            else
            {
                AE_ShootFan(); // dispara inmediatamente sin animación
            }
            ScheduleNextRandomShot();
        }
        wasTouching = touching;

        // --- Ataque melee por cooldown SOLO si está tocando al player ---
        if (touching && !isAttacking && Time.time >= nextAttackAt)
        {
            StartAttack(dir);
        }
        if (isAttacking && Time.time >= stateEndsAt)
        {
            EndAttack();
        }

        // Decisiones periódicas (idle/sprint) con lock de avance
        if (!isAttacking && Time.time >= nextThinkAt)
        {
            nextThinkAt = Time.time + thinkEvery;

            if (Time.time >= moveLockUntil)
            {
                if (!isIdle && Random.value < idleChance)
                {
                    isIdle = true; isRunning = false;
                    stateEndsAt = Time.time + Random.Range(idleDuration.x, idleDuration.y);
                }
                else if (!isIdle && Random.value < sprintChance)
                {
                    isRunning = true;
                    stateEndsAt = Time.time + Random.Range(sprintDuration.x, sprintDuration.y);
                }
            }
        }
        if (isIdle && Time.time >= stateEndsAt) isIdle = false;
        if (isRunning && Time.time >= stateEndsAt) isRunning = false;
        if (inStopZone || isAttacking) isRunning = false;

        // ================== MOVIMIENTO ==================
        bool canMove = !isAttacking && !isIdle && !inStopZone;
        float speedMul = isRunning ? sprintMultiplier : 1f;

        // dirección base hacia el jugador
        Vector2 moveDir = dir;

        // si podemos movernos, intenta evitar obstáculos
        if (canMove)
        {
            moveDir = AvoidObstacles(dir);
        }

        Vector2 desired = canMove ? moveDir * (walkSpeed * speedMul) : Vector2.zero;

        bool nextWillMove = desired.sqrMagnitude > 0.0001f;
        if (nextWillMove && !wasMoving)
            moveLockUntil = Time.time + minMoveDuration;

        // Durante el lock de movimiento, seguimos usando la dirección con evitación
        if (Time.time < moveLockUntil)
        {
            bool canMoveLock = !isAttacking && !inStopZone;
            if (canMoveLock)
            {
                Vector2 lockDir = AvoidObstacles(dir);
                desired = lockDir * (walkSpeed * speedMul);
            }
            else
            {
                desired = Vector2.zero;
            }
        }

        rb.linearVelocity = desired;

        // Animator
        bool moving = desired.sqrMagnitude > 0.0001f;
        anim.SetFloat("Speed", moving ? 1f : 0f);
        anim.SetBool("IsRunning", isRunning);

        int dir4 = ToDir4(dir);
        anim.SetInteger("Dir4", dir4);
        if (moving) lastDir4 = dir4;
        anim.SetInteger("LastDir4", lastDir4);

        wasMoving = moving;
    }

    // ----- ATAQUE MELEE -----
    void StartAttack(Vector2 dir)
    {
        if (isDead) return;

        isAttacking = true;
        anim.SetBool("IsAttacking", true);
        rb.linearVelocity = Vector2.zero;

        anim.SetInteger("ADir", ToDir4(dir));
        anim.ResetTrigger(attackTrigger);
        anim.SetTrigger(attackTrigger);

        stateEndsAt = Time.time + animAttackLock;
        nextAttackAt = Time.time + attackCooldown;
    }

    public void EndAttack()
    {
        isAttacking = false;
        anim.SetBool("IsAttacking", false);
    }

    // ===== Disparo en abanico (Animation Event o directo) =====
    public void AE_ShootFan()
    {
        if (isDead) return;
        if (!projectilePrefab || !shootPoint) return;

        // Dirección base del abanico
        Vector2 forward;
        if (aimAtPlayer && target)
            forward = ((Vector2)target.position - (Vector2)shootPoint.position);
        else
            forward = fanCenteredUp ? Vector2.up : Vector2.right;  // por defecto, ARRIBA

        if (forward.sqrMagnitude < 0.0001f) forward = Vector2.up;

        float baseAng = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
        float start = baseAng - fanArc * 0.5f;

        for (int i = 0; i < fanCount; i++)
        {
            float t = (fanCount == 1) ? 0.5f : i / (float)(fanCount - 1);
            float ang = start + t * fanArc;

            Quaternion rot = Quaternion.AngleAxis(ang, Vector3.forward);
            Vector2 dir = rot * Vector2.right;

            GameObject go = Instantiate(projectilePrefab, shootPoint.position, rot);

            var ep = go.GetComponent<EnemyProjectile>();
            if (ep) ep.Init(dir, projectileSpeed, projectileLife);
            else
            {
                var rbp = go.GetComponent<Rigidbody2D>();
                if (rbp) rbp.linearVelocity = dir.normalized * projectileSpeed;
                Destroy(go, projectileLife);
            }

            // Evitar colisión con el propio enemigo
            var colB = go.GetComponent<Collider2D>();
            if (colB != null && selfCols != null)
                foreach (var c in selfCols) if (c) Physics2D.IgnoreCollision(colB, c, true);
        }
    }

    // ----- HURT / KNOCKBACK -----
    public void StartHurt(Vector2 fromBullet)
    {
        if (isDead) return;

        // Cancelar ataque / movimiento especial
        isAttacking = false;
        isIdle = false;
        isRunning = false;
        anim.SetBool("IsAttacking", false);

        isHurt = true;
        hurtEndsAt = Time.time + hurtStun;

        // dirección de knockback (desde la bala hacia el enemigo)
        Vector2 away = fromBullet.normalized;
        if (away.sqrMagnitude < 0.0001f) away = Vector2.right;

        rb.linearVelocity = away * knockbackSpeed;

        int hDir = ToDir4(away);

        // Parámetros de Animator para seleccionar Hurt_*
        if (!string.IsNullOrEmpty(hurtDirParam))
            anim.SetInteger(hurtDirParam, hDir);
        if (!string.IsNullOrEmpty(hurtBool))
            anim.SetBool(hurtBool, true);
        if (!string.IsNullOrEmpty(hurtTrigger))
        {
            anim.ResetTrigger(hurtTrigger);
            anim.SetTrigger(hurtTrigger);
        }
    }

    // Llamar este método desde un Animation Event al final de Hurt_*
    public void EndHurtAnim()
    {
        isHurt = false;
        if (!string.IsNullOrEmpty(hurtBool))
            anim.SetBool(hurtBool, false);
    }

    // --------- Detección de contacto con Player (para pausar disparo aleatorio) ---------
    bool IsPlayerRoot(Transform t)
    {
        if (!t) return false;
        Transform root = t;
        if (t.TryGetComponent<Rigidbody2D>(out _)) root = t.transform;
        else if (t.GetComponentInParent<Rigidbody2D>()) root = t.GetComponentInParent<Rigidbody2D>().transform;

        return (target && root == target) || root.CompareTag("Player");
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // contacto con jugador
        if (IsPlayerRoot(other.transform))
        {
            touchingPlayerCount++;
            return;
        }

        if (isDead) return;

        // daño por balas del player
        if (isHurt) return;

        bool isBullet = (!string.IsNullOrEmpty(bulletTag) && other.CompareTag(bulletTag)) ||
                        other.GetComponent<Bullet>() != null;
        if (!isBullet) return;

        int damage = 1;
        Bullet b = other.GetComponent<Bullet>();
        if (b != null) damage = b.damage;

        Vector2 hitPos = other.transform.position;

        if (health != null)
            health.TakeDamage(damage, hitPos);
        else
            StartHurt((Vector2)transform.position - hitPos); // fallback, solo anim

        if (destroyBulletOnHit)
            Destroy(other.attachedRigidbody ? other.attachedRigidbody.gameObject : other.gameObject);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (IsPlayerRoot(other.transform))
            touchingPlayerCount = Mathf.Max(0, touchingPlayerCount - 1);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (IsPlayerRoot(col.collider.transform))
        {
            touchingPlayerCount++;
            return;
        }

        if (isDead) return;
        if (isHurt) return;

        bool isBullet = (!string.IsNullOrEmpty(bulletTag) && col.collider.CompareTag(bulletTag)) ||
                        col.collider.GetComponent<Bullet>() != null;
        if (!isBullet) return;

        int damage = 1;
        Bullet b = col.collider.GetComponent<Bullet>();
        if (b != null) damage = b.damage;

        Vector2 hitPos = col.collider.transform.position;

        if (health != null)
            health.TakeDamage(damage, hitPos);
        else
            StartHurt((Vector2)transform.position - hitPos);

        if (destroyBulletOnHit)
            Destroy(col.collider.gameObject);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (IsPlayerRoot(col.collider.transform))
            touchingPlayerCount = Mathf.Max(0, touchingPlayerCount - 1);
    }

    // --------- Muerte (animación) ---------
    public void PlayDeath()
    {
        if (isDead) return;
        isDead = true;

        // parar movimiento y física
        rb.linearVelocity = Vector2.zero;
        rb.simulated = false;

        // desactivar colliders
        if (selfCols != null)
        {
            foreach (var c in selfCols)
            {
                if (c) c.enabled = false;
            }
        }

        // resetear flags de anim
        anim.SetBool("IsAttacking", false);
        anim.SetBool("IsRunning", false);
        if (!string.IsNullOrEmpty(hurtBool))
            anim.SetBool(hurtBool, false);
        anim.SetFloat("Speed", 0f);

        // lanzar trigger de muerte
        if (!string.IsNullOrEmpty(deathTrigger))
        {
            anim.ResetTrigger(deathTrigger);
            anim.SetTrigger(deathTrigger);
        }
    }

    // ---- Evitar obstáculos con raycast frontal + desvío lateral persistente ----
    Vector2 AvoidObstacles(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            return dir;

        dir = dir.normalized;
        Vector2 origin = transform.position;

        // 1) Si estamos en modo "bordear obstáculo", seguir yendo de lado un rato
        if (Time.time < avoidSideUntil && currentAvoidSign != 0)
        {
            Vector2 perp = (currentAvoidSign < 0)
                ? new Vector2(-dir.y, dir.x)   // izquierda
                : new Vector2(dir.y, -dir.x);  // derecha

            return perp.normalized;
        }

        // 2) Mirar si hay algo enfrente
        RaycastHit2D hitForward = Physics2D.Raycast(origin, dir, obstacleCheckDistance, obstacleMask);
        if (!hitForward)
        {
            // Nada delante, reseteamos modo evasión y seguimos normal
            currentAvoidSign = 0;
            return dir;
        }

        // 3) Calcular perpendiculares izquierda/derecha
        Vector2 perpLeft = new Vector2(-dir.y, dir.x).normalized;
        Vector2 perpRight = new Vector2(dir.y, -dir.x).normalized;

        bool leftBlocked = Physics2D.Raycast(origin, perpLeft, sideCheckDistance, obstacleMask);
        bool rightBlocked = Physics2D.Raycast(origin, perpRight, sideCheckDistance, obstacleMask);

        // 4) Elegir lado
        if (!leftBlocked && rightBlocked)
            currentAvoidSign = -1;
        else if (!rightBlocked && leftBlocked)
            currentAvoidSign = 1;
        else if (!leftBlocked && !rightBlocked)
            currentAvoidSign = (Random.value < 0.5f) ? -1 : 1;
        else
        {
            // Ambos lados bloqueados → no sabemos por dónde, seguimos empujando un poco
            currentAvoidSign = 0;
            return dir;
        }

        // 5) Activar modo "bordear" durante avoidSideTime
        avoidSideUntil = Time.time + avoidSideTime;

        Vector2 chosenPerp = (currentAvoidSign < 0) ? perpLeft : perpRight;
        return chosenPerp;
    }

    // Helpers
    int ToDir4(Vector2 d)
    {
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
            return d.x >= 0 ? 2 : 3;   // derecha / izquierda
        else
            return d.y >= 0 ? 1 : 0;   // arriba / frontal (abajo)
    }

    void ScheduleNextRandomShot()
    {
        nextShootAt = Time.time + Random.Range(randomShootInterval.x, randomShootInterval.y);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, resumeDistance);
    }
#endif
}
