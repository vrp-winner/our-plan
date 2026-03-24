using UnityEngine;
using Configs;
using Unity.Netcode;

namespace Systems
{
    /// <summary>
    /// สคริปต์สำหรับแปะที่ตึก เพื่อตรวจจับการคลิกและการชนของผู้เล่น
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] // บังคับว่าต้องมี Collider
    public class InteractableLocation : MonoBehaviour
    {
        #region ค่าตึก (Configuration)
        [Header("Config")]
        [SerializeField] private LocationConfig locationConfig;

        [Header("Movement Target")]
        [Tooltip("จุดที่ตัวละครจะเดินไปยืนเมื่อคลิกที่ตึกนี้")]
        [SerializeField] private Transform entryPoint;
        #endregion

        #region ดึงข้อมูล (Getters)

        public LocationConfig Config => locationConfig; // เปิดให้ไฟล์อื่นเข้าถึง Config ได้

        /// <summary>
        /// Public Getter ให้ PlayerMovement ดึงตำแหน่งไปใช้
        /// ถ้าลืมใส่จะใช้ตำแหน่งตึกแทน (แต่จะเขียนเตือนเอาไว้ให้)
        /// </summary>
        public Vector3 GetWalkTarget()
        {
            if (entryPoint == null)
            {
                Debug.LogWarning($"[InteractableLocation] {name} ยังไม่ได้ตั้งค่า Entry Point! จะใช้ตำแหน่งตึกแทน");
                return transform.position;
            }
            return entryPoint.position;
        }

        public string GetLocationName()
        {
            return locationConfig != null ? locationConfig.LocationName : gameObject.name;
        }

    }
    #endregion
}