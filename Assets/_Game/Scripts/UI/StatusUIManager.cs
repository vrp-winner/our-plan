using UnityEngine;
using UnityEngine.UI;
using Systems;
using Managers;
using Unity.Netcode;

namespace UI
{
    /// <summary>
    /// ตัวจัดการการแสดงผล UI แบบ Ring (วงแหวนสถานะของผู้เล่น) (Global UI)
    /// ใช้ Reactive + Initial Sync Pattern เพื่อรองรับ UI ถูกเปิด/ปิดหลายรอบ (Subscribe -> Clear -> Sync)
    /// </summary>
    public class StatusUIManager : MonoBehaviour
    {
        #region UI References
        [Header("UI Rings")]
        [SerializeField] private Image relationshipRing;
        [SerializeField] private Image stressRing;

        private PlayerStatus _activeStatus;
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
                UpdateActivePlayerUI(TurnManager.Instance.activeActorNetworkId.Value);
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
            if (_activeStatus != null)
            {
                _activeStatus.Relationship.OnValueChanged -= OnRelationshipChanged;
                _activeStatus.Stress.OnValueChanged -= OnStressChanged;
                _activeStatus = null;
            }
        }
        #endregion

        #region UI Logic
        private void OnActiveActorChanged(ulong oldId, ulong newId)
        {
            UpdateActivePlayerUI(newId);
        }

        private void OnRelationshipChanged(int oldV, int newV) => SyncFill(relationshipRing, newV);
        private void OnStressChanged(int oldV, int newV) => SyncFill(stressRing, newV);

        private void UpdateActivePlayerUI(ulong activeActorId)
        {
            // STEP 1: ล้าง Event เก่า
            if (_activeStatus != null)
            {
                _activeStatus.Relationship.OnValueChanged -= OnRelationshipChanged;
                _activeStatus.Stress.OnValueChanged -= OnStressChanged;
            }

            // STEP 2: ล้าง reference หลังจาก unbind event
            _activeStatus = null;

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
                _activeStatus = netObj.GetComponent<PlayerStatus>();
                if (_activeStatus != null)
                {
                    _activeStatus.Relationship.OnValueChanged += OnRelationshipChanged;
                    _activeStatus.Stress.OnValueChanged += OnStressChanged;
                    
                    SyncFill(relationshipRing, _activeStatus.Relationship.Value);
                    SyncFill(stressRing, _activeStatus.Stress.Value);
                }
            }
        }

        /// <summary>
        /// ล้างค่าบนหน้าจอเมื่อไม่มีผู้เล่นที่ active
        /// </summary>
        private void ClearUI()
        {
            relationshipRing.fillAmount = 0;
            stressRing.fillAmount = 0;
        }

        private void SyncFill(Image img, int value) => img.fillAmount = value / 100f;
        #endregion
    }
}