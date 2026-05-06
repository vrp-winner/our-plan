using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using Systems.Map;
using Unity.Collections;

namespace Managers
{
    /// <summary>
    /// ตัวจัดการการ Spawn ผู้เล่นใน Network
    /// ให้ผู้เล่น Spawn อยู่บน MapNode ที่กำหนด
    /// </summary>
    public class PlayerSpawnManager : SingletonNetwork<PlayerSpawnManager>
    {
        [Header("Spawning Setup")]
        [SerializeField] private GameObject playerPrefab;
        [Tooltip("ถ้าไม่ได้กำหนด จะหา Node ตัวแรกจาก MapGraph ให้อัตโนมัติ")]
        [SerializeField] private MapNode startSpawnNode;

        private readonly Dictionary<ulong, int> _playerSelectedAvatars = new Dictionary<ulong, int>();

        /// <summary>
        /// บันทึก Avatar ที่ผู้เล่นเลือก
        /// </summary>
        public void RegisterPlayerAvatar(ulong clientId, int avatarIndex)
        {
            _playerSelectedAvatars[clientId] = avatarIndex;
        }

        /// <summary>
        /// สร้างตัวละครตามจำนวนผู้เล่นที่กำหนดและวางไว้บน Node
        /// </summary>
        public List<NetworkObject> SpawnPlayers(int playerRequired)
        {
            List<NetworkObject> spawnedActors = new List<NetworkObject>();

            if (!IsServer) return spawnedActors;
            
            if (MapGraph.Instance != null)
            {
                // ใส่ ID ให้ตรงกับที่ตั้งไว้ใน MapNode (Apartment)
                startSpawnNode = MapGraph.Instance.GetNode("Node_Apartment"); 
            }

            var connectedClients = NetworkManager.Singleton.ConnectedClientsIds.ToList();

            // STEP 1: ค้นหา Node เริ่มต้น หากยังไม่ได้ตั้งค่าไว้
            if (startSpawnNode == null && MapGraph.Instance != null)
            {
                startSpawnNode = MapGraph.Instance.GetDefaultSpawnNode();
            }

            for (int i = 0; i < playerRequired; i++)
            {
                ulong ownerId = connectedClients[i];
                Vector3 spawnPos = (startSpawnNode != null) ? startSpawnNode.transform.position : Vector3.zero;

                // STEP 2: สร้างและ Spawn ตัวละคร
                GameObject playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
                netObj.SpawnWithOwnership(ownerId);

                // STEP 3: ผูกสถานะ CurrentNodeId ทันทีเมื่อเกิด เพื่อให้ระบบเดินรู้จักจุดเริ่มต้น
                if (playerInstance.TryGetComponent(out Systems.PlayerMovement movement) && startSpawnNode != null)
                {
                    movement.currentNodeId.Value = new FixedString64Bytes(startSpawnNode.NodeID);
                }

                // STEP 4: อัปเดต Status และ Avatar
                if (playerInstance.TryGetComponent(out Systems.PlayerStatus status))
                {
                    status.teamId.Value = i;
                    status.memberIndex.Value = 0;

                    if (_playerSelectedAvatars.TryGetValue(ownerId, out int avatarIdx))
                    {
                        status.avatarIndex.Value = avatarIdx;
                    }
                }

                spawnedActors.Add(netObj);
            }

            return spawnedActors;
        }

        [Rpc(SendTo.Server)]
        public void SubmitAvatarSelectionServerRpc(int avatarIndex, RpcParams rpcParams = default)
        {
            ulong senderId = rpcParams.Receive.SenderClientId;
            RegisterPlayerAvatar(senderId, avatarIndex);
        }
    }
}