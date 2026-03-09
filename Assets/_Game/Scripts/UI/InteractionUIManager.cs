using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Configs;
using Systems;
using Managers;

namespace UI
{
    public class InteractionUIManager : MonoBehaviour
    {
        public static InteractionUIManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI locationNameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Transform actionButtonsContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button actionButtonPrefab;

        // เปลี่ยนประเภท Reference เป็น PlayerPointSystem
        private PlayerPointSystem _currentUser;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
            HideUI();
        }

        public void ShowLocation(LocationConfig config, PlayerPointSystem player)
        {
            _currentUser = player;

            // เช็คว่าผู้เล่นที่มาแตะตึก คือคนที่มีเทิร์นปัจจุบันหรือไม่
            bool isMyTurn = (player.OwnerClientId == TurnManager.Instance.CurrentActivePlayerId);
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

        private void OnActionClicked(LocationAction action)
        {
            var playerRef = _currentUser;
            if (playerRef == null || !playerRef.IsOwner) return;

            HideUI();
            playerRef.RequestCloseInteractionUI();

            if (action.ActionName == "Sleep")
            {
                playerRef.SleepAndEndTurn();
                return;
            }

            playerRef.UsePointsServerRpc(action.PointCost);

            if (playerRef.TryGetComponent(out PlayerStatus status))
            {
                status.ModifyStatsServerRpc(
                    action.RelationshipEffect,
                    action.StressEffect,
                    action.MoneyEffect
                );
            }
        }

        public void HideUI()
        {
            panelRoot.SetActive(false);
            _currentUser = null;
        }
    }
}