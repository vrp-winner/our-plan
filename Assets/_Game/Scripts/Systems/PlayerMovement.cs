using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine.InputSystem; 
using UnityEngine.EventSystems;
using Managers;
using Domain;

namespace Systems
{
    /// <summary>
    /// ควบคุมการเดินของผู้เล่น
    /// รับ input จาก Client และให้ Server เป็นคนควบคุมตำแหน่ง
    /// </summary>
    [RequireComponent(typeof(NetworkTransform))]
    public class PlayerMovement : NetworkBehaviour
    {
        #region Settings
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f; 
        [SerializeField] private float rotationSpeed = 10f; 

        [Header("Interaction Settings")]
        [SerializeField] private LayerMask interactableLayer; 
        #endregion

        #region State Variables
        private bool _isMoving = false;
        private Vector3 _targetPosition;
        private string _targetLocationId; // เก็บ location เป้าหมายไว้ใช้ตอนสั่งเดิน
        private Camera _mainCamera;
        #endregion

        #region Lifecycle
        private void Awake()
        {
            _mainCamera = Camera.main;
        }

        private void Update()
        {
            if (!IsSpawned) return;
            
            if (IsServer && _isMoving) ProcessMovement();

            bool isMyTurn = (TurnManager.Instance != null && TurnManager.Instance.activeActorNetworkId.Value == this.NetworkObjectId);
            
            if (!IsOwner) return;
            if (!isMyTurn) return;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleClickMovement();
            }
        }
        #endregion

        #region Client Input Logic
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
                    
                    if (location.Config == null || string.IsNullOrEmpty(location.Config.LocationId)) return;

                    RequestMoveServerRpc(target, location.Config.LocationId);
                }
            }
        }
        #endregion

        #region Server Movement Logic
        /// <summary>
        /// Request ให้ Server สั่งเดินไปตำแหน่งเป้าหมาย
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition, string locationId)
        {
            if (!IsServer) return;
            
            bool isMyTurn = (TurnManager.Instance != null && TurnManager.Instance.activeActorNetworkId.Value == this.NetworkObjectId);
            if (!isMyTurn) 
            {
                Debug.LogWarning($"[Security] Actor {this.NetworkObjectId} พยายามเดินผิดเทิร์น! (REJECTED)");
                return;
            }
            
            bool isValid = GameActionProcessor.ValidateMovementSetup(
                transform.position, 
                targetPosition, 
                out Vector3 validatedTarget
            );

            if (isValid)
            {
                _targetPosition = validatedTarget;
                _targetLocationId = locationId;
                _isMoving = true;
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
            }
        }
        #endregion
    }
}