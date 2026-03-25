using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.InputSystem; // สำหรับรับค่าเมาส์
using Unity.Cinemachine;
using UnityEngine.EventSystems;
using Managers;
using Domain;

namespace Systems
{
    /// <summary>
    /// ควบคุมการเดินของตัวละครบน Server และรับ Input จาก Client
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerMovement : NetworkBehaviour
    {
        #region Settings (ตั้งค่าการเดิน)
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f; // ความเร็วในการเดิน
        [SerializeField] private float rotationSpeed = 10f; // ความเร็วในการหมุนตัว

        [Header("Interaction Settings")]
        [SerializeField] private LayerMask interactableLayer; // Layer ของตึก
        #endregion

        #region State Variables (สถานะปัจจุบัน)
        private bool _isMoving = false;
        private Vector3 _targetPosition;
        private string _targetLocationId;
        private Camera _mainCamera;
        private ulong _lastOwnerId = ulong.MaxValue;
        #endregion

        #region Lifecycle (เริ่มเกม & อัปเดต)
        /// <summary>
        /// เตรียม Reference เริ่มต้น
        /// </summary>
        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        /// <summary>
        /// ทำงานเมื่อ Spawn ครั้งแรก
        /// </summary>
        public override void OnNetworkSpawn()
        {
            _lastOwnerId = OwnerClientId;
            if (IsOwner) SetupCamera();
        }

        /// <summary>
        /// Update ทุกเฟรม ครอบคลุมทั้งฝั่ง Server และ Client
        /// </summary>
        private void Update()
        {
            if (!IsSpawned) return;
            
            // STEP 1: [CRITICAL FIX] ตรวจสอบ Ownership Change เพื่ออัปเดตกล้อง
            if (_lastOwnerId != OwnerClientId)
            {
                _lastOwnerId = OwnerClientId;

                if (IsOwner)
                {
                    SetupCamera();
                    Debug.Log($"[Camera] Ownership changed! Queuing rebind for Actor {NetworkObjectId}");
                }
            }
            
            // STEP 2: Server Movement Logic
            if (IsServer && _isMoving) ProcessMovement();

            // STEP 3: Input Guard (ข้ามหากไม่ใช่ Owner หรือ Active Actor)
            if (!IsOwner) return;
            if (TurnManager.Instance == null) return;
            if (TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId) return;

            // STEP 4: Client Input
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleClickMovement();
            }
        }
        #endregion
        
        #region Setup
        /// <summary>
        /// สั่งให้ Cinemachine Camera ติดตามตัวละคร
        /// </summary>
        private void SetupCamera()
        {
            var vCam = FindFirstObjectByType<CinemachineCamera>();
            if (vCam != null) 
            {
                vCam.Follow = this.transform;
            }
        }
        #endregion

        #region Client Input Logic (คำสั่งคลิก)
        /// <summary>
        /// ยิง Raycast ตรวจหา target และส่ง move command
        /// </summary>
        private void HandleClickMovement()
        {
            if (_mainCamera == null || _isMoving) return;
            if (EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
            {
                if (hit.collider.TryGetComponent(out InteractableLocation location))
                {
                    Vector3 target = location.GetWalkTarget();
                    
                    // STEP 1: Validate Location ก่อนส่ง RPC
                    if (location.Config == null || string.IsNullOrEmpty(location.Config.LocationId)) return;

                    // STEP 2: ส่ง Request ไปยัง Server ด้วย LocationId
                    Debug.Log($"[Client] สั่งเดินไปที่: {location.GetLocationName()}");
                    RequestMoveServerRpc(target, location.Config.LocationId);
                }
            }
        }
        #endregion

        #region Server Movement Logic (ระบบบังคับตัวละคร)
        /// <summary>
        /// RPC Entry Point: รับ movement request จาก Owner Client
        /// </summary>
        /// <param name="targetPosition">Target position (ยังไม่ validated)</param>
        /// <param name="locationId">Location ID จาก Config</param>
        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition, string locationId)
        {
            // STEP 1: ตรวจสอบว่าเป็น Server และ Active Actor
            if (!IsServer) return;
            if (TurnManager.Instance.activeActorNetworkId.Value != this.NetworkObjectId) return;
            
            // STEP 2: ตรวจสอบ target position
            bool isValid = GameActionProcessor.ValidateMovementSetup(
                transform.position, 
                targetPosition, 
                out Vector3 validatedTarget
            );

            // STEP 3: เริ่มเดินไปยัง target
            if (isValid)
            {
                _targetPosition = validatedTarget;
                _targetLocationId = locationId;
                _isMoving = true;

                if (TryGetComponent(out PlayerPointSystem pointSystem))
                {
                    // แจ้งว่า player ออกจาก location ก่อนหน้า
                    pointSystem.NotifyLocationExit();
                }
            }
        }
        
        /// <summary>
        /// Core Logic: Actor Movement (ทำงานบน Server)
        /// </summary>
        private void ProcessMovement()
        {
            // STEP 1: Move towards target
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, moveSpeed * Time.deltaTime);

            // STEP 2: หมุนตัวไปทาง movement direction
            Vector3 direction = (_targetPosition - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // STEP 3: ตรวจสอบว่าถึง target หรือยัง
            if (Vector3.Distance(transform.position, _targetPosition) < 0.05f)
            {
                transform.position = _targetPosition; 
                _isMoving = false;

                if (TryGetComponent(out PlayerPointSystem pointSystem))
                {
                    // แจ้งว่า player ถึง location แล้ว 
                    Debug.Log($"[Server] เดินถึง {_targetLocationId} แล้ว เด้ง UI!");
                    pointSystem.NotifyLocationEnter(_targetLocationId);
                }
            }
        }
        #endregion
    }
}