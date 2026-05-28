using UnityEngine;

/// <summary>
/// 自动销毁组件 - 在指定时间后销毁GameObject
/// 用于VFX、临时特效、弹射物等
/// </summary>
public class AutoDestroyAfterTime : MonoBehaviour
{
    [SerializeField] private float lifetime = 1f;
    [SerializeField] private bool useUnscaledTime = false;

    private float timer;

    public void SetLifetime(float time)
    {
        lifetime = time;
    }

    void Update()
    {
        timer += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (timer >= lifetime)
            Destroy(gameObject);
    }
}
