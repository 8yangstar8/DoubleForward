using UnityEngine;
using System.Collections.Generic;

public class EffectsManager : MonoBehaviour
{
    public static EffectsManager Instance { get; private set; }

    [Header("Player Effects")]
    [SerializeField] private GameObject jumpDustPrefab;
    [SerializeField] private GameObject landDustPrefab;
    [SerializeField] private GameObject dashTrailPrefab;
    [SerializeField] private GameObject deathEffectPrefab;
    [SerializeField] private GameObject respawnEffectPrefab;

    [Header("Ability Effects")]
    [SerializeField] private GameObject lightBeamEffectPrefab;
    [SerializeField] private GameObject shadowPhaseEffectPrefab;
    [SerializeField] private GameObject lightBridgeEffectPrefab;
    [SerializeField] private GameObject shadowZoneEffectPrefab;

    [Header("Puzzle Effects")]
    [SerializeField] private GameObject switchActivateEffectPrefab;
    [SerializeField] private GameObject portalEffectPrefab;
    [SerializeField] private GameObject collectEffectPrefab;

    [Header("Environment")]
    [SerializeField] private GameObject dustParticlesPrefab;
    [SerializeField] private GameObject waterSplashPrefab;
    [SerializeField] private GameObject sparksPrefab;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 10;

    private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();

    void Awake()
    {
        Instance = this;
    }

    public void SpawnEffect(GameObject prefab, Vector3 position, float lifetime = 2f)
    {
        if (prefab == null) return;

        var effect = GetFromPool(prefab);
        effect.transform.position = position;
        effect.transform.rotation = Quaternion.identity;
        effect.SetActive(true);

        var ps = effect.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();

        ReturnToPoolDelayed(prefab.name, effect, lifetime);
    }

    public void SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation, float lifetime = 2f)
    {
        if (prefab == null) return;

        var effect = GetFromPool(prefab);
        effect.transform.position = position;
        effect.transform.rotation = rotation;
        effect.SetActive(true);

        var ps = effect.GetComponent<ParticleSystem>();
        if (ps != null) ps.Play();

        ReturnToPoolDelayed(prefab.name, effect, lifetime);
    }

    // 便捷方法
    public void PlayJumpDust(Vector3 pos) => SpawnEffect(jumpDustPrefab, pos, 1f);
    public void PlayLandDust(Vector3 pos) => SpawnEffect(landDustPrefab, pos, 1f);
    public void PlayDashTrail(Vector3 pos) => SpawnEffect(dashTrailPrefab, pos, 0.5f);
    public void PlayDeathEffect(Vector3 pos) => SpawnEffect(deathEffectPrefab, pos, 2f);
    public void PlayRespawnEffect(Vector3 pos) => SpawnEffect(respawnEffectPrefab, pos, 1.5f);
    public void PlaySwitchActivate(Vector3 pos) => SpawnEffect(switchActivateEffectPrefab, pos, 1f);
    public void PlayPortalEffect(Vector3 pos) => SpawnEffect(portalEffectPrefab, pos, 1.5f);
    public void PlayCollectEffect(Vector3 pos) => SpawnEffect(collectEffectPrefab, pos, 1f);
    public void PlayWaterSplash(Vector3 pos) => SpawnEffect(waterSplashPrefab, pos, 1f);
    public void PlaySparks(Vector3 pos) => SpawnEffect(sparksPrefab, pos, 0.8f);

    // 对象池
    private GameObject GetFromPool(GameObject prefab)
    {
        string key = prefab.name;

        if (!pools.ContainsKey(key))
            pools[key] = new Queue<GameObject>();

        if (pools[key].Count > 0)
        {
            var obj = pools[key].Dequeue();
            if (obj != null) return obj;
        }

        var newObj = Instantiate(prefab);
        newObj.name = key;
        return newObj;
    }

    private void ReturnToPoolDelayed(string key, GameObject obj, float delay)
    {
        StartCoroutine(ReturnAfterDelay(key, obj, delay));
    }

    private System.Collections.IEnumerator ReturnAfterDelay(string key, GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (obj == null) yield break;

        obj.SetActive(false);

        if (!pools.ContainsKey(key))
            pools[key] = new Queue<GameObject>();

        if (pools[key].Count < poolSize)
            pools[key].Enqueue(obj);
        else
            Destroy(obj);
    }
}
