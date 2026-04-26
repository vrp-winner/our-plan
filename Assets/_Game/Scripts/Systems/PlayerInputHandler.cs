using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Systems.Map;
using Managers;

namespace Systems
{
    /// <summary>
    /// จัดการ Input ของผู้เล่นแยกออกมาต่างหาก
    /// </summary>
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerInputHandler : NetworkBehaviour
    {
        [Header("Input Settings")]
        [Tooltip("Layer สำหรับดักจับการคลิกที่ Node ด้วย 2D Physics")]
        [SerializeField] private LayerMask nodeLayer;

        [Header("Camera Reference")]
        [Tooltip("อ้างอิงถึงกล้องที่ใช้แสดงผล (บังคับลากใส่ใน Inspector)")]
        [SerializeField] private Camera cameraRef;

        private PlayerMovement _playerMovement;

        /// <summary>
        /// เตรียม Reference เริ่มต้น
        /// </summary>
        private void Awake()
        {
            _playerMovement = GetComponent<PlayerMovement>();
            
            if (cameraRef == null)
            {
                cameraRef = Camera.main;
            }
            
            if (cameraRef == null)
            {
                Debug.LogError("[Input] ยังไม่ได้ลาก Camera ใส่ใน PlayerInputHandler!");
            }
        }

        /// <summary>
        /// ตรวจจับ Input (รันเฉพาะ Client เจ้าของ)
        /// </summary>
        private void Update()
        {
            // STEP 1: ตรวจสอบสิทธิ์และกล้อง
            if (!IsSpawned || !IsOwner || cameraRef == null) return;

            // STEP 2: ตรวจสอบเทิร์น
            bool isMyTurn = (TurnManager.Instance != null && TurnManager.Instance.activeActorNetworkId.Value == this.NetworkObjectId);
            if (!isMyTurn) return;

            // STEP 3: ป้องกันการกดซ้อนระหว่างเดิน
            if (_playerMovement.IsMoving.Value) return;

            // STEP 4: ตรวจจับคลิก
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                // ถ้าอยู่บน Interaction Panel ให้ return ออกไปเลย ห้ามเดิน
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    return; 
                }
                
                Handle2DClickMovement();
            }
        }

        /// <summary>
        /// แปลงพิกัด 2D และส่งคำสั่งเดิน
        /// </summary>
        private void Handle2DClickMovement()
        {
            // STEP 1: แปลง Screen -> World ผ่าน Camera ที่ Reference ไว้
            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Vector2 worldPoint = cameraRef.ScreenToWorldPoint(mousePosition);

            // STEP 2: เช็ค 2D Overlap
            Collider2D hitCollider = Physics2D.OverlapPoint(worldPoint, nodeLayer);
            if (hitCollider != null)
            {
                MapNode targetNode = null;

                // กรณีที่ 1: ผู้เล่นคลิกโดนวงกลม MapNode บนถนนตรงๆ
                if (hitCollider.TryGetComponent(out MapNode node))
                {
                    targetNode = node;
                }
                // กรณีที่ 2: ผู้เล่นคลิกโดนตัวตึก (ผ่าน BuildingClickTarget)
                else if (hitCollider.TryGetComponent(out BuildingClickTarget building))
                {
                    targetNode = building.linkedNode;
                }

                // ถ้าเจอเป้าหมาย และเป้าหมายนั้นเป็นสถานที่ (IsLocation) ให้เดิน!
                if (targetNode != null && targetNode.IsLocation)
                {
                    _playerMovement.RequestMoveServerRpc(targetNode.NodeID);
                }
            }
        }
    }
}