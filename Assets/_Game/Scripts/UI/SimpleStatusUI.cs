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
        private void OnStatsChanged(int oldV, int newV) => RefreshUI();
        private void OnMoneyChanged(float oldV, float newV) => RefreshUI();
        private void UpdateTargetPlayer(ulong activeActorId)
        {
            if (_currentStatus != null)
            {
                _currentStatus.Relationship.OnValueChanged -= OnStatsChanged;
                _currentStatus.Stress.OnValueChanged -= OnStatsChanged;
                _currentStatus.PersonalMoney.OnValueChanged -= OnMoneyChanged;
            }

            // เลิกใช้ foreach แล้วใช้ TryGetValue ดึงตัวละครออกมาตรงๆ
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeActorId, out var netObj))
            {
                _currentStatus = netObj.GetComponent<PlayerStatus>();
                if (_currentStatus != null)
                {
                    _currentStatus.Relationship.OnValueChanged += OnStatsChanged;
                    _currentStatus.Stress.OnValueChanged += OnStatsChanged;
                    _currentStatus.PersonalMoney.OnValueChanged += OnMoneyChanged;
                    RefreshUI();
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