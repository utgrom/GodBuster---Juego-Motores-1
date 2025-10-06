using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reloads the active scene, intended for use from UI button events.
/// </summary>
public class SceneReloadButton : MonoBehaviour
{
    [SerializeField] private bool resetTimeScale = true;

    public void ReloadCurrentScene()
    {
        if (resetTimeScale)
        {
            Time.timeScale = 1f;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.buildIndex);
    }
}
