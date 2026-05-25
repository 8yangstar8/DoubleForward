using UnityEngine;
using System.Collections.Generic;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private List<Vector3> waypoints = new List<Vector3>();
    [SerializeField] private float speed = 2f;
    [SerializeField] private float waitTime = 0.5f;
    [SerializeField] private bool isLoop = true;
    [SerializeField] private bool startActive = true;
    [SerializeField] private bool useLocalPositions = true;

    public bool IsActive { get; private set; }

    private int currentWaypointIndex;
    private float waitTimer;
    private bool isWaiting;
    private bool isReversing;
    private List<Vector3> worldWaypoints;
    private List<Transform> riders = new List<Transform>();

    void Start()
    {
        IsActive = startActive;
        worldWaypoints = new List<Vector3>();

        foreach (var wp in waypoints)
        {
            worldWaypoints.Add(useLocalPositions ? transform.position + wp : wp);
        }

        if (worldWaypoints.Count == 0)
            worldWaypoints.Add(transform.position);
    }

    void FixedUpdate()
    {
        if (!IsActive || worldWaypoints.Count < 2) return;

        if (isWaiting)
        {
            waitTimer -= Time.fixedDeltaTime;
            if (waitTimer <= 0)
                isWaiting = false;
            return;
        }

        Vector3 target = worldWaypoints[currentWaypointIndex];
        Vector3 previousPos = transform.position;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.fixedDeltaTime);

        Vector3 delta = transform.position - previousPos;
        foreach (var rider in riders)
        {
            if (rider != null)
                rider.position += delta;
        }

        if (Vector3.Distance(transform.position, target) < 0.01f)
        {
            isWaiting = true;
            waitTimer = waitTime;
            AdvanceWaypoint();
        }
    }

    private void AdvanceWaypoint()
    {
        if (isLoop)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % worldWaypoints.Count;
        }
        else
        {
            if (isReversing)
            {
                currentWaypointIndex--;
                if (currentWaypointIndex <= 0) isReversing = false;
            }
            else
            {
                currentWaypointIndex++;
                if (currentWaypointIndex >= worldWaypoints.Count - 1) isReversing = true;
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<PlayerController>() != null)
        {
            if (!riders.Contains(collision.transform))
                riders.Add(collision.transform);
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        riders.Remove(collision.transform);
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
    public void Toggle() => IsActive = !IsActive;

    /// <summary>
    /// 运行时设置路径（由LevelBuilder调用）
    /// </summary>
    public void SetPath(Vector2[] path, float moveSpeed, bool loop)
    {
        speed = moveSpeed;
        isLoop = loop;
        useLocalPositions = false;

        waypoints.Clear();
        if (path != null)
        {
            foreach (var p in path)
                waypoints.Add(new Vector3(p.x, p.y, 0));
        }

        // 重新初始化世界路径
        worldWaypoints = new List<Vector3>(waypoints);
        currentWaypointIndex = 0;
        IsActive = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Count; i++)
        {
            Vector3 pos = useLocalPositions ? transform.position + waypoints[i] : waypoints[i];
            Gizmos.DrawWireSphere(pos, 0.3f);
            if (i < waypoints.Count - 1)
            {
                Vector3 next = useLocalPositions ? transform.position + waypoints[i + 1] : waypoints[i + 1];
                Gizmos.DrawLine(pos, next);
            }
        }
    }
}
