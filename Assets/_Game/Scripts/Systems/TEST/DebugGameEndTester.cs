using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem; 
using Managers;
using Systems;

namespace DebugTools
{
    /// <summary>
    /// สคริปต์สำหรับเสกฉากจบเกมแบบสุ่ม เพื่อทดสอบ UI
    /// (ทำงานเฉพาะฝั่ง Server/Host เท่านั้น)
    /// </summary>
    public class DebugGameEndTester : NetworkBehaviour
    {
        private void Update()
        {
            if (!IsServer) return;

            if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
            {
                TriggerRandomGameOver();
            }
        }

        public void TriggerRandomGameOver()
        {



            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.currentObjective.Value = (GameObjective)Random.Range(0, 4);
            }

            PlayerStatus[] allPlayers = FindObjectsByType<PlayerStatus>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                p.PersonalMoney.Value = Random.Range(500f, 1500000f);
                p.Stress.Value = Random.Range(0, 100);
                p.Relationship.Value = Random.Range(0, 100);

                p.IsMarried.Value = Random.value > 0.5f;
                p.HasHouse.Value = Random.value > 0.5f;
                p.HasCar.Value = Random.value > 0.5f;
            }

            bool isWin = Random.value > 0.5f;
            string reason = isWin ? "(Debug Win)" : "(Debug Lose)";

            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.NotifyEndGameRpc(NetworkManager.Singleton.LocalClientId, isWin, reason);
            }
        }
    }
}