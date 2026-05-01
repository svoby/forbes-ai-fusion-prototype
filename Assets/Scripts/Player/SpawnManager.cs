// SpawnManager.cs
// Task #2 — Player Spawn
// AC: Host spawns player at SpawnPoints[0]; client spawns at SpawnPoints[1].
// AC: Both peers see both capsules within 1 tick of the second peer joining.

using Fusion;
using System.Collections.Generic;
using UnityEngine;

namespace ForbesPrototype.Player
{
    /// <summary>
    /// Spawns and despawns player NetworkObjects when peers join/leave.
    /// Must only be called on the host (StateAuthority).
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private Transform[] spawnPoints;

        private readonly Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            int index = _spawnedPlayers.Count % spawnPoints.Length;
            Vector3 spawnPos = spawnPoints[index].position;

            NetworkObject playerObj = runner.Spawn(
                playerPrefab,
                spawnPos,
                Quaternion.identity,
                player);

            _spawnedPlayers[player] = playerObj;
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (!runner.IsServer) return;

            if (_spawnedPlayers.TryGetValue(player, out NetworkObject obj))
            {
                runner.Despawn(obj);
                _spawnedPlayers.Remove(player);
            }
        }
    }
}
