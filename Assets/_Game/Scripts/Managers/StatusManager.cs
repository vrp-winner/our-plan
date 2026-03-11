using UnityEngine;
using Systems;
using Unity.Netcode;

namespace Managers
{
    public class StatusManager : SingletonNetwork<StatusManager>
    {
        public void ApplyGlobalRentPenalty(int unpaidLevel)
        {
            if (!IsServer) return;

            int penalty = unpaidLevel switch
            {
                1 => 15,
                2 => 30,
                3 => 50,
                _ => 0
            };

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject.TryGetComponent(out PlayerStatus status))
                {
                    status.ModifyStatsServerRpc(-penalty, penalty); // หัก Rel, เพิ่ม Stress
                }
            }
        }

        public void ProcessHomeEntry(PlayerStatus status)
        {
            if (!IsServer) return;

            int rel = status.Relationship.Value;
            int str = status.Stress.Value;

            int relChange = 0;
            int stressChange = 0;

            if (rel >= 80) relChange = 0;
            else if (rel >= 60) relChange = -5;
            else if (rel >= 40) relChange = -10;
            else if (rel >= 20) relChange = -15;
            else relChange = -20;

            if (str < 20) { }
            else if (str < 40) { stressChange = 5; }
            else if (str < 60) { stressChange = 10; relChange -= 5; }
            else if (str < 80) { stressChange = 15; relChange -= 10; }
            else { stressChange = 20; relChange -= 15; }

            status.ModifyStatsServerRpc(relChange, stressChange);

            CheckFailConditions(status);
        }

        private void CheckFailConditions(PlayerStatus status)
        {
            if (status.Relationship.Value <= 0)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "หย่าร้าง (ความสัมพันธ์เป็น 0)");
            else if (status.Stress.Value >= 100)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "ความเครียดกลืนกิน (Stress 100)");
        }
    }
}