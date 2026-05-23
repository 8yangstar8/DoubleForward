using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { MainMenu, Loading, Playing, Paused, LevelComplete, GameOver }
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    public event System.Action<GameState> OnGameStateChanged;

    public int CurrentChapter { get; private set; } = 1;
    public int CurrentLevel { get; private set; } = 1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;
        var oldState = CurrentState;
        CurrentState = newState;
        OnGameStateChanged?.Invoke(newState);

        switch (newState)
        {
            case GameState.Paused:
                Time.timeScale = 0f;
                break;
            case GameState.Playing:
                Time.timeScale = 1f;
                break;
        }
    }

    public void LoadLevel(int chapter, int level)
    {
        CurrentChapter = chapter;
        CurrentLevel = level;
        SetState(GameState.Loading);
        SceneLoader.Instance?.LoadScene($"Level_{chapter}_{level}");
    }

    public void PauseGame()
    {
        if (CurrentState == GameState.Playing)
            SetState(GameState.Paused);
    }

    public void ResumeGame()
    {
        if (CurrentState == GameState.Paused)
            SetState(GameState.Playing);
    }

    public void ReturnToMainMenu()
    {
        SetState(GameState.MainMenu);
        SceneLoader.Instance?.LoadScene("MainMenu");
    }
}
