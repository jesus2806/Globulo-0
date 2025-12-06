using UnityEngine;
using UnityEngine.SceneManagement;

public class ScenePortal : MonoBehaviour
{
    [Header("Nombre de la escena a cargar")]
    [SerializeField] private string sceneName;

    [Header("Tag del jugador")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Si el que entra es el jugador, cargar escena
        if (other.CompareTag(playerTag))
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.LogWarning($"[ScenePortal] No se asignó sceneName en {name}");
            }
        }
    }
}
