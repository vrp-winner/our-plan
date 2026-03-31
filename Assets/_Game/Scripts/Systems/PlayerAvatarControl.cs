using UnityEngine;
using Unity.Netcode;

namespace Systems
{
    /// <summary>
    /// ควบคุมการแสดงผล Avatar ของผู้เล่น
    /// แสดงโมเดลตาม avatarIndex (Visibility)
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
            // STEP 1: ดึง Component สถานะของผู้เล่น
            _playerStatus = GetComponent<PlayerStatus>();
            
            // STEP 2: ผูก Event เพื่อเปลี่ยนโมเดลเมื่อ AvatarIndex มีการเปลี่ยนแปลง
            if (_playerStatus != null)
            {
                // เปลี่ยนโมเดลเมื่อ avatarIndex เปลี่ยน
                _playerStatus.avatarIndex.OnValueChanged += (oldV, newV) => SetupModelVariant();
                SetupModelVariant();
            }
        }
        #endregion

        #region Visual Logic
        /// <summary>
        /// แสดงโมเดลตาม avatarIndex
        /// </summary>
        private void SetupModelVariant()
        {
            // STEP 1: ตรวจสอบความพร้อมของ array และสถานะ
            if (modelVariants == null || modelVariants.Length == 0 || _playerStatus == null) return;

            // STEP 2: ดึงค่า Index เพื่อระบุว่าจะใช้ Model ตัวไหน
            int targetIndex = _playerStatus.avatarIndex.Value;
            if (targetIndex < 0 || targetIndex >= modelVariants.Length) targetIndex = 0;
            
            // STEP 3: เปิดเฉพาะ Model Variant ที่ผู้เล่นเลือก (Root ไม่ถูกปิด ปิดแค่ Model ชิ้นอื่นที่ไม่ใช้)
            for (int i = 0; i < modelVariants.Length; i++)
            {
                if (modelVariants[i] != null) 
                {
                    modelVariants[i].SetActive(i == targetIndex);
                }
            }
        }
        #endregion
    }
}