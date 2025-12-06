using UnityEngine;

public class Bullet : MonoBehaviour
{
    private new Rigidbody2D rigidbody;

    [Header("Movimiento")]
    public float speed = 3f;
    public float lifeTime = 3f;

    [Header("Daño")]
    public int damage = 1;

    void Start()
    {
        rigidbody = GetComponent<Rigidbody2D>();

        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        rigidbody.MovePosition(
            rigidbody.position + (Vector2)transform.right * speed * Time.fixedDeltaTime
        );
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Destroy(gameObject);
    }
}
