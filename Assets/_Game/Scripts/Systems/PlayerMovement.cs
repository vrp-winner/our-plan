using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI; // สำหรับ NavMesh
using UnityEngine.InputSystem; // สำหรับรับค่าเมาส์
using Unity.Cinemachine;

namespace Systems
{
     /// <summary>
     /// ควบคุมการเดินของตัวละครด้วย NavMeshAgent (Point & Click)
     /// </summary>
     [RequireComponent(typeof(NavMeshAgent))]
     public class PlayerMovement : NetworkBehaviour
     {
        [Header("Settings")] [SerializeField]
        private LayerMask groundLayer; // กำหนดว่า Layer ตรงไหนคือพื้น (กันคลิกโดนกำแพง)

        private NavMeshAgent _agent;
        private Camera _mainCamera;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _mainCamera = Camera.main;
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
            // หา Cinemachine Camera ในฉาก
            var vCam = FindFirstObjectByType<CinemachineCamera>();

            if (vCam != null)
            {
                vCam.Follow = this.transform;
                Debug.Log($"[PlayerMovement] กล้องจับที่ตัวผู้เล่น {name} แล้ว!");
            }
            else
            {
                Debug.LogError("[PlayerMovement] หา CinemachineCamera ไม่เจอในฉาก!");
            }
        }

        private void Update()
        {
            // เช็คว่าเป็นตัวเองมั้ย (ห้ามบังคับตัวเพื่อน)
            if (!IsOwner) return;

            // เช็คว่ากดคลิกขวาหรือไม่ (Mouse Right Click)
            if (Mouse.current.rightButton.wasPressedThisFrame)
            {
                MoveToCursor();
            }
        }

        private void MoveToCursor()
        {
            if (_mainCamera == null) return; // กัน error ถ้าหากล้องไม่เจอ
            
            // ยิง Raycast จากหน้าจอไปที่โลก 3D
            Ray ray = _mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            // เช็คว่า Ray ชนพื้น (Ground Layer) มั้ย
            if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayer))
            {
                // สั่ง Server ให้เดินไปที่จุดที่คลิก
                RequestMoveServerRpc(hit.point);

                // Optional: อาจจะใส่ Effect ลูกศรตรงจุดที่คลิก เป็น aesthetic ได้นะ (ทำทีหลังได้ๆ)
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition)
        {
            // Server สั่ง NavMeshAgent ให้คำนวณเส้นทางและเดิน
            _agent.SetDestination(targetPosition);
        }
     }
}