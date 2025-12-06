using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] Transform target;          // Player (arr�stralo aqu�)
    [SerializeField] Vector2 offset = new Vector2(0f, 0.5f);

    [Header("Suavizado")]
    [SerializeField, Min(0f)] float smoothTime = 0.15f; // menor = m�s pegada
    [SerializeField] float maxSpeed = Mathf.Infinity;

    [Header("Look-Ahead (opcional)")]
    [SerializeField] bool useLookAhead = true;
    [SerializeField] float lookAheadMultiplier = 0.3f;  // cu�nto adelanta por frame
    [SerializeField] float lookAheadMax = 2f;           // l�mite en unidades

    [Header("L�mites del mapa (opcional)")]
    [SerializeField] Collider2D worldBounds;    // Box/Composite/Tilemap Collider que delimita el nivel

    Camera cam;
    Rigidbody2D targetRb;
    Vector3 velocity; // para SmoothDamp

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (!cam.orthographic) cam.orthographic = true;

        if (target != null)
            targetRb = target.GetComponent<Rigidbody2D>();
    }

    void LateUpdate()
    {
        if (!target) return;

        // Posici�n base (z actual para mantener la distancia de la c�mara)
        Vector3 desired = new Vector3(target.position.x + offset.x,
                                      target.position.y + offset.y,
                                      transform.position.z);

        // Look-ahead basado en la velocidad del jugador (suaviza el encuadre)
        if (useLookAhead && targetRb != null)
        {
            Vector2 la = targetRb.linearVelocity * Time.deltaTime * lookAheadMultiplier;
            la = Vector2.ClampMagnitude(la, lookAheadMax);
            desired.x += la.x;
            desired.y += la.y;
        }

        // Limitar a los bounds si est�n configurados
        if (worldBounds != null)
            desired = ClampToBounds(desired);

        // Movimiento suave
        transform.position = Vector3.SmoothDamp(
            transform.position, desired, ref velocity, smoothTime, maxSpeed, Time.deltaTime
        );
    }

    Vector3 ClampToBounds(Vector3 pos)
    {
        Bounds b = worldBounds.bounds;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        // Si el mapa es m�s chico que la c�mara en alg�n eje, centra en ese eje
        if (minX > maxX) pos.x = (b.min.x + b.max.x) * 0.5f;
        else pos.x = Mathf.Clamp(pos.x, minX, maxX);

        if (minY > maxY) pos.y = (b.min.y + b.max.y) * 0.5f;
        else pos.y = Mathf.Clamp(pos.y, minY, maxY);

        return pos;
    }

    // Para asignarlo en runtime si lo necesitas:
    public void SetTarget(Transform t)
    {
        target = t;
        targetRb = t ? t.GetComponent<Rigidbody2D>() : null;
    }
}
