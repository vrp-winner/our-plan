using UnityEngine;
using Unity.Netcode;
using Managers;

namespace Systems
{
    /// <summary>
    /// สคริปต์จัดการการแสดงผลตัวละคร (Hide/Show) ตาม Turn
    /// </summary>
    public class PlayerAvatarControl : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject visualModel; // โมเดลตัวละคร (Mesh)
        [SerializeField] private Collider col; // Collider ของตัวละคร

        // จุดเริ่มต้น (Spawn Point) - อาจจะดึงจาก Scene หรือ Config ในอนาคต
        private Vector3 _startPosition;
        private PlayerStatus _playerStatus;

        [Header("Character Models (4 Types)")]
        [Tooltip("ใส่โมเดล 4 แบบ เรียงตามลำดับ: ทีม1คน1, ทีม1คน2, ทีม2คน1, ทีม2คน2")]
        [SerializeField] private GameObject[] modelVariants;


        public override void OnNetworkSpawn()
        {
            _startPosition = transform.position; // จำจุดเกิดไว้

            if (_playerStatus != null)
            {
                _playerStatus.TeamId.OnValueChanged += (oldV, newV) => SetupModelVariant();
                _playerStatus.MemberIndex.OnValueChanged += (oldV, newV) => SetupModelVariant();
                SetupModelVariant(); // เรียกใช้เผื่อตั้งค่าเสร็จก่อน OnNetworkSpawn
            }

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged += CheckTurnVisibility;
                TurnManager.Instance.OnGameStateChanged += OnGameStateChanged;

                if (TurnManager.Instance.IsGameStarted)
                {
                    CheckTurnVisibility(TurnManager.Instance.CurrentActivePlayerId);
                }
                else
                {
                    UpdateVisuals(false);
                }
            }
        }
        private void SetupModelVariant()
        {
            if (modelVariants == null || modelVariants.Length == 0) return;

            int targetIndex = _playerStatus.AvatarIndex.Value;

            for (int i = 0; i < modelVariants.Length; i++)
            {
                if (modelVariants[i] != null)
                {
                    modelVariants[i].SetActive(i == targetIndex);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
            {
                _startPosition = hit.position;
            }
            else
            {
                _startPosition = transform.position;
            }

            _playerStatus = GetComponent<PlayerStatus>();
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged -= CheckTurnVisibility;
                TurnManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
        }
        
        private void OnGameStateChanged(bool isStarted)
        {
            if (isStarted)
            {
                CheckTurnVisibility(TurnManager.Instance.CurrentActivePlayerId);
            }
            else
            {
                UpdateVisuals(false);
            }
        }

        private void CheckTurnVisibility(ulong activeActorId)
        {
            bool amITheActiveActor = (this.NetworkObjectId == activeActorId);
            UpdateVisuals(amITheActiveActor);

            if (IsServer && amITheActiveActor)
            {
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.ResetPath(); 
                    agent.Warp(_startPosition); 
                                               
                }
            }
        }


        private void UpdateVisuals(bool isActive)
        {
            if (visualModel != null) visualModel.SetActive(isActive);
            if (col != null) col.enabled = isActive;
        }
    }
}