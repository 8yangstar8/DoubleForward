using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField] private LevelData levelData;
    [SerializeField] private GameObject luxPrefab;
    [SerializeField] private GameObject noxPrefab;

    public LevelData CurrentLevel => levelData;
    public PlayerController LuxPlayer { get; private set; }
    public PlayerController NoxPlayer { get; private set; }
    public bool IsLevelComplete { get; private set; }

    private float levelTimer;
    private int collectiblesGathered;

    public event System.Action OnLevelStart;
    public event System.Action OnLevelComplete;
    public event System.Action<int> OnCollectibleGathered;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        InitializeLevel();
    }

    private void InitializeLevel()
    {
        SpawnPlayers();

        if (levelData.bgmClip != null)
            AudioManager.Instance?.PlayBGM(levelData.bgmClip);
        if (levelData.ambientClip != null)
            AudioManager.Instance?.PlayAmbient(levelData.ambientClip);

        levelTimer = 0f;
        collectiblesGathered = 0;
        IsLevelComplete = false;

        GameManager.Instance?.SetState(GameManager.GameState.Playing);
        OnLevelStart?.Invoke();
    }

    private void SpawnPlayers()
    {
        if (luxPrefab != null)
        {
            var luxObj = Instantiate(luxPrefab, levelData.luxSpawnPoint, Quaternion.identity);
            LuxPlayer = luxObj.GetComponent<PlayerController>();
            var luxHealth = luxObj.GetComponent<PlayerHealth>();
            if (luxHealth != null) luxHealth.SetCheckpoint(levelData.luxSpawnPoint);
        }

        if (noxPrefab != null)
        {
            var noxObj = Instantiate(noxPrefab, levelData.noxSpawnPoint, Quaternion.identity);
            NoxPlayer = noxObj.GetComponent<PlayerController>();
            var noxHealth = noxObj.GetComponent<PlayerHealth>();
            if (noxHealth != null) noxHealth.SetCheckpoint(levelData.noxSpawnPoint);
        }
    }

    void Update()
    {
        if (!IsLevelComplete && GameManager.Instance?.CurrentState == GameManager.GameState.Playing)
        {
            levelTimer += Time.deltaTime;
        }
    }

    public void CollectItem()
    {
        collectiblesGathered++;
        OnCollectibleGathered?.Invoke(collectiblesGathered);
    }

    public void CompleteLevel()
    {
        if (IsLevelComplete) return;
        IsLevelComplete = true;

        SaveSystem.Instance?.MarkLevelComplete(levelData.chapter, levelData.levelIndex);
        GameManager.Instance?.SetState(GameManager.GameState.LevelComplete);
        OnLevelComplete?.Invoke();
    }

    public void RestartLevel()
    {
        SceneLoader.Instance?.LoadScene(levelData.sceneName);
    }

    public float GetLevelTime() => levelTimer;
    public int GetCollectiblesGathered() => collectiblesGathered;
    public int GetTotalCollectibles() => levelData.collectibleCount;
}
