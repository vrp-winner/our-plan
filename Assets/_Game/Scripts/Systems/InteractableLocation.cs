using UnityEngine;
using Configs;
using Unity.Netcode;
using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// แปะที่ GameObject ของตึกใน Scene 2D
    /// ทำหน้าที่ถือข้อมูล LocationConfig ไปลงทะเบียนใน Registry เพื่อให้ UI ดึงไปสร้างปุ่มได้
    /// </summary>
    public class InteractableLocation : MonoBehaviour
    {
        #region Config 
        [Header("Location Data")]
        [Tooltip("ลาก ScriptableObject ของสถานที่นี้มาใส่")]
        [SerializeField] private LocationConfig locationConfig;
        public LocationConfig Config => locationConfig; 
        #endregion

        #region Lifecycle
        private void Awake()
        {
            if (locationConfig == null || string.IsNullOrEmpty(locationConfig.LocationId))
            {
                Debug.LogError($"[InteractableLocation] {gameObject.name} ไม่มี LocationConfig หรือ LocationId ว่างเปล่า!");
                return;
            }

            LocationRegistry.Register(locationConfig.LocationId, this);
            Debug.Log($"[Location Registered] {locationConfig.LocationId} (GameObject: {gameObject.name})");
        }

        private void OnDestroy()
        {
            if (locationConfig != null && !string.IsNullOrEmpty(locationConfig.LocationId))
            {
                LocationRegistry.Unregister(locationConfig.LocationId);
            }
        }
        #endregion
    }
}