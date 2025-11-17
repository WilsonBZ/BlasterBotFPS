using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUi : MonoBehaviour
{

    [SerializeField] private Button resumeButtonPrefab;
    [SerializeField] private Button settingsButtonPrefab;
    [SerializeField] private Button quitButtonPrefab;

    void Awake()
    {
        // Instantiate and set up Resume Button
        Button resumeButton = Instantiate(resumeButtonPrefab, transform);
        resumeButton.onClick.AddListener(OnResumeButtonClicked);
        // Instantiate and set up Settings Button
        Button settingsButton = Instantiate(settingsButtonPrefab, transform);
        settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        // Instantiate and set up Quit Button
        Button quitButton = Instantiate(quitButtonPrefab, transform);
        quitButton.onClick.AddListener(OnQuitButtonClicked);
    }


    void OnResumeButtonClicked()
    {
        Debug.Log("Resume button clicked");
        // Add logic to resume the game
    }


    void OnSettingsButtonClicked()
    {
        Debug.Log("Settings button clicked");
        // Add logic to open settings menu
    }

    void OnQuitButtonClicked()
    {
        Debug.Log("Quit button clicked");
        // Add logic to quit the game
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
