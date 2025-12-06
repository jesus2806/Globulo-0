using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOver : MonoBehaviour
{
    [Header("UI de Game Over")]
    [SerializeField] private GameObject gameOverCanvas; // Canvas o panel de GameOver

    private void Awake()
    {
        // Aseguramos que empiece oculto
        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);
    }

    // --- Mostrar Game Over (lo llamará el jugador al morir) ---
    public void MostrarGameOver()
    {
        if (gameOverCanvas != null)
        {
            gameOverCanvas.SetActive(true);
            Time.timeScale = 0f; // pausa el juego (opcional)
        }
    }

    // --- Botones del UI ---
    public void Reiniciar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void MenuInicial(string nombre)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nombre);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
