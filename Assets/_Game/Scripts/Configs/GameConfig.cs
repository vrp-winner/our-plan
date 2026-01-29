using UnityEngine;

namespace Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        // ใช้ [field: SerializeField] เพื่อลดโค้ดและแก้ Warning เรื่อง readonly/unused field
        // แต่ค่าจะยังปรับใน Inspector ได้เหมือนเดิมนะ
        
        [Header("Time Settings")]
        [field: SerializeField] public float MaxTimePerTurn { get; private set; } = 120f; // อ่านได้อย่างเดียว แก้ไม่ได้
    
        [Header("Action Costs (Time)")]
        [field: SerializeField] public float CostMoveLocation { get; private set; } = 10f;
        [field: SerializeField] public float CostMoveWithCar { get; private set; } = 5f;
        [field: SerializeField] public float CostWork { get; private set; } = 30f;
        [field: SerializeField] public float CostTrade { get; private set; } = 10f;
        
        // ตรงนี้เดี๋ยวค่อยเพิ่มพวกค่าเงิน หรือค่าความเครียดทีหลังได้นะ
    }
}
