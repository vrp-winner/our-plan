using UnityEngine;
using Configs;
using Unity.Netcode;
using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// แปะที่ตึกเพื่อให้ระบบรู้จัก Location และป้องกัน Trigger Flicker
    /// </summary>
    [RequireComponent(typeof(BoxCollider))] 
    public class InteractableLocation : MonoBehaviour
    {
        #region Config 
        [Header("Config")]
        [SerializeField] private LocationConfig locationConfig;

        [Header("Movement Target")]
        [SerializeField] private Transform entryPoint;
        #endregion
        
        public LocationConfig Config => locationConfig; 
        
        // เก็บ NetworkObject ที่อยู่ภายในเพื่อป้องกัน Flicker เข้า-ออกรัวๆ
        private readonly HashSet<NetworkObject> _insideActors = new HashSet<NetworkObject>();

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

        #region Trigger Events (Server Only)
        private void OnTriggerEnter(Collider other)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // ป้องกัน Trigger ซ้ำ
            var netObj = other.GetComponent<NetworkObject>();
            if (netObj == null) return;
            if (_insideActors.Contains(netObj)) return;

            _insideActors.Add(netObj);
            Debug.Log($"[Trigger Enter] {other.name} | Layer: {LayerMask.LayerToName(other.gameObject.layer)}");

            var pointSystem = other.GetComponent<PlayerPointSystem>();
            if (pointSystem != null)
            {
                pointSystem.NotifyLocationEnter(Config.LocationId);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!NetworkManager.Singleton.IsServer) return;

            // ป้องกัน Trigger ซ้ำ
            var netObj = other.GetComponent<NetworkObject>();
            if (netObj == null) return;
            if (!_insideActors.Contains(netObj)) return;

            _insideActors.Remove(netObj);
            Debug.Log($"[Trigger Exit] {other.name} ออกจาก {Config.LocationId}");

            var pointSystem = other.GetComponent<PlayerPointSystem>();
            if (pointSystem != null)
            {
                pointSystem.NotifyLocationExit();
            }
        }
        #endregion

        #region Getters
        public Vector3 GetWalkTarget()
        {
            if (entryPoint == null) return transform.position;
            return entryPoint.position;
        }

        public string GetLocationName()
        {
            return locationConfig != null ? locationConfig.LocationName : gameObject.name;
        }
        #endregion
    }
}