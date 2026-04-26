using System.Collections.Generic;
using UnityEngine;

namespace Systems.Map
{
    /// <summary>
    /// ทำหน้าที่เป็น Data Holder สำหรับจุดบนแผนที่ (Node)
    /// </summary>
    public class MapNode : MonoBehaviour
    {
        [Header("Node Configuration")]
        [Tooltip("รหัสประจำตัวของ Node (ห้ามซ้ำ)")]
        public string NodeID;

        [Tooltip("รายชื่อ Node อื่นๆ ที่สามารถเดินไปถึงได้จาก Node นี้")]
        public List<MapNode> ConnectedNodes = new List<MapNode>();

        [Header("Location Info")]
        [Tooltip("กำหนดว่า Node นี้คือจุดหมายปลายทาง (สถานที่) หรือไม่")]
        public bool IsLocation;

        [Tooltip("ระบุ ID ของสถานที่ (ใช้เชื่อมโยงกับ LocationConfig หาก IsLocation เป็น true)")]
        public string LocationID;
        
        private void OnDrawGizmos()
        {
            // วาดเส้นเชื่อมในหน้า Scene View
            Gizmos.color = Color.yellow; 
            foreach (var node in ConnectedNodes)
            {
                if (node != null)
                {
                    Gizmos.DrawLine(transform.position, node.transform.position);
                }
            }
        }
    }
}