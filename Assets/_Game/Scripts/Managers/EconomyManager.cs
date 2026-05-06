using UnityEngine;
using Unity.Netcode;
using Systems;

namespace Managers
{
    public class EconomyManager : SingletonNetwork<EconomyManager>
    {
        [Header("Money Settings")]
        public NetworkVariable<float> JointMoney = new NetworkVariable<float>(1000f);
        
        // ตัวแปรจำว่าซื้อบ้านไปหรือยัง
        public NetworkVariable<bool> IsHouseBought = new NetworkVariable<bool>(false);

        [Header("Rent Configuration")]
        [SerializeField] private float baseRent = 9000f;

        // RentLevel คือ จำนวน "เดือน" ที่ค้างชำระ
        public NetworkVariable<int> RentLevel = new NetworkVariable<int>(0);

        public float GetCurrentRentPrice()
        {
            if (RentLevel.Value == 0) return 0;
            return baseRent;
        }

        /// <summary>
        /// ถูกเรียกโดย TurnManager เมื่อครบกำหนดเวลา (เช่น ทุก 4 ตา)
        /// ทำหน้าที่ "ตั้งหนี้" เพิ่ม RentLevel ขึ้น 1
        /// </summary>
        [Rpc(SendTo.Server)]
        public void ApplyRentChargeServerRpc()
        {
            RentLevel.Value++;
            Debug.Log($"[Economy] ถึงรอบจ่ายค่าเช่า! (ค้างชำระ: {RentLevel.Value} ยอด) ต้องจ่าย: {GetCurrentRentPrice()}");

            // ทำโทษทันทีตามจำนวนเดือนที่ค้าง (1 เดือน = -15, 2 เดือน = -30, ...)
            if (StatusManager.Instance != null)
            {
                StatusManager.Instance.ApplyGlobalRentPenalty(RentLevel.Value);
            }

            // ถ้าค้างเดือนที่ 4 จบเกม
            if (RentLevel.Value >= 4)
            {
                TurnManager.Instance.NotifyEndGameRpc(0, false, "ล้มละลาย (ค้างค่าเช่า 4 เดือนติดต่อกัน)");
            }
        }

        public void ExecutePayRent_ServerOnly()
        {
            float totalPrice = GetCurrentRentPrice();

            if (RentLevel.Value > 0 && JointMoney.Value >= totalPrice)
            {
                JointMoney.Value -= totalPrice;
                RentLevel.Value = 0; // จ่ายปุ๊บ เคลียร์ยอดค้างเป็น 0
                Debug.Log($"[Economy] จ่ายค่าเช่า 9,000 สำเร็จ! เงินกองกลางเหลือ: {JointMoney.Value}");
            }
        }
    }
}