using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button localPlayButton;
    [SerializeField] private Button lanPlayButton;
    [SerializeField] private Button onlinePlayButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject modeSelectPanel;

    void Start()
    {
        localPlayButton?.onClick.AddListener(OnLocalPlay);
        lanPlayButton?.onClick.AddListener(OnLanPlay);
        onlinePlayButton?.onClick.AddListener(OnOnlinePlay);
        continueButton?.onClick.AddListener(OnContinue);
        settingsButton?.onClick.AddListener(OnSettings);

        bool hasSave = SaveSystem.Instance != null && SaveSystem.Instance.Data.lastChapter > 0;
        if (continueButton != null)
            continueButton.interactable = hasSave;
    }

    private void OnLocalPlay()
    {
        InputManager.Instance?.SetPlayMode(InputManager.PlayMode.LocalSplitScreen);
        SplitScreenManager.Instance?.SetMode(SplitScreenManager.ScreenMode.SplitHorizontal);
        GameManager.Instance?.LoadLevel(1, 1);
    }

    private void OnLanPlay()
    {
        SceneLoader.Instance?.LoadScene("Lobby");
    }

    private void OnOnlinePlay()
    {
        SceneLoader.Instance?.LoadScene("Lobby");
    }

    private void OnContinue()
    {
        var save = SaveSystem.Instance?.Data;
        if (save != null)
            GameManager.Instance?.LoadLevel(save.lastChapter, save.lastLevel);
    }

    private void OnSettings()
    {
        mainPanel?.SetActive(false);
    }

    public void ShowMainPanel()
    {
        mainPanel?.SetActive(true);
        modeSelectPanel?.SetActive(false);
    }
}
