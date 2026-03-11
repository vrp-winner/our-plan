using UnityEngine;
using Unity.Netcode;
using Systems;

namespace Managers
{
    public class EconomyManager : SingletonNetwork<EconomyManager>
    {
        [Header("Money Settings")]
        public NetworkVariable<float> JointMoney = new NetworkVariable<float>(1000f);

        [Header("Rent Configuration")]
        [SerializeField] private float baseRent = 9000f;
        [SerializeField] private float penaltyRate = 3000f;

        public NetworkVariable<int> RentLevel = new NetworkVariable<int>(0);

        public float GetCurrentRentPrice()
        {
            return baseRent + (RentLevel.Value * penaltyRate);
        }

        [Rpc(SendTo.Server)]
        public void ProcessRentCollectionServerRpc()
        {
            float totalPrice = GetCurrentRentPrice();

            if (JointMoney.Value >= totalPrice)
            {
                ExecutePayment(totalPrice);
            }
            else
            {
                ExecutePenalty();
            }
        }

        private void ExecutePayment(float amount)
        {
            JointMoney.Value -= amount;
            RentLevel.Value = 0; 
            Debug.Log($"[Economy] Paid {amount}. Remaining: {JointMoney.Value}");
        }

        private void ExecutePenalty()
        {
            RentLevel.Value++;
            Debug.Log($"[Economy] Rent unpaid! Level: {RentLevel.Value}");

            StatusManager.Instance.ApplyGlobalRentPenalty(RentLevel.Value);

            if (RentLevel.Value >= 4)
            {
                TurnManager.Instance.NotifyEndGameRpc(0, false, "ล้มละลายเนื่องจากค้างค่าเช่าติดต่อกัน 4 เดือน");
            }
        }

        [Rpc(SendTo.Server)]
        public void TransactionJointMoneyServerRpc(float amount)
        {
            JointMoney.Value = Mathf.Max(0, JointMoney.Value + amount);
        }
    }
}