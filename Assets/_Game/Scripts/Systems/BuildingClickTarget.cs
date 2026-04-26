using UnityEngine;
using Systems.Map;

namespace Systems
{
    /// <summary>
    /// แปะไว้ที่ตัวตึก เพื่อใช้เชื่อมโยงตึกเข้ากับ Node หน้าประตู
    /// </summary>
    public class BuildingClickTarget : MonoBehaviour
    {
        [Tooltip("ลาก MapNode หน้าตึกมาใส่ช่องนี้")]
        public MapNode linkedNode;
    }
}