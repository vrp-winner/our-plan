using UnityEngine;
using Unity.Netcode;

namespace Systems
{
    public enum GameObjective
    {
        GetMarried,
        BuyHouseAndCar,
        RetireWealthy,
        DateEverywhere
    }

    /// <summary>
    /// เก็บสถานะของผู้เล่น (Stats + Team)
    /// แก้ไขค่าได้จาก Server เท่านั้น
    /// </summary>
    public class PlayerStatus : NetworkBehaviour
    {
        [Header("Team Settings")]
        public NetworkVariable<int> teamId = new NetworkVariable<int>(0);
        public NetworkVariable<int> memberIndex = new NetworkVariable<int>(0);
        public NetworkVariable<int> avatarIndex = new NetworkVariable<int>(0);
        
        [Header("Stats(Private)")]
        public readonly NetworkVariable<int> Stress = new NetworkVariable<int>(10);
        public readonly NetworkVariable<float> PersonalMoney = new NetworkVariable<float>(500f);
       
        public NetworkList<Unity.Collections.FixedString32Bytes> VisitedDatingSpots = new NetworkList<Unity.Collections.FixedString32Bytes>();

        public NetworkVariable<bool> IsMarried = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> HasHouse = new NetworkVariable<bool>(false);
        public NetworkVariable<bool> HasCar = new NetworkVariable<bool>(false);

        [Header("Stats(Shared by team)")]
        public readonly NetworkVariable<int> Relationship = new NetworkVariable<int>(35);

        private float _moneyAtStartOfMonth;

        public override void OnNetworkSpawn()
        {
            if (IsServer) _moneyAtStartOfMonth = PersonalMoney.Value;
        }

        #region Stats Management (Server Only)
        /// <summary>
        /// ปรับค่าสถานะของผู้เล่น
        /// </summary>
        public void ApplyStats_ServerOnly(int relChange, int stressChange, float moneyChange = 0)
        {
            if (!IsServer) return;

            Stress.Value = Mathf.Clamp(Stress.Value + stressChange, 0, 100);
            PersonalMoney.Value += moneyChange;

            UpdateTeamRelationship(relChange);
        }

        /// <summary>
        /// อัปเดตค่าความสัมพันธ์ของสมาชิกในทีมเดียวกัน
        /// </summary>
        private void UpdateTeamRelationship(int change)
        {
            var allPlayers = GameObject.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p.teamId.Value == this.teamId.Value)
                {
                    p.Relationship.Value = Mathf.Clamp(p.Relationship.Value + change, 0, 100);
                }
            }
        }

        /// <summary>
        /// คำนวณ % การเปลี่ยนแปลงเงินในรอบเดือน
        /// </summary>
        public float GetMonthlyMoneyChange()
        {
            if (_moneyAtStartOfMonth == 0) return 0;
            return (PersonalMoney.Value - _moneyAtStartOfMonth) / _moneyAtStartOfMonth;
        }

        public void ResetMonthTracker() => _moneyAtStartOfMonth = PersonalMoney.Value;
        #endregion
    }
}