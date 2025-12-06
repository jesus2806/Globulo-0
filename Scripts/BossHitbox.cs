using UnityEngine;

[RequireComponent(typeof(BossHealth))]
public class BossHitbox : MonoBehaviour
{
    [Header("Daño que recibe del jugador")]
    [SerializeField] string bulletTag = "Bullet";   // tag de tus balas
    [SerializeField] bool destroyBulletOnHit = true;

    BossHealth health;

    void Awake()
    {
        health = GetComponent<BossHealth>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other.gameObject);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        TryHit(col.collider.gameObject);
    }

    void TryHit(GameObject other)
    {
        // ¿es una bala del jugador?
        bool isBullet = other.CompareTag(bulletTag) ||
                        other.GetComponent<Bullet>() != null;
        if (!isBullet) return;

        int damage = 1;
        Bullet b = other.GetComponent<Bullet>();
        if (b != null) damage = b.damage;

        Vector2 hitPos = other.transform.position;

        // aquí se descuenta la vida y se llaman StartHurt / PlayDeath
        if (health != null)
            health.TakeDamage(damage, hitPos);

        if (destroyBulletOnHit)
        {
            var rb = other.GetComponent<Rigidbody2D>();
            Destroy(rb ? rb.gameObject : other);
        }
    }
}
