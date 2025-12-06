using System.Collections;
using UnityEngine;

[RequireComponent(typeof(EnemyController2D))]
public class EnemyHealth : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 3;
    [SerializeField] int currentHealth;

    [Header("Muerte")]
    public bool destroyOnDeath = true;
    public GameObject deathEffect;   // opcional: partícula / animación de muerte

    [Header("Fade de muerte")]
    [SerializeField] float fadeDuration = 0.8f;

    EnemyController2D controller;
    bool isDead;

    void Awake()
    {
        controller = GetComponent<EnemyController2D>();
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount, Vector2 hitPosition)
    {
        if (isDead) return;   // ya está muriendo, ignorar daño extra

        currentHealth -= amount;

        // --- si el daño es letal, NO reproducimos Hurt, vamos directo a Die ---
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
            return;
        }

        // Solo se ejecuta si sigue vivo
        if (controller != null)
        {
            Vector2 fromBullet = (Vector2)transform.position - hitPosition;
            controller.StartHurt(fromBullet);
        }
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;

        if (deathEffect != null)
        {
            Instantiate(deathEffect, transform.position, Quaternion.identity);
        }

        // avisar al CombatManager global
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.NotifyEnemyDeath(this);
        }

        // reproducir animación de muerte en el controller
        if (controller != null)
        {
            controller.PlayDeath();
        }

        if (destroyOnDeath)
        {
            StartCoroutine(FadeAndDestroy());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    IEnumerator FadeAndDestroy()
    {
        // todos los sprites del enemigo (por si tiene varios hijos)
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        if (sprites.Length == 0)
        {
            Destroy(gameObject);
            yield break;
        }

        float t = 0f;

        // guardar colores iniciales
        Color[] originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i]) originalColors[i] = sprites[i].color;
        }

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);

            for (int i = 0; i < sprites.Length; i++)
            {
                var sr = sprites[i];
                if (!sr) continue;

                Color c = originalColors[i];
                c.a = alpha;
                sr.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
