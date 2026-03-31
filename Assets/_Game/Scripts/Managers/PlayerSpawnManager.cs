using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

namespace Managers
{
    /// <summary>
    /// ตัวจัดการการ Spawn ผู้เล่นใน Network
    /// </summary>
    public class PlayerSpawnManager : SingletonNetwork<PlayerSpawnManager>
    {
        [Header("Spawning Setup")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Transform centerSpawnPoint;

        private readonly Dictionary<ulong, int> _playerSelectedAvatars = new Dictionary<ulong, int>();

        /// <summary>
        /// บันทึก Avatar ที่ผู้เล่นเลือก
        /// </summary>
        /// <param name="clientId">ID ของผู้เล่น</param>
        /// <param name="avatarIndex">Index ของ Avatar</param>
        public void RegisterPlayerAvatar(ulong clientId, int avatarIndex)
        {
            // STEP 1: บันทึกข้อมูล Avatar ที่ Client เลือกเข้าสู่ Dictionary
            _playerSelectedAvatars[clientId] = avatarIndex;
        }

        /// <summary>
        /// สร้างตัวละครตามจำนวนผู้เล่นที่กำหนด (ActorCount = playerRequired)
        /// </summary>
        /// <param name="playerRequired">จำนวนผู้เล่นที่ต้องการในรอบนี้</param>
        /// <returns>ตัวละครที่ Spawn แล้ว</returns>
        public List<NetworkObject> SpawnPlayers(int playerRequired)
        {
            List<NetworkObject> spawnedActors = new List<NetworkObject>();

            // STEP 1: ตรวจสอบสิทธิ์ฝั่ง Server
            if (!IsServer) return spawnedActors;

            // STEP 2: เตรียมข้อมูล Client ที่เชื่อมต่อและจุดเกิด
            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds.ToList();
            if (centerSpawnPoint == null)
            {
                GameObject sp = GameObject.FindWithTag("SpawnPoint");
                if (sp != null) centerSpawnPoint = sp.transform;
            }

            // STEP 3: วนลูปสร้างตัวละครตามจำนวน playerRequired (1 ตัวต่อ 1 Client)
            for (int i = 0; i < playerRequired; i++)
            {
                ulong ownerId = connectedClients[i];
                Vector3 spawnPos = (centerSpawnPoint != null) ? centerSpawnPoint.position : Vector3.zero;

                // STEP 4: Instantiate และ Spawn ด้วย Ownership ทันที
                GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
                netObj.SpawnWithOwnership(ownerId);

                // STEP 5: ตั้งค่าสถานะเริ่มต้นให้ Player
                if (playerInstance.TryGetComponent(out Systems.PlayerStatus status))
                {
                    status.teamId.Value = i; 
                    status.memberIndex.Value = 0;

                    if (_playerSelectedAvatars.TryGetValue(ownerId, out int avatarIdx))
                    {
                        status.avatarIndex.Value = avatarIdx;
                    }
                }

                // STEP 6: เก็บลง List เพื่อส่งกลับ
                spawnedActors.Add(netObj);
            }

            return spawnedActors;
        }

        /// <summary>
        /// RPC สำหรับส่ง Avatar ที่เลือกจาก Client ไป Server
        /// </summary>
        /// <param name="avatarIndex">Index ของ Avatar</param>
        /// <param name="rpcParams">ข้อมูลผู้ส่ง RPC</param>
        [Rpc(SendTo.Server)]
        public void SubmitAvatarSelectionServerRpc(int avatarIndex, RpcParams rpcParams = default)
        {
            // STEP 1: ดึง ID ของผู้ส่งและลงทะเบียนไว้
            ulong senderId = rpcParams.Receive.SenderClientId;
            RegisterPlayerAvatar(senderId, avatarIndex);
        }
    }
}