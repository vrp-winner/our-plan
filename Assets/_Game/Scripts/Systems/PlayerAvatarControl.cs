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
        private Vector3 startPosition = Vector3.zero; 

        public override void OnNetworkSpawn()
        {
            startPosition = transform.position; // จำจุดเกิดไว้

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged += CheckTurnVisibility;
                // พิ่ม Event เช็คว่าเกมเริ่มหรือยัง เพื่อซ่อนตัวตอนแรก
                TurnManager.Instance.OnGameStateChanged += OnGameStateChanged;

                // เช็คสถานะตอนเกิด
                if (TurnManager.Instance.IsGameStarted)
                {
                    CheckTurnVisibility(TurnManager.Instance.CurrentActivePlayerId);
                }
                else
                {
                    // ถ้าเกมยังไม่เริ่ม ซ่อนไปก่อนเลย
                    UpdateVisuals(false);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged -= CheckTurnVisibility;
                TurnManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
        }
        
        private void OnGameStateChanged(bool isStarted)
        {
            // ถ้าเกมเพิ่งเริ่ม ให้เช็คเลยว่าตาใคร
            if (isStarted)
            {
                CheckTurnVisibility(TurnManager.Instance.CurrentActivePlayerId);
            }
            else
            {
                UpdateVisuals(false);
            }
        }

        private void CheckTurnVisibility(ulong activePlayerId)
        {
            // Logic: "ตัวละครนี้" คือเจ้าของเทิร์นใช่ไหม? 
            // (ถ้า OwnerClientId ของตัวนี้ ตรงกับ activePlayerId แปลว่าถึงคิวแสดงของตัวนี้)
            // ทุกจอ (Host/Client) จะคำนวณได้ผลลัพธ์เดียวกัน
            bool amITheActiveActor = (OwnerClientId == activePlayerId);

            UpdateVisuals(amITheActiveActor);

            // ถ้าเป็นตาของ "ตัวนี้" ให้ย้ายกลับจุดเกิด (เฉพาะเจ้าของเครื่องสั่งย้ายได้)
            if (amITheActiveActor && IsOwner)
            {
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.Warp(startPosition);
                else transform.position = startPosition;
            }
        }

        private void UpdateVisuals(bool isActive)
        {
            // เปิด/ปิด Model และ Collider
            if (visualModel != null) visualModel.SetActive(isActive);
            if (col != null) col.enabled = isActive;
        }
    }
}