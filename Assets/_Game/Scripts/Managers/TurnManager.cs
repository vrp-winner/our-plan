using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using Systems;

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

        public NetworkVariable<GameObjective> currentObjective = new NetworkVariable<GameObjective>();

        public event Action<bool, string> OnGameOverTriggered;

        public NetworkVariable<ulong> activeActorNetworkId = new NetworkVariable<ulong>(0);
        public NetworkVariable<bool> isGameStartedState = new NetworkVariable<bool>(false);
        public NetworkVariable<int> currentRound = new NetworkVariable<int>(1);
        public NetworkVariable<int> currentCycle = new NetworkVariable<int>(1);
        private readonly NetworkVariable<int> _currentActorIndex = new NetworkVariable<int>(0);

        // สถานะ Interaction ปัจจุบัน (UI ของสถานที่ไหนเปิดอยู่)
        public NetworkVariable<FixedString64Bytes> currentInteractionLocationId = new NetworkVariable<FixedString64Bytes>(new FixedString64Bytes(""));
        public NetworkVariable<bool> isInteractionOpen = new NetworkVariable<bool>(false);
        public NetworkVariable<int> currentTime = new NetworkVariable<int>(0);
        
        public event Action<int> OnRoundChanged;
        public event Action<int> OnCycleChanged;
        public event Action<bool> OnGameStateChanged;

        // ตัวแปรกั้นการเบิ้ลคำสั่งจบเทิร์น (กันการเล่น 2 ตาติด)
        private bool _isProcessingEndTurn = false;
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
                
                NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
            }
        }
        
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
        }
        
        // ล้างค่าตัวเองทิ้งเมื่อปิดเกม ป้องกัน Error ข้าม Scene
        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }
        
        // ปิด Network ตอนปิดเกม
        private void OnApplicationQuit()
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
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
            NetworkManager.Singleton.SceneManager.LoadScene("Gameplay", UnityEngine.SceneManagement.LoadSceneMode.Single);
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
                activeActorNetworkId.Value = _playerActors[0].NetworkObjectId;
            if (IsServer)
            {
                currentObjective.Value = (GameObjective)UnityEngine.Random.Range(0, 4);
                Debug.Log($"[Objective] เป้าหมายของเกมนี้คือ: {currentObjective.Value}");
            }
        }
        
        /// <summary>
        /// จบเทิร์นปัจจุบันและไปผู้เล่นคนถัดไป
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestEndTurnRpc()
        {
            // ดักจับกันเบิ้ลเทิร์น
            if (!IsServer || _isProcessingEndTurn || !isGameStartedState.Value) return;
            
            // เช็ค Win/Fail ของคนที่เพิ่งจบเทิร์น
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(activeActorNetworkId.Value, out var netObj))
            {
                if (netObj.TryGetComponent<PlayerStatus>(out var status))
                {
                    StatusManager.Instance.CheckFailConditions(status);
                    StatusManager.Instance.CheckWinConditions(status);
                }
            }
            
            _isProcessingEndTurn = true; // เปิดกั้นคำสั่งซ้ำ

            // บังคับปิด Panel และล้าง LocationId
            currentInteractionLocationId.Value = new FixedString64Bytes("");
            isInteractionOpen.Value = false;
            
            if (_playerActors.Count <= 1) 
            {
                _isProcessingEndTurn = false;
                return;
            }

            int nextIndex = (_currentActorIndex.Value + 1) % _playerActors.Count;
            
            // Round จะเพิ่มขึ้น ก็ต่อเมื่อวนกลับมาที่ผู้เล่นคนแรกเท่านั้น
            if (nextIndex == 0)
            {
                currentRound.Value++;

                if (currentRound.Value > 16)
                {
                    NotifyEndGameRpc(0, false, "หมดเวลา! (เล่นครบ 16 ตาแล้ว)");
                    return;
                }

                // 1. "แจ้งบิล" ค่าเช่า (เข้า Round 4, 8, 12, 16)
                if (currentRound.Value % 4 == 0)
                {
                    currentCycle.Value++; // ขึ้นเดือนใหม่
                    if (EconomyManager.Instance != null)
                    {
                        EconomyManager.Instance.GenerateRentBill_ServerOnly();
                    }
                }
                // 2. "ทำโทษ" คนค้างจ่าย (เข้า Round 5, 9, 13)
                else if ((currentRound.Value - 1) % 4 == 0 && currentRound.Value > 1)
                {
                    if (EconomyManager.Instance != null)
                    {
                        EconomyManager.Instance.ApplyRentPenalty_ServerOnly();
                    }
                }
            }

            _currentActorIndex.Value = nextIndex;

            if (_playerActors.Count > 0)
            {
                NetworkObject nextActor = _playerActors[nextIndex];
                activeActorNetworkId.Value = nextActor.NetworkObjectId;
            }

            // ตั้งเวลาปลดกันเบิ้ล (0.5 วิ)
            StartCoroutine(ResetTurnLock());
        }

        private IEnumerator ResetTurnLock()
        {
            yield return new WaitForSeconds(0.5f);
            _isProcessingEndTurn = false;
        }
        
        private void HandleClientDisconnect(ulong clientId)
        {
            if (!IsServer || !isGameStartedState.Value) return;

            // หาตัวละครของคนที่เพิ่งออก
            NetworkObject disconnectedActor = null;
            for (int i = 0; i < _playerActors.Count; i++)
            {
                if (_playerActors[i].OwnerClientId == clientId)
                {
                    disconnectedActor = _playerActors[i];
                    break;
                }
            }

            if (disconnectedActor != null)
            {
                int disconnectedIndex = _playerActors.IndexOf(disconnectedActor);
                bool wasTheirTurn = (activeActorNetworkId.Value == disconnectedActor.NetworkObjectId);

                // เอาออกจากคิวเดิน
                _playerActors.RemoveAt(disconnectedIndex);
                Debug.Log($"[TurnManager] Player {clientId} ออกจากเกม ลบออกจากคิว (เหลือ {_playerActors.Count} คน)");

                // ถ้าเหลือคนเดียว หรือไม่เหลือคนเล่นแล้ว
                if (_playerActors.Count <= 1)
                {
                    Debug.Log("[TurnManager] เหลือผู้เล่นคนเดียว จบเกมอัตโนมัติ!");
                    isGameStartedState.Value = false;
                    
                    if (_playerActors.Count == 1)
                    {
                        StartCoroutine(AutoKickToMainMenuCoroutine(0f));
                    }
                    return; 
                }
                
                // ถ้าเป็นตาของคนที่เพิ่งออกพอดี ให้จัดคิวข้ามไปคนถัดไปเลย
                if (wasTheirTurn)
                {
                    // ถอย index กลับ 1 ก้าว เพื่อให้ฟังก์ชัน EndTurn ดันไปหาคนถัดไปได้ถูกต้อง
                    _currentActorIndex.Value = disconnectedIndex - 1; 
                    if (_currentActorIndex.Value < 0) _currentActorIndex.Value = _playerActors.Count - 1;
                    
                    _isProcessingEndTurn = false; // ปลดล็อกฉุกเฉิน
                    Debug.Log("[TurnManager] ข้ามเทิร์นอัตโนมัติ เนื่องจากคนเดินหลุด");
                    RequestEndTurnRpc();
                }
                else
                {
                    // ถ้าคนที่หลุดอยู่คิวก่อนหน้าคนที่กำลังเดินอยู่ ต้องปรับ Index ถอยหลังให้ตรงกับคิวปัจจุบัน
                    if (disconnectedIndex < _currentActorIndex.Value) _currentActorIndex.Value--;
                }
            }
        }
        
        /// <summary>
        /// แจ้งผลเกมจบไป Client
        /// </summary>
        [Rpc(SendTo.ClientsAndHost)]
        public void NotifyEndGameRpc(ulong playerId, bool isWin, string reason)
        {
            Debug.Log($"[Game Over] Player {playerId} {(isWin ? "ชนะ" : "แพ้")} - เหตุผล: {reason}");
            
            // ให้ Server เท่านั้นที่แก้ค่า NetworkVariable
            if (IsServer)
            {
                isGameStartedState.Value = false;
            }

            OnGameOverTriggered?.Invoke(isWin, reason);
        }
        
        private IEnumerator AutoKickToMainMenuCoroutine(float delay = 3f)
        {
            // หน่วงเวลาตามที่ส่งมา
            // ถ้า delay = 0 จะไม่รอ
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            
            // สั่งล้าง Network เหมือนที่ปุ่ม Exit to Menu ทำ
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                // รอ 1 เฟรม ให้มันดับสนิท
                yield return null;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.gameObject != null) 
                {
                    Destroy(NetworkManager.Singleton.gameObject);
                }
            }

            // ทำลาย Manager ทิ้ง
            GameObject gs = GameObject.Find("GameSystem");
            if (gs != null) Destroy(gs);

            GameObject lnm = GameObject.Find("LobbyNetworkManager");
            if (lnm != null) Destroy(lnm);

            // โหลดกลับหน้าแรก
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        #endregion
    }
}