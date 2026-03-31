using UnityEngine;
using System.Collections.Generic;

namespace Systems
{
    /// <summary>
    /// Registry Pattern: ใช้สำหรับเก็บ Location registry ในฉาก
    /// มีฟังก์ชัน Debug ตรวจสอบสถานที่
    /// </summary>
    public static class LocationRegistry
    {
        private static readonly Dictionary<string, InteractableLocation> Locations = new Dictionary<string, InteractableLocation>();

        /// <summary>
        /// เพิ่ม Location เข้า Registry
        /// </summary>
        /// <param name="locationId">ID ของสถานที่</param>
        /// <param name="location">Component ของสถานที่</param>
        public static void Register(string locationId, InteractableLocation location)
        {
            // STEP 1: ตรวจสอบการซ้ำซ้อน
            if (!Locations.ContainsKey(locationId))
            {
                Locations.Add(locationId, location);
            }
            else
            {
                Debug.LogWarning($"[LocationRegistry] LocationId ซ้ำ! ไม่สามารถเพิ่ม '{locationId}' ได้");
            }
        }

        /// <summary>
        /// เอา Location ออกจาก Registry
        /// </summary>
        public static void Unregister(string locationId)
        {
            if (Locations.ContainsKey(locationId))
            {
                Locations.Remove(locationId);
            }
        }

        /// <summary>
        /// ดึง Location จาก ID
        /// </summary>
        public static InteractableLocation GetLocation(string locationId)
        {
            // STEP 2: Log แจ้งเตือนเมื่อค้นหา Config ไม่เจอ
            if (!Locations.TryGetValue(locationId, out var location))
            {
                Debug.LogWarning($"[LocationRegistry] ไม่พบ LocationId: '{locationId}'");
            }
            return location;
        }

        /// <summary>
        /// ล้าง Location ทั้งหมด
        /// </summary>
        public static void Clear()
        {
            Locations.Clear();
            Debug.Log("[LocationRegistry] ล้าง Location ทั้งหมดเรียบร้อย");
        }

        /// <summary>
        /// แสดงรายชื่อ Location ที่ลงทะเบียนทั้งหมด (ใช้ตรวจสอบตอนเริ่มเกม)
        /// </summary>
        public static void DebugAllRegisteredLocations()
        {
            Debug.Log($"[LocationRegistry] มี Location ลงทะเบียนทั้งหมด: {Locations.Count} แห่ง");
            foreach (var loc in Locations)
            {
                Debug.Log($" - ID: {loc.Key} | GameObject: {loc.Value.gameObject.name}");
            }
        }
    }
}