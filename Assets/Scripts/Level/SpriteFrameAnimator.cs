using UnityEngine;

/// <summary>
/// 场景装饰的逐帧动画 - 按固定帧率循环一组精灵。
///
/// 玩家角色走 Animator 状态机,但终点旗、火把这类纯装饰用不上状态机,
/// 建一套 AnimatorController 反而更重。
/// </summary>
public class SpriteFrameAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 6f;

    private SpriteRenderer spriteRenderer;
    private float timer;
    private int index;

    public void Configure(Sprite[] animationFrames, float fps)
    {
        frames = animationFrames;
        framesPerSecond = fps;
    }

    private void Awake() => spriteRenderer = GetComponent<SpriteRenderer>();

    private void Update()
    {
        if (spriteRenderer == null || frames == null || frames.Length < 2 || framesPerSecond <= 0f) return;

        timer += Time.deltaTime;
        float step = 1f / framesPerSecond;
        if (timer < step) return;

        timer -= step;
        index = (index + 1) % frames.Length;
        spriteRenderer.sprite = frames[index];
    }
}
