using UnityEngine;
using Unity.Netcode;
using Managers;

namespace Systems
{
    /// <summary>
    /// ควบคุมการแสดงผลหรือซ่อนตัวละคร (Avatar) ให้สัมพันธ์กับ Active Actor
    /// ควบคุมโดยตรวจสอบ NetworkObjectId
    /// </summary>
    public class PlayerAvatarControl : NetworkBehaviour
    {
        #region References (ตัวแปร)
        [Header("References")]
        [SerializeField] private GameObject visualModel; // โมเดลตัวละคร (Mesh)
        [SerializeField] private Collider col; // Collider ของตัวละคร

        // จุดเริ่มต้น (Spawn Point) - อาจจะดึงจาก Scene หรือ Config ในอนาคต
        private Vector3 _startPosition;
        private PlayerStatus _playerStatus;

        [Header("Character Models (4 Types)")]
        [Tooltip("ใส่โมเดล 4 แบบ เรียงตามลำดับ: ทีม1คน1, ทีม1คน2, ทีม2คน1, ทีม2คน2")]
        [SerializeField] private GameObject[] modelVariants;
        #endregion

        #region Lifecycle
        /// <summary>
        /// เริ่มต้นการตั้งค่าเมื่อ Object ถูกสร้างขึ้นในระบบ Network
        /// </summary>
        public override void OnNetworkSpawn()
        {
            // STEP 1: บันทึกจุดเริ่มต้น และตั้งค่า Model Variant
            _startPosition = transform.position; // จำจุดเกิดไว้
            _playerStatus = GetComponent<PlayerStatus>();
            
            if (_playerStatus != null)
            {
                _playerStatus.TeamId.OnValueChanged += (oldV, newV) => SetupModelVariant();
                _playerStatus.MemberIndex.OnValueChanged += (oldV, newV) => SetupModelVariant();
                SetupModelVariant();
            }

            // STEP 2: ผูก Event ควบคุม Turn Visibility จาก TurnManager
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged += CheckTurnVisibility;
                TurnManager.Instance.OnGameStateChanged += OnGameStateChanged;

                // ตรวจสอบสถานะเกมและเปิดใช้งานตัวละครที่กำลังเข้าเทิร์น
                if (TurnManager.Instance.IsGameStarted)
                    CheckTurnVisibility(TurnManager.Instance.ActiveActorNetworkId.Value);
                else
                    UpdateVisuals(false);
            }
        }
        
        /// <summary>
        /// ยกเลิก Event เมื่อ Object ถูกทำลาย
        /// </summary>
        public override void OnNetworkDespawn()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged -= CheckTurnVisibility;
                TurnManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
        }
        #endregion

        #region Visual Logic (ภาพ)
        /// <summary>
        /// เลือกแสดงผลโมเดลตาม Avatar Index 
        /// </summary>
        private void SetupModelVariant()
        {
            // STEP 1: ตรวจสอบความถูกต้องของโมเดล
            if (modelVariants == null || modelVariants.Length == 0 || _playerStatus == null) return;

            // STEP 2: คำนวณ Index
            int targetIndex = _playerStatus.AvatarIndex.Value;
            if (targetIndex < 0 || targetIndex >= modelVariants.Length) targetIndex = 0;
            
            // STEP 3: เปิดเฉพาะโมเดลที่ถูกต้อง
            for (int i = 0; i < modelVariants.Length; i++)
            {
                if (modelVariants[i] != null) modelVariants[i].SetActive(i == targetIndex);
            }
        }
        
        /// <summary>
        /// ตรวจสอบสถานะเริ่มเกม เพื่ออัปเดตการแสดงผลให้ตรงกับเทิร์น
        /// </summary>
        /// <param name="isStarted">เกมเริ่มแล้วหรือยัง</param>
        private void OnGameStateChanged(bool isStarted)
        {
            if (isStarted) CheckTurnVisibility(TurnManager.Instance.ActiveActorNetworkId.Value);
            else UpdateVisuals(false);
        }

        /// <summary>
        /// ควบคุมการซ่อน/แสดงตัวละคร โดยเทียบรหัสของ Active Actor (เคร่งครัดที่ NetworkObjectId)
        /// </summary>
        /// <param name="activeActorId">ID ของตัวละครที่เป็นเทิร์นปัจจุบัน</param>
        private void CheckTurnVisibility(ulong activeActorId)
        {
            // STEP 1: ตรวจสอบความถูกต้องของ Actor Identity
            bool amITheActiveActor = (this.NetworkObjectId == activeActorId);
            
            // STEP 2: แสดง/ซ่อนโมเดล
            UpdateVisuals(amITheActiveActor);

            // STEP 3: รีเซ็ตตำแหน่งบน Server หากเป็นเทิร์นของตัวละครนี้
            if (IsServer && amITheActiveActor)
            {
                transform.position = _startPosition;
            }
        }

        /// <summary>
        /// ปรับสถานะ Game Object ที่แสดงผลและรับการชน
        /// </summary>
        /// <param name="isActive">อนุญาตให้แสดงผลหรือไม่</param>
        private void UpdateVisuals(bool isActive)
        {
            if (visualModel != null) visualModel.SetActive(isActive);
            if (col != null) col.enabled = isActive;
        }
    }
    #endregion
}