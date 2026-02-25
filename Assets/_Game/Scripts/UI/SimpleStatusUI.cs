using UnityEngine;
using TMPro;
using Systems;
using Managers;
using Unity.Netcode;

namespace UI
{
    public class SimpleStatusUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI relText;
        public TextMeshProUGUI stressText;
        public TextMeshProUGUI moneyText;

        private PlayerStatus _currentStatus;

        private void Start()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnPlayerTurnChanged += UpdateTargetPlayer;
            }
        }

        private void UpdateTargetPlayer(ulong activePlayerId)
        {
            foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjects.Values)
            {
                if (netObj.OwnerClientId == activePlayerId)
                {
                    _currentStatus = netObj.GetComponent<PlayerStatus>();
                    if (_currentStatus != null)
                    {
                        RefreshUI();

                        _currentStatus.Relationship.OnValueChanged += (oldV, newV) => RefreshUI();
                        _currentStatus.Stress.OnValueChanged += (oldV, newV) => RefreshUI();
                        _currentStatus.PersonalMoney.OnValueChanged += (oldV, newV) => RefreshUI();
                    }
                    break;
                }
            }
        }

        private void RefreshUI()
        {
            if (_currentStatus == null) return;

            relText.text = $"Relationship: {_currentStatus.Relationship.Value}";
            stressText.text = $"Stress: {_currentStatus.Stress.Value}";
            moneyText.text = $"Personal Money: {_currentStatus.PersonalMoney.Value:N0}";
        }
    }
}