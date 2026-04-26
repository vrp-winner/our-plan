using UnityEngine;
using Unity.Netcode;
using Managers;

namespace Systems
{
    /// <summary>
    /// ควบคุมการแสดงผล Avatar ของผู้เล่น
    /// แสดงตามเทิร์นของตัวเองเท่านั้น ป้องกันภาพซ้อนกัน
    /// </summary>
    public class PlayerAvatarControl : NetworkBehaviour
    {
        #region References
        [Header("Character Models (4 Types)")]
        [Tooltip("ใส่โมเดล 4 แบบ เรียงตามลำดับ")]
        [SerializeField] private GameObject[] modelVariants;

        private PlayerStatus _playerStatus;
        #endregion

        #region Lifecycle
        /// <summary>
        /// เรียกตอน Object ถูก Spawn ใน Network
        /// </summary>
        public override void OnNetworkSpawn()
        {
            // ดึง Component สถานะของผู้เล่น
            _playerStatus = GetComponent<PlayerStatus>();
            
            if (_playerStatus != null)
            {
                // เปลี่ยนโมเดลเมื่อ avatarIndex เปลี่ยน
                _playerStatus.avatarIndex.OnValueChanged += (oldV, newV) => UpdateVisibility();
            }
            
            if (TurnManager.Instance != null)
            {
                // ติดตาม Event เมื่อมีการเปลี่ยน Turn (activeActorNetworkId เปลี่ยน)
                TurnManager.Instance.activeActorNetworkId.OnValueChanged += OnTurnChanged;
                
                // อัปเดตการแสดงผลทันทีที่เกิดมา
                UpdateVisibility();
            }
        }
        
        public override void OnNetworkDespawn()
        {
            // ล้าง Event เมื่อตัวละครถูกทำลายหรือจบเกม ป้องกัน Memory Leak
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.activeActorNetworkId.OnValueChanged -= OnTurnChanged;
            }
        }
        #endregion

        #region Visual Logic
        private void OnTurnChanged(ulong oldId, ulong newId)
        {
            // เมื่อเปลี่ยนเทิร์น ให้คำนวณการแสดงผลใหม่
            UpdateVisibility();
        }
        
        /// <summary>
        /// อัปเดตการโชว์ / ซ่อน Avatar
        /// </summary>
        private void UpdateVisibility()
        {
            // ตรวจสอบความพร้อมของ array และสถานะ
            if (modelVariants == null || modelVariants.Length == 0 || _playerStatus == null) return;
            
            // เช็คว่า "นี่คือเทิร์นของฉันใช่หรือไม่?"
            bool isMyTurn = false;
            if (TurnManager.Instance != null)
            {
                isMyTurn = (TurnManager.Instance.activeActorNetworkId.Value == this.NetworkObjectId);
            }

            // ดึงค่า Index ของ Avatar ที่ผู้เล่นเลือก
            int targetIndex = _playerStatus.avatarIndex.Value;
            if (targetIndex < 0 || targetIndex >= modelVariants.Length) targetIndex = 0;
            
            // จัดการเปิด / ปิด ภาพตัวละคร
            for (int i = 0; i < modelVariants.Length; i++)
            {
                if (modelVariants[i] != null) 
                {
                    // ภาพจะเปิด (SetActive = true) ก็ต่อเมื่อ:
                    // 1. เป็นภาพที่ผู้เล่นเลือก (i == targetIndex)
                    // และ 2. เป็นเทิร์นของผู้เล่น (isMyTurn == true)
                    modelVariants[i].SetActive(i == targetIndex && isMyTurn);
                }
            }
        }
        #endregion
    }
}