using UnityEngine;

public class RoomTrigger : MonoBehaviour
{
    [Header("Padre que contiene los enemigos / boss de ESTA habitación")]
    [SerializeField] private Transform enemiesParent;

    [Header("Solo se activa una vez")]
    [SerializeField] private bool oneShot = true;

    private bool alreadyTriggered = false;

    private void Awake()
    {
        // asegurar que TODOS los hijos empiezan desactivados (incluido el boss)
        if (enemiesParent != null)
        {
            foreach (Transform child in enemiesParent)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[RoomTrigger] Entró: {other.name}, tag: {other.tag}");
        string[] validTags = { "Player", "Pared" };  // o solo "Player" si quieres

        if (System.Array.IndexOf(validTags, other.tag) < 0) return;
        if (oneShot && alreadyTriggered) return;

        ActivateRoomEnemies();
        alreadyTriggered = true;
    }

    private void ActivateRoomEnemies()
    {
        if (enemiesParent == null)
        {
            Debug.LogWarning($"[RoomTrigger] No hay enemiesParent asignado en {name}");
            return;
        }

        // --- activar enemigos normales ---
        EnemyHealth[] enemies = enemiesParent.GetComponentsInChildren<EnemyHealth>(true);
        foreach (var e in enemies)
        {
            e.gameObject.SetActive(true);
        }

        // --- activar boss(es) ---
        BossHealth[] bosses = enemiesParent.GetComponentsInChildren<BossHealth>(true);
        foreach (var b in bosses)
        {
            b.gameObject.SetActive(true);
        }

        int totalEnemies = enemies.Length + bosses.Length;

        // avisar al CombatManager de que empieza pelea con N enemigos (incluye boss)
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.StartFight(totalEnemies);
        }

        // --- MÚSICA ---
        if (MusicFader.Instance != null)
        {
            if (bosses.Length > 0)
            {
                // Sala de boss → música de boss con transición suave
                MusicFader.Instance.FadeToBoss();
            }
            else
            {
                // Sala normal → música de combate normal
                MusicFader.Instance.FadeToCombat();
            }
        }
    }
}
