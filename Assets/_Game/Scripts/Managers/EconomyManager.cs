using UnityEngine;
using Unity.Netcode;

namespace Managers
{
    public class EconomyManager : SingletonNetwork<EconomyManager>
    {
        [Header("Money Settings")]
        public NetworkVariable<float> jointMoney = new NetworkVariable<float>(1000f);
        
        // ตัวแปรจำว่าซื้อบ้านไปหรือยัง
        public NetworkVariable<bool> isHouseBought = new NetworkVariable<bool>(false);
        
        // [เพิ่มใหม่] ตัวแปรจำว่ามีรถหรือยัง
        public NetworkVariable<bool> isCarBought = new NetworkVariable<bool>(false);

        [Header("Rent Configuration")]
        [SerializeField] private float baseRent = 9000f;
        
        // จำนวนบิลที่ค้างชำระ (ใช้คูณค่าเช่า เช่น ค้าง 2 บิล = 18K)
        public NetworkVariable<int> pendingBills = new NetworkVariable<int>(0);

        // จำนวนเดือนที่ Overdue (ใช้ลงโทษตามขั้นบันได)
        public NetworkVariable<int> rentLevel = new NetworkVariable<int>(0);

        public float GetCurrentRentPrice()
        {
            return pendingBills.Value * baseRent; // ค้างกี่บิล เอาไปคูณ 9000
        }
        
        // เรียกตอนเข้า Round 4, 8, 12 (บิลมา แต่ยังไม่โดนปรับ Overdue)
        public void GenerateRentBill_ServerOnly()
        {
            if (!IsServer) return;
            pendingBills.Value++;
            Debug.Log($"[Economy] บิลค่าเช่ามาแล้ว! ยอดรวม: {GetCurrentRentPrice()} (Overdue: {rentLevel.Value})");
        }
        
        // เรียกตอนเข้า Round 5, 9, 13 (ปรับ Overdue ตามบิลที่ค้าง)
        public void ApplyRentPenalty_ServerOnly()
        {
            if (!IsServer) return;

            // อัปเดตยอด Overdue ให้เท่ากับจำนวนบิลที่ยังไม่ได้จ่าย
            rentLevel.Value = pendingBills.Value;

            if (rentLevel.Value > 0)
            {
                Debug.Log($"[Economy] โดนทำโทษค่าเช่า! (ค้างชำระ: {rentLevel.Value} เดือน)");

                if (StatusManager.Instance != null)
                {
                    // ส่งจำนวนเดือนที่ค้างไปลงโทษ (1 เดือน = 15, 2 เดือน = 30, 3 เดือน = 50)
                    StatusManager.Instance.ApplyGlobalRentPenalty(rentLevel.Value);
                }
            }
        }

        public void ExecutePayRent_ServerOnly()
        {
            if (!IsServer) return;

            float totalPrice = GetCurrentRentPrice();
            if (pendingBills.Value > 0 && jointMoney.Value >= totalPrice)
            {
                jointMoney.Value -= totalPrice;
                pendingBills.Value = 0; // ล้างบิล
                rentLevel.Value = 0;    // ล้างยอด Overdue
                Debug.Log($"[Economy] จ่ายค่าเช่าสำเร็จ! เงินกองกลางเหลือ: {jointMoney.Value}");
            }
        }

        // ตรวจสอบเงื่อนไขชนะเกม
        public void CheckWinCondition(ulong clientId)
        {
            if (isHouseBought.Value && isCarBought.Value)
            {
                TurnManager.Instance.NotifyEndGameRpc(clientId, true, "เป้าหมายสูงสุดสำเร็จ! ซื้อบ้านและรถเรียบร้อยแล้ว (Win)");
            }
        }
    }
}