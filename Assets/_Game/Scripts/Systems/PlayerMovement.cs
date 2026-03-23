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
            if (IsOwner)
            {
                SetupCamera();
            }
            if (!IsServer)
                if (_agent != null)
                {
                    _agent.enabled = false;
                }
        }

        private void SetupCamera()
        {
            if (!IsOwner) return;

            var vCam = FindFirstObjectByType<CinemachineCamera>();
            if (vCam != null)
            {
                vCam.Follow = this.transform;
            }
        }

        private void Update()
        {
            if (!IsOwner || !IsSpawned) return;

            if (TurnManager.Instance == null || !TurnManager.Instance.IsGameStarted) return;

            if (TurnManager.Instance.ActiveActorNetworkId.Value != this.NetworkObjectId) return;

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

            if (Physics.Raycast(ray, out hit, 100f, interactableLayer))
            {
                if (hit.collider.TryGetComponent(out InteractableLocation location))
                {
                    Vector3 target = location.GetWalkTarget();
                    Debug.Log($"[Player] สั่งเดินไปที่: {location.GetLocationName()}");
                    RequestMoveServerRpc(target);
                    return;
                }
            }


        }

        [Rpc(SendTo.Server)]
        private void RequestMoveServerRpc(Vector3 targetPosition)
        {
            // 1. เมคชัวร์ว่า Agent บน Server เปิดอยู่แน่นอน
            if (!_agent.enabled) _agent.enabled = true;

            // 2. ถ้าหลุดจาก NavMesh ให้ใช้สูตร "ปิด-ย้าย-เปิด"
            if (!_agent.isOnNavMesh)
            {
                // ขยายวงค้นหาพื้นเป็น 5 เมตร เผื่อเกิดลอยสูงไปหน่อย
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    _agent.enabled = false;            // ปิด Agent ก่อนเพื่อตัดการเชื่อมต่อกับระบบชั่วคราว
                    transform.position = hit.position; // จับย้ายพิกัดลงพื้น NavMesh เป๊ะๆ
                    _agent.enabled = true;             // เปิด Agent ใหม่อีกครั้ง ระบบจะบังคับให้มันเกาะพื้น 100%

                    Debug.Log("[Server] รีเซ็ต Agent ลงพื้นด้วยสูตร ปิด-เปิด สำเร็จ!");
                }
                else
                {
                    Debug.LogError("[Server] หาพื้น NavMesh ไม่เจอเลย! บริเวณที่ตัวละครอยู่ตรงนี้อาจจะยังไม่ได้ Bake พื้น");
                    return;
                }
            }

            // 3. เมื่อมั่นใจว่าเกาะพื้นแล้ว ก็สั่งเดินได้เลย
            if (_agent.isOnNavMesh)
            {
                _agent.SetDestination(targetPosition);
            }
        }
    }
}