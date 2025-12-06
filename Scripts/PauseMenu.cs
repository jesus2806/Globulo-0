using UnityEngine;
using UnityEngine.UI;

public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject pausePanel;   // Tu panel/Canvas de pausa
    [SerializeField] private Button continueButton;   // Botón "Continuar" (opcional)

    public bool IsPaused { get; private set; }

    void Awake()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);

        // Enlaza el botón automáticamente si lo asignas en el Inspector
        if (continueButton != null)
            continueButton.onClick.AddListener(Resume);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    // Útil si tienes botón "Salir al menú" o "Reiniciar"
    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    void OnDisable()
    {
        // Seguridad: si desactivan el objeto con el juego pausado,
        // evita que el tiempo se quede en 0.
        if (IsPaused)
            Time.timeScale = 1f;
    }
}
