using UnityEngine;
using Systems;

namespace Managers
{
    public class StatusManager : SingletonNetwork<StatusManager>
    {
        public void ProcessHomeEntry(PlayerStatus status)
        {
            if (!IsServer) return;
            int currentRel = status.Relationship.Value;
            int relChange = 0, stressChange = 0;

            if (currentRel >= 80) { relChange = 0; stressChange = 0; }
            else if (currentRel >= 60) { relChange = -5; stressChange = 0; }
            else if (currentRel >= 40) { relChange = -10; stressChange = 5; }
            else if (currentRel >= 20) { relChange = -15; stressChange = 10; }
            else { relChange = -20; stressChange = 15; }

            status.ModifyStatsServerRpc(relChange, stressChange);
            CheckFailConditions(status);
        }

        public void EvaluateEndOfMonth(PlayerStatus status)
        {
            if (!IsServer) return;
            float changePercent = status.GetMonthlyMoneyChange();
            int rel = 0, stress = 0;

            if (changePercent <= -0.25f) { rel = -10; stress = 5; }
            else if (changePercent >= 0.25f) { rel = 10; stress = -10; }

            status.ModifyStatsServerRpc(rel, stress);
            status.ResetMonthTracker();
        }

        private void CheckFailConditions(PlayerStatus status)
        {
            if (status.Relationship.Value <= 0)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "หย่าร้าง");
            else if (status.Stress.Value >= 100)
                TurnManager.Instance.NotifyEndGameRpc(status.OwnerClientId, false, "ความเครียดสะสม");
        }
    }
}