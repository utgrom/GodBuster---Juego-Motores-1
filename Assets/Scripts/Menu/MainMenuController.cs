using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string levelSceneName = "Level";

    [Header("UI")]
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        // Asegura que el panel de opciones arranque oculto
        if (optionsPanel != null) optionsPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

    }

    // Botón: Empezar juego
    public void StartGame()
    {
        if (!string.IsNullOrEmpty(levelSceneName))
        {
            SceneManager.LoadScene(levelSceneName);
        }
        else
        {
            Debug.LogWarning("No se configuró el nombre de la escena del nivel.");
        }
    }

    // Botón: Options (abrir)
    public void OpenOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    // Botón dentro de Options: Cerrar
    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
    }

    // Botón: Salir del juego
    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
