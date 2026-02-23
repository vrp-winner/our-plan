using UnityEngine;

namespace Configs
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        // ใช้ [field: SerializeField] เพื่อลดโค้ดและแก้ Warning เรื่อง readonly/unused field
        // แต่ค่าจะยังปรับใน Inspector ได้เหมือนเดิมนะ

        [Header("Point-to-Time System")]
        [Tooltip("1 Point เท่ากับกี่วินาทีใน UI (เช่น 6 วินาที)")]
        [SerializeField] private int secondsPerPoint = 6;
        public int SecondsPerPoint => secondsPerPoint;

        [Header("Energy (Points) per Turn")]
        [SerializeField] private int maxPointsPerTurn = 20; // 20 points * 6s = 120s (2 mins)
        public int MaxPointsPerTurn => maxPointsPerTurn;

        [Header("Action Costs (Points)")]
        public int CostMove = 2;       // 12s
        public int CostWork = 5;       // 30s
        public int CostSickPenalty = 5;

        // ตรงนี้เดี๋ยวค่อยเพิ่มพวกค่าเงิน หรือค่าความเครียดทีหลังได้นะ
    }
}
