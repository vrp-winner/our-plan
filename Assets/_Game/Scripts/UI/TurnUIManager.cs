using UnityEngine;
using TMPro;
using Managers;
using Systems;
using Unity.Netcode;

namespace UI
{
    public class TurnUIManager : MonoBehaviour
    {
        #region ตัวแปร (UI References)
        [Header("References")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI cycleText;
        [SerializeField] private TextMeshProUGUI timeText;

        private PlayerPointSystem _activePlayerPointSystem;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.OnRoundChanged += UpdateTurnUI;
                TurnManager.Instance.OnCycleChanged += UpdateCycleUI;
                TurnManager.Instance.ActiveActorNetworkId.OnValueChanged += (oldId, newId) => OnPlayerTurnChanged(newId);
                OnPlayerTurnChanged(TurnManager.Instance.ActiveActorNetworkId.Value);
            }

        }
        #endregion

        #region เวลาและเทิร์น (Time & Turn Logic)
        private void OnPlayerTurnChanged(ulong activeActorId)
        {
            if (_activePlayerPointSystem != null)
            {
                _activePlayerPointSystem.OnVirtualTimeChanged -= UpdateTimeUI;
            }

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeActorId, out var netObj))
            {
                _activePlayerPointSystem = netObj.GetComponent<PlayerPointSystem>();
                if (_activePlayerPointSystem != null)
                {
                    _activePlayerPointSystem.OnVirtualTimeChanged += UpdateTimeUI;
                }
                _activePlayerPointSystem.RefreshTimeUI();
                _activePlayerPointSystem.RefreshTimeUI();
            }
        }

        private void UpdateTimeUI(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timeText.text = $"Time: {minutes:00}:{seconds:00}";
            timeText.color = totalSeconds <= 20 ? Color.red : Color.white;
        }

        private void UpdateTurnUI(int turn) => turnText.text = turn > 0 ? $"Round: {turn}" : "";
        private void UpdateCycleUI(int cycle) => cycleText.text = cycle > 0 ? $"Month: {cycle}" : "";
        #endregion
    }
}