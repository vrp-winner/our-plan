using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Configs;
using Systems;
using Managers;
using Unity.Collections;
using Unity.Netcode;

namespace UI
{
    /// <summary>
    /// ตัวจัดการ UI สำหรับการ Interact กับสถานที่
    /// ใช้ Reactive + Initial Sync Pattern เพื่อรองรับ UI ถูกเปิด/ปิดหลายรอบ (Subscribe -> Clear -> Sync)
    /// </summary>
    public class InteractionUIManager : MonoBehaviour
    {
        #region UI References
        public static InteractionUIManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI locationNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Transform actionButtonsContainer;
        [SerializeField] private Button actionButtonPrefab;
        [SerializeField] private Button closeButton;

        private PlayerActionHandler _currentActionHandler;
        private string _lastBoundLocationId = "";
        private bool _forceRefresh = false; 
        #endregion
        
        #region Lifecycle
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            // ผูก Close Button Listener ครั้งเดียว
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (TurnManager.Instance != null)
            {
                // 1. Subscribe Event
                TurnManager.Instance.currentInteractionLocationId.OnValueChanged += OnLocationChanged;
                TurnManager.Instance.isInteractionOpen.OnValueChanged += OnInteractionStateChanged;
                
                // 2. Clear UI
                ClearUI();
                
                // 3. Initial Sync
                OnInteractionStateChanged(false, TurnManager.Instance.isInteractionOpen.Value);
            }
        }

        private void OnDisable()
        {
            if (TurnManager.Instance != null)
            {
                // Unsubscribe Event
                TurnManager.Instance.currentInteractionLocationId.OnValueChanged -= OnLocationChanged;
                TurnManager.Instance.isInteractionOpen.OnValueChanged -= OnInteractionStateChanged;
            }
        }
        #endregion

        #region Presentation Logic
        private void OnLocationChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
        {
            _forceRefresh = true; 

            if (TurnManager.Instance.isInteractionOpen.Value)
            {
                TryBindUI();
            }
        }

        private void OnInteractionStateChanged(bool oldVal, bool newVal)
        {
            panelRoot.SetActive(newVal);

            if (newVal)
            {
                TryBindUI(); 
            }
            else
            {
                ClearUI();   
            }
        }

        private void TryBindUI()
        {
            string locId = TurnManager.Instance.currentInteractionLocationId.Value.ToString();

            if (string.IsNullOrEmpty(locId)) return; 

            // Skip bind หากเป็น Location เดิมและไม่บังคับ refresh
            if (!_forceRefresh && _lastBoundLocationId == locId) return;

            _forceRefresh = false;
            _lastBoundLocationId = locId; 
            BindUIData(locId);
        }

        private void ClearUI()
        {
            _currentActionHandler = null;
            _lastBoundLocationId = ""; 
            _forceRefresh = false;

            locationNameText.text = "";
            descriptionText.text = "";

            foreach (Transform child in actionButtonsContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void BindUIData(string serverLocationId)
        {
            ulong activeId = TurnManager.Instance.activeActorNetworkId.Value;
            if (activeId == 0) return;

            var loc = LocationRegistry.GetLocation(serverLocationId);
            if (loc == null) return;

            if (NetworkManager.Singleton != null && 
                NetworkManager.Singleton.SpawnManager != null && 
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeId, out var netObj))
            {
                _currentActionHandler = netObj.GetComponent<PlayerActionHandler>();
                
                if (_currentActionHandler == null)
                {
                    Debug.LogError($"[UI] Missing PlayerActionHandler on Actor {activeId}");
                    return;
                }

                bool isMyTurn = (netObj.OwnerClientId == NetworkManager.Singleton.LocalClientId);

                locationNameText.text = loc.Config.LocationName;
                descriptionText.text = isMyTurn ? loc.Config.Description : "ผู้เล่นอื่นกำลังตัดสินใจ...";

                foreach (Transform child in actionButtonsContainer) Destroy(child.gameObject);

                for (int i = 0; i < loc.Config.AvailableActions.Count; i++)
                {
                    CreateActionButton(loc.Config.AvailableActions[i], isMyTurn, loc.Config.LocationId, i);
                }

                closeButton.interactable = isMyTurn; 
            }
        }
        
        private void CreateActionButton(LocationAction action, bool isInteractive, string locId, int actionIndex)
        {
            Button btn = Instantiate(actionButtonPrefab, actionButtonsContainer);
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();

            if (btnText != null) 
            {
                string costText = action.IsConsumeAllPoints ? "ALL" : $"-{action.PointCost * 6}s";
                btnText.text = $"{action.ActionName} ({costText})";
            }

            btn.interactable = isInteractive;

            if (isInteractive)
            {
                btn.onClick.AddListener(() => OnActionClicked(locId, actionIndex));
            }
        }
        #endregion

        #region Input Execution
        private void OnCloseButtonClicked()
        {
            if (TurnManager.Instance == null || !TurnManager.Instance.isInteractionOpen.Value) return;
            if (_currentActionHandler == null) return;

            ulong activeId = TurnManager.Instance.activeActorNetworkId.Value;
            if (activeId == 0) return;

            _currentActionHandler.RequestCloseUI_ServerRpc();
        }

        private void OnActionClicked(string locId, int actionIndex)
        {
            if (_currentActionHandler == null) return;
            
            PlayerActionHandler handlerToExecute = _currentActionHandler;
            handlerToExecute.RequestExecuteLocationActionServerRpc(locId, actionIndex);
        }
        #endregion
    }
}