using UnityEngine;
using System.Collections.Generic;

namespace Configs
{
    /// <summary>
    /// ข้อมูลของการกระทำแต่ละประเภทที่สามารถทำได้ในสถานที่
    /// </summary>
    [System.Serializable]
    public class LocationAction
    {
        [field: SerializeField] public string ActionName { get; private set; }
        
        // เปลี่ยนจาก float เป็น int เพื่อใช้เป็นแต้ม (Point)
        [Header("Effects on Player")]
        [field: SerializeField] public int PointCost { get; private set; }
        [field: SerializeField] public int RelationshipEffect { get; private set; }
        [field: SerializeField] public int StressEffect { get; private set; }
        [field: SerializeField] public float MoneyEffect { get; private set; }
    }

    /// <summary>
    /// Config หลักของสถานที่ 
    /// เพิ่ม LocationId เพื่อใช้เป็น Unique Identifier แทนการใช้ชื่อ GameObject
    /// </summary>
    [CreateAssetMenu(fileName = "LocationConfig", menuName = "Game/LocationConfig")]
    public class LocationConfig : ScriptableObject
    {
        [Header("System Info")]
        [Tooltip("รหัสอ้างอิงสถานที่ (ห้ามซ้ำกัน เช่น LOC_Home, LOC_Work)")]
        [field: SerializeField] public string LocationId { get; private set; }
        
        [Header("General Info")]
        [field: SerializeField] public string LocationName { get; private set; }
        [field: TextArea]
        [field: SerializeField] public string Description { get; private set; }

        [Header("Available Actions")]
        [field: SerializeField] public List<LocationAction> AvailableActions { get; private set; }
    }
}