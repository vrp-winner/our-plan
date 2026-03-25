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
        #endregion

        #region Lifecycle (เริ่มเกม & อัปเดต)
        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) SetupCamera();
        }

        private void Update()
        {
            // STEP 1: Server Logic
            if (IsServer && _isMoving) ProcessMovement();

            // STEP 2: Input Guard
            if (!IsSpawned || !IsOwner) return;
            if (TurnManager.Instance == null) return;
            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;

            // STEP 3: Client Input
            if (Mouse.current.leftButton.wasPressedThisFrame) HandleClickMovement();
        }
        #endregion
        
        #region Setup
        private void SetupCamera()
        {
            var vCam = FindFirstObjectByType<CinemachineCamera>();
            if (vCam != null) vCam.Follow = this.transform;
        }
        #endregion

        #region Client Input Logic (คำสั่งคลิก)
        /// <summary>
        /// ยิง Raycast เพื่อหาเป้าหมายและส่งคำสั่งเดิน
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
                    
                    // STEP 1: ตรวจสอบว่า Location Config ถูกต้องก่อนส่ง
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
        /// RPC Entry Point: รับคำสั่งเริ่มเดินจาก Owner Client
        /// </summary>
        /// <param name="targetPosition">พิกัดเป้าหมายดิบ</param>
        /// <param name="locationId">รหัสสถานที่จาก Config</param>
        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition, string locationId)
        {
            // STEP 1: Guard Check
            if (!IsServer) return;
            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;
            
            // STEP 2: Domain Layer Validate
            bool isValid = GameActionProcessor.ValidateMovementSetup(
                transform.position, 
                targetPosition, 
                out Vector3 validatedTarget
            );

            // STEP 3: เริ่มเดิน
            if (isValid)
            {
                _targetPosition = validatedTarget;
                _targetLocationId = locationId;
                _isMoving = true;

                if (TryGetComponent(out PlayerPointSystem pointSystem))
                {
                    pointSystem.NotifyLocationExit();
                }
            }
        }
        
        private void ProcessMovement()
        {
            transform.position = Vector3.MoveTowards(transform.position, _targetPosition, moveSpeed * Time.deltaTime);

            Vector3 direction = (_targetPosition - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            if (Vector3.Distance(transform.position, _targetPosition) < 0.05f)
            {
                transform.position = _targetPosition; 
                _isMoving = false;

                if (TryGetComponent(out PlayerPointSystem pointSystem))
                {
                    Debug.Log($"[Server] เดินถึง {_targetLocationId} แล้ว เด้ง UI!");
                    pointSystem.NotifyLocationEnter(_targetLocationId);
                }
            }
        }
        #endregion
    }
}