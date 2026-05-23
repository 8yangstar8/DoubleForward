using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject luxPrefab;
    [SerializeField] private GameObject noxPrefab;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += SpawnPlayerForClient;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= SpawnPlayerForClient;
    }

    private void SpawnPlayerForClient(ulong clientId)
    {
        bool isHost = clientId == NetworkManager.Singleton.LocalClientId;
        var prefab = isHost ? luxPrefab : noxPrefab;
        var levelData = LevelManager.Instance?.CurrentLevel;

        Vector3 spawnPos;
        if (levelData != null)
            spawnPos = isHost ? (Vector3)levelData.luxSpawnPoint : (Vector3)levelData.noxSpawnPoint;
        else
            spawnPos = isHost ? new Vector3(-2, 0, 0) : new Vector3(2, 0, 0);

        var playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        var netObj = playerObj.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId);
    }
}
