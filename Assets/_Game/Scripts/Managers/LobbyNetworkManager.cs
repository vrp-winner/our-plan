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
        
        public NetworkList<ulong> AvatarSelections = new NetworkList<ulong>();
        public NetworkList<ulong> ConnectedPlayers = new NetworkList<ulong>();

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
        }
        
        // ล้างค่าทั้งหมดก่อนตาย ป้องกัน NullReferenceException ในตาต่อไป
        public override void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            
            // ล้างข้อมูล List ก่อนตาย เพื่อความชัวร์ว่าไม่ค้างใน Network
            if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                ConnectedPlayers.Clear();
                AvatarSelections.Clear();
            }
            
            base.OnDestroy();
        }

        private async void Start()
        {
            try 
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Lobby] เริ่มระบบ Unity Services ไม่สำเร็จ: {e.Message}");
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

                // ย้าย StartHost() ขึ้นมา เพื่อให้ Network เตรียมพร้อม
                // จากนั้นค่อยเซ็ตค่า NetworkVariable
                bool isHostStarted = NetworkManager.Singleton.StartHost();
                if (isHostStarted)
                {
                    MaxPlayers.Value = maxPlayers;
                    CountdownTimer.Value = -1; 
                }

                return isHostStarted;
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
                // บังคับให้ Invite Code เป็นตัวพิมพ์ใหญ่ทั้งหมด
                CurrentJoinCode = joinCode.ToUpper();
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

                // ระบบ Auto-Assign Avatar (จับยัดตัวละครให้ผู้เล่นที่เลือกไม่ทัน)
                foreach (ulong clientId in ConnectedPlayers)
                {
                    if (GetSelectedAvatarIndexForClient(clientId) == -1) // ถ้าหาไม่เจอว่าเลือกตัวไหน
                    {
                        for (int i = 0; i < AvatarSelections.Count; i++) // หาช่องที่ยังว่างอยู่
                        {
                            if (AvatarSelections[i] == ulong.MaxValue)
                            {
                                AvatarSelections[i] = clientId; // ยัดตัวละครนี้ให้เลย
                                break;
                            }
                        }
                    }
                }

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