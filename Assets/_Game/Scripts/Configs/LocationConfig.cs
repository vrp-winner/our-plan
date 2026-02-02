using UnityEngine;
using System.Collections.Generic;

namespace Configs
{
    // กำหนดข้อมูลของ "การกระทำ" 1 อย่าง (เช่น ทำงาน, นอน, ซื้อของ)
    [System.Serializable]
    public class LocationAction
    {
        [field: SerializeField] public string ActionName { get; private set; } // ชื่อปุ่ม
        [field: SerializeField] public float TimeCost { get; private set; } // เวลาที่ใช้
        
        // ในอนาคตอาจจะเพิ่มพวก MoneyCost, StressEffect ตรงนี้
    }

    // กำหนดข้อมูลของ "สถานที่" (เช่น บ้าน, ที่ทำงาน)
    [CreateAssetMenu(fileName = "LocationConfig", menuName = "Game/LocationConfig")]
    public class LocationConfig : ScriptableObject
    {
        [Header("General Info")]
        [field: SerializeField] public string LocationName { get; private set; }
        [field: TextArea] 
        [field: SerializeField] public string Description { get; private set; }

        [Header("Available Actions")]
        [field: SerializeField] public List<LocationAction> AvailableActions { get; private set; }
    }
}