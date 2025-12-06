using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("Bloqueo global de salida")]
    public ExitBlocker exitBlocker;

    int enemiesAlive = 0;
    bool fightActive = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!exitBlocker)
            exitBlocker = FindObjectOfType<ExitBlocker>();
    }

    /// <summary>
    /// Llamado por RoomTrigger al empezar una pelea en cualquier room.
    /// </summary>
    public void StartFight(int enemyCount)
    {
        enemiesAlive = enemyCount;
        fightActive = enemyCount > 0;

        if (fightActive && exitBlocker != null)
        {
            exitBlocker.SetClosed(true);   // cierra el tilemap global
        }
    }

    /// <summary>
    /// Llamado por EnemyHealth cuando muere un enemigo.
    /// </summary>
    public void NotifyEnemyDeath(EnemyHealth enemy)
    {
        if (!fightActive) return;

        enemiesAlive--;
        //Debug.Log($"[CombatManager] Muere enemigo, quedan: {enemiesAlive}");

        if (enemiesAlive <= 0)
        {
            fightActive = false;

            if (exitBlocker != null)
            {
                exitBlocker.SetClosed(false); // abre el tilemap global
            }

            // 👇 BAJAR MÚSICA SUAVE A VOLUMEN NORMAL
            if (MusicFader.Instance != null)
            {
                MusicFader.Instance.FadeToNormal();
            }
        }
    }
}
