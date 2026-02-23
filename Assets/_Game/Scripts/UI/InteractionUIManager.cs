using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Configs;
using Systems;

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
            bool isMyCharacter = player.IsOwner;

            locationNameText.text = config.LocationName;
            descriptionText.text = isMyCharacter ? config.Description : $"[Player {player.OwnerClientId}] is visiting here...";
            closeButton.gameObject.SetActive(isMyCharacter);

            foreach (Transform child in actionButtonsContainer) Destroy(child.gameObject);

            foreach (var action in config.AvailableActions)
            {
                CreateActionButton(action, isMyCharacter);
            }

            panelRoot.SetActive(true);
        }

        private void CreateActionButton(LocationAction action, bool isInteractive)
        {
            Button btn = Instantiate(actionButtonPrefab, actionButtonsContainer);
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();

            // แสดงผลเป็นวินาทีเพื่อให้ผู้เล่นรู้สึกเหมือนใช้เวลา (แต้ม * 6)
            // สมมติว่าดึงค่า SecondsPerPoint มาใช้ หรือเขียนเลข 6 ไปก่อนเพื่อทดสอบ
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
            HideUI();

            if (playerRef != null && playerRef.IsOwner)
            {
                playerRef.RequestCloseInteractionUI();

                if (action.ActionName == "Sleep") playerRef.SleepAndEndTurn();
                else playerRef.RequestAction(action.PointCost); // ส่งค่าแต้มไปหัก
            }
        }

        public void HideUI()
        {
            panelRoot.SetActive(false);
            _currentUser = null;
        }
    }
}