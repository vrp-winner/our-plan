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

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(activeId, out var netObj))
            {
                var actionHandler = netObj.GetComponent<PlayerActionHandler>();
                var pointSystem = netObj.GetComponent<PlayerPointSystem>();
                var status = netObj.GetComponent<PlayerStatus>();

                if (actionHandler != null && pointSystem != null && status != null)
                {
                    string locId = TurnManager.Instance.currentInteractionLocationId.Value.ToString();
                    if (string.IsNullOrEmpty(locId)) return;
                    
                    // 1. เช็คว่าเป็นตาของตนเองหรือไม่? (ถ้าใช่ ถึงจะกดได้)
                    bool isMyTurn = netObj.OwnerClientId == NetworkManager.Singleton.LocalClientId;
                    
                    // 2. เช็คว่า Action นี้ถูกตั้งเป็น Once Per Turn และเคยกดไปแล้วในตานี้หรือเปล่า? (ดึง Location + Index)
                    FixedString32Bytes actionKey = new FixedString32Bytes($"{locId}_{actionIndex}");
                    bool isUsed = actionHandler.UsedOncePerTurnActions.Contains(actionKey);
                    
                    // เช็คว่ามีเงินพอกับการจ่ายมั้ย?
                    bool hasEnoughMoney = true;
                    var locConfig = LocationRegistry.GetLocation(locId);
                    if (locConfig != null && locConfig.Config.AvailableActions.Count > actionIndex)
                    {
                        float requiredMoney = locConfig.Config.AvailableActions[actionIndex].MoneyEffect;
                        if (requiredMoney < 0) // ถ้า Effect ติดลบ (คือต้องจ่ายเงิน)
                        {
                            hasEnoughMoney = status.PersonalMoney.Value >= Mathf.Abs(requiredMoney);
                        }
                    }
                    
                    // 3. ปรับสถานะปุ่ม (ต้องเป็นตาเรา + ยังไม่เคยกดในตานี้ + มีเงินพอ)
                    // (ในอนาคตถ้าอยากให้เช็ค Point Cost ด้วยว่าพอไหม สามารถมาเพิ่มเงื่อนไขตรงนี้ได้)
                    _btn.interactable = isMyTurn && !isUsed && hasEnoughMoney;
                }
            }
        }
    }
}