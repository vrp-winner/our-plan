using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Class นี้ทำหน้าที่จัดการระบบค่าเช่าและหนี้สิน
/// (เบื้องต้น: สร้างไว้เพื่อรับ Event จาก TurnManager ก่อน)
/// </summary>
public class RentManager : NetworkBehaviour
{
    public static RentManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        // ทำงานเฉพาะที่ Server เท่านั้น (เพราะเรื่องเงินต้องคำนวณที่ Server)
        if (IsServer)
        {
            // Subscribe Event เมื่อมีการเปลี่ยนรอบเดือน (Cycle)
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnCycleChanged += OnCycleChanged;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && TurnManager.Instance != null)
        {
            TurnManager.Instance.OnCycleChanged -= OnCycleChanged;
        }
    }

    private void OnCycleChanged(int newCycle)
    {
        // Logic การคำนวณเงิน ให้มาใส่ตรงนี้ในอนาคต
        // ตอนนี้เอาแค่ Log เพื่อ Test ระบบก่อน
        Debug.Log($"[RentManager] สิ้นเดือนแล้ว! เข้าสู่เดือนที่ {newCycle} เตรียมคำนวณค่าเช่าและภาษี...");
    }
}