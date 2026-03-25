using UnityEngine;
using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// Registry Pattern: ใช้สำหรับเก็บ Location registry ในฉาก
    /// </summary>
    public static class LocationRegistry
    {
        private static readonly Dictionary<string, InteractableLocation> _locations = new Dictionary<string, InteractableLocation>();

        /// <summary>
        /// Registers a location in the system.
        /// </summary>
        /// <param name="locationId">The unique identifier ของ Location</param>
        /// <param name="location">Location component</param>
        public static void Register(string locationId, InteractableLocation location)
        {
            if (!_locations.ContainsKey(locationId))
            {
                _locations.Add(locationId, location);
            }
        }

        /// <summary>
        /// Unregisters a location from the system.
        /// </summary>
        /// <param name="locationId">The unique identifier ของ Location</param>
        public static void Unregister(string locationId)
        {
            if (_locations.ContainsKey(locationId))
            {
                _locations.Remove(locationId);
            }
        }

        /// <summary>
        /// ค้นหา Location จาก locationId
        /// </summary>
        /// <param name="locationId">The unique identifier ของ Location</param>
        /// <returns>Location component</returns>
        public static InteractableLocation GetLocation(string locationId)
        {
            _locations.TryGetValue(locationId, out var location);
            return location;
        }

        /// <summary>
        /// ล้างข้อมูล Location ทั้งหมด (ใช้เมื่อโหลด Scene ใหม่ หรือ Reset เกม)
        /// </summary>
        public static void Clear()
        {
            // STEP 1: เคลียร์ Dictionary เพื่อป้องกัน memory leak หรือ Reference ค้าง
            _locations.Clear();
            Debug.Log("[LocationRegistry] Cleared all registered locations.");
        }
    }
}