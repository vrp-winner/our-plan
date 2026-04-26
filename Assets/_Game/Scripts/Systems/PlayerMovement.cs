using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System;
using System.Collections;
using System.Collections.Generic;
using Systems.Map;
using Domain;
using Managers;
using Unity.Netcode.Components;

namespace Systems
{
    /// <summary>
    /// รับผิดชอบเรื่องการเดินตาม Path บนแกน 2D ล้วน
    /// เพิ่ม Server Validation Guard ครบถ้วน เพื่อป้องกัน Exploit
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("ความเร็วในการเดิน 2D (หน่วยต่อวินาที)")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("State (Network Synced)")]
        [Tooltip("สถานะล็อคการเดิน")]
        public NetworkVariable<bool> IsMoving = new NetworkVariable<bool>(false);
        [Tooltip("ID ของ Node ปัจจุบัน")]
        public NetworkVariable<FixedString64Bytes> CurrentNodeId = new NetworkVariable<FixedString64Bytes>("");

        public event Action OnMovementStart;
        public event Action<MapNode> OnReachNode;
        public event Action OnMovementComplete;

        /// <summary>
        /// คำสั่ง Request ที่จะถูกตรวจสอบโดย Server 100%
        /// </summary>
        [Rpc(SendTo.Server)]
        public void RequestMoveServerRpc(string targetNodeId)
        {
            // STEP 1: RPC Spam Protection (เช็คสถานะและ Node เป้าหมาย)
            if (IsMoving.Value) return;
            if (targetNodeId == CurrentNodeId.Value.ToString()) return;

            // STEP 2: Server Turn Validation (กัน Client โกงเทิร์น)
            if (TurnManager.Instance == null || TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId)
            {
                Debug.LogWarning($"[Security] ปฏิเสธการเดิน: ไม่ใช่เทิร์นของ NetworkObject {this.NetworkObjectId}");
                return;
            }

            // STEP 3: ดึงข้อมูล Node
            MapNode startNode = MapGraph.Instance.GetNode(CurrentNodeId.Value.ToString());
            MapNode endNode = MapGraph.Instance.GetNode(targetNodeId);

            if (startNode == null || endNode == null || !endNode.IsLocation) return;

            // STEP 4: ค้นหา Path
            List<MapNode> path = PathfindingSystem.FindPath(startNode, endNode);

            // STEP 5: Path Validation Guard (กัน Edge Case)
            if (path == null || path.Count <= 1)
            {
                Debug.LogWarning("[Movement] ปฏิเสธการเดิน: ไม่พบเส้นทาง หรือเส้นทางสั้นเกินไป");
                return;
            }

            // STEP 6: เริ่มกระบวนการเดิน
            int distanceCost = path.Count - 1; 
            IsMoving.Value = true;
            StartCoroutine(MoveAlongPathCoroutine(path));
        }

        /// <summary>
        /// เดินทีละ Step ด้วย 2D Normalize ป้องกันความผิดพลาดจากการคำนวณ
        /// </summary>
        private IEnumerator MoveAlongPathCoroutine(List<MapNode> path)
        {
            OnMovementStart?.Invoke();

            for (int i = 1; i < path.Count; i++)
            {
                MapNode targetNode = path[i];

                // STEP 1: Movement Safety Guard (กัน Coroutine พัง)
                if (targetNode == null)
                {
                    Debug.LogError("[Movement] Safety Guard ทำงาน: พบ Node ปลายทางถูกทำลายระหว่างเดิน");
                    IsMoving.Value = false;
                    yield break;
                }
                
                // STEP 2: Normalize Position (บังคับคำนวณเฉพาะ X, Y)
                Vector2 targetPos2D = new Vector2(targetNode.transform.position.x, targetNode.transform.position.y);

                while (true)
                {
                    Vector2 currentPos2D = new Vector2(transform.position.x, transform.position.y);
                    
                    // เช็คระยะห่างเฉพาะแกน 2D
                    if (Vector2.Distance(currentPos2D, targetPos2D) <= 0.01f)
                        break;

                    // เคลื่อนที่ด้วยแกน 2D
                    Vector2 newPos = Vector2.MoveTowards(currentPos2D, targetPos2D, moveSpeed * Time.deltaTime);
                    transform.position = new Vector3(newPos.x, newPos.y, 0f);
                    
                    yield return null;
                }

                // STEP 3: Snap ให้อยู่จุดศูนย์กลางเป๊ะๆ
                transform.position = new Vector3(targetPos2D.x, targetPos2D.y, 0f);
                CurrentNodeId.Value = new FixedString64Bytes(targetNode.NodeID);

                OnReachNode?.Invoke(targetNode);
            }

            IsMoving.Value = false;
            
            // ดึงข้อมูล Node สุดท้ายที่เราเดินมาถึง
            MapNode finalNode = path[path.Count - 1];

            // ถ้าจุดนี้เป็นสถานที่ (IsLocation) ให้เรียกใช้ระบบ UI
            if (finalNode.IsLocation)
            {
                if (TryGetComponent(out PlayerPointSystem pointSystem))
                {
                    // ส่ง LocationID ไปที่ PointSystem เพื่อสั่งเปิด Interaction Panel
                    pointSystem.NotifyLocationEnter(finalNode.LocationID);
                    Debug.Log($"[Movement] ถึงที่หมาย: {finalNode.LocationID} สั่งเปิด UI");
                }
            }
            
            OnMovementComplete?.Invoke();
        }
    }
}