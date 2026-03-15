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
                TurnManager.Instance.OnCycleChanged += HandleCycleEnd;
            }
        }

        private void HandleCycleEnd(int newCycle)
        {
            if (!IsServer) return;

            // ค้นหาตัวละครทั้งหมดแทน
            var allPlayers = GameObject.FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);
            foreach (var status in allPlayers)
            {
                // โค้ดที่ต้องการให้รันทุกรอบเดือนใส่ตรงนี้
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
                TurnManager.Instance.OnCycleChanged -= HandleCycleEnd;
        }
    }
}