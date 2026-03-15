using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

namespace Managers
{
    public class PlayerSpawnManager : SingletonNetwork<PlayerSpawnManager>
    {
        [Header("Spawning Setup")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform centerSpawnPoint;
        [SerializeField] private int totalActors = 4;

        // สำหรับเก็บข้อมูลว่าใครเลือก Avatar ตัวไหนมา
        private Dictionary<ulong, int> _playerSelectedAvatars = new Dictionary<ulong, int>();

        public void RegisterPlayerAvatar(ulong clientId, int avatarIndex)
        {
            _playerSelectedAvatars[clientId] = avatarIndex;
        }

        // ฟังก์ชันนี้จะถูกเรียกโดย TurnManager เมื่อโหลดฉากเสร็จ
        public List<NetworkObject> SpawnPlayers(int playerRequired)
        {
            List<NetworkObject> spawnedActors = new List<NetworkObject>();

            if (!IsServer) return spawnedActors;

            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds.ToList();

            if (centerSpawnPoint == null)
            {
                GameObject sp = GameObject.FindWithTag("SpawnPoint");
                if (sp != null) centerSpawnPoint = sp.transform;
            }

            for (int i = 0; i < totalActors; i++)
            {
                // แบ่งทีม: ถ้าใช้ 2 คนเล่น ก็แบ่งคนละ 2 ตัวละคร, ถ้า 4 คน ก็คนละ 1 ตัวละคร
                ulong ownerId = (playerRequired == 2) ? connectedClients[i < 2 ? 0 : 1] : connectedClients[i];
                Vector3 spawnPos = (centerSpawnPoint != null) ? centerSpawnPoint.position : Vector3.zero;

                // เสกตัวละครและมอบสิทธิ์ (Ownership)
                GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
                netObj.SpawnWithOwnership(ownerId);

                // ตั้งค่า Status เบื้องต้น (ทีม, ลำดับ, รูปหน้า)
                if (playerInstance.TryGetComponent(out Systems.PlayerStatus status))
                {
                    status.TeamId.Value = (i < 2) ? 0 : 1;
                    status.MemberIndex.Value = (i % 2);

                    if (_playerSelectedAvatars.TryGetValue(ownerId, out int avatarIdx))
                        status.AvatarIndex.Value = avatarIdx;
                }

                spawnedActors.Add(netObj);
            }

            return spawnedActors;
        }
        [Rpc(SendTo.Server)]
        public void SubmitAvatarSelectionServerRpc(int avatarIndex, RpcParams rpcParams = default)
        {
            // rpcParams จะรู้โดยอัตโนมัติว่าใคร (ClientId อะไร) เป็นคนส่งคำสั่งนี้มา
            ulong senderId = rpcParams.Receive.SenderClientId;
            RegisterPlayerAvatar(senderId, avatarIndex);

            Debug.Log($"[SpawnManager] ผู้เล่น {senderId} เลือก Avatar เบอร์ {avatarIndex}");
        }
    }
    
}