using UnityEngine;
using Unity.Netcode;
using Systems;

namespace Systems
{
    public class PlayerStatus : NetworkBehaviour
    {
        [Header("Team Settings")]
        public NetworkVariable<int> TeamId = new NetworkVariable<int>(0);
        public NetworkVariable<int> MemberIndex = new NetworkVariable<int>(0);
        public NetworkVariable<int> AvatarIndex = new NetworkVariable<int>(0);
        [Header("Stats(Private)")]
        public readonly NetworkVariable<int> Stress = new NetworkVariable<int>(10);
        public readonly NetworkVariable<float> PersonalMoney = new NetworkVariable<float>(500f);
        [Header("Stats(Shared by team)")]
        public readonly NetworkVariable<int> Relationship = new NetworkVariable<int>(35);


        private float _moneyAtStartOfMonth;

        public override void OnNetworkSpawn()
        {
            if (IsServer) _moneyAtStartOfMonth = PersonalMoney.Value;
        }
        #region Stats Management
        [Rpc(SendTo.Server)]
        //public void ModifyStatsServerRpc(int relChange, int stressChange, float moneyChange = 0)
        //{
        //    Relationship.Value = Mathf.Clamp(Relationship.Value + relChange, 0, 100);
        //    Stress.Value = Mathf.Clamp(Stress.Value + stressChange, 0, 100);
        //    PersonalMoney.Value += moneyChange;

        //    if (Relationship.Value <= 0 || Stress.Value >= 100)
        //    {
        //        Debug.LogError($"[Game Over] Player {OwnerClientId} failed the mission!");
        //    }
        //}
        
        //แบบใหม่: สนใจ "สมาชิกในทีม" โดยแยกแยะว่าค่าไหนเป็นของส่วนตัว และค่าไหนเป็นของส่วนรวม
        public void ModifyStatsServerRpc(int relChange, int stressChange, float moneyChange = 0)
        {
            Stress.Value = Mathf.Clamp(Stress.Value + stressChange, 0, 100);
            PersonalMoney.Value += moneyChange;

            UpdateTeamRelationship(relChange);
        }

        private void UpdateTeamRelationship(int change)
        {
            var allPlayers = GameObject.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p.TeamId.Value == this.TeamId.Value)
                {
                    p.Relationship.Value = Mathf.Clamp(p.Relationship.Value + change, 0, 100);
                }
            }
        }
        public float GetMonthlyMoneyChange()
        {
            if (_moneyAtStartOfMonth == 0) return 0;
            return (PersonalMoney.Value - _moneyAtStartOfMonth) / _moneyAtStartOfMonth;
        }

        public void ResetMonthTracker() => _moneyAtStartOfMonth = PersonalMoney.Value;
    }
    #endregion

}