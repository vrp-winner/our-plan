using UnityEngine;
using System.Collections.Generic;

namespace Configs
{
    /// <summary>
    /// ข้อมูลของการกระทำแต่ละประเภทที่สามารถทำได้ในสถานที่
    /// อัปเดต: เพิ่ม Data-Driven ให้ครอบคลุมทุก Action (รวมถึง Sleep/End Turn)
    /// </summary>
    [System.Serializable]
    public class LocationAction
    {
        [field: SerializeField] public string ActionName { get; private set; }
        
        [Header("Effects on Player")]
        [field: SerializeField] public int PointCost { get; private set; }
        [field: SerializeField] public int RelationshipEffect { get; private set; }
        [field: SerializeField] public int StressEffect { get; private set; }
        [field: SerializeField] public float MoneyEffect { get; private set; }

        [Header("Special Behaviors")]
        [Tooltip("หากเปิด ค่านี้จะกวาดเวลาที่เหลือทั้งหมด (เช่นการ นอน)")]
        [field: SerializeField] public bool IsConsumeAllPoints { get; private set; }
        
        [Tooltip("หากเปิด เมื่อทำ Action นี้จบจะบังคับจบเทิร์นทันที (แม้เวลาจะยังคงเหลืออยู่)")]
        [field: SerializeField] public bool EndsTurn { get; private set; }
        
        // ควบคุมว่า Action นี้กดได้แค่ครั้งเดียวต่อเทิร์นหรือไม่ (เช่น Date)
        [Tooltip("หากเปิด Action นี้จะกดได้แค่ 1 ครั้งต่อ 1 เทิร์นเท่านั้น")]
        [field: SerializeField] public bool IsOncePerTurn { get; private set; }
    }

    /// <summary>
    /// Config หลักของสถานที่ 
    /// </summary>
    [CreateAssetMenu(fileName = "LocationConfig", menuName = "Game/LocationConfig")]
    public class LocationConfig : ScriptableObject
    {
        [Header("System Info")]
        [field: SerializeField] public string LocationId { get; private set; }
        
        [Header("General Info")]
        [field: SerializeField] public string LocationName { get; private set; }
        [field: TextArea]
        [field: SerializeField] public string Description { get; private set; }

        [Header("Available Actions")]
        [field: SerializeField] public List<LocationAction> AvailableActions { get; private set; }
    }
}