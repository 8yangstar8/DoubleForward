using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public event System.Action<float> OnLoadProgress;
    public event System.Action OnLoadComplete;
    public event System.Action<int> OnLoadStart; // chapterIndex
    public event System.Action OnLoadFinished;   // 场景激活后

    // 供UI层注入的委托
    public System.Func<Coroutine> TransitionInFunc;
    public System.Func<Coroutine> TransitionOutFunc;

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

    public void LoadScene(string sceneName, int chapterIndex = -1)
    {
        StartCoroutine(LoadSceneAsync(sceneName, chapterIndex));
    }

    private IEnumerator LoadSceneAsync(string sceneName, int chapterIndex = -1)
    {
        // 通知加载开始（UI层可响应显示加载界面）
        OnLoadStart?.Invoke(chapterIndex);

        // 播放过渡动画（如果UI层注册了）
        if (TransitionInFunc != null)
            yield return TransitionInFunc();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        operation.allowSceneActivation = false;

        while (!operation.isDone)
        {
            float progress = Mathf.Clamp01(operation.progress / 0.9f);
            OnLoadProgress?.Invoke(progress);

            if (operation.progress >= 0.9f)
            {
                operation.allowSceneActivation = true;
            }

            yield return null;
        }

        OnLoadComplete?.Invoke();

        // 通知加载完毕（UI层可响应隐藏加载界面）
        OnLoadFinished?.Invoke();

        // 播放过渡出场动画
        if (TransitionOutFunc != null)
            yield return TransitionOutFunc();

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Loading)
        {
            GameManager.Instance.SetState(GameManager.GameState.Playing);
        }
    }
}
