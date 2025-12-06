using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BossController2D))]
public class BossHealth : MonoBehaviour
{
    [Header("Vida")]
    public int maxHealth = 20;
    [SerializeField] int currentHealth;

    [Header("Muerte")]
    [Tooltip("Si es true, además de hacer fade, se destruye el GameObject al final.")]
    public bool destroyOnDeath = false;   // para tu boss déjalo en FALSE
    public GameObject deathEffect;

    [Header("Fade de muerte")]
    [SerializeField] float fadeDuration = 0.8f;

    [Header("UI Victoria")]
    [SerializeField] GameObject victoriaPanel;   // arrastra aquí "Victoria" del Canvas
    [SerializeField] bool pauseOnVictory = true; // pausar el juego al ganar

    BossController2D controller;
    bool isDead;

    void Awake()
    {
        controller = GetComponent<BossController2D>();
        currentHealth = maxHealth;

        // Si no se asignó desde el inspector, intenta localizarlo
        if (!victoriaPanel)
        {
            var canvasObj = GameObject.Find("Canvas");
            if (canvasObj != null)
            {
                Transform v = canvasObj.transform.Find("Victoria");
                if (v != null)
                    victoriaPanel = v.gameObject;
            }
        }

        // Arranca oculto
        if (victoriaPanel != null)
            victoriaPanel.SetActive(false);
    }

    public void TakeDamage(int amount, Vector2 hitPosition)
    {
        if (isDead) return;

        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
            return;
        }

        // Sigue vivo → anim de Hurt del boss
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
            Instantiate(deathEffect, transform.position, Quaternion.identity);

        if (controller != null)
            controller.PlayDeath(); // animación de muerte del boss

        // Lanza SIEMPRE el panel de victoria a los 5 segundos,
        // independientemente del fade, efectos, etc.
        StartCoroutine(ShowVictoryAfterDelay(5f));

        // Siempre hace fade; solo destruye si destroyOnDeath = true
        StartCoroutine(FadeRoutine(destroyOnDeath));
    }

    IEnumerator ShowVictoryAfterDelay(float delay)
    {
        // Usamos tiempo REAL, no afectado por Time.timeScale
        float elapsed = 0f;
        while (elapsed < delay)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (victoriaPanel != null)
            victoriaPanel.SetActive(true);

        if (pauseOnVictory)
            Time.timeScale = 0f; // pausa después de mostrar la victoria
    }

    /// <summary>
    /// Hace fade de todos los SpriteRenderer hijos hasta alpha 0.
    /// Si doDestroy es true, destruye el GameObject al final.
    /// </summary>
    IEnumerator FadeRoutine(bool doDestroy)
    {
        SpriteRenderer[] sprites = GetComponentsInChildren<SpriteRenderer>();
        if (sprites.Length == 0)
        {
            if (doDestroy) Destroy(gameObject);
            yield break;
        }

        float t = 0f;
        Color[] original = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            original[i] = sprites[i].color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, t / fadeDuration);

            for (int i = 0; i < sprites.Length; i++)
            {
                var sr = sprites[i];
                if (!sr) continue;

                Color c = original[i];
                c.a = a;
                sr.color = c;
            }

            yield return null;
        }

        // Aquí ya está "desaparecido" visualmente.
        if (doDestroy)
            Destroy(gameObject);
    }
}
