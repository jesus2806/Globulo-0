using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyProjectile : MonoBehaviour
{
    public float speed = 5f;
    public float life = 2.5f;

    Rigidbody2D rb;
    Vector2 dir;

    public void Init(Vector2 direction, float speedOverride = 0f, float lifeOverride = 0f)
    {
        dir = direction.sqrMagnitude > 0 ? direction.normalized : Vector2.right;
        if (speedOverride > 0f) speed = speedOverride;
        if (lifeOverride > 0f) life = lifeOverride;
        Destroy(gameObject, life);
    }

    void Awake() => rb = GetComponent<Rigidbody2D>();

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Evita destruirse al tocar al propio enemigo si no usas layers
        if (other.GetComponentInParent<EnemyController2D>()) return;
        Destroy(gameObject);
    }
}
