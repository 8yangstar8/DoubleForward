using UnityEngine;
using Unity.Netcode;

public class NetworkGameManager : NetworkBehaviour
{
    public static NetworkGameManager Instance { get; private set; }

    [SerializeField] private GameObject luxNetworkPrefab;
    [SerializeField] private GameObject noxNetworkPrefab;

    public NetworkVariable<bool> IsGameReady = new NetworkVariable<bool>(false);
    public NetworkVariable<int> ConnectedPlayers = new NetworkVariable<int>(0);

    public event System.Action OnAllPlayersReady;
    public event System.Action<ulong> OnPlayerDisconnected;

    void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
            ConnectedPlayers.Value = 1;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        ConnectedPlayers.Value++;

        if (ConnectedPlayers.Value >= 2)
        {
            SpawnPlayersClientRpc();
            IsGameReady.Value = true;
            OnAllPlayersReady?.Invoke();
        }
    }

    private void OnClientDisconnect(ulong clientId)
    {
        ConnectedPlayers.Value--;
        OnPlayerDisconnected?.Invoke(clientId);
    }

    [ClientRpc]
    private void SpawnPlayersClientRpc()
    {
        var levelManager = LevelManager.Instance;
        if (levelManager == null) return;
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestSpawnPlayerServerRpc(ulong clientId, bool isLux)
    {
        var prefab = isLux ? luxNetworkPrefab : noxNetworkPrefab;
        var spawnPos = isLux
            ? LevelManager.Instance?.CurrentLevel?.luxSpawnPoint ?? Vector2.zero
            : LevelManager.Instance?.CurrentLevel?.noxSpawnPoint ?? Vector2.zero;

        var playerObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        playerObj.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void NotifyLevelCompleteServerRpc()
    {
        LevelCompleteClientRpc();
    }

    [ClientRpc]
    private void LevelCompleteClientRpc()
    {
        LevelManager.Instance?.CompleteLevel();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncPuzzleStateServerRpc(string puzzleId, bool state)
    {
        UpdatePuzzleStateClientRpc(puzzleId, state);
    }

    [ClientRpc]
    private void UpdatePuzzleStateClientRpc(string puzzleId, bool state)
    {
        var puzzles = FindObjectsByType<PressurePlate>(FindObjectsSortMode.None);
        foreach (var puzzle in puzzles)
        {
            if (puzzle.gameObject.name == puzzleId)
            {
                if (state) puzzle.OnPressed?.Invoke();
                else puzzle.OnReleased?.Invoke();
            }
        }
    }
}
