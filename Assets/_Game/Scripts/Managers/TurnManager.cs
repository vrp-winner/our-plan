using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Managers
{
    /// <summary>
    /// ตัวจัดการระบบเทิร์นของเกม
    /// แก้ไขระบบ Enforce Ownership ให้ทำงานได้จริง โดยอ้างอิงจาก Actor-to-Client Map
    /// </summary>
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
        
        // Mapping สำหรับเก็บว่า Actor(NetworkObjectId) ตัวไหน ต้องถูกควบคุมโดย Client(ClientId) อะไร
        private Dictionary<ulong, ulong> _actorToClientMap = new Dictionary<ulong, ulong>();
        private List<NetworkObject> _playerActors = new List<NetworkObject>();

        // public event Action<ulong, int> OnAvatarAssigned;

        public NetworkVariable<ulong> activeActorNetworkId = new NetworkVariable<ulong>(0);
        public NetworkVariable<bool> isGameStartedState = new NetworkVariable<bool>(false);
        public NetworkVariable<int> currentRound = new NetworkVariable<int>(0);
        public NetworkVariable<int> currentCycle = new NetworkVariable<int>(1);
        private NetworkVariable<int> _currentActorIndex = new NetworkVariable<int>(0);
        
        public event Action<ulong> OnPlayerTurnChanged;
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
        protected override void Awake()
        {
            base.Awake();
        }

        public override void OnNetworkSpawn()
        {
            isGameStartedState.OnValueChanged += (oldV, newV) => OnGameStateChanged?.Invoke(newV);
            _currentActorIndex.OnValueChanged += (oldV, newVal) => NotifyTurnChange();
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
            if (!IsServer || isGameStartedState.Value) return;
            
            isGameStartedState.Value = true;
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            
            // OnGameplaySceneLoaded จะถูกเรียกฝั่ง Server เท่านั้นตามตำแหน่งที่ Subscribe
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnGameplaySceneLoaded;
        }

        /// <summary>
        /// ทำงานบน Server เมื่อโหลดฉากเสร็จสิ้น: สร้างตัวละครและบันทึก Ownership Mapping
        /// </summary>
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
            _actorToClientMap.Clear(); // ล้างข้อมูลเก่า

            // STEP 1: วนลูปสร้างตัวละครทั้งหมด
            for (int i = 0; i < totalActors; i++)
            {
                ulong correctClientId = (playerRequired == 2) ? connectedClients[i < 2 ? 0 : 1] : connectedClients[i];
                Vector3 basePos = (centerSpawnPoint != null) ? centerSpawnPoint.position : Vector3.zero;
                Vector3 spawnPos = basePos + new Vector3(i * 2f, 0, 0);

                GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
                
                // STEP 2: มอบ Ownership และบันทึกลง Map ไว้เป็น Source of Truth
                netObj.SpawnWithOwnership(correctClientId);
                _actorToClientMap[netObj.NetworkObjectId] = correctClientId;

                if (playerInstance.TryGetComponent(out Systems.PlayerStatus status))
                {
                    status.TeamId.Value = (i < 2) ? 0 : 1;
                    status.MemberIndex.Value = (i % 2);
                    if (_playerSelectedAvatars.TryGetValue(correctClientId, out int avatarIdx))
                        status.AvatarIndex.Value = avatarIdx;

                    _playerActors.Add(netObj);
                }
            }

            currentRound.Value = 1;
            currentCycle.Value = 1;
            _currentActorIndex.Value = 0;

            if (_playerActors.Count > 0)
            {
                activeActorNetworkId.Value = _playerActors[0].NetworkObjectId;
                EnforceOwnershipOnTurnChange(_playerActors[0]);
            }

            NotifyTurnChange();
        }
        
        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            if (!IsServer) return;

            int nextIndex = _currentActorIndex.Value + 1;
            currentRound.Value++;

            if (nextIndex >= _playerActors.Count)
            {
                nextIndex = 0;
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
                
                // สลับสิทธิ์
                EnforceOwnershipOnTurnChange(nextActor);
            }
        }
        
        /// <summary>
        /// Validate และบังคับเปลี่ยน Ownership ไปยังผู้เล่นที่ถูกต้อง (อิงจาก Dictionary Mapping)
        /// ทำงานเฉพาะฝั่ง Server เท่านั้น
        /// </summary>
        /// <param name="activeActor">Active Actor</param>
        private void EnforceOwnershipOnTurnChange(NetworkObject activeActor)
        {
            // STEP 1: หา target ClientId จาก Actor-to-Client Map
            if (_actorToClientMap.TryGetValue(activeActor.NetworkObjectId, out ulong correctClientId))
            {
                // STEP 2: เช็คว่า Owner ปัจจุบันตรงกับ target หรือไม่
                if (activeActor.OwnerClientId != correctClientId)
                {
                    // STEP 3: ถ้าไม่ตรง ให้เปลี่ยน Ownership กลับไปยัง client ที่ถูกต้อง
                    activeActor.ChangeOwnership(correctClientId);
                }
            }

            Debug.Log($"[Turn] Active Actor = {activeActor.NetworkObjectId}");
            Debug.Log($"[Ownership] Who owns now = {activeActor.OwnerClientId}");
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
            OnPlayerTurnChanged?.Invoke(activeActorNetworkId.Value);
        }
        #endregion
    }
}