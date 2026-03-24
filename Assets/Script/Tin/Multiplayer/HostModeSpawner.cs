using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HostModeSpawner : SimulationBehaviour, IPlayerJoined, IPlayerLeft
{
    [SerializeField] private NetworkPrefabRef playerPrefab;
    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

    public void PlayerJoined(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        // Trục Z ép cứng bằng 0 cho 2D
        var spawnPosition = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-2f, 2f), 0f);
        var networkObject = Runner.Spawn(playerPrefab, spawnPosition, Quaternion.identity, player);

        _spawnedCharacters[player] = networkObject;
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!Runner.IsServer) return;

        if (_spawnedCharacters.TryGetValue(player, out var networkObject))
        {
            Runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }
}