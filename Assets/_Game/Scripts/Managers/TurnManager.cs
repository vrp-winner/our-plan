using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Managers
{
    public class TurnManager : SingletonNetwork<TurnManager>
    {
        #region Variables & Events
        [Header("Team Settings")]
        [SerializeField] private int playerRequired = 2;
        [SerializeField] private int totalActors = 4;

        [Header("Game Length Settings")]
        [SerializeField] private int maxCycles = 15;

        [Header("Spawning Setup")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform centerSpawnPoint;

        private Dictionary<ulong, int> _playerSelectedAvatars = new Dictionary<ulong, int>();

        public event Action<ulong, int> OnAvatarAssigned;

        public NetworkVariable<ulong> ActiveActorNetworkId = new NetworkVariable<ulong>(0);

        private NetworkVariable<bool> _isGameStarted = new NetworkVariable<bool>(false);
        private NetworkVariable<int> _currentActorIndex = new NetworkVariable<int>(0);
        private NetworkVariable<int> _currentRound = new NetworkVariable<int>(0);
        private NetworkVariable<int> _currentCycle = new NetworkVariable<int>(1);

        private List<NetworkObject> _playerActors = new List<NetworkObject>();

        public event Action<ulong> OnPlayerTurnChanged;
        public event Action<int> OnRoundChanged;
        public event Action<int> OnCycleChanged;
        public event Action<bool> OnGameStateChanged;
        #endregion

        #region Properties
        public bool IsGameStarted => _isGameStarted.Value;
        public void SetPlayerRequired(int count) => playerRequired = count;
        public int GetPlayerRequired() => playerRequired;

        public void SetMaxCycles(int cycles) => maxCycles = cycles;
        public int GetMaxCycles() => maxCycles;

        public ulong CurrentActivePlayerId
        {
            get
            {
                if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ActiveActorNetworkId.Value, out var activeObj))
                {
                    return activeObj.OwnerClientId;
                }
                return 99999;
            }
        }
        #endregion

        #region Unity Logic
        protected override void Awake()
        {
            base.Awake();
        }

        public override void OnNetworkSpawn()
        {
            _isGameStarted.OnValueChanged += (oldV, newV) => OnGameStateChanged?.Invoke(newV);
            _currentActorIndex.OnValueChanged += (oldV, newVal) => NotifyTurnChange();

            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
                {
                    if (NetworkManager.Singleton.ConnectedClients.Count >= playerRequired && !IsGameStarted)
                    {
                        Debug.Log("Clients Ready. Please Start Game.");
                    }
                };
            }
        }
        public void RegisterPlayerAvatar(ulong clientId, int avatarIndex)
        {
            _playerSelectedAvatars[clientId] = avatarIndex;
        }

        [Rpc(SendTo.Server)]
        public void SubmitAvatarSelectionServerRpc(int avatarIndex, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            RegisterPlayerAvatar(senderId, avatarIndex);
        }
        #endregion

        #region Game Flow Controls
        [Rpc(SendTo.Server)]
        public void StartGameServerRpc()
        {
            if (!IsServer || _isGameStarted.Value) return;
            _isGameStarted.Value = true;
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnGameplaySceneLoaded;
        }

        private void OnGameplaySceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameplaySceneLoaded;

            if (centerSpawnPoint == null)
            {
                GameObject sp = GameObject.FindWithTag("SpawnPoint");
                if (sp != null) centerSpawnPoint = sp.transform;
            }

            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
            _playerActors.Clear();

            for (int i = 0; i < totalActors; i++)
            {
                ulong ownerId = (playerRequired == 2) ? connectedClients[i < 2 ? 0 : 1] : connectedClients[i];
                Vector3 spawnPos = (centerSpawnPoint != null) ? centerSpawnPoint.position : Vector3.zero;

                GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
                netObj.SpawnWithOwnership(ownerId);

                if (playerInstance.TryGetComponent(out Systems.PlayerStatus status))
                {
                    status.TeamId.Value = (i < 2) ? 0 : 1;
                    status.MemberIndex.Value = (i % 2);
                    if (_playerSelectedAvatars.TryGetValue(ownerId, out int avatarIdx))
                        status.AvatarIndex.Value = avatarIdx;

                    _playerActors.Add(netObj);
                }
            }

            _currentRound.Value = 1;
            _currentCycle.Value = 1;
            _currentActorIndex.Value = 0;

            if (_playerActors.Count > 0)
            {
                ActiveActorNetworkId.Value = _playerActors[0].NetworkObjectId;
            }

            NotifyTurnChange();
        }

        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            if (!IsServer) return;

            int nextIndex = _currentActorIndex.Value + 1;
            _currentRound.Value++;
            OnRoundChanged?.Invoke(_currentRound.Value);

            if (nextIndex >= _playerActors.Count)
            {
                nextIndex = 0;
                _currentCycle.Value++;
                OnCycleChanged?.Invoke(_currentCycle.Value);

                if (_currentCycle.Value > 1 && (_currentCycle.Value - 1) % 3 == 0)
                {
                    EconomyManager.Instance.ProcessRentCollectionServerRpc();
                }
            }

            _currentActorIndex.Value = nextIndex;

            if (_playerActors.Count > 0)
            {
                ActiveActorNetworkId.Value = _playerActors[nextIndex].NetworkObjectId;
            }
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void NotifyEndGameRpc(ulong playerId, bool isWin, string reason)
        {
            Debug.Log($"[Game Over] Player {playerId} {(isWin ? "ชนะ" : "แพ้")} - เหตุผล: {reason}");
        }
        #endregion

        #region Helper Methods
        private void NotifyTurnChange()
        {
            OnPlayerTurnChanged?.Invoke(ActiveActorNetworkId.Value);
        }
        #endregion
    }
}