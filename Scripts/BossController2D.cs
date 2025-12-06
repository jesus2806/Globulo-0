using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator))]
public class BossController2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] Transform target;          // Player
    Rigidbody2D rb;
    Animator anim;
    Collider2D[] selfCols;                      // para desactivar al morir

    [Header("Sprite / Flip horizontal")]
    [SerializeField] SpriteRenderer sprite;     // sprite del boss (child)

    [Header("Proyectiles")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] Transform shootPoint;
    [SerializeField] float projectileSpeed = 6f;
    [SerializeField] float projectileLife = 3f;

    [Header("Distancia al jugador")]
    [SerializeField] float desiredDistance = 5f;       // distancia que intenta mantener
    [SerializeField] float distanceTolerance = 1f;     // margen antes de acercarse/alejarse
    [SerializeField] float moveSpeed = 3f;
    [SerializeField] float retreatMultiplier = 1.2f;
    [SerializeField] float orbitMultiplier = 0.7f;

    [Header("Ataques generales")]
    [SerializeField] Vector2 attackIntervalRange = new Vector2(2f, 4f);
    [SerializeField] float attackPrepareTime = 0.35f;
    [SerializeField] float attackRecoverTime = 0.4f;

    [Header("Patrón circular (360)")]
    [SerializeField] int circleBullets = 18;
    [SerializeField] float circleSpeed = 5f;

    [Header("Patrón rehilete / espiral")]
    [SerializeField] int spiralWaves = 6;
    [SerializeField] int spiralBulletsPerWave = 14;
    [SerializeField] float spiralWaveInterval = 0.15f;
    [SerializeField] float spiralSpeed = 5f;

    [Header("Patrón lineal hacia el jugador")]
    [SerializeField] int aimedBursts = 4;
    [SerializeField] int bulletsPerBurst = 3;
    [SerializeField] float burstInterval = 0.15f;
    [SerializeField] float aimedSpreadAngle = 7f;

    [Header("Embestida")]
    [SerializeField] float chargeChance = 0.25f;
    [SerializeField] float chargeSpeed = 10f;
    [SerializeField] float chargeDuration = 0.6f;
    [SerializeField] float chargeRecoverTime = 0.5f;

    [Header("Animator – ataques")]
    [SerializeField] string attackTrigger = "Attack";
    [SerializeField] string isChargingBool = "IsCharging";

    // ------- HURT / Muerte -------
    [Header("Hurt")]
    [SerializeField] float hurtStun = 0.4f;
    [SerializeField] float knockbackSpeed = 6f;
    [SerializeField] float knockbackDrag = 12f;

    [SerializeField] string hurtTrigger = "Hurt";   // Trigger que entra a Hurt_*
    [SerializeField] string hurtBool = "IsHurt";    // Bool que mantiene la anim de Hurt
    [SerializeField] string hurtDirParam = "HDir";  // Int para elegir Hurt_Abajo/Arriba/Izquierda

    [Header("Muerte")]
    [SerializeField] string deathTrigger = "Die";

    // ---------- NUEVO: evitar paredes / atascos ----------
    [Header("Evitar paredes / atascarse")]
    [SerializeField] LayerMask obstacleMask;          // capa de paredes / obstáculos
    [SerializeField] float wallCheckDistance = 1.2f;  // hasta dónde mira para detectar pared
    [SerializeField] float avoidanceStrength = 1.0f;  // qué tanto se desvía lateralmente
    [SerializeField] float stuckCheckInterval = 0.6f; // cada cuánto revisa si está atorado
    [SerializeField] float stuckMinMoveDistance = 0.15f; // si se movió menos que esto -> atascado
    [SerializeField] float unstuckDuration = 0.5f;    // tiempo que usa dirección de "desatorar"

    float nextStuckCheckTime;
    Vector2 lastStuckPos;
    bool forceUnstuck = false;
    float forceUnstuckUntil = 0f;
    Vector2 forceUnstuckDir = Vector2.zero;

    // ---- estado interno ----
    bool isAttacking = false;
    bool isCharging = false;
    bool isHurt = false;
    bool isDead = false;

    float nextAttackTime = 0f;
    int lastDir4 = 0;
    int orbitDir = 1;
    float nextOrbitFlipTime = 0f;
    float hurtEndsAt = 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        selfCols = GetComponentsInChildren<Collider2D>(includeInactive: true);

        if (!target)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) target = p.transform;
        }

        if (!sprite)
        {
            // intenta buscar un SpriteRenderer en hijos
            sprite = GetComponentInChildren<SpriteRenderer>();
        }
    }

    void Start()
    {
        nextAttackTime = Time.time + Random.Range(1f, 2f);
        nextOrbitFlipTime = Time.time + Random.Range(2f, 5f);

        lastStuckPos = transform.position;
        nextStuckCheckTime = Time.time + stuckCheckInterval;
    }

    void Update()
    {
        if (isDead) return;
        if (!target) return;

        // ---------- Estado de HURT ----------

        if (isHurt)
        {
            // si quieres que siga moviéndose, también puedes quitar esto:
            // rb.velocity = Vector2.zero;

            if (Time.time >= hurtEndsAt)
            {
                isHurt = false;
                if (!string.IsNullOrEmpty(hurtBool))
                    anim.SetBool(hurtBool, false);
            }
        }

        // ---------- Movimiento normal / órbita ----------
        if (!isAttacking && !isCharging)
        {
            HandleMovement();
        }

        HandleAnimator();

        // ---------- Ataques ----------
        if (!isAttacking && !isCharging && Time.time >= nextAttackTime)
        {
            StartCoroutine(DoRandomAttack());
        }
    }

    // ===================== MOVIMIENTO =====================
    void HandleMovement()
    {
        Vector2 toPlayer = (Vector2)(target.position - transform.position);
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.0001f ? toPlayer / dist : Vector2.zero;

        Vector2 moveDir = Vector2.zero;

        if (forceUnstuck && Time.time < forceUnstuckUntil)
        {
            // mientras intentamos "desatorarlo", forzamos esta dirección
            moveDir = forceUnstuckDir;
        }
        else
        {
            forceUnstuck = false;

            if (dist > desiredDistance + distanceTolerance)
            {
                // demasiado lejos -> acercarse
                moveDir = dir;
            }
            else if (dist < desiredDistance - distanceTolerance)
            {
                // demasiado cerca -> alejarse
                moveDir = -dir * retreatMultiplier;
            }
            else
            {
                // rango óptimo -> orbitar alrededor del jugador
                if (Time.time >= nextOrbitFlipTime)
                {
                    orbitDir *= -1;
                    nextOrbitFlipTime = Time.time + Random.Range(2f, 5f);
                }

                Vector2 perp = new Vector2(-dir.y, dir.x) * orbitDir;
                moveDir = perp * orbitMultiplier;
            }

            // NUEVO: aplicar evitación de paredes a la dirección calculada
            moveDir = ApplyWallAvoidance(moveDir);
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = moveDir * moveSpeed;
#else
        rb.velocity = moveDir * moveSpeed;
#endif

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            lastDir4 = ToDir4(moveDir);
        }

        CheckStuck();
    }

    // --- NUEVO: usa raycasts para no irse directo contra la pared ---
    Vector2 ApplyWallAvoidance(Vector2 rawDir)
    {
        if (rawDir.sqrMagnitude < 0.0001f) return rawDir;

        Vector2 desiredDir = rawDir.normalized;
        Vector2 origin = transform.position;

        // Raycast hacia adelante (en la dirección deseada)
        RaycastHit2D hitForward = Physics2D.Raycast(origin, desiredDir, wallCheckDistance, obstacleMask);
        if (!hitForward)
        {
            // no hay pared enfrente -> todo bien
            return rawDir;
        }

        // Hay una pared enfrente: probamos izquierda y derecha y elegimos por donde hay más espacio
        Vector2 left = new Vector2(-desiredDir.y, desiredDir.x);
        Vector2 right = new Vector2(desiredDir.y, -desiredDir.x);

        float leftFree = wallCheckDistance;
        float rightFree = wallCheckDistance;

        RaycastHit2D hitLeft = Physics2D.Raycast(origin, left, wallCheckDistance, obstacleMask);
        if (hitLeft) leftFree = hitLeft.distance;

        RaycastHit2D hitRight = Physics2D.Raycast(origin, right, wallCheckDistance, obstacleMask);
        if (hitRight) rightFree = hitRight.distance;

        Vector2 avoidDir = (leftFree > rightFree) ? left : right;

        // Combinamos la dirección original con la de esquiva para hacer un "slide" por la pared
        Vector2 finalDir = (desiredDir + avoidDir * avoidanceStrength).normalized;

        return finalDir;
    }

    // --- NUEVO: detector de atascos / desatorar ---
    void CheckStuck()
    {
        if (Time.time < nextStuckCheckTime) return;

        float moved = Vector2.Distance(transform.position, lastStuckPos);
#if UNITY_6000_0_OR_NEWER
        float speed = rb.linearVelocity.magnitude;
#else
        float speed = rb.velocity.magnitude;
#endif

        // Si intenta moverse (tiene velocidad), pero casi no avanzó -> está atorado
        if (moved < stuckMinMoveDistance && speed > 0.1f)
        {
            Vector2 toPlayer = (target ? (Vector2)(target.position - transform.position) : Vector2.right);
            if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector2.right;

            // elegimos una dirección lateral aleatoria respecto al jugador
            Vector2 perp = new Vector2(-toPlayer.y, toPlayer.x).normalized;
            float sign = (Random.value < 0.5f) ? -1f : 1f;

            forceUnstuckDir = perp * sign;
            forceUnstuck = true;
            forceUnstuckUntil = Time.time + unstuckDuration;
        }

        lastStuckPos = transform.position;
        nextStuckCheckTime = Time.time + stuckCheckInterval;
    }

    void HandleAnimator()
    {
#if UNITY_6000_0_OR_NEWER
        Vector2 v = rb.linearVelocity;
#else
        Vector2 v = rb.velocity;
#endif
        bool moving = v.sqrMagnitude > 0.0001f;
        anim.SetFloat("Speed", moving ? 1f : 0f);
        anim.SetInteger("Dir4", lastDir4);
    }

    // ===================== ATAQUES =====================

    IEnumerator DoRandomAttack()
    {
        if (isDead) yield break;

        isAttacking = true;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        Vector2 toPlayer = (Vector2)(target.position - transform.position);
        Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;

        lastDir4 = ToDir4(dir);
        anim.SetInteger("ADir", lastDir4);

        bool willCharge = Random.value < chargeChance;

        if (willCharge)
        {
            yield return StartCoroutine(ChargeRoutine(dir));
        }
        else
        {
            anim.ResetTrigger(attackTrigger);
            anim.SetTrigger(attackTrigger);

            yield return new WaitForSeconds(attackPrepareTime);

            float r = Random.value;
            if (r < 0.33f)
            {
                ShootCircle();
            }
            else if (r < 0.66f)
            {
                yield return StartCoroutine(SpiralPattern());
            }
            else
            {
                yield return StartCoroutine(AimedBurstPattern());
            }

            yield return new WaitForSeconds(attackRecoverTime);
        }

        isAttacking = false;
        nextAttackTime = Time.time + Random.Range(attackIntervalRange.x, attackIntervalRange.y);
    }

    // ---- Embestida ----
    IEnumerator ChargeRoutine(Vector2 dir)
    {
        if (isDead) yield break;

        isCharging = true;
        if (HasBool(isChargingBool))
            anim.SetBool(isChargingBool, true);

        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
        lastDir4 = ToDir4(dir);

        float t = 0f;
        while (t < chargeDuration && !isDead && !isHurt)
        {
            // NUEVO: si hay pared justo enfrente, cortamos la embestida antes
            Vector2 origin = transform.position;
            RaycastHit2D hit = Physics2D.Raycast(origin, dir, 0.7f, obstacleMask);
            if (hit)
            {
                break; // choca de lleno contra una pared -> frena embestida
            }

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = dir * chargeSpeed;
#else
            rb.velocity = dir * chargeSpeed;
#endif
            t += Time.deltaTime;
            yield return null;
        }

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
        rb.velocity = Vector2.zero;
#endif

        if (HasBool(isChargingBool))
            anim.SetBool(isChargingBool, false);

        yield return new WaitForSeconds(chargeRecoverTime);

        isCharging = false;
    }

    // ---- Patrón circular ----
    void ShootCircle()
    {
        if (!projectilePrefab || !shootPoint) return;

        float angleStep = 360f / circleBullets;
        float angle = 0f;

        for (int i = 0; i < circleBullets; i++)
        {
            float rad = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

            SpawnProjectile(dir, circleSpeed);
            angle += angleStep;
        }
    }

    // ---- Patrón espiral ----
    IEnumerator SpiralPattern()
    {
        if (!projectilePrefab || !shootPoint) yield break;

        float baseAngle = 0f;
        float angleStep = 360f / spiralBulletsPerWave;

        for (int wave = 0; wave < spiralWaves; wave++)
        {
            float angle = baseAngle;

            for (int i = 0; i < spiralBulletsPerWave; i++)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

                SpawnProjectile(dir, spiralSpeed);
                angle += angleStep;
            }

            baseAngle += angleStep * 0.5f;
            yield return new WaitForSeconds(spiralWaveInterval);
        }
    }

    // ---- Patrón lineal hacia el jugador ----
    IEnumerator AimedBurstPattern()
    {
        if (!projectilePrefab || !shootPoint || !target) yield break;

        for (int burst = 0; burst < aimedBursts; burst++)
        {
            Vector2 toPlayer = (Vector2)(target.position - shootPoint.position);
            if (toPlayer.sqrMagnitude < 0.0001f)
                toPlayer = Vector2.down;

            float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
            int n = Mathf.Max(1, bulletsPerBurst);

            for (int i = 0; i < n; i++)
            {
                float t = (n == 1) ? 0.5f : i / (float)(n - 1);
                float offset = Mathf.Lerp(-aimedSpreadAngle, aimedSpreadAngle, t);
                float ang = baseAngle + offset;
                float rad = ang * Mathf.Deg2Rad;

                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                SpawnProjectile(dir, projectileSpeed);
            }

            yield return new WaitForSeconds(burstInterval);
        }
    }

    // ===================== HURT / DEATH API (para BossHealth) =====================

    public void StartHurt(Vector2 fromBullet)
    {
        if (isDead) return;

        if (isHurt) return;

        // Cancelar ataques/embestida en curso
        StopAllCoroutines();
        isAttacking = false;
        isCharging = false;

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
    rb.velocity = Vector2.zero;
#endif

        isHurt = true;
        hurtEndsAt = Time.time + hurtStun;

        // Usamos la dirección en la que YA está mirando el boss
        int hDir = lastDir4;

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

    public void EndHurtAnim()
    {
        if (isDead) return;
        isHurt = false;
        if (!string.IsNullOrEmpty(hurtBool))
            anim.SetBool(hurtBool, false);
    }

    public void PlayDeath()
    {
        if (isDead) return;
        isDead = true;

        StopAllCoroutines();

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = Vector2.zero;
#else
    rb.velocity = Vector2.zero;
#endif
        rb.simulated = false;

        if (selfCols != null)
        {
            foreach (var c in selfCols)
                if (c) c.enabled = false;
        }

        anim.SetFloat("Speed", 0f);
        if (HasBool(isChargingBool))
            anim.SetBool(isChargingBool, false);
        if (!string.IsNullOrEmpty(hurtBool))
            anim.SetBool(hurtBool, false);

        if (!string.IsNullOrEmpty(deathTrigger))
        {
            anim.ResetTrigger(deathTrigger);
            anim.SetTrigger(deathTrigger);
        }
    }


    // ===================== Helpers =====================

    void SpawnProjectile(Vector2 dir, float speed)
    {
        GameObject go = Instantiate(
            projectilePrefab,
            shootPoint.position,
            Quaternion.AngleAxis(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, Vector3.forward)
        );

        var ep = go.GetComponent<EnemyProjectile>();
        if (ep != null)
        {
            ep.Init(dir, speed, projectileLife);
            return;
        }

        var prb = go.GetComponent<Rigidbody2D>();
        if (prb != null)
        {
#if UNITY_6000_0_OR_NEWER
            prb.linearVelocity = dir.normalized * speed;
#else
            prb.velocity = dir.normalized * speed;
#endif
        }

        Destroy(go, projectileLife);
    }

    /// <summary>
    /// Convierte un vector a 4 direcciones para el Animator
    /// 0 = Abajo, 1 = Arriba, 3 = Lado (usa animación izquierda espejo para derecha)
    /// </summary>
    int ToDir4(Vector2 d)
    {
        if (d.sqrMagnitude < 0.0001f)
            return lastDir4;

        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
        {
            // LADO (izquierda / derecha) -> siempre animación "Izquierda"
            bool toRight = d.x > 0f;

            if (sprite != null)
            {
                // Asumiendo que la anim original mira a la IZQUIERDA:
                // izquierda  -> flipX = false
                // derecha    -> flipX = true
                sprite.flipX = toRight;
            }

            return 3; // Dir4 lateral (usará la anim izquierda en espejo)
        }
        else
        {
            if (sprite != null)
                sprite.flipX = false;

            return d.y >= 0 ? 1 : 0;   // arriba / abajo
        }
    }

    bool HasBool(string paramName)
    {
        if (string.IsNullOrEmpty(paramName)) return false;
        foreach (var p in anim.parameters)
        {
            if (p.name == paramName && p.type == AnimatorControllerParameterType.Bool)
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, desiredDistance);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, desiredDistance - distanceTolerance);
        Gizmos.DrawWireSphere(transform.position, desiredDistance + distanceTolerance);

        // Gizmo extra para ver el rango de chequeo de paredes
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * wallCheckDistance);
    }
#endif
}
