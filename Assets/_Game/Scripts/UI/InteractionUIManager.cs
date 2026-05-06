using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Systems;
using Managers;
using Unity.Collections;
using Unity.Netcode;
using System.Collections.Generic;

namespace UI
{
    [System.Serializable]
    public struct LocationPanelMapping
    {
        [Tooltip("ใส่ LocationId ให้ตรงกับ Config เช่น LOC_Beach, LOC_ShoppingMall")]
        public string LocationId;
        [Tooltip("ลาก GameObject ของหน้าต่างสถานที่นั้นๆ มาใส่")]
        public GameObject PanelObject;
    }
    
    /// <summary>
    /// ตัวจัดการ UI แบบใหม่ (Sub-Panel System + Player Stats Sidebar)
    /// </summary>
    public class InteractionUIManager : MonoBehaviour
    {
        public static InteractionUIManager Instance { get; private set; }

        [Header("Main Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Location Sub-Panels")]
        [SerializeField] private List<LocationPanelMapping> locationPanels;

        [Header("Player Stats Sidebar")]
        [SerializeField] private GameObject sidebarRoot;
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI relationshipText;
        [SerializeField] private TextMeshProUGUI stressText;
        [SerializeField] private TextMeshProUGUI moneyText;
        [SerializeField] private Sprite[] avatarSprites;

        private PlayerActionHandler _currentActionHandler;
        private PlayerStatus _currentStatus;
        private string _lastBoundLocationId = "";
        
        #region Lifecycle
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (TurnManager.Instance != null)
            {
                // Subscribe Event
                TurnManager.Instance.currentInteractionLocationId.OnValueChanged += OnLocationChanged;
                TurnManager.Instance.isInteractionOpen.OnValueChanged += OnInteractionStateChanged;
                TurnManager.Instance.activeActorNetworkId.OnValueChanged += OnActiveActorChanged;
            }
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                // Unsubscribe Event
                TurnManager.Instance.currentInteractionLocationId.OnValueChanged -= OnLocationChanged;
                TurnManager.Instance.isInteractionOpen.OnValueChanged -= OnInteractionStateChanged;
                TurnManager.Instance.activeActorNetworkId.OnValueChanged -= OnActiveActorChanged;
            }
            UnbindPlayerStatus();
        }
        #endregion
        
        #region State Changes
        private void OnLocationChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
        {
            if (TurnManager.Instance.isInteractionOpen.Value) TryBindUI();
        }

        private void OnInteractionStateChanged(bool oldVal, bool newVal)
        {
            panelRoot.SetActive(newVal);
            if (newVal) TryBindUI();
            else ClearUI();
        }
        
        private void OnActiveActorChanged(ulong oldId, ulong newId)
        {
            if (panelRoot.activeInHierarchy) TryBindUI();
        }
        #endregion

        #region Presentation Logic
        private void TryBindUI()
        {
            string locId = TurnManager.Instance.currentInteractionLocationId.Value.ToString();
            ulong activeId = TurnManager.Instance.activeActorNetworkId.Value;
            
            if (string.IsNullOrEmpty(locId) || activeId == 0) return;
            
            _lastBoundLocationId = locId;
            
            // 1. เปิด Sub-Panel ให้ถูกสถานที่
            foreach (var mapping in locationPanels)
            {
                if (mapping.PanelObject != null)
                {
                    mapping.PanelObject.SetActive(mapping.LocationId == locId);
                }
            }

            // 2. ดึงข้อมูล Player และ Update Sidebar
            if (NetworkManager.Singleton != null && 
                NetworkManager.Singleton.SpawnManager != null && 
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeId, out var netObj))
            {
                _currentActionHandler = netObj.GetComponent<PlayerActionHandler>();
                
                UnbindPlayerStatus(); // ล้างของเก่าก่อน
                _currentStatus = netObj.GetComponent<PlayerStatus>();
                
                if (_currentStatus != null)
                {
                    _currentStatus.Relationship.OnValueChanged += OnStatsChanged;
                    _currentStatus.Stress.OnValueChanged += OnStatsChanged;
                    _currentStatus.PersonalMoney.OnValueChanged += OnMoneyChanged;
                    UpdateSidebarUI();
                }
            }
        }

        private void ClearUI()
        {
            _currentActionHandler = null;
            _lastBoundLocationId = ""; 

            // ซ่อนทุก Panel
            foreach (var mapping in locationPanels)
            {
                if (mapping.PanelObject != null) mapping.PanelObject.SetActive(false);
            }
            
            UnbindPlayerStatus();
        }

        private void UnbindPlayerStatus()
        {
            if (_currentStatus != null)
            {
                _currentStatus.Relationship.OnValueChanged -= OnStatsChanged;
                _currentStatus.Stress.OnValueChanged -= OnStatsChanged;
                _currentStatus.PersonalMoney.OnValueChanged -= OnMoneyChanged;
                _currentStatus = null;
            }
        }
        #endregion

        #region Sidebar Updates
        private void OnStatsChanged(int oldV, int newV) => UpdateSidebarUI();
        private void OnMoneyChanged(float oldV, float newV) => UpdateSidebarUI();

        private void UpdateSidebarUI()
        {
            if (_currentStatus == null) return;

            if (relationshipText != null) relationshipText.text = _currentStatus.Relationship.Value.ToString();
            if (stressText != null) stressText.text = _currentStatus.Stress.Value.ToString();
            if (moneyText != null) moneyText.text = _currentStatus.PersonalMoney.Value.ToString("N0");

            int avatarIdx = _currentStatus.avatarIndex.Value;
            if (avatarImage != null && avatarSprites != null && avatarIdx >= 0 && avatarIdx < avatarSprites.Length)
            {
                avatarImage.sprite = avatarSprites[avatarIdx];
            }
        }
        #endregion

        #region Public Methods for Buttons (ให้ UI Button ใน Unity เรียกใช้)
        /// <summary>
        /// ให้ปุ่ม Action ใน Unity ชี้มาที่ฟังก์ชันนี้ และใส่ Index ให้ตรงกับใน Config
        /// </summary>
        public void ExecuteAction(int actionIndex)
        {
            if (_currentActionHandler == null || string.IsNullOrEmpty(_lastBoundLocationId)) return;
            _currentActionHandler.RequestExecuteLocationActionServerRpc(_lastBoundLocationId, actionIndex);
        }

        /// <summary>
        /// ให้ปุ่ม Close (X) ใน Unity ชี้มาที่ฟังก์ชันนี้
        /// </summary>
        public void CloseUI()
        {
            if (_currentActionHandler == null) return;
            _currentActionHandler.RequestCloseUI_ServerRpc();
        }
        #endregion
    }
}