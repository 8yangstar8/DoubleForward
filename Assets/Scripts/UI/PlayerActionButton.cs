using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 屏幕上的动作按钮 - 直接驱动指定玩家的动作。
///
/// 为什么不走 InputManager: PlayerController.HandleInput 检测到键盘就直接 return,
/// 触屏分支根本读不到,在PC上点按钮不会有任何反应。这里绕开输入层直接调用,
/// 手机和PC都有效。
///
/// 跳跃/攻击用按下即触发;移动类按钮需要按住,所以实现了 Down/Up。
/// </summary>
public class PlayerActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public enum Action { Jump, Attack, Skill1, Skill2, MoveLeft, MoveRight }

    [SerializeField] private int playerIndex;
    [SerializeField] private Action action = Action.Attack;

    private bool held;

    public void Configure(int index, Action a)
    {
        playerIndex = index;
        action = a;
    }

    public void OnPointerDown(PointerEventData e)
    {
        var p = FindPlayer();
        if (p == null) return;

        switch (action)
        {
            case Action.Jump: p.TryJump(); break;
            case Action.Attack: p.TryAttack(); break;
            case Action.Skill1: p.TrySkill1(); break;
            case Action.Skill2: p.TrySkill2(); break;
            case Action.MoveLeft:
            case Action.MoveRight: held = true; break;
        }
    }

    public void OnPointerUp(PointerEventData e) => held = false;

    void Update()
    {
        if (!held) return;
        var p = FindPlayer();
        if (p == null) return;
        p.SetMoveInput(new Vector2(action == Action.MoveLeft ? -1f : 1f, 0f));
    }

    private PlayerController FindPlayer()
    {
        foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (p.PlayerIndex == playerIndex) return p;
        return null;
    }
}
