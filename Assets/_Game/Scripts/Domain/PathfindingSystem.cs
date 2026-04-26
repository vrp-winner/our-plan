using System.Collections.Generic;
using Systems.Map;

namespace Domain
{
    /// <summary>
    /// ระบบคำนวณเส้นทางด้วยอัลกอริทึม Breadth-First Search (BFS)
    /// เป็น Pure Logic ไม่ยุ่งเกี่ยวกับ GameObject หรือ Movement
    /// </summary>
    public static class PathfindingSystem
    {
        /// <summary>
        /// ค้นหาเส้นทางที่สั้นที่สุดจากจุดเริ่มต้นไปยังเป้าหมาย (นับตามจำนวน Node)
        /// </summary>
        public static List<MapNode> FindPath(MapNode startNode, MapNode targetNode)
        {
            // STEP 1: ตรวจสอบความถูกต้องของ Input
            if (startNode == null || targetNode == null) return null;
            if (startNode == targetNode) return new List<MapNode> { startNode };

            // STEP 2: เตรียมข้อมูลสำหรับ BFS
            Queue<MapNode> queue = new Queue<MapNode>();
            Dictionary<MapNode, MapNode> cameFrom = new Dictionary<MapNode, MapNode>();

            queue.Enqueue(startNode);
            cameFrom[startNode] = null; // จุดเริ่มต้นไม่มีที่มา

            bool pathFound = false;

            // STEP 3: วนลูปค้นหา Node
            while (queue.Count > 0)
            {
                MapNode current = queue.Dequeue();

                // ตรวจสอบว่าถึงเป้าหมายหรือยัง
                if (current == targetNode)
                {
                    pathFound = true;
                    break;
                }

                // นำ Node ที่เชื่อมต่อกันใส่คิว
                foreach (MapNode neighbor in current.ConnectedNodes)
                {
                    if (neighbor != null && !cameFrom.ContainsKey(neighbor))
                    {
                        cameFrom[neighbor] = current;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // STEP 4: ประกอบเส้นทางกลับคืน (Retrace Path) หากเจอเส้นทาง
            if (pathFound)
            {
                List<MapNode> path = new List<MapNode>();
                MapNode currentPathNode = targetNode;

                // ไล่ย้อนกลับจากเป้าหมายไปหาจุดเริ่มต้น
                while (currentPathNode != null)
                {
                    path.Add(currentPathNode);
                    currentPathNode = cameFrom[currentPathNode];
                }

                // สลับลำดับให้ถูกต้อง (Start -> Target)
                path.Reverse();
                return path;
            }

            // คืนค่า null หากไม่พบเส้นทาง
            return null;
        }
    }
}