using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI; // สำหรับ NavMesh
using UnityEngine.InputSystem; // สำหรับรับค่าเมาส์
using Unity.Cinemachine;
using Managers;

namespace Systems
{
     /// <summary>
     /// ควบคุมการเดินของตัวละครด้วย NavMeshAgent (Click Building -> Walk to EntryPoint)
     /// </summary>
     [RequireComponent(typeof(NavMeshAgent))]
     public class PlayerMovement : NetworkBehaviour
     {
        [Header("Interaction Settings")] 
        [SerializeField] private LayerMask interactableLayer; // Layer ของตึก
        [SerializeField] private LayerMask groundLayer; // Layer พื้น (เผื่ออยากให้เดินบนพื้นได้ที่วๆ)
        
        [Header("Movement Settings")]
        [SerializeField] private bool allowClickGround = false; // ปิดไว้ก่อน ตอนนี้ให้เดินไปที่ตึกเท่านั้น

        private NavMeshAgent _agent;
        private Camera _mainCamera;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _mainCamera = Camera.main;
            
            // Tweak NavMeshAgent สำหรับแก้ปัญหาเดินสไลด์
            // Acceleration สูงๆ จะทำให้เริ่มเดินและหยุดทันที ไม่ไถล
            _agent.acceleration = 5f; 
            _agent.angularSpeed = 360f; // หันทันที ไม่ตีวงกว้าง
            _agent.autoBraking = true;
        }

        public override void OnNetworkSpawn()
        {
            // ฟังก์ชันสำหรับสั่งให้กล้อง Cinemachine มาจับที่ตัวผู้เล่น
            if (IsOwner)
            {
                SetupCamera();
            }
        }

        private void SetupCamera()
        {
            if (!IsOwner) return; // ต้องมั่นใจว่ารันเฉพาะเจ้าของเครื่องเท่านั้น

            var vCam = FindFirstObjectByType<CinemachineCamera>();
            if (vCam != null)
            {
                vCam.Follow = this.transform; // ให้กล้องตามเฉพาะตัวเราเอง
            }
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;

            if (TurnManager.Instance == null || !TurnManager.Instance.IsGameStarted) return;

            ulong activeId = TurnManager.Instance.CurrentActivePlayerId;

            if (NetworkManager.Singleton.LocalClientId != activeId)
            {
                return; 
            }

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                HandleClickMovement();
            }
        }

        private void HandleClickMovement()
        {
            if (_mainCamera == null) return;
            
            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;

            // เช็คว่าคลิกโดนตึก (Interactable) มั้ย
            if (Physics.Raycast(ray, out hit, 100f, interactableLayer))
            {
                // ลองดึง Component InteractableLocation ออกมา
                if (hit.collider.TryGetComponent(out InteractableLocation location))
                {
                    // เดินไปที่จุด Entry Point ของตึกนั้น
                    Vector3 target = location.GetWalkTarget();
                    Debug.Log($"[Player] สั่งเดินไปที่: {location.GetLocationName()}");
                    RequestMoveServerRpc(target);
                    return; // จบ function ไม่ต้องเช็คพื้นต่อ
                }
            }

            // ถ้าไม่ได้คลิกตึก แล้วอนุญาตให้คลิกพื้นได้
            if (allowClickGround && Physics.Raycast(ray, out hit, 100f, groundLayer))
            {
                RequestMoveServerRpc(hit.point);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition)
        {
            // Server สั่ง NavMeshAgent ให้คำนวณเส้นทางและเดิน
            if (_agent.isOnNavMesh)
            {
                _agent.SetDestination(targetPosition);
            }
        }
     }
}