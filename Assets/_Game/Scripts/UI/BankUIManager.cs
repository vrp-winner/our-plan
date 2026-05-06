using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Systems;
using Managers;
using Unity.Netcode;

namespace UI
{
    public class BankUIManager : MonoBehaviour
    {
        [Header("UI Texts")]
        [Tooltip("Text แสดงจำนวนเดือนที่ค้างค่าเช่า")]
        public TextMeshProUGUI overdueMonthsText;
        [Tooltip("Text แสดงจำนวนเงินค่าเช่าที่ต้องจ่าย (เช่น 9K)")]
        public TextMeshProUGUI payRentAmountText;
        [Tooltip("Text แสดงเงินกองกลางในธนาคาร (มีไว้เพื่อให้ผู้เล่นดูได้)")]
        public TextMeshProUGUI jointMoneyText;

        [Header("Deposit Buttons (เรียงตาม 5k, 10k, 50k, 100k, 500k)")]
        public Button[] depositButtons;

        [Header("Withdraw Buttons (เรียงตาม 5k, 10k, 50k, 100k, 500k)")]
        public Button[] withdrawButtons;

        [Header("Special Buttons")]
        public Button payRentButton;
        public Button buyHouseButton;
        public Button closeButton;

        // ค่าเงินที่จับคู่กับ Index ของปุ่มฝาก/ถอน
        private float[] _amounts = { 5000f, 10000f, 50000f, 100000f, 500000f };

        private void Start()
        {
            // ผูก Event ให้ปุ่มทั้งหมด
            for (int i = 0; i < depositButtons.Length; i++)
            {
                int index = i; 
                if (depositButtons[i] != null)
                    depositButtons[i].onClick.AddListener(() => SendBankRequest(PlayerActionHandler.BankActionType.Deposit, _amounts[index]));
            }

            for (int i = 0; i < withdrawButtons.Length; i++)
            {
                int index = i;
                if (withdrawButtons[i] != null)
                    withdrawButtons[i].onClick.AddListener(() => SendBankRequest(PlayerActionHandler.BankActionType.Withdraw, _amounts[index]));
            }

            if (payRentButton != null)
                payRentButton.onClick.AddListener(() => SendBankRequest(PlayerActionHandler.BankActionType.PayRent, 0f));

            if (buyHouseButton != null)
                buyHouseButton.onClick.AddListener(() => SendBankRequest(PlayerActionHandler.BankActionType.BuyHouse, 500000f));
                
            if (closeButton != null)
                closeButton.onClick.AddListener(() => {
                    if (InteractionUIManager.Instance != null) InteractionUIManager.Instance.CloseUI();
                });
        }

        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.SpawnManager == null) return;
            if (EconomyManager.Instance == null || TurnManager.Instance == null) return;

            // 1. อัปเดตตัวหนังสือ
            if (overdueMonthsText != null) overdueMonthsText.text = EconomyManager.Instance.RentLevel.Value.ToString();
            
            float currentRent = EconomyManager.Instance.GetCurrentRentPrice();
            if (payRentAmountText != null) payRentAmountText.text = $"{(currentRent / 1000f)}K";
            
            if (jointMoneyText != null) jointMoneyText.text = $"Joint Money: {EconomyManager.Instance.JointMoney.Value:N0}";

            // 2. เช็คว่าตานี้เป็นของตนเองหรือไม่
            ulong activeId = TurnManager.Instance.activeActorNetworkId.Value;
            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeId, out var activeActorObj)) return;

            bool isMyPlayer = activeActorObj.OwnerClientId == NetworkManager.Singleton.LocalClientId;
            
            var pointSystem = activeActorObj.GetComponent<PlayerPointSystem>();
            var status = activeActorObj.GetComponent<PlayerStatus>();
            
            if (pointSystem == null || status == null) return;

            bool hasPoints = pointSystem.PointsRemaining >= 2;
            float personalMoney = status.PersonalMoney.Value;
            float jointMoney = EconomyManager.Instance.JointMoney.Value;
            int rentLevel = EconomyManager.Instance.RentLevel.Value;

            // 3. จัดการเปิด/ปิดปุ่มอัตโนมัติ ตามเงินและ Point ที่เหลืออยู่
            for (int i = 0; i < depositButtons.Length; i++)
            {
                if (depositButtons[i] != null && i < _amounts.Length)
                    depositButtons[i].interactable = isMyPlayer && hasPoints && (personalMoney >= _amounts[i]);
            }

            for (int i = 0; i < withdrawButtons.Length; i++)
            {
                if (withdrawButtons[i] != null && i < _amounts.Length)
                    withdrawButtons[i].interactable = isMyPlayer && hasPoints && (jointMoney >= _amounts[i]);
            }

            if (payRentButton != null)
                payRentButton.interactable = isMyPlayer && hasPoints && (rentLevel > 0) && (jointMoney >= currentRent);

            if (buyHouseButton != null) 
                buyHouseButton.interactable = isMyPlayer && hasPoints && !EconomyManager.Instance.IsHouseBought.Value && (jointMoney >= 500000f);

        }

        private void SendBankRequest(PlayerActionHandler.BankActionType type, float amount)
        {
            ulong activeId = TurnManager.Instance.activeActorNetworkId.Value;
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeId, out var activeActorObj))
            {
                var handler = activeActorObj.GetComponent<PlayerActionHandler>();
                // ตรวจสอบว่าเป็นการกดจากเจ้าของตัวละครจริงๆ
                if (handler != null && activeActorObj.OwnerClientId == NetworkManager.Singleton.LocalClientId)
                {
                    handler.RequestBankActionServerRpc(type, amount);
                }
            }
        }
    }
}