using UnityEngine;
using Unity.Netcode;

namespace Managers
{
    public class EconomyManager : SingletonNetwork<EconomyManager>
    {
        public NetworkVariable<float> JointMoney = new NetworkVariable<float>(1000f);
        private int _unpaidMonths = 0; 

        public void ProcessMonthlyRent()
        {
            if (!IsServer) return;

            float rentAmount = 9000f; 

            if (JointMoney.Value >= rentAmount)
            {
                JointMoney.Value -= rentAmount;
                _unpaidMonths = 0;
                Debug.Log("จ่ายค่าเช่าเรียบร้อย");
            }
            else
            {
                _unpaidMonths++;
                Debug.Log($"ค้างค่าเช่าเดือนที่ {_unpaidMonths}");

                // เช็คเงื่อนไขล้มละลาย
                if (_unpaidMonths >= 4)
                {
                    TurnManager.Instance.NotifyEndGameRpc(0, false, "ล้มละลาย (ค้างค่าเช่าครบ 4 เดือน)");
                }
            }

        }
        [Rpc(SendTo.Server)]
        public void TransactionJointMoneyServerRpc(float amount) // ตัดตัว 'v' ออก
        {
            JointMoney.Value = Mathf.Max(0, JointMoney.Value + amount);
        }
    }
}