using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    [Tooltip("Name of the base gameplay scene that hosts the player and HUD. " +
             "This scene is loaded non-additively to replace the main menu.")]
    [SerializeField] private string bootSceneName = "03_Generator Room 1";

    /// <summary>Starts a fresh game from a clean state.</summary>
    public void StartGame()
    {
        if (AdditiveSceneManager.Instance != null)
        {
            // Singletons are alive from a previous session — do a full controlled restart.
            // This loads the boot scene non-additively (replacing the main menu) then
            // loads rooms on top, so the main menu canvas is fully destroyed.
            AdditiveSceneManager.Instance.NewGameFromMenu(bootSceneName);
        }
        else
        {
            // Very first launch — no singletons exist yet, load normally.
            SceneManager.LoadScene(bootSceneName);
        }
    }

    /// <summary>Called by the Quit button.</summary>
    public void QuitGame()
    {
        Debug.Log("Quit Game");
        Application.Quit();
    }
}
