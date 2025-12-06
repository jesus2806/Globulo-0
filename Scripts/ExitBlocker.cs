using UnityEngine;

public class ExitBlocker : MonoBehaviour
{
    [Tooltip("GameObject que bloquea la salida (Tilemap + Collider)")]
    public GameObject bloqueo;

    Collider2D col;

    void Awake()
    {
        if (bloqueo == null)
            bloqueo = gameObject;

        col = bloqueo.GetComponent<Collider2D>();
    }

    // Cerrar o abrir
    public void SetClosed(bool closed)
    {
        // versión simple: activar/desactivar objeto completo
        bloqueo.SetActive(closed);

        // si quieres solo quitar colisión:
        if (col != null)
            col.enabled = closed;
    }

    // por si quieres seguir llamando a OpenGate desde otros sitios
    public void OpenGate()
    {
        SetClosed(false);
    }
}
