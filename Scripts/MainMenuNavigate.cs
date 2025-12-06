using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuNavigate : MonoBehaviour
{

    public void Jugar()
    {
        Debug.Log("Cargando el juego...");
        SceneManager.LoadScene("Nivel1");
    }
    public void Opciones()
    {
        Debug.Log("Cargando menú opciones…");
    }
    public void Salir()
    {
        Debug.Log("Saliendo del juego...");
        Application.Quit();
    }


}
