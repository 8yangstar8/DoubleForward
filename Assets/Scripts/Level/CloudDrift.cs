using UnityEngine;

/// <summary>
/// 云朵飘动 - 沿X轴匀速漂移,越界后从另一侧绕回,给静止的背景加一点活气
/// </summary>
public class CloudDrift : MonoBehaviour
{
    [SerializeField] private float speed = 0.35f;
    [SerializeField] private float wrapDistance = 40f;

    private float startX;

    void Start()
    {
        startX = transform.position.x;
    }

    void Update()
    {
        transform.position += Vector3.right * (speed * Time.deltaTime);

        if (transform.position.x > startX + wrapDistance * 0.5f)
        {
            var p = transform.position;
            p.x = startX - wrapDistance * 0.5f;
            transform.position = p;
        }
    }

    /// <summary>编辑器配置用</summary>
    public void Configure(float driftSpeed, float wrapWidth)
    {
        speed = driftSpeed;
        wrapDistance = wrapWidth;
    }
}
