using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections.Generic;

namespace Managers
{
    /// <summary>
    /// ตัวจัดการระบบเทิร์นของเกม
    /// </summary>
    public class TurnManager : SingletonNetwork<TurnManager>
    {
        #region Variables & Events
        [Header("Team Settings")]
        [SerializeField] private int playerRequired = 2;

        [Header("Game Length Settings")]
        [SerializeField] private int maxCycles = 15;

        private List<NetworkObject> _playerActors = new List<NetworkObject>();

        public NetworkVariable<ulong> activeActorNetworkId = new NetworkVariable<ulong>(0);
        public NetworkVariable<bool> isGameStartedState = new NetworkVariable<bool>(false);
        public NetworkVariable<int> currentRound = new NetworkVariable<int>(0);
        public NetworkVariable<int> currentCycle = new NetworkVariable<int>(1);
        private readonly NetworkVariable<int> _currentActorIndex = new NetworkVariable<int>(0);

        // สถานะ Interaction ปัจจุบัน (UI ของสถานที่ไหนเปิดอยู่)
        public NetworkVariable<FixedString64Bytes> currentInteractionLocationId = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(""));
        public NetworkVariable<bool> isInteractionOpen = new NetworkVariable<bool>(false);
        public NetworkVariable<int> currentTime = new NetworkVariable<int>(0);
        
        public event Action<int> OnRoundChanged;
        public event Action<int> OnCycleChanged;
        public event Action<bool> OnGameStateChanged;
        #endregion

        #region Properties
        public bool IsGameStarted => isGameStartedState.Value;
        public void SetPlayerRequired(int count) => playerRequired = count;
        public int GetPlayerRequired() => playerRequired;
        public void SetMaxCycles(int cycles) => maxCycles = cycles;
        public int GetMaxCycles() => maxCycles;
        #endregion

        #region Unity Logic
        public override void OnNetworkSpawn()
        {
            isGameStartedState.OnValueChanged += (oldV, newV) => OnGameStateChanged?.Invoke(newV);
            currentRound.OnValueChanged += (oldV, newV) => OnRoundChanged?.Invoke(newV);
            currentCycle.OnValueChanged += (oldV, newV) => OnCycleChanged?.Invoke(newV);
            
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
                {
                    if (NetworkManager.Singleton.ConnectedClients.Count >= playerRequired && !IsGameStarted)
                    {
                        Debug.Log("[TurnManager] Clients Ready. Please Start Game.");
                    }
                };
            }
        }
        
        /// <summary>
        /// บันทึก Avatar ของผู้เล่น
        /// </summary>
        public void RegisterPlayerAvatar(ulong clientId, int avatarIndex)
        {
            if (PlayerSpawnManager.Instance != null)
            {
                PlayerSpawnManager.Instance.RegisterPlayerAvatar(clientId, avatarIndex);
            }
        }

        /// <summary>
        /// RPC จาก Client ส่ง Avatar ที่เลือกไป Server
        /// </summary>
        [Rpc(SendTo.Server)]
        public void SubmitAvatarSelectionServerRpc(int avatarIndex, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            RegisterPlayerAvatar(senderId, avatarIndex);
        }
        #endregion

        #region Game Flow Controls
        /// <summary>
        /// เริ่มเกม (Server เท่านั้น)
        /// </summary>
        [Rpc(SendTo.Server)]
        public void StartGameServerRpc()
        {
            if (!IsServer || isGameStartedState.Value) return;
            
            isGameStartedState.Value = true;
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnGameplaySceneLoaded;
        }

        /// <summary>
        /// Spawn ผู้เล่นหลังโหลด Scene เสร็จ
        /// </summary>
        private void OnGameplaySceneLoaded(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnGameplaySceneLoaded;

            if (PlayerSpawnManager.Instance != null)
            {
                _playerActors = PlayerSpawnManager.Instance.SpawnPlayers(playerRequired);
            }
            else
            {
                Debug.LogError("[TurnManager] PlayerSpawnManager is missing! Cannot spawn players.");
                return;
            }

            currentRound.Value = 1;
            currentCycle.Value = 1;
            _currentActorIndex.Value = 0;

            if (_playerActors != null && _playerActors.Count > 0)
            {
                activeActorNetworkId.Value = _playerActors[0].NetworkObjectId;
            }
        }
        
        /// <summary>
        /// จบเทิร์นปัจจุบันและไปผู้เล่นคนถัดไป
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            if (!IsServer) return;

            // บังคับปิด Panel และล้าง LocationId
            currentInteractionLocationId.Value = new FixedString64Bytes("");
            isInteractionOpen.Value = false;

            int nextIndex = (_currentActorIndex.Value + 1) % _playerActors.Count;
            currentRound.Value++;

            if (nextIndex == 0)
            {
                currentCycle.Value++;

                if (currentCycle.Value > 1 && (currentCycle.Value - 1) % 3 == 0)
                {
                    EconomyManager.Instance.ProcessRentCollectionServerRpc();
                }
            }

            _currentActorIndex.Value = nextIndex;

            if (_playerActors.Count > 0)
            {
                NetworkObject nextActor = _playerActors[nextIndex];
                activeActorNetworkId.Value = nextActor.NetworkObjectId;
            }
        }
        
        /// <summary>
        /// แจ้งผลเกมจบไป Client
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        public void NotifyEndGameRpc(ulong playerId, bool isWin, string reason)
        {
            Debug.Log($"[Game Over] Player {playerId} {(isWin ? "ชนะ" : "แพ้")} - เหตุผล: {reason}");
        }
        #endregion
    }
}