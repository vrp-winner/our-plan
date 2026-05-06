using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using static Systems.PlayerStatus;
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
            if (!IsServer) return;

            // บังคับปิด Panel และล้าง LocationId
            currentInteractionLocationId.Value = new FixedString64Bytes("");
            isInteractionOpen.Value = false;
            
            if (_playerActors.Count <= 1) return;

            int nextIndex = (_currentActorIndex.Value + 1) % _playerActors.Count;
            
            // Round จะเพิ่มขึ้น ก็ต่อเมื่อวนกลับมาที่ผู้เล่นคนแรกเท่านั้น
            if (nextIndex == 0)
            {
                currentRound.Value++; 

                // ทุกๆ 4 Rounds (เช่น จบ 4 ขึ้น 5, จบ 8 ขึ้น 9) ให้เปลี่ยนเดือน และเก็บค่าเช่า
                if ((currentRound.Value - 1) % 4 == 0)
                {
                    currentCycle.Value++; // ขึ้นเดือนใหม่
                    if(EconomyManager.Instance != null)
                        EconomyManager.Instance.ApplyRentChargeServerRpc();
                }
            }

            _currentActorIndex.Value = nextIndex;

            if (_playerActors.Count > 0)
            {
                NetworkObject nextActor = _playerActors[nextIndex];
                activeActorNetworkId.Value = nextActor.NetworkObjectId;
            }
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
                        NotifyEndGameRpc(_playerActors[0].OwnerClientId, true, "ผู้เล่นอื่นออกห้องหมดแล้ว คุณคือผู้ชนะ!");
                        StartCoroutine(AutoKickToMainMenuCoroutine());
                    }
                    return; 
                }
                
                // ถ้าเป็นตาของคนที่เพิ่งออกพอดี ให้จัดคิวข้ามไปคนถัดไปเลย
                if (wasTheirTurn)
                {
                    // ถอย index กลับ 1 ก้าว เพื่อให้ฟังก์ชัน EndTurn ดันไปหาคนถัดไปได้ถูกต้อง
                    _currentActorIndex.Value = disconnectedIndex - 1; 
                    if (_currentActorIndex.Value < 0) _currentActorIndex.Value = _playerActors.Count - 1;
                    
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
            
            // หมายเหตุ: ตรงจุดนี้ในอนาคตอาจจะต้องเรียก UI "Game Over" ขึ้นมาแสดงผล
            // แล้วให้ผู้เล่นกดปุ่มเพื่อกลับหน้า Main Menu ด้วยตัวเอง (แทนที่จะเด้งออกทันที)
        }
        
        private IEnumerator AutoKickToMainMenuCoroutine()
        {
            // โชว์หน้าเกมค้างไว้ 3 วินาที ให้เห็นข้อความว่าชนะ หรือรู้ตัวว่าเพื่อนหลุดหมดแล้ว
            yield return new WaitForSeconds(3f);
            
            // สั่งล้าง Network เหมือนที่ปุ่ม Exit to Menu ทำ
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                yield return new WaitWhile(() => NetworkManager.Singleton != null && NetworkManager.Singleton.ShutdownInProgress);
                if (NetworkManager.Singleton != null) Destroy(NetworkManager.Singleton.gameObject);
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