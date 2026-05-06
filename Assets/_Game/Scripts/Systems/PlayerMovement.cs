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
    [RequireComponent(typeof(NetworkTransform), typeof(PlayerPointSystem))]
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("ความเร็วในการเดิน 2D (หน่วยต่อวินาที)")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float dragBackSpeedMultiplier = 3f; 

        [Header("State (Network Synced)")]
        [Tooltip("สถานะล็อคการเดิน")]
        public NetworkVariable<bool> isMoving = new NetworkVariable<bool>(false);
        [Tooltip("ID ของ Node ปัจจุบัน")]
        public NetworkVariable<FixedString64Bytes> currentNodeId = new NetworkVariable<FixedString64Bytes>("");

        public event Action OnMovementStart;
        public event Action<MapNode> OnReachNode;
        public event Action OnMovementComplete;

        private PlayerPointSystem _pointSystem;

        private void Awake()
        {
            _pointSystem = GetComponent<PlayerPointSystem>();
        }

        [Rpc(SendTo.Server)]
        public void RequestMoveServerRpc(string targetNodeId)
        {
            if (isMoving.Value) return;
            if (targetNodeId == currentNodeId.Value.ToString()) return;
            if (TurnManager.Instance == null || TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId) return;

            MapNode startNode = MapGraph.Instance.GetNode(currentNodeId.Value.ToString());
            MapNode endNode = MapGraph.Instance.GetNode(targetNodeId);

            if (startNode == null || endNode == null || !endNode.IsLocation) return;

            List<MapNode> path = PathfindingSystem.FindPath(startNode, endNode);
            if (path == null || path.Count <= 1) return;

            // ถ้ามีรถ ค่าเดินจะเป็น 1, ถ้ายังไม่มีจะเป็น 2
            int moveCost = (EconomyManager.Instance != null && EconomyManager.Instance.isCarBought.Value) ? 1 : 2; 
            
            bool isWalkOfShame = _pointSystem.PointsRemaining < moveCost;

            isMoving.Value = true;
            StartCoroutine(MoveAlongPathCoroutine(path, moveCost, isWalkOfShame));
        }

        private IEnumerator MoveAlongPathCoroutine(List<MapNode> path, int moveCost, bool isWalkOfShame)
        {
            OnMovementStart?.Invoke();

            yield return StartCoroutine(ExecutePath(path, moveSpeed));

            MapNode finalNode = path[path.Count - 1];
            currentNodeId.Value = new FixedString64Bytes(finalNode.NodeID);

            if (isWalkOfShame)
            {
                Debug.Log("[Movement] เวลาไม่พอ! โดนดึงกลับ Apartment อย่างไว!");
                MapNode apartmentNode = MapGraph.Instance.GetNode("LOC_Apartment");
                if (apartmentNode != null)
                {
                    List<MapNode> returnPath = PathfindingSystem.FindPath(finalNode, apartmentNode);
                    if (returnPath != null && returnPath.Count > 1)
                    {
                        yield return StartCoroutine(ExecutePath(returnPath, moveSpeed * dragBackSpeedMultiplier));
                        currentNodeId.Value = new FixedString64Bytes(apartmentNode.NodeID);
                        finalNode = apartmentNode; 
                    }
                }
                _pointSystem.ConsumeAllPoints();
            }
            else
            {
                _pointSystem.ConsumePoints(moveCost);
                if (finalNode.IsLocation && _pointSystem.PointsRemaining > 0)
                {
                    _pointSystem.NotifyLocationEnter(finalNode.LocationID);
                }
            }

            isMoving.Value = false;
            OnMovementComplete?.Invoke();
        }

        private IEnumerator ExecutePath(List<MapNode> path, float speed)
        {
            for (int i = 1; i < path.Count; i++)
            {
                MapNode targetNode = path[i];
                if (targetNode == null) yield break;
                
                Vector2 targetPos2D = new Vector2(targetNode.transform.position.x, targetNode.transform.position.y);

                while (true)
                {
                    Vector2 currentPos2D = new Vector2(transform.position.x, transform.position.y);
                    if (Vector2.Distance(currentPos2D, targetPos2D) <= 0.01f) break;

                    Vector2 newPos = Vector2.MoveTowards(currentPos2D, targetPos2D, speed * Time.deltaTime);
                    transform.position = new Vector3(newPos.x, newPos.y, 0f);
                    yield return null;
                }

                transform.position = new Vector3(targetPos2D.x, targetPos2D.y, 0f);
                OnReachNode?.Invoke(targetNode);
            }
        }
    }
}