using UnityEngine;
using TMPro;
using Systems;
using Managers;
using Unity.Netcode;

namespace UI
{
    /// <summary>
    /// แสดงสถานะพื้นฐานของผู้เล่นปัจจุบัน (Global UI)
    /// ใช้ Reactive + Initial Sync Pattern เพื่อรองรับ UI ถูกเปิด/ปิดหลายรอบ (Subscribe -> Clear -> Sync)
    /// </summary>
    public class SimpleStatusUI : MonoBehaviour
    {
        #region UI References
        [Header("UI References")]
        public TextMeshProUGUI relText;
        public TextMeshProUGUI stressText;
        public TextMeshProUGUI moneyText;

        private PlayerStatus _currentStatus;
        #endregion

        #region Lifecycle
        private void OnEnable()
        {
            if (TurnManager.Instance != null)
            {
                // 1. Subscribe Event
                TurnManager.Instance.activeActorNetworkId.OnValueChanged += OnActiveActorChanged;
                
                // 2. Clear UI
                ClearUI();
                
                // 3. Initial Sync
                UpdateTargetPlayer(TurnManager.Instance.activeActorNetworkId.Value);
            }
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                // Unsubscribe Event
                TurnManager.Instance.activeActorNetworkId.OnValueChanged -= OnActiveActorChanged;
            }
            
            // Clear Reference ตอน UI ถูกปิด
            if (_currentStatus != null)
            {
                _currentStatus.Relationship.OnValueChanged -= OnStatsChanged;
                _currentStatus.Stress.OnValueChanged -= OnStatsChanged;
                _currentStatus.PersonalMoney.OnValueChanged -= OnMoneyChanged;
                _currentStatus = null;
            }
        }
        #endregion

        #region Update Logic
        private void OnActiveActorChanged(ulong oldId, ulong newId)
        {
            UpdateTargetPlayer(newId);
        }

        private void OnStatsChanged(int oldV, int newV) => RefreshUI();
        private void OnMoneyChanged(float oldV, float newV) => RefreshUI();

        private void UpdateTargetPlayer(ulong activeActorId)
        {
            // STEP 1: ล้าง Event เก่า
            if (_currentStatus != null)
            {
                _currentStatus.Relationship.OnValueChanged -= OnStatsChanged;
                _currentStatus.Stress.OnValueChanged -= OnStatsChanged;
                _currentStatus.PersonalMoney.OnValueChanged -= OnMoneyChanged;
            }

            // STEP 2: ล้าง reference หลัง unbind event
            _currentStatus = null;

            // STEP 3: ไม่มี active actor -> Clear UI
            if (activeActorId == 0) 
            {
                ClearUI();
                return;
            }

            if (NetworkManager.Singleton != null && 
                NetworkManager.Singleton.SpawnManager != null && 
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeActorId, out var netObj))
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

        /// <summary>
        /// ล้างค่าบนหน้าจอเมื่อไม่มีผู้เล่นที่ active
        /// </summary>
        private void ClearUI()
        {
            relText.text = "Relationship: -";
            stressText.text = "Stress: -";
            moneyText.text = "Personal Money: -";
        }
        #endregion
    }
}