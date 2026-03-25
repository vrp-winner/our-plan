using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Configs;
using Systems;
using Managers;

namespace UI
{
    /// <summary>
    /// จัดการ UI สำหรับการ Interact กับสถานที่ (Presentation Layer)
    /// </summary>
    public class InteractionUIManager : MonoBehaviour
    {
        #region References (UI References)
        public static InteractionUIManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI locationNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Transform actionButtonsContainer;
        [SerializeField] private Button actionButtonPrefab;
        [SerializeField] private Button closeButton;

        // เปลี่ยนประเภท Reference เป็น PlayerPointSystem
        private PlayerPointSystem _currentUser;
        #endregion
        
        #region Lifecycle
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HideUI);
            }
            HideUI();
        }
        #endregion

        #region Presentation Logic (แสดงหน้าต่าง)
        public void ShowLocation(LocationConfig config, PlayerPointSystem player)
        {
            _currentUser = player;

            // เช็คว่าผู้เล่นที่มาแตะตึก คือคนที่มีเทิร์นปัจจุบันหรือไม่
            bool isMyTurn = (player.NetworkObjectId == TurnManager.Instance.activeActorNetworkId.Value);
            bool isOwner = player.IsOwner; // เป็นตัวละครของเราเองไหม

            locationNameText.text = config.LocationName;

            // ถ้าไม่ใช่ตาของเรา ให้แสดงข้อความบอก
            descriptionText.text = isMyTurn ? config.Description : "It's not your turn yet...";

            foreach (Transform child in actionButtonsContainer) Destroy(child.gameObject);

            foreach (var action in config.AvailableActions)
            {
                // ปุ่มจะกดได้ (Interactable) เฉพาะเมื่อเป็นเจ้าของตัวละครและเป็นตาของเขาเท่านั้น
                CreateActionButton(action, isOwner && isMyTurn);
            }

            panelRoot.SetActive(true);
        }

        public void HideUI()
        {
            panelRoot.SetActive(false);
            _currentUser = null;
        }
        
        private void CreateActionButton(LocationAction action, bool isInteractive)
        {
            Button btn = Instantiate(actionButtonPrefab, actionButtonsContainer);
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();

            if (btnText != null) btnText.text = $"{action.ActionName} (-{action.PointCost * 6}s)";

            btn.interactable = isInteractive;

            if (isInteractive)
            {
                btn.onClick.AddListener(() => OnActionClicked(action));
            }
        }
        #endregion

        #region Input Execution (จัดการปุ่ม)
        /// <summary>
        /// เมื่อคลิก Action ส่งคำสั่งไปที่ Actor
        /// </summary>
        private void OnActionClicked(LocationAction action)
        {
            var playerRef = _currentUser;
            
            // STEP 1: ปิดหน้าจอ UI ลงทันทีแบบ Local (ไม่ต้องรอ Server ตอบกลับ)
            HideUI();
            
            // STEP 2: ส่งคำสั่งต่อไปให้ Actor หากอ้างอิงยังอยู่
            if (playerRef == null || !playerRef.IsOwner) return;

            // STEP 3: ประมวลผล Action หรือ Sleep
            if (action.ActionName == "Sleep")
            {
                playerRef.SleepAndEndTurn();
                return;
            }
            
            playerRef.RequestAction(action.PointCost);

            if (playerRef.TryGetComponent(out PlayerStatus status))
            {
                status.ModifyStatsServerRpc(
                    action.RelationshipEffect,
                    action.StressEffect,
                    action.MoneyEffect
                );
            }
        }
        #endregion
    }
}