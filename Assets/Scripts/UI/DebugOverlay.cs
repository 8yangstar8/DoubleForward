using UnityEngine;
using TMPro;

/// <summary>
/// 调试信息叠加层 - 显示FPS、内存、对象池状态
/// 仅在Debug模式下可用
/// </summary>
public class DebugOverlay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private GameObject debugPanel;
    [SerializeField] private float updateInterval = 0.5f;

    private float timer;
    private int frameCount;
    private float fps;
    private bool isVisible;

    void Start()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        if (debugPanel != null) debugPanel.SetActive(false);
        enabled = false;
        return;
#endif
        if (debugPanel != null) debugPanel.SetActive(false);
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 三指点击切换调试面板
        if (Input.touchCount == 3 && Input.GetTouch(0).phase == TouchPhase.Began)
            ToggleDebug();

        // PC端用F12
        if (Input.GetKeyDown(KeyCode.F12))
            ToggleDebug();

        if (!isVisible) return;

        frameCount++;
        timer += Time.unscaledDeltaTime;

        if (timer >= updateInterval)
        {
            fps = frameCount / timer;
            frameCount = 0;
            timer = 0;
            UpdateDebugText();
        }
#endif
    }

    private void ToggleDebug()
    {
        isVisible = !isVisible;
        if (debugPanel != null) debugPanel.SetActive(isVisible);
    }

    private void UpdateDebugText()
    {
        if (debugText == null) return;

        var sb = new System.Text.StringBuilder();

        // FPS
        sb.AppendLine($"FPS: {fps:F1}");

        // 内存
        float usedMB = (float)System.GC.GetTotalMemory(false) / (1024 * 1024);
        sb.AppendLine($"GC Mem: {usedMB:F1} MB");
        sb.AppendLine($"Sys Mem: {SystemInfo.systemMemorySize} MB");

        // 性能等级
        if (PerformanceManager.Instance != null)
            sb.AppendLine($"Perf: {PerformanceManager.Instance.CurrentLevel}");

        // 屏幕信息
        sb.AppendLine($"Res: {Screen.width}x{Screen.height}");
        sb.AppendLine($"DPI: {Screen.dpi:F0}");

        // 对象池
        if (ObjectPool.Instance != null)
            sb.Append(ObjectPool.Instance.GetPoolStats());

        debugText.text = sb.ToString();
    }
}
