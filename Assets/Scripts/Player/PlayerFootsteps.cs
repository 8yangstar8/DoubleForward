using UnityEngine;

/// <summary>
/// 玩家脚步声系统 - 根据地面材质播放不同的脚步音效
/// 支持多种地面类型：石头、木板、金属、沙地、水面、草地
/// 通过射线检测地面Tag或Layer来判断材质
/// </summary>
public class PlayerFootsteps : MonoBehaviour
{
    [Header("检测")]
    [SerializeField] private Transform footPosition;
    [SerializeField] private float checkRadius = 0.3f;
    [SerializeField] private LayerMask groundLayer;

    [Header("步频")]
    [SerializeField] private float walkStepInterval = 0.4f;
    [SerializeField] private float runStepInterval = 0.25f;
    [SerializeField] private float speedThreshold = 3f;   // 走/跑的速度分界
    [SerializeField] private float minMoveSpeed = 0.5f;   // 低于此不播放

    [Header("音量")]
    [SerializeField] private float walkVolume = 0.3f;
    [SerializeField] private float runVolume = 0.5f;
    [SerializeField] private float landingVolume = 0.6f;

    [Header("粒子")]
    [SerializeField] private GameObject dustParticlePrefab;
    [SerializeField] private GameObject waterSplashPrefab;
    [SerializeField] private float dustSpawnChance = 0.5f;

    private PlayerController controller;
    private AudioSource audioSource;
    private float stepTimer;
    private SurfaceType currentSurface = SurfaceType.Stone;
    private bool wasGrounded;

    public enum SurfaceType
    {
        Stone,
        Wood,
        Metal,
        Sand,
        Water,
        Grass,
        Ice,
        Snow
    }

    // 每种材质的音频key前缀
    private static readonly string[] surfaceSfxKeys =
    {
        "footstep_stone",
        "footstep_wood",
        "footstep_metal",
        "footstep_sand",
        "footstep_water",
        "footstep_grass",
        "footstep_ice",
        "footstep_snow"
    };

    // 每种材质的着地音频key
    private static readonly string[] landingSfxKeys =
    {
        "land_stone",
        "land_wood",
        "land_metal",
        "land_sand",
        "land_water",
        "land_grass",
        "land_ice",
        "land_snow"
    };

    void Awake()
    {
        controller = GetComponent<PlayerController>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.loop = false;
        }

        if (footPosition == null)
            footPosition = transform;
    }

    void Start()
    {
        // 监听着地事件
        if (controller != null)
        {
            controller.OnLanded += OnPlayerLanded;
            controller.OnJumped += OnPlayerJumped;
        }
    }

    void Update()
    {
        if (controller == null) return;
        if (!controller.IsGrounded || controller.IsDashing || controller.IsOnLadder)
            return;

        // 检测地面材质
        DetectSurface();

        // 步频计时
        float speed = Mathf.Abs(controller.Velocity.x);
        if (speed < minMoveSpeed)
        {
            stepTimer = 0f;
            return;
        }

        bool isRunning = speed >= speedThreshold;
        float interval = isRunning ? runStepInterval : walkStepInterval;

        stepTimer += Time.deltaTime;
        if (stepTimer >= interval)
        {
            stepTimer = 0f;
            PlayFootstep(isRunning);
        }
    }

    void OnDestroy()
    {
        if (controller != null)
        {
            controller.OnLanded -= OnPlayerLanded;
            controller.OnJumped -= OnPlayerJumped;
        }
    }

    // ==================== 地面检测 ====================

    private void DetectSurface()
    {
        var hit = Physics2D.OverlapCircle(footPosition.position, checkRadius, groundLayer);
        if (hit == null) return;

        // 通过Tag判断地面材质
        currentSurface = hit.tag switch
        {
            "WoodFloor" => SurfaceType.Wood,
            "MetalFloor" => SurfaceType.Metal,
            "SandFloor" => SurfaceType.Sand,
            "WaterSurface" => SurfaceType.Water,
            "GrassFloor" => SurfaceType.Grass,
            "IceFloor" => SurfaceType.Ice,
            "SnowFloor" => SurfaceType.Snow,
            _ => SurfaceType.Stone
        };
    }

    // ==================== 播放 ====================

    private void PlayFootstep(bool isRunning)
    {
        string sfxKey = surfaceSfxKeys[(int)currentSurface];
        float volume = isRunning ? runVolume : walkVolume;

        // 添加细微的随机音调变化
        float pitch = Random.Range(0.9f, 1.1f);

        PlaySFX(sfxKey, volume, pitch);
        SpawnFootstepParticle(isRunning);

        // 轻微振动
        if (isRunning && HapticFeedback.Instance != null)
            HapticFeedback.Instance.Custom(5, 0.1f);
    }

    private void OnPlayerLanded()
    {
        string sfxKey = landingSfxKeys[(int)currentSurface];
        PlaySFX(sfxKey, landingVolume, Random.Range(0.85f, 1f));

        // 着地粒子（更多）
        SpawnLandingParticle();

        // 轻振动
        if (HapticFeedback.Instance != null)
            HapticFeedback.Instance.Light();
    }

    private void OnPlayerJumped()
    {
        // 跳跃时播放一个轻微的脚步音
        string sfxKey = surfaceSfxKeys[(int)currentSurface];
        PlaySFX(sfxKey, walkVolume * 0.7f, 1.2f);
    }

    private void PlaySFX(string sfxKey, float volume, float pitch)
    {
        if (SoundFeedback.Instance != null)
        {
            SoundFeedback.Instance.Play(sfxKey);
        }
        else if (audioSource != null)
        {
            // 回退: 从Resources加载
            var clip = Resources.Load<AudioClip>($"Audio/SFX/{sfxKey}");
            if (clip != null)
            {
                audioSource.pitch = pitch;
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }

    // ==================== 粒子 ====================

    private void SpawnFootstepParticle(bool isRunning)
    {
        if (Random.value > dustSpawnChance) return;

        GameObject prefab = currentSurface == SurfaceType.Water
            ? waterSplashPrefab
            : dustParticlePrefab;

        if (prefab == null) return;

        Vector3 pos = footPosition.position;

        if (ObjectPool.Instance != null)
        {
            var go = ObjectPool.Instance.Get(prefab, pos, Quaternion.identity);
            // 自动回收
            StartCoroutine(ReturnToPoolAfter(go, prefab, 1f));
        }
        else
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            Destroy(go, 1f);
        }
    }

    private void SpawnLandingParticle()
    {
        GameObject prefab = currentSurface == SurfaceType.Water
            ? waterSplashPrefab
            : dustParticlePrefab;

        if (prefab == null) return;

        Vector3 pos = footPosition.position;

        if (ObjectPool.Instance != null)
        {
            var go = ObjectPool.Instance.Get(prefab, pos, Quaternion.identity);
            // 放大一点
            go.transform.localScale = Vector3.one * 1.5f;
            StartCoroutine(ReturnToPoolAfter(go, prefab, 1.5f));
        }
        else
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            go.transform.localScale = Vector3.one * 1.5f;
            Destroy(go, 1.5f);
        }
    }

    private System.Collections.IEnumerator ReturnToPoolAfter(GameObject go, GameObject prefab, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null && ObjectPool.Instance != null)
            ObjectPool.Instance.Return(go);
    }

    // ==================== Gizmo ====================

    void OnDrawGizmosSelected()
    {
        Transform point = footPosition != null ? footPosition : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(point.position, checkRadius);
    }
}
