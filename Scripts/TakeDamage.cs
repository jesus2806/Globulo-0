using UnityEngine;
using System.Collections;

public class TakeDamage : MonoBehaviour
{
    [SerializeField] int damage = 1;
    [SerializeField] float cooldown = 3f;

    PlayerLife vidaJugador;
    bool puedeDañar = true;

    void Awake()
    {
        if (!vidaJugador)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player) vidaJugador = player.GetComponent<PlayerLife>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!puedeDañar) return;

        // fallback: si no estaba cacheado, toma el PlayerLife del que colisiona
        if (!vidaJugador) vidaJugador = other.GetComponent<PlayerLife>();

        if (vidaJugador && (other.CompareTag("Player") || other.GetComponent<PlayerLife>()))
        {
            vidaJugador.RecibirDaño(damage);
            StartCoroutine(CooldownDaño());
        }
    }

    IEnumerator CooldownDaño()
    {
        puedeDañar = false;
        yield return new WaitForSeconds(cooldown);
        puedeDañar = true;
    }
}

