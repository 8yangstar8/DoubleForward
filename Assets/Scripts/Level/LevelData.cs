using UnityEngine;

[CreateAssetMenu(fileName = "NewLevelData", menuName = "DoubleForward/Level Data")]
public class LevelData : ScriptableObject
{
    [Header("Level Info")]
    public int chapter;
    public int levelIndex;
    public string levelName;
    [TextArea] public string description;

    [Header("Scene")]
    public string sceneName;

    [Header("Spawn Points")]
    public Vector2 luxSpawnPoint;
    public Vector2 noxSpawnPoint;

    [Header("Settings")]
    public float parTime = 300f;
    public bool hasTimerChallenge;
    public int collectibleCount;

    [Header("Audio")]
    public AudioClip bgmClip;
    public AudioClip ambientClip;

    public string DisplayName => $"{chapter}-{levelIndex}: {levelName}";
}
