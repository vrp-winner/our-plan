using UnityEngine;
using Configs;
using UI;

namespace Systems
{
    /// <summary>
    /// สคริปต์สำหรับแปะที่ตึก เพื่อตรวจจับการคลิกและการชนของผู้เล่น
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] // บังคับว่าต้องมี Collider
    public class InteractableLocation : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private LocationConfig locationConfig;
        
        [Header("Movement Target")]
        [Tooltip("จุดที่ตัวละครจะเดินไปยืนเมื่อคลิกที่ตึกนี้")]
        [SerializeField] private Transform entryPoint;
        
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

        private void OnTriggerEnter(Collider other)
        {
            // เช็คว่าสิ่งที่ชนมี PlayerTimeSystem หรือไม่ (เช็คว่าเป็นตัวผู้เล่นไหม)
            if (other.TryGetComponent(out PlayerTimeSystem player))
            {
                // UI จะขึ้นเฉพาะเครื่องที่เป็น "เจ้าของ" ตัวละครนั้น
                if (player.IsOwner)
                {
                    Debug.Log($"[Interaction] ผู้เล่นเดินเข้าสู่ {locationConfig.LocationName}");
                    InteractionUIManager.Instance.ShowLocation(locationConfig, player);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out PlayerTimeSystem player))
            {
                if (player.IsOwner)
                {
                    InteractionUIManager.Instance.HideUI();
                }
            }
        }
    }
}