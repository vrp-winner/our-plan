using UnityEngine;
using Configs;

namespace Systems
{
    /// <summary>
    /// สคริปต์สำหรับแปะที่ตึก เพื่อลงทะเบียนเข้าสู่ LocationRegistry
    /// และใช้ LocationId จาก Config แทนชื่อ GameObject
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] // บังคับว่าต้องมี Collider
    public class InteractableLocation : MonoBehaviour
    {
        #region Config (ค่าตึก)
        [Header("Config")]
        [SerializeField] private LocationConfig locationConfig;

        [Header("Movement Target")]
        [Tooltip("จุดที่ตัวละครจะเดินไปยืนเมื่อคลิกที่ตึกนี้")]
        [SerializeField] private Transform entryPoint;
        #endregion
        
        public LocationConfig Config => locationConfig; // เปิดให้ไฟล์อื่นเข้าถึง Config ได้
        
        #region Lifecycle
        /// <summary>
        /// ลงทะเบียนตึกเข้าสู่ระบบเมื่อเริ่มเกมด้วย LocationId
        /// </summary>
        private void Awake()
        {
            // STEP 1: ตรวจสอบ Config
            if (locationConfig == null || string.IsNullOrEmpty(locationConfig.LocationId))
            {
                Debug.LogError($"[InteractableLocation] {gameObject.name} ไม่มี LocationConfig หรือ LocationId ว่างเปล่า!");
                return;
            }

            // STEP 2: ลงทะเบียนด้วย LocationId จาก Config (Source of Truth)
            LocationRegistry.Register(locationConfig.LocationId, this);
        }

        /// <summary>
        /// ถอนการลงทะเบียนเมื่อตึกถูกทำลาย
        /// </summary>
        private void OnDestroy()
        {
            if (locationConfig != null && !string.IsNullOrEmpty(locationConfig.LocationId))
            {
                LocationRegistry.Unregister(locationConfig.LocationId);
            }
        }
        #endregion


        #region Getters (ดึงข้อมูล)
        /// <summary>
        /// ดึงตำแหน่งพิกัดเป้าหมายสำหรับการเดิน
        /// </summary>
        /// <returns>พิกัด Vector3</returns>
        public Vector3 GetWalkTarget()
        {
            if (entryPoint == null)
            {
                Debug.LogWarning($"[InteractableLocation] {name} ยังไม่ได้ตั้งค่า Entry Point! จะใช้ตำแหน่งตึกแทน");
                return transform.position;
            }
            return entryPoint.position;
        }

        /// <summary>
        /// ดึงชื่อสถานที่เพื่อนำไปแสดงผล
        /// </summary>
        /// <returns>ชื่อสถานที่</returns>
        public string GetLocationName()
        {
            return locationConfig != null ? locationConfig.LocationName : gameObject.name;
        }
    }
    #endregion
}