using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("Tutorial");
    }

    // Called by Quit button
    public void QuitGame()
    {
        Debug.Log("Quit Game"); 
        Application.Quit();    
    }
}
