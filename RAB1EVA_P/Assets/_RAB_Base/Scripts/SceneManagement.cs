using UnityEngine;
using UnityEngine.SceneManagement; //Librería que contiene acciones de carga/descarga de escenas

public class SceneManagement : MonoBehaviour
{
    public void SceneChanger(int sceneToLoad)
    {
        //Carga un número de escena determinado
        SceneManager.LoadScene(sceneToLoad);
    }

    public void ExitGame()
    {
        //Acción que cierra la build sin usar Alt + F4
        Debug.Log("El botón de salir del juego funciona.");
        Application.Quit(); //Acción que cierra el juego
    }
}
