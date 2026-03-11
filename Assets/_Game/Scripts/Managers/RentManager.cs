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

            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject.TryGetComponent(out PlayerStatus status))
                {
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && TurnManager.Instance != null)
                TurnManager.Instance.OnCycleChanged -= HandleCycleEnd;
        }
    }
}