using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button mainMenuButton;

    void Start()
    {
        resumeButton?.onClick.AddListener(OnResume);
        restartButton?.onClick.AddListener(OnRestart);
        settingsButton?.onClick.AddListener(OnSettings);
        mainMenuButton?.onClick.AddListener(OnMainMenu);

        if (pausePanel != null)
            pausePanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance?.CurrentState == GameManager.GameState.Playing)
                ShowPause();
            else if (GameManager.Instance?.CurrentState == GameManager.GameState.Paused)
                OnResume();
        }
    }

    public void ShowPause()
    {
        pausePanel?.SetActive(true);
        GameManager.Instance?.PauseGame();
    }

    private void OnResume()
    {
        pausePanel?.SetActive(false);
        GameManager.Instance?.ResumeGame();
    }

    private void OnRestart()
    {
        pausePanel?.SetActive(false);
        GameManager.Instance?.ResumeGame();
        LevelManager.Instance?.RestartLevel();
    }

    private void OnSettings()
    {
        // 打开设置面板
    }

    private void OnMainMenu()
    {
        pausePanel?.SetActive(false);
        GameManager.Instance?.ReturnToMainMenu();
    }
}
