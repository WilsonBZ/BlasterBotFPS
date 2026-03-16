using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    public void StartGame()
    {
        SceneManager.LoadScene("03_Generator Room 1");
    }

    // Called by Quit button
    public void QuitGame()
    {
        Debug.Log("Quit Game"); 
        Application.Quit();    
    }
}
