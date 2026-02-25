using UnityEngine;
using Unity.Netcode;
using System;

namespace System
{
    public class PlayerStatus : NetworkBehaviour
    {
        [Header("Player Status")]
        public readonly NetworkVariable<int> Relationship = new NetworkVariable<int>(35);
        public readonly NetworkVariable<int> Stress = new NetworkVariable<int>(10);
        public readonly NetworkVariable<float> PersonalMoney = new NetworkVariable<float>(500f);

        private float _moneyAtstartOfMonth;

        public override void OnNetworkSpawn()
        {
            if (IsServer) _moneyAtstartOfMonth = PersonalMoney.Value;
        }
        [Rpc(SendTo.Server)]
        public void ModifyStatusServerRpc(int relChange, int stressChange, float moneyChange = 0)
        {
            Relationship.Value = Mathf.Clamp(Relationship.Value + relChange, 0, 100);
            Stress.Value = Mathf.Clamp(Stress.Value + stressChange, 0, 100);
            PersonalMoney.Value += moneyChange;

            if (Relationship.Value <= 0 || Stress.Value >= 100)
            {
                Debug.LogError($"[Game Over] Player {OwnerClientId} failed the mission");
            }
        }
        public float GetMonthlyMoneyChange()
        {
            if (_moneyAtstartOfMonth == 0) return 0;
            return (PersonalMoney.Value - _moneyAtstartOfMonth) / _moneyAtstartOfMonth;
        }
        public void ResetMonthTracker() => _moneyAtstartOfMonth = PersonalMoney.Value;

    }
}