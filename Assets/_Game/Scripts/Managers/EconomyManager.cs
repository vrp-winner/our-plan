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

        // RentLevel คือ จำนวน "เดือน" ที่ค้างชำระ
        public NetworkVariable<int> rentLevel = new NetworkVariable<int>(0);

        public float GetCurrentRentPrice()
        {
            if (rentLevel.Value == 0) return 0;
            return baseRent;
        }

        public void ApplyRentCharge_ServerOnly()
        {
            if (!IsServer) return;

            rentLevel.Value++;
            Debug.Log($"[Economy] ถึงรอบจ่ายค่าเช่า! (ค้างชำระ: {rentLevel.Value} ยอด) ต้องจ่าย: {GetCurrentRentPrice()}");

            // ทำโทษทันทีตามจำนวนเดือนที่ค้าง (1 เดือน = -15, 2 เดือน = -30, ...)
            if (StatusManager.Instance != null)
            {
                StatusManager.Instance.ApplyGlobalRentPenalty(rentLevel.Value);
            }

            // ถ้าค้างเดือนที่ 4 จบเกม
            if (rentLevel.Value >= 4)
            {
                TurnManager.Instance.NotifyEndGameRpc(0, false, "ล้มละลาย (ค้างค่าเช่า 4 เดือนติดต่อกัน)");
            }
        }

        public void ExecutePayRent_ServerOnly()
        {
            if (!IsServer) return;

            float totalPrice = GetCurrentRentPrice();
            if (rentLevel.Value > 0 && jointMoney.Value >= totalPrice)
            {
                jointMoney.Value -= totalPrice;
                rentLevel.Value = 0; // จ่ายปุ๊บ เคลียร์ยอดค้างเป็น 0
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