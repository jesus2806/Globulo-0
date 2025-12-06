using System.Collections;              // NUEVO
using UnityEngine;
using UnityEngine.UI;

public class PlayerLife : MonoBehaviour
{
    public int VidaMaxima = 3;
    private int VidaActual;
    public Image[] VidaImagen;

    [Header("Invulnerabilidad tras daño")]
    [SerializeField] float InvulnerableAfterHit = 1f;   // pon 1f para 1 segundo
    private float invulnUntil = -1f;

    [Header("Game Over")]
    [SerializeField] private GameOver gameOverManager;

    [Header("Feedback de daño")]             // NUEVO
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] PlayerController2D controller;
    [SerializeField] float blinkInterval = 0.1f;
    Coroutine blinkCoroutine;
    // ======================================

    void Start()
    {
        VidaActual = VidaMaxima;
        actualizarInterfaz();

        // Fallbacks si no los asignas en el Inspector  // NUEVO
        if (!spriteRenderer) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (!controller) controller = GetComponent<PlayerController2D>();
    }

    void actualizarInterfaz()
    {
        for (int i = 0; i < VidaImagen.Length; i++)
        {
            VidaImagen[i].enabled = i < VidaActual;
        }

        if (VidaActual <= 0)
        {
            Morir();
        }
    }

    void Morir()
    {
        if (gameOverManager != null)
            gameOverManager.MostrarGameOver();
    }

    // Versión antigua sigue funcionando
    public void RecibirDaño(int CantidadDaño)
    {
        RecibirDaño(CantidadDaño, Vector2.zero);
    }

    // NUEVO: permite pasar el punto desde donde vino el golpe
    public void RecibirDaño(int CantidadDaño, Vector2 hitPoint)
    {
        if (Time.time < invulnUntil) return;

        VidaActual -= CantidadDaño;
        VidaActual = Mathf.Clamp(VidaActual, 0, VidaMaxima);
        actualizarInterfaz();

        // ==== Knockback ====
        if (controller != null)
        {
            // dirección desde el golpe hacia el jugador (salir disparado "hacia atrás")
            Vector2 dir = ((Vector2)transform.position - hitPoint).normalized;
            controller.ApplyKnockback(dir);
        }

        // ==== Invulnerabilidad ====
        invulnUntil = Time.time + InvulnerableAfterHit;

        // ==== Parpadeo ====
        if (blinkCoroutine != null)
            StopCoroutine(blinkCoroutine);
        if (spriteRenderer != null)
            blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    public void ObtenerVida(int CuraTotal)
    {
        VidaActual += CuraTotal;
        VidaActual = Mathf.Clamp(VidaActual, 0, VidaMaxima);
        actualizarInterfaz();
    }

    // Coroutine de parpadeo  // NUEVO
    IEnumerator BlinkRoutine()
    {
        Color c = spriteRenderer.color;
        bool visible = true;

        while (Time.time < invulnUntil)
        {
            visible = !visible;
            c.a = visible ? 1f : 0.3f;   // alterna entre opaco y semi-transparente
            spriteRenderer.color = c;

            yield return new WaitForSeconds(blinkInterval);
        }

        // Asegurar que quede visible al final
        c.a = 1f;
        spriteRenderer.color = c;
    }
}
