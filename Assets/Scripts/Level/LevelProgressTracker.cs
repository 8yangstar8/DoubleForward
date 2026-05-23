using UnityEngine;
using System.Collections.Generic;

public class LevelProgressTracker : MonoBehaviour
{
    [SerializeField] private List<LevelData> allLevels = new List<LevelData>();

    public int GetUnlockedLevelCount()
    {
        int count = 1;
        for (int i = 0; i < allLevels.Count; i++)
        {
            var level = allLevels[i];
            if (SaveSystem.Instance != null && SaveSystem.Instance.IsLevelCompleted(level.chapter, level.levelIndex))
                count++;
        }
        return Mathf.Min(count, allLevels.Count);
    }

    public bool IsLevelUnlocked(int chapter, int levelIndex)
    {
        if (chapter == 1 && levelIndex == 1) return true;

        int globalIndex = GetGlobalIndex(chapter, levelIndex);
        if (globalIndex <= 0) return false;

        var prevLevel = allLevels[globalIndex - 1];
        return SaveSystem.Instance != null && SaveSystem.Instance.IsLevelCompleted(prevLevel.chapter, prevLevel.levelIndex);
    }

    public LevelData GetLevelData(int chapter, int levelIndex)
    {
        return allLevels.Find(l => l.chapter == chapter && l.levelIndex == levelIndex);
    }

    public LevelData GetNextLevel(int currentChapter, int currentLevel)
    {
        int globalIndex = GetGlobalIndex(currentChapter, currentLevel);
        if (globalIndex >= 0 && globalIndex < allLevels.Count - 1)
            return allLevels[globalIndex + 1];
        return null;
    }

    private int GetGlobalIndex(int chapter, int levelIndex)
    {
        return allLevels.FindIndex(l => l.chapter == chapter && l.levelIndex == levelIndex);
    }

    public List<LevelData> GetChapterLevels(int chapter)
    {
        return allLevels.FindAll(l => l.chapter == chapter);
    }
}
