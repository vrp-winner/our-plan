using UnityEngine;
using Unity.Netcode;
using Systems; 

namespace Managers
{
    public class RentManager : SingletonNetwork<RentManager>
    {
        public override void OnNetworkSpawn()
        {
            if (IsServer && TurnManager.Instance != null)
            {
                TurnManager.Instance.OnCycleChanged += OnCycleChanged;
            }
        }

        private void OnCycleChanged(int newCycle)
        {
            if (!IsServer) return;

            // 1. จัดการค่าเช่าผ่าน EconomyManager
            EconomyManager.Instance.TransactionJointMoneyServerRpc(-9000f); // สมมติค่าเช่าตามคอนเซปต์

            // 2. ประเมินสถานะผู้เล่นทุกคน
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject.TryGetComponent(out PlayerStatus status))
                {
                    StatusManager.Instance.EvaluateEndOfMonth(status);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
                TurnManager.Instance.OnCycleChanged -= OnCycleChanged;
        }
    }
}