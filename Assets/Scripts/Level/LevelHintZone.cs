using UnityEngine;

/// <summary>
/// 关卡提示区 - 玩家走进来时弹一条文字提示,告诉他这里该用哪个能力。
/// 关卡设计得再巧,玩家看不懂也没用。
///
/// 与 TutorialTrigger 的区别: 那个只能触发预定义的教程步骤ID,
/// 这个直接携带文本,便于关卡构建脚本按位置就地配置。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LevelHintZone : MonoBehaviour
{
    [SerializeField] [TextArea(2, 3)] private string hintText = "";
    [SerializeField] private float displayDuration = 5f;
    [SerializeField] private bool triggerOnce = true;

    private bool triggered;

    public string HintText => hintText;

    void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered && triggerOnce) return;
        if (other.GetComponent<PlayerController>() == null) return;
        if (string.IsNullOrEmpty(hintText)) return;

        triggered = true;
        HintSystem.Instance?.ShowHint(hintText, displayDuration);
    }

    /// <summary>编辑器配置用</summary>
    public void Configure(string text, float duration, bool once = true)
    {
        hintText = text;
        displayDuration = duration;
        triggerOnce = once;
    }
}
