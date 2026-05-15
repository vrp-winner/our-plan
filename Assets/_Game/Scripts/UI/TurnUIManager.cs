using UnityEngine;
using TMPro;
using Managers;
using Systems;

namespace UI
{
    /// <summary>
    /// ตัวจัดการ UI ในการแสดงสถานะ Turn/Month และเวลา
    /// ใช้ Reactive + Initial Sync Pattern เพื่อรองรับ UI ถูกเปิด/ปิดหลายรอบ (Subscribe -> Clear -> Sync)
    /// </summary>
    public class TurnUIManager : MonoBehaviour
    {
        #region UI References
        [Header("References")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private TextMeshProUGUI objectiveText;

        private bool _hasReceivedFirstSync = false;
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (TurnManager.Instance != null)
            {
                // 1. Subscribe Event
                TurnManager.Instance.OnRoundChanged += UpdateTurnUI;
                TurnManager.Instance.currentTime.OnValueChanged += OnTimeChanged;
                TurnManager.Instance.currentObjective.OnValueChanged += OnObjectiveChanged;

                // 2. Clear UI
                ClearInitialUI();

                // 3. Initial Sync
                UpdateTurnUI(TurnManager.Instance.currentRound.Value);
                OnTimeChanged(0, TurnManager.Instance.currentTime.Value);
                OnObjectiveChanged(TurnManager.Instance.currentObjective.Value, TurnManager.Instance.currentObjective.Value);
            }
        }
        
        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                // Unsubscribe Event
                TurnManager.Instance.OnRoundChanged -= UpdateTurnUI;
                TurnManager.Instance.currentTime.OnValueChanged -= OnTimeChanged;
                TurnManager.Instance.currentObjective.OnValueChanged -= OnObjectiveChanged;
            }
        }
        #endregion

        #region Update Logic
        private void ClearInitialUI()
        {
            turnText.text = "";
            timeText.text = "";
        }

        private void OnTimeChanged(int oldVal, int newVal)
        {
            if (!_hasReceivedFirstSync && newVal <= 0) return;

            _hasReceivedFirstSync = true;
            UpdateTimeUI(newVal);
        }

        private void UpdateTimeUI(int totalSeconds)
        {
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;

            timeText.text = $"{minutes:00}:{seconds:00}";
        }
        
        private void UpdateTurnUI(int turn) 
        {
            if (turn <= 0) return;
            turnText.text = $"{turn}";
        }
        #endregion
        
        private void OnObjectiveChanged(GameObjective oldObj, GameObjective newObj)
        {
            if (objectiveText == null) return;

            switch (newObj)
            {
                case GameObjective.GetMarried:
                    objectiveText.text = "Objective:\nGet married";
                    break;

                case GameObjective.BuyHouseAndCar:
                    objectiveText.text = "Objective:\nBuy a house & a car";
                    break;

                case GameObjective.RetireWealthy:
                    objectiveText.text = "Objective:\nRetired with $800,000";
                    break;

                case GameObjective.DateEveryPlaces:
                    objectiveText.text = "Objective:\nDating every places";
                    break;
            }
        }
    }
}