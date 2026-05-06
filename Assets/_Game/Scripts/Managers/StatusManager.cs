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
        /// ลงโทษผู้เล่นทุกคนเมื่อไม่มีเงินจ่ายค่าเช่า
        /// </summary>
        /// <param name="unpaidLevel">ระดับความรุนแรงของการค้างค่าเช่า</param>
        public void ApplyGlobalRentPenalty(int unpaidLevel)
        {
            // STEP 1: Guard ป้องกัน Client เรียกใช้งาน (Server Authority 100%)
            if (!IsServer) return;

            // STEP 2: คำนวณค่า Penalty
            int penalty = unpaidLevel switch
            {
                1 => 15,
                2 => 30,
                3 => 50,
                _ => 0
            };

            // STEP 3: บังคับใช้ Penalty กับผู้เล่นทุกคนในระบบ
            var allPlayers = GameObject.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
            foreach (var status in allPlayers)
            {
                // เรียก Server Method โดยตรง (ไม่ใช่ RPC แล้ว)
                status.ApplyStats_ServerOnly(-penalty, penalty, 0);
            }
        }

        /// <summary>
        /// อัปเดตสถานะผู้เล่นเมื่อกลับถึงบ้าน
        /// </summary>
        /// <param name="status">สถานะผู้เล่น</param>
        public void ProcessHomeEntry(PlayerStatus status)
        {
            // STEP 1: Guard ป้องกัน Client เรียกใช้งาน
            if (!IsServer) return;

            int rel = status.Relationship.Value;
            int str = status.Stress.Value;

            int relChange = 0;
            int stressChange = 0;

            // STEP 2: คำนวณความสัมพันธ์
            if (rel >= 80) relChange = 0;
            else if (rel >= 60) relChange = -5;
            else if (rel >= 40) relChange = -10;
            else if (rel >= 20) relChange = -15;
            else relChange = -20;

            // STEP 3: คำนวณความเครียด
            if (str < 20) { }
            else if (str < 40) { stressChange = 5; }
            else if (str < 60) { stressChange = 10; relChange -= 5; }
            else if (str < 80) { stressChange = 15; relChange -= 10; }
            else { stressChange = 20; relChange -= 15; }

            // STEP 4: อัปเดตสถานะด้วย Server Method โดยตรง
            status.ApplyStats_ServerOnly(relChange, stressChange, 0);

            // STEP 5: ตรวจสอบเงื่อนไข Mission Failed
            CheckFailConditions(status);
        }


        public void CheckWinConditions(PlayerStatus status)
        {
            if (!IsServer) return;

            if (status.Relationship.Value < 80) return;

            bool isObjectiveCompleted = false;

            switch (TurnManager.Instance.currentObjective.Value)
            {
                case GameObjective.GetMarried:
                    isObjectiveCompleted = status.IsMarried.Value;
                    break;

                case GameObjective.BuyHouseAndCar:
                    isObjectiveCompleted = status.HasHouse.Value && status.HasCar.Value;
                    break;

                case GameObjective.RetireWealthy:
                    isObjectiveCompleted = EconomyManager.Instance.JointMoney.Value >= 800000f;
                    break;

                case GameObjective.DateEverywhere:
                    isObjectiveCompleted = status.VisitedDatingSpots.Count >= 5;
                    break;
            }

            if (isObjectiveCompleted)
            {
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, true, $"บรรลุเป้าหมาย: {TurnManager.Instance.currentObjective.Value} รักษาความสัมพันธ์ได้");
            }
        }

        /// <summary>
        /// ตรวจสอบเงื่อนไข Mission Failed
        /// </summary>
        /// <param name="status">สถานะผู้เล่นปัจจุบัน</param>
        private void CheckFailConditions(PlayerStatus status)
        {
            // หากความสัมพันธ์พัง หรือ เครียดทะลุปรอท ถือว่า  Mission Failed
            if (status.Relationship.Value <= 0)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "หย่าร้าง (ความสัมพันธ์เหลือ 0)");
            else if (status.Stress.Value >= 100)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "ความเครียดกลืนกิน (ความเครียดถึง 100)");
        }
    }
}