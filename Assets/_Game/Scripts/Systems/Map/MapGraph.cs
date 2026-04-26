using System.Collections.Generic;
using UnityEngine;

namespace Systems.Map
{
    /// <summary>
    /// ตัวจัดการ Node ทั้งหมดในฉาก (Registry)
    /// ทำการค้นหาและลงทะเบียน Node ทั้งหมดด้วยตัวเองในตอน Awake (Deterministic)
    /// </summary>
    public class MapGraph : MonoBehaviour
    {
        public static MapGraph Instance { get; private set; }

        private readonly Dictionary<string, MapNode> _nodeDictionary = new Dictionary<string, MapNode>();

        /// <summary>
        /// กำหนดค่า Singleton และแสกนหา Node ทั้งหมดในฉาก
        /// </summary>
        private void Awake()
        {
            // STEP 1: จัดการ Singleton Pattern
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // STEP 2: ค้นหา MapNode ทั้งหมดใน Scene ล่วงหน้า เพื่อให้เกิด Deterministic Registration
            MapNode[] allNodes = FindObjectsByType<MapNode>(FindObjectsSortMode.None);
            foreach (MapNode node in allNodes)
            {
                RegisterNode(node);
            }
            
            Debug.Log($"[MapGraph] ลงทะเบียน Node สำเร็จทั้งหมด {_nodeDictionary.Count} จุด");
        }

        /// <summary>
        /// ลงทะเบียน Node เข้าสู่ระบบส่วนกลาง
        /// </summary>
        private void RegisterNode(MapNode node)
        {
            if (string.IsNullOrEmpty(node.NodeID)) return;

            if (!_nodeDictionary.ContainsKey(node.NodeID))
            {
                _nodeDictionary.Add(node.NodeID, node);
            }
        }

        /// <summary>
        /// ค้นหา Node จาก ID
        /// </summary>
        public MapNode GetNode(string nodeId)
        {
            if (_nodeDictionary.TryGetValue(nodeId, out MapNode node))
            {
                return node;
            }
            return null;
        }

        /// <summary>
        /// ดึง Node เริ่มต้น
        /// </summary>
        public MapNode GetDefaultSpawnNode()
        {
            foreach (var node in _nodeDictionary.Values)
            {
                return node;
            }
            return null;
        }
    }
}