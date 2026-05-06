using UnityEngine;
using UnityEngine.UI;
using Systems;
using Unity.Netcode;
using Managers;
using Unity.Collections;

namespace UI
{
    /// <summary>
    /// ตัวช่วยสำหรับปุ่ม Action 
    /// ทำหน้าที่อัปเดตสถานะปุ่ม (Interactable) ให้อัตโนมัติ (เช่น เป็นตาของตนเองหรือไม่, เงินพอมั้ย, เคยกด Once Per Turn ไปหรือยัง)
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ActionButtonVisual : MonoBehaviour
    {
        [Tooltip("ใส่ Index ของปุ่มนี้ให้ตรงกับใน Location Config (ค่าเดียวกับที่ใส่ใน OnClick)")]
        public int actionIndex;
        private Button _btn;

        private void Awake()
        {
            _btn = GetComponent<Button>();
        }

        private void Update()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.SpawnManager == null) return;
            if (TurnManager.Instance == null || InteractionUIManager.Instance == null) return;
            
            ulong activeId = TurnManager.Instance.activeActorNetworkId.Value;
            
            // ถ้าไม่มีใครกำลังเดิน หรือเกิดข้อผิดพลาด ให้ปิดปุ่มไว้ก่อน
            if (activeId == 0) 
            {
                _btn.interactable = false;
                return;
            }

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeId, out var netObj)) return;
            if (netObj == null || !netObj.IsSpawned) return; 

            var actionHandler = netObj.GetComponent<PlayerActionHandler>();
            var pointSystem = netObj.GetComponent<PlayerPointSystem>();
            var status = netObj.GetComponent<PlayerStatus>();

            if (actionHandler != null && pointSystem != null && status != null)
            {
                string locId = TurnManager.Instance.currentInteractionLocationId.Value.ToString();
                if (string.IsNullOrEmpty(locId)) return;

                bool isMyTurn = netObj.OwnerClientId == NetworkManager.Singleton.LocalClientId;
                FixedString32Bytes actionKey = new FixedString32Bytes($"{locId}_{actionIndex}");
                bool isUsed = actionHandler.UsedOncePerTurnActions.Contains(actionKey);

                // เช็คพิเศษ: ถ้าเป็นปุ่มซื้อรถ และซื้อไปแล้ว ให้ปิดปุ่มถาวร
                bool isCarAlreadyBought = false;
                if (locId == "LOC_ShoppingMall" && EconomyManager.Instance != null)
                {
                    var config = LocationRegistry.GetLocation(locId);
                    if (config != null && config.Config.AvailableActions.Count > actionIndex)
                    {
                        if (config.Config.AvailableActions[actionIndex].ActionName == "Buy (Car)")
                        {
                            isCarAlreadyBought = EconomyManager.Instance.isCarBought.Value;
                        }
                    }
                }

                bool hasEnoughMoney = true;
                var locConfig = LocationRegistry.GetLocation(locId);
                if (locConfig != null && locConfig.Config.AvailableActions.Count > actionIndex)
                {
                    float requiredMoney = locConfig.Config.AvailableActions[actionIndex].MoneyEffect;
                    if (requiredMoney < 0) 
                    {
                        hasEnoughMoney = status.PersonalMoney.Value >= Mathf.Abs(requiredMoney);
                    }
                }

                _btn.interactable = isMyTurn && !isUsed && !isCarAlreadyBought && hasEnoughMoney;
            }
        }
    }
}