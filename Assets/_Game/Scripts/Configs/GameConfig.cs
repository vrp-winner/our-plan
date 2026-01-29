using UnityEngine;

[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Time Settings")]
    public float MaxTimePerTurn = 120f; // 2 นาที (120 วินาที)

    [Header("Action Costs (Time)")]
    public float CostMoveLocation = 10f;
    public float CostMoveWithCar = 5f;
    public float CostWork = 30f;
    public float CostTrade = 10f;
    
    // ตรงนี้เดี๋ยวค่อยเพิ่มพวกค่าเงิน หรือค่าความเครียดทีหลังได้นะ
}