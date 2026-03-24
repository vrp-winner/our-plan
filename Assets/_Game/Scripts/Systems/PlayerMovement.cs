using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI; // สำหรับ NavMesh
using UnityEngine.InputSystem; // สำหรับรับค่าเมาส์
using Unity.Cinemachine;
using UnityEngine.EventSystems;
using Managers;

namespace Systems
{
    /// <summary>
    /// ควบคุมการเดินของตัวละครด้วย NavMeshAgent (Click Building -> Walk to EntryPoint)
    /// </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        #region ตั้งค่าการเดิน (Settings)
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f; // ความเร็วในการเดิน
        [SerializeField] private float rotationSpeed = 10f; // ความเร็วในการหมุนตัว

        [Header("Interaction Settings")]
        [SerializeField] private LayerMask interactableLayer; // Layer ของตึก
        [SerializeField] private LayerMask groundLayer; // Layer พื้น (เผื่ออยากให้เดินบนพื้นได้ที่วๆ)
        #endregion

        #region สถานะปัจจุบัน (State Variables)
        private bool _isMoving = false;
        private Vector3 _targetPosition;
        private string _targetLocationName;
        #endregion

        private Camera _mainCamera;

        #region Lifecycle (เริ่มเกม & อัปเดต)
        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) SetupCamera();
        }

        private void SetupCamera()
        {
            var vCam = FindFirstObjectByType<CinemachineCamera>();
            if (vCam != null) vCam.Follow = this.transform;
        }

        private void Update()
        {
            if (IsServer && _isMoving)
            {
                MoveTowardsTarget();
            }

            if (!IsOwner || !IsSpawned) return;
            if (TurnManager.Instance == null) return;
            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleClickMovement();
            }
        }
        #endregion

        #region คำสั่งคลิก (Client)
        private void HandleClickMovement()
        {
            if (_mainCamera == null || _isMoving) return;
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return; 
            }

            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
            {
                if (hit.collider.TryGetComponent(out InteractableLocation location))
                {
                    Vector3 target = location.GetWalkTarget();
                    target.y = transform.position.y; 

                    Debug.Log($"[Client] สั่งเดินไปที่: {location.GetLocationName()}");
                    RequestMoveServerRpc(target, location.gameObject.name);
                }
            }


        }
        #endregion

        #region ระบบบังคับตัวละคร (Server)
        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition, string locationName)
        {
            _targetPosition = targetPosition;
            _targetLocationName = locationName;
            _isMoving = true;

            // สั่งปิด UI ก่อนเริ่มเดิน
            if (TryGetComponent(out PlayerPointSystem pointSystem))
            {
                pointSystem.NotifyLocationExit();
            }
        }
        private void MoveTowardsTarget()
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
                    Debug.Log($"[Server] เดินถึง {_targetLocationName} แล้ว เด้ง UI!");
                    pointSystem.NotifyLocationEnter(_targetLocationName);
                }
            }
        }
        #endregion
    }
}