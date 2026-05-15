using UnityEngine;
using Systems;

namespace Managers
{
    /// <summary>
    /// ตัวจัดการสถานะส่วนกลางที่ถูก Trigger โดยเหตุการณ์ต่างๆ ของเกม
    /// ทำงานเฉพาะบนฝั่ง Server เท่านั้น (Server Authoritative)
    /// </summary>
    public class StatusManager : SingletonNetwork<StatusManager>
    {
        /// <summary>
        /// ลงโทษผู้เล่นทุกคนตามจำนวนเดือนที่ค้างค่าเช่า
        /// </summary>
        public void ApplyGlobalRentPenalty(int overdueMonths)
        {
            // STEP 1: Guard ป้องกัน Client เรียกใช้งาน (Server Authority 100%)
            if (!IsServer) return;

            // STEP 2: คำนวณค่า Penalty
            int statPenalty = overdueMonths switch
            {
                1 => 15,
                2 => 30,
                3 => 50,
                _ => 0
            };
            
            if (statPenalty == 0) return;

            // STEP 3: บังคับใช้ Penalty กับผู้เล่นทุกคนในระบบ
            var allPlayers = GameObject.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
            
            foreach (var status in allPlayers)
            {
                status.Stress.Value = Mathf.Clamp(status.Stress.Value + statPenalty, 0, 100);
                status.Relationship.Value = Mathf.Clamp(status.Relationship.Value - statPenalty, 0, 100);
            }
        }

        /// <summary>
        /// อัปเดตสถานะผู้เล่นเมื่อกลับถึงบ้านหรือ Apartment
        /// </summary>
        /// <param name="status">สถานะผู้เล่น</param>
        public void ProcessHomeEntry(PlayerStatus status)
        {
            // STEP 1: Guard ป้องกัน Client เรียกใช้งาน
            if (!IsServer) return;

            int currentRel = status.Relationship.Value;
            int currentStress = status.Stress.Value;

            int relChange = 0;
            int stressChange = 0;

            // STEP 2: Check Stress Condition
            if (currentStress == 100) { relChange -= 30; }
            else if (currentStress >= 80) { stressChange += 20; relChange -= 15; }
            else if (currentStress >= 60) { stressChange += 15; relChange -= 10; }
            else if (currentStress >= 40) { stressChange += 10; relChange -= 5; }
            else if (currentStress >= 20) { stressChange += 5; }
            // น้อยกว่า 20 ไม่เกิดอะไรขึ้น

            // STEP 3: Check Relationship Condition
            if (currentRel >= 80) { /* ไม่เกิดอะไรขึ้น */ }
            else if (currentRel >= 60) { relChange -= 5; }
            else if (currentRel >= 40) { relChange -= 10; stressChange += 5; }
            else if (currentRel >= 20) { relChange -= 15; stressChange += 10; }
            else { relChange -= 20; stressChange += 15; } // น้อยกว่า 20

            // STEP 4: นำผลลัพธ์ที่คำนวณได้ ไปปรับค่าสถานะจริง
            if (relChange != 0 || stressChange != 0)
            {
                status.ApplyStats_ServerOnly(relChange, stressChange, 0);
                Debug.Log($"[Homecoming] กลับบ้าน! Stress: {currentStress} -> +{stressChange}, Rel: {currentRel} -> {relChange}");
            }
        }

        public void CheckWinConditions(PlayerStatus status)
        {
            if (!IsServer) return;

            bool isObjectiveCompleted = false;

            switch (TurnManager.Instance.currentObjective.Value)
            {
                case GameObjective.GetMarried:
                    isObjectiveCompleted = status.IsMarried.Value;
                    break;

                case GameObjective.BuyHouseAndCar:
                    isObjectiveCompleted = EconomyManager.Instance.isHouseBought.Value && EconomyManager.Instance.isCarBought.Value; // เช็คจากระบบส่วนกลาง
                    break;

                case GameObjective.RetireWealthy:
                    isObjectiveCompleted = EconomyManager.Instance.jointMoney.Value >= 800000f;
                    break;

                case GameObjective.DateEveryPlaces:
                    // รวมสถานที่เดทของทุกคน
                    System.Collections.Generic.HashSet<Unity.Collections.FixedString32Bytes>
                        allSpots = new System.Collections.Generic.HashSet<Unity.Collections.FixedString32Bytes>();

                    var allPlayers =
                        GameObject.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);

                    foreach (var p in allPlayers)
                    {
                        foreach (var spot in p.VisitedDatingSpots)
                            allSpots.Add(spot);
                    }

                    isObjectiveCompleted = allSpots.Count >= 5;
                    break;
            }

            if (isObjectiveCompleted)
            {
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, true, $"บรรลุเป้าหมาย: {TurnManager.Instance.currentObjective.Value}");
            }
        }

        /// <summary>
        /// ตรวจสอบเงื่อนไข Mission Failed
        /// </summary>
        /// <param name="status">สถานะผู้เล่นปัจจุบัน</param>
        public void CheckFailConditions(PlayerStatus status)
        {
            if (!IsServer) return;
            
            // ความสัมพันธ์เหลือ 0 (หย่าร้าง) ถือว่า  Mission Failed
            if (status.Relationship.Value <= 0)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "หย่าร้าง (ความสัมพันธ์เหลือ 0)");
        }
    }
}