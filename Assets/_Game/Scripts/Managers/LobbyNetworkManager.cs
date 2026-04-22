using System.Collections;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Managers
{
    /// <summary>
    /// ตัวจัดการ Lobby
    /// ดูแลการเชื่อมต่อ Relay, การเลือก Avatar และระบบนับถอยหลังเริ่มเกม
    /// </summary>
    public class LobbyNetworkManager : NetworkBehaviour
    {
        public static LobbyNetworkManager Instance { get; private set; }
        
        public string CurrentJoinCode { get; private set; } = "";

        public NetworkVariable<int> MaxPlayers = new NetworkVariable<int>(2);
        public NetworkVariable<int> CountdownTimer = new NetworkVariable<int>(-1); 
        
        public NetworkList<ulong> AvatarSelections;
        public NetworkList<ulong> ConnectedPlayers;

        private Coroutine _countdownCoroutine;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // สร้าง List รอไว้ตั้งแต่ Awake ป้องกัน Error NullReference
            AvatarSelections = new NetworkList<ulong>();
            ConnectedPlayers = new NetworkList<ulong>();
        }

        private async void Start()
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ConnectedPlayers.Clear();

                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    ConnectedPlayers.Add(client.ClientId);
                }
                
                AvatarSelections.Clear();
                for (int i = 0; i < 4; i++)
                {
                    AvatarSelections.Add(ulong.MaxValue); 
                }

                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        #region Relay Connection Logic
        public async Task<bool> StartHostRelay(int maxPlayers)
        {
            try
            {
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                CurrentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetHostRelayData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                MaxPlayers.Value = maxPlayers;
                CountdownTimer.Value = -1; 

                return NetworkManager.Singleton.StartHost();
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[Relay] สร้างห้องไม่สำเร็จ: {e.Message}");
                return false;
            }
        }

        public async Task<bool> StartClientRelay(string joinCode)
        {
            try
            {
                CurrentJoinCode = joinCode;
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetClientRelayData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                return NetworkManager.Singleton.StartClient();
            }
            catch (RelayServiceException e)
            {
                Debug.LogError($"[Relay] เข้าร่วมห้องไม่สำเร็จ (โค้ดอาจจะผิด): {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ปิดห้อง 
        /// </summary>
        public void LeaveLobby()
        {
            CurrentJoinCode = "";
            
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            // ทำลายตัวเองทิ้ง ป้องกันการซ้อนกันตอนสร้างห้องใหม่
            Destroy(gameObject);

            // โหลด Scene หน้าเมนูใหม่ทั้งหมด เพื่อรีเซ็ตค่าทุกอย่าง
            SceneManager.LoadScene("MainMenu");
        }
        #endregion

        #region Avatar Selection (Toggle Logic)
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ToggleAvatarSelectionServerRpc(int avatarIndex, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;

            for (int i = 0; i < AvatarSelections.Count; i++)
            {
                if (AvatarSelections[i] == senderId && i != avatarIndex)
                {
                    AvatarSelections[i] = ulong.MaxValue; 
                }
            }

            if (AvatarSelections[avatarIndex] == ulong.MaxValue)
            {
                AvatarSelections[avatarIndex] = senderId;
            }
            else if (AvatarSelections[avatarIndex] == senderId)
            {
                AvatarSelections[avatarIndex] = ulong.MaxValue;
            }
        }

        public int GetSelectedAvatarIndexForClient(ulong clientId)
        {
            for (int i = 0; i < AvatarSelections.Count; i++)
            {
                if (AvatarSelections[i] == clientId)
                {
                    return i;
                }
            }
            return -1;
        }
        #endregion

        #region Countdown & Auto-Start Logic
        private void OnClientConnected(ulong clientId)
        {
            if (IsServer && !ConnectedPlayers.Contains(clientId))
            {
                ConnectedPlayers.Add(clientId);
            }
            CheckLobbyCondition();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsServer && ConnectedPlayers.Contains(clientId))
            {
                ConnectedPlayers.Remove(clientId);
            }
            
            for (int i = 0; i < AvatarSelections.Count; i++)
            {
                if (AvatarSelections[i] == clientId)
                {
                    AvatarSelections[i] = ulong.MaxValue;
                }
            }

            CheckLobbyCondition();
        }

        private void CheckLobbyCondition()
        {
            if (!IsServer) return;

            int currentPlayers = NetworkManager.Singleton.ConnectedClients.Count;

            if (currentPlayers >= MaxPlayers.Value)
            {
                if (CountdownTimer.Value == -1)
                {
                    if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
                    _countdownCoroutine = StartCoroutine(CountdownRoutine());
                }
            }
            else
            {
                if (CountdownTimer.Value != -1)
                {
                    if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
                    CountdownTimer.Value = -1; 
                    Debug.Log("[Lobby] มีผู้เล่นหลุด ยกเลิกการนับถอยหลัง กลับไปรอคนใหม่");
                }
            }
        }

        private IEnumerator CountdownRoutine()
        {
            CountdownTimer.Value = 10; 

            while (CountdownTimer.Value > 0)
            {
                yield return new WaitForSeconds(1f);
                CountdownTimer.Value--;
            }

            if (CountdownTimer.Value == 0)
            {
                Debug.Log("[Lobby] เวลาหมด เตรียมโหลด Scene เริ่มเกม...");

                // ส่งข้อมูลการเลือกตัวละครไปให้ PlayerSpawnManager (ใน GameSystem)
                if (PlayerSpawnManager.Instance != null)
                {
                    for (int i = 0; i < AvatarSelections.Count; i++)
                    {
                        ulong ownerId = AvatarSelections[i];
                        if (ownerId != ulong.MaxValue)
                        {
                            PlayerSpawnManager.Instance.RegisterPlayerAvatar(ownerId, i);
                        }
                    }
                    Debug.Log("[Lobby] ส่งข้อมูล Avatar ให้ PlayerSpawnManager สำเร็จ");
                }
                else
                {
                    Debug.LogWarning("[Lobby] ไม่พบ PlayerSpawnManager ลืมวาง GameSystem หรือเปล่า?");
                }
                
                // สั่ง TurnManager ให้เริ่มเกม
                if (TurnManager.Instance != null)
                {
                    // สำหรับบอกให้ TurnManager รู้ว่าต้องสร้างกี่ตัว
                    TurnManager.Instance.SetPlayerRequired(MaxPlayers.Value); 
                    TurnManager.Instance.StartGameServerRpc();
                }
                else
                {
                    Debug.LogError("[Lobby] หา TurnManager ไม่เจอ! เกมเริ่มไม่ได้");
                }
            }
        }
        #endregion
    }
}